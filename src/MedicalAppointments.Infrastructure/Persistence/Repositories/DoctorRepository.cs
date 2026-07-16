using MedicalAppointments.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MedicalAppointments.Infrastructure.Persistence.Repositories;

public sealed class DoctorRepository(MedicalAppointmentsDbContext dbContext) : IDoctorRepository
{
    public Task<bool> IsActiveAsync(Guid doctorId, CancellationToken cancellationToken) =>
        dbContext.Doctors.AnyAsync(
            doctor => doctor.Id == doctorId && doctor.Active,
            cancellationToken);
}
