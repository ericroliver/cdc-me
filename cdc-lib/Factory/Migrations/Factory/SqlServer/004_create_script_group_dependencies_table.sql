-- Migration 004: Create factory_script_group_dependencies table
-- DAG edges: a group can depend on other groups that must complete first.

CREATE TABLE factory_script_group_dependencies (
    group_id        UNIQUEIDENTIFIER NOT NULL REFERENCES factory_script_groups(id) ON DELETE CASCADE,
    depends_on_id   UNIQUEIDENTIFIER NOT NULL REFERENCES factory_script_groups(id) ON DELETE CASCADE,
    PRIMARY KEY (group_id, depends_on_id)
);