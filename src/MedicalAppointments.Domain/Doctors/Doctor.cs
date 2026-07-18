using MedicalAppointments.Domain.Common;

namespace MedicalAppointments.Domain.Doctors;

public sealed class Doctor
{
    private Doctor()
    {
    }

    public Doctor(Guid id, Guid userId, Guid specialtyId)
    {
        if (id == Guid.Empty || userId == Guid.Empty || specialtyId == Guid.Empty)
        {
            throw new DomainException("Doctor identifiers are required.");
        }

        Id = id;
        UserId = userId;
        SpecialtyId = specialtyId;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid SpecialtyId { get; private set; }

    public bool Active { get; private set; } = true;

    public uint Version { get; private set; }

    public void ChangeSpecialty(Guid specialtyId)
    {
        if (specialtyId == Guid.Empty)
        {
            throw new DomainException("Specialty is required.");
        }

        SpecialtyId = specialtyId;
    }

    public void SetActive(bool active) => Active = active;
}
