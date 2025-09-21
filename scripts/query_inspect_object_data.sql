
select *
from ArTransaction at2
select *
from ArTransactionDetail
select *
from ChartOfAccounts
select *
from GlTransaction
select *
from GlTransactionDetail
select *
from SalesOrder
select *
from SalesOrderDetail

declare @sessionName

GO;

CREATE EVENT SESSION [CDC_Trace_3d17978b6431454b9650f7e53dbcb2ba] ON SERVER

                            ADD EVENT sqlserver.sql_batch_completed(
                                ACTION(sqlserver.client_app_name, sqlserver.client_hostname, 
                                       sqlserver.database_name, sqlserver.session_id, sqlserver.username)
                                WHERE ([sqlserver].[database_name] = N'cdctest')
                            ),
                            ADD EVENT sqlserver.rpc_completed(
                                ACTION(sqlserver.client_app_name, sqlserver.client_hostname, 
                                       sqlserver.database_name, sqlserver.session_id, sqlserver.username)
                                WHERE ([sqlserver].[database_name] = N'cdctest')
                            )

                ADD TARGET package0.ring_buffer(
                    SET max_memory = 65536
                )
                WITH (
                    MAX_MEMORY = 64MB,
                    EVENT_RETENTION_MODE = ALLOW_SINGLE_EVENT_LOSS,
                    MAX_DISPATCH_LATENCY = 30 SECONDS,
                    MAX_EVENT_SIZE = 0KB,
                    MEMORY_PARTITION_MODE = NONE,
                    TRACK_CAUSALITY = ON,
                    STARTUP_STATE = OFF
                );


CREATE EVENT SESSION sanity_xe ON SERVER
ADD EVENT sqlserver.sql_batch_completed(
  ACTION(sqlserver.database_name, sqlserver.sql_text)),
ADD EVENT sqlserver.rpc_completed(
  ACTION(sqlserver.database_name, sqlserver.sql_text))
ADD TARGET package0.ring_buffer(SET max_memory=1024)
WITH (TRACK_CAUSALITY=ON);
GO
ALTER EVENT SESSION sanity_xe ON SERVER STATE = START;
GO





-- 4) Read from the ring_buffer target
;WITH
    x
    AS
    (
        SELECT CAST(t.target_data AS xml) x
        FROM sys.dm_xe_session_targets t
            JOIN sys.dm_xe_sessions s ON s.address = t.event_session_address
        WHERE s.name = 'CDC_Trace_bc7f6fc39e864b9aa225fcb3e5f03967' AND t.target_name = 'ring_buffer'
    )
SELECT x.value('count(/RingBufferTarget/event)','int') AS event_count
FROM x;

-- Optionally shred a few rows to confirm
;WITH
    x
    AS
    (
        SELECT CAST(t.target_data AS xml) x
        FROM sys.dm_xe_session_targets t
            JOIN sys.dm_xe_sessions s ON s.address = t.event_session_address
        WHERE s.name = 'CDC_Trace_bc7f6fc39e864b9aa225fcb3e5f03967' AND t.target_name = 'ring_buffer'
    )
SELECT TOP (10)
    x1.value('@name','sysname')     AS event_name,
    x1.value('@timestamp','datetime2') AS ts,
    x1.value('(action[@name="database_name"]/value)[1]','sysname') AS dbname,
    x1.value('(action[@name="sql_text"]/value)[1]','nvarchar(max)') AS sql_text
FROM x
CROSS APPLY x.x.nodes('/RingBufferTarget/event') AS q(x1)
ORDER BY ts DESC;






SELECT
    event_data.value('(@timestamp)[1]', 'datetime2') AS event_time,
    event_data.value('(@name)[1]', 'varchar(50)') AS event_name,
    event_data.value('(data[@name=''database_name'']/value)[1]', 'varchar(128)') AS database_name,
    event_data.value('(action[@name=''username'']/value)[1]', 'varchar(128)') AS login_name,
    event_data.value('(action[@name=''client_app_name'']/value)[1]', 'varchar(256)') AS application_name,
    event_data.value('(action[@name=''client_hostname'']/value)[1]', 'varchar(128)') AS host_name,
    event_data.value('(action[@name=''session_id'']/value)[1]', 'int') AS spid,
    event_data.value('(data[@name=''duration'']/value)[1]', 'bigint') AS duration,
    event_data.value('(data[@name=''cpu_time'']/value)[1]', 'bigint') AS cpu_time,
    event_data.value('(data[@name=''logical_reads'']/value)[1]', 'bigint') AS reads,
    event_data.value('(data[@name=''writes'']/value)[1]', 'bigint') AS writes,
    event_data.value('(data[@name=''statement'']/value)[1]', 'nvarchar(max)') AS raw_statement,
    event_data.value('(data[@name=''sql_text'']/value)[1]', 'nvarchar(max)') AS sql_text,
    ROW_NUMBER() OVER (ORDER BY event_data.value('(@timestamp)[1]', 'datetime2')) AS execution_order
FROM (
                    SELECT CAST(target_data AS XML) AS target_data
    FROM sys.dm_xe_session_targets st
        INNER JOIN sys.dm_xe_sessions s ON s.address = st.event_session_address
    WHERE s.name = 'CDC_Trace_9110d19aa6c440e19ed41816522575d1' AND st.target_name = 'ring_buffer'
                ) AS data
                CROSS APPLY target_data.nodes('RingBufferTarget/event') AS XEventData(event_data)
ORDER BY execution_order
