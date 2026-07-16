using MedicalAppointments.Domain.Doctors;
using MedicalAppointments.Domain.Specialties;
using MedicalAppointments.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedicalAppointments.Infrastructure.Persistence.Configurations;

internal sealed class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
{
    public void Configure(EntityTypeBuilder<Doctor> builder)
    {
        builder.ToTable("doctors");
        builder.HasKey(doctor => doctor.Id);
        builder.Property(doctor => doctor.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(doctor => doctor.UserId).HasColumnName("user_id");
        builder.Property(doctor => doctor.SpecialtyId).HasColumnName("specialty_id");
        builder.Property(doctor => doctor.Active).HasColumnName("active");
        builder.HasIndex(doctor => doctor.UserId).IsUnique().HasDatabaseName("ux_doctors_user_id");
        builder.HasOne<UserProfile>().WithMany().HasForeignKey(doctor => doctor.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Specialty>().WithMany().HasForeignKey(doctor => doctor.SpecialtyId).OnDelete(DeleteBehavior.Restrict);
    }
}
