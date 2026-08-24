namespace ResellerSystem.Server.Domain.Abstractions;

/// <summary>
/// Architectural extension point for future marketplace integrations
/// (eBay, Etsy, Mercari, Facebook Marketplace, ...).
///
/// NOT implemented at this stage and NOT wired to any public API endpoint —
/// per the Stage 1 scope, real integrations (and their /integrations/*
/// endpoints) are added only when a concrete provider is built.
/// This interface exists purely so Server.Application can be written
/// against a stable abstraction later without a redesign.
/// </summary>
public interface IMarketplaceIntegration
{
    /// <summary>Stable key, e.g. "ebay", "etsy", "mercari".</summary>
    string ProviderKey { get; }

    /// <summary>Human-readable name shown in Settings > Integrations.</summary>
    string DisplayName { get; }
}
