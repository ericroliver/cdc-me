-- Migration 004: Create factory_script_group_dependencies table
-- DAG edges: a group can depend on other groups that must complete first.
--
-- NOTE: only ONE of the two FKs may be ON DELETE CASCADE. SQL Server rejects
-- a table with two cascade paths to the same table (error 1785, "Could not
-- create constraint"). Edges pointing FROM the deleted group (group_id)
-- cascade with it; edges pointing AT it (depends_on_id) are deleted
-- explicitly by ScriptGroupRepository.DeleteGroupAsync, mirroring the
-- PostgreSQL behavior where both FKs cascade.

CREATE TABLE factory_script_group_dependencies (
    group_id        UNIQUEIDENTIFIER NOT NULL REFERENCES factory_script_groups(id) ON DELETE CASCADE,
    depends_on_id   UNIQUEIDENTIFIER NOT NULL REFERENCES factory_script_groups(id),
    PRIMARY KEY (group_id, depends_on_id)
);
