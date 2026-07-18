using MedicalAppointments.Application.Abstractions.Queries;
using Microsoft.EntityFrameworkCore;

namespace MedicalAppointments.Infrastructure.Persistence.Readers;

public sealed class AdminDoctorReader(MedicalAppointmentsDbContext dbContext) : IAdminDoctorReader
{
    public async Task<IReadOnlyList<AdminDoctorItem>> GetAllAsync(CancellationToken cancellationToken) =>
        await (
            from doctor in dbContext.Doctors.AsNoTracking()
            join profile in dbContext.UserProfiles.AsNoTracking() on doctor.UserId equals profile.Id
            join specialty in dbContext.Specialties.AsNoTracking() on doctor.SpecialtyId equals specialty.Id
            orderby profile.LastName, profile.FirstName, doctor.Id
            select new AdminDoctorItem(
                doctor.Id,
                doctor.UserId,
                profile.FirstName,
                profile.LastName,
                profile.Email,
                specialty.Id,
                specialty.Name,
                doctor.Active,
                doctor.Version))
            .ToArrayAsync(cancellationToken);
}
