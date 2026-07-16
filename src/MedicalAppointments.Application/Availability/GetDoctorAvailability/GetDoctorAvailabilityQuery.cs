using MedicalAppointments.Application.Abstractions.Messaging;

namespace MedicalAppointments.Application.Availability.GetDoctorAvailability;

public sealed record GetDoctorAvailabilityQuery(
    Guid DoctorId,
    DateOnly From,
    DateOnly To) : IQuery<IReadOnlyList<DayAvailabilityResponse>>;

public sealed record TimeSlotResponse(DateTimeOffset StartsAt, bool Available);

public sealed record DayAvailabilityResponse(DateOnly Date, IReadOnlyList<TimeSlotResponse> Slots);
