using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Messaging;
using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Application.Common.Exceptions;

namespace MedicalAppointments.Application.Users.GetCurrentUser;

public sealed class GetCurrentUserQueryHandler(
    ICurrentUser currentUser,
    IUserProfileReader userProfileReader)
    : IQueryHandler<GetCurrentUserQuery, CurrentUserResponse>
{
    public async Task<CurrentUserResponse> Handle(
        GetCurrentUserQuery query,
        CancellationToken cancellationToken)
    {
        UserProfileSnapshot profile = await userProfileReader.GetByIdAsync(
            currentUser.UserId,
            cancellationToken)
            ?? throw new NotFoundException("The authenticated user profile does not exist.");

        if (!profile.Active)
        {
            throw new ForbiddenException("The authenticated user profile is inactive.");
        }

        return new CurrentUserResponse(
            profile.Id,
            profile.FirstName,
            profile.LastName,
            profile.Email,
            profile.Role.ToString().ToUpperInvariant(),
            profile.Active);
    }
}
