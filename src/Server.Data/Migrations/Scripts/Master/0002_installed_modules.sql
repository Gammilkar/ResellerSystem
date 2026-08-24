-- Registry of installed modules (Core + business modules) for this server
-- installation. Populated by ModuleRegistry at startup (see
-- Server.Host StartupChecks) — not per-tenant, since modules are installed
-- at the server level, not per business database.

CREATE TABLE IF NOT EXISTS installed_modules (
    module_key   TEXT PRIMARY KEY,
    version      TEXT NOT NULL,
    installed_at TIMESTAMPTZ NOT NULL,
    updated_at   TIMESTAMPTZ NOT NULL
);
