using ResellerSystem.Server.Domain.Abstractions;

namespace ResellerSystem.Server.Infrastructure.Clock;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
