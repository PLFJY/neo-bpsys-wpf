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
/// 前台布局包管理器的ManifestPaths逻辑。
/// </summary>
public sealed partial class FrontedLayoutPackageManager
{
    public string GetPackageLayoutsRootFolder(string packageId)
    {
        if (string.Equals(packageId, BuiltInPackageId, StringComparison.OrdinalIgnoreCase))
        {
            return _builtInLayoutRoot;
        }

        if (string.Equals(packageId, LocalPackageId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The local resource package is not a layout scheme.");
        }

        EnsureSafePackageId(packageId);
            return Path.Combine(GetInstalledPackagePath(packageId), "FrontedLayouts");
    }

    /// <summary>
    /// 获取指定包中特定窗口的布局文件完整路径。
    /// </summary>
    /// <param name="packageId">包 ID。</param>
    /// <param name="canonicalWindowId">窗口 Canonical ID。</param>
    /// <returns>布局文件的完整路径。</returns>
    public string GetPackageLayoutPath(string packageId, string canonicalWindowId)
    {
        return Path.Combine(
            GetPackageLayoutsRootFolder(packageId),
            FrontedV3LayoutWindowPathHelper.GetLayoutRelativePath(canonicalWindowId));
    }

    public string GetPackageRootFolder()
    {
        return _packageRoot;
    }

    private FrontedLayoutPackageInfo CreateBuiltInPackage(string activePackageId)
    {
        return new FrontedLayoutPackageInfo
        {
            PackageId = BuiltInPackageId,
            Name = LocalizedOrFallback("BuiltInLayoutSchemeName", "Built-in Layout Scheme"),
            Description = LocalizedOrFallback("BuiltInLayoutSchemeDescription", "Built-in Designer v3 frontend layouts."),
            Source = FrontedLayoutPackageSource.BuiltIn,
            IsBuiltin = true,
            IsActivePackage = string.Equals(activePackageId, BuiltInPackageId, StringComparison.OrdinalIgnoreCase),
            InstallPath = _builtInLayoutRoot,
            LayoutCount = CountFiles(_builtInLayoutRoot, "*.json"),
            ResourceCount = 0,
            ValidationStatus = FrontedLayoutPackageValidationStatus.Valid
        };
    }

    private async Task<FrontedLayoutPackageInfo> LoadInstalledPackageAsync(
        string directory,
        string packageIdFromFolder,
        string activePackageId,
        CancellationToken cancellationToken)
    {
        var info = new FrontedLayoutPackageInfo
        {
            PackageId = packageIdFromFolder,
            Name = packageIdFromFolder,
            InstallPath = directory,
            Source = FrontedLayoutPackageSource.Installed,
            IsActivePackage = string.Equals(packageIdFromFolder, activePackageId, StringComparison.OrdinalIgnoreCase),
            ValidationStatus = FrontedLayoutPackageValidationStatus.Valid,
            LayoutCount = CountFiles(Path.Combine(directory, "FrontedLayouts"), "*.json"),
            ResourceCount = CountFiles(Path.Combine(directory, "Resources"), "*")
                + CountFiles(Path.Combine(directory, "resources"), "*")
        };

        if (!IsSafePackageId(packageIdFromFolder))
        {
            info.ValidationStatus = FrontedLayoutPackageValidationStatus.Error;
            info.ValidationMessage = "PackageId is not safe.";
            return info;
        }

        var manifestPath = Path.Combine(directory, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            info.ValidationStatus = FrontedLayoutPackageValidationStatus.Error;
            info.ValidationMessage = "manifest.json is missing.";
            return info;
        }

        try
        {
            if (new FileInfo(manifestPath).Length > FrontedLayoutLimits.MaxManifestBytes)
            {
                info.ValidationStatus = FrontedLayoutPackageValidationStatus.Error;
                info.ValidationMessage = "ManifestTooLarge";
                return info;
            }

            var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            using var document = JsonDocument.Parse(
                json,
                new JsonDocumentOptions { MaxDepth = FrontedLayoutLimits.MaxJsonDepth });
            ApplyManifest(info, document.RootElement);
        }
        catch (Exception ex)
        {
            info.ValidationStatus = FrontedLayoutPackageValidationStatus.Error;
            info.ValidationMessage = ex.Message;
        }

        return info;
    }

    private void ApplyManifest(FrontedLayoutPackageInfo info, JsonElement root)
    {
        var manifestPackageId = GetString(root, "PackageId");
        if (string.IsNullOrWhiteSpace(manifestPackageId))
        {
            info.ValidationStatus = FrontedLayoutPackageValidationStatus.Error;
            info.ValidationMessage = "PackageId is missing.";
            return;
        }

        if (!IsSafePackageId(manifestPackageId))
        {
            info.ValidationStatus = FrontedLayoutPackageValidationStatus.Error;
            info.ValidationMessage = "PackageId is not safe.";
            return;
        }

        if (!string.Equals(manifestPackageId, info.PackageId, StringComparison.OrdinalIgnoreCase))
        {
            info.ValidationStatus = FrontedLayoutPackageValidationStatus.Warning;
            info.ValidationMessage = "PackageId does not match the install folder name.";
        }

        info.PackageId = manifestPackageId;
        info.Name = GetString(root, "Name") ?? manifestPackageId;
        info.Description = GetString(root, "Description") ?? string.Empty;
        info.Author = GetString(root, "Author") ?? string.Empty;
        info.MinVersion = GetString(root, "MinVersion") ?? string.Empty;
        info.CreatedVersion = GetString(root, "CreatedVersion") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(info.CreatedVersion))
        {
            if (!FrontedPackageVersionComparer.TryParseNumeric(info.CreatedVersion, out var createdVersion))
            {
                info.HasCreatedVersionWarning = true;
                AppendWarning(
                    info,
                    LocalizedOrFallback(
                        "PackageCreatedVersionInvalid",
                        "The package creation version cannot be parsed."));
            }
            else if (FrontedPackageVersionComparer.TryParseNumeric(AppConstants.AppVersion, out var currentVersion)
                     && currentVersion < createdVersion)
            {
                info.HasCreatedVersionWarning = true;
                AppendWarning(
                    info,
                    LocalizedOrFallback(
                        "PackageCreatedByNewerVersion",
                        "This package was created by a newer version of the application."));
            }
        }

        var createdAt = GetString(root, "CreatedAt");
        if (DateTimeOffset.TryParse(createdAt, out var parsedCreatedAt))
        {
            info.CreatedAt = parsedCreatedAt;
        }

        if (root.TryGetProperty("Content", out var content))
        {
            if (content.TryGetProperty("Layouts", out var layouts) && layouts.ValueKind == JsonValueKind.Array)
            {
                info.LayoutCount = layouts.GetArrayLength();
            }

            if (content.TryGetProperty("CustomWindows", out var customWindows)
                && customWindows.ValueKind == JsonValueKind.Array)
            {
                info.LayoutCount += customWindows.GetArrayLength();
            }

            if (content.TryGetProperty("Resources", out var resources) && resources.ValueKind == JsonValueKind.Array)
            {
                info.ResourceCount = resources.GetArrayLength();
            }
        }
    }

