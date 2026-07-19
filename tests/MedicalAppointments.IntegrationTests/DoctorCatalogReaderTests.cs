using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Domain.Doctors;
using MedicalAppointments.Domain.Specialties;
using MedicalAppointments.Domain.Users;
using MedicalAppointments.Infrastructure.Persistence;
using MedicalAppointments.Infrastructure.Persistence.Readers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MedicalAppointments.IntegrationTests;

// DoctorCatalogReader.GetActiveAsync filters on three independent flags (doctor.Active,
// profile.Active, specialty.Active) - each seeded case below isolates exactly one of them to
// prove the public catalog genuinely excludes it, not just that "some" filter happens to apply.
// See RealDatabaseSeeding for how the auth.users FK is satisfied safely under
// RUN_REAL_DB_TESTS=true.
public sealed class DoctorCatalogReaderTests
{
    [RealDatabaseFact]
    public async Task GetActiveAsync_AgainstRealDatabase_ExposesOnlyFullyActiveDoctors()
    {
        await using MedicalAppointmentsDbContext dbContext = CreateRealDbContext();
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(
            CancellationToken.None);
        try
        {
            string suffix = Guid.NewGuid().ToString("N");
            var activeSpecialty = new Specialty(Guid.NewGuid(), $"Catalog test active specialty {suffix}");
            var inactiveSpecialty = new Specialty(Guid.NewGuid(), $"Catalog test inactive specialty {suffix}");
            inactiveSpecialty.Deactivate();
            dbContext.Specialties.AddRange(activeSpecialty, inactiveSpecialty);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            // Case 1: doctor active, profile active, specialty active -> must appear.
            Guid visibleUserId = await RealDatabaseSeeding.InsertDoctorProfileAsync(
                dbContext, "Elena", "Torres", $"catalog-visible-{suffix}@example.test", CancellationToken.None);
            var visibleDoctor = new Doctor(Guid.NewGuid(), visibleUserId, activeSpecialty.Id);

            // Case 2: doctor row itself inactive -> excluded by the reader's own Where(Active).
            Guid inactiveDoctorUserId = await RealDatabaseSeeding.InsertDoctorProfileAsync(
                dbContext, "Iván", "Soto", $"catalog-inactive-doctor-{suffix}@example.test", CancellationToken.None);
            var inactiveDoctorRow = new Doctor(Guid.NewGuid(), inactiveDoctorUserId, activeSpecialty.Id);
            inactiveDoctorRow.SetActive(false);

            // Case 3: doctor/profile active but specialty inactive -> excluded by the join's
            // `specialty.Active` condition.
            Guid inactiveSpecialtyDoctorUserId = await RealDatabaseSeeding.InsertDoctorProfileAsync(
                dbContext, "Renata", "Blanco", $"catalog-inactive-specialty-{suffix}@example.test", CancellationToken.None);
            var inactiveSpecialtyDoctor = new Doctor(Guid.NewGuid(), inactiveSpecialtyDoctorUserId, inactiveSpecialty.Id);

            dbContext.Doctors.AddRange(visibleDoctor, inactiveDoctorRow, inactiveSpecialtyDoctor);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            // Case 4: doctor/specialty active but the underlying profile was deactivated (the
            // inactive-account enforcement flag from docs/SECURITY.md) -> excluded by the join's
            // `profile.Active` condition.
            Guid inactiveProfileUserId = await RealDatabaseSeeding.InsertDoctorProfileAsync(
                dbContext, "Hugo", "Prieto", $"catalog-inactive-profile-{suffix}@example.test", CancellationToken.None);
            var inactiveProfileDoctor = new Doctor(Guid.NewGuid(), inactiveProfileUserId, activeSpecialty.Id);
            dbContext.Doctors.Add(inactiveProfileDoctor);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            UserProfile inactiveProfile = await dbContext.UserProfiles.SingleAsync(
                p => p.Id == inactiveProfileUserId, CancellationToken.None);
            inactiveProfile.Deactivate();
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var reader = new DoctorCatalogReader(dbContext);

            IReadOnlyList<DoctorCatalogItem> catalog = await reader.GetActiveAsync(null, CancellationToken.None);

            DoctorCatalogItem visibleItem = Assert.Single(catalog, item => item.Id == visibleDoctor.Id);
            Assert.Equal("Elena", visibleItem.FirstName);
            Assert.Equal("Torres", visibleItem.LastName);
            Assert.Equal($"catalog-visible-{suffix}@example.test", visibleItem.Email);
            Assert.Equal(activeSpecialty.Id, visibleItem.SpecialtyId);
            Assert.Equal(activeSpecialty.Name, visibleItem.SpecialtyName);
            Assert.True(visibleItem.Active);

            Assert.DoesNotContain(catalog, item => item.Id == inactiveDoctorRow.Id);
            Assert.DoesNotContain(catalog, item => item.Id == inactiveSpecialtyDoctor.Id);
            Assert.DoesNotContain(catalog, item => item.Id == inactiveProfileDoctor.Id);

            // The specialtyId filter combines with (does not replace) the active-flags filter.
            IReadOnlyList<DoctorCatalogItem> byActiveSpecialty = await reader.GetActiveAsync(
                activeSpecialty.Id, CancellationToken.None);
            Assert.Contains(byActiveSpecialty, item => item.Id == visibleDoctor.Id);

            IReadOnlyList<DoctorCatalogItem> byInactiveSpecialty = await reader.GetActiveAsync(
                inactiveSpecialty.Id, CancellationToken.None);
            Assert.DoesNotContain(byInactiveSpecialty, item => item.Id == inactiveSpecialtyDoctor.Id);
        }
        finally
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
    }

    private static MedicalAppointmentsDbContext CreateRealDbContext() =>
        new(RealDatabaseConnection.BuildOptions(RealDatabaseConnection.GetRequiredLocalConnectionString()));
}
