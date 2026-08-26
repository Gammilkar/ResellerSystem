-- Extends Item with descriptive fields the user asked for when creating a
-- physical item during a Purchase: Brand/Model/SerialNumber/SKU/Condition/
-- StorageLocation. Pure descriptive data, not touched by allocation math —
-- deliberately not added to purchase_item_lines (per architecture decision:
-- PurchaseLine only keeps snapshots that are financially/audit-relevant).
ALTER TABLE items ADD COLUMN IF NOT EXISTS brand TEXT;
ALTER TABLE items ADD COLUMN IF NOT EXISTS model TEXT;
ALTER TABLE items ADD COLUMN IF NOT EXISTS serial_number TEXT;
ALTER TABLE items ADD COLUMN IF NOT EXISTS sku_custom_label TEXT;
ALTER TABLE items ADD COLUMN IF NOT EXISTS condition TEXT;
ALTER TABLE items ADD COLUMN IF NOT EXISTS storage_location TEXT;

-- Condition list, same reference_list_values "constructor" mechanism as
-- Category/PurchaseSource/PaymentMethod/ExpenseType/PurchaseType.
INSERT INTO reference_list_values (id, list_key, value, sort_order, is_system_default, created_at, updated_at) VALUES
    (gen_random_uuid(), 'Condition', 'New', 1, TRUE, now(), now()),
    (gen_random_uuid(), 'Condition', 'Open Box', 2, TRUE, now(), now()),
    (gen_random_uuid(), 'Condition', 'Used', 3, TRUE, now(), now()),
    (gen_random_uuid(), 'Condition', 'For Parts', 4, TRUE, now(), now()),
    (gen_random_uuid(), 'Condition', 'Other', 5, TRUE, now(), now())
ON CONFLICT DO NOTHING;
