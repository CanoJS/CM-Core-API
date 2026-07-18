using MedicalAppointments.Application.Abstractions.Messaging;
using MedicalAppointments.Application.Specialties.GetSpecialties;

namespace MedicalAppointments.Application.Doctors.RegisterDoctor;

public sealed record RegisterDoctorCommand(
    string FirstName,
    string LastName,
    string Email,
    Guid SpecialtyId) : ICommand<RegisterDoctorResponse>;

public sealed record RegisterDoctorResponse(
    Guid Id,
    Guid UserId,
    string FirstName,
    string LastName,
    string Email,
    SpecialtyResponse Specialty,
    bool Active,
    string Version);
