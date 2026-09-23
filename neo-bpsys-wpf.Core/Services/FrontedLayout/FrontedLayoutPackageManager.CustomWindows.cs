#pragma warning disable CS1591

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 前台布局包管理器的CustomWindows逻辑。
/// </summary>
public sealed partial class FrontedLayoutPackageManager
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<FrontedCustomV3LayoutWindowRegistration>> GetActiveCustomWindowsAsync(
        CancellationToken cancellationToken = default)
    {
        var activeState = await GetActivePackageStateAsync(cancellationToken);
        if (string.Equals(activeState.PackageId, BuiltInPackageId, StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<FrontedCustomV3LayoutWindowRegistration>();
        }

        EnsureSafePackageId(activeState.PackageId);
        var packagePath = GetInstalledPackagePath(activeState.PackageId);
        var manifest = await ReadManifestAsync(packagePath, cancellationToken);
        var registrations = new List<FrontedCustomV3LayoutWindowRegistration>();
        foreach (var entry in manifest.Content.CustomWindows)
        {
            if (!FrontedV3LayoutWindowPathHelper.TryParseCustomCanonicalWindowId(
                    entry.Window, out var packageId, out var localWindowId)
                || !string.Equals(packageId, activeState.PackageId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    entry.Path.Replace('\\', '/'),
                    Path.Combine("FrontedLayouts", FrontedV3LayoutWindowPathHelper.GetLayoutRelativePath(entry.Window))
                        .Replace('\\', '/'),
                    StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Ignoring invalid custom window entry {Window} in package {PackageId}.", entry.Window, activeState.PackageId);
                continue;
            }

            var configPath = GetPackageLayoutPath(activeState.PackageId, entry.Window);
            if (!File.Exists(configPath))
            {
                _logger.LogWarning("Ignoring missing custom window layout {Path}.", configPath);
                continue;
            }

            try
            {
                var json = await File.ReadAllTextAsync(configPath, cancellationToken);
                var config = JsonSerializer.Deserialize<FrontedWindowConfig>(json, _jsonSerializerOptions);
                if (config is null || config.Version != 3)
                {
                    continue;
                }

                registrations.Add(new FrontedCustomV3LayoutWindowRegistration
                {
                    Id = entry.Window,
                    LocalId = localWindowId,
                    PackageId = null,
                    PackageScopeId = activeState.PackageId,
                    IsBuiltIn = false,
                    DisplayName = ResolveCustomDisplayName(config.DisplayNames, localWindowId),
                    DisplayNames = new Dictionary<string, string>(config.DisplayNames, StringComparer.Ordinal)
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read custom window layout {Path}.", configPath);
            }
        }

        return registrations;
    }

    /// <inheritdoc />
    public async Task<FrontedCustomV3LayoutWindowRegistration> CreateCustomWindowAsync(
        FrontedCustomWindowCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var package = await EnsureWritableActivePackageAsync(cancellationToken);
        EnsureSafePackageId(package.PackageId);

        var windowId = string.IsNullOrWhiteSpace(request.WindowId)
            ? $"window-{Guid.NewGuid():N}"
            : request.WindowId.Trim();
        FrontedWindowIdentity.EnsureValidWindowLocalId(windowId);
        if (!FrontedV3LayoutWindowPathHelper.IsSafePathSegment(windowId))
        {
            throw new ArgumentException("Window ID contains unsupported characters.", nameof(request));
        }

        var displayNames = request.DisplayNames
            .Where(pair => pair.Key is "zh_Hans" or "en_US" or "ja_JP")
            .Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value?.Trim() ?? string.Empty))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        if (displayNames.Count == 0)
        {
            throw new ArgumentException("At least one display name is required.", nameof(request));
        }

        if (displayNames.Values.Any(value => value.Length > FrontedLayoutLimits.MaxWindowDisplayNameLength))
        {
            throw new ArgumentException("A display name is too long.", nameof(request));
        }

        var canonicalWindowId = FrontedWindowIdentity.BuildCustomCanonicalId(package.PackageId, windowId);
        var manifest = await ReadManifestAsync(package.InstallPath, cancellationToken);
        if (manifest.Content.CustomWindows.Any(entry =>
                string.Equals(entry.Window, canonicalWindowId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("A custom window with this ID already exists.");
        }

        var config = new FrontedWindowConfig { Version = 3, DisplayNames = displayNames };
        var layoutPath = GetPackageLayoutPath(package.PackageId, canonicalWindowId);
        if (File.Exists(layoutPath))
        {
            throw new InvalidOperationException("The custom window layout file already exists.");
        }

        await WriteConfigAsync(layoutPath, config, cancellationToken);
        try
        {
            manifest.CreatedVersion = string.IsNullOrWhiteSpace(manifest.CreatedVersion)
                ? AppConstants.AppVersion
                : manifest.CreatedVersion;
            manifest.Content.CustomWindows.Add(new FrontedLayoutPackageLayoutEntry
            {
                Window = canonicalWindowId,
                Path = Path.Combine("FrontedLayouts", FrontedV3LayoutWindowPathHelper.GetLayoutRelativePath(canonicalWindowId))
                    .Replace('\\', '/')
            });
            await WriteManifestAsync(package.InstallPath, manifest, cancellationToken);
        }
        catch
        {
            File.Delete(layoutPath);
            throw;
        }

        return new FrontedCustomV3LayoutWindowRegistration
        {
            Id = canonicalWindowId,
            LocalId = windowId,
            PackageId = null,
            PackageScopeId = package.PackageId,
            IsBuiltIn = false,
            DisplayName = ResolveCustomDisplayName(displayNames, windowId),
            DisplayNames = new Dictionary<string, string>(displayNames, StringComparer.Ordinal)
        };
    }

    /// <inheritdoc />
    public async Task DeleteCustomWindowAsync(
        string canonicalWindowId,
        CancellationToken cancellationToken = default)
    {
        if (!FrontedV3LayoutWindowPathHelper.TryParseCustomCanonicalWindowId(
                canonicalWindowId, out var packageId, out _))
        {
            throw new ArgumentException("The window ID is not a valid custom Canonical ID.", nameof(canonicalWindowId));
        }

        var activeState = await GetActivePackageStateAsync(cancellationToken);
        if (!string.Equals(activeState.PackageId, packageId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(activeState.PackageId, BuiltInPackageId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only a custom window in the active user package can be deleted.");
        }

        var packagePath = GetInstalledPackagePath(packageId);
        var manifest = await ReadManifestAsync(packagePath, cancellationToken);
        var entry = manifest.Content.CustomWindows.FirstOrDefault(item =>
            string.Equals(item.Window, canonicalWindowId, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return;
        }

        manifest.Content.CustomWindows.Remove(entry);
        await WriteManifestAsync(packagePath, manifest, cancellationToken);

        var layoutPath = CombineInsideRoot(packagePath, entry.Path);
        if (File.Exists(layoutPath))
        {
            File.Delete(layoutPath);
        }

        var behaviorPath = GetBehaviorPath(packagePath, canonicalWindowId);
        if (File.Exists(behaviorPath))
        {
            File.Delete(behaviorPath);
        }
    }

}
