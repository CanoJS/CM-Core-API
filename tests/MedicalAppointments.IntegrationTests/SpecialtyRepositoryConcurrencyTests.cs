using MedicalAppointments.Domain.Specialties;
using MedicalAppointments.Infrastructure.Persistence;
using MedicalAppointments.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;

namespace MedicalAppointments.IntegrationTests;

public sealed class SpecialtyRepositoryConcurrencyTests
{
    [Fact]
    public void PrepareStatusUpdate_MarksActiveModifiedAndSetsOriginalVersion()
    {
        DbContextOptions<MedicalAppointmentsDbContext> options = new DbContextOptionsBuilder<MedicalAppointmentsDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options;
        using var dbContext = new MedicalAppointmentsDbContext(options);
        var specialty = new Specialty(Guid.NewGuid(), "Pediatría");
        dbContext.Attach(specialty);
        var repository = new SpecialtyRepository(dbContext);

        repository.PrepareStatusUpdate(specialty, 42);

        EntityEntry<Specialty> entry = dbContext.Entry(specialty);
        Assert.True(entry.Property(s => s.Active).IsModified);
        Assert.Equal(42u, entry.Property(s => s.Version).OriginalValue);
    }

    [RealDatabaseFact]
    public async Task ChangeStatus_WithStaleVersionAndUnchangedState_ThrowsConcurrencyConflict_AgainstRealDatabase()
    {
        string connectionString = GetRequiredLocalConnectionString();

        DbContextOptions<MedicalAppointmentsDbContext> options = new DbContextOptionsBuilder<MedicalAppointmentsDbContext>()
            .UseNpgsql(
                connectionString,
                postgres => postgres.MigrationsHistoryTable("__ef_migrations_history", "medical"))
            .Options;

        await using var dbContext = new MedicalAppointmentsDbContext(options);
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(
            CancellationToken.None);
        try
        {
            var repository = new SpecialtyRepository(dbContext);
            var specialty = new Specialty(Guid.NewGuid(), $"Concurrency test {Guid.NewGuid():N}");
            repository.Add(specialty);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            uint staleVersion = specialty.Version;
            Assert.NotEqual(0u, staleVersion);

            // Bumps xmin without changing `active`, reproducing the race: a concurrent write
            // lands between this read and the PATCH that still thinks staleVersion is current.
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"update medical.specialties set active = active where id = {specialty.Id}",
                CancellationToken.None);

            repository.PrepareStatusUpdate(specialty, staleVersion);
            specialty.Activate();

            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
                dbContext.SaveChangesAsync(CancellationToken.None));
        }
        finally
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
    }

    [RealDatabaseFact]
    public async Task ChangeStatus_WithCurrentVersion_ReturnsNewVersionAfterUpdate_AgainstRealDatabase()
    {
        string connectionString = GetRequiredLocalConnectionString();

        DbContextOptions<MedicalAppointmentsDbContext> options = new DbContextOptionsBuilder<MedicalAppointmentsDbContext>()
            .UseNpgsql(
                connectionString,
                postgres => postgres.MigrationsHistoryTable("__ef_migrations_history", "medical"))
            .Options;

        await using var dbContext = new MedicalAppointmentsDbContext(options);
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(
            CancellationToken.None);
        try
        {
            var repository = new SpecialtyRepository(dbContext);
            var specialty = new Specialty(Guid.NewGuid(), $"Concurrency test {Guid.NewGuid():N}");
            repository.Add(specialty);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            uint originalVersion = specialty.Version;

            repository.PrepareStatusUpdate(specialty, originalVersion);
            specialty.Deactivate();
            await dbContext.SaveChangesAsync(CancellationToken.None);

            Assert.NotEqual(originalVersion, specialty.Version);
            Assert.False(specialty.Active);
        }
        finally
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
    }

    private static string GetRequiredLocalConnectionString()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        return configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException(
                "RUN_REAL_DB_TESTS=true requires ConnectionStrings:Database via user-secrets or an environment variable.");
    }
}
