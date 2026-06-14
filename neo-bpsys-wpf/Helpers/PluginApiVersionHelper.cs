using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Models.Plugins;

namespace neo_bpsys_wpf.Helpers;

/// <summary>
/// 提供插件 API 版本兼容性评估的辅助方法。
/// </summary>
public static class PluginApiVersionHelper
{
    /// <summary>
    /// 评估指定的插件 API 版本是否与当前宿主兼容。
    /// </summary>
    /// <param name="apiVersion">插件声明的 API 版本字符串。</param>
    /// <returns>兼容性检查结果。</returns>
    public static PluginApiCompatibilityResult Evaluate(string apiVersion)
    {
        var minApiVersion = IAppHost.CoreVersion;
        var hostApiVersion = typeof(PluginBase).Assembly.GetName().Version ?? minApiVersion;

        if (!Version.TryParse(apiVersion, out var parsedVersion))
        {
            return new PluginApiCompatibilityResult
            {
                IsCompatible = false,
                IsFormatValid = false,
                Message = $"Invalid API version format: {apiVersion}. Expected a valid System.Version value (e.g. 2.0.0.0)."
            };
        }

        if (parsedVersion.Major > hostApiVersion.Major)
        {
            return new PluginApiCompatibilityResult
            {
                IsCompatible = false,
                IsFormatValid = true,
                IsTooHigh = true,
                Message = $"Plugin API version is too high: {parsedVersion}. Current host supports {hostApiVersion.Major}.x API."
            };
        }

        if (parsedVersion < minApiVersion)
        {
            return new PluginApiCompatibilityResult
            {
                IsCompatible = false,
                IsFormatValid = true,
                IsTooLow = true,
                Message = $"Plugin API version is incompatible. Please migrate to v3 plugin APIs. Plugin API version is too low: {parsedVersion}. The minimum required API version is {minApiVersion}."
            };
        }

        return new PluginApiCompatibilityResult
        {
            IsCompatible = true,
            IsFormatValid = true,
            Message = string.Empty
        };
    }
}
