using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.WebRenderer.Protocol;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using neo_bpsys_wpf.Core.Enums;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace neo_bpsys_wpf.WebRenderer.Services;

/// <summary>将当前布局实际引用的共享数据投影为只读 Web Runtime 状态。</summary>
public sealed class WebRendererRuntimeStatePublisher : IDisposable
{
    private readonly ISharedDataService _sharedData;
    private readonly IFrontedEventBus _eventBus;
    private readonly WebRuntimeAssetRegistry _assets = new();
    private readonly WebRuntimeValueFactory _valueFactory;
    private readonly IWebGameProgressProvider? _gameProgressProvider;
    private readonly ISettingsHostService? _settingsHostService;
    private readonly IWebLocalizationProvider? _localizationProvider;
    private readonly IGameGuidanceService? _gameGuidanceService;
    private readonly ILogger<WebRendererRuntimeStatePublisher>? _logger;
    private readonly object _gate = new();
    private readonly Dictionary<string, BindingPathObserver> _observers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WebRuntimeValue> _values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WebRuntimeAsset> _stableAssets = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, IReadOnlyList<string>> _pathsByBehaviorGuid = [];
    private readonly Dictionary<string, WebControlProjection> _controlProjections = new(StringComparer.Ordinal);
    private readonly List<WebRuntimeCommitBarrier> _commitBarriers = [];
    private long _generation;
    private long _sequence;
    private long _recalculationVersion;
    private long _localizationRevision;
    private CultureInfo _localizationCulture = CultureInfo.InvariantCulture;
    private int _clientCount;
    private bool _recalculationQueued;

    /// <summary>发生可发送的完整快照或增量更新时触发。</summary>
    public event EventHandler<WebRendererRuntimeUpdate>? Updated;
    /// <summary>需要 sidecar 异步准备远程图片时发生。</summary>
    public event EventHandler<WebRemoteAssetFetch>? RemoteAssetRequested;
    /// <summary>发布可安全发送给 Web 页面的语义行为事件。</summary>
    public event EventHandler<WebBehaviorEventMessage>? BehaviorEventPublished;
    /// <summary>获取当前由 sidecar 报告的已连接客户端数量。</summary>
    public int ClientCount => Volatile.Read(ref _clientCount);
    /// <summary>获取最近发布的 runtime sequence。</summary>
    public long CurrentSequence { get { lock (_gate) return _sequence; } }

    /// <summary>创建运行时发布器。</summary>
    public WebRendererRuntimeStatePublisher(
        ISharedDataService sharedData,
        IFrontedEventBus eventBus,
        IWebGameProgressProvider? gameProgressProvider = null,
        ISettingsHostService? settingsHostService = null,
        IWebLocalizationProvider? localizationProvider = null,
        ILogger<WebRendererRuntimeStatePublisher>? logger = null,
        IGameGuidanceService? gameGuidanceService = null)
    {
        _sharedData = sharedData;
        _eventBus = eventBus;
        _gameProgressProvider = gameProgressProvider;
        _settingsHostService = settingsHostService;
        _localizationProvider = localizationProvider;
        _logger = logger;
        _gameGuidanceService = gameGuidanceService;
        _valueFactory = new(_assets);
        _assets.AssetStateChanged += OnAssetStateChanged;
    }

