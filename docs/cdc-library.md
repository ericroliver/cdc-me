# CDC Library Documentation

## Overview

The `cdc-lib` project is the core library that provides all Change Data Capture (CDC) functionality for the testing framework. It contains the essential components for managing CDC operations, generating data profiles, and comparing database changes.

## Namespace Structure

All library components are organized under the `Softbase` namespace with the following sub-namespaces:

- `Softbase.Cdc` - Core CDC functionality
- `Softbase` - Data access and utilities

## Core Components

### CdcDataUtilities

The primary class for CDC operations, providing static methods for database and table-level CDC management.

#### Database-Level Operations

```csharp
public static class CdcDataUtilities
{
    // Enable CDC on the entire database
    public static void EnableCdcOnDatabase(SimpleDac dac);

    // Disable CDC on the entire database
    public static void DisableCdcOnDatabase(SimpleDac dac);
}
```

**Usage Example:**

```csharp
var logger = loggerFactory.CreateLogger("CDC");
var dac = new SimpleDac(connectionString, logger);

// Enable CDC on database
CdcDataUtilities.EnableCdcOnDatabase(dac);
```

#### Table-Level Operations

```csharp
// Enable CDC on multiple tables
public static void EnableTableCdc(SimpleDac dac, IEnumerable<SqlTable> tableResult, ILogger logger);

// Get all user tables (excluding CDC system tables)
public static IEnumerable<SqlTable> GetTables(SimpleDac dac);

// Get indexes for a specific table
public static IEnumerable<SqlIndex> GetIndexes(SimpleDac dac, string schema, string tableName);
```

**Table CDC Enablement Process:**

1. Identifies tables with primary keys
2. Enables CDC using the primary key as the index
3. Logs success/failure for each table
4. Continues processing even if individual tables fail

#### Profile Generation

The library provides two types of profile generation:

##### Full Profile Generation

Captures all CDC data from CDC tables:

```csharp
public static IDictionary<string, IEnumerable<IDictionary<string, object>>> BuildProfile(
    SimpleDac dac,
    IEnumerable<SqlTable> tableResult,
    ILogger logger);
```

##### Net Changes Profile Generation

Captures only net changes using CDC's net change functions:

```csharp
public static IDictionary<string, IEnumerable<IDictionary<string, object>>> BuildNetProfile(
    SimpleDac dac,
    IEnumerable<SqlTable> tableResult,
    ILogger logger);
```

**Net Changes SQL Template:**

```sql
declare @min BINARY(10), @max BINARY(10);
select @min = sys.fn_cdc_get_min_lsn('{schema}_{tableName}'), @max = sys.fn_cdc_get_max_lsn()
select * from cdc.fn_cdc_get_net_changes_{schema}_{tableName}(@min, @max, 'all')
```

### Data Models

#### SqlTable

Represents a database table with its metadata and indexes.

```csharp
public class SqlTable
{
    public SqlTable(string catalog, string schema, string name);

    public string Catalog { get; set; }
    public string Schema { get; set; }
    public string Name { get; set; }
    public IEnumerable<SqlIndex> Indexes { get; set; }

    // Computed properties
    public bool HasPrimaryKey { get; }
    public SqlIndex? GetPrimaryIndex();
}
```

**Key Features:**

- Automatically detects primary key presence
- Provides easy access to primary key index
- Stores complete index information

#### SqlIndex

Represents a database index with its properties.

```csharp
public class SqlIndex
{
    public SqlIndex(string indexName, string indexType, string indexColumns);

    public string IndexName { get; }
    public string IndexType { get; }
    public string IndexKeys { get; }
}
```

**Index Type Examples:**

- `"clustered, unique, primary key"`
- `"nonclustered, unique"`
- `"clustered"`

### ProfileDiffer

Sophisticated difference engine for comparing data profiles.

```csharp
public class ProfileDiffer
{
    public IDictionary<string, IDictionary<string,object>> Diff(
        IEnumerable<SqlTable> tables,
        IDictionary<string, IEnumerable<IDictionary<string, object>>> leftProfile,
        IDictionary<string, IEnumerable<IDictionary<string, object>>> rightProfile);
}
```

#### Difference Detection Logic

The differ implements comprehensive change detection:

1. **Record Indexing**: Creates indexes based on primary key values
2. **Change Classification**: Identifies New, Changed, and Deleted records
3. **Field-Level Comparison**: Detects specific field changes
4. **Metadata Filtering**: Excludes CDC system fields from comparison

#### Difference Types

```csharp
public enum DiffType
{
    None = 0,
    New = 1,
    Changed = 2,
    Deleted = 3
}
```

#### Difference Models

```csharp
public class Diff
{
    public DiffType Action { get; set; }
    public string Key { get; set; }
    public IDictionary<string, object> Left { get; set; }
    public IDictionary<string, object> Right { get; set; }
    public IDictionary<string, ValueDiff> Changes { get; set; }
}

public class ValueDiff
{
    public DiffType Action { get; set; }
    public object Left { get; set; }
    public object Right { get; set; }
}
```

#### Excluded Fields

The differ automatically excludes CDC system fields:

- `__$start_lsn` - Log Sequence Number start
- `__$operation` - Operation type (Insert/Update/Delete)

#### DateTime Handling

Special logic for DateTime comparisons:

- Ignores DateTime changes within a 24-hour window of current time
- Helps handle timestamp fields that change due to system time differences

### Data Access Layer

#### SimpleDac

Lightweight data access component providing SQL Server connectivity.

