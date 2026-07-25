using System.IO;
using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout.V3;

/// <summary>
/// 验证 v3 前台控件的局部控件标识（<c>ControlId</c>）。
/// </summary>
/// <remarks>
/// 局部控件标识是提供方内部的短控件名（例如 <c>TeamCard</c> 或内置的 <c>Text</c>），
/// 不应包含路径分隔符、<c>plugin:</c> 前缀形式或任何文件系统非法字符。
/// 不允许直接传入完整的 canonical ID（<c>plugin:package/control</c> 形式）。
/// </remarks>
public static class FrontedV3ControlIdValidator
{
    /// <summary>
    /// 返回给定的局部控件标识是否合法。
    /// </summary>
    /// <param name="controlId">要验证的局部控件标识。</param>
    /// <returns>当标识非空白且不含 <c>/</c>、<c>\</c>、<c>:</c> 以及
    /// <see cref="Path.GetInvalidFileNameChars"/> 中的任何字符时为 <see langword="true"/>。</returns>
    public static bool IsValidControlId(string? controlId)
    {
        if (string.IsNullOrWhiteSpace(controlId))
        {
            return false;
        }

        if (controlId.Contains('/')
            || controlId.Contains('\\')
            || controlId.Contains(':'))
        {
            return false;
        }

        if (controlId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 验证给定的局部控件标识，无效时抛出 <see cref="FrontedLayoutConfigException"/>。
    /// </summary>
    /// <param name="controlId">要验证的局部控件标识。</param>
    /// <exception cref="FrontedLayoutConfigException">当 <paramref name="controlId"/> 为 null/空白，
    /// 或包含 <c>/</c>、<c>\</c>、<c>:</c>、<see cref="Path.GetInvalidFileNameChars"/> 中的字符，
    /// 或为 <c>plugin:package/control</c> 形式时抛出。</exception>
    public static void EnsureValidControlId(string? controlId)
    {
        if (string.IsNullOrWhiteSpace(controlId))
        {
            throw new FrontedLayoutConfigException(
                $"Control id must be a non-empty, non-whitespace string, but was '{controlId}'.");
        }

        if (controlId!.Contains('/') || controlId.Contains('\\'))
        {
            throw new FrontedLayoutConfigException(
                $"Control id '{controlId}' must not contain path separators ('/' or '\\'), " +
                "and the 'plugin:package/control' form is not a valid local id.");
        }

        if (controlId.Contains(':'))
        {
            throw new FrontedLayoutConfigException(
                $"Control id '{controlId}' must not contain ':' " +
                "(the 'plugin:' prefix form is not a valid local id).");
        }

        var invalidIndex = controlId.IndexOfAny(Path.GetInvalidFileNameChars());
        if (invalidIndex >= 0)
        {
            throw new FrontedLayoutConfigException(
                $"Control id '{controlId}' contains invalid file name character " +
                $"'{controlId[invalidIndex]}'.");
        }
    }
}
