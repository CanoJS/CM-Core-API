using MedicalAppointments.Domain.Common;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Domain.UnitTests.Users;

public sealed class UserProfileTests
{
    [Fact]
    public void Constructor_WithValidData_NormalizesProfile()
    {
        var profile = new UserProfile(
            Guid.NewGuid(),
            "  Ana  ",
            "  López  ",
            "  ANA@EXAMPLE.COM  ",
            UserRole.Patient);

        Assert.Equal("Ana", profile.FirstName);
        Assert.Equal("López", profile.LastName);
        Assert.Equal("ana@example.com", profile.Email);
        Assert.Equal(UserRole.Patient, profile.Role);
        Assert.True(profile.Active);
    }

    [Theory]
    [InlineData("", "López")]
    [InlineData("Ana", "")]
    [InlineData(" ", "López")]
    [InlineData("Ana", " ")]
    public void Constructor_WithMissingName_Throws(string firstName, string lastName)
    {
        Action action = () => _ = new UserProfile(
            Guid.NewGuid(),
            firstName,
            lastName,
            "ana@example.com",
            UserRole.Patient);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Constructor_WithNameLongerThanLimit_Throws()
    {
        Action action = () => _ = new UserProfile(
            Guid.NewGuid(),
            new string('A', 101),
            "López",
            "ana@example.com",
            UserRole.Patient);

        Assert.Throws<DomainException>(action);
    }
}
