using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

internal static class FrontedLayoutPluginDependencyScanner
{
    public static List<FrontedPluginDependency> SyncCanvasRequiredPlugins(
        FrontedCanvasConfig config,
        string windowTypeName,
        string canvasName,
        IFrontedControlRegistry? controlRegistry = null,
        IFrontedPluginMetadataProvider? pluginMetadataProvider = null)
    {
        var existingByPackage = config.RequiredPlugins
            .Where(dependency => !string.IsNullOrWhiteSpace(dependency.PackageId))
            .GroupBy(dependency => dependency.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var dependencies = config.Controls
            .Values
            .Select(control => control.ControlType)
            .Where(FrontedPluginControlType.IsPluginControlType)
            .Select(FrontedPluginControlType.Parse)
            .GroupBy(parsed => parsed.PackageId, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                existingByPackage.TryGetValue(group.Key, out var existing);
                var controls = group
                    .Select(parsed => parsed.ToString())
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToList();

                return new FrontedPluginDependency
                {
                    PackageId = group.Key,
                    MinVersion = ResolveMinVersion(group.Key, existing, pluginMetadataProvider),
                    DisplayName = ResolveDisplayName(existing, controls, controlRegistry, pluginMetadataProvider),
                    MarketplaceId = string.IsNullOrWhiteSpace(existing?.MarketplaceId) ? group.Key : existing.MarketplaceId,
                    Controls = controls,
                    RequiredBy = [$"{windowTypeName}/{canvasName}"]
                };
            })
            .ToList();

        config.RequiredPlugins = dependencies;
        return dependencies;
    }

    public static List<FrontedPluginDependency> MergePackageDependencies(
        IEnumerable<(string Window, string Canvas, FrontedCanvasConfig Config)> layouts,
        IEnumerable<FrontedPluginDependency>? manifestDependencies,
        IFrontedControlRegistry? controlRegistry = null,
        IFrontedPluginMetadataProvider? pluginMetadataProvider = null)
    {
        var packageSummaries = new Dictionary<string, FrontedPluginDependency>(StringComparer.OrdinalIgnoreCase);
        foreach (var dependency in manifestDependencies ?? [])
        {
            if (string.IsNullOrWhiteSpace(dependency.PackageId))
            {
                continue;
            }

            packageSummaries[dependency.PackageId] = CloneDependency(dependency);
        }

        foreach (var (window, canvas, config) in layouts)
        {
            foreach (var dependency in SyncCanvasRequiredPlugins(config, window, canvas, controlRegistry, pluginMetadataProvider))
            {
                if (!packageSummaries.TryGetValue(dependency.PackageId, out var summary))
                {
                    summary = new FrontedPluginDependency
                    {
                        PackageId = dependency.PackageId,
                        MarketplaceId = dependency.MarketplaceId,
                        DisplayName = dependency.DisplayName
                    };
                    packageSummaries.Add(summary.PackageId, summary);
                }

                summary.MinVersion = ResolveSummaryMinVersion(summary.PackageId, summary.MinVersion, dependency.MinVersion, pluginMetadataProvider);
                summary.DisplayName = string.IsNullOrWhiteSpace(summary.DisplayName)
                    ? dependency.DisplayName
                    : summary.DisplayName;
                summary.MarketplaceId = string.IsNullOrWhiteSpace(summary.MarketplaceId)
                    ? dependency.MarketplaceId
                    : summary.MarketplaceId;
                AddDistinct(summary.Controls, dependency.Controls);
                AddDistinct(summary.RequiredBy, dependency.RequiredBy);
            }
        }

        return packageSummaries.Values
            .Where(dependency => dependency.Controls.Count > 0 || dependency.RequiredBy.Count > 0)
            .OrderBy(dependency => dependency.PackageId, StringComparer.OrdinalIgnoreCase)
            .Select(NormalizeDependency)
            .ToList();
    }

