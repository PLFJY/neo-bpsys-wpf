using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.Archives;
using neo_bpsys_wpf.Core.Models.SmartBpModule;
using neo_bpsys_wpf.ProductTour;

namespace neo_bpsys_wpf.Services.SmartBpModule;

/// <summary>
/// SmartBP 模块管理器的NativeLibraries逻辑。
/// </summary>
public sealed partial class SmartBpModuleManager
{
    /// <summary>
    /// 在 SmartBP 模块 native 探测目录中查找非托管库。
    /// </summary>
    /// <param name="moduleRoot">SmartBP 模块根目录。</param>
    /// <param name="libraryName">库文件名，可带或不带 .dll 扩展名。</param>
    /// <returns>匹配的库路径；未找到时返回 <see langword="null"/>。</returns>
    internal static string? FindModuleUnmanagedLibraryPath(string moduleRoot, string libraryName)
    {
        if (string.IsNullOrWhiteSpace(moduleRoot) || string.IsNullOrWhiteSpace(libraryName))
            return null;

        var safeLibraryName = Path.GetFileName(libraryName);
        if (!string.Equals(safeLibraryName, libraryName, StringComparison.Ordinal))
            return null;

        var fileName = Path.HasExtension(safeLibraryName) ? safeLibraryName : $"{safeLibraryName}.dll";
        var candidates = GetModuleNativeSearchDirectories(moduleRoot)
            .Select(directory => Path.Combine(directory, fileName));

        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// 判断 native 库是否仅由 PaddleInference 用于探测可选计算后端。
    /// </summary>
    /// <param name="libraryName">P/Invoke 请求的库名。</param>
    /// <returns>该库缺失不代表当前已选 Paddle runtime 不可用时返回 <see langword="true"/>。</returns>
    private static bool IsOptionalPaddleBackendLibrary(string libraryName)
    {
        var fileName = Path.GetFileName(libraryName);
        return string.Equals(fileName, "openblas", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "openblas.dll", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 为 Windows DLL 解析注册 SmartBP native 目录，并预加载已知依赖锚点。
    /// </summary>
    /// <param name="moduleRoot">SmartBP 模块根目录。</param>
    /// <param name="logger">可选的 native 探测诊断日志记录器。</param>
    internal static void RegisterModuleNativeSearchDirectories(
        string moduleRoot,
        ILogger? logger = null)
    {
        lock (NativeSearchPathSync)
        {
            foreach (var directory in GetModuleNativeSearchDirectories(moduleRoot).Where(Directory.Exists))
            {
                if (!RegisteredNativeSearchDirectories.Add(directory)) continue;

                var cookie = AddDllDirectory(directory);
                if (cookie == IntPtr.Zero)
                {
                    logger?.LogDebug(
                        "Failed to add SmartBP module native search directory through AddDllDirectory. Directory={Directory}, Error={Error}",
                        directory,
                        Marshal.GetLastWin32Error());
                }
                else
                {
                    logger?.LogDebug("Added SmartBP module native search directory: {Directory}", directory);
                }

                // AddDllDirectory 帮助 P/Invoke；PATH 也能帮助仍使用旧版 Windows
                // 搜索语义加载自身依赖的 native 库。
                PrependProcessPath(directory);
            }

            PreloadModuleNativeLibraries(moduleRoot, logger);
        }
    }

    /// <summary>
    /// 预加载依赖解析顺序敏感的已知 native 库。
    /// </summary>
    /// <param name="moduleRoot">SmartBP 模块根目录。</param>
    /// <param name="logger">可选的预加载诊断日志记录器。</param>
    private static void PreloadModuleNativeLibraries(
        string moduleRoot,
        ILogger? logger)
    {
        foreach (var libraryPath in GetKnownModuleNativeLibrariesToPreload(moduleRoot))
        {
            if (PreloadedNativeLibraries.ContainsKey(libraryPath)) continue;

            try
            {
                var handle = NativeLibrary.Load(libraryPath);
                PreloadedNativeLibraries[libraryPath] = handle;
                logger?.LogDebug("Preloaded SmartBP module native library: {LibraryPath}", libraryPath);
            }
            catch (Exception ex)
            {
                logger?.LogDebug(
                    ex,
                    "Failed to preload SmartBP module native library. LibraryPath={LibraryPath}",
                    libraryPath);
            }
        }
    }

    /// <summary>
    /// 枚举在托管模块代码运行前应提前加载的 native 库。
    /// </summary>
    /// <param name="moduleRoot">SmartBP 模块根目录。</param>
    /// <returns>按依赖友好顺序排列的现有库路径。</returns>
    private static IEnumerable<string> GetKnownModuleNativeLibrariesToPreload(string moduleRoot)
    {
        var nativeDirectories = GetModuleNativeSearchDirectories(moduleRoot).Where(Directory.Exists).ToArray();
        var orderedNames = new[]
        {
            "onnxruntime_providers_shared.dll",
            "onnxruntime.dll"
        };

        foreach (var name in orderedNames)
        {
            foreach (var directory in nativeDirectories)
            {
                var path = Path.Combine(directory, name);
                if (File.Exists(path)) yield return path;
            }
        }
    }

    /// <summary>
    /// 构建 SmartBP 模块加载所需 native 探测目录的有序集合。
    /// </summary>
    /// <param name="moduleRoot">SmartBP 模块根目录。</param>
    /// <returns>native 探测目录，包含模块 RID 特定目录、模块自带 CPU Paddle runtime 和模块根目录回退。</returns>
    internal static IReadOnlyList<string> GetModuleNativeSearchDirectories(string moduleRoot)
    {
        if (string.IsNullOrWhiteSpace(moduleRoot)) return [];

        var directories = new List<string>
        {
            Path.Combine(moduleRoot, "runtimes", SmartBpModuleConstants.Rid, "native")
        };

        var runtimesRoot = Path.Combine(moduleRoot, "runtimes");
        if (Directory.Exists(runtimesRoot))
        {
            foreach (var runtimeDirectory in Directory.EnumerateDirectories(runtimesRoot))
            {
                var nativeDirectory = Path.Combine(runtimeDirectory, "native");
                if (!directories.Contains(nativeDirectory, StringComparer.OrdinalIgnoreCase))
                    directories.Add(nativeDirectory);
            }
        }

        var paddleCpuRoot = Path.Combine(moduleRoot, "Runtime", "Paddle", "cpu");
        if (Directory.Exists(paddleCpuRoot))
        {
            foreach (var versionDirectory in Directory.EnumerateDirectories(paddleCpuRoot))
            {
                var nativeDirectory = Path.Combine(versionDirectory, "native");
                if (!directories.Contains(nativeDirectory, StringComparer.OrdinalIgnoreCase))
                    directories.Add(nativeDirectory);
            }
        }

        directories.Add(moduleRoot);

        return directories;
    }

    /// <summary>
    /// 当 native 探测目录尚未存在于进程 PATH 中时，将其前置加入 PATH。
    /// </summary>
    /// <param name="directory">要添加的目录。</param>
    private static void PrependProcessPath(string directory)
    {
        var current = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var paths = current.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (paths.Any(path => string.Equals(path, directory, StringComparison.OrdinalIgnoreCase))) return;
        Environment.SetEnvironmentVariable("PATH", string.IsNullOrWhiteSpace(current)
            ? directory
            : $"{directory}{Path.PathSeparator}{current}");
    }

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr AddDllDirectory(string newDirectory);

}
