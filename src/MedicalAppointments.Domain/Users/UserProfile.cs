namespace MedicalAppointments.Domain.Users;

public sealed class UserProfile
{
    private UserProfile()
    {
    }

    public UserProfile(Guid id, string fullName, string email, UserRole role)
    {
        Id = id;
        FullName = fullName.Trim();
        Email = email.Trim().ToLowerInvariant();
        Role = role;
    }

    public Guid Id { get; private set; }

    public string FullName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    public bool Active { get; private set; } = true;

    public void Deactivate() => Active = false;
}