    private string GetInstalledPackagePath(string packageId)
    {
        return Path.Combine(_packageRoot, packageId);
    }

    private string GetActivePackageStatePath()
    {
        return Path.Combine(_packageRoot, ActivePackageFileName);
    }

    private async Task<(string PackageId, string DisplayName)> GenerateUserSchemeIdentityAsync(
        string? requestedName,
        CancellationToken cancellationToken)
    {
        var packages = await ListPackagesAsync(cancellationToken);
        var usedNames = packages.Select(package => package.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var usedIds = packages.Select(package => package.PackageId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(requestedName))
        {
            var trimmedName = requestedName.Trim();
            for (var i = 1; i < 10000; i++)
            {
                var name = i == 1 ? trimmedName : $"{trimmedName} {i}";
                var id = GenerateSafePackageIdFromName(name, i);
                if (!usedNames.Contains(name) && !usedIds.Contains(id) && !Directory.Exists(GetInstalledPackagePath(id)))
                {
                    return (id, name);
                }
            }
        }

        var format = LocalizedOrFallback("UserLayoutSchemeNameFormat", "User Layout Scheme {0}");
        for (var i = 1; i < 10000; i++)
        {
            var name = string.Format(format, i);
            var id = $"user-layout-scheme-{i}";
            if (!usedNames.Contains(name) && !usedIds.Contains(id) && !Directory.Exists(GetInstalledPackagePath(id)))
            {
                return (id, name);
            }
        }

        throw new InvalidOperationException("Failed to generate a unique layout scheme name.");
    }

}
