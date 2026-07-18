using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Messaging;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Domain.Specialties;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.Specialties.ChangeSpecialtyStatus;

public sealed class ChangeSpecialtyStatusCommandHandler(
    ICurrentUser currentUser,
    ISpecialtyRepository specialtyRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ChangeSpecialtyStatusCommand, ChangeSpecialtyStatusResponse>
{
    public async Task<ChangeSpecialtyStatusResponse> Handle(
        ChangeSpecialtyStatusCommand command,
        CancellationToken cancellationToken)
    {
        if (currentUser.Role != UserRole.Admin)
        {
            throw new ForbiddenException("Only administrators can manage specialties.");
        }

        if (!SpecialtyVersion.TryParse(command.Version, out uint version))
        {
            throw new ArgumentException("The specialty version is invalid.");
        }

        Specialty specialty = await specialtyRepository.GetByIdAsync(command.SpecialtyId, cancellationToken)
            ?? throw new NotFoundException("The specialty does not exist.");

        specialtyRepository.PrepareStatusUpdate(specialty, version);

        if (command.Active)
        {
            specialty.Activate();
        }
        else
        {
            specialty.Deactivate();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ChangeSpecialtyStatusResponse(
            specialty.Id,
            specialty.Name,
            specialty.Active,
            SpecialtyVersion.ToToken(specialty.Version));
    }
}
