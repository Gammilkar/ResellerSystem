-- Tenant database: intentionally minimal at Stage 1. Purchase/Item/Sale and
-- the rest of the business schema are added in later migrations without
-- touching this file (append-only migration history).

CREATE TABLE IF NOT EXISTS tenant_info (
    id          BOOLEAN PRIMARY KEY DEFAULT TRUE CHECK (id), -- singleton row
    provisioned_at TIMESTAMPTZ NOT NULL
);

INSERT INTO tenant_info (id, provisioned_at)
VALUES (TRUE, now())
ON CONFLICT (id) DO NOTHING;
