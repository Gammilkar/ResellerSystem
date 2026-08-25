-- Suppliers ("Поставщики") — a proper counterparty database, added because
-- Purchase.source_name was plain free text with no reusable record behind
-- it. Kept inside the Inventory module (not a separate module) since only
-- Purchase references it and this avoids the no-cross-module-FK convention
-- entirely — see 0001_init.sql's header for why source_name started as
-- free text.

CREATE TABLE IF NOT EXISTS suppliers (
    id           UUID PRIMARY KEY,
    name         TEXT NOT NULL,
    phone        TEXT,
    email        TEXT,
    address      TEXT,
    notes        TEXT,
    created_at   TIMESTAMPTZ NOT NULL,
    created_by   TEXT NOT NULL DEFAULT 'system',
    updated_at   TIMESTAMPTZ NOT NULL,
    updated_by   TEXT NOT NULL DEFAULT 'system',
    deleted_at   TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_suppliers_name ON suppliers(name) WHERE deleted_at IS NULL;

-- Nullable: existing purchases keep their free-text source_name untouched.
-- source_name itself stays as a denormalized snapshot of the supplier's
-- name at the time it was assigned (see UpdatePurchaseAsync) rather than
-- always being resolved live, since InventoryTableReader is the grid's hot
-- read path and shouldn't need a third join just to show a name.
ALTER TABLE purchases ADD COLUMN IF NOT EXISTS supplier_id UUID REFERENCES suppliers(id);
CREATE INDEX IF NOT EXISTS idx_purchases_supplier ON purchases(supplier_id);
