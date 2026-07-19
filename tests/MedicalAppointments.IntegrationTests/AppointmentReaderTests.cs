using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Domain.Appointments;
using MedicalAppointments.Domain.Doctors;
using MedicalAppointments.Domain.Specialties;
using MedicalAppointments.Infrastructure.Persistence;
using MedicalAppointments.Infrastructure.Persistence.Readers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;

namespace MedicalAppointments.IntegrationTests;

// Regression coverage for two compounding translation issues that used to crash both
// GET /api/v1/appointments and GET .../{id} in production: DateTimeOffset.AddMinutes inside the
// multi-join projection, and (found while fixing that) chaining .OrderBy(row => row.StartsAt)
// after the projection - EF Core re-inlines the whole constructed AppointmentRow into the ORDER
// BY clause and cannot translate it, independent of whether AddMinutes is present. The
// ToQueryString() tests below prove translation without a live connection; the real-database
// tests further down (GetAsync_AgainstRealDatabase_ReturnsSeededDataScopedByPatientAndDoctor)
// additionally materialize real rows against Postgres, satisfying appointments' FK chain to
// auth.users via RealDatabaseSeeding (see that file for why this is safe under
// RUN_REAL_DB_TESTS=true).
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

    [RealDatabaseFact]
    public async Task GetAsync_AgainstRealDatabase_ReturnsSeededDataScopedByPatientAndDoctor()
    {
        await using MedicalAppointmentsDbContext dbContext = CreateRealDbContext();
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(
            CancellationToken.None);
        try
        {
            string suffix = Guid.NewGuid().ToString("N");
            var specialty = new Specialty(Guid.NewGuid(), $"Reader test specialty {suffix}");
            dbContext.Specialties.Add(specialty);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            Guid doctorAUserId = await RealDatabaseSeeding.InsertDoctorProfileAsync(
                dbContext, "Ana", "López", $"reader-doctor-a-{suffix}@example.test", CancellationToken.None);
            Guid doctorBUserId = await RealDatabaseSeeding.InsertDoctorProfileAsync(
                dbContext, "Sofía", "Vega", $"reader-doctor-b-{suffix}@example.test", CancellationToken.None);
            Guid patientAUserId = await RealDatabaseSeeding.InsertPatientProfileAsync(
                dbContext, "Carlos", "Ruiz", $"reader-patient-a-{suffix}@example.test", CancellationToken.None);
            Guid patientBUserId = await RealDatabaseSeeding.InsertPatientProfileAsync(
                dbContext, "Laura", "Gómez", $"reader-patient-b-{suffix}@example.test", CancellationToken.None);

            var doctorA = new Doctor(Guid.NewGuid(), doctorAUserId, specialty.Id);
            var doctorB = new Doctor(Guid.NewGuid(), doctorBUserId, specialty.Id);
            dbContext.Doctors.AddRange(doctorA, doctorB);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            // 2026-07-20 is a Monday within clinic hours (08:00-18:00 America/Mexico_City) -
            // required by the ck_appointments_clinic_hours CHECK constraint, which only exists in
            // Postgres, not in the C# domain model.
            DateTimeOffset scheduledSlot = new(2026, 7, 20, 15, 0, 0, TimeSpan.Zero);
            DateTimeOffset attendedSlot = new(2026, 7, 20, 16, 0, 0, TimeSpan.Zero);
            DateTimeOffset otherDoctorSlot = new(2026, 7, 20, 17, 0, 0, TimeSpan.Zero);

            Appointment scheduled = Appointment.Schedule(
                patientAUserId, doctorA.Id, scheduledSlot, "Control anual", scheduledSlot.AddDays(-1));
            Appointment attended = Appointment.Schedule(
                patientAUserId, doctorA.Id, attendedSlot, "Revisión", attendedSlot.AddDays(-1));
            attended.Attend("Paciente estable, sin hallazgos.", attendedSlot.AddMinutes(30));
            Appointment otherDoctorAppointment = Appointment.Schedule(
                patientBUserId, doctorB.Id, otherDoctorSlot, "Consulta", otherDoctorSlot.AddDays(-1));

            dbContext.Appointments.AddRange(scheduled, attended, otherDoctorAppointment);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var reader = new AppointmentReader(dbContext);

            IReadOnlyList<AppointmentListItem> byPatientA = await reader.GetAsync(
                patientAUserId, null, null, null, null, null, CancellationToken.None);

            Assert.Equal(2, byPatientA.Count);
            Assert.DoesNotContain(byPatientA, item => item.Id == otherDoctorAppointment.Id);

            AppointmentListItem scheduledItem = Assert.Single(byPatientA, item => item.Id == scheduled.Id);
            Assert.Equal("Carlos", scheduledItem.PatientFirstName);
            Assert.Equal("Ruiz", scheduledItem.PatientLastName);
            Assert.Equal("Ana", scheduledItem.DoctorFirstName);
            Assert.Equal("López", scheduledItem.DoctorLastName);
            Assert.Equal(specialty.Name, scheduledItem.SpecialtyName);
            Assert.Equal(AppointmentStatus.Scheduled, scheduledItem.Status);
            Assert.Equal("Control anual", scheduledItem.Reason);
            Assert.Null(scheduledItem.MedicalNote);

            AppointmentListItem attendedItem = Assert.Single(byPatientA, item => item.Id == attended.Id);
            Assert.Equal(AppointmentStatus.Attended, attendedItem.Status);
            Assert.Equal("Paciente estable, sin hallazgos.", attendedItem.MedicalNote);

            // Scoping by doctorId must exclude the other doctor's appointment even though it
            // shares the same specialty and even though patient B also exists in the database.
            IReadOnlyList<AppointmentListItem> byDoctorA = await reader.GetAsync(
                null, doctorA.Id, null, null, null, null, CancellationToken.None);
            Assert.Equal(2, byDoctorA.Count);
            Assert.DoesNotContain(byDoctorA, item => item.Id == otherDoctorAppointment.Id);

            // patientNameContains is partial + case-insensitive (ILIKE), and combines with
            // doctorId rather than replacing it: "his own doctor's history" scoping.
            IReadOnlyList<AppointmentListItem> matchingDoctorAByName = await reader.GetAsync(
                null, doctorA.Id, null, null, null, "ARLO", CancellationToken.None);
            Assert.Equal(2, matchingDoctorAByName.Count);

            IReadOnlyList<AppointmentListItem> nonMatchingDoctorAByName = await reader.GetAsync(
                null, doctorA.Id, null, null, null, "gómez", CancellationToken.None);
            Assert.Empty(nonMatchingDoctorAByName);
        }
        finally
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
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
