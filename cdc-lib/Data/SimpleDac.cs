using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using Softbase.Cdc.Data;

namespace Softbase
{
    public class SimpleDac
    {
        const int defaultTimeout = 120;
        private IDbConnection _connection;
        private readonly string _connectionString;
        private readonly DatabaseProvider _provider;
        private readonly ILogger _logger;

        public SimpleDac(string connectionString, DatabaseProvider provider, ILogger logger)
        {
            _connectionString = connectionString;
            _provider = provider;
            _logger = logger;
        }

        private IDbConnection OpenConnection()
        {
            if (_connectionString != null)
            {
                _connection = _provider switch
                {
                    DatabaseProvider.SqlServer => new SqlConnection(_connectionString),
                    DatabaseProvider.PostgreSQL => new NpgsqlConnection(_connectionString),
                    _ => throw new NotSupportedException($"Provider {_provider} not supported")
                };
                _connection.Open();
                return _connection;
            }
            else if (_connection != null)
            {
                if (_connection.State != ConnectionState.Open && _connection.State != ConnectionState.Connecting)
                    _connection.Open();
                return _connection;
            }

            throw new InvalidOperationException("No connection string or connection specified!");
        }

        public object ExecuteScalar(string command)
        {
            return ExecuteScalar(command, null);
        }

        public T ExecuteScalar<T>(string command, IDictionary<string, object> param)
        {
            return (T)ExecuteScalar(command, param);
        }

        public T ExecuteScalar<T>(string command)
        {
            return (T)ExecuteScalar(command, null);
        }

        private string PreprocessCommand(string command)
        {
            return command;
        }

        public object ExecuteScalar(string command, IDictionary<string, object> param)
        {
            var dbConn = default(IDbConnection);

            try
            {
                dbConn = OpenConnection();
                var dbCmd = dbConn.CreateCommand();
                dbCmd.CommandTimeout = defaultTimeout;
                dbCmd.CommandText = PreprocessCommand(command);

                if (param != null)
                {
                    foreach (var parameter in param)
                    {
                        var p = dbCmd.CreateParameter();
                        p.ParameterName = parameter.Key;
                        p.Value = parameter.Value ?? DBNull.Value;
                        dbCmd.Parameters.Add(p);
                    }
                }

                return dbCmd.ExecuteScalar();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error executing statement: {command}");
                throw;
            }
            finally
            {
                if (!object.Equals(dbConn, default(IDbConnection)))
                    dbConn.Close();
            }

        }

