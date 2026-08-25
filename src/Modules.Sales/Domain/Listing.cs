namespace ResellerSystem.Modules.Sales.Domain;

public sealed class Listing
{
    public Guid Id { get; private set; }
    public Guid ItemId { get; private set; }
    public string Marketplace { get; set; } = string.Empty;
    public string? MarketplaceAccount { get; set; }
    public string? ExternalListingId { get; set; }
    public DateOnly? PublishedDate { get; set; }
    public decimal? ListingPrice { get; set; }
    public bool Promoted { get; set; }
    public decimal? PromotedRate { get; set; }
    public string Status { get; set; } = "Active";
    public string? Url { get; set; }
    public DateOnly? EndDate { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "system";
    public DateTimeOffset UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = "system";
    public DateTimeOffset? DeletedAt { get; set; }

    private Listing() { }

    public static Listing CreateNew(Guid itemId, string marketplace, string? marketplaceAccount, decimal? listingPrice)
    {
        var now = DateTimeOffset.UtcNow;
        return new Listing
        {
            Id = Guid.NewGuid(),
            ItemId = itemId,
            Marketplace = marketplace.Trim(),
            MarketplaceAccount = marketplaceAccount,
            ListingPrice = listingPrice,
            PublishedDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = "Active",
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
