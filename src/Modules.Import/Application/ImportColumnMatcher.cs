using System.Text;

namespace ResellerSystem.Modules.Import.Application;

/// <summary>
/// "The system should figure out real-world spreadsheets on its own —
/// I should only need to tweak, not configure everything by hand"
/// (direct user requirement). Matches source column headers to
/// ImportTargetFields by a synonym table covering both the field's own
/// English display name and the common English/Russian header variants
/// real reseller trackers actually use (e.g. a personal RU-language
/// Excel tracker: "Дата покупки", "Источник", "Закупка $", ...).
/// Never a substitute for review — SuggestMapping's output is always
/// just a starting point the user can still change per field.
/// </summary>
internal static class ImportColumnMatcher
{
    // Target field key -> extra synonym phrases (normalized at lookup
    // time). The field's own DisplayName is always included automatically.
    private static readonly Dictionary<string, string[]> Synonyms = new()
    {
        ["purchase.sourceName"] = new[] { "source", "purchase source", "place", "seller", "vendor", "источник", "место покупки", "место", "продавец" },
        ["purchase.date"] = new[] { "date purchased", "buy date", "date bought", "дата покупки", "дата закупки" },
        ["purchase.type"] = new[] { "tax type", "тип закупки" },
        ["purchase.salesTaxAmount"] = new[] { "tax paid", "sales tax paid", "налог", "сумма налога" },
        ["purchase.salesTaxRate"] = new[] { "tax %", "ставка налога", "налог %" },
        ["purchase.paymentMethod"] = new[] { "paid with", "payment", "способ оплаты", "оплачено чем", "оплата" },
        ["purchase.additionalExpenses"] = new[] { "extra fees", "buyer premium", "доп расходы", "дополнительные расходы" },
        ["purchase.comment"] = new[] { "note", "notes", "заметки", "примечание", "комментарий" },
        ["purchase.groupKey"] = new[] { "purchase #", "batch id", "id закупки", "номер закупки" },

        ["item.name"] = new[] { "product name", "title", "item", "product", "название товара", "наименование", "товар", "название" },
        ["item.category"] = new[] { "категория", "тип товара" },
        ["item.status"] = new[] { "статус" },
        ["item.notes"] = new[] { "note", "notes", "заметки", "примечание", "комментарий" },
        ["item.purchasePrice"] = new[] { "cost", "buy price", "purchase cost", "unit cost", "закупка", "закупочная цена", "цена закупки", "закупка $" },
        ["item.costBasisOverride"] = new[] { "cost basis", "final cost", "итоговая себестоимость" },
        ["item.quantity"] = new[] { "qty", "quantity", "units", "кол-во", "количество", "кол во" },

        ["listing.marketplace"] = new[] { "marketplace", "platform", "listed on", "площадка", "площадка листинга" },
        ["listing.marketplaceAccount"] = new[] { "listing account", "аккаунт листинга" },
        ["listing.externalListingId"] = new[] { "listing id", "id листинга" },
        ["listing.publishedDate"] = new[] { "date listed", "listed date", "дата листинга", "дата публикации" },
        ["listing.listingPrice"] = new[] { "asking price", "list price", "цена листинга" },
        ["listing.url"] = new[] { "listing url", "link", "ссылка" },
        ["listing.promoted"] = new[] { "promoted" },
        ["listing.promotedRate"] = new[] { "promoted rate" },
        ["listing.endDate"] = new[] { "listing end date" },

        ["sale.marketplace"] = new[] { "marketplace", "platform", "sold on", "площадка продажи", "площадка" },
        ["sale.marketplaceAccount"] = new[] { "sale account", "аккаунт продажи" },
        ["sale.orderId"] = new[] { "order #", "order number", "номер заказа" },
        ["sale.transactionId"] = new[] { "transaction #", "номер транзакции" },
        ["sale.saleDate"] = new[] { "date sold", "sold date", "дата продажи" },
        ["sale.itemSalePrice"] = new[] { "sale price", "sold price", "selling price", "цена продажи", "цена продажи $" },
        ["sale.buyerPaidShipping"] = new[] { "shipping charged", "buyer shipping", "доставка от покупателя" },
        ["sale.buyerPaidSalesTax"] = new[] { "tax charged", "buyer tax", "налог с покупателя" },
        ["sale.marketplaceCollectedTax"] = new[] { "marketplace tax" },
        ["sale.payoutAmount"] = new[] { "payout", "net payout", "выплата" },
        ["sale.destinationState"] = new[] { "buyer state", "ship to state", "штат покупателя", "штат" },
        ["sale.destinationZip"] = new[] { "buyer zip", "ship to zip", "zip", "индекс" },
        ["sale.paymentMethod"] = new[] { "sale payment", "способ оплаты продажи" },

        ["fee.finalValueFee"] = new[] { "final value fee", "fvf" },
        ["fee.finalValueFeeRate"] = new[] { "fvf rate", "final value fee %" },
        ["fee.perOrderFee"] = new[] { "per order fee", "per-order fee" },
        ["fee.insertionFee"] = new[] { "insertion fee" },
        ["fee.listingUpgradeFee"] = new[] { "listing upgrade fee", "upgrade fee" },
        ["fee.promotedListingsFee"] = new[] { "ad fee", "promoted fee", "advertising fee" },
        ["fee.internationalFee"] = new[] { "international fee" },
        ["fee.taxOnSellerFees"] = new[] { "tax on fees" },
        ["fee.feeCredit"] = new[] { "fee credit" },
        ["fee.disputeFee"] = new[] { "dispute fee" },
        ["fee.chargebackFee"] = new[] { "chargeback fee" },
        ["fee.otherMarketplaceFees"] = new[] { "other fees", "misc fees" },

        ["expense.shippingLabel"] = new[] { "shipping label", "shipping cost", "почта", "доставка" },
        ["expense.packaging"] = new[] { "packaging", "упаковка" },
        ["expense.insurance"] = new[] { "insurance", "страховка" },
        ["expense.other"] = new[] { "other expense", "misc expense" },

        ["return.type"] = new[] { "return type", "тип возврата" },
        ["return.date"] = new[] { "return date", "дата возврата" },
        ["return.refundAmount"] = new[] { "refund", "refund amount", "сумма возврата" },
        ["return.refundedShipping"] = new[] { "refunded shipping" },
        ["return.marketplaceFeeCredit"] = new[] { "return fee credit" },
        ["return.shippingCost"] = new[] { "return shipping cost" },
        ["return.physicallyReturned"] = new[] { "physically returned" },
        ["return.conditionOnReturn"] = new[] { "condition", "состояние" },
        ["return.statusAfterReturn"] = new[] { "status after return" },
    };

