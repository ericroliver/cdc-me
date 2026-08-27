-- Migration 008: Create factory_order_parameters table
-- Key/value parameters passed to script groups during hydration.

CREATE TABLE IF NOT EXISTS factory_order_parameters (
    order_id           UUID NOT NULL REFERENCES factory_orders(id) ON DELETE CASCADE,
    key                VARCHAR(255) NOT NULL,
    value              TEXT,
    PRIMARY KEY (order_id, key)
);
