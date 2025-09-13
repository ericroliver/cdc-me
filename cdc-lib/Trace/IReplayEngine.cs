using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Softbase.Cdc.Models;

namespace Softbase.Cdc.Trace
{
    public interface IReplayEngine
    {
        Task<ReplayResult> ReplayTraceSessionAsync(Guid sessionId, ReplayOptions options);
        Task<ReplayResult> ExecuteStatementsFromFileAsync(string filePath, ReplayOptions options);
        Task<ReplayResult> ReplayTraceAsync(Guid sessionId, ReplayOptions options);
        Task<IEnumerable<ReplayStatement>> PrepareStatementsAsync(Guid sessionId, ReplayOptions options);
        Task<StatementResult> ExecuteStatementAsync(ReplayStatement statement, ReplayOptions options);
        Task<ReplayResult> ReplayTraceWithValidationAsync(Guid sessionId, ReplayOptions options, string validationSnapshotName = null);
        Task<Dictionary<string, object>> GetReplayStatisticsAsync(Guid sessionId);
    }
}