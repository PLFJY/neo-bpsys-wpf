using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;
using OpenCvSharp;
using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 智慧 BP 服务实现。
/// 负责对完整窗口捕获帧进行赛后数据 OCR，并按文本坐标回填当前对局数据。
/// </summary>
public class SmartBpService : ISmartBpService, IGameDataRecognitionDebugState, IPostGameRecognitionProgressSource
{
    private readonly ISharedDataService _sharedDataService;
    private readonly IWindowCaptureService _windowCaptureService;
    private readonly IOcrService _ocrService;
    private readonly ICharacterSelectionService _characterSelectionService;
    private readonly ISmartBpRecognitionSettingsService _recognitionSettingsService;
    private readonly ISmartBpDebugLog _debugLog;
    private readonly ILogger<SmartBpService> _logger;
    private readonly DispatcherTimer _timer;
    private readonly PostGameOcrEngine _postGameOcrEngine;

    /// <inheritdoc />
    public event EventHandler? SnapshotChanged;

    /// <inheritdoc />
    public GameDataRecognitionDebugSnapshot Current { get; private set; } = GameDataRecognitionDebugSnapshot.Empty;

    /// <inheritdoc />
    public event EventHandler<PostGameRecognitionProgressEventArgs>? ProgressChanged;

    /// <inheritdoc />
    public PostGameRecognitionProgress CurrentProgress { get; private set; } = PostGameRecognitionProgress.Idle;

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
    /// <param name="debugLog">SmartBP 统一识别调试日志。</param>
    /// <param name="logger">日志记录器。</param>
    public SmartBpService(
        ISharedDataService sharedDataService,
        IWindowCaptureService windowCaptureService,
        IOcrService ocrService,
        ICharacterSelectionService characterSelectionService,
        ISmartBpRecognitionSettingsService recognitionSettingsService,
        ISmartBpDebugLog debugLog,
        ILogger<SmartBpService> logger)
    {
        _sharedDataService = sharedDataService;
        _windowCaptureService = windowCaptureService;
        _ocrService = ocrService;
        _characterSelectionService = characterSelectionService;
        _recognitionSettingsService = recognitionSettingsService;
        _debugLog = debugLog;
        _logger = logger;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
        _timer.Tick += Timer_Tick;
        _postGameOcrEngine = new PostGameOcrEngine(_ocrService, _debugLog, _logger);
        _postGameOcrEngine.StageProgress = (percent, stage) => RaiseProgress(percent, stage);
    }

    /// <summary>
    /// 更新当前进度快照并触发 <see cref="ProgressChanged"/> 事件。
    /// 可能在后台线程被调用；订阅方需自行切换到 UI 线程。
    /// </summary>
    /// <param name="percent">非线性进度百分比。</param>
    /// <param name="stage">当前逻辑阶段。</param>
    private void RaiseProgress(int percent, PostGameRecognitionStage stage)
    {
        CurrentProgress = new PostGameRecognitionProgress(percent, stage, ResolveStageText(stage));
        ProgressChanged?.Invoke(this, new PostGameRecognitionProgressEventArgs(CurrentProgress));
    }

    /// <summary>
    /// 将逻辑阶段映射为面向用户的本地化提示文本。
    /// </summary>
    /// <param name="stage">逻辑阶段。</param>
    /// <returns>本地化提示文本。</returns>
    private static string ResolveStageText(PostGameRecognitionStage stage) => stage switch
    {
        PostGameRecognitionStage.Preparing => I18nHelper.GetLocalizedString("SmartBpPostGameStagePreparing"),
        PostGameRecognitionStage.PrimaryOcr => I18nHelper.GetLocalizedString("SmartBpPostGameStagePrimaryOcr"),
        PostGameRecognitionStage.GridOcr => I18nHelper.GetLocalizedString("SmartBpPostGameStageGridOcr"),
        PostGameRecognitionStage.SingleCell => I18nHelper.GetLocalizedString("SmartBpPostGameStageSingleCell"),
        PostGameRecognitionStage.Applying => I18nHelper.GetLocalizedString("SmartBpPostGameStageApplying"),
        PostGameRecognitionStage.Completed => I18nHelper.GetLocalizedString("SmartBpPostGameStageCompleted"),
        _ => string.Empty
    };

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
        _ = _postGameOcrEngine.EnsureWarmupAsync();
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
        _debugLog.Write("post-game", "赛后数据识别 requested.");
        try
        {
            if (!IsOcrReady())
            {
                _logger.LogDebug("SmartBp AutoFill skipped: OCR model is not ready.");
                _debugLog.Write("post-game", "skipped: OCR provider is not ready.");
                await MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("SmartBpOcrNotReadyFirstDownloadAndSwitchModel"));
                return;
            }

            if (!_windowCaptureService.IsCapturing || _windowCaptureService.GetCurrentFrame() == null)
            {
                _logger.LogDebug("SmartBp AutoFill skipped: capture or current frame is unavailable.");
                _debugLog.Write("post-game", $"skipped: capture_available={_windowCaptureService.IsCapturing}; frame_available={_windowCaptureService.GetCurrentFrame() != null}.");
                await MessageBoxHelper.ShowInfoAsync(I18nHelper.GetLocalizedString(
                    _windowCaptureService.IsCapturing
                        ? "SmartBpValidationCaptureFrameUnavailable"
                        : "SmartBpValidationCaptureNotRunning"));
                return;
            }