        public TResult ExecuteReader<TResult>(string command, Func<IDataReader, TResult> readerDelegate, IDictionary<string, object> parameters)
        {
            // ReSharper disable RedundantAssignment
            var dbConn = default(IDbConnection);
            var result = default(TResult);
            // ReSharper restore RedundantAssignment

            try
            {
                dbConn = OpenConnection();

                var dbCmd = dbConn.CreateCommand();
                dbCmd.CommandTimeout = defaultTimeout;
                dbCmd.CommandText = command;

                if (parameters != null)
                {
                    foreach (var p in parameters)
                    {
                        var parameter = dbCmd.CreateParameter();
                        parameter.ParameterName = p.Key;
                        parameter.Value = p.Value == null ? (object)DBNull.Value : (object)p.Value;
                        dbCmd.Parameters.Add(parameter);
                    }
                }
                using (var reader = dbCmd.ExecuteReader())
                {
                    result = readerDelegate(reader);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error executing statement: {command}");
                throw;
            }
            finally
            {
                if (!object.Equals(dbConn, default(IDbConnection)))
                    dbConn.Close();
            }

            return result;
        }

        public TResult ExecuteReader<TResult>(string command, Func<IDataReader, TResult> readerDelegate)
        {
            return ExecuteReader(command, readerDelegate, null);
        }

        public int ExecuteCommand(string command)
        {
            return ExecuteCommand(command, null);
        }

        public int ExecuteCommand(string command, IDictionary<string, object> param)
        {
            var dbConn = default(IDbConnection);

            try
            {
                dbConn = OpenConnection();
                var dbCmd = dbConn.CreateCommand();
                dbCmd.CommandTimeout = defaultTimeout;
                dbCmd.CommandText = command;

                if (param != null)
                {
                    foreach (var parameter in param)
                    {
                        var p = dbCmd.CreateParameter();
                        p.ParameterName = parameter.Key;
                        p.Value = parameter.Value ?? DBNull.Value;
                        dbCmd.Parameters.Add(p);
                    }
                }

                return dbCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error executing statement: {command}");
                throw;
            }
            finally
            {
                if (!object.Equals(dbConn, default(IDbConnection)))
                    dbConn.Close();
            }
        }

        // Async methods for trace functionality
        public async Task<object> ExecuteScalarAsync(string command)
        {
            return await ExecuteScalarAsync(command, null);
        }

        public async Task<T> ExecuteScalarAsync<T>(string command, IDictionary<string, object> param)
        {
            return (T)await ExecuteScalarAsync(command, param);
        }

        public async Task<T> ExecuteScalarAsync<T>(string command)
        {
            return (T)await ExecuteScalarAsync(command, null);
        }

        public async Task<object> ExecuteScalarAsync(string command, IDictionary<string, object> param)
        {
            var dbConn = default(IDbConnection);

            try
            {
                dbConn = OpenConnection();
                var dbCmd = dbConn.CreateCommand();
                dbCmd.CommandTimeout = defaultTimeout;
                dbCmd.CommandText = PreprocessCommand(command);

                if (param != null)
                {
                    foreach (var parameter in param)
                    {
                        var p = dbCmd.CreateParameter();
                        p.ParameterName = parameter.Key;
                        p.Value = parameter.Value ?? DBNull.Value;
                        dbCmd.Parameters.Add(p);
                    }
                }

                return _provider switch
                {
                    DatabaseProvider.SqlServer when dbCmd is SqlCommand sqlCmd => await sqlCmd.ExecuteScalarAsync(),
                    DatabaseProvider.PostgreSQL when dbCmd is NpgsqlCommand npgsqlCmd => await npgsqlCmd.ExecuteScalarAsync(),
                    _ => dbCmd.ExecuteScalar()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error executing statement: {command}");
                throw;
            }
            finally
            {
                if (!object.Equals(dbConn, default(IDbConnection)))
                    dbConn.Close();
            }
        }

        public async Task<TResult> ExecuteReaderAsync<TResult>(string command, Func<IDataReader, TResult> readerDelegate, IDictionary<string, object> parameters)
        {
            var dbConn = default(IDbConnection);
            var result = default(TResult);

            try
            {
                dbConn = OpenConnection();

                var dbCmd = dbConn.CreateCommand();
                dbCmd.CommandTimeout = defaultTimeout;
                dbCmd.CommandText = command;

                if (parameters != null)
                {
                    foreach (var p in parameters)
                    {
                        var parameter = dbCmd.CreateParameter();
                        parameter.ParameterName = p.Key;
                        parameter.Value = p.Value == null ? (object)DBNull.Value : (object)p.Value;
                        dbCmd.Parameters.Add(parameter);
                    }
                }

                switch (_provider)
                {
                    case DatabaseProvider.SqlServer when dbCmd is SqlCommand sqlCmd:
                        using (var reader = await sqlCmd.ExecuteReaderAsync())
                        {
                            result = readerDelegate(reader);
                        }
                        break;
                    case DatabaseProvider.PostgreSQL when dbCmd is NpgsqlCommand npgsqlCmd:
                        using (var reader = await npgsqlCmd.ExecuteReaderAsync())
                        {
                            result = readerDelegate(reader);
                        }
                        break;
                    default:
                        using (var reader = dbCmd.ExecuteReader())
                        {
                            result = readerDelegate(reader);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error executing statement: {command}");
                throw;
            }
            finally
            {
                if (!object.Equals(dbConn, default(IDbConnection)))
                    dbConn.Close();
            }

            return result;
        }

        public async Task<TResult> ExecuteReaderAsync<TResult>(string command, Func<IDataReader, TResult> readerDelegate)
        {
            return await ExecuteReaderAsync(command, readerDelegate, null);
        }

        public async Task<int> ExecuteCommandAsync(string command)
        {
            return await ExecuteCommandAsync(command, null);
        }

        public async Task<int> ExecuteCommandAsync(string command, IDictionary<string, object> param)
        {
            var dbConn = default(IDbConnection);

            try
            {
                dbConn = OpenConnection();
                var dbCmd = dbConn.CreateCommand();
                dbCmd.CommandTimeout = defaultTimeout;
                dbCmd.CommandText = command;

                if (param != null)
                {
                    foreach (var parameter in param)
                    {
                        var p = dbCmd.CreateParameter();
                        p.ParameterName = parameter.Key;
                        p.Value = parameter.Value ?? DBNull.Value;
                        dbCmd.Parameters.Add(p);
                    }
                }

                return _provider switch
                {
                    DatabaseProvider.SqlServer when dbCmd is SqlCommand sqlCmd => await sqlCmd.ExecuteNonQueryAsync(),
                    DatabaseProvider.PostgreSQL when dbCmd is NpgsqlCommand npgsqlCmd => await npgsqlCmd.ExecuteNonQueryAsync(),
                    _ => dbCmd.ExecuteNonQuery()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error executing statement: {command}");
                throw;
            }
            finally
            {
                if (!object.Equals(dbConn, default(IDbConnection)))
                    dbConn.Close();
            }
        }

        /// <summary>
        /// Begins a database transaction
        /// </summary>
        /// <returns>A transaction wrapper that can be used to commit or rollback</returns>
        public async Task<SimpleDacTransaction> BeginTransactionAsync()
        {
            var connection = OpenConnection();
            var transaction = await Task.Run(() => connection.BeginTransaction());
            return new SimpleDacTransaction(connection, transaction, _logger);
        }
    }

    /// <summary>
    /// Transaction wrapper for SimpleDac operations
    /// </summary>
    public class SimpleDacTransaction : IDisposable
    {
        private readonly IDbConnection _connection;
        private readonly IDbTransaction _transaction;
        private readonly ILogger _logger;
        private bool _disposed = false;

        public SimpleDacTransaction(IDbConnection connection, IDbTransaction transaction, ILogger logger)
        {
            _connection = connection;
            _transaction = transaction;
            _logger = logger;
        }

        /// <summary>
        /// Commits the transaction
        /// </summary>
        public async Task CommitAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SimpleDacTransaction));

            try
            {
                await Task.Run(() => _transaction.Commit());
                _logger.LogDebug("Transaction committed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to commit transaction");
                throw;
            }
        }

        /// <summary>
        /// Rolls back the transaction
        /// </summary>
        public async Task RollbackAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SimpleDacTransaction));

            try
            {
                await Task.Run(() => _transaction.Rollback());
                _logger.LogDebug("Transaction rolled back successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rollback transaction");
                throw;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    _transaction?.Dispose();
                    _connection?.Close();
                    _connection?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing transaction resources");
                }
                _disposed = true;
            }
        }
    }
}