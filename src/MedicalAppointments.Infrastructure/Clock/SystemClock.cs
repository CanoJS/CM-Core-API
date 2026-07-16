using MedicalAppointments.Application.Abstractions.Clock;

namespace MedicalAppointments.Infrastructure.Clock;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
