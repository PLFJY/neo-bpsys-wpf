using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// File-backed window-level Designer v3 options store.
/// </summary>
public class FrontedWindowLayoutOptionsService : IFrontedWindowLayoutOptionsService
{
    private static readonly Regex SafeWindowTypeNameRegex = new(
        "^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly string _frontedLayoutsRoot;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        MaxDepth = FrontedLayoutLimits.MaxJsonDepth
    };

    public FrontedWindowLayoutOptionsService()
        : this(AppConstants.FrontedLayoutsPath)
    {
    }

    public FrontedWindowLayoutOptionsService(string frontedLayoutsRoot)
    {
        _frontedLayoutsRoot = frontedLayoutsRoot;
    }

    public FrontedWindowLayoutOptions LoadOptions(string windowTypeName)
    {
        var path = GetUserOptionsPath(windowTypeName);
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
        var path = GetUserOptionsPath(windowTypeName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        options.Version = 3;
        var json = JsonSerializer.Serialize(options, _jsonSerializerOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    public string GetUserOptionsPath(string windowTypeName)
    {
        if (!SafeWindowTypeNameRegex.IsMatch(windowTypeName))
        {
            throw new ArgumentException("Window type name is not safe.", nameof(windowTypeName));
        }

        return Path.Combine(_frontedLayoutsRoot, windowTypeName, "window.json");
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
}
