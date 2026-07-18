using MedicalAppointments.Application.Abstractions.Auditing;
using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Messaging;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Application.Common;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Domain.Appointments;
using MedicalAppointments.Domain.Doctors;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.Appointments.GetMyAppointments;

public sealed class GetMyAppointmentsQueryHandler(
    ICurrentUser currentUser,
    IDoctorRepository doctorRepository,
    IAppointmentReader appointmentReader,
    IMedicalNoteAuditLog medicalNoteAuditLog,
    TimeZoneInfo clinicTimeZone)
    : IQueryHandler<GetMyAppointmentsQuery, IReadOnlyList<AppointmentResponse>>
{
    // Inclusive on both ends, same convention as GetDoctorAvailabilityQueryHandler's 31-day cap.
    private const int MaxInclusiveDays = 366;

    public async Task<IReadOnlyList<AppointmentResponse>> Handle(
        GetMyAppointmentsQuery query,
        CancellationToken cancellationToken)
    {
        AppointmentStatus? status = ParseStatus(query.Status);
        (DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive) = ResolveRange(query.From, query.To);

        Guid? patientId = null;
        Guid? doctorId = null;

        switch (currentUser.Role)
        {
            case UserRole.Patient:
                patientId = currentUser.UserId;
                break;
            case UserRole.Doctor:
                Doctor doctor = await doctorRepository.GetByUserIdAsync(currentUser.UserId, cancellationToken)
                    ?? throw new NotFoundException("No doctor profile is associated with this account.");
                doctorId = doctor.Id;
                break;
            case UserRole.Admin:
                break;
        }

        IReadOnlyList<AppointmentListItem> items = await appointmentReader.GetAsync(
            patientId,
            doctorId,
            status,
            fromUtc,
            toUtcExclusive,
            cancellationToken);

        var responses = new List<AppointmentResponse>(items.Count);
        foreach (AppointmentListItem item in items)
        {
            AppointmentResponse response = AppointmentResponseMapper.ToResponse(item, currentUser.Role);
            if (response.MedicalNote is not null)
            {
                medicalNoteAuditLog.RecordRead(item.Id, currentUser.UserId);
            }

            responses.Add(response);
        }

        return responses;
    }

    private static AppointmentStatus? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        return Enum.TryParse(status, ignoreCase: true, out AppointmentStatus parsed)
            ? parsed
            : throw new ArgumentException("Status must be one of SCHEDULED, ATTENDED, CANCELLED.");
    }

    private (DateTimeOffset? FromUtc, DateTimeOffset? ToUtcExclusive) ResolveRange(DateOnly? from, DateOnly? to)
    {
        if (from is { } fromDate && to is { } toDate)
        {
            if (toDate < fromDate || toDate.DayNumber - fromDate.DayNumber > MaxInclusiveDays - 1)
            {
                throw new ArgumentException($"Date range must be between 1 and {MaxInclusiveDays} inclusive days.");
            }

            (DateTimeOffset utcFrom, DateTimeOffset utcToExclusive) =
                LocalDateRange.ToUtcBounds(fromDate, toDate, clinicTimeZone);
            return (utcFrom, utcToExclusive);
        }

        if (from is { } fromOnly)
        {
            (DateTimeOffset utcFrom, _) = LocalDateRange.ToUtcBounds(fromOnly, fromOnly, clinicTimeZone);
            return (utcFrom, null);
        }

        if (to is { } toOnly)
        {
            (_, DateTimeOffset utcToExclusive) = LocalDateRange.ToUtcBounds(toOnly, toOnly, clinicTimeZone);
            return (null, utcToExclusive);
        }

        return (null, null);
    }
}
