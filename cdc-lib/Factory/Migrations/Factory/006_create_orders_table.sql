-- Migration 006: Create factory_orders table
-- Provisioning requests: restore a template, run script groups, deliver a database.

CREATE TABLE IF NOT EXISTS factory_orders (
    id                    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    template_id           UUID NOT NULL REFERENCES factory_templates(id),
    target_connection_id  UUID REFERENCES factory_connections(id),
    target_database_name  VARCHAR(255) NOT NULL,
    status                VARCHAR(50)  NOT NULL DEFAULT 'Pending',
    error_message         TEXT,
    created_at            TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    started_at            TIMESTAMPTZ,
    completed_at          TIMESTAMPTZ
);
