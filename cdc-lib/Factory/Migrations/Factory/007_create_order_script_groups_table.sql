-- Migration 007: Create factory_order_script_groups table
-- Many-to-many: which script groups to run for a given order.

CREATE TABLE IF NOT EXISTS factory_order_script_groups (
    order_id           UUID NOT NULL REFERENCES factory_orders(id) ON DELETE CASCADE,
    script_group_id    UUID NOT NULL REFERENCES factory_script_groups(id),
    PRIMARY KEY (order_id, script_group_id)
);
