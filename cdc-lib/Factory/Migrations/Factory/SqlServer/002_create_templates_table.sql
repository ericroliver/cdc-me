-- Migration 002: Create factory_templates table
-- Database backup files registered as starting points for provisioning.

CREATE TABLE factory_templates (
    id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    name            NVARCHAR(255)    NOT NULL,
    version         NVARCHAR(50)     NOT NULL,
    platform        NVARCHAR(50)     NOT NULL DEFAULT 'SqlServer',
    file_path       NVARCHAR(500)    NOT NULL,
    description     NVARCHAR(MAX)    NULL,
    checksum        NVARCHAR(128)    NULL,
    created_at      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    created_by      NVARCHAR(255)    NULL
);