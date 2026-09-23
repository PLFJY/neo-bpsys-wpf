using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Tutorial;

namespace neo_bpsys_wpf.ViewModels.FrontedDesigner.GraphEditor;

public sealed partial class FrontedNodePortViewModel : ObservableObject
{
    private readonly Func<string, string, string>? _localize;
    private const double HeaderHeight = 36D;
    private const double PortAreaTopMargin = 8D;
    private const double PortRowHeight = 24D;

    public FrontedNodeEditorViewModel Node { get; }
    public FrontedNodePortDescriptor Descriptor { get; }
    public int Index { get; }
    public string Name => Descriptor.Name;

    /// <summary>本地化的端口种类名称（"Flow" / "Value"）</summary>
    public string PortKindName { get; }

    /// <summary>本地化的值类型名称（"Number" / "String" / …），非值端口为 null</summary>
    public string? ValueTypeName { get; }

    /// <summary>基于端口类型的颜色十六进制值</summary>
    public string PortColorHex { get; }

    /// <summary>获取可见端口标签。</summary>
    public string DisplayName { get; }

    /// <summary>获取用于编辑器样式和帮助文本的语义角色。</summary>
    public FrontedNodePortRole Role { get; }

    /// <summary>获取该端口是否为并行继续端口。</summary>
    public bool IsParallelContinuation => Role == FrontedNodePortRole.ParallelContinuation;

    /// <summary>获取该端口是否为并行分支端口。</summary>
    public bool IsParallelBranch => Role == FrontedNodePortRole.ParallelBranch;

    /// <summary>获取该端口行前应用的外边距。</summary>
    public Thickness RowMargin { get; }

    /// <summary>获取端口中心相对节点卡片顶部的偏移量。</summary>
    public double CenterOffsetY { get; }

    /// <summary>获取解释端口语义的工具提示文本。</summary>
    public string TooltipText { get; }

    /// <summary>获取用于连接检查的简短含义描述。</summary>
    public string Meaning { get; }

    /// <summary>是否为 Flow 端口（FlowIn 或 FlowOut）</summary>
    public bool IsFlowPort { get; }

    /// <summary>连接过程中此端口与待连端口兼容</summary>
    [ObservableProperty]
    private bool _isHighlighted;

    /// <summary>连接过程中此端口与待连端口不兼容</summary>
    [ObservableProperty]
    private bool _isDimmed;

    /// <summary>此端口已有连线</summary>
    [ObservableProperty]
    private bool _isConnected;

    public FrontedNodePortViewModel(
        FrontedNodeEditorViewModel node,
        FrontedNodePortDescriptor descriptor,
        int index,
        double accumulatedExtraSpacing,
        double beforeSpacing,
        Func<string, string, string>? localize = null)
    {
        Node = node;
        Descriptor = descriptor;
        Index = index;
        _localize = localize;
        RowMargin = new Thickness(0, beforeSpacing, 0, 0);
        CenterOffsetY = HeaderHeight + PortAreaTopMargin + index * PortRowHeight + accumulatedExtraSpacing + PortRowHeight / 2D;

        IsFlowPort = descriptor.PortKind is FrontedNodePortKind.FlowIn or FrontedNodePortKind.FlowOut;
        Role = GetRole(node.Model.NodeType, descriptor.Name);
        DisplayName = GetDisplayName(descriptor);

        PortKindName = IsFlowPort
            ? Localize("Designer.Graph.PortKind.Flow", "Flow")
            : Localize("Designer.Graph.PortKind.Value", "Value");

        ValueTypeName = GetValueTypeDisplayName(descriptor.ValueType);
        PortColorHex = GetPortColor(descriptor, Role);
        TooltipText = GetTooltipText();
        Meaning = GetMeaning();
    }

    /// <summary>
    /// 判断两个端口是否可以建立连接。委托到 <see cref="FrontedNodePortDescriptor.AreCompatible"/>。
    /// </summary>
    public static bool ArePortsCompatible(FrontedNodePortDescriptor source, FrontedNodePortDescriptor target) =>
        FrontedNodePortDescriptor.AreCompatible(source, target);