    /// <summary>使用新 bootstrap 重新收集所有可消费的绑定路径。</summary>
    public void ReplaceLayout(WebRendererBootstrapSnapshot snapshot)
    {
        lock (_gate)
        {
            StopLocked();
            // StopLocked 也用于“最后一个浏览器断开”场景，不能在那里清空 observer；
            // 但布局 generation 切换必须释放路径表，否则第二次保存会对相同路径重复 Add。
            _observers.Clear();
            _pathsByBehaviorGuid.Clear();
            _controlProjections.Clear();
            _generation = snapshot.Generation;
            _assets.ResetRemoteSession();
            var paths = snapshot.Windows.Where(window => window.Layout is not null).SelectMany(window => EnumeratePaths(window.Layout!)).Where(path => !string.IsNullOrWhiteSpace(path)).ToHashSet(StringComparer.Ordinal);
            foreach (var path in paths) _observers.Add(path, new BindingPathObserver(_sharedData, path, OnPathChanged));
            foreach (var control in snapshot.Windows
                         .Where(window => window.Layout is not null)
                         .SelectMany(window => window.Layout!.ControlLayout.Controls.Values)
                         .Where(control => control.BehaviorGuid != Guid.Empty))
            {
                var controlPaths = EnumerateControlPaths(control).Distinct(StringComparer.Ordinal).ToArray();
                if (controlPaths.Length > 0) _pathsByBehaviorGuid[control.BehaviorGuid] = controlPaths;
            }
            foreach (var window in snapshot.Windows.Where(window => window.Layout is not null))
            {
                foreach (var pair in window.Layout!.ControlLayout.Controls)
                    _controlProjections[WebRendererBootstrapBuilder.GetControlId(window.FullWindowType, pair.Key)] = new(window.FullWindowType, pair.Key, pair.Value);
                foreach (var state in window.Layout.CanvasSettings.BoModeStates?.Values ?? Enumerable.Empty<FrontedCanvasStateConfig>())
                    foreach (var pair in state.Controls ?? [])
                        _controlProjections[WebRendererBootstrapBuilder.GetControlId(window.FullWindowType, pair.Key)] = new(window.FullWindowType, pair.Key, pair.Value);
            }
            if (_clientCount > 0) StartLocked(true);
        }
    }

    /// <summary>在 bootstrap 获得 sidecar 确认后发布当前完整运行时快照。</summary>
    public void PublishConfirmedSnapshot() { lock (_gate) { StartLocked(true); } }

    /// <summary>原子切换当前 runtime 使用的本地化修订与显式文化。</summary>
    /// <param name="snapshot">已经由 sidecar 确认的本地化快照。</param>
    public void SetLocalizationSnapshot(WebLocalizationSnapshot snapshot)
    {
        lock (_gate)
        {
            if (snapshot.Revision < _localizationRevision) return;
            _localizationRevision = snapshot.Revision;
            _localizationCulture = CultureInfo.GetCultureInfo(snapshot.Culture);
        }
        QueueRecalculation();
    }

    /// <summary>应用 sidecar 返回的远程图片准备结果。</summary>
    /// <param name="result">远程图片结果。</param>
    public void ApplyRemoteAssetResult(WebRemoteAssetResult result)
    {
        lock (_gate)
        {
            if (result.Generation != _generation) return;
            _assets.CompleteRemote(result.Token, result.Revision, result.ContentType, result.Diagnostic);
        }
    }
    /// <summary>更新 sidecar 当前的连接页面数量。</summary>
    public void SetClientCount(int clientCount) { lock (_gate) { _clientCount = Math.Max(0, clientCount); if (_clientCount == 0) StopLocked(); else StartLocked(true); } }

    private void StartLocked(bool snapshot)
    {
        if (!_observers.Values.Any(observer => observer.IsRunning))
        {
            foreach (var observer in _observers.Values) observer.Start();
            _eventBus.EventPublished += OnEventPublished;
        }
        RecalculateLocked(snapshot);
        PublishAuthoritativeBehaviorStateLocked();
    }

    private void StopLocked()
    {
        _eventBus.EventPublished -= OnEventPublished;
        foreach (var observer in _observers.Values) observer.Dispose();
        _values.Clear();
        _stableAssets.Clear();
        _assets.ReplaceReferences([]);
        CompleteCommitBarriersLocked(isStable: false);
    }

    private void OnPathChanged() => QueueRecalculation();
    private void OnAssetStateChanged(object? sender, EventArgs args)
    {
        QueueRecalculation();
    }
    private void OnEventPublished(object? sender, FrontedBehaviorEvent args)
    {
        lock (_gate) if (_clientCount > 0)
        {
            var message = WebBehaviorEventMessage.From(args, diagnostic => _logger?.LogWarning("{Diagnostic}", diagnostic));
            BehaviorEventPublished?.Invoke(this, message);
        }
        QueueRecalculation();
    }

    private void PublishAuthoritativeBehaviorStateLocked()
    {
        if (_clientCount == 0) return;

        foreach (var pair in _sharedData.CurrentGame.MapV2Dictionary)
        {
            PublishStateEvent(new FrontedBehaviorEvent
            {
                EventType = "MapV2.PickingBorderStateChanged",
                Source = "WebRendererStateReplay",
                Payload = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["MapKey"] = pair.Key,
                    ["IsMapV2Breathing"] = _sharedData.IsMapV2Breathing,
                    ["IsMapBanned"] = pair.Value.IsBanned,
                    ["IsPickingBorderVisible"] = _sharedData.IsMapV2Breathing && !pair.Value.IsBanned
                }
            });
        }

