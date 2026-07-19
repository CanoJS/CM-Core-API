using System.Net.Mail;
using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Messaging;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Common;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Application.Specialties.GetSpecialties;
using MedicalAppointments.Domain.Doctors;
using MedicalAppointments.Domain.Specialties;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.Doctors.RegisterDoctor;

public sealed class RegisterDoctorCommandHandler(
    ICurrentUser currentUser,
    ISpecialtyRepository specialtyRepository,
    IUserProfileRepository userProfileRepository,
    IDoctorRepository doctorRepository,
    IAuthAdminService authAdminService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RegisterDoctorCommand, RegisterDoctorResponse>
{
    private static readonly TimeSpan CompensationTimeout = TimeSpan.FromSeconds(5);

    public async Task<RegisterDoctorResponse> Handle(
        RegisterDoctorCommand command,
        CancellationToken cancellationToken)
    {
        if (currentUser.Role != UserRole.Admin)
        {
            throw new ForbiddenException("Only administrators can register doctors.");
        }

        string firstName = ValidateName(command.FirstName, "First name");
        string lastName = ValidateName(command.LastName, "Last name");
        string email = ValidateEmail(command.Email);
        string temporaryPassword = ValidatePassword(command.TemporaryPassword);

        if (command.SpecialtyId == Guid.Empty)
        {
            throw new ArgumentException("Specialty is required.");
        }

        Specialty specialty = await specialtyRepository.GetByIdAsync(command.SpecialtyId, cancellationToken)
            ?? throw new NotFoundException("The specialty does not exist.");

        if (!specialty.Active)
        {
            throw new ConflictException("The specialty is inactive.");
        }

        if (await userProfileRepository.ExistsByEmailAsync(email, cancellationToken))
        {
            throw new ConflictException("A user with this email already exists.");
        }

        Guid createdUserId = await authAdminService.CreateDoctorUserAsync(
            email,
            firstName,
            lastName,
            temporaryPassword,
            cancellationToken);

        try
        {
            UserProfile profile = await userProfileRepository.GetByIdAsync(createdUserId, cancellationToken)
                ?? throw new InvalidOperationException(
                    "The created user profile was not provisioned by the signup trigger.");

            await using IUnitOfWorkTransaction transaction =
                await unitOfWork.BeginTransactionAsync(cancellationToken);

            // ux_doctors_user_id is the definitive guard against double-registering the same
            // created user, but it says nothing about the specialty's active flag. The Supabase
            // Auth Admin call is a network round-trip, so another admin can deactivate the
            // specialty between the check above and this point; re-read it under a row lock so
            // that race is either serialized against us or caught here instead of silently
            // persisted.
            Specialty lockedSpecialty =
                await specialtyRepository.GetByIdForUpdateAsync(command.SpecialtyId, cancellationToken)
                    ?? throw new NotFoundException("The specialty does not exist.");

            if (!lockedSpecialty.Active)
            {
                throw new ConflictException("The specialty is inactive.");
            }

            profile.PromoteToDoctor();

            var doctor = new Doctor(Guid.NewGuid(), createdUserId, lockedSpecialty.Id);
            doctorRepository.Add(doctor);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new RegisterDoctorResponse(
                doctor.Id,
                doctor.UserId,
                profile.FirstName,
                profile.LastName,
                profile.Email,
                new SpecialtyResponse(lockedSpecialty.Id, lockedSpecialty.Name),
                doctor.Active,
                ConcurrencyToken.ToToken(doctor.Version));
        }
        catch (Exception)
        {
            await CompensateAsync(createdUserId);
            throw;
        }
    }

    private async Task CompensateAsync(Guid createdUserId)
    {
        // `cancellationToken` from the original request may already be cancelled (client
        // disconnect, request timeout) by the time we get here, which would make DeleteUserAsync
        // a guaranteed no-op right when it's needed most. Compensation runs on its own bounded
        // token instead, independent of the caller's, so a cancelled or failed request still
        // deletes the Supabase Auth user just created. A compensation failure must never mask
        // the original persistence/validation error, so it is swallowed here; DeleteUserAsync
        // already logs it with structured, secret-free fields.
        using var compensationCts = new CancellationTokenSource(CompensationTimeout);
        try
        {
            await authAdminService.DeleteUserAsync(createdUserId, compensationCts.Token);
        }
        catch
        {
        }
    }

    private static string ValidateName(string name, string fieldLabel)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
        {
            throw new ArgumentException($"{fieldLabel} is required and cannot exceed 100 characters.");
        }

        return name.Trim();
    }

    private static string ValidatePassword(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8)
        {
            throw new ArgumentException("Temporary password is required and must be at least 8 characters long.");
        }

        return password;
    }

    private static string ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.");
        }

        string trimmed = email.Trim();
        if (trimmed.Length > 320 || !MailAddress.TryCreate(trimmed, out _))
        {
            throw new ArgumentException("Email must be a valid address and cannot exceed 320 characters.");
        }

        return trimmed.ToLowerInvariant();
    }
}
