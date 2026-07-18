using MedicalAppointments.Domain.Common;

namespace MedicalAppointments.Domain.Specialties;

public sealed class Specialty
{
    private Specialty()
    {
    }

    public Specialty(Guid id, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Specialty name is required.");
        }

        string trimmedName = name.Trim();
        if (trimmedName.Length > 120)
        {
            throw new DomainException("Specialty name cannot exceed 120 characters.");
        }

        Id = id;
        Name = trimmedName;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public bool Active { get; private set; } = true;

    public uint Version { get; private set; }

    public void Activate() => Active = true;

    public void Deactivate() => Active = false;
}
