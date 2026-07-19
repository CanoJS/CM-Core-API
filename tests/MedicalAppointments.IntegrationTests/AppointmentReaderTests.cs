using MedicalAppointments.Domain.Appointments;
using MedicalAppointments.Infrastructure.Persistence;
using MedicalAppointments.Infrastructure.Persistence.Readers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MedicalAppointments.IntegrationTests;

// Regression coverage for two compounding translation issues that used to crash both
// GET /api/v1/appointments and GET .../{id} in production: DateTimeOffset.AddMinutes inside the
// multi-join projection, and (found while fixing that) chaining .OrderBy(row => row.StartsAt)
// after the projection - EF Core re-inlines the whole constructed AppointmentRow into the ORDER
// BY clause and cannot translate it, independent of whether AddMinutes is present. No test here
// seeds real `appointments` rows - patient_id/doctor_id both chain to medical.user_profiles ->
// auth.users, which only a real Supabase Auth user can satisfy, and creating one in an automated
// test is out of scope (same limitation documented on AppointmentRepositoryConcurrencyTests).
public sealed class AppointmentReaderTests
{
    private static readonly DateTimeOffset StartsAt = new(2026, 7, 20, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ToListItem_ComputesEndsAtAsStartsAtPlusDurationMinutes()
    {
        var row = new AppointmentReader.AppointmentRow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Ana",
            "López",
            Guid.NewGuid(),
            "Luis",
            "García",
            Guid.NewGuid(),
            "Pediatría",
            StartsAt,
            AppointmentStatus.Scheduled,
            "Consulta general",
            null,
            StartsAt,
            StartsAt,
            1);

        var item = AppointmentReader.ToListItem(row);

        Assert.Equal(StartsAt, item.StartsAt);
        Assert.Equal(StartsAt.AddMinutes(Appointment.DurationMinutes), item.EndsAt);
    }

