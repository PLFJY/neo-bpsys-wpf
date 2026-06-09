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
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        MaxDepth = FrontedLayoutLimits.MaxJsonDepth
    };

    /// <summary>
    /// Initializes a user layout store under the default application data layout folder.
    /// </summary>
    public FrontedUserLayoutStore()
        : this(AppConstants.FrontedLayoutsPath)
    {
    }

    /// <summary>
    /// Initializes a user layout store under a custom layout root folder.
    /// </summary>
    /// <param name="rootFolder">Root folder for user window layout JSON files.</param>
    public FrontedUserLayoutStore(string rootFolder)
    {
        _rootFolder = rootFolder;
    }

    /// <inheritdoc />
    public bool Exists(string windowTypeName)
    {
        return File.Exists(GetLayoutPath(windowTypeName));
    }

    /// <inheritdoc />
    public async Task<FrontedWindowConfig?> LoadAsync(
        string windowTypeName,
        CancellationToken cancellationToken = default)
    {
        var path = GetLayoutPath(windowTypeName);
        if (!File.Exists(path))
        {
            return null;
        }

        if (new FileInfo(path).Length > FrontedLayoutLimits.MaxLayoutJsonBytes)
        {
            throw new InvalidDataException("LayoutJsonTooLarge");
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<FrontedWindowConfig>(json, _jsonSerializerOptions);
    }

    /// <inheritdoc />
    public async Task SaveAsync(
        string windowTypeName,
        FrontedWindowConfig config,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_rootFolder);

        config.Version = 3;
        var json = JsonSerializer.Serialize(config, _jsonSerializerOptions);
        await File.WriteAllTextAsync(GetLayoutPath(windowTypeName), json, cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteAsync(
        string windowTypeName,
        CancellationToken cancellationToken = default)
    {
        var path = GetLayoutPath(windowTypeName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public string GetLayoutPath(string windowTypeName)
    {
        return Path.Combine(_rootFolder, FrontedLayoutWindowPathHelper.GetLayoutRelativePath(windowTypeName));
    }

    /// <inheritdoc />
    public string GetRootFolder()
    {
        return _rootFolder;
    }

    /// <inheritdoc />
    public bool LegacyCanvasExists(string windowTypeName, string canvasName)
    {
        return File.Exists(GetLegacyCanvasLayoutPath(windowTypeName, canvasName));
    }

    /// <inheritdoc />
    public async Task<FrontedCanvasConfig?> LoadLegacyCanvasAsync(
        string windowTypeName,
        string canvasName,
        CancellationToken cancellationToken = default)
    {
        var path = GetLegacyCanvasLayoutPath(windowTypeName, canvasName);
        if (!File.Exists(path))
        {
            return null;
        }

        if (new FileInfo(path).Length > FrontedLayoutLimits.MaxLayoutJsonBytes)
        {
            throw new InvalidDataException("LayoutJsonTooLarge");
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<FrontedCanvasConfig>(json, _jsonSerializerOptions);
    }

    /// <inheritdoc />
    public string GetLegacyCanvasLayoutPath(string windowTypeName, string canvasName)
    {
        return Path.Combine(
            _rootFolder,
            FrontedLayoutWindowPathHelper.GetLegacyCanvasLayoutRelativePath(windowTypeName, canvasName));
    }
}
