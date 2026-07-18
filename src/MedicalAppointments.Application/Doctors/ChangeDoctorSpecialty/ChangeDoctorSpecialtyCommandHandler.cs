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

        doctorRepository.PrepareSpecialtyUpdate(doctor, version);
        doctor.ChangeSpecialty(specialty.Id);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ChangeDoctorSpecialtyResponse(
            doctor.Id,
            new SpecialtyResponse(specialty.Id, specialty.Name),
            ConcurrencyToken.ToToken(doctor.Version));
    }
}
