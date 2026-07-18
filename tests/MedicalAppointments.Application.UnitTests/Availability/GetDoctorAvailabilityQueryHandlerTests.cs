using MedicalAppointments.Application.Abstractions.Clock;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Application.Abstractions.Scheduling;
using MedicalAppointments.Application.Availability.GetDoctorAvailability;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Domain.Doctors;

namespace MedicalAppointments.Application.UnitTests.Availability;

public sealed class GetDoctorAvailabilityQueryHandlerTests
{
    // 2026-07-20 is a Monday, 2026-07-24 a Friday, 2026-07-18/19 a Saturday/Sunday - verified
    // against a real calendar, not assumed.
    private static readonly Guid DoctorId = Guid.NewGuid();
    private static readonly DateOnly Monday = new(2026, 7, 20);
    private static readonly DateOnly Friday = new(2026, 7, 24);
    private static readonly DateOnly Saturday = new(2026, 7, 18);
    private static readonly DateOnly Sunday = new(2026, 7, 19);

    [Fact]
    public async Task Handle_WithSingleDayRange_ReturnsOneDay()
    {
        var handler = CreateHandler();

        IReadOnlyList<DayAvailabilityResponse> result = await handler.Handle(
            new GetDoctorAvailabilityQuery(DoctorId, Monday, Monday),
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(Monday, result[0].Date);
    }

    [Fact]
    public async Task Handle_With31InclusiveDays_DoesNotThrow()
    {
        var handler = CreateHandler();
        DateOnly to = Monday.AddDays(30);

        IReadOnlyList<DayAvailabilityResponse> result = await handler.Handle(
            new GetDoctorAvailabilityQuery(DoctorId, Monday, to),
            CancellationToken.None);

        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task Handle_With32InclusiveDays_ThrowsArgumentException()
    {
        var handler = CreateHandler();
        DateOnly to = Monday.AddDays(31);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(new GetDoctorAvailabilityQuery(DoctorId, Monday, to), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenToIsBeforeFrom_ThrowsArgumentException()
    {
        var handler = CreateHandler();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(
                new GetDoctorAvailabilityQuery(DoctorId, Monday, Monday.AddDays(-1)),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenDoctorDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(doctorExists: false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetDoctorAvailabilityQuery(DoctorId, Monday, Monday), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenDoctorIsInactive_ThrowsNotFound()
    {
        // IDoctorRepository.IsActiveAsync returns false for both "missing" and "inactive" -
        // the handler (like CreateAppointmentCommandHandler) cannot and need not distinguish
        // them; both map to the same 404.
        var handler = CreateHandler(doctorExists: false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetDoctorAvailabilityQuery(DoctorId, Monday, Monday), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_OnBusinessDay_Produces20Slots()
    {
        var handler = CreateHandler();

        IReadOnlyList<DayAvailabilityResponse> result = await handler.Handle(
            new GetDoctorAvailabilityQuery(DoctorId, Monday, Monday),
            CancellationToken.None);

        Assert.Equal(20, result[0].Slots.Count);
    }

    [Fact]
    public async Task Handle_FirstSlot_IsAt0800Local()
    {
        var handler = CreateHandler();

        IReadOnlyList<DayAvailabilityResponse> result = await handler.Handle(
            new GetDoctorAvailabilityQuery(DoctorId, Monday, Monday),
            CancellationToken.None);

        DateTimeOffset firstSlotLocal = TimeZoneInfo.ConvertTime(result[0].Slots[0].StartsAt, ClinicTimeZone);
        Assert.Equal(new TimeOnly(8, 0), TimeOnly.FromDateTime(firstSlotLocal.DateTime));
    }

    [Fact]
    public async Task Handle_LastSlot_StartsAt1730LocalAndEndsAt1800Local()
    {
        var handler = CreateHandler();

        IReadOnlyList<DayAvailabilityResponse> result = await handler.Handle(
            new GetDoctorAvailabilityQuery(DoctorId, Monday, Monday),
            CancellationToken.None);

        TimeSlotResponse lastSlot = result[0].Slots[^1];
        DateTimeOffset lastSlotLocal = TimeZoneInfo.ConvertTime(lastSlot.StartsAt, ClinicTimeZone);
        DateTimeOffset lastSlotEndLocal = TimeZoneInfo.ConvertTime(lastSlot.StartsAt.AddMinutes(30), ClinicTimeZone);
        Assert.Equal(new TimeOnly(17, 30), TimeOnly.FromDateTime(lastSlotLocal.DateTime));
        Assert.Equal(new TimeOnly(18, 0), TimeOnly.FromDateTime(lastSlotEndLocal.DateTime));
    }

    [Fact]
    public async Task Handle_WithWeekendRange_ProducesNoDays()
    {
        var handler = CreateHandler();

        IReadOnlyList<DayAvailabilityResponse> result = await handler.Handle(
            new GetDoctorAvailabilityQuery(DoctorId, Saturday, Sunday),
            CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_WhenSlotIsOccupiedBySCheduledAppointment_ReturnsAvailableFalse()
    {
        DateTimeOffset firstSlot = new FakeClinicSchedule().GetBookableSlots(Monday)[0];
        var handler = CreateHandler(occupiedSlots: [firstSlot]);

        IReadOnlyList<DayAvailabilityResponse> result = await handler.Handle(
            new GetDoctorAvailabilityQuery(DoctorId, Monday, Monday),
            CancellationToken.None);

        Assert.False(result[0].Slots[0].Available);
    }

    [Fact]
    public async Task Handle_WhenSlotIsFree_ReturnsAvailableTrue()
    {
        var handler = CreateHandler();

        IReadOnlyList<DayAvailabilityResponse> result = await handler.Handle(
            new GetDoctorAvailabilityQuery(DoctorId, Monday, Monday),
            CancellationToken.None);

        Assert.True(result[0].Slots[0].Available);
    }

    [Fact]
    public async Task Handle_WhenSlotAlreadyPassed_ReturnsAvailableFalse()
    {
        IReadOnlyList<DateTimeOffset> slots = new FakeClinicSchedule().GetBookableSlots(Monday);
        var handler = CreateHandler(now: slots[5].AddMinutes(1));

        IReadOnlyList<DayAvailabilityResponse> result = await handler.Handle(
            new GetDoctorAvailabilityQuery(DoctorId, Monday, Monday),
            CancellationToken.None);

        Assert.False(result[0].Slots[5].Available);
    }

    [Fact]
    public async Task Handle_WhenSlotStartsExactlyNow_ReturnsAvailableFalse()
    {
        IReadOnlyList<DateTimeOffset> slots = new FakeClinicSchedule().GetBookableSlots(Monday);
        var handler = CreateHandler(now: slots[3]);

        IReadOnlyList<DayAvailabilityResponse> result = await handler.Handle(
            new GetDoctorAvailabilityQuery(DoctorId, Monday, Monday),
            CancellationToken.None);

        Assert.False(result[0].Slots[3].Available);
    }

    [Fact]
    public async Task Handle_WithMultiDayRange_ReturnsDaysAndSlotsInAscendingOrder()
    {
        var handler = CreateHandler();

        IReadOnlyList<DayAvailabilityResponse> result = await handler.Handle(
            new GetDoctorAvailabilityQuery(DoctorId, Monday, Friday),
            CancellationToken.None);

        Assert.Equal(result.Select(day => day.Date).OrderBy(date => date), result.Select(day => day.Date));
        foreach (DayAvailabilityResponse day in result)
        {
            Assert.Equal(
                day.Slots.Select(slot => slot.StartsAt).OrderBy(startsAt => startsAt),
                day.Slots.Select(slot => slot.StartsAt));
        }
    }

    [Fact]
    public async Task Handle_ConvertsSlotsUsingAmericaMexicoCityTimeZone()
    {
        var handler = CreateHandler();

        IReadOnlyList<DayAvailabilityResponse> result = await handler.Handle(
            new GetDoctorAvailabilityQuery(DoctorId, Monday, Monday),
            CancellationToken.None);

        foreach (TimeSlotResponse slot in result[0].Slots)
        {
            DateTimeOffset local = TimeZoneInfo.ConvertTime(slot.StartsAt, ClinicTimeZone);
            Assert.Equal(Monday, DateOnly.FromDateTime(local.DateTime));
        }
    }

    private static readonly TimeZoneInfo ClinicTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");

    private static GetDoctorAvailabilityQueryHandler CreateHandler(
        bool doctorExists = true,
        IReadOnlyCollection<DateTimeOffset>? occupiedSlots = null,
        DateTimeOffset? now = null) =>
        new(
            new DoctorRepositoryStub(doctorExists),
            new AvailabilityReaderStub(occupiedSlots ?? []),
            new FakeClinicSchedule(),
            new ClockStub(now ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));

    private sealed class DoctorRepositoryStub(bool isActive) : IDoctorRepository
    {
        public Task<bool> IsActiveAsync(Guid doctorId, CancellationToken cancellationToken) =>
            Task.FromResult(isActive);

        public void Add(Doctor doctor)
        {
        }

        public Task<Doctor?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<Doctor?>(null);

        public void PrepareStatusUpdate(Doctor doctor, uint version)
        {
        }

        public void PrepareSpecialtyUpdate(Doctor doctor, uint version)
        {
        }
    }

    private sealed class AvailabilityReaderStub(IReadOnlyCollection<DateTimeOffset> occupied) : IAvailabilityReader
    {
        public Task<IReadOnlySet<DateTimeOffset>> GetOccupiedSlotsAsync(
            Guid doctorId,
            DateOnly fromDate,
            DateOnly toDate,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<DateTimeOffset>>(occupied.ToHashSet());
    }

    private sealed class ClockStub(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    // Mirrors production ClinicSchedule's rules (Mon-Fri, 08:00-18:00 America/Mexico_City,
    // 30-minute slots) so handler tests exercise realistic slot data. The rules themselves are
    // independently verified against the real implementation in ClinicScheduleTests.
    private sealed class FakeClinicSchedule : IClinicSchedule
    {
        private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");

        public bool IsBookableSlot(DateTimeOffset startsAt) =>
            throw new NotSupportedException("Not used by GetDoctorAvailabilityQueryHandler.");

        public IReadOnlyList<DateTimeOffset> GetBookableSlots(DateOnly localDate)
        {
            if (localDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                return [];
            }

            DateTime localStart = localDate.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Unspecified);
            var slots = new DateTimeOffset[20];
            for (int index = 0; index < 20; index++)
            {
                DateTime local = localStart.AddMinutes(index * 30);
                slots[index] = new DateTimeOffset(local, Zone.GetUtcOffset(local)).ToUniversalTime();
            }

            return slots;
        }
    }
}
