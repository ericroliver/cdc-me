using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Softbase.Cdc.Models;

namespace Softbase.Cdc.Trace
{
    public interface ITraceDataProvider
    {
        Task<TraceSession> CreateSessionAsync(TraceConfiguration config);
        Task<TraceSession> GetSessionAsync(Guid sessionId);
        Task<TraceSession> GetSessionByNameAsync(string sessionName);
        Task<IEnumerable<TraceSession>> GetActiveSessionsAsync();
        Task UpdateSessionAsync(TraceSession session);
        Task DeleteSessionAsync(Guid sessionId);

        // API compatibility methods
        Task CreateTraceSessionAsync(TraceSession session);
        Task<TraceSession> GetTraceSessionAsync(Guid sessionId);
        Task<TraceSession> GetTraceSessionByNameAsync(string sessionName);
        Task<IEnumerable<TraceSession>> GetTraceSessionsAsync();
        Task UpdateTraceSessionAsync(TraceSession session);
        Task DeleteTraceSessionAsync(Guid sessionId);

        Task<long> SaveTraceEventAsync(TraceEvent traceEvent);
        Task<IEnumerable<TraceEvent>> GetTraceEventsAsync(Guid sessionId);
        Task<IEnumerable<TraceEvent>> GetTraceEventsAsync(Guid sessionId, int skip, int take);
        Task<int> GetTraceEventCountAsync(Guid sessionId);

        Task<Guid> SaveCdcCaptureAsync(CdcCapture capture);
        Task<CdcCapture> GetCdcCaptureAsync(Guid captureId);
        Task<IEnumerable<CdcCapture>> GetCdcCapturesAsync(Guid sessionId);
        Task<IEnumerable<CdcCapture>> GetCdcCapturesByTypeAsync(Guid sessionId, string captureType);

        Task<Guid> SaveComparisonResultAsync(ComparisonResult result);
        Task<ComparisonResult> GetComparisonResultAsync(Guid comparisonId);
        Task<IEnumerable<ComparisonResult>> GetComparisonResultsAsync(Guid sessionId);

        Task<bool> TestConnectionAsync();
        Task InitializeSchemaAsync();
        Task<string> GetProviderInfoAsync();
    }
}