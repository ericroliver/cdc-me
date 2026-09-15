-- Migration 008: Create factory_order_parameters table
-- Key/value parameters passed to script groups during hydration.

CREATE TABLE factory_order_parameters (
    order_id           UNIQUEIDENTIFIER NOT NULL REFERENCES factory_orders(id) ON DELETE CASCADE,
    [key]              NVARCHAR(255)    NOT NULL,
    value              NVARCHAR(MAX)    NULL,
    PRIMARY KEY (order_id, [key])
);