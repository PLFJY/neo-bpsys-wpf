using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 文件支持的窗口级设计器 v3 选项存储。
/// </summary>
public class FrontedWindowLayoutOptionsService : IFrontedWindowLayoutOptionsService
{
    private readonly string _frontedLayoutsRoot;
    private readonly IFrontedLayoutPackageManager? _packageManager;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        MaxDepth = FrontedLayoutLimits.MaxJsonDepth
    };

    public FrontedWindowLayoutOptionsService()
        : this(AppConstants.FrontedLayoutsPath, null)
    {
    }

    public FrontedWindowLayoutOptionsService(string frontedLayoutsRoot)
        : this(frontedLayoutsRoot, null)
    {
    }

    public FrontedWindowLayoutOptionsService(IFrontedLayoutPackageManager packageManager)
        : this(AppConstants.FrontedLayoutsPath, packageManager)
    {
    }

    public FrontedWindowLayoutOptionsService(string frontedLayoutsRoot, IFrontedLayoutPackageManager? packageManager)
    {
        _frontedLayoutsRoot = frontedLayoutsRoot;
        _packageManager = packageManager;
    }

    public FrontedWindowLayoutOptions LoadOptions(string canonicalWindowId)
    {
        var path = GetReadOptionsPath(canonicalWindowId);
        if (!File.Exists(path))
        {
            return new FrontedWindowLayoutOptions();
        }

        try
        {
            if (new FileInfo(path).Length > FrontedLayoutLimits.MaxWindowOptionsJsonBytes)
            {
                return new FrontedWindowLayoutOptions();
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<FrontedWindowLayoutOptions>(json, _jsonSerializerOptions)
                   ?? new FrontedWindowLayoutOptions();
        }
        catch
        {
            return new FrontedWindowLayoutOptions();
        }
    }

    public async Task SaveOptionsAsync(
        string canonicalWindowId,
        FrontedWindowLayoutOptions options,
        CancellationToken cancellationToken = default)
    {
        var path = await GetWriteOptionsPathAsync(canonicalWindowId, cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        options.Version = 3;
        var json = JsonSerializer.Serialize(options, _jsonSerializerOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    public string GetUserOptionsPath(string canonicalWindowId)
    {
        return GetReadOptionsPath(canonicalWindowId);
    }

    public Task ResetOptionsAsync(string canonicalWindowId, CancellationToken cancellationToken = default)
    {
        var path = GetUserOptionsPath(canonicalWindowId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string GetReadOptionsPath(string canonicalWindowId)
    {
        if (_packageManager is null)
        {
            return GetLegacyOptionsPath(canonicalWindowId);
        }

        var active = _packageManager.GetActivePackageStateAsync().GetAwaiter().GetResult();
        if (string.Equals(active.PackageId, FrontedLayoutPackageManager.BuiltInPackageId, StringComparison.OrdinalIgnoreCase))
        {
            return GetLegacyOptionsPath(canonicalWindowId);
        }

        var packagePath = GetPackageOptionsPath(active.PackageId, canonicalWindowId);
        return File.Exists(packagePath) ? packagePath : GetLegacyOptionsPath(canonicalWindowId);
    }

    private async Task<string> GetWriteOptionsPathAsync(string canonicalWindowId, CancellationToken cancellationToken)
    {
        if (_packageManager is null)
        {
            return GetLegacyOptionsPath(canonicalWindowId);
        }

        var package = await _packageManager.EnsureWritableActivePackageAsync(cancellationToken);
        return GetPackageOptionsPath(package.PackageId, canonicalWindowId);
    }

    private string GetPackageOptionsPath(string packageId, string canonicalWindowId)
    {
        return Path.Combine(
            _packageManager!.GetPackageLayoutsRootFolder(packageId),
            FrontedV3LayoutWindowPathHelper.GetWindowOptionsRelativePath(canonicalWindowId));
    }

    private string GetLegacyOptionsPath(string canonicalWindowId)
    {
        return Path.Combine(_frontedLayoutsRoot, FrontedV3LayoutWindowPathHelper.GetWindowOptionsRelativePath(canonicalWindowId));
    }
}
