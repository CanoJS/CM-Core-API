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

        Id = id;
        Name = name.Trim();
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public bool Active { get; private set; } = true;

    public void Deactivate() => Active = false;
}
