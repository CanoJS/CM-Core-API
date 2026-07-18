using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Messaging;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Common;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Application.Specialties.GetSpecialties;
using MedicalAppointments.Domain.Doctors;
using MedicalAppointments.Domain.Specialties;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.Doctors.ChangeDoctorSpecialty;

public sealed class ChangeDoctorSpecialtyCommandHandler(
    ICurrentUser currentUser,
    IDoctorRepository doctorRepository,
    ISpecialtyRepository specialtyRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ChangeDoctorSpecialtyCommand, ChangeDoctorSpecialtyResponse>
{
    public async Task<ChangeDoctorSpecialtyResponse> Handle(
        ChangeDoctorSpecialtyCommand command,
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

        Specialty specialty = await specialtyRepository.GetByIdAsync(command.SpecialtyId, cancellationToken)
            ?? throw new NotFoundException("The specialty does not exist.");

        if (!specialty.Active)
        {
            throw new ConflictException("The specialty is inactive.");
        }

        Doctor doctor = await doctorRepository.GetByIdAsync(command.DoctorId, cancellationToken)
            ?? throw new NotFoundException("The doctor does not exist.");

        await using IUnitOfWorkTransaction transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        // The check above only improves the error message for the common case; it says nothing
        // about the specialty's active flag once another admin deactivates it concurrently.
        // Re-read under a row lock so that race is serialized against this write instead of
        // silently reassigning the doctor to an inactive specialty.
        Specialty lockedSpecialty =
            await specialtyRepository.GetByIdForUpdateAsync(command.SpecialtyId, cancellationToken)
                ?? throw new NotFoundException("The specialty does not exist.");

        if (!lockedSpecialty.Active)
        {
            throw new ConflictException("The specialty is inactive.");
        }

        doctorRepository.PrepareSpecialtyUpdate(doctor, version);
        doctor.ChangeSpecialty(lockedSpecialty.Id);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ChangeDoctorSpecialtyResponse(
            doctor.Id,
            new SpecialtyResponse(lockedSpecialty.Id, lockedSpecialty.Name),
            ConcurrencyToken.ToToken(doctor.Version));
    }
}
