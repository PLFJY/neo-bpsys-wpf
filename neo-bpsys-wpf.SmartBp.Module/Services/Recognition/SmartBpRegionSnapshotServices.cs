using System.Windows.Media.Imaging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

/// <summary>
/// 将阶段识别和各区域聚焦识别结果合并成单帧 BP 识别结果。
/// </summary>
internal sealed class SmartBpBusinessStateMerger : ISmartBpBusinessStateMerger
{
    /// <inheritdoc />
    public SmartBpBusinessStateRecognitionResult Merge(
        SmartBpPhaseRecognitionResult phase,
        SmartBpFocusedBusinessExtractionResult? bannedSur,
        SmartBpFocusedBusinessExtractionResult? bannedHun,
        SmartBpFocusedBusinessExtractionResult? pickedSur,
        SmartBpFocusedBusinessExtractionResult? pickedHun)
    {
        var result = new SmartBpBusinessStateRecognitionResult
        {
            Phase = phase.Phase,
            BannedSur = bannedSur?.Slots.Select(ToCharacterSlot).ToList() ?? DefaultCharacterSlots(4),
            BannedHun = bannedHun?.Slots.Select(ToCharacterSlot).ToList() ?? DefaultCharacterSlots(2),
            PickedSur = pickedSur?.Slots.Select(ClonePlayerSlot).ToList() ?? DefaultPlayerSlots(4),
            PickedHun = pickedHun?.PickedHun is { } hunter
                ? ClonePlayerSlot(hunter)
                : new SmartBpRecognizedPlayerCharacterSlot { Index = 0, CharacterName = "未选择" }
        };
        SmartBpBusinessStateParser.NormalizeAndValidate(result);
        return result;
    }

    private static List<SmartBpRecognizedCharacterSlot> DefaultCharacterSlots(int count) =>
        Enumerable.Range(0, count).Select(index => new SmartBpRecognizedCharacterSlot { Index = index, CharacterName = "未选择" }).ToList();

    private static List<SmartBpRecognizedPlayerCharacterSlot> DefaultPlayerSlots(int count) =>
        Enumerable.Range(0, count).Select(index => new SmartBpRecognizedPlayerCharacterSlot { Index = index, CharacterName = "未选择" }).ToList();

    private static SmartBpRecognizedCharacterSlot ToCharacterSlot(SmartBpRecognizedPlayerCharacterSlot slot) =>
        new() { Index = slot.Index, CharacterName = slot.CharacterName, SlotState = slot.SlotState, RecognitionConfidence = slot.RecognitionConfidence, IsAutoApplySafe = slot.IsAutoApplySafe, RecognitionReason = slot.RecognitionReason, BoundingBox = slot.BoundingBox };

    private static SmartBpRecognizedPlayerCharacterSlot ClonePlayerSlot(SmartBpRecognizedPlayerCharacterSlot slot) =>
        new() { Index = slot.Index, CharacterName = slot.CharacterName, PlayerId = slot.PlayerId, SlotState = slot.SlotState, RecognitionConfidence = slot.RecognitionConfidence, IsAutoApplySafe = slot.IsAutoApplySafe, RecognitionReason = slot.RecognitionReason, BoundingBox = slot.BoundingBox };
}

/// <summary>
/// 为每次识别请求当前画面中的全部 BP 角色槽位，不读取或维护 SmartBP 业务状态。
/// </summary>
internal sealed class SmartBpSnapshotRecognitionPlanner : ISmartBpSnapshotRecognitionPlanner
{
    /// <inheritdoc />
    public SmartBpSnapshotDeltaRequest BuildRequest(GameGuidanceRuntimeSnapshot guidanceSnapshot) =>
        new(
        [
            (SmartBpRecognitionRegion.RightTop, "banned_sur"),
            (SmartBpRecognitionRegion.LeftTop, "banned_hun"),
            (SmartBpRecognitionRegion.LeftBottom, "picked_sur"),
            (SmartBpRecognitionRegion.RightBottom, "picked_hun")
        ],
        [
            $"Current-frame slot recognition requested all BP character fields; guidanceStep={guidanceSnapshot.CurrentStepIndex}; action={guidanceSnapshot.CurrentAction}."
        ]);
}