    public static List<FrontedLayoutPackagePluginControlIssue> FindMissingPluginControls(
        IEnumerable<(string Window, string Canvas, FrontedCanvasConfig Config)> layouts,
        IFrontedControlRegistry? controlRegistry)
    {
        if (controlRegistry is null)
        {
            return [];
        }

        return layouts
            .SelectMany(layout => layout.Config.Controls
                .Where(control => FrontedPluginControlType.IsPluginControlType(control.Value.ControlType))
                .Where(control => !controlRegistry.IsPluginControlRegistered(control.Value.ControlType))
                .Select(control =>
                {
                    var parsed = FrontedPluginControlType.Parse(control.Value.ControlType);
                    return new FrontedLayoutPackagePluginControlIssue
                    {
                        Window = layout.Window,
                        Canvas = layout.Canvas,
                        ControlName = control.Key,
                        ControlType = control.Value.ControlType,
                        PackageId = parsed.PackageId
                    };
                }))
            .OrderBy(issue => issue.Window, StringComparer.Ordinal)
            .ThenBy(issue => issue.Canvas, StringComparer.Ordinal)
            .ThenBy(issue => issue.ControlName, StringComparer.Ordinal)
            .ToList();
    }

    public static List<FrontedLayoutPackagePluginDependencyIssue> FindUnsatisfiedPluginDependencies(
        IEnumerable<(string Window, string Canvas, FrontedCanvasConfig Config)> layouts,
        IEnumerable<FrontedPluginDependency>? manifestDependencies,
        IFrontedControlRegistry? controlRegistry,
        IFrontedPluginMetadataProvider? pluginMetadataProvider)
    {
        var layoutList = layouts.ToList();
        var dependencies = MergePackageDependencies(layoutList, manifestDependencies, controlRegistry);
        var missingControls = FindMissingPluginControls(layoutList, controlRegistry);
        var issues = new List<FrontedLayoutPackagePluginDependencyIssue>();

        foreach (var dependency in dependencies)
        {
            var isInstalled = pluginMetadataProvider?.IsPluginInstalled(dependency.PackageId) == true;
            var installedVersion = string.Empty;
            pluginMetadataProvider?.TryGetPluginVersion(dependency.PackageId, out installedVersion);
            var affectedControls = missingControls
                .Where(control => string.Equals(control.PackageId, dependency.PackageId, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (dependency.Controls.Count == 0 && affectedControls.Count == 0)
            {
                continue;
            }

            var versionSatisfied = IsVersionSatisfied(installedVersion, dependency.MinVersion);
            if (isInstalled && versionSatisfied && affectedControls.Count == 0)
            {
                continue;
            }

            issues.Add(new FrontedLayoutPackagePluginDependencyIssue
            {
                PackageId = dependency.PackageId,
                DisplayName = dependency.DisplayName,
                MinVersion = dependency.MinVersion,
                InstalledVersion = string.IsNullOrWhiteSpace(installedVersion) ? null : installedVersion,
                MarketplaceId = string.IsNullOrWhiteSpace(dependency.MarketplaceId) ? dependency.PackageId : dependency.MarketplaceId,
                IsInstalled = isInstalled,
                IsVersionSatisfied = versionSatisfied,
                Controls = [.. dependency.Controls],
                RequiredBy = [.. dependency.RequiredBy],
                AffectedControls = affectedControls
            });
        }

        return issues
            .OrderBy(issue => issue.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<FrontedLayoutPackageRemovedPluginControl> RemoveMissingPluginControls(
        IEnumerable<(string Window, string Canvas, FrontedCanvasConfig Config)> layouts,
        IFrontedControlRegistry? controlRegistry,
        IReadOnlySet<string>? unsatisfiedPackageIds = null)
    {
        var removed = new List<FrontedLayoutPackageRemovedPluginControl>();
        foreach (var (window, canvas, config) in layouts)
        {
            var missingControlNames = config.Controls
                .Where(control => FrontedPluginControlType.IsPluginControlType(control.Value.ControlType))
                .Where(control =>
                {
                    var parsed = FrontedPluginControlType.Parse(control.Value.ControlType);
                    return controlRegistry?.IsPluginControlRegistered(control.Value.ControlType) != true
                           || unsatisfiedPackageIds?.Contains(parsed.PackageId) == true;
                })
                .Select(control =>
                {
                    var parsed = FrontedPluginControlType.Parse(control.Value.ControlType);
                    removed.Add(new FrontedLayoutPackageRemovedPluginControl
                    {
                        Window = window,
                        Canvas = canvas,
                        ControlName = control.Key,
                        ControlType = control.Value.ControlType,
                        PackageId = parsed.PackageId
                    });
                    return control.Key;
                })
                .ToArray();

            foreach (var name in missingControlNames)
            {
                config.Controls.Remove(name);
            }

            SyncCanvasRequiredPlugins(config, window, canvas, controlRegistry);
        }

        return removed;
    }

    private static bool IsVersionSatisfied(string? installedVersion, string? minVersion)
    {
        if (string.IsNullOrWhiteSpace(minVersion))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(installedVersion))
        {
            return false;
        }

        if (!TryParseVersion(installedVersion, out var installed)
            || !TryParseVersion(minVersion, out var required))
        {
            return false;
        }

        return installed >= required;
    }

    private static bool TryParseVersion(string value, out Version version)
    {
        var normalized = value.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        var metadataIndex = normalized.IndexOfAny(['+', '-']);
        if (metadataIndex > 0)
        {
            normalized = normalized[..metadataIndex];
        }

        return Version.TryParse(normalized, out version!);
    }

    private static string ResolveDisplayName(
        FrontedPluginDependency? existing,
        IReadOnlyList<string> controls,
        IFrontedControlRegistry? controlRegistry,
        IFrontedPluginMetadataProvider? pluginMetadataProvider)
    {
        var packageId = existing?.PackageId ?? FrontedPluginControlType.Parse(controls[0]).PackageId;
        if (pluginMetadataProvider?.TryGetPluginDisplayName(packageId, out var displayName) == true
            && !string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        if (!string.IsNullOrWhiteSpace(existing?.DisplayName))
        {
            return existing.DisplayName;
        }

        var descriptor = controls
            .Select(controlType => controlRegistry?.GetPluginDescriptor(controlType))
            .FirstOrDefault(descriptor => descriptor is not null);

        return descriptor?.PackageId ?? existing?.PackageId ?? FrontedPluginControlType.Parse(controls[0]).PackageId;
    }

    private static string? ResolveMinVersion(
        string packageId,
        FrontedPluginDependency? existing,
        IFrontedPluginMetadataProvider? pluginMetadataProvider)
    {
        return pluginMetadataProvider?.TryGetPluginVersion(packageId, out var version) == true
            && !string.IsNullOrWhiteSpace(version)
            ? version
            : existing?.MinVersion;
    }

    private static string? ResolveSummaryMinVersion(
        string packageId,
        string? current,
        string? incoming,
        IFrontedPluginMetadataProvider? pluginMetadataProvider)
    {
        if (pluginMetadataProvider?.TryGetPluginVersion(packageId, out var version) == true
            && !string.IsNullOrWhiteSpace(version))
        {
            return version;
        }

        return string.IsNullOrWhiteSpace(current) ? incoming : current;
    }

    private static FrontedPluginDependency CloneDependency(FrontedPluginDependency dependency)
    {
        return NormalizeDependency(new FrontedPluginDependency
        {
            PackageId = dependency.PackageId,
            MinVersion = dependency.MinVersion,
            DisplayName = dependency.DisplayName,
            MarketplaceId = dependency.MarketplaceId,
            Controls = [.. dependency.Controls],
            RequiredBy = [.. dependency.RequiredBy]
        });
    }

    private static FrontedPluginDependency NormalizeDependency(FrontedPluginDependency dependency)
    {
        dependency.MarketplaceId = string.IsNullOrWhiteSpace(dependency.MarketplaceId)
            ? dependency.PackageId
            : dependency.MarketplaceId;
        dependency.DisplayName = string.IsNullOrWhiteSpace(dependency.DisplayName)
            ? dependency.PackageId
            : dependency.DisplayName;
        dependency.Controls = dependency.Controls
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        dependency.RequiredBy = dependency.RequiredBy
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        return dependency;
    }

    private static void AddDistinct(List<string> target, IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value)
                && !target.Contains(value, StringComparer.Ordinal))
            {
                target.Add(value);
            }
        }
    }
}
