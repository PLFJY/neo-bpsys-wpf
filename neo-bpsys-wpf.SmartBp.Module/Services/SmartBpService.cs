using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;
using OpenCvSharp;
using System.Threading;
using System.Windows.Threading;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 智慧 BP 服务实现。
/// 负责对完整窗口捕获帧进行赛后数据 OCR，并按文本坐标回填当前对局数据。
/// </summary>
public class SmartBpService : ISmartBpService, IGameDataRecognitionDebugState
{
    private readonly ISharedDataService _sharedDataService;
    private readonly IWindowCaptureService _windowCaptureService;
    private readonly IOcrService _ocrService;
    private readonly ICharacterSelectionService _characterSelectionService;
    private readonly ISmartBpRecognitionSettingsService _recognitionSettingsService;
    private readonly ILogger<SmartBpService> _logger;
    private readonly DispatcherTimer _timer;
    private int _ocrWarmupStarted;

    /// <inheritdoc />
    public event EventHandler? SnapshotChanged;

    /// <inheritdoc />
    public GameDataRecognitionDebugSnapshot Current { get; private set; } = GameDataRecognitionDebugSnapshot.Empty;

    /// <summary>
    /// 获取当前 SmartBp 是否处于运行状态。
    /// </summary>
    public bool IsSmartBpRunning { get; private set; }

    /// <summary>
    /// 初始化 <see cref="SmartBpService"/> 的新实例。
    /// </summary>
    /// <param name="sharedDataService">共享对局数据服务。</param>
    /// <param name="windowCaptureService">窗口捕获服务。</param>
    /// <param name="ocrService">OCR 服务。</param>
    /// <param name="characterSelectionService">角色匹配与选择服务。</param>
    /// <param name="recognitionSettingsService">SmartBP 识别设置服务。</param>
    /// <param name="logger">日志记录器。</param>
    public SmartBpService(
        ISharedDataService sharedDataService,
        IWindowCaptureService windowCaptureService,
        IOcrService ocrService,
        ICharacterSelectionService characterSelectionService,
        ISmartBpRecognitionSettingsService recognitionSettingsService,
        ILogger<SmartBpService> logger)
    {
        _sharedDataService = sharedDataService;
        _windowCaptureService = windowCaptureService;
        _ocrService = ocrService;
        _characterSelectionService = characterSelectionService;
        _recognitionSettingsService = recognitionSettingsService;
        _logger = logger;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
        _timer.Tick += Timer_Tick;
    }

    /// <inheritdoc />
    public void StartSmartBp()
    {
        if (!IsOcrReady())
        {
            IsSmartBpRunning = false;
            _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("SmartBpOcrNotReadyFirstDownloadAndSwitchModel"));
            return;
        }

