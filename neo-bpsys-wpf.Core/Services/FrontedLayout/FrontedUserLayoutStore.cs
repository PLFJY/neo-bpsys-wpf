using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// File-backed Designer v3 user layout store.
/// </summary>
public class FrontedUserLayoutStore : IFrontedUserLayoutStore
{
    private readonly string _rootFolder;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public FrontedUserLayoutStore()
        : this(AppConstants.FrontedLayoutsPath)
    {
    }

    public FrontedUserLayoutStore(string rootFolder)
    {
        _rootFolder = rootFolder;
    }

    public bool Exists(string windowTypeName, string canvasName)
    {
        return File.Exists(GetLayoutPath(windowTypeName, canvasName));
    }

    public async Task<FrontedCanvasConfig?> LoadAsync(
        string windowTypeName,
        string canvasName,
        CancellationToken cancellationToken = default)
    {
        var path = GetLayoutPath(windowTypeName, canvasName);
        if (!File.Exists(path))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<FrontedCanvasConfig>(json, _jsonSerializerOptions);
    }

    public async Task SaveAsync(
        string windowTypeName,
        string canvasName,
        FrontedCanvasConfig config,
        CancellationToken cancellationToken = default)
    {
        var folder = GetLayoutFolder(windowTypeName, canvasName);
        Directory.CreateDirectory(folder);

        config.Version = 3;
        var json = JsonSerializer.Serialize(config, _jsonSerializerOptions);
        await File.WriteAllTextAsync(GetLayoutPath(windowTypeName, canvasName), json, cancellationToken);
    }

    public Task DeleteAsync(
        string windowTypeName,
        string canvasName,
        CancellationToken cancellationToken = default)
    {
        var path = GetLayoutPath(windowTypeName, canvasName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public string GetLayoutPath(string windowTypeName, string canvasName)
    {
        return Path.Combine(GetLayoutFolder(windowTypeName, canvasName), $"{canvasName}.json");
    }

    public string GetLayoutFolder(string windowTypeName, string canvasName)
    {
        return Path.Combine(_rootFolder, windowTypeName);
    }

    public string GetRootFolder()
    {
        return _rootFolder;
    }
}
