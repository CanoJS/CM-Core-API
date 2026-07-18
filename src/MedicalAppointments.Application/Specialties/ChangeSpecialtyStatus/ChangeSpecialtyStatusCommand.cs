using MedicalAppointments.Application.Abstractions.Messaging;

namespace MedicalAppointments.Application.Specialties.ChangeSpecialtyStatus;

public sealed record ChangeSpecialtyStatusCommand(Guid SpecialtyId, bool Active, string Version)
    : ICommand<ChangeSpecialtyStatusResponse>;

public sealed record ChangeSpecialtyStatusResponse(Guid Id, string Name, bool Active, string Version);
