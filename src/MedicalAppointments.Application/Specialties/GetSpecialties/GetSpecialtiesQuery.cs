using MedicalAppointments.Application.Abstractions.Messaging;

namespace MedicalAppointments.Application.Specialties.GetSpecialties;

public sealed record GetSpecialtiesQuery : IQuery<IReadOnlyList<SpecialtyResponse>>;

public sealed record SpecialtyResponse(Guid Id, string Name);
