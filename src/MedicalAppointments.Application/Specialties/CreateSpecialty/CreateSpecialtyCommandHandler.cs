using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Messaging;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Domain.Specialties;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.Specialties.CreateSpecialty;

public sealed class CreateSpecialtyCommandHandler(
    ICurrentUser currentUser,
    ISpecialtyRepository specialtyRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateSpecialtyCommand, CreateSpecialtyResponse>
{
    public async Task<CreateSpecialtyResponse> Handle(
        CreateSpecialtyCommand command,
        CancellationToken cancellationToken)
    {
        if (currentUser.Role != UserRole.Admin)
        {
            throw new ForbiddenException("Only administrators can manage specialties.");
        }

        var specialty = new Specialty(Guid.NewGuid(), command.Name);

        if (await specialtyRepository.ExistsByNameAsync(specialty.Name, cancellationToken))
        {
            throw new ConflictException("A specialty with this name already exists.");
        }

        specialtyRepository.Add(specialty);

        // The unique index ux_specialties_name on lower(name) is the definitive guard against races;
        // the check above only improves the error message for the common case.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateSpecialtyResponse(
            specialty.Id,
            specialty.Name,
            specialty.Active,
            SpecialtyVersion.ToToken(specialty.Version));
    }
}
