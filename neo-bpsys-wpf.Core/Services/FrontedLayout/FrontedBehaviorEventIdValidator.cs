using System.IO;
using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 验证插件前台行为事件的局部事件标识。
/// </summary>
public static class FrontedBehaviorEventIdValidator
{
    /// <summary>
    /// 判断局部事件标识是否合法。
    /// </summary>
    /// <param name="eventId">待验证的局部事件标识。</param>
    /// <returns>合法时为 <see langword="true"/>。</returns>
    public static bool IsValidLocalEventId(string? eventId)
    {
        return !string.IsNullOrWhiteSpace(eventId)
               && !eventId.StartsWith("plugin:", StringComparison.OrdinalIgnoreCase)
               && !eventId.Contains('/')
               && !eventId.Contains('\\')
               && !eventId.Contains(':')
               && !eventId.Contains("..", StringComparison.Ordinal)
               && eventId.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }

    /// <summary>
    /// 验证局部事件标识，无效时抛出配置异常。
    /// </summary>
    /// <param name="eventId">待验证的局部事件标识。</param>
    /// <exception cref="FrontedLayoutConfigException">标识为空、使用 canonical 前缀、包含路径分隔符、冒号、<c>..</c> 或文件名非法字符时抛出。</exception>
    public static void EnsureValidLocalEventId(string? eventId)
    {
        if (!IsValidLocalEventId(eventId))
        {
            throw new FrontedLayoutConfigException(
                $"Behavior event local id '{eventId}' is invalid. Use a non-empty local id without 'plugin:', ':', '/', '\\', '..', or file-name-invalid characters.");
        }
    }

    /// <summary>
    /// 从插件包 ID 和局部事件标识生成规范事件类型。
    /// </summary>
    /// <param name="packageId">插件包 ID。</param>
    /// <param name="localEventId">局部事件标识。</param>
    /// <returns><c>plugin:{PackageId}/{LocalEventId}</c> 形式的事件类型。</returns>
    /// <exception cref="FrontedLayoutConfigException">任一参数不合法时抛出。</exception>
    public static string BuildCanonicalEventType(string packageId, string localEventId)
    {
        if (string.IsNullOrWhiteSpace(packageId)
            || packageId.Contains('/')
            || packageId.Contains('\\')
            || packageId.Contains(':')
            || packageId.Contains("..", StringComparison.Ordinal))
        {
            throw new FrontedLayoutConfigException($"Plugin package id '{packageId}' cannot be used in a behavior event namespace.");
        }

        EnsureValidLocalEventId(localEventId);
        return $"plugin:{packageId}/{localEventId}";
    }

    /// <summary>
    /// 尝试解析插件规范事件类型。
    /// </summary>
    /// <param name="eventType">规范事件类型。</param>
    /// <param name="packageId">成功时返回包 ID。</param>
    /// <param name="localEventId">成功时返回局部事件标识。</param>
    /// <returns>格式和值均合法时为 <see langword="true"/>。</returns>
    public static bool TryParseCanonicalEventType(string? eventType, out string packageId, out string localEventId)
    {
        packageId = string.Empty;
        localEventId = string.Empty;
        if (string.IsNullOrWhiteSpace(eventType)
            || !eventType.StartsWith("plugin:", StringComparison.Ordinal))
        {
            return false;
        }

        var separator = eventType.IndexOf('/', "plugin:".Length);
        if (separator <= "plugin:".Length || separator == eventType.Length - 1
            || eventType.IndexOf('/', separator + 1) >= 0)
        {
            return false;
        }

        packageId = eventType["plugin:".Length..separator];
        localEventId = eventType[(separator + 1)..];
        try
        {
            return string.Equals(BuildCanonicalEventType(packageId, localEventId), eventType, StringComparison.Ordinal);
        }
        catch (FrontedLayoutConfigException)
        {
            packageId = string.Empty;
            localEventId = string.Empty;
            return false;
        }
    }
}
