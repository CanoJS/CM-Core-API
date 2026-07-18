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

        Guid invitedUserId = await authAdminService.InviteDoctorAsync(
            email,
            firstName,
            lastName,
            cancellationToken);

        try
        {
            UserProfile profile = await userProfileRepository.GetByIdAsync(invitedUserId, cancellationToken)
                ?? throw new InvalidOperationException(
                    "The invited user profile was not provisioned by the signup trigger.");

            profile.PromoteToDoctor();

            var doctor = new Doctor(Guid.NewGuid(), invitedUserId, specialty.Id);
            doctorRepository.Add(doctor);

            // The specialty's unique index and ux_doctors_user_id are the definitive guards
            // against races; the checks above only improve the error message for the common case.
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new RegisterDoctorResponse(
                doctor.Id,
                doctor.UserId,
                profile.FirstName,
                profile.LastName,
                profile.Email,
                new SpecialtyResponse(specialty.Id, specialty.Name),
                doctor.Active,
                ConcurrencyToken.ToToken(doctor.Version));
        }
        catch (Exception)
        {
            // A compensation failure must never mask the original persistence error.
            try
            {
                await authAdminService.DeleteUserAsync(invitedUserId, cancellationToken);
            }
            catch
            {
            }

            throw;
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
