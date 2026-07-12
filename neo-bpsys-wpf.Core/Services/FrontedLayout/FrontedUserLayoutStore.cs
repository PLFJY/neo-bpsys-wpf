using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 文件支持的设计器 v3 用户布局存储。
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
    /// 在默认应用数据布局文件夹下初始化用户布局存储。
    /// </summary>
    public FrontedUserLayoutStore()
        : this(AppConstants.FrontedLayoutsPath)
    {
    }

    /// <summary>
    /// 在自定义布局根文件夹下初始化用户布局存储。
    /// </summary>
    /// <param name="rootFolder">用户窗口布局 JSON 文件的根文件夹。</param>
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
}
