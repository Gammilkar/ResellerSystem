namespace ResellerSystem.Server.Domain.Abstractions;

/// <summary>
/// Architectural extension point for the future import pipeline
/// (Excel/CSV/PDF, marketplace reports, bank statements, ...).
/// Real Upload -> Parse -> Staging -> Preview -> Validation -> Import
/// implementations arrive in a later stage; this interface only reserves
/// the shape so Server.Application doesn't need to change later.
/// </summary>
public interface IImportProvider
{
    /// <summary>Stable key, e.g. "excel", "csv", "pdf", "ebay-report".</summary>
    string ProviderKey { get; }
}
