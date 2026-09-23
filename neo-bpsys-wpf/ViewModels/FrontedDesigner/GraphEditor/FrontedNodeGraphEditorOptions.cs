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

public sealed record FrontedNodeTargetOptionViewModel(string Value, string DisplayName);

/// <summary>
/// 上下文感知图条件编辑器可用的事件 payload 字段。
/// </summary>
/// <param name="ValuePath">图中持久化的稳定条件路径。</param>
/// <param name="DisplayText">面向用户的本地化字段标签加稳定路径。</param>
/// <param name="Description">本地化字段描述。</param>
/// <param name="TypeName">负载值类型名称。</param>
/// <param name="EnumValues">字段接受的稳定枚举名称。</param>
/// <param name="EventType">贡献该字段的事件类型（用于消歧）。</param>
/// <param name="LocalizedDisplayName">不含稳定路径的本地化字段标签。</param>
public sealed record FrontedGraphConditionFieldOptionViewModel(
    string ValuePath,
    string DisplayText,
    string Description,
    string TypeName,
    IReadOnlyList<string> EnumValues,
    string? EventType,
    string LocalizedDisplayName);

/// <summary>
/// 节点属性编辑器显示的选项，同时保留稳定的存储值。
/// </summary>
/// <param name="Value">存储在节点 JSON 中的值。</param>
/// <param name="DisplayName">向用户显示的本地化选项标签。</param>
public sealed record FrontedNodePropertyOptionViewModel(string Value, string DisplayName)
{
    /// <inheritdoc />
    public override string ToString() => DisplayName;
}

public sealed class FrontedNodeCatalogItemViewModel
{
    public FrontedNodeCatalogItemViewModel(FrontedNodeTypeDescriptor descriptor, Func<string, string, string> localize)
    {
        Descriptor = descriptor;
        DisplayName = localize(descriptor.DisplayNameKey, descriptor.NodeType.Split('.').Last());
        Description = localize(descriptor.DescriptionKey, descriptor.NodeType);
        Category = localize($"Designer.Graph.Category.{descriptor.Category}", descriptor.Category);
    }

    public FrontedNodeTypeDescriptor Descriptor { get; }
    public string NodeType => Descriptor.NodeType;
    public string DisplayName { get; }
    public string Description { get; }
    public string Category { get; }
}

/// <summary>
/// 节点目录中的一个用途分组。
/// </summary>
public sealed class FrontedNodeCatalogGroupViewModel
{
    /// <summary>
    /// 初始化节点目录分组。
    /// </summary>
    /// <param name="displayName">显示给用户的分组名称。</param>
    /// <param name="items">分组内的节点目录项。</param>
    public FrontedNodeCatalogGroupViewModel(
        string displayName,
        IReadOnlyList<FrontedNodeCatalogItemViewModel> items)
    {
        DisplayName = displayName;
        Items = items;
    }

    /// <summary>
    /// 获取显示给用户的分组名称。
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// 获取分组内的节点目录项。
    /// </summary>
    public IReadOnlyList<FrontedNodeCatalogItemViewModel> Items { get; }
}
