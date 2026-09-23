using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.StyleTransfer;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Geometry;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.StyleTransfer;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Services.FrontedDesigner;
using neo_bpsys_wpf.ViewModels.FrontedDesigner;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace neo_bpsys_wpf.ViewModels.Windows;

public sealed class FrontedDesignerWindowOption(
    string windowTypeName,
    string displayName)
{
    public string WindowTypeName { get; } = windowTypeName;

    public string DisplayName { get; } = displayName;
}

public sealed class FrontedDesignerZoomPreset(string displayName, double scale, bool isFit = false)
{
    public string DisplayName { get; } = displayName;

    public double Scale { get; } = scale;

    public bool IsFit { get; } = isFit;
}

internal sealed record PendingImportedResource(
    string ResourceUri,
    string PhysicalPath,
    DateTimeOffset ImportedAt,
    string SourceContext);

public sealed class FrontedDesignerClipboardPayload(
    string sourceName,
    string controlType,
    string configJson,
    Type configType)
{
    public string SourceName { get; } = sourceName;

    public string ControlType { get; } = controlType;

    public string ConfigJson { get; } = configJson;

    public Type ConfigType { get; } = configType;

    public static FrontedDesignerClipboardPayload Create(FrontedControlDesignItem item)
    {
        var configType = item.Config.GetType();
        return new FrontedDesignerClipboardPayload(
            item.Name,
            item.Config.ControlType,
            JsonSerializer.Serialize(item.Config, configType),
            configType);
    }

    public FrontedControlConfigBase CreateConfig()
    {
        return (FrontedControlConfigBase?)JsonSerializer.Deserialize(ConfigJson, ConfigType)
               ?? throw new InvalidOperationException("Failed to deserialize copied control config.");
    }
}

public sealed class FrontedCanvasBoModeStateOption(
    FrontedCanvasBoModeState state,
    string displayName)
{
    public FrontedCanvasBoModeState State { get; } = state;

    public string DisplayName { get; } = displayName;
}

/// <summary>
/// Designer 中具名布局模板按钮的视图模型，包装模板 Id 与本地化显示名/描述，
/// 供 <see cref="FrontedDesignerWindowViewModel.LayoutTemplates"/> 绑定渲染。
/// </summary>
public sealed class FrontedV3LayoutTemplateViewModel
{
    /// <summary>
    /// 获取或设置模板唯一标识，作为 <see cref="FrontedV3TemplateContext.TemplateId"/> 传给回调。
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 获取或设置模板按钮的本地化显示文本。
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 获取或设置模板按钮的本地化工具提示；无描述时为 <see langword="null"/>。
    /// </summary>
    public string? ToolTip { get; init; }
}

public sealed class FrontedDesignerPreviewRenderRequestedEventArgs(
    FrontedCanvasConfig? config,
    FrontedBehaviorDocument? behaviorDocument,
    FrontedRenderContext? context) : EventArgs
{
    public FrontedCanvasConfig? Config { get; } = config;

    public FrontedBehaviorDocument? BehaviorDocument { get; } = behaviorDocument;

    public FrontedRenderContext? Context { get; } = context;
}

internal sealed class FrontedDesignerUndoSnapshot
{
    /// <summary>
    /// 获取或设置为撤销/重做捕获的画布布局配置。
    /// </summary>
    public FrontedCanvasConfig? CanvasConfig { get; set; }

    /// <summary>
    /// 获取或设置随布局配置一起捕获的行为文档。
    /// </summary>
    public FrontedBehaviorDocument? BehaviorDocument { get; set; }
}

public sealed class FrontedDesignerGeometryPatchRequestedEventArgs(
    IReadOnlyList<FrontedControlDesignItem> changedItems,
    bool rebuildLayerPanel,
    bool rebuildInteractionLayer,
    bool updateSelection,
    bool zIndexChanged) : EventArgs
{
    public IReadOnlyList<FrontedControlDesignItem> ChangedItems { get; } = changedItems;

    public bool RebuildLayerPanel { get; } = rebuildLayerPanel;

    public bool RebuildInteractionLayer { get; } = rebuildInteractionLayer;

    public bool UpdateSelection { get; } = updateSelection;

    public bool ZIndexChanged { get; } = zIndexChanged;

    public bool Applied { get; private set; } = true;

    public string? FailureReason { get; private set; }

    public void RequestFullRenderFallback(string reason)
    {
        Applied = false;
        FailureReason = reason;
    }
}

