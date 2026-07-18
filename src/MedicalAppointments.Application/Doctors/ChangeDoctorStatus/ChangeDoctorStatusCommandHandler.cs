using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Messaging;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Common;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Domain.Doctors;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.Doctors.ChangeDoctorStatus;

public sealed class ChangeDoctorStatusCommandHandler(
    ICurrentUser currentUser,
    IDoctorRepository doctorRepository,
    IUserProfileRepository userProfileRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ChangeDoctorStatusCommand, ChangeDoctorStatusResponse>
{
    public async Task<ChangeDoctorStatusResponse> Handle(
        ChangeDoctorStatusCommand command,
        CancellationToken cancellationToken)
    {
        if (currentUser.Role != UserRole.Admin)
        {
            throw new ForbiddenException("Only administrators can manage doctors.");
        }

        if (!ConcurrencyToken.TryParse(command.Version, out uint version))
        {
            throw new ArgumentException("The doctor version is invalid.");
        }

        Doctor doctor = await doctorRepository.GetByIdAsync(command.DoctorId, cancellationToken)
            ?? throw new NotFoundException("The doctor does not exist.");

        doctorRepository.PrepareStatusUpdate(doctor, version);
        doctor.SetActive(command.Active);

        UserProfile profile = await userProfileRepository.GetByIdAsync(doctor.UserId, cancellationToken)
            ?? throw new InvalidOperationException("The doctor's user profile is missing.");

        if (command.Active)
        {
            profile.Activate();
        }
        else
        {
            profile.Deactivate();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ChangeDoctorStatusResponse(
            doctor.Id,
            doctor.Active,
            ConcurrencyToken.ToToken(doctor.Version));
    }
}