        var guidance = _gameGuidanceService?.GetRuntimeSnapshot();
        if (guidance is not { IsStarted: true, CurrentAction: not null }) return;
        PublishStateEvent(new FrontedBehaviorEvent
        {
            EventType = "Guidance.StepChanged",
            Source = "WebRendererStateReplay",
            Payload = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["StepIndex"] = guidance.CurrentStepIndex,
                ["Action"] = guidance.CurrentAction.Value,
                ["Indexes"] = guidance.CurrentIndexes.ToArray(),
                ["Index"] = guidance.CurrentIndexes.Count > 0 ? guidance.CurrentIndexes[0] : null,
                ["Time"] = guidance.CurrentTime
            }
        });
    }

    private void PublishStateEvent(FrontedBehaviorEvent value)
    {
        var message = WebBehaviorEventMessage.From(value, diagnostic => _logger?.LogWarning("{Diagnostic}", diagnostic));
        _logger?.LogDebug("behavior state replay EventType={EventType} Source={Source}", value.EventType, value.Source);
        BehaviorEventPublished?.Invoke(this, message);
    }

    private void QueueRecalculation()
    {
        var dispatcher = Application.Current?.Dispatcher;
        lock (_gate)
        {
            if (_clientCount == 0 || _recalculationQueued) return;
            _recalculationQueued = true;
        }

        if (dispatcher is null)
        {
            FlushQueuedRecalculation();
            return;
        }

        try
        {
            _ = dispatcher.BeginInvoke(DispatcherPriority.Background, FlushQueuedRecalculation);
        }
        catch (InvalidOperationException)
        {
            lock (_gate) _recalculationQueued = false;
        }
    }

    private void FlushQueuedRecalculation()
    {
        lock (_gate)
        {
            _recalculationQueued = false;
            if (_clientCount > 0) RecalculateLocked(false);
        }
    }

    private void RecalculateLocked(bool snapshot)
    {
        _recalculationVersion++;
        var changed = new Dictionary<string, WebRuntimeValue>(StringComparer.Ordinal);
        var diagnostics = new List<WebRuntimeDiagnostic>();
        var activeImages = new HashSet<ImageSource>(ReferenceEqualityComparer.Instance);
        foreach (var pair in _observers)
        {
            var result = pair.Value.Resolve();
            if (result.Value is ImageSource image) activeImages.Add(image);
            WebRuntimeDiagnostic? conversionDiagnostic = null;
            var value = result.Diagnostic is null && pair.Key == "CurrentGame.GameProgress" && result.Value is GameProgress progress && _gameProgressProvider is not null
                ? new WebRuntimeValue("gameProgress", _gameProgressProvider.Create(progress, ResolveBo3Mode(), ResolveCulture()), typeof(GameProgress).FullName)
                : result.Diagnostic is null ? _valueFactory.Create(result.Value, pair.Key, out conversionDiagnostic) : new WebRuntimeValue("null", null, result.SourceType, result.Diagnostic);
            if (result.Diagnostic is not null) diagnostics.Add(new(pair.Key, result.Diagnostic, result.SourceType));
            else if (conversionDiagnostic is not null) diagnostics.Add(conversionDiagnostic);
            if (!_values.TryGetValue(pair.Key, out var previous) || previous != value) changed[pair.Key] = value;
            _values[pair.Key] = value;
            if (value.State == WebRuntimeValueStates.Resolved && value.Asset is not null)
            {
                _stableAssets[pair.Key] = value.Asset;
            }
            else if (value.State == WebRuntimeValueStates.Null)
            {
                _stableAssets.Remove(pair.Key);
            }
        }
        _assets.ReplaceActiveSources(activeImages);
        _assets.ReplaceReferences(_stableAssets.Values.Select(value => value.Token));
        foreach (var projection in _controlProjections.Values)
        {
            var controlId = WebRendererBootstrapBuilder.GetControlId(projection.WindowType, projection.ControlName);
            switch (projection.Config)
            {
                case LocalizedTextControlConfig localized:
                    changed[controlId] = _values[controlId] = new("localizedControl", ResolveLocalizedText(controlId, localized), typeof(string).FullName);
                    break;
                case MapNameTextControlConfig mapName:
                    var map = ResolvePath(mapName.BindingPath ?? "CurrentGame.PickedMap") is Map resolvedMap ? (Map?)resolvedMap : null;
                    changed[controlId] = _values[controlId] = new("mapName", _localizationProvider?.ResolveMapName(controlId, map, mapName.EmptyText, _localizationCulture), typeof(Map).FullName);
                    break;
                case GameProgressTextControlConfig progressConfig:
                    if (ResolvePath("CurrentGame.GameProgress") is GameProgress resolvedProgress && _localizationProvider is not null)
                        changed[controlId] = _values[controlId] = new("gameProgressDisplay", _localizationProvider.CreateGameProgress(resolvedProgress, ResolveBo3Mode(), progressConfig.DisplayLanguage, progressConfig.NumberStyle, _localizationCulture), typeof(GameProgress).FullName);
                    break;
                case MapV2DisplayControlConfig mapConfig:
                    var mapV2 = ResolvePath($"CurrentGame.MapV2Dictionary['{mapConfig.MapKey}']") as MapV2;
                    if (mapV2 is not null && _localizationProvider is not null)
                    {
                        var camp = mapV2.OperationTeam?.Camp;
                        var mapAsset = AssetFor($"CurrentGame.MapV2Dictionary['{mapConfig.MapKey}'].ImageSource");
                        var teamAsset = AssetFor($"CurrentGame.MapV2Dictionary['{mapConfig.MapKey}'].OperationTeam.Logo");
                        var state = new WebMapV2DisplayState(mapConfig.MapKey,
                            _localizationProvider.ResolveMapName(controlId, mapV2.MapName, null, _localizationCulture).DisplayText,
                            camp is null ? string.Empty : _localizationProvider.ResolveCamp(camp.Value, _localizationCulture),
                            mapV2.OperationTeam?.Name ?? string.Empty, teamAsset, mapAsset,
                            mapV2.IsBanned, mapV2.IsPicked, mapV2.IsCampVisible, camp?.ToString());
                        changed[controlId] = _values[controlId] = new("mapV2Display", state, typeof(MapV2).FullName);
                    }
                    break;
            }
        }
        foreach (var descriptor in _assets.DrainRemoteRequests())
            RemoteAssetRequested?.Invoke(this,
                new WebRemoteAssetFetch(_generation, descriptor.Token, descriptor.Revision, descriptor.NormalizedUri));
        if (snapshot) Updated?.Invoke(this, new WebRendererRuntimeUpdate(true, _generation, ++_sequence, new Dictionary<string, WebRuntimeValue>(_values), diagnostics) { LocalizationRevision = _localizationRevision });
        else if (changed.Count > 0) Updated?.Invoke(this, new WebRendererRuntimeUpdate(false, _generation, ++_sequence, changed, diagnostics) { LocalizationRevision = _localizationRevision });
        CompleteCommitBarriersLocked(isStable: true);
    }

    private bool ResolveBo3Mode() => _observers.TryGetValue("IsBo3Mode", out var observer)
        && observer.Resolve().Value is bool mode && mode;

    private CultureInfo ResolveCulture() =>
        _settingsHostService?.Settings.CultureInfo ?? CultureInfo.InvariantCulture;

    private object? ResolvePath(string path) => WebRendererBindingPathResolver.Resolve(_sharedData, path).Value;

    private WebLocalizedControlState ResolveLocalizedText(string controlId, LocalizedTextControlConfig config)
    {
        if (_localizationProvider is null) return new(controlId, string.Empty);
        var key = config.LocalizationKey;
        if (config.TextBinding is not null)
        {
            var values = config.TextBinding.GetActiveSources().Select(source => ResolvePath(source.Path)).ToArray();
            key = Convert.ToString(new FrontedTextMultiBindingConverter().Convert(values!, typeof(string), config.TextBinding, _localizationCulture), _localizationCulture) ?? string.Empty;
        }
        return _localizationProvider.ResolveLocalizedControl(controlId, key, config.FallbackText, _localizationCulture);
    }

    private WebRuntimeAsset? AssetFor(string path) => _values.TryGetValue(path, out var value) ? value.Asset : null;

    private sealed record WebControlProjection(string WindowType, string ControlName, FrontedControlConfigBase Config);

    internal WebRuntimeCommitBarrier BeginCommitBarrier(
        IReadOnlyList<FrontedTransitionRequest> requests,
        long generation)
    {
        lock (_gate)
        {
            var paths = requests
                .SelectMany(request => _pathsByBehaviorGuid.GetValueOrDefault(request.TargetBehaviorGuid) ?? [])
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var barrier = new WebRuntimeCommitBarrier(generation, _recalculationVersion, paths);
            if (_clientCount == 0 || generation != _generation)
            {
                barrier.Completion.TrySetResult(new WebRuntimeCommitPoint(_generation, _sequence, false));
            }
            else
            {
                _commitBarriers.Add(barrier);
            }

            return barrier;
        }
    }

    internal async Task<WebRuntimeCommitPoint> WaitForCommitBarrierAsync(
        WebRuntimeCommitBarrier barrier,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        QueueRecalculation();
        try
        {
            return await barrier.Completion.Task.WaitAsync(timeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            lock (_gate)
            {
                _commitBarriers.Remove(barrier);
                return new WebRuntimeCommitPoint(_generation, _sequence, false);
            }
        }
    }

    internal void CancelCommitBarrier(WebRuntimeCommitBarrier barrier)
    {
        lock (_gate)
        {
            _commitBarriers.Remove(barrier);
            barrier.Completion.TrySetResult(new WebRuntimeCommitPoint(_generation, _sequence, false));
        }
    }

    private void CompleteCommitBarriersLocked(bool isStable)
    {
        foreach (var barrier in _commitBarriers.ToArray())
        {
            if (!isStable || barrier.Generation != _generation)
            {
                _commitBarriers.Remove(barrier);
                barrier.Completion.TrySetResult(new WebRuntimeCommitPoint(_generation, _sequence, false));
                continue;
            }

            if (_recalculationVersion <= barrier.BaselineRecalculationVersion)
            {
                continue;
            }

            var pathsStable = barrier.Paths.All(path =>
                _values.TryGetValue(path, out var value)
                && value.State != WebRuntimeValueStates.Pending);
            if (!pathsStable)
            {
                continue;
            }

            _commitBarriers.Remove(barrier);
            barrier.Completion.TrySetResult(new WebRuntimeCommitPoint(_generation, _sequence, true));
        }
    }

    private static IEnumerable<string> EnumeratePaths(FrontedWindowConfig layout)
    {
        foreach (var control in layout.ControlLayout.Controls.Values)
        {
            foreach (var requiredPath in GetSpecialControlPaths(control.ControlType)) yield return requiredPath;
            if (control is MapV2DisplayControlConfig map && !string.IsNullOrWhiteSpace(map.MapKey))
            { var prefix = $"CurrentGame.MapV2Dictionary['{map.MapKey}']"; foreach (var suffix in new[] { "MapName", "ImageSource", "IsBanned", "IsPicked", "IsCampVisible", "OperationTeam.Name", "OperationTeam.Logo", "OperationTeam.Camp" }) yield return prefix + "." + suffix; }
            if (control is TalentTraitDisplayControlConfig talent)
            { var player = talent.DisplayKind.ToString().StartsWith("Survivor", StringComparison.Ordinal) ? $"CurrentGame.SurPlayerList[{talent.PlayerIndex ?? 0}]" : "CurrentGame.HunPlayer"; foreach (var name in new[] { "BorrowedTime", "TideTurner", "FlywheelEffect", "KneeJerkReflex", "TrumpCard", "Detention", "ConfinedSpace", "Insolence" }) yield return $"{player}.Talent.{name}"; }
            if (!string.IsNullOrWhiteSpace(control.BindingPath)) yield return control.BindingPath;
            foreach (var property in control.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.Name.EndsWith("BindingPath", StringComparison.Ordinal) && property.GetValue(control) is string path && !string.IsNullOrWhiteSpace(path)) yield return path;
                if (property.Name == "TextBinding" && property.GetValue(control) is { } binding && binding.GetType().GetProperty("Sources")?.GetValue(binding) is IEnumerable sources)
                    foreach (var source in sources) if (source?.GetType().GetProperty("Path")?.GetValue(source) is string sourcePath && !string.IsNullOrWhiteSpace(sourcePath)) yield return sourcePath;
            }
        }
    }
    private static IEnumerable<string> EnumerateControlPaths(FrontedControlConfigBase control)
    {
        if (!string.IsNullOrWhiteSpace(control.BindingPath)) yield return control.BindingPath;
        foreach (var property in control.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.Name.EndsWith("BindingPath", StringComparison.Ordinal)
                && property.GetValue(control) is string path
                && !string.IsNullOrWhiteSpace(path))
            {
                yield return path;
            }
        }
    }
    private static IEnumerable<string> GetSpecialControlPaths(string type) => type switch { "GameProgressText" => ["CurrentGame.GameProgress", "IsBo3Mode"], "MapNameText" => ["CurrentGame.PickedMap"], "TalentTraitDisplay" => ["IsTraitVisible"], "MapV2Display" => ["IsMapV2CampVisible", "IsMapV2Breathing"], "GlobalScoreRow" => ["IsBo3Mode"], _ => [] };
    /// <inheritdoc />
    public void Dispose() { lock (_gate) { StopLocked(); _assets.AssetStateChanged -= OnAssetStateChanged; _assets.Dispose(); } }
}

