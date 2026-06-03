using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// File-backed window-level Designer v3 options store.
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

    public FrontedWindowLayoutOptions LoadOptions(string windowTypeName)
    {
        var path = GetReadOptionsPath(windowTypeName);
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
        string windowTypeName,
        FrontedWindowLayoutOptions options,
        CancellationToken cancellationToken = default)
    {
        var path = await GetWriteOptionsPathAsync(windowTypeName, cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        options.Version = 3;
        var json = JsonSerializer.Serialize(options, _jsonSerializerOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    public string GetUserOptionsPath(string windowTypeName)
    {
        return GetReadOptionsPath(windowTypeName);
    }

    public Task ResetOptionsAsync(string windowTypeName, CancellationToken cancellationToken = default)
    {
        var path = GetUserOptionsPath(windowTypeName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string GetReadOptionsPath(string windowTypeName)
    {
        if (_packageManager is null)
        {
            return GetLegacyOptionsPath(windowTypeName);
        }

        var active = _packageManager.GetActivePackageStateAsync().GetAwaiter().GetResult();
        if (string.Equals(active.PackageId, FrontedLayoutPackageManager.BuiltInPackageId, StringComparison.OrdinalIgnoreCase))
        {
            return GetLegacyOptionsPath(windowTypeName);
        }

        var packagePath = GetPackageOptionsPath(active.PackageId, windowTypeName);
        return File.Exists(packagePath) ? packagePath : GetLegacyOptionsPath(windowTypeName);
    }

    private async Task<string> GetWriteOptionsPathAsync(string windowTypeName, CancellationToken cancellationToken)
    {
        if (_packageManager is null)
        {
            return GetLegacyOptionsPath(windowTypeName);
        }

        var package = await _packageManager.EnsureWritableActivePackageAsync(cancellationToken);
        return GetPackageOptionsPath(package.PackageId, windowTypeName);
    }

    private string GetPackageOptionsPath(string packageId, string windowTypeName)
    {
        return Path.Combine(
            _packageManager!.GetPackageLayoutsRootFolder(packageId),
            FrontedLayoutWindowPathHelper.GetWindowOptionsRelativePath(windowTypeName));
    }

    private string GetLegacyOptionsPath(string windowTypeName)
    {
        return Path.Combine(_frontedLayoutsRoot, FrontedLayoutWindowPathHelper.GetWindowOptionsRelativePath(windowTypeName));
    }
}
