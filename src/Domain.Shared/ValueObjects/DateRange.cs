namespace ResellerSystem.Domain.Shared.ValueObjects;

/// <summary>
/// Inclusive date range, used across filters and reports.
/// </summary>
public readonly record struct DateRange(DateOnly? From, DateOnly? To)
{
    public static readonly DateRange Unbounded = new(null, null);

    public bool Contains(DateOnly date)
    {
        if (From.HasValue && date < From.Value) return false;
        if (To.HasValue && date > To.Value) return false;
        return true;
    }

    public bool IsValid => !From.HasValue || !To.HasValue || From.Value <= To.Value;
}
