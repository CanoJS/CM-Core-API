using MedicalAppointments.Domain.Common;
using MedicalAppointments.Domain.Specialties;

namespace MedicalAppointments.Domain.UnitTests.Specialties;

public sealed class SpecialtyTests
{
    [Fact]
    public void Constructor_TrimsName()
    {
        var specialty = new Specialty(Guid.NewGuid(), "  Pediatría  ");

        Assert.Equal("Pediatría", specialty.Name);
        Assert.True(specialty.Active);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyName_Throws(string name)
    {
        Assert.Throws<DomainException>(() => new Specialty(Guid.NewGuid(), name));
    }

    [Fact]
    public void Constructor_WithNameLongerThan120Characters_Throws()
    {
        string name = new string('a', 121);

        Assert.Throws<DomainException>(() => new Specialty(Guid.NewGuid(), name));
    }

    [Fact]
    public void Constructor_WithNameOf120Characters_DoesNotThrow()
    {
        string name = new string('a', 120);

        var specialty = new Specialty(Guid.NewGuid(), name);

        Assert.Equal(name, specialty.Name);
    }

    [Fact]
    public void Deactivate_SetsActiveToFalse()
    {
        var specialty = new Specialty(Guid.NewGuid(), "Cardiología");

        specialty.Deactivate();

        Assert.False(specialty.Active);
    }

    [Fact]
    public void Activate_SetsActiveToTrue()
    {
        var specialty = new Specialty(Guid.NewGuid(), "Cardiología");
        specialty.Deactivate();

        specialty.Activate();

        Assert.True(specialty.Active);
    }
}
