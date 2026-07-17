using MedicalAppointments.Application.Abstractions.Messaging;
using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Application.Specialties.GetSpecialties;

namespace MedicalAppointments.Application.Doctors.GetActiveDoctors;

public sealed class GetActiveDoctorsQueryHandler(IDoctorCatalogReader doctorCatalogReader)
    : IQueryHandler<GetActiveDoctorsQuery, IReadOnlyList<DoctorResponse>>
{
    public async Task<IReadOnlyList<DoctorResponse>> Handle(
        GetActiveDoctorsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.SpecialtyId == Guid.Empty)
        {
            throw new ArgumentException("Specialty identifier cannot be empty.");
        }

        IReadOnlyList<DoctorCatalogItem> doctors = await doctorCatalogReader.GetActiveAsync(
            query.SpecialtyId,
            cancellationToken);

        return doctors
            .Select(doctor => new DoctorResponse(
                doctor.Id,
                $"{doctor.FirstName} {doctor.LastName}",
                doctor.Email,
                new SpecialtyResponse(doctor.SpecialtyId, doctor.SpecialtyName),
                doctor.Active))
            .ToArray();
    }
}
