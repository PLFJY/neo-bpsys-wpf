using System.IO;

namespace neo_bpsys_wpf.Core.Services.Archives;

/// <summary>
/// 定位随应用发布的官方 x64 7-Zip 工具。
/// </summary>
public sealed class SevenZipToolLocator
{
    /// <summary>
    /// 解析 7z.exe 的完整路径。位于 <see cref="AppContext.BaseDirectory"/>/Tools/7Zip/7z.exe。
    /// </summary>
    /// <returns>7z.exe 完整路径。</returns>
    /// <exception cref="FileNotFoundException">当 7z.exe 不存在时抛出。</exception>
    public string GetExecutablePath()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Tools", "7Zip", "7z.exe");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "Official 7-Zip x64 binary is missing. The application requires 7z.exe at: " + path,
                path);
        }

        return path;
    }

    /// <summary>
    /// 解析 7z.exe 所在目录。
    /// </summary>
    /// <returns>7z.exe 所在目录。</returns>
    /// <exception cref="FileNotFoundException">当 7z.exe 不存在时抛出。</exception>
    public string GetToolDirectory()
    {
        return Path.GetDirectoryName(GetExecutablePath())!;
    }
}
