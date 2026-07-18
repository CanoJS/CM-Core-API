using MedicalAppointments.Application.Abstractions.Messaging;
using MedicalAppointments.Application.Specialties.GetSpecialties;

namespace MedicalAppointments.Application.Doctors.GetActiveDoctors;

public sealed record GetActiveDoctorsQuery(Guid? SpecialtyId)
    : IQuery<IReadOnlyList<DoctorResponse>>;

public sealed record DoctorResponse(
    Guid Id,
    string FullName,
    string Email,
    SpecialtyResponse Specialty,
    bool Active);
