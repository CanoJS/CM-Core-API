using MedicalAppointments.Application.Abstractions.Messaging;

namespace MedicalAppointments.Application.Specialties.CreateSpecialty;

public sealed record CreateSpecialtyCommand(string Name) : ICommand<CreateSpecialtyResponse>;

public sealed record CreateSpecialtyResponse(Guid Id, string Name, bool Active, string Version);