internal sealed class WebRuntimeCommitBarrier(
    long generation,
    long baselineRecalculationVersion,
    IReadOnlyList<string> paths)
{
    public long Generation { get; } = generation;
    public long BaselineRecalculationVersion { get; } = baselineRecalculationVersion;
    public IReadOnlyList<string> Paths { get; } = paths;
    public TaskCompletionSource<WebRuntimeCommitPoint> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

internal sealed record WebRuntimeCommitPoint(long Generation, long Sequence, bool IsStable);

/// <summary>运行时状态更新。</summary>
public sealed record WebRendererRuntimeUpdate(bool IsSnapshot, long Generation, long Sequence, IReadOnlyDictionary<string, WebRuntimeValue> Values, IReadOnlyList<WebRuntimeDiagnostic> Diagnostics)
{
    /// <summary>runtime 值 schema 版本。</summary>
    public int SchemaVersion { get; init; } = 2;
    /// <summary>与本次 runtime 值匹配的本地化修订。</summary>
    public long LocalizationRevision { get; init; }
}


/// <summary>只订阅一个解析路径实际经过对象的观察器。</summary>
internal sealed class BindingPathObserver(ISharedDataService root, string path, Action changed) : IDisposable
{
    private readonly List<IDisposable> _subscriptions = [];
    public bool IsRunning { get; private set; }
    public void Start() { if (IsRunning) return; IsRunning = true; Rebuild(); }
    public (object? Value, string? Diagnostic, string? SourceType) Resolve() => WebRendererBindingPathResolver.Resolve(root, path);
    private void Rebuild()
    {
        foreach (var subscription in _subscriptions) subscription.Dispose(); _subscriptions.Clear();
        if (FrontedBindingPathParser.ContainsDynamicIndexer(path)
            && FrontedBindingPathParser.TryParse(path, out var dynamicPath, out _))
        {
            WebRendererBindingPathResolver.VisitDynamicPath(root, dynamicPath, Subscribe);
            return;
        }
        object? current = root;
        foreach (var part in WebRendererBindingPathResolver.Parts(path))
        {
            Subscribe(current); var next = WebRendererBindingPathResolver.ReadPart(current, part, out _); current = next;
            if (current is null) break;
        }
    }
    private void Subscribe(object? value)
    {
        if (value is INotifyPropertyChanged propertyChanged) { PropertyChangedEventHandler handler = (_, _) => { Rebuild(); changed(); }; propertyChanged.PropertyChanged += handler; _subscriptions.Add(new ActionSubscription(() => propertyChanged.PropertyChanged -= handler)); }
        if (value is INotifyCollectionChanged collectionChanged) { NotifyCollectionChangedEventHandler handler = (_, _) => { Rebuild(); changed(); }; collectionChanged.CollectionChanged += handler; _subscriptions.Add(new ActionSubscription(() => collectionChanged.CollectionChanged -= handler)); }
    }
    public void Dispose() { IsRunning = false; foreach (var subscription in _subscriptions) subscription.Dispose(); _subscriptions.Clear(); }
}
internal sealed class ActionSubscription(Action dispose) : IDisposable { public void Dispose() => dispose(); }

/// <summary>安全、无调用的绑定路径求值器。</summary>
public static class WebRendererBindingPathResolver
{
    /// <summary>解析由设计器生成的属性/索引路径。</summary>
    public static (object? Value, string? Diagnostic, string? SourceType) Resolve(ISharedDataService root, string path)
    {
        if (FrontedBindingPathParser.ContainsDynamicIndexer(path))
        {
            if (!FrontedBindingPathParser.TryParse(path, out var dynamicPath, out _))
            {
                return (null, "InvalidBindingPath", null);
            }

            var value = ReadDynamicPath(root, root, dynamicPath, visit: null, out var diagnostic);
            return diagnostic is null
                ? (value, null, value?.GetType().FullName)
                : (null, diagnostic, null);
        }

        if (!IsValid(path)) return (null, "InvalidBindingPath", null);
        object? current = root;
        foreach (var part in Parts(path)) { current = ReadPart(current, part, out var diagnostic); if (diagnostic is not null) return (null, diagnostic, current?.GetType().FullName); }
        return (current, null, current?.GetType().FullName);
    }
    internal static IEnumerable<string> Parts(string path) => path.Split('.', StringSplitOptions.RemoveEmptyEntries);
    internal static object? ReadPart(object? current, string part, out string? diagnostic)
    {
        diagnostic = null; var name = part.Split('[', 2)[0]; var property = current?.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        if (property?.GetMethod is null || property.GetIndexParameters().Length != 0) { diagnostic = "UnknownBindingMember"; return null; }
        try { current = property.GetValue(current); } catch { diagnostic = "BindingReadFailed"; return null; }
        var remaining = part[name.Length..];
        while (remaining.StartsWith('[')) { var end = remaining.IndexOf(']'); if (end <= 1) { diagnostic = "InvalidBindingIndex"; return null; } var key = remaining[1..end].Trim('\'', '"'); if (current is IList list && int.TryParse(key, out var index) && index >= 0 && index < list.Count) current = list[index]; else if (current is IDictionary dictionary && dictionary.Contains(key)) current = dictionary[key]; else { diagnostic = "BindingIndexUnavailable"; return null; } remaining = remaining[(end + 1)..]; }
        return current;
    }

    internal static void VisitDynamicPath(
        ISharedDataService root,
        FrontedBindingPath path,
        Action<object?> visit) =>
        _ = ReadDynamicPath(root, root, path, visit, out _);

    private static object? ReadDynamicPath(
        object root,
        object? current,
        FrontedBindingPath path,
        Action<object?>? visit,
        out string? diagnostic)
    {
        diagnostic = null;
        foreach (var segment in path.Segments)
        {
            if (current is null)
            {
                diagnostic = "BindingReadUnavailable";
                return null;
            }

            visit?.Invoke(current);
            switch (segment)
            {
                case FrontedPropertyPathSegment propertySegment:
                    if (!FrontedBindingPathValueAccessor.TryReadProperty(current, propertySegment.Name, out current))
                    {
                        diagnostic = "UnknownBindingMember";
                        return null;
                    }
                    break;

                case FrontedLiteralIndexerPathSegment literalSegment:
                    if (!FrontedBindingPathValueAccessor.TryReadIndexer(current, literalSegment.Value, out current))
                    {
                        diagnostic = "BindingIndexUnavailable";
                        return null;
                    }
                    break;

                case FrontedDynamicIndexerPathSegment dynamicSegment:
                    var indexValue = ReadDynamicPath(root, root, dynamicSegment.Path, visit, out var indexDiagnostic);
                    if (indexDiagnostic is not null || !FrontedBindingPathValueAccessor.TryReadIndexer(current, indexValue, out current))
                    {
                        diagnostic = indexDiagnostic ?? "BindingIndexUnavailable";
                        return null;
                    }
                    break;
            }
        }
        return current;
    }

    private static bool IsValid(string path) => !string.IsNullOrWhiteSpace(path) && !path.Contains('(') && !path.Contains("..", StringComparison.Ordinal);
}
