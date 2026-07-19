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
        AppointmentRow[] rows = await BuildListQuery(patientId, doctorId, status, fromUtc, toUtcExclusive)
            .ToArrayAsync(cancellationToken);

        return Array.ConvertAll(rows, ToListItem);
    }

    public async Task<AppointmentListItem?> GetByIdAsync(Guid appointmentId, CancellationToken cancellationToken)
    {
        AppointmentRow? row = await Project(
                dbContext.Appointments.AsNoTracking().Where(appointment => appointment.Id == appointmentId))
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? null : ToListItem(row);
    }

    // internal (not private): lets AppointmentReaderTests call ToQueryString() on the full
    // filtered/joined/ordered pipeline - proving it still translates to SQL without a live
    // connection, and without DateTimeOffset.AddMinutes creeping back into the projection - while
    // GetAsync above remains the only production caller that actually executes it.
    internal IQueryable<AppointmentRow> BuildListQuery(
        Guid? patientId,
        Guid? doctorId,
        AppointmentStatus? status,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtcExclusive)
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

        return Project(appointments);
    }

    // One composed query (single SQL statement with joins) regardless of caller/filter - never
    // once per row. Projects StartsAt as-is (no AddMinutes here): Npgsql/EF Core cannot translate
    // DateTimeOffset.AddMinutes inside a projection to SQL, so EndsAt is computed afterwards in
    // ToListItem, once rows are already filtered/joined/ordered/materialized - not before.
    //
    // The `orderby` clause references `appointment` directly, in this same query expression,
    // rather than being chained as a separate .OrderBy(row => row.StartsAt) after this method
    // returns: EF Core cannot collapse a member access on a freshly constructed AppointmentRow
    // back down to the underlying column once the query is already composed of four joins - it
    // re-inlines the whole record constructor into the ORDER BY clause and fails to translate it
    // (independent of, and in addition to, the original AddMinutes bug this method also fixes).
    // Ordering on `appointment.StartsAt`/`appointment.Id` here avoids that entirely.
    private IQueryable<AppointmentRow> Project(IQueryable<Appointment> appointments) =>
        from appointment in appointments
        join patientProfile in dbContext.UserProfiles.AsNoTracking()
            on appointment.PatientId equals patientProfile.Id
        join doctor in dbContext.Doctors.AsNoTracking() on appointment.DoctorId equals doctor.Id
        join doctorProfile in dbContext.UserProfiles.AsNoTracking() on doctor.UserId equals doctorProfile.Id
        join specialty in dbContext.Specialties.AsNoTracking() on doctor.SpecialtyId equals specialty.Id
        orderby appointment.StartsAt, appointment.Id
        select new AppointmentRow(
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
            appointment.Status,
            appointment.Reason,
            appointment.MedicalNote,
            appointment.CreatedAt,
            appointment.UpdatedAt,
            appointment.Version);

    // internal: lets AppointmentReaderTests verify EndsAt = StartsAt + DurationMinutes directly,
    // with no database involved - it is pure arithmetic once a row is already in memory.
    internal static AppointmentListItem ToListItem(AppointmentRow row) =>
        new(
            row.Id,
            row.PatientId,
            row.PatientFirstName,
            row.PatientLastName,
            row.DoctorId,
            row.DoctorFirstName,
            row.DoctorLastName,
            row.SpecialtyId,
            row.SpecialtyName,
            row.StartsAt,
            row.StartsAt.AddMinutes(Appointment.DurationMinutes),
            row.Status,
            row.Reason,
            row.MedicalNote,
            row.CreatedAt,
            row.UpdatedAt,
            row.Version);

    // SQL-facing shape: same fields as AppointmentListItem minus EndsAt, which cannot be computed
    // in the SQL projection (see Project above) and is added in ToListItem once materialized.
    internal sealed record AppointmentRow(
        Guid Id,
        Guid PatientId,
        string PatientFirstName,
        string PatientLastName,
        Guid DoctorId,
        string DoctorFirstName,
        string DoctorLastName,
        Guid SpecialtyId,
        string SpecialtyName,
        DateTimeOffset StartsAt,
        AppointmentStatus Status,
        string Reason,
        string? MedicalNote,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        uint Version);
}
