using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Sdcb.PaddleOCR.Models;
using Sdcb.PaddleOCR.Models.Online;
using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.Services;

public class OcrModelService : IOcrModelService
{
    private FullOcrModel? _model;
    public bool IsDownloading { get; private set; }
    public event EventHandler<OcrDownloadProgressEventArgs>? ProgressChanged;

    private static readonly Dictionary<string, long> ModelSizeBytes = new()
    {
        { "ChineseV3", 105L * 1024 * 1024 },
        { "ChineseV4", 111L * 1024 * 1024 },
        { "EnglishV3", 100L * 1024 * 1024 },
        { "EnglishV4", 106L * 1024 * 1024 }
    };

    public async Task<FullOcrModel> EnsureAsync(string modelSpec, string? mirror = null, CancellationToken cancellationToken = default)
    {
        if (_model != null) return _model;
        IsDownloading = true;
        var sw = Stopwatch.StartNew();
        var size = ModelSizeBytes.TryGetValue(modelSpec, out var v) ? v : 105L * 1024 * 1024;
        double lastReported = 0;
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
        var progressTask = Task.Run(async () =>
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var elapsed = sw.Elapsed.TotalSeconds;
                var speed = Math.Max(4.0, Math.Min(20.0, elapsed < 2 ? 6.0 : 12.0)); // MB/s 估算
                var downloaded = Math.Min(size, (long)(speed * 1024 * 1024 * elapsed));
                var pct = Math.Max(lastReported, Math.Min(99.0, downloaded * 100.0 / size));
                lastReported = pct;
                var remainingBytes = Math.Max(0, size - downloaded);
                var remainingSec = speed <= 0 ? (double?)null : remainingBytes / 1024.0 / 1024.0 / speed;
                ProgressChanged?.Invoke(this, new OcrDownloadProgressEventArgs
                {
                    ProgressPercentage = pct,
                    BytesPerSecondSpeed = speed * 1024 * 1024,
                    EstimatedRemaining = remainingSec is null ? null : TimeSpan.FromSeconds(remainingSec.Value)
                });
            }
        }, cancellationToken);

        try
        {
            _model = await DownloadModelBySpecAsync(modelSpec, cancellationToken);
            ProgressChanged?.Invoke(this, new OcrDownloadProgressEventArgs
            {
                ProgressPercentage = 100.0,
                BytesPerSecondSpeed = size / Math.Max(1, sw.Elapsed.TotalSeconds),
                EstimatedRemaining = TimeSpan.Zero
            });
            return _model;
        }
        finally
        {
            IsDownloading = false;
            sw.Stop();
        }
    }

    private static Task<FullOcrModel> DownloadModelBySpecAsync(string modelSpec, CancellationToken ct)
    {
        return modelSpec switch
        {
            "ChineseV4" => OnlineFullModels.ChineseV4.DownloadAsync(ct),
            "EnglishV3" => OnlineFullModels.EnglishV3.DownloadAsync(ct),
            "EnglishV4" => OnlineFullModels.EnglishV4.DownloadAsync(ct),
            _ => OnlineFullModels.ChineseV3.DownloadAsync(ct)
        };
    }
}
