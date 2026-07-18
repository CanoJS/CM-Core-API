using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedicalAppointments.Infrastructure.Persistence.Configurations;

internal sealed class IdempotencyRequestConfiguration : IEntityTypeConfiguration<IdempotencyRequest>
{
    public void Configure(EntityTypeBuilder<IdempotencyRequest> builder)
    {
        builder.ToTable("idempotency_requests");
        builder.HasKey(request => request.Id);
        builder.Property(request => request.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(request => request.UserId).HasColumnName("user_id");
        builder.Property(request => request.Operation).HasColumnName("operation").HasMaxLength(100).IsRequired();
        builder.Property(request => request.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(request => request.RequestHash).HasColumnName("request_hash").HasMaxLength(128).IsRequired();
        builder.Property(request => request.ResponseStatus).HasColumnName("response_status");
        builder.Property(request => request.ResponseBody).HasColumnName("response_body").HasColumnType("jsonb");
        builder.Property(request => request.ExpiresAt).HasColumnName("expires_at");
        builder.HasIndex(request => new { request.UserId, request.Operation, request.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("ux_idempotency_user_operation_key");
    }
}
