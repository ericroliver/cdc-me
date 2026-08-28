-- Migration 002: Create factory_templates table
-- Database backup files registered as starting points for provisioning.

CREATE TABLE IF NOT EXISTS factory_templates (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name            VARCHAR(255) NOT NULL,
    version         VARCHAR(50)  NOT NULL,
    platform        VARCHAR(50)  NOT NULL DEFAULT 'SqlServer',
    file_path       VARCHAR(500) NOT NULL,
    description     TEXT,
    checksum         VARCHAR(128),
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    created_by      VARCHAR(255)
);
