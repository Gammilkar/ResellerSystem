namespace ResellerSystem.Server.Domain.Abstractions;

/// <summary>
/// Architectural extension point for future read-only external data sources
/// (bank feeds, AI/OCR services, exchange-rate providers, ...) that are
/// neither marketplace integrations nor import providers.
/// </summary>
public interface IExternalDataProvider
{
    string ProviderKey { get; }
}
