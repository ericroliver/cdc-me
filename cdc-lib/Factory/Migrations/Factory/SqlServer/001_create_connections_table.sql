-- Migration 001: Create factory_connections table
-- Registered database server instances referenced by templates, orders, and the registry.

CREATE TABLE factory_connections (
    id                  UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    name                NVARCHAR(255)    NOT NULL UNIQUE,
    platform            NVARCHAR(50)     NOT NULL DEFAULT 'SqlServer',
    host                NVARCHAR(255)    NOT NULL,
    port                INT              NULL,
    connection_string   NVARCHAR(MAX)    NOT NULL,
    description         NVARCHAR(MAX)    NULL,
    is_default          BIT              NOT NULL DEFAULT 0,
    created_at          DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at          DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME()
);