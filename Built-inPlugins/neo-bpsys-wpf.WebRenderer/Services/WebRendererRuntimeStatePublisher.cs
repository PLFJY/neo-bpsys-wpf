using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using System.Windows;

namespace neo_bpsys_wpf.WebRenderer.Services;

/// <summary>将当前布局实际引用的共享数据投影为只读 Web Runtime 状态。</summary>
public sealed class WebRendererRuntimeStatePublisher : IDisposable
{
    private readonly ISharedDataService _sharedData;
    private readonly IFrontedEventBus _eventBus;
    private readonly WebRuntimeAssetRegistry _assets = new();
    private readonly WebRuntimeValueFactory _valueFactory;
    private readonly object _gate = new();
    private readonly Dictionary<string, BindingPathObserver> _observers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WebRuntimeValue> _values = new(StringComparer.Ordinal);
    private long _generation;
    private long _sequence;
    private int _clientCount;

    /// <summary>发生可发送的完整快照或增量更新时触发。</summary>
    public event EventHandler<WebRendererRuntimeUpdate>? Updated;
    /// <summary>发布可安全发送给 Web 页面的语义行为事件。</summary>
    public event EventHandler<WebRendererBehaviorEvent>? BehaviorEventPublished;
    /// <summary>获取当前由 sidecar 报告的已连接客户端数量。</summary>
    public int ClientCount => Volatile.Read(ref _clientCount);

    /// <summary>创建运行时发布器。</summary>
    public WebRendererRuntimeStatePublisher(ISharedDataService sharedData, IFrontedEventBus eventBus)
    {
        _sharedData = sharedData; _eventBus = eventBus; _valueFactory = new(_assets); _assets.AssetReady += OnAssetReady;
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
            _generation = snapshot.Generation;
            var paths = snapshot.Windows.Where(window => window.Layout is not null).SelectMany(window => EnumeratePaths(window.Layout!)).Where(path => !string.IsNullOrWhiteSpace(path)).ToHashSet(StringComparer.Ordinal);
            foreach (var path in paths) _observers.Add(path, new BindingPathObserver(_sharedData, path, OnPathChanged));
            if (_clientCount > 0) StartLocked(true);
        }
    }

    /// <summary>在 bootstrap 获得 sidecar 确认后发布当前完整运行时快照。</summary>
    public void PublishConfirmedSnapshot() { lock (_gate) { StartLocked(true); } }
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
    }

    private void StopLocked()
    {
        _eventBus.EventPublished -= OnEventPublished;
        foreach (var observer in _observers.Values) observer.Dispose();
        _values.Clear(); _assets.ReplaceReferences([]);
    }

    private void OnPathChanged() { lock (_gate) if (_clientCount > 0) RecalculateLocked(false); }
    private void OnAssetReady(object? sender, EventArgs args)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) OnPathChanged();
        else _ = dispatcher.BeginInvoke(OnPathChanged);
    }
    private void OnEventPublished(object? sender, FrontedBehaviorEvent args)
    {
        lock (_gate) if (_clientCount > 0) { BehaviorEventPublished?.Invoke(this, WebRendererBehaviorEvent.From(args)); RecalculateLocked(false); }
    }

    private void RecalculateLocked(bool snapshot)
    {
        var changed = new Dictionary<string, WebRuntimeValue>(StringComparer.Ordinal);
        var diagnostics = new List<WebRuntimeDiagnostic>();
        foreach (var pair in _observers)
        {
            var result = pair.Value.Resolve();
            WebRuntimeDiagnostic? conversionDiagnostic = null;
            var value = result.Diagnostic is null ? _valueFactory.Create(result.Value, pair.Key, out conversionDiagnostic) : new WebRuntimeValue("null", null, result.SourceType, result.Diagnostic);
            if (result.Diagnostic is not null) diagnostics.Add(new(pair.Key, result.Diagnostic, result.SourceType));
            else if (conversionDiagnostic is not null) diagnostics.Add(conversionDiagnostic);
            if (!_values.TryGetValue(pair.Key, out var previous) || previous != value) changed[pair.Key] = value;
            _values[pair.Key] = value;
        }
        _assets.ReplaceReferences(_values.Values.Where(value => value.Asset is not null).Select(value => value.Asset!.Token));
        if (snapshot) Updated?.Invoke(this, new(true, _generation, ++_sequence, new Dictionary<string, WebRuntimeValue>(_values), diagnostics));
        else if (changed.Count > 0) Updated?.Invoke(this, new(false, _generation, ++_sequence, changed, diagnostics));
    }

    private static IEnumerable<string> EnumeratePaths(FrontedWindowConfig layout)
    {
        foreach (var control in layout.ControlLayout.Controls.Values)
        {
            foreach (var requiredPath in GetSpecialControlPaths(control.ControlType)) yield return requiredPath;
            if (control is MapV2DisplayControlConfig map && !string.IsNullOrWhiteSpace(map.MapKey))
            { var prefix = $"CurrentGame.MapV2Dictionary['{map.MapKey}']"; foreach (var suffix in new[] { "MapName", "IsBanned", "IsPicked", "IsCampVisible", "OperationTeam.Name", "OperationTeam.Camp" }) yield return prefix + "." + suffix; }
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
    private static IEnumerable<string> GetSpecialControlPaths(string type) => type switch { "GameProgressText" => ["CurrentGame.GameProgress", "IsBo3Mode"], "MapNameText" => ["CurrentGame.PickedMap"], "TalentTraitDisplay" => ["IsTraitVisible"], "MapV2Display" => ["IsMapV2CampVisible", "IsMapV2Breathing"], "GlobalScoreRow" => ["IsBo3Mode"], _ => [] };
    /// <inheritdoc />
    public void Dispose() { lock (_gate) { StopLocked(); _assets.AssetReady -= OnAssetReady; _assets.Dispose(); } }
}

/// <summary>运行时状态更新。</summary>
public sealed record WebRendererRuntimeUpdate(bool IsSnapshot, long Generation, long Sequence, IReadOnlyDictionary<string, WebRuntimeValue> Values, IReadOnlyList<WebRuntimeDiagnostic> Diagnostics)
{
    /// <summary>runtime 值 schema 版本。</summary>
    public int SchemaVersion { get; init; } = 1;
}

/// <summary>经标准化后可发送到浏览器的只读行为事件。</summary>
public sealed record WebRendererBehaviorEvent(string EventType, string? WindowId, string? WindowType, string? CanvasName, DateTimeOffset Timestamp, string? Source, IReadOnlyDictionary<string, WebRuntimeValue> Payload)
{
    /// <summary>从宿主事件创建安全投影。</summary>
    public static WebRendererBehaviorEvent From(FrontedBehaviorEvent value) => new(value.EventType, value.WindowId, value.WindowType, value.CanvasName, value.Timestamp, value.Source, value.Payload.ToDictionary(pair => pair.Key, pair => new WebRuntimeValue(pair.Value is null ? "null" : "string", pair.Value?.ToString(), pair.Value?.GetType().FullName), StringComparer.Ordinal));
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
    private static bool IsValid(string path) => !string.IsNullOrWhiteSpace(path) && !path.Contains('(') && !path.Contains("..", StringComparison.Ordinal);
}
