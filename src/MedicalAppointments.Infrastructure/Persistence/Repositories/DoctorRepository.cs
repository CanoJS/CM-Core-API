using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Domain.Doctors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace MedicalAppointments.Infrastructure.Persistence.Repositories;

public sealed class DoctorRepository(MedicalAppointmentsDbContext dbContext) : IDoctorRepository
{
    public Task<bool> IsActiveAsync(Guid doctorId, CancellationToken cancellationToken) =>
        dbContext.Doctors.AnyAsync(
            doctor => doctor.Id == doctorId && doctor.Active,
            cancellationToken);

    public void Add(Doctor doctor) => dbContext.Doctors.Add(doctor);

    public Task<Doctor?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Doctors.FirstOrDefaultAsync(doctor => doctor.Id == id, cancellationToken);

    public void PrepareStatusUpdate(Doctor doctor, uint version)
    {
        EntityEntry<Doctor> entry = dbContext.Entry(doctor);
        entry.Property(entity => entity.Active).IsModified = true;
        entry.Property(entity => entity.Version).OriginalValue = version;
    }

    public void PrepareSpecialtyUpdate(Doctor doctor, uint version)
    {
        EntityEntry<Doctor> entry = dbContext.Entry(doctor);
        entry.Property(entity => entity.SpecialtyId).IsModified = true;
        entry.Property(entity => entity.Version).OriginalValue = version;
    }
}
