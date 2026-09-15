-- Migration 009: Create factory_provisioned_databases table
-- Audit registry of every database DTAI has provisioned, linked to its connection.

CREATE TABLE factory_provisioned_databases (
    id                  UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    order_id            UNIQUEIDENTIFIER NOT NULL REFERENCES factory_orders(id),
    database_name       NVARCHAR(255)    NOT NULL,
    connection_id       UNIQUEIDENTIFIER NOT NULL REFERENCES factory_connections(id),
    template_id         UNIQUEIDENTIFIER NOT NULL REFERENCES factory_templates(id),
    status              NVARCHAR(50)     NOT NULL DEFAULT 'Active',
    created_at          DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    decommissioned_at   DATETIME2        NULL
);