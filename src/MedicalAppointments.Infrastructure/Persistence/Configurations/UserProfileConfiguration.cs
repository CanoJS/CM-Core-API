using MedicalAppointments.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedicalAppointments.Infrastructure.Persistence.Configurations;

internal sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("user_profiles");
        builder.HasKey(profile => profile.Id);
        builder.Property(profile => profile.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(profile => profile.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
        builder.Property(profile => profile.LastName).HasColumnName("last_name").HasMaxLength(100).IsRequired();
        builder.Property(profile => profile.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        builder.Property(profile => profile.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20);
        builder.Property(profile => profile.Active).HasColumnName("active");
        builder.HasIndex(profile => profile.Email).IsUnique().HasDatabaseName("ux_user_profiles_email");
    }
}
