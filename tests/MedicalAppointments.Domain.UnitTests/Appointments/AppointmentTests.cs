using MedicalAppointments.Domain.Appointments;
using MedicalAppointments.Domain.Common;

namespace MedicalAppointments.Domain.UnitTests.Appointments;

public sealed class AppointmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Schedule_WithValidData_CreatesScheduledAppointment()
    {
        Guid patientId = Guid.NewGuid();
        Guid doctorId = Guid.NewGuid();
        DateTimeOffset startsAt = new(2026, 7, 20, 15, 30, 0, TimeSpan.Zero);

        Appointment appointment = Appointment.Schedule(patientId, doctorId, startsAt, "Control anual", Now);

        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
        Assert.Equal(startsAt, appointment.StartsAt);
        Assert.Equal(startsAt.AddMinutes(30), appointment.EndsAt);
        Assert.Null(appointment.MedicalNote);
    }

    [Fact]
    public void Schedule_SetsCreatedAtAndUpdatedAtFromThePassedNow_NotSystemClock()
    {
        Appointment appointment = Appointment.Schedule(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateTimeOffset(2026, 7, 20, 15, 30, 0, TimeSpan.Zero),
            "Control anual",
            Now);

        Assert.Equal(Now, appointment.CreatedAt);
        Assert.Equal(Now, appointment.UpdatedAt);
    }

    [Fact]
    public void Schedule_OutsideHalfHourBoundary_Throws()
    {
        Action action = () => Appointment.Schedule(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateTimeOffset(2026, 7, 20, 15, 10, 0, TimeSpan.Zero),
            "Control anual",
            Now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Schedule_WithFractionalSecond_Throws()
    {
        // 15:30:00.500 - whole minute and whole second are both "valid" on their own; only the
        // sub-second remainder makes this not a 30-minute boundary.
        DateTimeOffset startsAt = new DateTimeOffset(2026, 7, 20, 15, 30, 0, TimeSpan.Zero)
            .AddMilliseconds(500);

        Action action = () => Appointment.Schedule(
            Guid.NewGuid(),
            Guid.NewGuid(),
            startsAt,
            "Control anual",
            Now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Reschedule_WithFractionalSecond_Throws()
    {
        Appointment appointment = Appointment.Schedule(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddDays(1),
            "Control anual",
            Now);
        DateTimeOffset startsAt = new DateTimeOffset(2026, 7, 21, 15, 30, 0, TimeSpan.Zero)
            .AddMilliseconds(500);

        Action action = () => appointment.Reschedule(Guid.NewGuid(), startsAt, Now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Reschedule_WithValidData_UpdatesDoctorAndStartsAt()
    {
        Appointment appointment = Appointment.Schedule(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddDays(1),
            "Control anual",
            Now);
        Guid newDoctorId = Guid.NewGuid();
        DateTimeOffset newStartsAt = Now.AddDays(2);

        appointment.Reschedule(newDoctorId, newStartsAt, Now);

        Assert.Equal(newDoctorId, appointment.DoctorId);
        Assert.Equal(newStartsAt, appointment.StartsAt);
    }

    [Fact]
    public void Reschedule_OutsideHalfHourBoundary_Throws()
    {
        Appointment appointment = Appointment.Schedule(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddDays(1),
            "Control anual",
            Now);

        Action action = () => appointment.Reschedule(
            Guid.NewGuid(),
            new DateTimeOffset(2026, 7, 21, 15, 10, 0, TimeSpan.Zero),
            Now);

        Assert.Throws<DomainException>(action);
    }

    [Theory]
    [InlineData(AppointmentStatus.Attended)]
    [InlineData(AppointmentStatus.Cancelled)]
    public void Reschedule_WhenNotScheduled_Throws(AppointmentStatus status)
    {
        Appointment appointment = CreateInStatus(status);

        Action action = () => appointment.Reschedule(Guid.NewGuid(), Now.AddDays(1), Now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void CancelByPatient_WithMoreThanTwentyFourHours_Cancels()
    {
        Appointment appointment = Appointment.Schedule(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddHours(24).AddMinutes(30),
            "Control anual",
            Now);

        appointment.CancelByPatient(Now);

        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
    }

    [Fact]
    public void CancelByPatient_AtExactlyTwentyFourHours_Throws()
    {
        Appointment appointment = Appointment.Schedule(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddHours(24),
            "Control anual",
            Now);

        Action action = () => appointment.CancelByPatient(Now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void CancelByPatient_WithinTwentyFourHours_Throws()
    {
        Appointment appointment = Appointment.Schedule(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddHours(23).AddMinutes(30),
            "Control anual",
            Now);

        Action action = () => appointment.CancelByPatient(Now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void CancelByAdmin_WithinTwentyFourHours_Cancels()
    {
        Appointment appointment = Appointment.Schedule(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddMinutes(30),
            "Control anual",
            Now);

        appointment.CancelByAdmin(Now);

        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
    }

    [Theory]
    [InlineData(AppointmentStatus.Attended)]
    [InlineData(AppointmentStatus.Cancelled)]
    public void CancelByPatient_WhenNotScheduled_Throws(AppointmentStatus status)
    {
        Appointment appointment = CreateInStatus(status);

        Action action = () => appointment.CancelByPatient(Now.AddDays(-10));

        Assert.Throws<DomainException>(action);
    }

    [Theory]
    [InlineData(AppointmentStatus.Attended)]
    [InlineData(AppointmentStatus.Cancelled)]
    public void CancelByAdmin_WhenNotScheduled_Throws(AppointmentStatus status)
    {
        Appointment appointment = CreateInStatus(status);

        Action action = () => appointment.CancelByAdmin(Now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Attend_WithMedicalNote_ChangesStatus()
    {
        Appointment appointment = Appointment.Schedule(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddDays(4),
            "Control anual",
            Now);

        appointment.Attend("Paciente estable.", Now.AddDays(4));

        Assert.Equal(AppointmentStatus.Attended, appointment.Status);
        Assert.Equal("Paciente estable.", appointment.MedicalNote);
    }

    [Fact]
    public void Attend_BeforeStartsAt_Throws()
    {
        Appointment appointment = Appointment.Schedule(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddDays(4),
            "Control anual",
            Now);

        Action action = () => appointment.Attend("Paciente estable.", Now.AddDays(4).AddMinutes(-1));

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Attend_AtExactlyStartsAt_Attends()
    {
        Appointment appointment = Appointment.Schedule(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddDays(4),
            "Control anual",
            Now);

        appointment.Attend("Paciente estable.", Now.AddDays(4));

        Assert.Equal(AppointmentStatus.Attended, appointment.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Attend_WithoutMedicalNote_Throws(string note)
    {
        Appointment appointment = Appointment.Schedule(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddDays(4),
            "Control anual",
            Now);

        Action action = () => appointment.Attend(note, Now.AddDays(4));

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Attend_WithMedicalNoteOverMaxLength_Throws()
    {
        Appointment appointment = Appointment.Schedule(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddDays(4),
            "Control anual",
            Now);

        Action action = () => appointment.Attend(new string('a', 4_001), Now.AddDays(4));

        Assert.Throws<DomainException>(action);
    }

    [Theory]
    [InlineData(AppointmentStatus.Attended)]
    [InlineData(AppointmentStatus.Cancelled)]
    public void Attend_WhenNotScheduled_Throws(AppointmentStatus status)
    {
        Appointment appointment = CreateInStatus(status);

        Action action = () => appointment.Attend("Nota.", Now.AddDays(10));

        Assert.Throws<DomainException>(action);
    }

    private static Appointment CreateInStatus(AppointmentStatus status)
    {
        Appointment appointment = Appointment.Schedule(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddDays(1),
            "Control anual",
            Now);

        switch (status)
        {
            case AppointmentStatus.Attended:
                appointment.Attend("Nota inicial.", Now.AddDays(1));
                break;
            case AppointmentStatus.Cancelled:
                appointment.CancelByAdmin(Now);
                break;
            case AppointmentStatus.Scheduled:
                break;
        }

        return appointment;
    }
}
