-- Import module: mandatory staging workflow (Architecture Plan v0.1
-- section 40 — "НИКОГДА не записывать распознанный импорт напрямую в
-- основную БД"). Rows land here first; only Confirm moves them into the
-- Inventory module's tables (via a direct service call — see
-- ImportModule.cs for why that's a deliberate exception to the
-- modules-don't-depend-on-each-other rule).

CREATE TABLE IF NOT EXISTS import_batches (
    id                 UUID PRIMARY KEY,
    source_filename    TEXT NOT NULL,
    import_type        TEXT NOT NULL DEFAULT 'csv-purchases',
    status             TEXT NOT NULL DEFAULT 'Staged', -- Staged | Confirmed | Rejected
    created_at         TIMESTAMPTZ NOT NULL,
    confirmed_at       TIMESTAMPTZ
);

CREATE TABLE IF NOT EXISTS import_staging_rows (
    id                  UUID PRIMARY KEY,
    import_batch_id     UUID NOT NULL REFERENCES import_batches(id),
    row_index           INT NOT NULL,
    raw_data            JSONB NOT NULL,
    mapped_source_name  TEXT,
    mapped_item_name    TEXT,
    mapped_total_amount NUMERIC(14,2),
    mapped_quantity     INT,
    mapped_purchase_date DATE,
    validation_errors   JSONB NOT NULL DEFAULT '[]',
    is_valid            BOOLEAN NOT NULL DEFAULT FALSE,
    possible_duplicate  BOOLEAN NOT NULL DEFAULT FALSE
);
CREATE INDEX IF NOT EXISTS idx_staging_rows_batch ON import_staging_rows(import_batch_id);
