-- Product Specification section 78 ("Audit Log"): history of changes to
-- financial data. field_name/old_value/new_value are null for a plain
-- "entity created" entry — only set when logging a specific field change.
CREATE TABLE IF NOT EXISTS audit_log (
    id           UUID PRIMARY KEY,
    entity_type  TEXT NOT NULL,   -- 'Purchase' | 'Item' | 'Listing' | 'Sale' | 'Return' | 'Expense' | ...
    entity_id    UUID NOT NULL,
    action       TEXT NOT NULL,   -- 'Created' | 'Updated' | 'Deleted'
    field_name   TEXT,
    old_value    TEXT,
    new_value    TEXT,
    changed_at   TIMESTAMPTZ NOT NULL,
    changed_by   TEXT NOT NULL DEFAULT 'system',
    source       TEXT NOT NULL DEFAULT 'manual' -- 'manual' | 'import' | 'api' | 'migration' | 'system'
);
CREATE INDEX IF NOT EXISTS idx_audit_log_entity ON audit_log(entity_type, entity_id);
CREATE INDEX IF NOT EXISTS idx_audit_log_changed_at ON audit_log(changed_at DESC);
