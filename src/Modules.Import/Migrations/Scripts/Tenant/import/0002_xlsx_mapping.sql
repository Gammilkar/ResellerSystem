-- Product Specification sections 57-61: XLSX import with user-driven
-- column mapping (saved as reusable templates), covering the full
-- Purchase/Item/Listing/Sale/Fees/Return/Expenses field set — not just
-- the original 5-column CSV shape.
ALTER TABLE import_batches ADD COLUMN IF NOT EXISTS column_mapping JSONB NOT NULL DEFAULT '{}';

CREATE TABLE IF NOT EXISTS import_mapping_templates (
    id           UUID PRIMARY KEY,
    name         TEXT NOT NULL,
    import_type  TEXT NOT NULL,
    mapping      JSONB NOT NULL,
    created_at   TIMESTAMPTZ NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_import_mapping_templates_name ON import_mapping_templates(import_type, name);
