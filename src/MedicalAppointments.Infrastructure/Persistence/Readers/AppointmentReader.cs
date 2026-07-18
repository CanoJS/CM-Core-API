using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Domain.Appointments;
using Microsoft.EntityFrameworkCore;

namespace MedicalAppointments.Infrastructure.Persistence.Readers;

public sealed class AppointmentReader(MedicalAppointmentsDbContext dbContext) : IAppointmentReader
{
    public async Task<IReadOnlyList<AppointmentListItem>> GetAsync(
        Guid? patientId,
        Guid? doctorId,
        AppointmentStatus? status,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtcExclusive,
        CancellationToken cancellationToken)
    {
        IQueryable<Appointment> appointments = dbContext.Appointments.AsNoTracking();

        if (patientId is { } patient)
        {
            appointments = appointments.Where(appointment => appointment.PatientId == patient);
        }

        if (doctorId is { } doctor)
        {
            appointments = appointments.Where(appointment => appointment.DoctorId == doctor);
        }

        if (status is { } appointmentStatus)
        {
            appointments = appointments.Where(appointment => appointment.Status == appointmentStatus);
        }

        if (fromUtc is { } from)
        {
            appointments = appointments.Where(appointment => appointment.StartsAt >= from);
        }

        if (toUtcExclusive is { } toExclusive)
        {
            appointments = appointments.Where(appointment => appointment.StartsAt < toExclusive);
        }

        return await Project(appointments)
            .OrderBy(item => item.StartsAt)
            .ThenBy(item => item.Id)
            .ToArrayAsync(cancellationToken);
    }

    public Task<AppointmentListItem?> GetByIdAsync(Guid appointmentId, CancellationToken cancellationToken) =>
        Project(dbContext.Appointments.AsNoTracking().Where(appointment => appointment.Id == appointmentId))
            .FirstOrDefaultAsync(cancellationToken);

    // One composed query (single SQL statement with joins) regardless of caller/filter - never
    // once per row.
    private IQueryable<AppointmentListItem> Project(IQueryable<Appointment> appointments) =>
        from appointment in appointments
        join patientProfile in dbContext.UserProfiles.AsNoTracking()
            on appointment.PatientId equals patientProfile.Id
        join doctor in dbContext.Doctors.AsNoTracking() on appointment.DoctorId equals doctor.Id
        join doctorProfile in dbContext.UserProfiles.AsNoTracking() on doctor.UserId equals doctorProfile.Id
        join specialty in dbContext.Specialties.AsNoTracking() on doctor.SpecialtyId equals specialty.Id
        select new AppointmentListItem(
            appointment.Id,
            appointment.PatientId,
            patientProfile.FirstName,
            patientProfile.LastName,
            appointment.DoctorId,
            doctorProfile.FirstName,
            doctorProfile.LastName,
            specialty.Id,
            specialty.Name,
            appointment.StartsAt,
            appointment.StartsAt.AddMinutes(Appointment.DurationMinutes),
            appointment.Status,
            appointment.Reason,
            appointment.MedicalNote,
            appointment.CreatedAt,
            appointment.UpdatedAt,
            appointment.Version);
}
