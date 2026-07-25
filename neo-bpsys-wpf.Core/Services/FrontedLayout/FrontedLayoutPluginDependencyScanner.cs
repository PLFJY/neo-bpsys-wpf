using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

internal static class FrontedLayoutPluginDependencyScanner
{
    public static List<FrontedPluginDependency> SyncCanvasRequiredPlugins(
        FrontedCanvasConfig config,
        string canonicalWindowId,
        string canvasName,
        IFrontedV3ControlRegistry? controlRegistry = null,
        IFrontedPluginMetadataProvider? pluginMetadataProvider = null)
    {
        _ = canvasName;
        var existingByPackage = config.RequiredPlugins
            .Where(dependency => !string.IsNullOrWhiteSpace(dependency.PackageId))
            .GroupBy(dependency => dependency.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var dependencies = EnumerateStateControls(config)
            .SelectMany(state => state.Controls.Values)
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
                    Reason = FrontedPluginDependencyReason.FrontedControl,
                    Controls = controls,
                    RequiredBy = [canonicalWindowId]
                };
            })
            .ToList();

        if (FrontedV3LayoutWindowPathHelper.TryParsePluginCanonicalWindowId(canonicalWindowId, out var windowPackageId, out _))
        {
            var existing = existingByPackage.GetValueOrDefault(windowPackageId);
            var windowDependency = dependencies.FirstOrDefault(dependency =>
                string.Equals(dependency.PackageId, windowPackageId, StringComparison.OrdinalIgnoreCase));
            if (windowDependency is null)
            {
                dependencies.Add(new FrontedPluginDependency
                {
                    PackageId = windowPackageId,
                    MinVersion = ResolveMinVersion(windowPackageId, existing, pluginMetadataProvider),
                    DisplayName = pluginMetadataProvider?.TryGetPluginDisplayName(windowPackageId, out var displayName) == true
                        ? displayName
                        : existing?.DisplayName ?? windowPackageId,
                    MarketplaceId = string.IsNullOrWhiteSpace(existing?.MarketplaceId) ? windowPackageId : existing.MarketplaceId,
                    RequiredBy = [canonicalWindowId],
                    Reason = FrontedPluginDependencyReason.FrontedWindow
                });
            }
            else
            {
                AddDistinct(windowDependency.RequiredBy, [canonicalWindowId]);
                windowDependency.Reason = FrontedPluginDependencyReason.Both;
            }
        }

        dependencies = dependencies
            .OrderBy(dependency => dependency.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        config.RequiredPlugins = dependencies;
        foreach (var state in config.BoModeStates.Values)
        {
            state.RequiredPlugins = dependencies;
        }

        return dependencies;
    }

    public static List<FrontedPluginDependency> MergePackageDependencies(
        IEnumerable<(string Window, string Canvas, FrontedCanvasConfig Config)> layouts,
        IEnumerable<FrontedPluginDependency>? manifestDependencies,
        IFrontedV3ControlRegistry? controlRegistry = null,
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
                        DisplayName = dependency.DisplayName,
                        Reason = dependency.Reason
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
                summary.Reason = MergeReason(summary.Reason, dependency.Reason);
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
        IFrontedV3ControlRegistry? controlRegistry)
    {
        if (controlRegistry is null)
        {
            return [];
        }

        return layouts
            .SelectMany(layout => EnumerateStateControls(layout.Config)
                .SelectMany(state => state.Controls)
                .Where(control => FrontedPluginControlType.IsPluginControlType(control.Value.ControlType))
                .Where(control => controlRegistry.GetRegistration(control.Value.ControlType) is null)
                .Select(control =>
                {
                    var parsed = FrontedPluginControlType.Parse(control.Value.ControlType);
                    return new FrontedLayoutPackagePluginControlIssue
                    {
                        Window = layout.Window,
                        ControlName = control.Key,
                        ControlType = control.Value.ControlType,
                        PackageId = parsed.PackageId
                    };
                }))
            .OrderBy(issue => issue.Window, StringComparer.Ordinal)
            .ThenBy(issue => issue.ControlName, StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<(string StateName, IReadOnlyDictionary<string, FrontedControlConfigBase> Controls)> EnumerateStateControls(
        FrontedCanvasConfig config)
    {
        yield return ("Bo5", config.Controls);
        foreach (var (stateName, state) in config.BoModeStates)
        {
            yield return (stateName, state.Controls);
        }
    }

    public static List<FrontedLayoutPackagePluginDependencyIssue> FindUnsatisfiedPluginDependencies(
        IEnumerable<(string Window, string Canvas, FrontedCanvasConfig Config)> layouts,
        IEnumerable<FrontedPluginDependency>? manifestDependencies,
        IFrontedV3ControlRegistry? controlRegistry,
        IFrontedPluginMetadataProvider? pluginMetadataProvider)
    {
        var layoutList = layouts.ToList();
        var dependencies = MergePackageDependencies(layoutList, manifestDependencies, controlRegistry, pluginMetadataProvider);
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
            if (dependency.Controls.Count == 0
                && affectedControls.Count == 0
                && dependency.Reason != FrontedPluginDependencyReason.FrontedWindow)
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
                Reason = dependency.Reason,
                RequiredBy = [.. dependency.RequiredBy],
                AffectedControls = affectedControls
            });
        }

        return issues
            .OrderBy(issue => issue.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToList();
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
        IFrontedV3ControlRegistry? controlRegistry,
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

        var registration = controls
            .Select(controlType => controlRegistry?.GetRegistration(controlType))
            .FirstOrDefault(registration => registration is not null);

        return registration?.PackageId ?? existing?.PackageId ?? FrontedPluginControlType.Parse(controls[0]).PackageId;
    }

    private static string? ResolveMinVersion(
        string packageId,
        FrontedPluginDependency? existing,
        IFrontedPluginMetadataProvider? pluginMetadataProvider)
    {
        var installedVersion = string.Empty;
        pluginMetadataProvider?.TryGetPluginVersion(packageId, out installedVersion);
        return ChooseHigherVersion(existing?.MinVersion, installedVersion);
    }

    private static string? ResolveSummaryMinVersion(
        string packageId,
        string? current,
        string? incoming,
        IFrontedPluginMetadataProvider? pluginMetadataProvider)
    {
        var installedVersion = string.Empty;
        pluginMetadataProvider?.TryGetPluginVersion(packageId, out installedVersion);
        return ChooseHigherVersion(ChooseHigherVersion(current, incoming), installedVersion);
    }

    private static string? ChooseHigherVersion(string? first, string? second)
    {
        if (string.IsNullOrWhiteSpace(first))
        {
            return string.IsNullOrWhiteSpace(second) ? null : second;
        }

        if (string.IsNullOrWhiteSpace(second))
        {
            return first;
        }

        if (TryParseVersion(first, out var firstVersion)
            && TryParseVersion(second, out var secondVersion))
        {
            return secondVersion > firstVersion ? second : first;
        }

        return first;
    }

    private static FrontedPluginDependency CloneDependency(FrontedPluginDependency dependency)
    {
        return NormalizeDependency(new FrontedPluginDependency
        {
            PackageId = dependency.PackageId,
            MinVersion = dependency.MinVersion,
            DisplayName = dependency.DisplayName,
            MarketplaceId = dependency.MarketplaceId,
            Reason = dependency.Reason,
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
        if (dependency.Reason == FrontedPluginDependencyReason.Unknown)
        {
            dependency.Reason = dependency.Controls.Count > 0
                ? FrontedPluginDependencyReason.FrontedControl
                : FrontedPluginDependencyReason.Unknown;
        }

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

    private static FrontedPluginDependencyReason MergeReason(
        FrontedPluginDependencyReason left,
        FrontedPluginDependencyReason right)
    {
        if (left == right)
        {
            return left;
        }

        if (left == FrontedPluginDependencyReason.Unknown)
        {
            return right;
        }

        if (right == FrontedPluginDependencyReason.Unknown)
        {
            return left;
        }

        return FrontedPluginDependencyReason.Both;
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
