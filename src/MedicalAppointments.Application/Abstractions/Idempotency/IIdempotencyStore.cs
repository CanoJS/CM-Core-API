namespace MedicalAppointments.Application.Abstractions.Idempotency;

// Backs the optional Idempotency-Key header on POST /api/v1/appointments, scoped to
// (userId, operation, key) via medical.idempotency_requests' own unique constraint - the
// definitive defense against two concurrent identical requests, same as every other race in
// this codebase.
public interface IIdempotencyStore
{
    Task<IdempotencyRecord?> FindAsync(
        Guid userId,
        string operation,
        string idempotencyKey,
        CancellationToken cancellationToken);

    // Stages an insert; not committed until the caller's IUnitOfWork.SaveChangesAsync runs, so
    // it lands in the same transaction as the operation it records the outcome of.
    void Stage(
        Guid userId,
        string operation,
        string idempotencyKey,
        string requestHash,
        int responseStatus,
        string responseBody,
        DateTimeOffset expiresAt);
}

public sealed record IdempotencyRecord(string RequestHash, int ResponseStatus, string ResponseBody);
