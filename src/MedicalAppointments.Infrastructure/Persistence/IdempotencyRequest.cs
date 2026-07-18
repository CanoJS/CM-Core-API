namespace MedicalAppointments.Infrastructure.Persistence;

// Maps medical.idempotency_requests. Not a Domain entity: this is a technical HTTP-idempotency
// record, not a business concept with domain invariants.
internal sealed class IdempotencyRequest
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Operation { get; set; } = string.Empty;

    public string IdempotencyKey { get; set; } = string.Empty;

    public string RequestHash { get; set; } = string.Empty;

    public int? ResponseStatus { get; set; }

    public string? ResponseBody { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}
