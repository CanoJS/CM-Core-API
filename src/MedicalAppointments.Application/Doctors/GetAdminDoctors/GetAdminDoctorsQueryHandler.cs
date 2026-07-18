using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Messaging;
using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Application.Common;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Application.Specialties.GetSpecialties;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.Doctors.GetAdminDoctors;

public sealed class GetAdminDoctorsQueryHandler(
    ICurrentUser currentUser,
    IAdminDoctorReader adminDoctorReader)
    : IQueryHandler<GetAdminDoctorsQuery, IReadOnlyList<AdminDoctorResponse>>
{
    public async Task<IReadOnlyList<AdminDoctorResponse>> Handle(
        GetAdminDoctorsQuery query,
        CancellationToken cancellationToken)
    {
        if (currentUser.Role != UserRole.Admin)
        {
            throw new ForbiddenException("Only administrators can view the doctor directory.");
        }

        IReadOnlyList<AdminDoctorItem> doctors = await adminDoctorReader.GetAllAsync(cancellationToken);

        return doctors
            .Select(doctor => new AdminDoctorResponse(
                doctor.Id,
                doctor.UserId,
                doctor.FirstName,
                doctor.LastName,
                $"{doctor.FirstName} {doctor.LastName}",
                doctor.Email,
                new SpecialtyResponse(doctor.SpecialtyId, doctor.SpecialtyName),
                doctor.Active,
                ConcurrencyToken.ToToken(doctor.Version)))
            .ToArray();
    }
}
