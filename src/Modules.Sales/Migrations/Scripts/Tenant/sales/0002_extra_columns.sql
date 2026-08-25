-- Product Specification sections 41/75: destination state/ZIP needed for
-- tax/reporting purposes. Sections 32/78: who created/changed each record.
ALTER TABLE sales ADD COLUMN IF NOT EXISTS destination_state TEXT;
ALTER TABLE sales ADD COLUMN IF NOT EXISTS destination_zip TEXT;
ALTER TABLE sales ADD COLUMN IF NOT EXISTS created_by TEXT NOT NULL DEFAULT 'system';
ALTER TABLE sales ADD COLUMN IF NOT EXISTS updated_by TEXT NOT NULL DEFAULT 'system';

ALTER TABLE listings ADD COLUMN IF NOT EXISTS created_by TEXT NOT NULL DEFAULT 'system';
ALTER TABLE listings ADD COLUMN IF NOT EXISTS updated_by TEXT NOT NULL DEFAULT 'system';

ALTER TABLE returns ADD COLUMN IF NOT EXISTS created_by TEXT NOT NULL DEFAULT 'system';
ALTER TABLE returns ADD COLUMN IF NOT EXISTS updated_by TEXT NOT NULL DEFAULT 'system';
