using MedicalAppointments.Domain.Appointments;
using MedicalAppointments.Domain.Doctors;
using MedicalAppointments.Domain.Specialties;
using MedicalAppointments.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace MedicalAppointments.Infrastructure.Persistence;

public sealed class MedicalAppointmentsDbContext(DbContextOptions<MedicalAppointmentsDbContext> options)
    : DbContext(options)
{
    public DbSet<Appointment> Appointments => Set<Appointment>();

    public DbSet<Doctor> Doctors => Set<Doctor>();

    public DbSet<Specialty> Specialties => Set<Specialty>();

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    internal DbSet<IdempotencyRequest> IdempotencyRequests => Set<IdempotencyRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("medical");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MedicalAppointmentsDbContext).Assembly);
    }
}
