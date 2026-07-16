using MedicalAppointments.Domain.Appointments;
using MedicalAppointments.Domain.Doctors;
using MedicalAppointments.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedicalAppointments.Infrastructure.Persistence.Configurations;

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("appointments");
        builder.HasKey(appointment => appointment.Id);
        builder.Property(appointment => appointment.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(appointment => appointment.PatientId).HasColumnName("patient_id");
        builder.Property(appointment => appointment.DoctorId).HasColumnName("doctor_id");
        builder.Property(appointment => appointment.StartsAt).HasColumnName("starts_at");
        builder.Ignore(appointment => appointment.EndsAt);
        builder.Property(appointment => appointment.Reason).HasColumnName("reason").HasMaxLength(500).IsRequired();
        builder.Property(appointment => appointment.Status)
            .HasColumnName("status")
            .HasConversion(
                value => value.ToString().ToUpperInvariant(),
                value => Enum.Parse<AppointmentStatus>(value, true))
            .HasMaxLength(20);
        builder.Property(appointment => appointment.MedicalNote).HasColumnName("medical_note").HasMaxLength(4_000);
        builder.Property(appointment => appointment.CreatedAt).HasColumnName("created_at");
        builder.Property(appointment => appointment.UpdatedAt).HasColumnName("updated_at");
        builder.Property(appointment => appointment.Version).IsRowVersion().HasColumnName("xmin");
        builder.HasOne<UserProfile>().WithMany().HasForeignKey(appointment => appointment.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Doctor>().WithMany().HasForeignKey(appointment => appointment.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(appointment => new { appointment.DoctorId, appointment.StartsAt })
            .IsUnique()
            .HasFilter("status = 'SCHEDULED'")
            .HasDatabaseName("ux_appointments_doctor_slot_scheduled");
        builder.HasIndex(appointment => new { appointment.PatientId, appointment.StartsAt })
            .HasDatabaseName("ix_appointments_patient_starts_at");
    }
}
