using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Domain.Appointments;
using Microsoft.EntityFrameworkCore;

namespace MedicalAppointments.Infrastructure.Persistence.Readers;

public sealed class AvailabilityReader(
    MedicalAppointmentsDbContext dbContext,
    TimeZoneInfo clinicTimeZone) : IAvailabilityReader
{
    public async Task<IReadOnlySet<DateTimeOffset>> GetOccupiedSlotsAsync(
        Guid doctorId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        DateTime localFrom = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        DateTime localToExclusive = toDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        DateTimeOffset utcFrom = new DateTimeOffset(localFrom, clinicTimeZone.GetUtcOffset(localFrom)).ToUniversalTime();
        DateTimeOffset utcToExclusive = new DateTimeOffset(localToExclusive, clinicTimeZone.GetUtcOffset(localToExclusive)).ToUniversalTime();

        DateTimeOffset[] occupied = await dbContext.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.DoctorId == doctorId
                && appointment.Status == AppointmentStatus.Scheduled
                && appointment.StartsAt >= utcFrom
                && appointment.StartsAt < utcToExclusive)
            .Select(appointment => appointment.StartsAt)
            .ToArrayAsync(cancellationToken);

        return occupied.ToHashSet();
    }
}