    /// <summary>获取端口在端口类型维度上的颜色映射</summary>
    public static string GetPortColor(FrontedNodePortDescriptor descriptor, FrontedNodePortRole role = FrontedNodePortRole.Default)
    {
        if (role == FrontedNodePortRole.ParallelContinuation)
        {
            return "#8BC34A";
        }

        if (descriptor.PortKind is FrontedNodePortKind.FlowIn or FrontedNodePortKind.FlowOut)
            return "#1976D2"; // 蓝色（更饱和，与 Control 明显区分）

        return descriptor.ValueType switch
        {
            FrontedNodePortValueType.Number => "#43A047",  // 绿色
            FrontedNodePortValueType.String => "#8E24AA",  // 紫色
            FrontedNodePortValueType.Boolean => "#FB8C00", // 橙色
            FrontedNodePortValueType.Color => "#E53935",   // 红色（从粉色改为红色，与 String 区分）
            FrontedNodePortValueType.Control => "#00897B", // 青绿色（从青色改为青绿，与 Flow 明显区分）
            FrontedNodePortValueType.Object => "#757575",  // 灰色
            _ => "#757575"                                  // 灰色（未知类型）
        };
    }

    private string GetDisplayName(FrontedNodePortDescriptor descriptor)
    {
        if (Role == FrontedNodePortRole.ParallelContinuation)
        {
            return Localize("Designer.Graph.Port.ParallelOut", "全部完成后");
        }

        if (Role == FrontedNodePortRole.ParallelBranch
            && FrontedParallelNodePorts.TryGetBranchIndex(descriptor.Name, out var branchIndex))
        {
            return string.Format(Localize("Designer.Graph.Port.ParallelBranch", "分支 {0}"), branchIndex);
        }

        return Localize(descriptor.DisplayNameKey, descriptor.Name);
    }

    private string GetTooltipText()
    {
        if (Node.Model.NodeType != "flow.parallel")
        {
            return Localize("Designer.Graph.Tooltip.CompatibleHint", "Drag to a compatible port to connect.");
        }

        if (Name == "Out")
        {
            return Localize("Designer.Graph.Port.ParallelOut.Tooltip", "所有已连接的并行分支执行完成后，从这里继续。");
        }

        return FrontedParallelNodePorts.TryGetBranchIndex(Name, out var branchIndex)
            ? string.Format(Localize("Designer.Graph.Port.ParallelBranch.Tooltip", "并行分支 {0}。此分支会和其他分支同时执行。"), branchIndex)
            : Localize("Designer.Graph.Tooltip.CompatibleHint", "Drag to a compatible port to connect.");
    }

    private string GetMeaning()
    {
        if (Node.Model.NodeType == "flow.parallel")
        {
            if (Name == "Out")
            {
                return Localize("Designer.Graph.Connection.Meaning.ParallelOut", "所有并行分支完成后继续");
            }

            if (FrontedParallelNodePorts.TryGetBranchIndex(Name, out var branchIndex))
            {
                return string.Format(Localize("Designer.Graph.Connection.Meaning.ParallelBranch", "并行分支 {0}"), branchIndex);
            }
        }

        return DisplayName;
    }

    private static FrontedNodePortRole GetRole(string nodeType, string portName)
    {
        if (nodeType != "flow.parallel")
        {
            return FrontedNodePortRole.Default;
        }

        if (portName == "Out")
        {
            return FrontedNodePortRole.ParallelContinuation;
        }

        return FrontedParallelNodePorts.TryGetBranchIndex(portName, out _)
            ? FrontedNodePortRole.ParallelBranch
            : FrontedNodePortRole.Default;
    }

    private string? GetValueTypeDisplayName(string? valueType)
    {
        if (valueType is null) return null;
        var key = valueType switch
        {
            FrontedNodePortValueType.Number => "Designer.Graph.Port.ValueType.Number",
            FrontedNodePortValueType.String => "Designer.Graph.Port.ValueType.String",
            FrontedNodePortValueType.Boolean => "Designer.Graph.Port.ValueType.Boolean",
            FrontedNodePortValueType.Color => "Designer.Graph.Port.ValueType.Color",
            FrontedNodePortValueType.Control => "Designer.Graph.Port.ValueType.Control",
            FrontedNodePortValueType.Object => "Designer.Graph.Port.ValueType.Object",
            _ => null
        };
        return key is not null ? Localize(key, valueType) : valueType;
    }

    private string Localize(string key, string fallback) =>
        _localize?.Invoke(key, fallback) ?? fallback;
}
