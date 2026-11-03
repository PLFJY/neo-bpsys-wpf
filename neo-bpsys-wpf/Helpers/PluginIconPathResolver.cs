using System.IO;

namespace neo_bpsys_wpf.Helpers;

/// <summary>
/// 解析插件 <c>manifest.yml</c> 中声明的图标路径，确保图标只引用插件安装目录内的文件。
/// </summary>
internal static class PluginIconPathResolver
{
    private const string DefaultIconFileName = "icon.png";

    /// <summary>
    /// 解析插件图标的绝对路径。
    /// 拒绝绝对路径、路径分隔符穿越与目录外结果；声明无效时回退到插件目录下的 <c>icon.png</c>。
    /// </summary>
    /// <param name="pluginDirectory">插件安装目录。</param>
    /// <param name="declaredIcon">manifest 中声明的图标相对路径。</param>
    /// <returns>插件目录内图标的绝对路径；目录本身无效时返回空字符串。</returns>
    internal static string Resolve(string pluginDirectory, string? declaredIcon)
    {
        if (string.IsNullOrWhiteSpace(pluginDirectory))
        {
            return string.Empty;
        }

        var pluginRoot = Path.GetFullPath(pluginDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var icon = string.IsNullOrWhiteSpace(declaredIcon) ? DefaultIconFileName : declaredIcon;
        if (Path.IsPathRooted(icon))
        {
            icon = DefaultIconFileName;
        }

        var candidate = Path.GetFullPath(Path.Combine(pluginRoot, icon));
        if (candidate.StartsWith(pluginRoot, StringComparison.OrdinalIgnoreCase))
        {
            return candidate;
        }

        var fallback = Path.GetFullPath(Path.Combine(pluginRoot, DefaultIconFileName));
        return fallback.StartsWith(pluginRoot, StringComparison.OrdinalIgnoreCase) ? fallback : string.Empty;
    }
}