    [Theory]
    [MemberData(nameof(FilterCombinations))]
    public void BuildListQuery_WithOrWithoutFilters_TranslatesToSqlWithoutThrowing(
        Guid? patientId,
        Guid? doctorId,
        AppointmentStatus? status,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtcExclusive,
        string? patientNameContains)
    {
        var reader = new AppointmentReader(CreateUnconnectedDbContext());

        // ToQueryString() forces full expression-tree translation without opening a connection -
        // it is exactly what threw InvalidOperationException in production when the projection
        // still contained AddMinutes. A translation failure here would throw before any string
        // is produced, so simply not throwing is the assertion.
        string sql = reader
            .BuildListQuery(patientId, doctorId, status, fromUtc, toUtcExclusive, patientNameContains)
            .ToQueryString();

        Assert.Contains("ORDER BY", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildListQuery_WithPatientIdFilter_IncludesPatientIdInGeneratedSql()
    {
        var reader = new AppointmentReader(CreateUnconnectedDbContext());

        string sql = reader.BuildListQuery(Guid.NewGuid(), null, null, null, null).ToQueryString();

        Assert.Contains("patient_id", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildListQuery_WithDoctorIdFilter_IncludesDoctorIdInGeneratedSql()
    {
        var reader = new AppointmentReader(CreateUnconnectedDbContext());

        string sql = reader.BuildListQuery(null, Guid.NewGuid(), null, null, null).ToQueryString();

        Assert.Contains("doctor_id", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildListQuery_WithPatientNameFilter_TranslatesToIlikeInGeneratedSql()
    {
        var reader = new AppointmentReader(CreateUnconnectedDbContext());

        string sql = reader.BuildListQuery(null, null, null, null, null, "ana").ToQueryString();

        Assert.Contains("ILIKE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ChainingOrderByAfterTheProjection_FailsToTranslateToSql()
    {
        // Reproduces the second issue found while fixing the reported bug: ordering by a member
        // of the type Project() just constructed (rather than by `appointment.StartsAt`/`.Id`
        // directly, which is what BuildListQuery/Project actually do - see the `orderby` comment
        // there) cannot be translated once the query is composed of four joins. This is what
        // guards against that regression sneaking back in.
        using MedicalAppointmentsDbContext dbContext = CreateUnconnectedDbContext();

        IQueryable<AppointmentReader.AppointmentRow> untranslatable =
            (from appointment in dbContext.Appointments.AsNoTracking()
             join patientProfile in dbContext.UserProfiles.AsNoTracking()
                 on appointment.PatientId equals patientProfile.Id
             join doctor in dbContext.Doctors.AsNoTracking() on appointment.DoctorId equals doctor.Id
             join doctorProfile in dbContext.UserProfiles.AsNoTracking() on doctor.UserId equals doctorProfile.Id
             join specialty in dbContext.Specialties.AsNoTracking() on doctor.SpecialtyId equals specialty.Id
             select new AppointmentReader.AppointmentRow(
                 appointment.Id,
                 appointment.PatientId,
                 patientProfile.FirstName,
                 patientProfile.LastName,
                 appointment.DoctorId,
                 doctorProfile.FirstName,
                 doctorProfile.LastName,
                 specialty.Id,
                 specialty.Name,
                 appointment.StartsAt,
                 appointment.Status,
                 appointment.Reason,
                 appointment.MedicalNote,
                 appointment.CreatedAt,
                 appointment.UpdatedAt,
                 appointment.Version))
            .OrderBy(row => row.StartsAt)
            .ThenBy(row => row.Id);

        Assert.Throws<InvalidOperationException>(() => untranslatable.ToQueryString());
    }

    [RealDatabaseFact]
    public async Task GetAsync_AgainstRealDatabase_AppliesFiltersWithoutTranslationFailure()
    {
        await using MedicalAppointmentsDbContext dbContext = CreateRealDbContext();
        var reader = new AppointmentReader(dbContext);

        IReadOnlyList<Application.Abstractions.Queries.AppointmentListItem> result = await reader.GetAsync(
            patientId: Guid.NewGuid(),
            doctorId: Guid.NewGuid(),
            status: AppointmentStatus.Scheduled,
            fromUtc: StartsAt,
            toUtcExclusive: StartsAt.AddDays(1),
            patientNameContains: "ana",
            CancellationToken.None);

        // No seeded row can match these random ids: an empty result (not an exception) proves the
        // whole filtered/joined/ordered/projected query executed successfully against real Npgsql.
        Assert.Empty(result);
    }

    [RealDatabaseFact]
    public async Task GetByIdAsync_AgainstRealDatabase_ReturnsNullWithoutTranslationFailure()
    {
        await using MedicalAppointmentsDbContext dbContext = CreateRealDbContext();
        var reader = new AppointmentReader(dbContext);

        Application.Abstractions.Queries.AppointmentListItem? result = await reader.GetByIdAsync(
            Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }

    public static TheoryData<Guid?, Guid?, AppointmentStatus?, DateTimeOffset?, DateTimeOffset?, string?> FilterCombinations() =>
        new()
        {
            { null, null, null, null, null, null },
            { Guid.NewGuid(), null, null, null, null, null },
            { null, Guid.NewGuid(), null, null, null, null },
            { null, null, AppointmentStatus.Scheduled, null, null, null },
            { null, null, null, StartsAt, StartsAt.AddDays(31), null },
            { Guid.NewGuid(), Guid.NewGuid(), AppointmentStatus.Attended, StartsAt, StartsAt.AddDays(31), "ana" },
        };

    private static MedicalAppointmentsDbContext CreateUnconnectedDbContext()
    {
        // Without a live connection, Npgsql can't auto-detect the server version (it normally
        // does this on first connect), and defaults to a conservative capability set that
        // translates this query differently than a real, version-known Postgres would - pin the
        // same version Supabase runs so ToQueryString() here matches what actually happens
        // against the real database.
        DbContextOptions<MedicalAppointmentsDbContext> options = new DbContextOptionsBuilder<MedicalAppointmentsDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=unused;Username=unused;Password=unused",
                npgsql => npgsql.SetPostgresVersion(new Version(15, 1)))
            .Options;
        return new MedicalAppointmentsDbContext(options);
    }

    private static MedicalAppointmentsDbContext CreateRealDbContext()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();
        string connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException(
                "RUN_REAL_DB_TESTS=true requires ConnectionStrings:Database via user-secrets or an environment variable.");

        DbContextOptions<MedicalAppointmentsDbContext> options = new DbContextOptionsBuilder<MedicalAppointmentsDbContext>()
            .UseNpgsql(
                connectionString,
                postgres => postgres.MigrationsHistoryTable("__ef_migrations_history", "medical"))
            .Options;
        return new MedicalAppointmentsDbContext(options);
    }
}
