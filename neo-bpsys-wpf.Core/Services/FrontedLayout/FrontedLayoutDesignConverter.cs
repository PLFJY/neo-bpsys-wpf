using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Abstractions.Services;
using System.Collections.ObjectModel;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// v3 Canvas 配置和设计期文档之间的转换器。
/// </summary>
public class FrontedLayoutDesignConverter
{
    private readonly IFrontedControlRegistry? _controlRegistry;
    private readonly IFrontedPluginMetadataProvider? _pluginMetadataProvider;

    public FrontedLayoutDesignConverter()
    {
    }

    public FrontedLayoutDesignConverter(IFrontedControlRegistry controlRegistry)
    {
        _controlRegistry = controlRegistry;
    }

    public FrontedLayoutDesignConverter(
        IFrontedControlRegistry controlRegistry,
        IFrontedPluginMetadataProvider pluginMetadataProvider)
    {
        _controlRegistry = controlRegistry;
        _pluginMetadataProvider = pluginMetadataProvider;
    }

    /// <summary>
    /// 从运行时 Canvas 配置创建单 Canvas 设计文档。
    /// </summary>
    public FrontedCanvasDesignDocument FromConfig(
        string windowTypeName,
        string canvasName,
        FrontedCanvasConfig config,
        FrontedLayoutRuntimeContractCatalog runtimeContracts,
        FrontedCanvasBoModeState editingState = FrontedCanvasBoModeState.Bo5)
    {
        var state = GetEditableState(config, editingState);
        return new FrontedCanvasDesignDocument
        {
            WindowTypeName = windowTypeName,
            CanvasName = canvasName,
            CanvasConfig = config,
            EditingBoModeState = editingState,
            Controls = new ObservableCollection<FrontedControlDesignItem>(
                state.Controls.Select(control => CreateDesignItem(
                    windowTypeName,
                    canvasName,
                    control.Key,
                    FrontedPluginControlConfigMaterializer.Materialize(control.Key, control.Value, _controlRegistry),
                    runtimeContracts)))
        };
    }

    /// <summary>
    /// 从单 Canvas 设计文档生成运行时 Canvas 配置。
    /// </summary>
    public FrontedCanvasConfig ToConfig(FrontedCanvasDesignDocument document)
    {
        var config = new FrontedCanvasConfig
        {
            Version = document.CanvasConfig.Version,
            CanvasWidth = document.CanvasConfig.CanvasWidth,
            CanvasHeight = document.CanvasConfig.CanvasHeight,
            BackgroundImage = document.CanvasConfig.BackgroundImage,
            EnableBoModeStates = document.CanvasConfig.EnableBoModeStates,
            BoModeStates = document.CanvasConfig.BoModeStates.ToDictionary(
                state => state.Key,
                state => CloneState(state.Value),
                StringComparer.Ordinal),
            RequiredPlugins = new List<FrontedPluginDependency>(document.CanvasConfig.RequiredPlugins),
            Controls = new Dictionary<string, FrontedControlConfigBase>(
                document.CanvasConfig.Controls,
                StringComparer.Ordinal)
        };

        var controls = document.Controls.ToDictionary(
            item => item.Name,
            item => item.Config,
            StringComparer.Ordinal);

        if (document.EditingBoModeState == FrontedCanvasBoModeState.Bo3)
        {
            if (!config.BoModeStates.TryGetValue(FrontedCanvasRuntimeStateResolver.Bo3StateKey, out var bo3State))
            {
                bo3State = new FrontedCanvasStateConfig();
                config.BoModeStates[FrontedCanvasRuntimeStateResolver.Bo3StateKey] = bo3State;
            }

            bo3State.RequiredPlugins = SyncRequiredPlugins(document, controls);
            bo3State.Controls = controls;
        }
        else
        {
            config.RequiredPlugins = SyncRequiredPlugins(document, controls);
            config.Controls = controls;
        }

        document.CanvasConfig = config;
        return config;
    }

    private static FrontedCanvasStateConfig GetEditableState(
        FrontedCanvasConfig config,
        FrontedCanvasBoModeState editingState)
    {
        if (editingState == FrontedCanvasBoModeState.Bo3)
        {
            if (!config.BoModeStates.TryGetValue(FrontedCanvasRuntimeStateResolver.Bo3StateKey, out var bo3State))
            {
                bo3State = new FrontedCanvasStateConfig();
                config.BoModeStates[FrontedCanvasRuntimeStateResolver.Bo3StateKey] = bo3State;
            }

            return bo3State;
        }

        return new FrontedCanvasStateConfig
        {
            BackgroundImage = config.BackgroundImage,
            RequiredPlugins = config.RequiredPlugins,
            Controls = config.Controls
        };
    }

    private static FrontedCanvasStateConfig CloneState(FrontedCanvasStateConfig state) =>
        new()
        {
            BackgroundImage = state.BackgroundImage,
            RequiredPlugins = new List<FrontedPluginDependency>(state.RequiredPlugins),
            Controls = new Dictionary<string, FrontedControlConfigBase>(state.Controls, StringComparer.Ordinal)
        };

