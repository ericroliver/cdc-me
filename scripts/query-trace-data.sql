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
    event_data.value('(action[@name=''sql_text'']/value)[1]', 'nvarchar(max)') AS sql_text,
    event_data.value('(action[@name=''tsql_stack'']/value)[1]', 'nvarchar(max)') AS tsql_stack,
    event_data.value('(action[@name=''plan_handle'']/value)[1]', 'varbinary(64)') AS plan_handle,
    event_data.value('(action[@name=''request_id'']/value)[1]', 'int') AS request_id,
    event_data.value('(action[@name=''client_connection_id'']/value)[1]', 'uniqueidentifier') AS client_connection_id,
    event_data.value('(action[@name=''transaction_id'']/value)[1]', 'bigint') AS transaction_id,
    event_data.value('(action[@name=''statement'']/value)[1]', 'nvarchar(max)') AS statement,
    ROW_NUMBER() OVER (ORDER BY event_data.value('(@timestamp)[1]', 'datetime2')) AS execution_order
FROM (
                    SELECT CAST(target_data AS XML) AS target_data
    FROM sys.dm_xe_session_targets st
        INNER JOIN sys.dm_xe_sessions s ON s.address = st.event_session_address
    WHERE s.name ='CDC_Trace_9110d19aa6c440e19ed41816522575d1' AND st.target_name = 'ring_buffer'
                ) AS data
                CROSS APPLY target_data.nodes('RingBufferTarget/event') AS XEventData(event_data)
ORDER BY execution_order
