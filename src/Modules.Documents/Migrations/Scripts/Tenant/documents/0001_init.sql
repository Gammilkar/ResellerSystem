-- Documents module — fix from architecture review: Document and
-- DocumentLink are separate tables (not a single OwnerType+OwnerId column
-- on Document itself), so one physical file (e.g. one receipt) can link to
-- multiple business objects (e.g. one Purchase AND several Items).

CREATE TABLE IF NOT EXISTS documents (
    id                 UUID PRIMARY KEY,
    original_filename  TEXT NOT NULL,
    storage_path       TEXT NOT NULL,
    mime_type          TEXT,
    size_bytes         BIGINT NOT NULL,
    sha256_checksum    TEXT NOT NULL,
    uploaded_at        TIMESTAMPTZ NOT NULL
);

CREATE TABLE IF NOT EXISTS document_links (
    id            UUID PRIMARY KEY,
    document_id   UUID NOT NULL REFERENCES documents(id),
    entity_type   TEXT NOT NULL, -- 'Purchase' | 'Item' | 'Sale' | 'Return' | 'Expense'
    entity_id     UUID NOT NULL,
    created_at    TIMESTAMPTZ NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_document_links_document ON document_links(document_id);
CREATE INDEX IF NOT EXISTS idx_document_links_entity ON document_links(entity_type, entity_id);
