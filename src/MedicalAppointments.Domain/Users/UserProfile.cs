using MedicalAppointments.Domain.Common;

namespace MedicalAppointments.Domain.Users;

public sealed class UserProfile
{
    private UserProfile()
    {
    }

    public UserProfile(Guid id, string firstName, string lastName, string email, UserRole role)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("User identifier is required.");
        }

        if (string.IsNullOrWhiteSpace(firstName) || firstName.Trim().Length > 100)
        {
            throw new DomainException("First name is required and cannot exceed 100 characters.");
        }

        if (string.IsNullOrWhiteSpace(lastName) || lastName.Trim().Length > 100)
        {
            throw new DomainException("Last name is required and cannot exceed 100 characters.");
        }

        if (string.IsNullOrWhiteSpace(email) || email.Trim().Length > 320)
        {
            throw new DomainException("Email is required and cannot exceed 320 characters.");
        }

        Id = id;
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Email = email.Trim().ToLowerInvariant();
        Role = role;
    }

    public Guid Id { get; private set; }

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    public bool Active { get; private set; } = true;

    public void Activate() => Active = true;

    public void Deactivate() => Active = false;

    public void PromoteToDoctor()
    {
        if (Role != UserRole.Patient)
        {
            throw new DomainException("Only patient profiles can be promoted to doctor.");
        }

        Role = UserRole.Doctor;
    }
}
