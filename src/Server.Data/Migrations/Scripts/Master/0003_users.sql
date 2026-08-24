-- Security foundation: a small, local user store. No roles/permissions yet
-- (explicitly out of scope per Architecture Plan v0.1, section 2 — "сложная
-- система ролей не требуется на данном этапе") — just enough to replace the
-- SystemCurrentUserContext stub with a real logged-in local user and to
-- gate the Update Engine's Install Update action behind authentication.

CREATE TABLE IF NOT EXISTS users (
    id               UUID PRIMARY KEY,
    username         TEXT NOT NULL UNIQUE,
    password_hash    TEXT NOT NULL,     -- PBKDF2 (Rfc2898DeriveBytes), see PasswordHasher
    password_salt    TEXT NOT NULL,
    is_active        BOOLEAN NOT NULL DEFAULT TRUE,
    created_at       TIMESTAMPTZ NOT NULL,
    created_by       TEXT NOT NULL DEFAULT 'system',
    updated_at       TIMESTAMPTZ NOT NULL,
    updated_by       TEXT NOT NULL DEFAULT 'system'
);

CREATE TABLE IF NOT EXISTS sessions (
    token            TEXT PRIMARY KEY,
    user_id          UUID NOT NULL REFERENCES users(id),
    created_at       TIMESTAMPTZ NOT NULL,
    expires_at       TIMESTAMPTZ NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_sessions_user ON sessions(user_id);
CREATE INDEX IF NOT EXISTS idx_sessions_expires ON sessions(expires_at);
