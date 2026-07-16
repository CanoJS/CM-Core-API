using MedicalAppointments.Application.Abstractions.Messaging;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Application.Common.Exceptions;

namespace MedicalAppointments.Application.Availability.GetDoctorAvailability;

public sealed class GetDoctorAvailabilityQueryHandler(
    IDoctorRepository doctorRepository,
    IAvailabilityReader availabilityReader,
    TimeZoneInfo clinicTimeZone)
    : IQueryHandler<GetDoctorAvailabilityQuery, IReadOnlyList<DayAvailabilityResponse>>
{
    public async Task<IReadOnlyList<DayAvailabilityResponse>> Handle(
        GetDoctorAvailabilityQuery query,
        CancellationToken cancellationToken)
    {
        if (query.To < query.From || query.To.DayNumber - query.From.DayNumber > 31)
        {
            throw new ArgumentException("Availability range must be between 1 and 31 days.");
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

        var days = new List<DayAvailabilityResponse>();

        for (DateOnly date = query.From; date <= query.To; date = date.AddDays(1))
        {
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            var slots = new List<TimeSlotResponse>(20);
            DateTime localStart = date.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Unspecified);

            for (int index = 0; index < 20; index++)
            {
                DateTime localSlot = localStart.AddMinutes(index * 30);
                DateTimeOffset slot = new(
                    localSlot,
                    clinicTimeZone.GetUtcOffset(localSlot));
                DateTimeOffset utcSlot = slot.ToUniversalTime();
                slots.Add(new TimeSlotResponse(utcSlot, !occupied.Contains(utcSlot)));
            }

            days.Add(new DayAvailabilityResponse(date, slots));
        }

        return days;
    }
}
