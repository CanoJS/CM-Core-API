using MedicalAppointments.Application.Abstractions.Messaging;

namespace MedicalAppointments.Application.Specialties.GetAdminSpecialties;

public sealed record GetAdminSpecialtiesQuery : IQuery<IReadOnlyList<AdminSpecialtyResponse>>;

public sealed record AdminSpecialtyResponse(Guid Id, string Name, bool Active, string Version);
