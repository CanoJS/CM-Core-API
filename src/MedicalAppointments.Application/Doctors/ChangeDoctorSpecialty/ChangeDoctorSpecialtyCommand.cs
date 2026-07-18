using MedicalAppointments.Application.Abstractions.Messaging;
using MedicalAppointments.Application.Specialties.GetSpecialties;

namespace MedicalAppointments.Application.Doctors.ChangeDoctorSpecialty;

public sealed record ChangeDoctorSpecialtyCommand(Guid DoctorId, Guid SpecialtyId, string Version)
    : ICommand<ChangeDoctorSpecialtyResponse>;

public sealed record ChangeDoctorSpecialtyResponse(Guid Id, SpecialtyResponse Specialty, string Version);
