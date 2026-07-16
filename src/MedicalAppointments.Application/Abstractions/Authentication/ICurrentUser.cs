using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.Abstractions.Authentication;

public interface ICurrentUser
{
    Guid UserId { get; }

    UserRole Role { get; }
}
