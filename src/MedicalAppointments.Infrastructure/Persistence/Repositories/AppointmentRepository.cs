using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Domain.Appointments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace MedicalAppointments.Infrastructure.Persistence.Repositories;

public sealed class AppointmentRepository(MedicalAppointmentsDbContext dbContext) : IAppointmentRepository
{
    public Task<bool> HasScheduledAppointmentAsync(
        Guid doctorId,
        DateTimeOffset startsAt,
        Guid? excludeAppointmentId,
        CancellationToken cancellationToken) =>
        dbContext.Appointments.AnyAsync(
            appointment => appointment.DoctorId == doctorId
                && appointment.StartsAt == startsAt.ToUniversalTime()
                && appointment.Status == AppointmentStatus.Scheduled
                && (excludeAppointmentId == null || appointment.Id != excludeAppointmentId),
            cancellationToken);

    public void Add(Appointment appointment) => dbContext.Appointments.Add(appointment);

    public Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Appointments.FirstOrDefaultAsync(appointment => appointment.Id == id, cancellationToken);

    public void PrepareStatusUpdate(Appointment appointment, uint version)
    {
        EntityEntry<Appointment> entry = dbContext.Entry(appointment);
        entry.Property(entity => entity.Status).IsModified = true;
        entry.Property(entity => entity.Version).OriginalValue = version;
    }

    public void PrepareRescheduleUpdate(Appointment appointment, uint version)
    {
        EntityEntry<Appointment> entry = dbContext.Entry(appointment);
        entry.Property(entity => entity.DoctorId).IsModified = true;
        entry.Property(entity => entity.StartsAt).IsModified = true;
        entry.Property(entity => entity.Version).OriginalValue = version;
    }
}