internal sealed class FrontedDesignerSnapshotDiff(
    bool CanRestoreGeometryOnly,
    bool OrderChanged,
    bool ZIndexChanged,
    string Reason)
{
    public bool CanRestoreGeometryOnly { get; } = CanRestoreGeometryOnly;

    public bool OrderChanged { get; } = OrderChanged;

    public bool ZIndexChanged { get; } = ZIndexChanged;

    public string Reason { get; } = Reason;
}

internal static class FrontedDesignerSnapshotRestorePlanner
{
    private static readonly string[] GeometryProperties =
    [
        nameof(FrontedControlConfigBase.Left),
        nameof(FrontedControlConfigBase.Top),
        nameof(FrontedControlConfigBase.Width),
        nameof(FrontedControlConfigBase.Height),
        nameof(FrontedControlConfigBase.ZIndex),
        "ImageWidth",
        "ImageHeight"
    ];

    public static FrontedDesignerSnapshotDiff CreatePlan(
        FrontedCanvasConfig current,
        FrontedCanvasConfig target)
    {
        if (current.Version != target.Version
            || Math.Abs(current.CanvasWidth - target.CanvasWidth) >= 0.0001D
            || Math.Abs(current.CanvasHeight - target.CanvasHeight) >= 0.0001D
            || !string.Equals(current.BackgroundImage, target.BackgroundImage, StringComparison.Ordinal)
            || current.EnableBoModeStates != target.EnableBoModeStates
            || !JsonEquivalent(current.BoModeStates, target.BoModeStates)
            || !JsonEquivalent(current.RequiredPlugins, target.RequiredPlugins))
        {
            return Fail("canvas/window config changed");
        }

        if (current.Controls.Count != target.Controls.Count)
        {
            return Fail("control count changed");
        }

        var currentNames = current.Controls.Keys.ToArray();
        var targetNames = target.Controls.Keys.ToArray();
        var orderChanged = !currentNames.SequenceEqual(targetNames, StringComparer.Ordinal);
        if (!currentNames.OrderBy(name => name, StringComparer.Ordinal)
                .SequenceEqual(targetNames.OrderBy(name => name, StringComparer.Ordinal), StringComparer.Ordinal))
        {
            return Fail("control names changed");
        }

        var zIndexChanged = false;
        foreach (var (name, targetControl) in target.Controls)
        {
            var currentControl = current.Controls[name];
            if (!string.Equals(currentControl.ControlType, targetControl.ControlType, StringComparison.Ordinal)
                || currentControl.GetType() != targetControl.GetType())
            {
                return Fail($"control identity changed: {name}");
            }

            if (!JsonEquivalentWithoutGeometry(currentControl, targetControl))
            {
                return Fail($"non-geometry property changed: {name}");
            }

            zIndexChanged |= currentControl.ZIndex != targetControl.ZIndex;
        }

        return new FrontedDesignerSnapshotDiff(
            CanRestoreGeometryOnly: true,
            OrderChanged: orderChanged,
            ZIndexChanged: zIndexChanged,
            Reason: "geometry-only");
    }

    private static FrontedDesignerSnapshotDiff Fail(string reason)
    {
        return new FrontedDesignerSnapshotDiff(false, false, false, reason);
    }

    private static bool JsonEquivalentWithoutGeometry(
        FrontedControlConfigBase current,
        FrontedControlConfigBase target)
    {
        return string.Equals(
            CanonicalNonGeometryJson(current),
            CanonicalNonGeometryJson(target),
            StringComparison.Ordinal);
    }

    private static string CanonicalNonGeometryJson(FrontedControlConfigBase config)
    {
        var json = JsonSerializer.Serialize(config, config.GetType());
        var node = JsonNode.Parse(json)?.AsObject();
        if (node is null)
        {
            IAppHost.TryGetService<ILogger<FrontedDesignerWindowViewModel>>()
                ?.LogError("Failed to parse fronted control config JSON.");
            throw new InvalidOperationException("Failed to parse fronted control config JSON.");
        }
        foreach (var property in GeometryProperties)
        {
            node.Remove(property);
        }

        return node.ToJsonString();
    }

    private static bool JsonEquivalent<T>(T current, T target)
    {
        return string.Equals(
            JsonSerializer.Serialize(current),
            JsonSerializer.Serialize(target),
            StringComparison.Ordinal);
    }

}
