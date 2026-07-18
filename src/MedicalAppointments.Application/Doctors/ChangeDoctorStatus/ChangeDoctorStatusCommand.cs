using MedicalAppointments.Application.Abstractions.Messaging;

namespace MedicalAppointments.Application.Doctors.ChangeDoctorStatus;

public sealed record ChangeDoctorStatusCommand(Guid DoctorId, bool Active, string Version)
    : ICommand<ChangeDoctorStatusResponse>;

public sealed record ChangeDoctorStatusResponse(Guid Id, bool Active, string Version);
