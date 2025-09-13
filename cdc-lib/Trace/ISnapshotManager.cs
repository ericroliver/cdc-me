using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Softbase.Cdc.Models;

namespace Softbase.Cdc.Trace
{
    public interface ISnapshotManager
    {
        Task<string> CreateSnapshotAsync(string databaseName, string snapshotName);
        Task<SnapshotResult> CreateSnapshotAsync(string databaseName, string snapshotName, string connectionString);
        Task<SnapshotResult> RestoreSnapshotAsync(string snapshotName, string targetDatabaseName, string connectionString);
        Task<bool> SnapshotExistsAsync(string snapshotName);
        Task RestoreFromSnapshotAsync(string databaseName, string snapshotName);
        Task<SnapshotResult> DropSnapshotAsync(string snapshotName, string connectionString);
        Task DropSnapshotAsync(string snapshotName);
        Task<SnapshotInfo> GetSnapshotInfoAsync(string snapshotName);
        Task<List<SnapshotInfo>> ListSnapshotsAsync(string databaseName, string connectionString);
        Task<List<SnapshotInfo>> ListSnapshotsAsync();
    }
}