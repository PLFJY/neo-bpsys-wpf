using System.Diagnostics;
using System.IO;

namespace neo_bpsys_wpf.WebRenderer.Services;

/// <summary>
/// 检测可供 framework-dependent sidecar 使用的 x64 ASP.NET Core Runtime。
/// </summary>
public sealed class WebRendererRuntimeDetector
{
    /// <summary>
    /// 查找包含 ASP.NET Core 10 shared framework 的 x64 dotnet 主机。
    /// </summary>
    /// <returns>检测结果。</returns>
    public async Task<WebRendererRuntimeDetectionResult> DetectAsync()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe"),
            Environment.GetEnvironmentVariable("DOTNET_ROOT_X64") is { Length: > 0 } root ? Path.Combine(root, "dotnet.exe") : null
        }.Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path)).Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo(candidate!)
                {
                    Arguments = "--list-runtimes",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                });
                if (process is null)
                    continue;

                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                if (process.ExitCode == 0 && output.Split(Environment.NewLine).Any(line => line.StartsWith("Microsoft.AspNetCore.App 10.", StringComparison.Ordinal)))
                    return new(candidate!, true, null);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            {
                // Try the next x64 host. Detection is intentionally fail-safe.
            }
        }

        return new(null, false, "未检测到 ASP.NET Core Runtime 10 (x64)。");
    }
}

/// <summary>
/// ASP.NET Core Runtime 检测结果。
/// </summary>
/// <param name="DotnetPath">可用的 x64 dotnet.exe 路径。</param>
/// <param name="IsAvailable">是否已找到需要的 runtime。</param>
/// <param name="ErrorMessage">检测失败说明。</param>
public sealed record WebRendererRuntimeDetectionResult(string? DotnetPath, bool IsAvailable, string? ErrorMessage);
