using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Domain.Appointments;
using Microsoft.EntityFrameworkCore;

namespace MedicalAppointments.Infrastructure.Persistence.Repositories;

public sealed class AppointmentRepository(MedicalAppointmentsDbContext dbContext) : IAppointmentRepository
{
    public Task<bool> HasScheduledAppointmentAsync(
        Guid doctorId,
        DateTimeOffset startsAt,
        CancellationToken cancellationToken) =>
        dbContext.Appointments.AnyAsync(
            appointment => appointment.DoctorId == doctorId
                && appointment.StartsAt == startsAt.ToUniversalTime()
                && appointment.Status == AppointmentStatus.Scheduled,
            cancellationToken);

    public void Add(Appointment appointment) => dbContext.Appointments.Add(appointment);
}
