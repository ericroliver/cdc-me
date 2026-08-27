-- Migration 009: Create factory_provisioned_databases table
-- Audit registry of every database DTAI has provisioned, linked to its connection.

CREATE TABLE IF NOT EXISTS factory_provisioned_databases (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id            UUID NOT NULL REFERENCES factory_orders(id),
    database_name       VARCHAR(255) NOT NULL,
    connection_id       UUID NOT NULL REFERENCES factory_connections(id),
    template_id         UUID NOT NULL REFERENCES factory_templates(id),
    status              VARCHAR(50)  NOT NULL DEFAULT 'Active',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    decommissioned_at   TIMESTAMPTZ
);
