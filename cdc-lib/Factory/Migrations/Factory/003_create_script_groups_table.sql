-- Migration 003: Create factory_script_groups table
-- Logical groupings of scripts that share parameters, ordered within layers.

CREATE TABLE IF NOT EXISTS factory_script_groups (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name            VARCHAR(255) NOT NULL,
    description     TEXT,
    layer           INT NOT NULL DEFAULT 0,
    "order"         INT NOT NULL DEFAULT 0,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