    private List<FrontedPluginDependency> SyncRequiredPlugins(
        FrontedCanvasDesignDocument document,
        IReadOnlyDictionary<string, FrontedControlConfigBase> controls)
    {
        var previousDependencies = document.EditingBoModeState == FrontedCanvasBoModeState.Bo3
            && document.CanvasConfig.BoModeStates.TryGetValue(
                FrontedCanvasRuntimeStateResolver.Bo3StateKey,
                out var bo3State)
            ? bo3State.RequiredPlugins
            : document.CanvasConfig.RequiredPlugins;

        var previous = previousDependencies
            .Where(plugin => !string.IsNullOrWhiteSpace(plugin.PackageId))
            .ToDictionary(plugin => plugin.PackageId, StringComparer.OrdinalIgnoreCase);

        var dependencies = controls.Values
            .Select(config => config.ControlType)
            .Select(controlType => FrontedPluginControlType.TryParse(controlType, out var parsed)
                ? parsed
                : (FrontedPluginControlType?)null)
            .Where(parsed => parsed.HasValue)
            .Select(parsed => parsed!.Value)
            .GroupBy(parsed => parsed.PackageId, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                previous.TryGetValue(group.Key, out var existing);
                var controls = group
                    .Select(parsed => parsed.ToString())
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToList();
                var descriptor = controls
                    .Select(controlType => _controlRegistry?.GetPluginDescriptor(controlType))
                    .FirstOrDefault(descriptor => descriptor is not null);

                return new FrontedPluginDependency
                {
                    PackageId = group.Key,
                    MinVersion = ResolveMinVersion(group.Key, existing),
                    DisplayName = ResolveDisplayName(group.Key, existing, descriptor),
                    MarketplaceId = existing?.MarketplaceId,
                    Reason = FrontedPluginDependencyReason.FrontedControl,
                    Controls = controls,
                    RequiredBy = [$"{document.WindowTypeName}/{document.CanvasName}"]
                };
            })
            .ToList();

        if (FrontedLayoutWindowPathHelper.TryParsePluginFullWindowType(
                document.WindowTypeName,
                out var windowPackageId,
                out _))
        {
            previous.TryGetValue(windowPackageId, out var existing);
            var windowDependency = dependencies.FirstOrDefault(dependency =>
                string.Equals(dependency.PackageId, windowPackageId, StringComparison.OrdinalIgnoreCase));
            if (windowDependency is null)
            {
                dependencies.Add(new FrontedPluginDependency
                {
                    PackageId = windowPackageId,
                    MinVersion = ResolveMinVersion(windowPackageId, existing),
                    DisplayName = _pluginMetadataProvider?.TryGetPluginDisplayName(windowPackageId, out var displayName) == true
                        ? displayName
                        : existing?.DisplayName ?? windowPackageId,
                    MarketplaceId = existing?.MarketplaceId,
                    Reason = FrontedPluginDependencyReason.FrontedWindow,
                    RequiredBy = [$"{document.WindowTypeName}/{document.CanvasName}"]
                });
            }
            else
            {
                windowDependency.Reason = FrontedPluginDependencyReason.Both;
                if (!windowDependency.RequiredBy.Contains($"{document.WindowTypeName}/{document.CanvasName}", StringComparer.Ordinal))
                {
                    windowDependency.RequiredBy.Add($"{document.WindowTypeName}/{document.CanvasName}");
                }
            }
        }

        return dependencies;
    }

    private string? ResolveMinVersion(string packageId, FrontedPluginDependency? existing)
    {
        return _pluginMetadataProvider?.TryGetPluginVersion(packageId, out var version) == true
            && !string.IsNullOrWhiteSpace(version)
            ? version
            : existing?.MinVersion;
    }

    private string ResolveDisplayName(
        string packageId,
        FrontedPluginDependency? existing,
        IFrontedPluginControlDescriptor? descriptor)
    {
        if (_pluginMetadataProvider?.TryGetPluginDisplayName(packageId, out var displayName) == true
            && !string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        return existing?.DisplayName ?? descriptor?.PackageId ?? packageId;
    }

    private static FrontedControlDesignItem CreateDesignItem(
        string windowTypeName,
        string canvasName,
        string name,
        FrontedControlConfigBase config,
        FrontedLayoutRuntimeContractCatalog runtimeContracts)
    {
        var item = new FrontedControlDesignItem
        {
            Name = name,
            Config = config,
            IsRuntimeCritical = runtimeContracts.IsRuntimeCritical(windowTypeName, canvasName, name)
        };

        if (config is PickingBorderOverlayControlConfig pickingBorder)
        {
            item.IsSelectableInEditor = false;
            item.IsEditableInEditor = false;
            item.IsLinkedOverlay = true;
            item.LinkedTargetControlName = pickingBorder.TargetControlName;
        }

        return item;
    }
}
