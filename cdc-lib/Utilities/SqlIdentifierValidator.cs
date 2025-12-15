using System;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Softbase.Cdc.Utilities
{
    /// <summary>
    /// Provides validation for SQL Server identifiers to prevent SQL injection attacks.
    /// </summary>
    public static class SqlIdentifierValidator
    {
        // SQL Server identifier rules:
        // - First character must be letter (a-z, A-Z), underscore, @, or #
        // - Subsequent characters can be letters, digits, @, $, #, or underscore
        // - Maximum length is 128 characters
        // - Cannot be a reserved keyword (checked separately)
        private static readonly Regex ValidIdentifierPattern = new Regex(
            @"^[a-zA-Z_@#][a-zA-Z0-9_@$#]{0,127}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant
        );

        // Common SQL Server reserved keywords that should not be used as identifiers
        private static readonly string[] ReservedKeywords = new[]
        {
            "SELECT", "INSERT", "UPDATE", "DELETE", "DROP", "CREATE", "ALTER", "EXEC",
            "EXECUTE", "DECLARE", "SET", "FROM", "WHERE", "JOIN", "UNION", "ORDER",
            "GROUP", "HAVING", "TABLE", "DATABASE", "INDEX", "VIEW", "PROCEDURE",
            "FUNCTION", "TRIGGER", "SCHEMA", "USER", "ROLE", "GRANT", "REVOKE",
            "BEGIN", "END", "IF", "ELSE", "WHILE", "RETURN", "MASTER", "TEMPDB",
            "MODEL", "MSDB", "SYS", "INFORMATION_SCHEMA"
        };

        /// <summary>
        /// Validates a SQL Server identifier (database, schema, table, or column name).
        /// </summary>
        /// <param name="identifier">The identifier to validate.</param>
        /// <param name="identifierType">The type of identifier (for error messages).</param>
        /// <returns>The validated identifier.</returns>
        /// <exception cref="ArgumentException">Thrown when the identifier is invalid.</exception>
        public static string ValidateIdentifier(string identifier, string identifierType = "identifier")
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentException($"The {identifierType} cannot be null or empty.", nameof(identifier));
            }

            // Check for leading/trailing whitespace before processing
            var trimmedIdentifier = identifier.Trim();
            if (trimmedIdentifier != identifier)
            {
                throw new ArgumentException(
                    $"The {identifierType} '{identifier}' contains invalid characters. " +
                    "Identifiers must start with a letter, underscore, @, or # and contain only " +
                    "letters, digits, @, $, #, or underscores (max 128 characters).",
                    nameof(identifier));
            }

            // Remove brackets if present (SQL Server allows [identifier] syntax)
            var cleanIdentifier = trimmedIdentifier;
            if (cleanIdentifier.StartsWith("[") && cleanIdentifier.EndsWith("]"))
            {
                cleanIdentifier = cleanIdentifier.Substring(1, cleanIdentifier.Length - 2);
            }

            // Check for whitespace or control characters in the cleaned identifier
            if (cleanIdentifier.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
            {
                throw new ArgumentException(
                    $"The {identifierType} '{identifier}' contains invalid characters. " +
                    "Identifiers must start with a letter, underscore, @, or # and contain only " +
                    "letters, digits, @, $, #, or underscores (max 128 characters).",
                    nameof(identifier));
            }

            // Check against regex pattern
            if (!ValidIdentifierPattern.IsMatch(cleanIdentifier))
            {
                throw new ArgumentException(
                    $"The {identifierType} '{identifier}' contains invalid characters. " +
                    "Identifiers must start with a letter, underscore, @, or # and contain only " +
                    "letters, digits, @, $, #, or underscores (max 128 characters).",
                    nameof(identifier));
            }

            // Check against reserved keywords
            if (Array.Exists(ReservedKeywords, keyword =>
                keyword.Equals(cleanIdentifier, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException(
                    $"The {identifierType} '{identifier}' is a reserved SQL Server keyword and cannot be used.",
                    nameof(identifier));
            }

            return cleanIdentifier;
        }

        /// <summary>
        /// Validates a SQL Server identifier and verifies it exists in the database.
        /// </summary>
        /// <param name="identifier">The identifier to validate.</param>
        /// <param name="connection">The database connection to use for verification.</param>
        /// <param name="identifierType">The type of identifier (database, schema, table).</param>
        /// <returns>The validated identifier.</returns>
        /// <exception cref="ArgumentException">Thrown when the identifier is invalid or doesn't exist.</exception>
        public static async Task<string> ValidateAndVerifyIdentifierAsync(
            string identifier,
            IDbConnection connection,
            string identifierType = "identifier")
        {
            // First validate the format
            var validatedIdentifier = ValidateIdentifier(identifier, identifierType);

            // Then verify it exists in the database
            var exists = identifierType.ToLowerInvariant() switch
            {
                "database" => await DatabaseExistsAsync(validatedIdentifier, connection),
                "schema" => await SchemaExistsAsync(validatedIdentifier, connection),
                "table" => throw new NotSupportedException("Table verification requires schema name. Use ValidateAndVerifyTableAsync instead."),
                _ => throw new ArgumentException($"Unknown identifier type: {identifierType}", nameof(identifierType))
            };

            if (!exists)
            {
                throw new ArgumentException(
                    $"The {identifierType} '{identifier}' does not exist in the database.",
                    nameof(identifier));
            }

            return validatedIdentifier;
        }

        /// <summary>
        /// Validates a table identifier and verifies it exists in the specified schema.
        /// </summary>
        /// <param name="schemaName">The schema name.</param>
        /// <param name="tableName">The table name.</param>
        /// <param name="connection">The database connection to use for verification.</param>
        /// <returns>A tuple of (validatedSchema, validatedTable).</returns>
        /// <exception cref="ArgumentException">Thrown when identifiers are invalid or table doesn't exist.</exception>
        public static async Task<(string schema, string table)> ValidateAndVerifyTableAsync(
            string schemaName,
            string tableName,
            IDbConnection connection)
        {
            var validatedSchema = ValidateIdentifier(schemaName, "schema");
            var validatedTable = ValidateIdentifier(tableName, "table");

            if (!await TableExistsAsync(validatedSchema, validatedTable, connection))
            {
                throw new ArgumentException(
                    $"The table '{schemaName}.{tableName}' does not exist in the database.");
            }

            return (validatedSchema, validatedTable);
        }

        /// <summary>
        /// Escapes a SQL Server identifier by wrapping it in square brackets.
        /// </summary>
        /// <param name="identifier">The identifier to escape.</param>
        /// <returns>The escaped identifier.</returns>
        public static string EscapeIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentException("Identifier cannot be null or empty.", nameof(identifier));
            }

            // Remove existing brackets if present
            var cleanIdentifier = identifier.Trim();
            if (cleanIdentifier.StartsWith("[") && cleanIdentifier.EndsWith("]"))
            {
                cleanIdentifier = cleanIdentifier.Substring(1, cleanIdentifier.Length - 2);
            }

            // Escape any closing brackets within the identifier
            cleanIdentifier = cleanIdentifier.Replace("]", "]]");

            return $"[{cleanIdentifier}]";
        }

        /// <summary>
        /// Validates and escapes a SQL Server identifier.
        /// </summary>
        /// <param name="identifier">The identifier to validate and escape.</param>
        /// <param name="identifierType">The type of identifier (for error messages).</param>
        /// <returns>The validated and escaped identifier.</returns>
        public static string ValidateAndEscape(string identifier, string identifierType = "identifier")
        {
            var validated = ValidateIdentifier(identifier, identifierType);
            return EscapeIdentifier(validated);
        }

        private static async Task<bool> DatabaseExistsAsync(string databaseName, IDbConnection connection)
        {
            const string sql = "SELECT COUNT(1) FROM sys.databases WHERE name = @databaseName";

            using var command = connection.CreateCommand();
            command.CommandText = sql;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@databaseName";
            parameter.Value = databaseName;
            command.Parameters.Add(parameter);

            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }

            var result = await Task.Run(() => command.ExecuteScalar());
            return Convert.ToInt32(result) > 0;
        }

        private static async Task<bool> SchemaExistsAsync(string schemaName, IDbConnection connection)
        {
            const string sql = "SELECT COUNT(1) FROM sys.schemas WHERE name = @schemaName";

            using var command = connection.CreateCommand();
            command.CommandText = sql;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@schemaName";
            parameter.Value = schemaName;
            command.Parameters.Add(parameter);

            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }

            var result = await Task.Run(() => command.ExecuteScalar());
            return Convert.ToInt32(result) > 0;
        }

        private static async Task<bool> TableExistsAsync(string schemaName, string tableName, IDbConnection connection)
        {
            const string sql = @"
                SELECT COUNT(1) 
                FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_SCHEMA = @schemaName AND TABLE_NAME = @tableName";

            using var command = connection.CreateCommand();
            command.CommandText = sql;

            var schemaParam = command.CreateParameter();
            schemaParam.ParameterName = "@schemaName";
            schemaParam.Value = schemaName;
            command.Parameters.Add(schemaParam);

            var tableParam = command.CreateParameter();
            tableParam.ParameterName = "@tableName";
            tableParam.Value = tableName;
            command.Parameters.Add(tableParam);

            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }

            var result = await Task.Run(() => command.ExecuteScalar());
            return Convert.ToInt32(result) > 0;
        }
    }
}
