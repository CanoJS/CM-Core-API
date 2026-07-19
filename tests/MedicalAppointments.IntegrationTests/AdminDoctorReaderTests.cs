using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Domain.Doctors;
using MedicalAppointments.Domain.Specialties;
using MedicalAppointments.Infrastructure.Persistence;
using MedicalAppointments.Infrastructure.Persistence.Readers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MedicalAppointments.IntegrationTests;

// No unit-level (ToQueryString) coverage exists for AdminDoctorReader - its query has no
// projection/ordering pitfalls like AppointmentReader's, so a real materialized result is the
// only test worth writing. See RealDatabaseSeeding for how the auth.users FK is satisfied safely
// under RUN_REAL_DB_TESTS=true.
public sealed class AdminDoctorReaderTests
{
    [RealDatabaseFact]
    public async Task GetAllAsync_AgainstRealDatabase_ReturnsActiveAndInactiveDoctorsWithCorrectFields()
    {
        await using MedicalAppointmentsDbContext dbContext = CreateRealDbContext();
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(
            CancellationToken.None);
        try
        {
            string suffix = Guid.NewGuid().ToString("N");
            var specialty = new Specialty(Guid.NewGuid(), $"AdminDoctorReader test specialty {suffix}");
            dbContext.Specialties.Add(specialty);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            Guid activeUserId = await RealDatabaseSeeding.InsertDoctorProfileAsync(
                dbContext, "Sofía", "Vega", $"admin-doctor-active-{suffix}@example.test", CancellationToken.None);
            Guid inactiveUserId = await RealDatabaseSeeding.InsertDoctorProfileAsync(
                dbContext, "Marco", "Díaz", $"admin-doctor-inactive-{suffix}@example.test", CancellationToken.None);

            var activeDoctor = new Doctor(Guid.NewGuid(), activeUserId, specialty.Id);
            var inactiveDoctor = new Doctor(Guid.NewGuid(), inactiveUserId, specialty.Id);
            inactiveDoctor.SetActive(false);
            dbContext.Doctors.AddRange(activeDoctor, inactiveDoctor);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var reader = new AdminDoctorReader(dbContext);

            // GetAllAsync has no filter and no scoping - it can legitimately return other doctors
            // already present in this database, so every assertion below is scoped by the exact
            // ids just seeded rather than assuming an empty/known-size result set.
            IReadOnlyList<AdminDoctorItem> items = await reader.GetAllAsync(CancellationToken.None);

            AdminDoctorItem activeItem = Assert.Single(items, item => item.Id == activeDoctor.Id);
            Assert.Equal(activeUserId, activeItem.UserId);
            Assert.Equal("Sofía", activeItem.FirstName);
            Assert.Equal("Vega", activeItem.LastName);
            Assert.Equal($"admin-doctor-active-{suffix}@example.test", activeItem.Email);
            Assert.Equal(specialty.Id, activeItem.SpecialtyId);
            Assert.Equal(specialty.Name, activeItem.SpecialtyName);
            Assert.True(activeItem.Active);
            Assert.NotEqual(0u, activeItem.Version);

            AdminDoctorItem inactiveItem = Assert.Single(items, item => item.Id == inactiveDoctor.Id);
            Assert.Equal("Marco", inactiveItem.FirstName);
            Assert.Equal("Díaz", inactiveItem.LastName);
            Assert.False(inactiveItem.Active);
        }
        finally
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
    }

    private static MedicalAppointmentsDbContext CreateRealDbContext() =>
        new(RealDatabaseConnection.BuildOptions(RealDatabaseConnection.GetRequiredLocalConnectionString()));
}
