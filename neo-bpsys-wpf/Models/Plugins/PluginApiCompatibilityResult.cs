namespace neo_bpsys_wpf.Models.Plugins;

/// <summary>
/// 表示插件 API 版本兼容性检查的结果。
/// </summary>
public class PluginApiCompatibilityResult
{
    /// <summary>
    /// 插件 API 版本是否与宿主兼容。
    /// </summary>
    public bool IsCompatible { get; init; }

    /// <summary>
    /// 插件声明的 API 版本格式是否有效。
    /// </summary>
    public bool IsFormatValid { get; init; }

    /// <summary>
    /// 插件要求的 API 版本是否高于宿主支持的版本。
    /// </summary>
    public bool IsTooHigh { get; init; }

    /// <summary>
    /// 插件要求的 API 版本是否低于宿主支持的最低版本。
    /// </summary>
    public bool IsTooLow { get; init; }

    /// <summary>
    /// 兼容性检查结果的描述信息。
    /// </summary>
    public string Message { get; init; } = string.Empty;
}
