namespace MedicalAppointments.Application.Abstractions.Persistence;

public interface IDoctorRepository
{
    Task<bool> IsActiveAsync(Guid doctorId, CancellationToken cancellationToken);
}
