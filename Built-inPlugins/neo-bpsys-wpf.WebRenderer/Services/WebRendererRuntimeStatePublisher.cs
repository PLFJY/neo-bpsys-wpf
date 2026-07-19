using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;

namespace neo_bpsys_wpf.WebRenderer.Services;

/// <summary>将当前布局实际引用的共享数据投影为只读 Web Runtime 状态。</summary>
public sealed class WebRendererRuntimeStatePublisher : IDisposable
{
    private readonly ISharedDataService _sharedData;
    private readonly IFrontedEventBus _eventBus;
    private readonly object _gate = new();
    private readonly List<IDisposable> _subscriptions = [];
    private readonly Dictionary<string, JsonElement> _values = new(StringComparer.Ordinal);
    private HashSet<string> _paths = new(StringComparer.Ordinal);
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
        _sharedData = sharedData;
        _eventBus = eventBus;
    }

    /// <summary>使用新 bootstrap 重新收集所有可消费的绑定路径。</summary>
    public void ReplaceLayout(WebRendererBootstrapSnapshot snapshot)
    {
        lock (_gate)
        {
            _generation = snapshot.Generation;
            _paths = snapshot.Windows.Where(window => window.Layout is not null)
                .SelectMany(window => EnumeratePaths(window.Layout!))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToHashSet(StringComparer.Ordinal);
            _values.Clear();
            if (_clientCount > 0) StartLocked(sendSnapshot: true);
        }
    }

    /// <summary>更新 sidecar 当前的连接页面数量。</summary>
    public void SetClientCount(int clientCount)
    {
        lock (_gate)
        {
            _clientCount = Math.Max(0, clientCount);
            if (_clientCount == 0) StopLocked();
            else StartLocked(sendSnapshot: true);
        }
    }

    private void StartLocked(bool sendSnapshot)
    {
        if (_subscriptions.Count == 0)
        {
            Observe(_sharedData, new HashSet<object>(ReferenceEqualityComparer.Instance), 0);
            _eventBus.EventPublished += OnEventPublished;
            _subscriptions.Add(new ActionSubscription(() => _eventBus.EventPublished -= OnEventPublished));
        }
        RecalculateLocked(sendSnapshot);
    }

    private void StopLocked()
    {
        foreach (var subscription in _subscriptions) subscription.Dispose();
        _subscriptions.Clear();
        _values.Clear();
    }

    private void Observe(object? value, HashSet<object> visited, int depth)
    {
        if (value is null || depth > 5 || !visited.Add(value)) return;
        if (value is INotifyPropertyChanged changed)
        {
            PropertyChangedEventHandler handler = (_, _) => OnStateChanged();
            changed.PropertyChanged += handler;
            _subscriptions.Add(new ActionSubscription(() => changed.PropertyChanged -= handler));
        }
        if (value is INotifyCollectionChanged collectionChanged)
        {
            NotifyCollectionChangedEventHandler handler = (_, _) => OnStateChanged();
            collectionChanged.CollectionChanged += handler;
            _subscriptions.Add(new ActionSubscription(() => collectionChanged.CollectionChanged -= handler));
        }
        if (value is IEnumerable enumerable and not string)
            foreach (var item in enumerable) Observe(item, visited, depth + 1);
        var type = value.GetType();
        if (type.IsPrimitive || value is string || type.IsEnum) return;
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(property => property.GetIndexParameters().Length == 0 && property.GetMethod is not null))
        {
            try { Observe(property.GetValue(value), visited, depth + 1); } catch { /* projection is best effort */ }
        }
    }

    private void OnEventPublished(object? sender, FrontedBehaviorEvent args)
    {
        lock (_gate)
        {
            if (_clientCount > 0)
            {
                BehaviorEventPublished?.Invoke(this, WebRendererBehaviorEvent.From(args));
                RecalculateLocked(sendSnapshot: false);
            }
        }
    }
    private void OnStateChanged() { lock (_gate) { if (_clientCount > 0) RecalculateLocked(sendSnapshot: false); } }

    private void RecalculateLocked(bool sendSnapshot)
    {
        var changed = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var path in _paths)
        {
            var result = WebRendererBindingPathResolver.Resolve(_sharedData, path);
            var value = WebRendererBindingValue.Create(result.Value, out var diagnostic);
            if (diagnostic is not null) value = JsonSerializer.SerializeToElement<object?>(null);
            if (!_values.TryGetValue(path, out var previous) || previous.GetRawText() != value.GetRawText()) changed[path] = value;
            _values[path] = value;
        }
        if (sendSnapshot)
            Updated?.Invoke(this, new WebRendererRuntimeUpdate(true, _generation, ++_sequence, new Dictionary<string, JsonElement>(_values), []));
        else if (changed.Count > 0)
            Updated?.Invoke(this, new WebRendererRuntimeUpdate(false, _generation, ++_sequence, changed, []));
    }

    private static IEnumerable<string> EnumeratePaths(FrontedWindowConfig layout)
    {
        foreach (var control in layout.ControlLayout.Controls.Values)
        {
            foreach (var requiredPath in GetSpecialControlPaths(control.ControlType)) yield return requiredPath;
            if (control is MapV2DisplayControlConfig map && !string.IsNullOrWhiteSpace(map.MapKey))
            {
                var prefix = $"CurrentGame.MapV2Dictionary['{map.MapKey}']";
                yield return $"{prefix}.MapName"; yield return $"{prefix}.IsBanned"; yield return $"{prefix}.IsPicked";
                yield return $"{prefix}.IsCampVisible"; yield return $"{prefix}.OperationTeam.Name"; yield return $"{prefix}.OperationTeam.Camp";
            }
            if (control is TalentTraitDisplayControlConfig talent)
            {
                var player = talent.DisplayKind.ToString().StartsWith("Survivor", StringComparison.Ordinal)
                    ? $"CurrentGame.SurPlayerList[{talent.PlayerIndex ?? 0}]" : "CurrentGame.HunPlayer";
                foreach (var name in new[] { "BorrowedTime", "TideTurner", "FlywheelEffect", "KneeJerkReflex", "TrumpCard", "Detention", "ConfinedSpace", "Insolence" })
                    yield return $"{player}.Talent.{name}";
            }
            if (!string.IsNullOrWhiteSpace(control.BindingPath)) yield return control.BindingPath;
            foreach (var property in control.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.Name.EndsWith("BindingPath", StringComparison.Ordinal) && property.GetValue(control) is string path && !string.IsNullOrWhiteSpace(path)) yield return path;
                if (property.Name == "TextBinding" && property.GetValue(control) is { } binding)
                {
                    var sources = binding.GetType().GetProperty("Sources")?.GetValue(binding) as IEnumerable;
                    if (sources is null) continue;
                    foreach (var source in sources)
                        if (source?.GetType().GetProperty("Path")?.GetValue(source) is string sourcePath && !string.IsNullOrWhiteSpace(sourcePath)) yield return sourcePath;
                }
            }
        }
    }

    private static IEnumerable<string> GetSpecialControlPaths(string controlType) => controlType switch
    {
        "GameProgressText" => ["CurrentGame.GameProgress", "IsBo3Mode"],
        "MapNameText" => ["CurrentGame.PickedMap"],
        "TalentTraitDisplay" => ["IsTraitVisible"],
        "MapV2Display" => ["IsMapV2CampVisible", "IsMapV2Breathing"],
        "GlobalScoreRow" => ["IsBo3Mode"],
        _ => []
    };

    /// <inheritdoc />
    public void Dispose() { lock (_gate) StopLocked(); }
}

