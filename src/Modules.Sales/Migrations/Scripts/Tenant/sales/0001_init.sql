-- Sales module: Listing -> Sale -> SaleFee / Return, per Architecture Plan
-- v0.1 sections 17-26 AND the explicit fixes from the follow-up brief:
--   - Sale keeps GrossTransactionAmount and PayoutAmount as SEPARATE
--     columns (never conflate gross revenue with what actually got paid out).
--   - sale_fees is ONLY marketplace fees (FVF, insertion, promoted, ...).
--     Selling expenses (shipping label, return shipping, packaging) belong
--     to a future Expense entity/module, NOT here — return_shipping_cost
--     below is a deliberate, documented exception (see module README /
--     KNOWN_LIMITATIONS.md "Sales module scope") kept on Return until the
--     Expenses module exists to own it properly.
--
-- item_id / listing_id are NOT enforced as SQL foreign keys against the
-- Inventory module's tables — modules intentionally do not take hard DB
-- dependencies on each other's schema (see Modules.Sales README). They are
-- still meaningful UUIDs by convention (Inventory owns item identity).

CREATE TABLE IF NOT EXISTS listings (
    id                   UUID PRIMARY KEY,
    item_id              UUID NOT NULL,
    marketplace          TEXT NOT NULL,
    marketplace_account  TEXT,
    external_listing_id  TEXT,
    published_date       DATE,
    listing_price        NUMERIC(14,2),
    promoted             BOOLEAN NOT NULL DEFAULT FALSE,
    promoted_rate        NUMERIC(6,4),
    status               TEXT NOT NULL DEFAULT 'Active',
    url                  TEXT,
    end_date             DATE,
    created_at           TIMESTAMPTZ NOT NULL,
    updated_at           TIMESTAMPTZ NOT NULL,
    deleted_at           TIMESTAMPTZ
);
CREATE INDEX IF NOT EXISTS idx_listings_item ON listings(item_id);

CREATE TABLE IF NOT EXISTS sales (
    id                        UUID PRIMARY KEY,
    item_id                   UUID NOT NULL,
    listing_id                UUID REFERENCES listings(id),
    marketplace               TEXT NOT NULL,
    marketplace_account       TEXT,
    order_id                  TEXT,
    transaction_id            TEXT,
    sale_date                 DATE NOT NULL,

    -- Gross side (buyer-facing / revenue reporting):
    item_sale_price           NUMERIC(14,2) NOT NULL,
    buyer_paid_shipping       NUMERIC(14,2) NOT NULL DEFAULT 0,
    buyer_paid_sales_tax      NUMERIC(14,2) NOT NULL DEFAULT 0,
    handling                  NUMERIC(14,2) NOT NULL DEFAULT 0,
    seller_discount           NUMERIC(14,2) NOT NULL DEFAULT 0,
    gross_transaction_amount  NUMERIC(14,2) NOT NULL,
    marketplace_collected_tax NUMERIC(14,2) NOT NULL DEFAULT 0,

    -- Payout side (what actually landed in the seller's account) —
    -- NEVER used interchangeably with gross_transaction_amount above.
    payout_amount             NUMERIC(14,2) NOT NULL,

    quantity                  INT NOT NULL DEFAULT 1,
    payment_method            TEXT,

    created_at                TIMESTAMPTZ NOT NULL,
    updated_at                TIMESTAMPTZ NOT NULL,
    deleted_at                TIMESTAMPTZ
);
CREATE INDEX IF NOT EXISTS idx_sales_item ON sales(item_id);
CREATE UNIQUE INDEX IF NOT EXISTS uq_sales_dedup ON sales(marketplace, order_id, transaction_id) WHERE deleted_at IS NULL AND order_id IS NOT NULL;

-- Marketplace fees ONLY — see header comment. fee_type is free text for
-- now (FinalValueFee, PerOrderFee, InsertionFee, ListingUpgrade,
-- PromotedAdFee, InternationalFee, DisputeFee, FeeTax, FeeCredit, Other).
CREATE TABLE IF NOT EXISTS sale_fees (
    id          UUID PRIMARY KEY,
    sale_id     UUID NOT NULL REFERENCES sales(id),
    fee_type    TEXT NOT NULL,
    amount      NUMERIC(14,2) NOT NULL,
    rate        NUMERIC(6,4),
    source      TEXT NOT NULL DEFAULT 'manual',
    created_at  TIMESTAMPTZ NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_sale_fees_sale ON sale_fees(sale_id);

CREATE TABLE IF NOT EXISTS returns (
    id                      UUID PRIMARY KEY,
    sale_id                 UUID NOT NULL REFERENCES sales(id),
    item_id                 UUID NOT NULL,
    return_date             DATE NOT NULL,
    return_type             TEXT NOT NULL DEFAULT 'Full',
    refund_to_buyer         NUMERIC(14,2) NOT NULL,
    refunded_shipping       NUMERIC(14,2) NOT NULL DEFAULT 0,
    marketplace_fee_credit  NUMERIC(14,2) NOT NULL DEFAULT 0,
    return_shipping_cost    NUMERIC(14,2) NOT NULL DEFAULT 0,
    other_expense           NUMERIC(14,2) NOT NULL DEFAULT 0,
    physically_returned     BOOLEAN NOT NULL,
    condition_on_return     TEXT,
    comment                 TEXT,
    created_at              TIMESTAMPTZ NOT NULL,
    updated_at              TIMESTAMPTZ NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_returns_sale ON returns(sale_id);
