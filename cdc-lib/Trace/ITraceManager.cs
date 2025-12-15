using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Softbase.Cdc.Models;

namespace Softbase.Cdc.Trace
{
    public interface ITraceManager
    {
        Task<TraceSession> StartTraceAsync(TraceConfiguration config);
        Task<TraceSession> StopTraceAsync(Guid sessionId);
        Task<TraceStatus> GetTraceStatusAsync(Guid sessionId);
        Task<IEnumerable<TraceSession>> GetActiveSessionsAsync();
        Task<string> ExportTraceDataAsync(Guid sessionId, string exportPath);
        Task<bool> IsTraceRunningAsync(string sessionName);
    }
}
