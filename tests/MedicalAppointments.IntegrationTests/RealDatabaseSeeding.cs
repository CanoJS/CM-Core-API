using System.Text.Json;
using MedicalAppointments.Domain.Users;
using MedicalAppointments.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MedicalAppointments.IntegrationTests;

// Shared helpers for [RealDatabaseFact] tests that need a row satisfying medical.user_profiles'
// foreign key to auth.users(id) - required by any reader that joins through doctors/user_profiles
// (AppointmentReader, AdminDoctorReader, DoctorCatalogReader).
//
// RUN_REAL_DB_TESTS=true only ever targets a local Supabase CLI stack (README "Docker Desktop
// para ejecutar Supabase localmente" / `npx supabase start`), never a hosted project - so
// inserting a throwaway auth.users row directly via SQL, inside the same transaction the caller
// rolls back at the end, never creates a real Supabase Auth account and never calls GoTrue's
// HTTP API. This is a different thing from the Supabase Auth Admin API
// (POST /auth/v1/admin/users) that docs/SECURITY.md forbids test code from calling: that call
// creates a real, non-transactional account outside our EF transaction that Rollback cannot undo.
// A raw INSERT on the same connection/transaction is fully undone by that same Rollback.
//
// The column list mirrors Supabase's standard `auth.users` schema (stable across the GoTrue
// versions the Supabase CLI ships locally) and supplies exactly what
// medical.handle_new_auth_user() (the on_auth_user_created trigger, see
// supabase/migrations/20260717172033_split_user_names_and_provision_profiles.sql) reads:
// raw_user_meta_data's first_name/last_name and email. That trigger provisions the matching
// medical.user_profiles row automatically with role=PATIENT, active=true; InsertDoctorProfileAsync
// promotes it afterwards with the existing UserProfile.PromoteToDoctor() domain method - a plain
// EF update against our own medical schema, not Supabase's.
internal static class RealDatabaseSeeding
{
    public static async Task<Guid> InsertPatientProfileAsync(
        MedicalAppointmentsDbContext dbContext,
        string firstName,
        string lastName,
        string email,
        CancellationToken cancellationToken) =>
        await InsertAuthUserAsync(dbContext, email, firstName, lastName, cancellationToken);

    public static async Task<Guid> InsertDoctorProfileAsync(
        MedicalAppointmentsDbContext dbContext,
        string firstName,
        string lastName,
        string email,
        CancellationToken cancellationToken)
    {
        Guid userId = await InsertAuthUserAsync(dbContext, email, firstName, lastName, cancellationToken);

        UserProfile profile = await dbContext.UserProfiles.SingleAsync(
            p => p.Id == userId, cancellationToken);
        profile.PromoteToDoctor();
        await dbContext.SaveChangesAsync(cancellationToken);

        return userId;
    }

    private static async Task<Guid> InsertAuthUserAsync(
        MedicalAppointmentsDbContext dbContext,
        string email,
        string firstName,
        string lastName,
        CancellationToken cancellationToken)
    {
        Guid id = Guid.NewGuid();
        string metadata = JsonSerializer.Serialize(new { first_name = firstName, last_name = lastName });

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $@"insert into auth.users
                   (id, instance_id, aud, role, email, encrypted_password, raw_user_meta_data, created_at, updated_at)
               values
                   ({id}, '00000000-0000-0000-0000-000000000000'::uuid, 'authenticated', 'authenticated',
                    {email}, '', {metadata}::jsonb, now(), now())",
            cancellationToken);

        return id;
    }
}

// Duplicated per real-db-backed reader test file in this codebase's existing style (see
// RealDatabaseFactAttribute usages) rather than folded into each file separately: this connection
// lookup is identical everywhere it is used, so it is centralized here instead of being retyped a
// third time.
internal static class RealDatabaseConnection
{
    public static string GetRequiredLocalConnectionString()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        return configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException(
                "RUN_REAL_DB_TESTS=true requires ConnectionStrings:Database via user-secrets or an environment variable.");
    }

    public static DbContextOptions<MedicalAppointmentsDbContext> BuildOptions(string connectionString) =>
        new DbContextOptionsBuilder<MedicalAppointmentsDbContext>()
            .UseNpgsql(
                connectionString,
                postgres => postgres.MigrationsHistoryTable("__ef_migrations_history", "medical"))
            .Options;
}
