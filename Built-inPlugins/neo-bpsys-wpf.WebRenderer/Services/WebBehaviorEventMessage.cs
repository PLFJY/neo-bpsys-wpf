using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.WebRenderer.Services;

/// <summary>
/// Web Renderer 专用的行为事件线协议。Payload 保留 JSON 原生语义，不使用
/// <see cref="WebRuntimeValue"/> 联合类型包装。
/// </summary>
public sealed record WebBehaviorEventMessage(
    int SchemaVersion,
    string EventType,
    string? WindowId,
    string? WindowType,
    string? CanvasName,
    DateTimeOffset Timestamp,
    string? Source,
    bool IsPreview,
    IReadOnlyDictionary<string, object?> Payload,
    IReadOnlyList<string> Diagnostics)
{
    /// <summary>当前行为事件负载协议版本。</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>从宿主行为事件创建安全的 JSON 语义投影。</summary>
    /// <param name="value">宿主事件。</param>
    /// <param name="diagnose">限流诊断回调。</param>
    /// <returns>Web Renderer 行为事件。</returns>
    public static WebBehaviorEventMessage From(FrontedBehaviorEvent value, Action<string>? diagnose = null)
    {
        var diagnostics = new List<string>();
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var pair in value.Payload)
        {
            payload[pair.Key] = WebBehaviorPayloadProjector.Project(
                pair.Value,
                value.EventType,
                pair.Key,
                diagnostics.Add,
                diagnose);
        }

        return new(
            CurrentSchemaVersion,
            value.EventType,
            value.WindowId,
            value.WindowType,
            value.CanvasName,
            value.Timestamp,
            value.Source,
            value.IsPreview,
            payload,
            diagnostics);
    }
}

/// <summary>将行为事件 Payload 限制为安全 JSON 原生值。</summary>
public static class WebBehaviorPayloadProjector
{
    /// <summary>单个 Payload 的最大嵌套深度。</summary>
    public const int MaxDepth = 8;

    /// <summary>数组或列表的最大元素数量。</summary>
    public const int MaxCollectionLength = 128;

    /// <summary>字典的最大项目数量。</summary>
    public const int MaxDictionaryEntries = 128;

    private static readonly ConcurrentDictionary<string, byte> ReportedDiagnostics = new(StringComparer.Ordinal);

    /// <summary>投影一个受支持的行为 Payload 值。</summary>
    /// <param name="value">原始 CLR 值。</param>
    /// <param name="eventType">事件类型，用于诊断。</param>
    /// <param name="path">Payload 字段路径。</param>
    /// <param name="diagnostics">当前事件诊断收集器。</param>
    /// <param name="diagnose">限流诊断回调。</param>
    /// <param name="depth">当前递归深度。</param>
    /// <returns>JSON 原生值；不支持的值为 null。</returns>
    public static object? Project(
        object? value,
        string eventType,
        string path,
        Action<string>? diagnostics = null,
        Action<string>? diagnose = null,
        int depth = 0)
    {
        if (value is null) return null;
        if (depth > MaxDepth)
        {
            return Unsupported("BehaviorPayloadDepthExceeded", eventType, path, diagnostics, diagnose);
        }

        switch (value)
        {
            case string text:
                return text;
            case char character:
                return character.ToString();
            case bool boolean:
                return boolean;
            case byte or sbyte or short or ushort or int or uint or long or ulong
                or nint or nuint or float or double or decimal:
                return value;
            case Enum enumeration:
                return enumeration.ToString();
            case DateTime dateTime:
                return dateTime.ToString("O", CultureInfo.InvariantCulture);
            case DateTimeOffset dateTimeOffset:
                return dateTimeOffset.ToString("O", CultureInfo.InvariantCulture);
            case IDictionary dictionary:
                return ProjectDictionary(dictionary, eventType, path, diagnostics, diagnose, depth);
            case IEnumerable readOnlyDictionary when IsReadOnlyDictionary(value.GetType()):
                return ProjectReadOnlyDictionary(readOnlyDictionary, eventType, path, diagnostics, diagnose, depth);
            case IEnumerable enumerable:
                return ProjectCollection(enumerable, eventType, path, diagnostics, diagnose, depth);
            default:
                return Unsupported("BehaviorPayloadUnsupportedType", eventType, path, diagnostics, diagnose, value.GetType());
        }
    }

    private static object?[]? ProjectCollection(
        IEnumerable values,
        string eventType,
        string path,
        Action<string>? diagnostics,
        Action<string>? diagnose,
        int depth)
    {
        var result = new List<object?>();
        foreach (var item in values)
        {
            if (result.Count == MaxCollectionLength)
            {
                Unsupported("BehaviorPayloadCollectionLimitExceeded", eventType, path, diagnostics, diagnose);
                break;
            }

            result.Add(Project(item, eventType, $"{path}[{result.Count}]", diagnostics, diagnose, depth + 1));
        }

        return result.ToArray();
    }

    private static Dictionary<string, object?>? ProjectDictionary(
        IDictionary values,
        string eventType,
        string path,
        Action<string>? diagnostics,
        Action<string>? diagnose,
        int depth)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in values)
        {
            if (result.Count == MaxDictionaryEntries)
            {
                Unsupported("BehaviorPayloadDictionaryLimitExceeded", eventType, path, diagnostics, diagnose);
                break;
            }

            if (entry.Key is not string key)
            {
                Unsupported("BehaviorPayloadDictionaryKeyUnsupported", eventType, path, diagnostics, diagnose, entry.Key?.GetType());
                continue;
            }

            result[key] = Project(entry.Value, eventType, $"{path}.{key}", diagnostics, diagnose, depth + 1);
        }

        return result;
    }

    private static Dictionary<string, object?> ProjectReadOnlyDictionary(
        IEnumerable values,
        string eventType,
        string path,
        Action<string>? diagnostics,
        Action<string>? diagnose,
        int depth)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var item in values)
        {
            if (result.Count == MaxDictionaryEntries)
            {
                Unsupported("BehaviorPayloadDictionaryLimitExceeded", eventType, path, diagnostics, diagnose);
                break;
            }

            var itemType = item?.GetType();
            var key = itemType?.GetProperty("Key")?.GetValue(item);
            var itemValue = itemType?.GetProperty("Value")?.GetValue(item);
            if (key is not string stringKey)
            {
                Unsupported("BehaviorPayloadDictionaryKeyUnsupported", eventType, path, diagnostics, diagnose, key?.GetType());
                continue;
            }

            result[stringKey] = Project(itemValue, eventType, $"{path}.{stringKey}", diagnostics, diagnose, depth + 1);
        }

        return result;
    }

    private static bool IsReadOnlyDictionary(Type type) => type.GetInterfaces().Any(item =>
        item.IsGenericType && item.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>));

    private static object? Unsupported(
        string code,
        string eventType,
        string path,
        Action<string>? diagnostics,
        Action<string>? diagnose,
        Type? sourceType = null)
    {
        var diagnostic = $"{code}:{eventType}:{path}:{sourceType?.FullName ?? "unknown"}";
        diagnostics?.Invoke(diagnostic);
        if (ReportedDiagnostics.TryAdd(diagnostic, 0)) diagnose?.Invoke(diagnostic);
        return null;
    }
}
