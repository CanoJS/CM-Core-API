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

    [RealDatabaseFact]
    public async Task GetByIdForUpdateAsync_BlocksConcurrentDeactivate_UntilTransactionCompletes_AgainstRealDatabase()
    {
        string connectionString = GetRequiredLocalConnectionString();
        DbContextOptions<MedicalAppointmentsDbContext> options = new DbContextOptionsBuilder<MedicalAppointmentsDbContext>()
            .UseNpgsql(
                connectionString,
                postgres => postgres.MigrationsHistoryTable("__ef_migrations_history", "medical"))
            .Options;

        await using var seedContext = new MedicalAppointmentsDbContext(options);
        var specialty = new Specialty(Guid.NewGuid(), $"Concurrency test {Guid.NewGuid():N}");
        new SpecialtyRepository(seedContext).Add(specialty);
        await seedContext.SaveChangesAsync(CancellationToken.None);

        try
        {
            await using var lockingContext = new MedicalAppointmentsDbContext(options);
            var lockingRepository = new SpecialtyRepository(lockingContext);
            await using IDbContextTransaction lockingTransaction =
                await lockingContext.Database.BeginTransactionAsync(CancellationToken.None);

            // Row lock acquired here must block the concurrent deactivate below until this
            // transaction commits - this is what RegisterDoctor/ChangeDoctorSpecialty rely on
            // to serialize against another admin deactivating the specialty mid-flight.
            Specialty? locked = await lockingRepository.GetByIdForUpdateAsync(specialty.Id, CancellationToken.None);
            Assert.NotNull(locked);
            Assert.True(locked.Active);

            await using var deactivatingContext = new MedicalAppointmentsDbContext(options);
            var deactivatingRepository = new SpecialtyRepository(deactivatingContext);
            Task deactivateTask = Task.Run(async () =>
            {
                Specialty toDeactivate = await deactivatingRepository.GetByIdAsync(specialty.Id, CancellationToken.None)
                    ?? throw new InvalidOperationException("Seeded specialty is missing.");
                deactivatingRepository.PrepareStatusUpdate(toDeactivate, toDeactivate.Version);
                toDeactivate.Deactivate();
                await deactivatingContext.SaveChangesAsync(CancellationToken.None);
            });

            Task firstToComplete = await Task.WhenAny(deactivateTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.NotSame(deactivateTask, firstToComplete);

            await lockingTransaction.CommitAsync(CancellationToken.None);
            await deactivateTask;

            await using var readContext = new MedicalAppointmentsDbContext(options);
            Specialty? afterCommit = await new SpecialtyRepository(readContext)
                .GetByIdAsync(specialty.Id, CancellationToken.None);
            Assert.NotNull(afterCommit);
            Assert.False(afterCommit.Active);
        }
        finally
        {
            await using var cleanupContext = new MedicalAppointmentsDbContext(options);
            await cleanupContext.Database.ExecuteSqlInterpolatedAsync(
                $"delete from medical.specialties where id = {specialty.Id}",
                CancellationToken.None);
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
