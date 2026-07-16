using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MedicalAppointments.Infrastructure.Persistence;

public sealed class UnitOfWork(MedicalAppointmentsDbContext dbContext) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConflictException("The resource was changed by another request. Refresh and try again.")
            {
                Source = exception.Source,
            };
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("The selected time slot is no longer available.")
            {
                Source = exception.Source,
            };
        }
    }
}
