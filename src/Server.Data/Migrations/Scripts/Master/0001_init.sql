-- Master database: registry of all tenant ("business") databases.
-- This script is applied by SqlScriptMigrationRunner, tracked in schema_migrations.

CREATE TABLE IF NOT EXISTS database_profiles (
    id                     UUID PRIMARY KEY,
    name                   TEXT NOT NULL,
    physical_database_name TEXT NOT NULL UNIQUE,
    time_zone              TEXT NOT NULL,
    currency               TEXT NOT NULL,
    status                 SMALLINT NOT NULL DEFAULT 0, -- 0=Creating,1=Ready,2=MigrationFailed,3=Disabled
    is_active              BOOLEAN NOT NULL DEFAULT TRUE,
    schema_version         INT NOT NULL DEFAULT 0,
    created_at             TIMESTAMPTZ NOT NULL,
    created_by             TEXT NOT NULL DEFAULT 'system',
    updated_at             TIMESTAMPTZ NOT NULL,
    updated_by             TEXT NOT NULL DEFAULT 'system'
);

CREATE INDEX IF NOT EXISTS idx_database_profiles_status ON database_profiles(status);

-- Monotonic sequence used to generate physical database names
-- (reseller_db_000001, reseller_db_000002, ...). Never derived from user input.
CREATE SEQUENCE IF NOT EXISTS database_physical_seq START WITH 1 INCREMENT BY 1;