            RaiseProgress(5, PostGameRecognitionStage.Preparing);
            await _postGameOcrEngine.EnsureWarmupAsync().ConfigureAwait(false);

            var recognizedData = await Task.Run(
                () => CaptureAndRecognizeGameData(cancellationToken),
                cancellationToken);
            if (recognizedData == null)
            {
                _debugLog.Write("post-game", "finished: no usable post-game rows were parsed.");
                RaiseProgress(100, PostGameRecognitionStage.Completed);
                await MessageBoxHelper.ShowInfoAsync(I18nHelper.GetLocalizedString("SmartBpValidationGameDataRecognitionNoResult"));
                return;
            }

            RaiseProgress(95, PostGameRecognitionStage.Applying);
            ApplyRecognizedData(recognizedData);
            _logger.LogDebug("SmartBp AutoFill succeeded: {SurvivorCount} survivor rows applied.", recognizedData.SurvivorInfos.Count);
            _debugLog.Write("post-game", $"finished: hunter_present={recognizedData.HunterData != null}; survivor_rows={recognizedData.SurvivorInfos.Count}.");
            RaiseProgress(100, PostGameRecognitionStage.Completed);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("SmartBp AutoFill canceled.");
            _debugLog.Write("post-game", "canceled.");
            RaiseProgress(0, PostGameRecognitionStage.Idle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SmartBp AutoFill failed with exception. {Message}", ex.Message);
            _debugLog.Write("post-game", $"failed: {ToLogText(ex.ToString())}");
            RaiseProgress(0, PostGameRecognitionStage.Idle);
            await MessageBoxHelper.ShowErrorAsync(string.Format(
                I18nHelper.GetLocalizedString("SmartBpOperationFailedFormat"), ex.Message));
        }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (!_windowCaptureService.IsCapturing)
            _logger.LogDebug("SmartBp auto BP skipped: capture is not running.");
    }

    private RecognizedGameData? CaptureAndRecognizeGameData(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var captureSw = Stopwatch.StartNew();
        var frame = _windowCaptureService.GetCurrentFrame();
        captureSw.Stop();
        if (frame == null)
            return null;

        var bitmapToMatSw = Stopwatch.StartNew();
        using var full = frame.ToBgrMat();
        bitmapToMatSw.Stop();

        _debugLog.Write(
            "post-game",
            $"capture: pixel_size={frame.PixelWidth}x{frame.PixelHeight}; provider={_ocrService.SelectedProvider}; configured_provider_details=[{ToLogText(_ocrService.GetProviderStatus(_ocrService.SelectedProvider).Details)}].");

        var runResult = _postGameOcrEngine.RunAsync(full, captureSw.ElapsedMilliseconds, bitmapToMatSw.ElapsedMilliseconds, cancellationToken)
            .GetAwaiter().GetResult();
        var parsed = runResult.Parsed;
        var ocrLineCount = runResult.Lines.Count;

        foreach (var diagnostic in parsed.Diagnostics)
            _logger.LogDebug("SmartBp GameData table OCR: {Diagnostic}", diagnostic);

        foreach (var row in parsed.Rows)
        {
            _debugLog.Write(
                "post-game",
                $"row[{row.RowIndex}]: raw_name=[{ToLogText(row.RawNameText)}]; player=[{ToLogText(row.PlayerName)}]; character=[{ToLogText(row.CharacterName)}]; values=[{string.Join(",", row.Values)}]; complete={row.HasAllDataColumns}.");
        }

        PublishGameDataDebugSnapshot(ocrLineCount, parsed);
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
            ocrLineCount, parsed.Rows.Count, parsed.Rows.Count(row => row.HasAllDataColumns), hunterData != null, survivorInfos.Count);
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
            _debugLog.Write("post-game", $"apply hunter data: values=[{recognizedData.HunterData.RemainingCipher},{recognizedData.HunterData.PalletsDestroyed},{recognizedData.HunterData.SurvivorHits},{recognizedData.HunterData.TerrorShocks},{recognizedData.HunterData.Knockdowns}].");
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
                _debugLog.Write("post-game", $"apply survivor skipped: player=[{ToLogText(survivorInfo.PlayerName)}]; character=[{ToLogText(survivorInfo.CharacterName)}]; resolved_character=[{character?.Name ?? "unresolved"}].");
                continue;
            }

            _debugLog.Write("post-game", $"apply survivor: player=[{ToLogText(survivorInfo.PlayerName)}]; character=[{character?.Name ?? "unresolved"}]; values=[{survivorInfo.PlayerData.DecodingProgress},{survivorInfo.PlayerData.PalletStrikes},{survivorInfo.PlayerData.Rescues},{survivorInfo.PlayerData.Heals},{survivorInfo.PlayerData.ContainmentTime}].");
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
