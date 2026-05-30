using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 默认 v3 前台布局配置读写服务。
/// </summary>
public class FrontedLayoutService : IFrontedLayoutService
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <inheritdoc />
    public async Task<FrontedCanvasConfig?> LoadCanvasConfigAsync(
        string windowTypeName,
        string canvasName,
        CancellationToken cancellationToken = default)
    {
        var userPath = GetUserLayoutPath(windowTypeName, canvasName);
        var builtInPath = GetBuiltInDefaultLayoutPath(windowTypeName, canvasName);

        if (File.Exists(userPath))
        {
            return await ReadConfigAsync(userPath, cancellationToken);
        }

        return File.Exists(builtInPath)
            ? await ReadConfigAsync(builtInPath, cancellationToken)
            : null;
    }

    /// <inheritdoc />
    public async Task SaveCanvasConfigAsync(
        string windowTypeName,
        string canvasName,
        FrontedCanvasConfig config,
        CancellationToken cancellationToken = default)
    {
        var path = GetUserLayoutPath(windowTypeName, canvasName);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        config.Version = 3;
        var json = JsonSerializer.Serialize(config, _jsonSerializerOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    /// <inheritdoc />
    public string GetUserLayoutPath(string windowTypeName, string canvasName)
    {
        return Path.Combine(AppConstants.FrontedLayoutsPath, windowTypeName, $"{canvasName}.json");
    }

    /// <inheritdoc />
    public string GetBuiltInDefaultLayoutPath(string windowTypeName, string canvasName)
    {
        return Path.Combine(
            AppConstants.ResourcesPath,
            "FrontedLayouts",
            windowTypeName,
            $"{canvasName}.json");
    }

    /// <inheritdoc />
    public string GetPluginDefaultLayoutPath(string pluginFolder, string windowTypeName, string canvasName)
    {
        return Path.Combine(pluginFolder, "FrontedLayouts", windowTypeName, $"{canvasName}.json");
    }

    private async Task<FrontedCanvasConfig?> ReadConfigAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<FrontedCanvasConfig>(json, _jsonSerializerOptions);
    }
}
