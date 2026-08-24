namespace ResellerSystem.Server.Domain.Abstractions;

/// <summary>
/// Testable clock abstraction. All server-side timestamps are UTC
/// (see architecture principle: "UTC on server, tenant timezone on client").
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
