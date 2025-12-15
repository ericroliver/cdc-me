using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Softbase.Cdc.Models;

namespace Softbase.Cdc.Trace
{
    public interface ISnapshotManager
    {
        Task<SnapshotResult> CreateSnapshotAsync(string databaseName, string snapshotName);
        Task<SnapshotResult> RestoreSnapshotAsync(string snapshotName, string targetDatabaseName);
        Task<bool> SnapshotExistsAsync(string snapshotName);
        Task RestoreFromSnapshotAsync(string databaseName, string snapshotName);
        Task<SnapshotResult> DropSnapshotAsync(string snapshotName);
        Task<SnapshotInfo> GetSnapshotInfoAsync(string snapshotName);
        Task<List<SnapshotInfo>> ListSnapshotsAsync(string databaseName);
        Task<List<SnapshotInfo>> ListSnapshotsAsync();
    }
}
