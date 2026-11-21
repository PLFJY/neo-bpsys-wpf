using System.Diagnostics.CodeAnalysis;

namespace neo_bpsys_wpf.WebRenderer.Services;

/// <summary>Web 控件适配器注册表；当前仅声明受控元数据，不加载第三方脚本。</summary>
public interface IWebControlRegistry
{
    /// <summary>按稳定的 <c>plugin:*</c> ControlType 查询适配器描述符。</summary>
    /// <param name="controlType">完整控件类型。</param>
    /// <param name="descriptor">已注册的描述符；未找到时为 <see langword="null"/>。</param>
    /// <returns>找到时为 <see langword="true"/>。</returns>
    bool TryGet(string controlType, [MaybeNullWhen(false)] out WebControlAdapterDescriptor descriptor);
}

/// <summary>
/// 未来第三方 Web 控件适配器的可序列化身份描述。
/// 构造时校验 <c>plugin:</c> 命名约定与受限标识符字符集，避免加载不可信适配器身份。
/// </summary>
public sealed record WebControlAdapterDescriptor
{
    /// <summary>初始化适配器描述符。</summary>
    /// <param name="controlType">完整控件类型，必须形如 <c>plugin:{PackageId}/{ControlId}</c>。</param>
    /// <param name="adapterId">适配器标识符，只允许字母、数字、<c>.</c>、<c>-</c>、<c>_</c>。</param>
    /// <exception cref="ArgumentException">控件类型或适配器标识符不符合约定。</exception>
    public WebControlAdapterDescriptor(string controlType, string adapterId)
    {
        ControlType = ValidateControlType(controlType);
        AdapterId = ValidateAdapterId(adapterId);
    }

    /// <summary>获取完整控件类型。</summary>
    public string ControlType { get; }

    /// <summary>获取适配器标识符。</summary>
    public string AdapterId { get; }

    private static string ValidateControlType(string controlType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(controlType);
        const string prefix = "plugin:";
        if (!controlType.StartsWith(prefix, StringComparison.Ordinal) || controlType.Length == prefix.Length)
            throw new ArgumentException("Web control type must use the 'plugin:' prefix.", nameof(controlType));

        var segments = controlType[prefix.Length..].Split('/');
        if (segments.Length != 2
            || segments.Any(segment => string.IsNullOrEmpty(segment) || segment is "." or ".."))
        {
            throw new ArgumentException("Web control type must contain exactly '{PackageId}/{ControlId}'.", nameof(controlType));
        }

        foreach (var character in controlType[prefix.Length..])
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('.' or '-' or '_' or '/'))
                throw new ArgumentException("Web control type contains invalid characters.", nameof(controlType));
        }

        return controlType;
    }

    private static string ValidateAdapterId(string adapterId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adapterId);
        if (adapterId.Length > 128)
            throw new ArgumentException("Adapter id is too long.", nameof(adapterId));
        foreach (var character in adapterId)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('.' or '-' or '_'))
                throw new ArgumentException("Adapter id contains invalid characters.", nameof(adapterId));
        }

        return adapterId;
    }
}

/// <summary>
/// 默认空注册表，使未适配控件稳定回退到诊断占位。
/// </summary>
/// <remarks>默认实现不加载任何适配器，所有查询都会返回 <see langword="false"/>，由前端渲染诊断占位。</remarks>
public sealed class WebControlRegistry : IWebControlRegistry
{
    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="controlType"/> 为空或空白。</exception>
    public bool TryGet(string controlType, [MaybeNullWhen(false)] out WebControlAdapterDescriptor descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(controlType);
        descriptor = null;
        return false;
    }
}
