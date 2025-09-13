using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Softbase.Cdc.Models;

namespace Softbase.Cdc.Trace
{
    public interface ICdcComparator
    {
        Task<ComparisonResult> CompareCdcDataAsync(string tableName, string connectionString, string traceConnectionString, ComparisonConfiguration config);
        Task<ComparisonResult> CompareCdcDataAsync(string tableName, Guid leftCaptureId, Guid rightCaptureId);
        Task<ComparisonResult> CompareCapturesAsync(Guid leftCaptureId, Guid rightCaptureId);
        Task<CdcCapture> CaptureCdcDataAsync(Guid sessionId, string captureType, string description = null);
        Task<IDictionary<string, object>> NormalizeCdcDataAsync(IDictionary<string, object> data);
        Task<DifferenceReport> GenerateDifferenceReportAsync(ComparisonResult result);
    }
}