-- Full purchase-intake workflow: a Purchase can contain several distinct
-- PurchaseItemLine rows (e.g. "Camera x1", "Books x5"), each of which
-- explodes into that many individual Item rows on save, with its
-- Purchase-level costs (tax, fees) allocated across lines and, within each
-- line, across its own physical units. See PurchaseAllocationCalculator/
-- MoneyAllocator for the allocation math. Also introduces the generic
-- "справочник-конструктор" mechanism (Product Specification section 76):
-- one reference_list_values table, discriminated by list_key, backs every
-- user-editable picklist this module needs.

CREATE TABLE IF NOT EXISTS reference_list_values (
    id                UUID PRIMARY KEY,
    list_key          TEXT NOT NULL,
    value             TEXT NOT NULL,
    sort_order        INT NOT NULL DEFAULT 0,
    is_system_default BOOLEAN NOT NULL DEFAULT FALSE,
    created_at        TIMESTAMPTZ NOT NULL,
    created_by        TEXT NOT NULL DEFAULT 'system',
    updated_at        TIMESTAMPTZ NOT NULL,
    updated_by        TEXT NOT NULL DEFAULT 'system',
    deleted_at        TIMESTAMPTZ
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_reference_list_values_key_value
    ON reference_list_values(list_key, value) WHERE deleted_at IS NULL;

CREATE TABLE IF NOT EXISTS purchase_item_lines (
    id                        UUID PRIMARY KEY,
    purchase_id               UUID NOT NULL REFERENCES purchases(id),
    line_number               INT NOT NULL,
    item_name                 TEXT NOT NULL,
    category_name             TEXT,
    quantity                  INT NOT NULL DEFAULT 1,
    unit_purchase_cost        NUMERIC(14,2) NOT NULL DEFAULT 0,
    line_purchase_cost        NUMERIC(14,2) NOT NULL DEFAULT 0,
    allocated_sales_tax       NUMERIC(14,2) NOT NULL DEFAULT 0,
    manual_allocated_sales_tax NUMERIC(14,2),
    allocated_expenses        NUMERIC(14,2) NOT NULL DEFAULT 0,
    manual_allocated_expenses NUMERIC(14,2),
    final_line_cost_basis     NUMERIC(14,2) NOT NULL DEFAULT 0,
    notes                     TEXT,
    created_at                TIMESTAMPTZ NOT NULL,
    created_by                TEXT NOT NULL DEFAULT 'system',
    updated_at                TIMESTAMPTZ NOT NULL,
    updated_by                TEXT NOT NULL DEFAULT 'system',
    deleted_at                TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_purchase_item_lines_purchase ON purchase_item_lines(purchase_id) WHERE deleted_at IS NULL;

CREATE TABLE IF NOT EXISTS purchase_expense_lines (
    id           UUID PRIMARY KEY,
    purchase_id  UUID NOT NULL REFERENCES purchases(id),
    expense_type TEXT NOT NULL,
    amount       NUMERIC(14,2) NOT NULL DEFAULT 0,
    notes        TEXT,
    created_at   TIMESTAMPTZ NOT NULL,
    created_by   TEXT NOT NULL DEFAULT 'system',
    updated_at   TIMESTAMPTZ NOT NULL,
    updated_by   TEXT NOT NULL DEFAULT 'system'
);

CREATE INDEX IF NOT EXISTS idx_purchase_expense_lines_purchase ON purchase_expense_lines(purchase_id);

-- Nullable: Items created via the older quick-entry/import paths have no line.
ALTER TABLE items ADD COLUMN IF NOT EXISTS purchase_item_line_id UUID REFERENCES purchase_item_lines(id);
CREATE INDEX IF NOT EXISTS idx_items_purchase_item_line ON items(purchase_item_line_id) WHERE deleted_at IS NULL;

ALTER TABLE purchases ADD COLUMN IF NOT EXISTS merchandise_subtotal NUMERIC(14,2) NOT NULL DEFAULT 0;
ALTER TABLE purchases ADD COLUMN IF NOT EXISTS taxable_amount NUMERIC(14,2) NOT NULL DEFAULT 0;
ALTER TABLE purchases ADD COLUMN IF NOT EXISTS sales_tax_amount_calculated NUMERIC(14,2);
ALTER TABLE purchases ADD COLUMN IF NOT EXISTS sales_tax_is_manual_override BOOLEAN NOT NULL DEFAULT FALSE;
-- Valid values: 'Proportional' | 'EqualPerUnit' | 'Manual'
ALTER TABLE purchases ADD COLUMN IF NOT EXISTS sales_tax_allocation_method TEXT NOT NULL DEFAULT 'Proportional';
ALTER TABLE purchases ADD COLUMN IF NOT EXISTS expense_allocation_method TEXT NOT NULL DEFAULT 'Proportional';
ALTER TABLE purchases ADD COLUMN IF NOT EXISTS manual_adjustment NUMERIC(14,2) NOT NULL DEFAULT 0;
ALTER TABLE purchases ADD COLUMN IF NOT EXISTS permit_number TEXT;
ALTER TABLE purchases ADD COLUMN IF NOT EXISTS permit_date DATE;
ALTER TABLE purchases ADD COLUMN IF NOT EXISTS tax_exempt_amount NUMERIC(14,2);
ALTER TABLE purchases ADD COLUMN IF NOT EXISTS source_type TEXT;

-- Seed data for the reference lists — PurchaseType values match
-- Purchase.PurchaseType's existing string constants exactly, so nothing
-- about the field's semantics changes, only its pick-list backing.
INSERT INTO reference_list_values (id, list_key, value, sort_order, is_system_default, created_at, updated_at) VALUES
    (gen_random_uuid(), 'PurchaseSource', 'Garage Sale', 1, TRUE, now(), now()),
    (gen_random_uuid(), 'PurchaseSource', 'Estate Sale', 2, TRUE, now(), now()),
    (gen_random_uuid(), 'PurchaseSource', 'Storage Auction', 3, TRUE, now(), now()),
    (gen_random_uuid(), 'PurchaseSource', 'Online Auction', 4, TRUE, now(), now()),
    (gen_random_uuid(), 'PurchaseSource', 'Facebook Marketplace', 5, TRUE, now(), now()),
    (gen_random_uuid(), 'PurchaseSource', 'Goodwill', 6, TRUE, now(), now()),
    (gen_random_uuid(), 'PurchaseSource', 'Private Seller', 7, TRUE, now(), now()),
    (gen_random_uuid(), 'PurchaseSource', 'Other', 8, TRUE, now(), now()),
    (gen_random_uuid(), 'PurchaseType', 'TaxPaid', 1, TRUE, now(), now()),
    (gen_random_uuid(), 'PurchaseType', 'ResellerPermit', 2, TRUE, now(), now()),
    (gen_random_uuid(), 'PurchaseType', 'NoTax', 3, TRUE, now(), now()),
    (gen_random_uuid(), 'PaymentMethod', 'Cash', 1, TRUE, now(), now()),
    (gen_random_uuid(), 'PaymentMethod', 'Credit Card', 2, TRUE, now(), now()),
    (gen_random_uuid(), 'PaymentMethod', 'Debit Card', 3, TRUE, now(), now()),
    (gen_random_uuid(), 'PaymentMethod', 'Check', 4, TRUE, now(), now()),
    (gen_random_uuid(), 'PaymentMethod', 'PayPal', 5, TRUE, now(), now()),
    (gen_random_uuid(), 'PaymentMethod', 'Venmo', 6, TRUE, now(), now()),
    (gen_random_uuid(), 'PaymentMethod', 'Zelle', 7, TRUE, now(), now()),
    (gen_random_uuid(), 'PaymentMethod', 'Other', 8, TRUE, now(), now()),
    (gen_random_uuid(), 'ExpenseType', 'BuyerPremium', 1, TRUE, now(), now()),
    (gen_random_uuid(), 'ExpenseType', 'ProcessingFee', 2, TRUE, now(), now()),
    (gen_random_uuid(), 'ExpenseType', 'Delivery', 3, TRUE, now(), now()),
    (gen_random_uuid(), 'ExpenseType', 'Other', 4, TRUE, now(), now())
ON CONFLICT DO NOTHING;
