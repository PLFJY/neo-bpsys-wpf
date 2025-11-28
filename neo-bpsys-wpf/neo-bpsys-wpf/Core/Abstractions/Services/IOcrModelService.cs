using System.Threading;
using System.Threading.Tasks;
using Sdcb.PaddleOCR.Models;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

public interface IOcrModelService
{
    Task<FullOcrModel> EnsureAsync(string modelSpec, string? mirror = null, CancellationToken cancellationToken = default);
    bool IsDownloading { get; }
    event EventHandler<OcrDownloadProgressEventArgs>? ProgressChanged;
}

public class OcrDownloadProgressEventArgs : EventArgs
{
    public double ProgressPercentage { get; init; }
    public double BytesPerSecondSpeed { get; init; }
    public TimeSpan? EstimatedRemaining { get; init; }
}