/// <summary>
/// 保留最近若干捕获帧，供捕获采样和 OCR 性能诊断使用；不保存 BP 业务状态。
/// </summary>
internal sealed class SmartBpFrameRingBuffer(
    ISmartBpRecognitionSettingsService settings,
    ISharedDataService shared) : ISmartBpFrameRingBuffer
{
    private readonly object _gate = new();
    private readonly Queue<SmartBpBufferedFrame> _frames = new();
    private long _maximumObservedOcrProcessingMilliseconds;

    /// <inheritdoc />
    public int Count
    {
        get { lock (_gate) return _frames.Count; }
    }

    /// <inheritdoc />
    public int Capacity
    {
        get
        {
            var sampleInterval = Math.Max(50, settings.Settings.RecognitionSamplingIntervalMilliseconds);
            return Math.Clamp((int)Math.Ceiling(EffectiveWindow.TotalMilliseconds / sampleInterval) + 4, 8, 256);
        }
    }

    /// <inheritdoc />
    public void AddFrame(long sequence, BitmapSource frame, DateTimeOffset timestamp)
    {
        lock (_gate)
        {
            var game = shared.CurrentGame;
            _frames.Enqueue(new(sequence, frame, timestamp, game.Guid, game.GameProgress));
            Trim(timestamp, EffectiveWindow);
            while (_frames.Count > Capacity)
                _frames.Dequeue();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<SmartBpBufferedFrame> GetRecentFrames(TimeSpan window)
    {
        var cutoff = DateTimeOffset.Now - window;
        var game = shared.CurrentGame;
        lock (_gate) return _frames.Where(frame =>
            frame.Timestamp >= cutoff &&
            frame.GameGuid == game.Guid &&
            frame.GameProgress == game.GameProgress).ToArray();
    }

    /// <inheritdoc />
    public SmartBpBufferedFrame? GetBestFrameForRegion(SmartBpRecognitionRegion region, TimeSpan lookBehind) =>
        GetRecentFrames(lookBehind).OrderByDescending(frame => frame.Sequence).FirstOrDefault();

    /// <inheritdoc />
    public void Reset()
    {
        lock (_gate)
            _frames.Clear();
    }

    /// <inheritdoc />
    public void ReportOcrProcessingDuration(TimeSpan duration)
    {
        var measured = Math.Clamp((long)Math.Ceiling(duration.TotalMilliseconds), 0, 300000);
        long previous;
        do
        {
            previous = Interlocked.Read(ref _maximumObservedOcrProcessingMilliseconds);
            if (measured <= previous)
                return;
        } while (Interlocked.CompareExchange(ref _maximumObservedOcrProcessingMilliseconds, measured, previous) != previous);
    }

    private void Trim(DateTimeOffset now, TimeSpan window)
    {
        while (_frames.TryPeek(out var frame) && now - frame.Timestamp > window)
            _frames.Dequeue();
    }

    private TimeSpan EffectiveWindow => TimeSpan.FromMilliseconds(Math.Max(
        Math.Max(
            settings.Settings.RecognitionFrameBufferMilliseconds,
            settings.Settings.RecognitionTransitionLookBehindMilliseconds),
        settings.Settings.OcrRecognitionIntervalMs +
        Math.Max(settings.Settings.MinimumOcrRecognitionIntervalMs, Interlocked.Read(ref _maximumObservedOcrProcessingMilliseconds))));
}

/// <summary>
/// 对裁剪图进行低分辨率采样，判断区域画面是否发生足够变化。
/// </summary>
internal sealed class SmartBpCropChangeDetector(ISmartBpRecognitionSettingsService settings) : ISmartBpCropChangeDetector
{
    private readonly object _gate = new();
    private readonly Dictionary<SmartBpRecognitionRegion, byte[]> _previous = [];
    private readonly Dictionary<SmartBpRecognitionRegion, int> _stableCounts = [];

    /// <inheritdoc />
    public SmartBpCropChangeResult Analyze(SmartBpRecognitionRegion region, BitmapSource crop, long sequence)
    {
        var sample = Sample(crop);
        lock (_gate)
        {
            var difference = _previous.TryGetValue(region, out var previous) ? Difference(previous, sample) : 1;
            var changed = difference >= settings.Settings.RecognitionCropChangeThreshold;
            _stableCounts[region] = changed ? 0 : _stableCounts.GetValueOrDefault(region) + 1;
            _previous[region] = sample;
            return new(region, sequence, difference, changed, _stableCounts[region] >= settings.Settings.RecognitionCropStableFrames);
        }
    }

    private static byte[] Sample(BitmapSource source)
    {
        var width = Math.Max(1, Math.Min(32, source.PixelWidth));
        var height = Math.Max(1, Math.Min(18, source.PixelHeight));
        var scaled = new TransformedBitmap(source, new System.Windows.Media.ScaleTransform((double)width / source.PixelWidth, (double)height / source.PixelHeight));
        var converted = new FormatConvertedBitmap(scaled, System.Windows.Media.PixelFormats.Gray8, null, 0);
        var pixels = new byte[width * height];
        converted.CopyPixels(pixels, width, 0);
        return pixels;
    }

    private static double Difference(byte[] left, byte[] right)
    {
        var count = Math.Min(left.Length, right.Length);
        if (count == 0) return 1;
        long sum = 0;
        for (var index = 0; index < count; index++)
            sum += Math.Abs(left[index] - right[index]);
        return sum / (count * 255.0);
    }
}
