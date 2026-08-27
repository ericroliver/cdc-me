-- Migration 005: Create factory_scripts table
-- Individual hydration scripts (SQL in Phase 1) belonging to a script group.

CREATE TABLE IF NOT EXISTS factory_scripts (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name            VARCHAR(255) NOT NULL,
    description     TEXT,
    type            VARCHAR(50)  NOT NULL DEFAULT 'SqlScript',
    content         TEXT,
    file_path       VARCHAR(500),
    script_group_id UUID NOT NULL REFERENCES factory_script_groups(id) ON DELETE CASCADE,
    "order"         INT NOT NULL DEFAULT 0,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
