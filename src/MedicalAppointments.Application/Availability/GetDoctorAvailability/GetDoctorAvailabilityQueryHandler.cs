using MedicalAppointments.Application.Abstractions.Clock;
using MedicalAppointments.Application.Abstractions.Messaging;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Application.Abstractions.Scheduling;
using MedicalAppointments.Application.Common.Exceptions;

namespace MedicalAppointments.Application.Availability.GetDoctorAvailability;

public sealed class GetDoctorAvailabilityQueryHandler(
    IDoctorRepository doctorRepository,
    IAvailabilityReader availabilityReader,
    IClinicSchedule clinicSchedule,
    IClock clock)
    : IQueryHandler<GetDoctorAvailabilityQuery, IReadOnlyList<DayAvailabilityResponse>>
{
    // Inclusive on both ends: `to == from` is a 1-day query, `to.DayNumber - from.DayNumber`
    // == MaxInclusiveDays - 1 is exactly MaxInclusiveDays inclusive dates.
    private const int MaxInclusiveDays = 31;

    public async Task<IReadOnlyList<DayAvailabilityResponse>> Handle(
        GetDoctorAvailabilityQuery query,
        CancellationToken cancellationToken)
    {
        if (query.To < query.From || query.To.DayNumber - query.From.DayNumber > MaxInclusiveDays - 1)
        {
            throw new ArgumentException($"Availability range must be between 1 and {MaxInclusiveDays} inclusive days.");
        }

        if (!await doctorRepository.IsActiveAsync(query.DoctorId, cancellationToken))
        {
            throw new NotFoundException("The selected doctor does not exist or is inactive.");
        }

        IReadOnlySet<DateTimeOffset> occupied = await availabilityReader.GetOccupiedSlotsAsync(
            query.DoctorId,
            query.From,
            query.To,
            cancellationToken);

        DateTimeOffset now = clock.UtcNow;
        var days = new List<DayAvailabilityResponse>();

        for (DateOnly date = query.From; date <= query.To; date = date.AddDays(1))
        {
            IReadOnlyList<DateTimeOffset> bookableSlots = clinicSchedule.GetBookableSlots(date);
            if (bookableSlots.Count == 0)
            {
                continue;
            }

            var slots = new List<TimeSlotResponse>(bookableSlots.Count);
            foreach (DateTimeOffset slot in bookableSlots)
            {
                // A slot is available only if it is strictly in the future (matches
                // Appointment.Schedule's own "startsAt must be in the future" invariant) and
                // not already taken by a SCHEDULED appointment.
                bool available = slot > now && !occupied.Contains(slot);
                slots.Add(new TimeSlotResponse(slot, available));
            }

            days.Add(new DayAvailabilityResponse(date, slots));
        }

        return days;
    }
}
