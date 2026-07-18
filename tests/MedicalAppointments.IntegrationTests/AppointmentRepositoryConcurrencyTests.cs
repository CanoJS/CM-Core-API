using MedicalAppointments.Domain.Appointments;
using MedicalAppointments.Infrastructure.Persistence;
using MedicalAppointments.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace MedicalAppointments.IntegrationTests;

// No RealDatabaseFact test hits medical.appointments directly here: appointments.patient_id and
// .doctor_id both chain to medical.user_profiles -> auth.users, which can only be satisfied by a
// real Supabase Auth user - creating one in an automated test is explicitly out of scope, same
// limitation already documented on DoctorRepositoryConcurrencyTests. The identical "force
// IsModified + xmin OriginalValue" mechanism is already proven against real Postgres by
// SpecialtyRepositoryConcurrencyTests, which needs no such external dependency.
public sealed class AppointmentRepositoryConcurrencyTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PrepareStatusUpdate_MarksStatusModifiedAndSetsOriginalVersion()
    {
        using MedicalAppointmentsDbContext dbContext = CreateUnconnectedDbContext();
        Appointment appointment = CreateAppointment();
        dbContext.Attach(appointment);
        var repository = new AppointmentRepository(dbContext);

        repository.PrepareStatusUpdate(appointment, 42);

        EntityEntry<Appointment> entry = dbContext.Entry(appointment);
        Assert.True(entry.Property(a => a.Status).IsModified);
        Assert.Equal(42u, entry.Property(a => a.Version).OriginalValue);
    }

    [Fact]
    public void PrepareRescheduleUpdate_MarksDoctorIdAndStartsAtModifiedAndSetsOriginalVersion()
    {
        using MedicalAppointmentsDbContext dbContext = CreateUnconnectedDbContext();
        Appointment appointment = CreateAppointment();
        dbContext.Attach(appointment);
        var repository = new AppointmentRepository(dbContext);

        repository.PrepareRescheduleUpdate(appointment, 7);

        EntityEntry<Appointment> entry = dbContext.Entry(appointment);
        Assert.True(entry.Property(a => a.DoctorId).IsModified);
        Assert.True(entry.Property(a => a.StartsAt).IsModified);
        Assert.Equal(7u, entry.Property(a => a.Version).OriginalValue);
    }

    private static Appointment CreateAppointment() =>
        Appointment.Schedule(Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(1), "Control anual", Now);

    private static MedicalAppointmentsDbContext CreateUnconnectedDbContext()
    {
        DbContextOptions<MedicalAppointmentsDbContext> options =
            new DbContextOptionsBuilder<MedicalAppointmentsDbContext>()
                .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
                .Options;
        return new MedicalAppointmentsDbContext(options);
    }
}