```csharp
public class SimpleDac
{
    public SimpleDac(string connectionString, ILogger logger);

    // Scalar operations
    public object ExecuteScalar(string command);
    public T ExecuteScalar<T>(string command);
    public T ExecuteScalar<T>(string command, IDictionary<string, object> param);

    // Reader operations
    public TResult ExecuteReader<TResult>(string command, Func<IDataReader, TResult> readerDelegate);
    public TResult ExecuteReader<TResult>(string command, Func<IDataReader, TResult> readerDelegate, IDictionary<string, object> parameters);

    // Command operations
    public int ExecuteCommand(string command);
    public int ExecuteCommand(string command, IDictionary<string, object> param);
}
```

**Key Features:**

- Automatic connection management
- Parameter binding support
- Comprehensive error logging
- 120-second default timeout
- Proper resource disposal

### Utility Classes

#### JsonUtilities

JSON serialization extensions using Newtonsoft.Json.

```csharp
public static class JsonUtilities
{
    public static string ToJson<T>(this T model, bool pretty = false);
    public static T FromJson<T>(this string buffer);
}
```

**Usage:**

```csharp
var profile = CdcDataUtilities.BuildNetProfile(dac, tables, logger);
var json = profile.ToJson(true); // Pretty-printed JSON
File.WriteAllText("profile.json", json);

var loadedProfile = File.ReadAllText("profile.json").FromJson<IDictionary<string, IEnumerable<IDictionary<string, object>>>>();
```

#### DataReaderUtilities

Extensions for safe data reader field access.

```csharp
public static class DataReaderUtilities
{
    public static T TryReadField<T>(this IDataReader reader, string fieldName);
    public static T TryReadField<T>(this IDataReader reader, string fieldName, T defaultValue);
    public static T ReadField<T>(this IDataReader reader, string fieldName);
    public static List<IDictionary<string, object>> ReadResultAsDictionary(this IDataReader reader);
}
```

#### StringUtilities

String manipulation and formatting utilities.

```csharp
public static class StringUtilities
{
    public static bool EqualsIgnoreCase(this string value, string valueToCompare);
    public static bool Contains(this string value, string valueToCompare);
    public static string ToBase64(this string data);
    public static string FromBase64(this string data);
    public static string MakeKey(string subKey1, string subKey2);
}
```

## Usage Patterns

### Basic CDC Workflow

```csharp
// Setup
var logger = loggerFactory.CreateLogger("CDC");
var connectionString = "Server=localhost;Database=TestDB;Integrated Security=true;";
var dac = new SimpleDac(connectionString, logger);

// 1. Enable CDC on database
CdcDataUtilities.EnableCdcOnDatabase(dac);

// 2. Get tables and enable CDC
var tables = CdcDataUtilities.GetTables(dac);
CdcDataUtilities.EnableTableCdc(dac, tables, logger);

// 3. Run your test scenarios here...

// 4. Generate profile
var profile = CdcDataUtilities.BuildNetProfile(dac, tables, logger);
File.WriteAllText("profile1.json", profile.ToJson(true));

// 5. Run different scenarios...

// 6. Generate second profile
var profile2 = CdcDataUtilities.BuildNetProfile(dac, tables, logger);
File.WriteAllText("profile2.json", profile2.ToJson(true));

// 7. Compare profiles
var differ = new ProfileDiffer();
var differences = differ.Diff(tables, profile, profile2);
File.WriteAllText("differences.json", differences.ToJson(true));

// 8. Cleanup
CdcDataUtilities.DisableCdcOnDatabase(dac);
```

### Profile Analysis

```csharp
// Load and analyze a profile
var profile = File.ReadAllText("profile.json")
    .FromJson<IDictionary<string, IEnumerable<IDictionary<string, object>>>>();

foreach (var tableChanges in profile)
{
    var tableName = tableChanges.Key;
    var changes = tableChanges.Value;

    Console.WriteLine($"Table: {tableName}");
    Console.WriteLine($"Changes: {changes.Count()}");

    foreach (var change in changes)
    {
        var operation = change["__$operation"];
        var lsn = change["__$start_lsn"];
        Console.WriteLine($"  Operation: {operation}, LSN: {lsn}");
    }
}
```

### Custom Difference Analysis

```csharp
var differ = new ProfileDiffer();
var differences = differ.Diff(tables, leftProfile, rightProfile);

foreach (var tableDiff in differences)
{
    var tableName = tableDiff.Key;
    var tableInfo = (IDictionary<string, object>)tableDiff.Value;
    var diffs = (List<Diff>)tableInfo["diff"];

    Console.WriteLine($"Table: {tableName}");

    foreach (var diff in diffs)
    {
        Console.WriteLine($"  {diff.Action}: Key={diff.Key}");

        if (diff.Changes != null)
        {
            foreach (var fieldChange in diff.Changes)
            {
                var field = fieldChange.Key;
                var change = fieldChange.Value;
                Console.WriteLine($"    {field}: {change.Left} -> {change.Right}");
            }
        }
    }
}
```

## Error Handling

The library implements comprehensive error handling:

- **Database Connection Errors**: Logged and re-thrown with context
- **CDC Enablement Failures**: Individual table failures don't stop processing
- **Query Execution Errors**: Detailed logging with SQL statement context
- **Data Type Conversion**: Safe field reading with default values

## Performance Considerations

- **Connection Pooling**: Uses standard SQL Server connection pooling
- **Memory Management**: Proper disposal of database connections and readers
- **Large Result Sets**: Streaming data reader approach for large profiles
- **Query Optimization**: Uses primary key indexes for efficient CDC queries

## Thread Safety

The library is designed for single-threaded usage per instance:

- `SimpleDac` instances should not be shared across threads
- Profile generation operations are not thread-safe
- Consider creating separate instances for concurrent operations

## Dependencies

- **.NET 6+**
- **System.Data.SqlClient** - SQL Server connectivity
- **Microsoft.Extensions.Logging** - Structured logging
- **Newtonsoft.Json** - JSON serialization
