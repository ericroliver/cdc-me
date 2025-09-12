using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Softbase
{
    public class SimpleDac
    {
        const int defaultTimeout = 120;
        private IDbConnection _connection;
        private readonly string _connectionString;
        private readonly ILogger _logger;

        public SimpleDac(string connectionString, ILogger logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        private IDbConnection OpenConnection()
        {
            if (_connectionString != null)
            {
                _connection = new SqlConnection(_connectionString);
                _connection.Open();
                return _connection;
            }
            else if (_connection != null)
            {
                if (_connection.State != ConnectionState.Open && _connection.State != ConnectionState.Connecting)
                    _connection.Open();
                return _connection;
            }

            throw new InvalidOperationException("No dbFactory or connection specified!");
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

                if (dbCmd is SqlCommand sqlCmd)
                {
                    return await sqlCmd.ExecuteScalarAsync();
                }
                else
                {
                    return dbCmd.ExecuteScalar();
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

                if (dbCmd is SqlCommand sqlCmd)
                {
                    using (var reader = await sqlCmd.ExecuteReaderAsync())
                    {
                        result = readerDelegate(reader);
                    }
                }
                else
                {
                    using (var reader = dbCmd.ExecuteReader())
                    {
                        result = readerDelegate(reader);
                    }
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

                if (dbCmd is SqlCommand sqlCmd)
                {
                    return await sqlCmd.ExecuteNonQueryAsync();
                }
                else
                {
                    return dbCmd.ExecuteNonQuery();
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
        }
    }
}