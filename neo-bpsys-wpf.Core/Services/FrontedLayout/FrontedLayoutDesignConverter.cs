using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using System.Collections.ObjectModel;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// v3 Canvas 配置和设计期文档之间的转换器。
/// </summary>
public class FrontedLayoutDesignConverter
{
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
                    control.Value,
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
            Controls = document.Controls.ToDictionary(
                item => item.Name,
                item => item.Config,
                StringComparer.Ordinal)
        };
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
