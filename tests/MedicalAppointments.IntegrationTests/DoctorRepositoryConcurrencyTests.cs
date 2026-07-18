using MedicalAppointments.Domain.Doctors;
using MedicalAppointments.Infrastructure.Persistence;
using MedicalAppointments.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace MedicalAppointments.IntegrationTests;

// No RealDatabaseFact test hits medical.doctors directly here: doctors.user_id has a foreign
// key to auth.users, which can only be satisfied by a real Supabase Auth user - creating one in
// an automated test is explicitly out of scope. The identical "force IsModified + xmin
// OriginalValue" mechanism is already proven against real Postgres by
// SpecialtyRepositoryConcurrencyTests, which needs no such external dependency.
public sealed class DoctorRepositoryConcurrencyTests
{
    [Fact]
    public void PrepareStatusUpdate_MarksActiveModifiedAndSetsOriginalVersion()
    {
        using MedicalAppointmentsDbContext dbContext = CreateUnconnectedDbContext();
        var doctor = new Doctor(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        dbContext.Attach(doctor);
        var repository = new DoctorRepository(dbContext);

        repository.PrepareStatusUpdate(doctor, 42);

        EntityEntry<Doctor> entry = dbContext.Entry(doctor);
        Assert.True(entry.Property(d => d.Active).IsModified);
        Assert.Equal(42u, entry.Property(d => d.Version).OriginalValue);
    }

    [Fact]
    public void PrepareSpecialtyUpdate_MarksSpecialtyIdModifiedAndSetsOriginalVersion()
    {
        using MedicalAppointmentsDbContext dbContext = CreateUnconnectedDbContext();
        var doctor = new Doctor(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        dbContext.Attach(doctor);
        var repository = new DoctorRepository(dbContext);

        repository.PrepareSpecialtyUpdate(doctor, 7);

        EntityEntry<Doctor> entry = dbContext.Entry(doctor);
        Assert.True(entry.Property(d => d.SpecialtyId).IsModified);
        Assert.Equal(7u, entry.Property(d => d.Version).OriginalValue);
    }

    private static MedicalAppointmentsDbContext CreateUnconnectedDbContext()
    {
        DbContextOptions<MedicalAppointmentsDbContext> options =
            new DbContextOptionsBuilder<MedicalAppointmentsDbContext>()
                .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
                .Options;
        return new MedicalAppointmentsDbContext(options);
    }
}
