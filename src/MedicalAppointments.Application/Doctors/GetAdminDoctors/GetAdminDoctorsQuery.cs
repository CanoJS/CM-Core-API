using MedicalAppointments.Application.Abstractions.Messaging;
using MedicalAppointments.Application.Specialties.GetSpecialties;

namespace MedicalAppointments.Application.Doctors.GetAdminDoctors;

public sealed record GetAdminDoctorsQuery : IQuery<IReadOnlyList<AdminDoctorResponse>>;

public sealed record AdminDoctorResponse(
    Guid Id,
    Guid UserId,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    SpecialtyResponse Specialty,
    bool Active,
    string Version);
