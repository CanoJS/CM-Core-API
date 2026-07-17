using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Domain.Doctors;
using Microsoft.EntityFrameworkCore;

namespace MedicalAppointments.Infrastructure.Persistence.Readers;

public sealed class DoctorCatalogReader(MedicalAppointmentsDbContext dbContext) : IDoctorCatalogReader
{
    public async Task<IReadOnlyList<DoctorCatalogItem>> GetActiveAsync(
        Guid? specialtyId,
        CancellationToken cancellationToken)
    {
        IQueryable<Doctor> doctors = dbContext.Doctors
            .AsNoTracking()
            .Where(doctor => doctor.Active);

        if (specialtyId.HasValue)
        {
            doctors = doctors.Where(doctor => doctor.SpecialtyId == specialtyId.Value);
        }

        return await (
            from doctor in doctors
            join profile in dbContext.UserProfiles.AsNoTracking()
                on doctor.UserId equals profile.Id
            join specialty in dbContext.Specialties.AsNoTracking()
                on doctor.SpecialtyId equals specialty.Id
            where profile.Active && specialty.Active
            orderby profile.LastName, profile.FirstName, doctor.Id
            select new DoctorCatalogItem(
                doctor.Id,
                profile.FirstName,
                profile.LastName,
                profile.Email,
                specialty.Id,
                specialty.Name,
                doctor.Active))
            .ToArrayAsync(cancellationToken);
    }
}
