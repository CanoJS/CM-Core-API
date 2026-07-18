namespace MedicalAppointments.Application.Abstractions.Persistence;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    // Default is a no-op transaction: only handlers that need a pessimistic lock spanning
    // multiple reads/writes (e.g. re-checking a specialty's active flag under FOR UPDATE
    // right before the final save) need a real database transaction here. Repository/handler
    // fakes that don't exercise that path keep working without implementing this member.
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IUnitOfWorkTransaction>(NoOpTransaction.Instance);

    private sealed class NoOpTransaction : IUnitOfWorkTransaction
    {
        public static readonly NoOpTransaction Instance = new();

        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
