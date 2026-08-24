-- Expenses module: standalone business expenses, optionally linked to a
-- Purchase/Item/Sale/Return by Id (no enforced FK — same cross-module
-- convention as Modules.Sales; see that module's migration header).
-- This is also where Return.ReturnShippingCost/OtherExpense SHOULD move
-- once a migration step links existing Return rows here — not done yet,
-- see KNOWN_LIMITATIONS.md.

CREATE TABLE IF NOT EXISTS expenses (
    id              UUID PRIMARY KEY,
    expense_type    TEXT NOT NULL,
    amount          NUMERIC(14,2) NOT NULL,
    expense_date    DATE NOT NULL,
    purchase_id     UUID,
    item_id         UUID,
    sale_id         UUID,
    return_id       UUID,
    payment_method  TEXT,
    comment         TEXT,
    created_at      TIMESTAMPTZ NOT NULL,
    updated_at      TIMESTAMPTZ NOT NULL,
    deleted_at      TIMESTAMPTZ
);
CREATE INDEX IF NOT EXISTS idx_expenses_date ON expenses(expense_date DESC);
CREATE INDEX IF NOT EXISTS idx_expenses_type ON expenses(expense_type);
