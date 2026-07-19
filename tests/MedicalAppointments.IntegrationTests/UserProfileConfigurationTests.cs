using MedicalAppointments.Domain.Users;
using MedicalAppointments.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MedicalAppointments.IntegrationTests;

// Exercises the EF model's configured value converter directly - no database connection is
// opened, since building DbContext.Model is pure metadata and does not require one.
public sealed class UserProfileConfigurationTests
{
    [Theory]
    [InlineData(UserRole.Patient, "PATIENT")]
    [InlineData(UserRole.Doctor, "DOCTOR")]
    [InlineData(UserRole.Admin, "ADMIN")]
    public void RoleColumn_WritesUppercase_MatchingCheckConstraint(UserRole role, string expectedStoredValue)
    {
        ValueConverter converter = GetRoleValueConverter();

        object stored = converter.ConvertToProvider(role)!;

        // ck_user_profiles_role only accepts uppercase values ("DOCTOR", not "Doctor").
        Assert.Equal(expectedStoredValue, stored);
    }

    [Theory]
    [InlineData("DOCTOR", UserRole.Doctor)]
    [InlineData("doctor", UserRole.Doctor)]
    [InlineData("PATIENT", UserRole.Patient)]
    [InlineData("ADMIN", UserRole.Admin)]
    public void RoleColumn_ReadsStoredValueCaseInsensitively(string storedValue, UserRole expectedRole)
    {
        ValueConverter converter = GetRoleValueConverter();

        object role = converter.ConvertFromProvider(storedValue)!;

        Assert.Equal(expectedRole, role);
    }

    private static ValueConverter GetRoleValueConverter()
    {
        DbContextOptions<MedicalAppointmentsDbContext> options = new DbContextOptionsBuilder<MedicalAppointmentsDbContext>()
            .UseNpgsql("Host=localhost;Database=model-only;Username=test;Password=test")
            .Options;

        using var context = new MedicalAppointmentsDbContext(options);
        IEntityType entityType = context.Model.FindEntityType(typeof(UserProfile))!;
        IProperty roleProperty = entityType.FindProperty(nameof(UserProfile.Role))!;

        return roleProperty.GetValueConverter()!;
    }
}
