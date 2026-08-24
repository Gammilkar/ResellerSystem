-- Inventory module: Purchase -> Item, per Architecture Plan v0.1 sections
-- 7-13, simplified for the first module release (see KNOWN_LIMITATIONS.md
-- "Inventory module scope"): purchase source / payment method / category /
-- status are plain text fields here rather than fully editable reference
-- tables (the "constructor" pattern from the architecture plan) — that is
-- a deliberate, documented scope cut, not an oversight.

CREATE TABLE IF NOT EXISTS purchases (
    id                     UUID PRIMARY KEY,
    purchase_date          DATE NOT NULL,
    source_name            TEXT NOT NULL,
    total_amount           NUMERIC(14,2) NOT NULL,
    sales_tax_amount       NUMERIC(14,2) NOT NULL DEFAULT 0,
    sales_tax_rate         NUMERIC(6,4),
    payment_method         TEXT,
    used_reseller_permit   BOOLEAN NOT NULL DEFAULT FALSE,
    comment                TEXT,
    created_at             TIMESTAMPTZ NOT NULL,
    created_by             TEXT NOT NULL DEFAULT 'system',
    updated_at             TIMESTAMPTZ NOT NULL,
    updated_by             TEXT NOT NULL DEFAULT 'system',
    deleted_at              TIMESTAMPTZ
);

-- Human-readable, sequential, never-reused Item Number — distinct from the
-- internal UUID primary key (Architecture Plan v0.1, section 29:
-- "Internal/Public Entity ID и Item Number — не путать").
CREATE SEQUENCE IF NOT EXISTS item_number_seq START WITH 1 INCREMENT BY 1;

CREATE TABLE IF NOT EXISTS items (
    id                       UUID PRIMARY KEY,
    item_number              BIGINT NOT NULL UNIQUE DEFAULT nextval('item_number_seq'),
    purchase_id              UUID NOT NULL REFERENCES purchases(id),
    name                     TEXT NOT NULL,
    category_name            TEXT,
    status                   TEXT NOT NULL DEFAULT 'Purchased',
    cost_basis_calculated    NUMERIC(14,2) NOT NULL,
    cost_basis_override      NUMERIC(14,2),
    notes                    TEXT,
    created_at               TIMESTAMPTZ NOT NULL,
    created_by               TEXT NOT NULL DEFAULT 'system',
    updated_at               TIMESTAMPTZ NOT NULL,
    updated_by               TEXT NOT NULL DEFAULT 'system',
    deleted_at                TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_items_purchase ON items(purchase_id);
CREATE INDEX IF NOT EXISTS idx_items_status ON items(status) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_purchases_date ON purchases(purchase_date DESC);
