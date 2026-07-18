using MedicalAppointments.Domain.Common;
using MedicalAppointments.Domain.Doctors;

namespace MedicalAppointments.Domain.UnitTests.Doctors;

public sealed class DoctorTests
{
    [Fact]
    public void Constructor_WithValidData_CreatesActiveDoctor()
    {
        var doctor = new Doctor(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        Assert.True(doctor.Active);
    }

    [Fact]
    public void Constructor_WithEmptyId_Throws()
    {
        Assert.Throws<DomainException>(() => new Doctor(Guid.Empty, Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public void Constructor_WithEmptyUserId_Throws()
    {
        Assert.Throws<DomainException>(() => new Doctor(Guid.NewGuid(), Guid.Empty, Guid.NewGuid()));
    }

    [Fact]
    public void Constructor_WithEmptySpecialtyId_Throws()
    {
        Assert.Throws<DomainException>(() => new Doctor(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty));
    }

    [Fact]
    public void SetActive_TogglesActive()
    {
        var doctor = new Doctor(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        doctor.SetActive(false);
        Assert.False(doctor.Active);

        doctor.SetActive(true);
        Assert.True(doctor.Active);
    }

    [Fact]
    public void ChangeSpecialty_WithValidId_UpdatesSpecialty()
    {
        var doctor = new Doctor(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Guid newSpecialtyId = Guid.NewGuid();

        doctor.ChangeSpecialty(newSpecialtyId);

        Assert.Equal(newSpecialtyId, doctor.SpecialtyId);
    }

    [Fact]
    public void ChangeSpecialty_WithEmptyId_Throws()
    {
        var doctor = new Doctor(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        Assert.Throws<DomainException>(() => doctor.ChangeSpecialty(Guid.Empty));
    }
}
