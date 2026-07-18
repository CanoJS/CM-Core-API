using MedicalAppointments.Domain.Specialties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedicalAppointments.Infrastructure.Persistence.Configurations;

internal sealed class SpecialtyConfiguration : IEntityTypeConfiguration<Specialty>
{
    public void Configure(EntityTypeBuilder<Specialty> builder)
    {
        builder.ToTable("specialties");
        builder.HasKey(specialty => specialty.Id);
        builder.Property(specialty => specialty.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(specialty => specialty.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(specialty => specialty.Active).HasColumnName("active");
        builder.Property(specialty => specialty.Version).IsRowVersion().HasColumnName("xmin");
        builder.HasIndex(specialty => specialty.Name).IsUnique().HasDatabaseName("ux_specialties_name");
    }
}
