namespace MedicalAppointments.IntegrationTests;

// Requires explicit opt-in (RUN_REAL_DB_TESTS=true) so tests never connect to a real database
// just because a developer happens to have Supabase user-secrets configured locally. Setting
// Skip in the constructor makes xunit report the test as genuinely Skipped, not Passed.
public sealed class RealDatabaseFactAttribute : FactAttribute
{
    public RealDatabaseFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_REAL_DB_TESTS") != "true")
        {
            Skip = "Set RUN_REAL_DB_TESTS=true to run tests against a real Postgres database.";
        }
    }
}