        if (IsSmartBpRunning)
        {
            _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("SmartBpAlreadyRunning"));
            return;
        }

        _timer.Start();
        IsSmartBpRunning = true;
        StartOcrWarmupIfNeeded();
    }

    /// <inheritdoc />
    public void StopSmartBp()
    {
        if (!IsSmartBpRunning)
            return;

        _timer.Stop();
        IsSmartBpRunning = false;
    }

    /// <inheritdoc />
    public async Task AutoFillGameDataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!IsOcrReady())
            {
                _logger.LogDebug("SmartBp AutoFill skipped: OCR model is not ready.");
                await MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("SmartBpOcrNotReadyFirstDownloadAndSwitchModel"));
                return;
            }

            if (!_windowCaptureService.IsCapturing || _windowCaptureService.GetCurrentFrame() == null)
            {
                _logger.LogDebug("SmartBp AutoFill skipped: capture or current frame is unavailable.");
                await MessageBoxHelper.ShowInfoAsync(I18nHelper.GetLocalizedString(
                    _windowCaptureService.IsCapturing
                        ? "SmartBpValidationCaptureFrameUnavailable"
                        : "SmartBpValidationCaptureNotRunning"));
                return;
            }

            var recognizedData = await Task.Run(
                () => CaptureAndRecognizeGameData(cancellationToken),
                cancellationToken);
            if (recognizedData == null)
            {
                await MessageBoxHelper.ShowInfoAsync(I18nHelper.GetLocalizedString("SmartBpValidationGameDataRecognitionNoResult"));
                return;
            }

            ApplyRecognizedData(recognizedData);
            _logger.LogDebug("SmartBp AutoFill succeeded: {SurvivorCount} survivor rows applied.", recognizedData.SurvivorInfos.Count);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("SmartBp AutoFill canceled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SmartBp AutoFill failed with exception. {Message}", ex.Message);
            await MessageBoxHelper.ShowErrorAsync(string.Format(
                I18nHelper.GetLocalizedString("SmartBpOperationFailedFormat"), ex.Message));
        }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (!_windowCaptureService.IsCapturing)
            _logger.LogDebug("SmartBp auto BP skipped: capture is not running.");
    }

    private void StartOcrWarmupIfNeeded()
    {
        if (Interlocked.Exchange(ref _ocrWarmupStarted, 1) == 1)
            return;

        _ = Task.Run(() =>
        {
            try
            {
                using var warmup = new Mat(new Size(512, 96), MatType.CV_8UC1, Scalar.All(255));
                _ = _ocrService.RecognizeTextLines(warmup);
            }
            catch
            {
                // 预热失败不影响主流程。
            }
        });
    }

    private RecognizedGameData? CaptureAndRecognizeGameData(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var frame = _windowCaptureService.GetCurrentFrame();
        if (frame == null)
            return null;

        using var full = frame.ToBgrMat();
        var ocrResult = _ocrService.RecognizeTextLines(full);
        var parsed = GameDataTableOcrParser.Parse(ocrResult.Lines);
        foreach (var diagnostic in parsed.Diagnostics)
            _logger.LogDebug("SmartBp GameData table OCR: {Diagnostic}", diagnostic);

        PublishGameDataDebugSnapshot(ocrResult.Lines.Count, parsed);
        if (parsed.Rows.Count == 0)
            return null;

        // OCR 常会漏掉界面中的“-”空值标记；名称和角色已可靠定位时，仍应回填该行，
        // 未识别的数据列沿用 PlayerData 的空字符串语义。
        var hunterRow = parsed.Rows.SingleOrDefault(row => row.RowIndex == 0);
        var hunterData = hunterRow == null ? null : ToHunterData(hunterRow.Values);
        var survivorInfos = parsed.Rows
            .Where(row => row.RowIndex is >= 1 and <= 4)
            .Select(row => new PlayerInfo(row.PlayerName, row.CharacterName, ToSurvivorData(row.Values)))
            .ToList();
        _logger.LogInformation(
            "SmartBp GameData table OCR parsed. OcrLineCount={OcrLineCount}, ParsedRowCount={ParsedRowCount}, CompleteRowCount={CompleteRowCount}, HunterFound={HunterFound}, SurvivorRowCount={SurvivorRowCount}",
            ocrResult.Lines.Count, parsed.Rows.Count, parsed.Rows.Count(row => row.HasAllDataColumns), hunterData != null, survivorInfos.Count);
        return hunterData == null && survivorInfos.Count == 0 ? null : new RecognizedGameData(hunterData, survivorInfos);
    }

    private void PublishGameDataDebugSnapshot(int ocrLineCount, GameDataTableParseResult parsed)
    {
        var rows = parsed.Rows.Select(row =>
        {
            var camp = row.RowIndex == 0 ? Camp.Hun : Camp.Sur;
            return new GameDataRecognitionDebugRow(
                row.RowIndex, row.PlayerName, row.CharacterName,
                _characterSelectionService.ResolveCharacter(row.CharacterName, camp)?.Name,
                row.Values, row.HasAllDataColumns);
        }).ToArray();
        Current = new GameDataRecognitionDebugSnapshot(ocrLineCount, rows, parsed.Diagnostics);
        SnapshotChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyRecognizedData(RecognizedGameData recognizedData)
    {
        if (recognizedData.HunterData != null)
        {
            var target = _sharedDataService.CurrentGame.HunPlayer.Data;
            target.RemainingCipher = recognizedData.HunterData.RemainingCipher;
            target.PalletsDestroyed = recognizedData.HunterData.PalletsDestroyed;
            target.SurvivorHits = recognizedData.HunterData.SurvivorHits;
            target.TerrorShocks = recognizedData.HunterData.TerrorShocks;
            target.Knockdowns = recognizedData.HunterData.Knockdowns;
        }

        foreach (var survivorInfo in recognizedData.SurvivorInfos)
        {
            var character = _characterSelectionService.ResolveCharacter(survivorInfo.CharacterName, Camp.Sur);
            var target = character == null ? null : _sharedDataService.CurrentGame.SurPlayerList
                .FirstOrDefault(player => string.Equals(player.Character?.Name, character.Name, StringComparison.Ordinal));
            if (target == null)
            {
                _logger.LogDebug("SmartBp Match failed: recognizedCharacter={Character}", ToLogText(survivorInfo.CharacterName));
                continue;
            }

            target.Data.DecodingProgress = survivorInfo.PlayerData.DecodingProgress;
            target.Data.PalletStrikes = survivorInfo.PlayerData.PalletStrikes;
            target.Data.Rescues = survivorInfo.PlayerData.Rescues;
            target.Data.Heals = survivorInfo.PlayerData.Heals;
            target.Data.ContainmentTime = survivorInfo.PlayerData.ContainmentTime;
        }
    }

    private bool IsOcrReady() => _ocrService.GetProviderStatus(_ocrService.SelectedProvider).IsReady;

    private static PlayerData ToHunterData(IReadOnlyList<string> values) => new()
    {
        RemainingCipher = values[0], PalletsDestroyed = values[1], SurvivorHits = values[2], TerrorShocks = values[3], Knockdowns = values[4]
    };

    private static PlayerData ToSurvivorData(IReadOnlyList<string> values) => new()
    {
        DecodingProgress = values[0], PalletStrikes = values[1], Rescues = values[2], Heals = values[3], ContainmentTime = values[4]
    };

    private static string ToLogText(string? text, int maxLength = 120)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var normalized = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return normalized.Length <= maxLength ? normalized : $"{normalized[..maxLength]}...";
    }

    /// <summary>
    /// 表示识别后的单个玩家信息。
    /// </summary>
    /// <param name="PlayerName">玩家名称。</param>
    /// <param name="CharacterName">角色名称。</param>
    /// <param name="PlayerData">玩家数据。</param>
    public record PlayerInfo(string PlayerName, string CharacterName, PlayerData PlayerData);

    private sealed record RecognizedGameData(PlayerData? HunterData, List<PlayerInfo> SurvivorInfos);
}
