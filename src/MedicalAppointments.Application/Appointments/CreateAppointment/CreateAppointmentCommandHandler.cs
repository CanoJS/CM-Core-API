using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Clock;
using MedicalAppointments.Application.Abstractions.Idempotency;
using MedicalAppointments.Application.Abstractions.Messaging;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Abstractions.Scheduling;
using MedicalAppointments.Application.Common;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Domain.Appointments;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.Appointments.CreateAppointment;

public sealed class CreateAppointmentCommandHandler(
    ICurrentUser currentUser,
    IClock clock,
    IClinicSchedule clinicSchedule,
    IDoctorRepository doctorRepository,
    IAppointmentRepository appointmentRepository,
    IIdempotencyStore idempotencyStore,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateAppointmentCommand, CreateAppointmentResponse>
{
    private const string Operation = "CreateAppointment";
    private const int MaxIdempotencyKeyLength = 200;

    // ASP.NET Core's own 201 for POST /appointments; kept as a literal rather than referencing
    // Microsoft.AspNetCore.Http.StatusCodes, which Application must not depend on.
    private const int CreatedStatusCode = 201;

    private static readonly TimeSpan IdempotencyRecordLifetime = TimeSpan.FromHours(24);

    public async Task<CreateAppointmentResponse> Handle(
        CreateAppointmentCommand command,
        CancellationToken cancellationToken)
    {
        if (currentUser.Role != UserRole.Patient)
        {
            throw new ForbiddenException("Only patients can book appointments.");
        }

        string? idempotencyKey = NormalizeIdempotencyKey(command.IdempotencyKey);
        string? requestHash = idempotencyKey is null ? null : ComputeRequestHash(command);

        if (idempotencyKey is not null)
        {
            IdempotencyRecord? existing = await idempotencyStore.FindAsync(
                currentUser.UserId,
                Operation,
                idempotencyKey,
                cancellationToken);
            if (existing is not null)
            {
                if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
                {
                    throw new ConflictException("This Idempotency-Key was already used with a different request.");
                }

                return DeserializeResponse(existing.ResponseBody);
            }
        }

        if (!clinicSchedule.IsBookableSlot(command.StartsAt))
        {
            throw new ArgumentException(
                "Appointments must use a 30-minute slot from Monday to Friday, 08:00 to 18:00 clinic time.");
        }

        if (!await doctorRepository.IsActiveAsync(command.DoctorId, cancellationToken))
        {
            throw new NotFoundException("The selected doctor does not exist or is inactive.");
        }

        if (await appointmentRepository.HasScheduledAppointmentAsync(
                command.DoctorId,
                command.StartsAt,
                excludeAppointmentId: null,
                cancellationToken))
        {
            throw new ConflictException("The selected time slot is no longer available.");
        }

        Appointment appointment = Appointment.Schedule(
            currentUser.UserId,
            command.DoctorId,
            command.StartsAt,
            command.Reason,
            clock.UtcNow);

        appointmentRepository.Add(appointment);

        if (idempotencyKey is null)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return BuildResponse(appointment);
        }

        return await SaveWithIdempotencyRecordAsync(
            appointment,
            idempotencyKey,
            requestHash!,
            cancellationToken);
    }

    private async Task<CreateAppointmentResponse> SaveWithIdempotencyRecordAsync(
        Appointment appointment,
        string idempotencyKey,
        string requestHash,
        CancellationToken cancellationToken)
    {
        // The idempotency record needs the appointment's final Version (xmin), which EF only
        // populates on the tracked entity after the INSERT executes. Both writes happen inside
        // one explicit transaction - not yet committed after the first SaveChangesAsync - so
        // either both land or neither does.
        await using IUnitOfWorkTransaction transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        CreateAppointmentResponse response = BuildResponse(appointment);

        try
        {
            idempotencyStore.Stage(
                currentUser.UserId,
                Operation,
                idempotencyKey,
                requestHash,
                CreatedStatusCode,
                JsonSerializer.Serialize(response),
                clock.UtcNow.Add(IdempotencyRecordLifetime));
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConflictException)
        {
            // Another request with the same key won the unique-constraint race and already
            // committed its own transaction; ours (including the appointment insert above)
            // rolls back on dispose below. Replay the winner's response instead of surfacing a
            // spurious error - Postgres only raises this once the winning insert has committed,
            // so a read here is guaranteed to see it.
            IdempotencyRecord? winner = await idempotencyStore.FindAsync(
                currentUser.UserId,
                Operation,
                idempotencyKey,
                cancellationToken);
            if (winner is not null && string.Equals(winner.RequestHash, requestHash, StringComparison.Ordinal))
            {
                return DeserializeResponse(winner.ResponseBody);
            }

            throw;
        }

        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    private static string? NormalizeIdempotencyKey(string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return null;
        }

        if (idempotencyKey.Length > MaxIdempotencyKeyLength)
        {
            throw new ArgumentException(
                $"Idempotency-Key cannot exceed {MaxIdempotencyKeyLength} characters.");
        }

        return idempotencyKey;
    }

    private static string ComputeRequestHash(CreateAppointmentCommand command)
    {
        string canonical = $"{command.DoctorId:D}|{command.StartsAt.UtcTicks}|{command.Reason.Trim()}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static CreateAppointmentResponse BuildResponse(Appointment appointment) =>
        new(
            appointment.Id,
            appointment.PatientId,
            appointment.DoctorId,
            appointment.StartsAt,
            appointment.EndsAt,
            appointment.Reason,
            appointment.Status.ToString().ToUpperInvariant(),
            ConcurrencyToken.ToToken(appointment.Version));

    private static CreateAppointmentResponse DeserializeResponse(string json) =>
        JsonSerializer.Deserialize<CreateAppointmentResponse>(json)
            ?? throw new InvalidOperationException("The stored idempotent response could not be read.");
}
