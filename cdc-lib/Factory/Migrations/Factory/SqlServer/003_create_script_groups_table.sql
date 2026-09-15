-- Migration 003: Create factory_script_groups table
-- Logical groupings of scripts that share parameters, ordered within layers.

CREATE TABLE factory_script_groups (
    id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    name            NVARCHAR(255)    NOT NULL,
    description     NVARCHAR(MAX)    NULL,
    layer           INT              NOT NULL DEFAULT 0,
    [order]         INT              NOT NULL DEFAULT 0,
    created_at      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME()
);