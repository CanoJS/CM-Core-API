using MedicalAppointments.Application.Abstractions.Messaging;

namespace MedicalAppointments.Application.Users.GetCurrentUser;

public sealed record GetCurrentUserQuery : IQuery<CurrentUserResponse>;

public sealed record CurrentUserResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Role,
    bool Active);
