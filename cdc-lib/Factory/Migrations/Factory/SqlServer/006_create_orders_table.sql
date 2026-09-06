-- Migration 006: Create factory_orders table
-- Provisioning requests: restore a template, run script groups, deliver a database.

CREATE TABLE factory_orders (
    id                    UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    template_id           UNIQUEIDENTIFIER NOT NULL REFERENCES factory_templates(id),
    target_connection_id UNIQUEIDENTIFIER NULL REFERENCES factory_connections(id),
    target_database_name  NVARCHAR(255)    NOT NULL,
    status                NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
    error_message         NVARCHAR(MAX)    NULL,
    created_at            DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    started_at            DATETIME2        NULL,
    completed_at          DATETIME2        NULL
);