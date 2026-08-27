-- Migration 001: Create factory_connections table
-- Registered database server instances referenced by templates, orders, and the registry.

CREATE TABLE IF NOT EXISTS factory_connections (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name              VARCHAR(255) NOT NULL UNIQUE,
    platform          VARCHAR(50)  NOT NULL DEFAULT 'SqlServer',
    host              VARCHAR(255) NOT NULL,
    port              INT,
    connection_string TEXT         NOT NULL,
    description       TEXT,
    is_default        BOOLEAN      NOT NULL DEFAULT FALSE,
    created_at        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at        TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);
