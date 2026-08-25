-- Product Specification section 26: Purchase must distinguish Tax Paid /
-- Reseller Permit (Tax Exempt) / No Tax (private purchase), separately from
-- the existing used_reseller_permit boolean (kept for backward
-- compatibility — always derived from purchase_type going forward).
ALTER TABLE purchases ADD COLUMN IF NOT EXISTS purchase_type TEXT NOT NULL DEFAULT 'TaxPaid';
-- Valid values: 'TaxPaid' | 'ResellerPermit' | 'NoTax'
