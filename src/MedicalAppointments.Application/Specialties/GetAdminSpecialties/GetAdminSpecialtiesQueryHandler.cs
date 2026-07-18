using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Messaging;
using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.Specialties.GetAdminSpecialties;

public sealed class GetAdminSpecialtiesQueryHandler(
    ICurrentUser currentUser,
    IAdminSpecialtyReader adminSpecialtyReader)
    : IQueryHandler<GetAdminSpecialtiesQuery, IReadOnlyList<AdminSpecialtyResponse>>
{
    public async Task<IReadOnlyList<AdminSpecialtyResponse>> Handle(
        GetAdminSpecialtiesQuery query,
        CancellationToken cancellationToken)
    {
        if (currentUser.Role != UserRole.Admin)
        {
            throw new ForbiddenException("Only administrators can view the specialty catalog.");
        }

        IReadOnlyList<AdminSpecialtyItem> specialties =
            await adminSpecialtyReader.GetAllAsync(cancellationToken);

        return specialties
            .Select(specialty => new AdminSpecialtyResponse(
                specialty.Id,
                specialty.Name,
                specialty.Active,
                SpecialtyVersion.ToToken(specialty.Version)))
            .ToArray();
    }
}
