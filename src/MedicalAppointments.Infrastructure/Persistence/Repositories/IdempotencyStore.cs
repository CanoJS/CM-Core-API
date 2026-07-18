using MedicalAppointments.Application.Abstractions.Clock;
using MedicalAppointments.Application.Abstractions.Idempotency;
using Microsoft.EntityFrameworkCore;

namespace MedicalAppointments.Infrastructure.Persistence.Repositories;

public sealed class IdempotencyStore(MedicalAppointmentsDbContext dbContext, IClock clock) : IIdempotencyStore
{
    public async Task<IdempotencyRecord?> FindAsync(
        Guid userId,
        string operation,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.UtcNow;
        IdempotencyRequest? entity = await dbContext.IdempotencyRequests
            .AsNoTracking()
            .Where(request => request.UserId == userId
                && request.Operation == operation
                && request.IdempotencyKey == idempotencyKey
                && request.ResponseStatus != null
                && request.ExpiresAt > now)
            .FirstOrDefaultAsync(cancellationToken);

        return entity is null || entity.ResponseStatus is null || entity.ResponseBody is null
            ? null
            : new IdempotencyRecord(entity.RequestHash, entity.ResponseStatus.Value, entity.ResponseBody);
    }

    // Deferred: only added to the change tracker here. Not persisted until the caller's
    // IUnitOfWork.SaveChangesAsync runs, same as IAppointmentRepository.Add.
    public void Stage(
        Guid userId,
        string operation,
        string idempotencyKey,
        string requestHash,
        int responseStatus,
        string responseBody,
        DateTimeOffset expiresAt) =>
        dbContext.IdempotencyRequests.Add(new IdempotencyRequest
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Operation = operation,
            IdempotencyKey = idempotencyKey,
            RequestHash = requestHash,
            ResponseStatus = responseStatus,
            ResponseBody = responseBody,
            ExpiresAt = expiresAt,
        });
}