    // Deliberately no target field (and no synonym anywhere above) for
    // Profit/ROI/Days Listed — Dashboard/Reports compute those live from
    // the raw fields, so a column with that header should stay unmapped
    // rather than being forced onto some unrelated target field.

    /// <summary>Best-guess target-field -> source-column mapping. Each
    /// target field is matched independently, so the same source column
    /// can legitimately end up assigned to more than one target (e.g. one
    /// "Marketplace" column feeding both listing.marketplace and
    /// sale.marketplace) — that's correct, not a bug.</summary>
    public static Dictionary<string, string> SuggestMapping(IReadOnlyList<string> sourceColumns)
    {
        var normalizedColumns = sourceColumns.Select(c => (Original: c, Normalized: Normalize(c))).ToList();
        var result = new Dictionary<string, string>();

        foreach (var field in ImportTargetFields.All)
        {
            var candidates = new List<string> { Normalize(field.DisplayName) };
            if (Synonyms.TryGetValue(field.Key, out var syns)) candidates.AddRange(syns.Select(Normalize));

            string? best = null;
            var bestScore = 0;
            foreach (var (original, normalized) in normalizedColumns)
            {
                var score = ScoreMatch(normalized, candidates);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = original;
                }
            }

            if (best is not null && bestScore >= 60) result[field.Key] = best;
        }

        return result;
    }

    /// <summary>How well this sheet's headers look like importable item
    /// data — used to auto-pick a sheet in a multi-sheet workbook (e.g.
    /// skip a "Dashboard"/"Summary" sheet in favor of the real data
    /// table). Just the count of target fields matched.</summary>
    public static int ScoreSheetHeaders(IReadOnlyList<string> columns) => SuggestMapping(columns).Count;

    private static int ScoreMatch(string normalizedColumn, List<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (normalizedColumn == candidate) return 100;
        }
        foreach (var candidate in candidates)
        {
            if (candidate.Length >= 3 && (normalizedColumn.Contains(candidate) || candidate.Contains(normalizedColumn)))
            {
                return 65;
            }
        }
        return 0;
    }

    private static string Normalize(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != ' ') sb.Append(' ');
        }
        return sb.ToString().Trim();
    }
}