/// <summary>运行时状态更新。</summary>
public sealed record WebRendererRuntimeUpdate(bool IsSnapshot, long Generation, long Sequence, IReadOnlyDictionary<string, JsonElement> Values, IReadOnlyList<string> Diagnostics);

/// <summary>经标准化后可发送到浏览器的只读行为事件。</summary>
public sealed record WebRendererBehaviorEvent(string EventType, string? WindowId, string? WindowType, string? CanvasName,
    DateTimeOffset Timestamp, string? Source, IReadOnlyDictionary<string, JsonElement> Payload)
{
    /// <summary>从宿主事件创建安全投影。</summary>
    public static WebRendererBehaviorEvent From(FrontedBehaviorEvent value) => new(value.EventType, value.WindowId, value.WindowType,
        value.CanvasName, value.Timestamp, value.Source, value.Payload.ToDictionary(pair => pair.Key,
            pair => WebRendererBindingValue.Create(pair.Value, out _), StringComparer.Ordinal));
}

internal sealed class ActionSubscription(Action dispose) : IDisposable { public void Dispose() => dispose(); }

/// <summary>安全、无调用的绑定路径求值器。</summary>
public static class WebRendererBindingPathResolver
{
    /// <summary>解析由设计器生成的属性/索引路径。</summary>
    public static (object? Value, string? Diagnostic) Resolve(ISharedDataService root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains("(", StringComparison.Ordinal) || path.Contains("..", StringComparison.Ordinal)) return (null, "InvalidBindingPath");
        object? current = root;
        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = part.Split('[', 2)[0];
            var property = current?.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (property?.GetMethod is null || property.GetIndexParameters().Length != 0) return (null, "UnknownBindingMember");
            try { current = property.GetValue(current); } catch { return (null, "BindingReadFailed"); }
            var remaining = part[name.Length..];
            while (remaining.StartsWith('['))
            {
                var end = remaining.IndexOf(']'); if (end <= 1) return (null, "InvalidBindingIndex");
                var key = remaining[1..end].Trim('\'', '"');
                if (current is IList list && int.TryParse(key, out var index) && index >= 0 && index < list.Count) current = list[index];
                else if (current is IDictionary dictionary && dictionary.Contains(key)) current = dictionary[key];
                else return (null, "BindingIndexUnavailable");
                remaining = remaining[(end + 1)..];
            }
        }
        return (current, null);
    }
}

internal static class WebRendererBindingValue
{
    public static JsonElement Create(object? value, out string? diagnostic)
    {
        diagnostic = null;
        if (value is null || value is string || value is bool || value is int or long or float or double or decimal) return JsonSerializer.SerializeToElement(value);
        if (value.GetType().IsEnum) return JsonSerializer.SerializeToElement(value.ToString());
        diagnostic = "UnsupportedBindingValue";
        return JsonSerializer.SerializeToElement<object?>(null);
    }
}
