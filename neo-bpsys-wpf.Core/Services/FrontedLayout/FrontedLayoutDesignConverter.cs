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

    public FrontedLayoutDesignConverter()
    {
    }

    public FrontedLayoutDesignConverter(IFrontedControlRegistry controlRegistry)
    {
        _controlRegistry = controlRegistry;
    }

    /// <summary>
    /// 从运行时 Canvas 配置创建单 Canvas 设计文档。
    /// </summary>
    public FrontedCanvasDesignDocument FromConfig(
        string windowTypeName,
        string canvasName,
        FrontedCanvasConfig config,
        FrontedLayoutRuntimeContractCatalog runtimeContracts)
    {
        return new FrontedCanvasDesignDocument
        {
            WindowTypeName = windowTypeName,
            CanvasName = canvasName,
            CanvasConfig = config,
            Controls = new ObservableCollection<FrontedControlDesignItem>(
                config.Controls.Select(control => CreateDesignItem(
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
        return new FrontedCanvasConfig
        {
            Version = document.CanvasConfig.Version,
            CanvasWidth = document.CanvasConfig.CanvasWidth,
            CanvasHeight = document.CanvasConfig.CanvasHeight,
            BackgroundImage = document.CanvasConfig.BackgroundImage,
            RequiredPlugins = SyncRequiredPlugins(document),
            Controls = document.Controls.ToDictionary(
                item => item.Name,
                item => item.Config,
                StringComparer.Ordinal)
        };
    }

    private List<FrontedPluginDependency> SyncRequiredPlugins(FrontedCanvasDesignDocument document)
    {
        var previous = document.CanvasConfig.RequiredPlugins
            .Where(plugin => !string.IsNullOrWhiteSpace(plugin.PackageId))
            .ToDictionary(plugin => plugin.PackageId, StringComparer.OrdinalIgnoreCase);

        var dependencies = document.Controls
            .Select(item => item.Config.ControlType)
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
                    MinVersion = existing?.MinVersion,
                    DisplayName = descriptor?.ControlTypeName ?? existing?.DisplayName,
                    MarketplaceId = existing?.MarketplaceId,
                    Controls = controls
                };
            })
            .ToList();

        document.CanvasConfig.RequiredPlugins = dependencies;
        return dependencies;
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
