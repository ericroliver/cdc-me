-- Migration 005: Create factory_scripts table
-- Individual hydration scripts (SQL in Phase 1) belonging to a script group.

CREATE TABLE factory_scripts (
    id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    name            NVARCHAR(255)    NOT NULL,
    description     NVARCHAR(MAX)    NULL,
    type            NVARCHAR(50)     NOT NULL DEFAULT 'SqlScript',
    content         NVARCHAR(MAX)    NULL,
    file_path       NVARCHAR(500)    NULL,
    script_group_id UNIQUEIDENTIFIER NOT NULL REFERENCES factory_script_groups(id) ON DELETE CASCADE,
    [order]         INT              NOT NULL DEFAULT 0,
    created_at      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME()
);