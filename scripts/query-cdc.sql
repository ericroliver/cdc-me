
SELECT name, is_cdc_enabled
FROM sys.databases
WHERE is_cdc_enabled = 1;

SELECT 
    s.name AS SchemaName,
    t.name AS TableName,
    is_tracked_by_cdc
FROM sys.tables t
JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE is_tracked_by_cdc = 1;


declare @end_lsn binary(10) = sys.fn_cdc_get_max_lsn()
declare @begin_lsn binary(10) = sys.fn_cdc_get_min_lsn('dbo_ArTransaction')

print @end_lsn
print @begin_lsn

select * from cdc.fn_cdc_get_all_changes_dbo_ArTransaction(@begin_lsn, @end_lsn, 'all')

select * From sys.dm_server_services dss