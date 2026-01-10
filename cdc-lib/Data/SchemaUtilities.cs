using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Softbase
{
    public static class RdbmsSchemaColumnFlags
    {
        public const int Identity = 1;
    }

    public static class DataReaderSchemaColumns
    {
        public const string ColumnName = "ColumnName";
        public const string DataType = "DataType";
        public const string AllowNull = "AllowDBNull";
        public const string Length = "ColumnSize";
        public const string Precision = "NumericPrecision";
        public const string Scale = "NumericScale";
        public const string ReadOnly = "IsReadOnly";
        public const string Identity = "IsIdentity";
    }

    //public static class RdbmsUtilities
    //{

    //    private static bool IgnoreValue(object value)
    //    {
    //        return value is IEnumerable && !(value is string);
    //    }

    //    private static bool IgnoreField(string fieldName)
    //    {
    //        return fieldName.EqualsIgnoreCase("_ident")
    //            || fieldName.StartsWith("$$")
    //            || "|typeName|fieldsToIgnore".IndexOf(fieldName, StringComparison.OrdinalIgnoreCase) >= 0;
    //    }

    //    private static string GetFieldDelimiter(object value)
    //    {
    //        if (value is string || value is DateTime)
    //            return "'";

    //        return "";
    //    }

    //    public static T TryReadField<T>(this IDataReader reader, string fieldName)
    //    {
    //        return TryReadField(reader, fieldName, default(T));
    //    }

    //    public static T TryReadField<T>(this IDataReader reader, string fieldName, T defaultValue)
    //    {
    //        var index = reader.GetOrdinal(fieldName);
    //        var value = reader.GetValue(index);

    //        if (value is DBNull || value == null)
    //            return defaultValue;

    //        return (T)value;
    //    }
    //}

    public static class StringUtilities
    {

        public static string Default(string preferredValue, string defaultValue)
        {
            return preferredValue ?? defaultValue;
        }

        public static string LowerCaseFirstLetter(this string field)
        {
            return field.Substring(0, 1).ToLower() + field.Substring(1);
        }

        public static string MakeKey(string subKey1, string subKey2)
        {
            return string.Format("{0}_{1}", subKey1, subKey2);
        }

        public static string MakeKey(string subKey1, string subKey2, string subKey3)
        {
            return string.Format("{0}_{1}_{2}", subKey1, subKey2, subKey3);
        }

        public static void ForEach(this string[] values, Action<string> lamba)
        {
            foreach (var value in values)
                lamba(value);
        }

        public static bool EqualsIgnoreCase(this string value, string valueToCompare)
        {
            return value.Equals(valueToCompare, StringComparison.CurrentCultureIgnoreCase);
        }

        public static bool Contains(this string value, string valueToCompare)
        {
            if (value == null)
                return false;

            return value.IndexOf(valueToCompare, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool StartsWithIgnoreCase(this string value, string valueToCompare)
        {
            return value.StartsWith(valueToCompare, StringComparison.OrdinalIgnoreCase);
        }

        public static bool EndsWithIgnoreCase(this string value, string valueToCompare)
        {
            return value.EndsWith(valueToCompare, StringComparison.OrdinalIgnoreCase);
        }

        public static string PadRightOrTruncate(this string value, int length)
        {
            return value.Length > length ? value.Substring(0, length) : value.PadRight(length);
        }

        public static string PadLeftOrTruncate(this string value, int length)
        {
            return value.Length > length ? value.Substring(0, length) : value.PadLeft(length);
        }

        public static string ToBase64(this string data)
        {
            try
            {
                var encDataByte = Encoding.UTF8.GetBytes(data);
                var encodedData = Convert.ToBase64String(encDataByte);
                return encodedData;
            }
            catch (Exception e)
            {
                throw new Exception("Error in base64Encode" + e.Message);
            }
        }

        public static string FromBase64(this string data)
        {
            try
            {
                var encoder = new UTF8Encoding();
                var utf8Decode = encoder.GetDecoder();
                var todecodeByte = Convert.FromBase64String(data);
                var charCount = utf8Decode.GetCharCount(todecodeByte, 0, todecodeByte.Length);
                var decodedChar = new char[charCount];

                utf8Decode.GetChars(todecodeByte, 0, todecodeByte.Length, decodedChar, 0);

                var result = new string(decodedChar);
                return result;
            }
            catch (Exception e)
            {
                throw new Exception("Error in base64Decode" + e.Message);
            }
        }

        public static string StripWhitespaceLineTerminatorsAndCase(this string expectedTemplate)
        {
            return expectedTemplate.Replace(" ", string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty).Replace("\t", string.Empty).ToLower();
        }
    }

    public static class TypeConversionUtilities
    {

        private static readonly IDictionary<string, DbType> TypeToDbTypeXRef = new Dictionary<string, DbType>()
                                                              {
                                                                  {"System.String", DbType.String}
                                                                  ,{"System.Int32",DbType.Int32}
                                                                  ,{"System.Int16",DbType.Int16}
                                                                  ,{"System.Int64",DbType.Int64}
                                                                  ,{"System.Guid",DbType.Guid}
                                                                  ,{"System.Decimal",DbType.Decimal}
                                                                  ,{"System.Double",DbType.Double}
                                                                  ,{"System.DateTime", DbType.DateTime}
                                                                  ,{"System.Boolean", DbType.Boolean}
                                                                  ,{"System.Byte[]", DbType.Binary}
                                                                  ,{"System.Byte", DbType.Binary}
                                                              };

        private static readonly IDictionary<DbType, Type> DbTypeToTypeXRef = new Dictionary<DbType, Type>()
                                                              {
                                                                  {DbType.String, typeof(string)}
                                                                  ,{DbType.Int32, typeof(Int32)}
                                                                  ,{DbType.Int16, typeof(Int16)}
                                                                  ,{DbType.Int64, typeof(Int64)}
                                                                  ,{DbType.Guid, typeof(System.Guid)}
                                                                  ,{DbType.Decimal, typeof(Decimal)}
                                                                  ,{DbType.Double, typeof(Double)}
                                                                  ,{DbType.DateTime, typeof(DateTime)}
                                                                  ,{DbType.Boolean, typeof(bool)}
                                                                  ,{DbType.Binary, typeof(byte[])}
                                                              };

        public static Type DbTypeToType(DbType type)
        {
            return DbTypeToTypeXRef[type];
        }

        public static DbType TypeToDbType(Type type)
        {
            return TypeNameToDbType(type.FullName);
        }

        public static DbType TypeNameToDbType(string typeName)
        {
            try
            {
                return TypeToDbTypeXRef[typeName];
            }
            catch (Exception)
            {
                throw new NotImplementedException(string.Format("Cannot convert to DbType. Type {0} has no conversion specified.", typeName));
            }

        }
    }

    public static class DataTableUtilities
    {
        public static List<string> DistinctField(DataTable dt, string fieldName)
        {
            var items = new List<string>();

            if (dt == null)
                throw new ArgumentNullException(nameof(dt));

            if (!dt.Columns.Contains(fieldName))
                throw new InvalidOperationException($"{fieldName} is not part of ${dt.TableName} schema");

            if (dt != null && dt.Rows.Count > 0)
            {
                for (var i = 0; i < dt.Rows.Count; i++)
                {
                    var val = Convert.ToString(dt.Rows[i][fieldName]);
                    if (!items.Contains(val))
                        items.Add(val);
                }
            }

            return items;
        }

    }

    public static class DataReaderUtilities
    {

        public static T TryReadField<T>(this IDataReader reader, string fieldName)
        {
            return ReaderFieldLoader.TryReadField<T>(fieldName, reader);
        }

        public static T TryReadField<T>(this IDataReader reader, string fieldName, T defaultValue)
        {
            return ReaderFieldLoader.TryReadField<T>(fieldName, reader, defaultValue);
        }

        public static DateTime ReadDateTimeAsUtc(this IDataReader reader, string fieldName)
        {
            var value = reader.ReadField<DateTime>(fieldName);
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        public static T ReadField<T>(this IDataReader reader, string fieldName)
        {
            return ReaderFieldLoader.ReadField<T>(fieldName, reader);
        }

        public static T ReadField<T>(this IDataReader reader, string fieldName, T defaultValue)
        {
            return ReaderFieldLoader.ReadField<T>(fieldName, reader, defaultValue);
        }

        public static List<T> ReadResultSet<T>(this IDataReader reader, Func<IDataReader, T> func)
        {
            var items = new List<T>();

            while (reader.Read())
                items.Add(func(reader));

            return items;
        }

        public static List<IDictionary<string, object>> ReadResultAsDictionary(this IDataReader reader)
        {
            var result = new List<IDictionary<string, object>>();

#pragma warning disable CA1062 // Validate arguments of public methods
            while (reader.Read())
#pragma warning restore CA1062 // Validate arguments of public methods
            {
                var item = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var fieldName = reader.GetName(i);
                    var value = reader.GetValue(i);
                    item.Add(fieldName, value);
                }
                result.Add(item);
            }
            return result;
        }

        public static IDictionary<string, object> ReadRecordAsDictionary(this IDataReader reader)
        {
            return ReadRecordAsDictionary(reader, false);
        }

        [UnconditionalSuppressMessage("Trimming", "IL2072:Target parameter argument does not satisfy 'DynamicallyAccessedMemberTypes' in call to target method. The return value of the source method does not have matching annotations.", Justification = "GetDefaultValueForType handles common database types explicitly and falls back to Activator.CreateInstance only for value types which should have parameterless constructors.")]
        public static IDictionary<string, object> ReadRecordAsDictionary(this IDataReader reader, bool withDbNulls)
        {
            // Trying to force current to pick up the good change
            var item = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
#pragma warning disable CA1062 // Validate arguments of public methods
            for (var i = 0; i < reader.FieldCount; i++)
#pragma warning restore CA1062 // Validate arguments of public methods
            {
                var fieldName = reader.GetName(i);
                var value = default(object);

                if (!reader.IsDBNull(i))
                    value = reader.GetValue(i);
                else
                {
                    if (!withDbNulls)
                    {
                        var type = reader.GetFieldType(i);
                        value = type.IsValueType ? GetDefaultValueForType(type) : null;
                    }
                    else
                        value = DBNull.Value;
                }

                item.Add(fieldName, value);
            }
            return item;
        }

        /// <summary>
        /// Gets the default value for a given type, handling common database types explicitly
        /// to avoid trimming issues with Activator.CreateInstance.
        /// </summary>
        /// <param name="type">The type to get the default value for.</param>
        /// <returns>The default value for the type.</returns>
        private static object? GetDefaultValueForType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type)
        {
            // Handle common database types explicitly to avoid trimming issues
            if (type == typeof(int)) return 0;
            if (type == typeof(long)) return 0L;
            if (type == typeof(short)) return (short)0;
            if (type == typeof(byte)) return (byte)0;
            if (type == typeof(bool)) return false;
            if (type == typeof(decimal)) return 0m;
            if (type == typeof(double)) return 0.0;
            if (type == typeof(float)) return 0f;
            if (type == typeof(DateTime)) return DateTime.MinValue;
            if (type == typeof(DateTimeOffset)) return DateTimeOffset.MinValue;
            if (type == typeof(TimeSpan)) return TimeSpan.Zero;
            if (type == typeof(Guid)) return Guid.Empty;

            // Handle nullable versions
            if (type == typeof(int?)) return null;
            if (type == typeof(long?)) return null;
            if (type == typeof(short?)) return null;
            if (type == typeof(byte?)) return null;
            if (type == typeof(bool?)) return null;
            if (type == typeof(decimal?)) return null;
            if (type == typeof(double?)) return null;
            if (type == typeof(float?)) return null;
            if (type == typeof(DateTime?)) return null;
            if (type == typeof(DateTimeOffset?)) return null;
            if (type == typeof(TimeSpan?)) return null;
            if (type == typeof(Guid?)) return null;

            // For other value types, try to use Activator.CreateInstance with proper annotation
            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }

            // For reference types, return null
            return null;
        }

        public static List<int> ReadListOfInts(this IDataReader reader)
        {
            return ReadListOfIntegers(reader);
        }

        public static List<int> ReadListOfIntegers(this IDataReader reader)
        {
            var item = new List<int>();
#pragma warning disable CA1062 // Validate arguments of public methods
            while (reader.Read())
#pragma warning restore CA1062 // Validate arguments of public methods
                item.Add(reader.GetInt32(0));
            return item;
        }

        public static List<string> ReadListOfStrings(IDataReader reader)
        {
            var item = new List<string>();
#pragma warning disable CA1062 // Validate arguments of public methods
            while (reader.Read())
#pragma warning restore CA1062 // Validate arguments of public methods
                item.Add(reader.GetString(0));
            return item;

        }
    }

    public static class ReaderFieldLoader
    {
        public static T TryReadField<T>(string fieldName, IDataReader reader)
        {
            return TryReadField<T>(fieldName, reader, default(T));
        }

        public static T TryReadField<T>(string fieldName, IDataReader reader, T defaultValue)
        {
            try
            {
                return ReadField<T>(fieldName, reader, defaultValue);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                //Console.WriteLine(string.Format("ReaderFieldLoader:TryReadField : Field {0} does not exist! Default will be returned.", fieldName));
                return defaultValue;
            }
        }

        public static T ReadField<T>(string fieldName, IDataReader reader)
        {
            return ReadField<T>(fieldName, reader, default(T));
        }

        public static T ReadField<T>(string fieldName, IDataReader reader, T defaultValue)
        {
            var value = defaultValue;

#pragma warning disable CA1062 // Validate arguments of public methods
            var ordinal = reader.GetOrdinal(fieldName);
#pragma warning restore CA1062 // Validate arguments of public methods
            if (!reader.IsDBNull(ordinal))
                value = (T)reader[fieldName];

            return value;
        }

        public static async Task ExportTablesAsync(string connectionString, string outputPath)
        {
            Directory.CreateDirectory(outputPath);

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            await using var tablesCmd = new SqlCommand(@"
        SELECT s.name AS SchemaName, t.name AS TableName
        FROM sys.tables t
        JOIN sys.schemas s ON t.schema_id = s.schema_id
        WHERE t.is_ms_shipped = 0
        ORDER BY s.name, t.name;", conn);

            await using var reader = await tablesCmd.ExecuteReaderAsync();
            var tables = new List<(string Schema, string Table)>();

            while (await reader.ReadAsync())
                tables.Add((reader.GetString(0), reader.GetString(1)));

            foreach (var (schema, table) in tables)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"-- {schema}.{table}");
                sb.AppendLine($"SELECT * FROM [{schema}].[{table}];");

                var file = Path.Combine(outputPath, $"{schema}.{table}.sql");
                await File.WriteAllTextAsync(file, sb.ToString());
            }
        }
        public static string TablesInRightNotInLeft(string pathLeft, string pathRight)
        {
            var leftTables = Directory.GetFiles(pathLeft, "*.sql")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var rightTables = Directory.GetFiles(pathRight, "*.sql")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var onlyInRight = rightTables
                .Except(leftTables, StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => t)
                .ToList();

            return JsonSerializer.Serialize(onlyInRight, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
    }
}
