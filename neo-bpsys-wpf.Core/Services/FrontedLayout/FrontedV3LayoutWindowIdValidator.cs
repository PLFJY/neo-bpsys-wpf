using System.IO;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 验证 v3 前台窗口的局部窗口标识（<c>LocalId</c>）。
/// </summary>
/// <remarks>
/// 局部窗口标识是提供方内部的短窗口名（例如 <c>BpWindow</c> 或插件的 <c>Overlay</c>），
/// 不应包含路径分隔符、<c>plugin:</c> 前缀形式或任何文件系统非法字符。
/// </remarks>
public static class FrontedV3LayoutWindowIdValidator
{
    /// <summary>
    /// 返回给定的局部窗口标识是否合法。
    /// </summary>
    /// <param name="localWindowId">要验证的局部窗口标识。</param>
    /// <returns>当标识非空白且不含 <c>/</c>、<c>\</c>、<c>:</c>、<c>.</c> 以及
    /// <see cref="Path.GetInvalidFileNameChars"/> 中的任何字符时为 <see langword="true"/>。</returns>
    /// <remarks>
    /// 拒绝包含 <c>plugin:</c> 前缀或 <c>/</c> 的完整 <c>plugin:package/window</c> 形式，
    /// 因为局部标识只应是单个窗口名段。纯空白字符串（如 <c>"   "</c>）同样被拒绝。
    /// </remarks>
    public static bool IsValidLocalWindowId(string localWindowId)
    {
        if (string.IsNullOrWhiteSpace(localWindowId))
        {
            return false;
        }

        if (localWindowId.Contains('/')
            || localWindowId.Contains('\\')
            || localWindowId.Contains(':')
            || localWindowId.Contains('.'))
        {
            return false;
        }

        if (localWindowId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 验证给定的局部窗口标识，无效时抛出 <see cref="ArgumentException"/>。
    /// </summary>
    /// <param name="localWindowId">要验证的局部窗口标识。</param>
    /// <exception cref="ArgumentException">当 <paramref name="localWindowId"/> 为 null/空白，
    /// 或包含 <c>/</c>、<c>\</c>、<c>:</c>、<c>.</c>、<c>..</c>、
    /// <see cref="Path.GetInvalidFileNameChars"/> 中的字符，或为 <c>plugin:package/window</c> 形式时抛出。
    /// 异常消息包含被拒绝的值与具体原因。</exception>
    public static void EnsureValidLocalWindowId(string localWindowId)
    {
        if (string.IsNullOrWhiteSpace(localWindowId))
        {
            throw new ArgumentException(
                $"Local window id must be a non-empty, non-whitespace string, but was '{localWindowId}'.",
                nameof(localWindowId));
        }

        if (localWindowId.Contains('/') || localWindowId.Contains('\\'))
        {
            throw new ArgumentException(
                $"Local window id '{localWindowId}' must not contain path separators ('/' or '\\'), " +
                "and the 'plugin:package/window' form is not a valid local id.",
                nameof(localWindowId));
        }

        if (localWindowId.Contains(':'))
        {
            throw new ArgumentException(
                $"Local window id '{localWindowId}' must not contain ':' " +
                "(the 'plugin:' prefix form is not a valid local id).",
                nameof(localWindowId));
        }

        if (localWindowId.Contains('.'))
        {
            throw new ArgumentException(
                $"Local window id '{localWindowId}' must not contain '.' or '..'.",
                nameof(localWindowId));
        }

        var invalidIndex = localWindowId.IndexOfAny(Path.GetInvalidFileNameChars());
        if (invalidIndex >= 0)
        {
            throw new ArgumentException(
                $"Local window id '{localWindowId}' contains invalid file name character " +
                $"'{localWindowId[invalidIndex]}'.",
                nameof(localWindowId));
        }
    }
}
