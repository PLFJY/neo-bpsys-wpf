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

public sealed partial class FrontedNodeEditorViewModel : ObservableObject
{
    public const double Width = 190;
    private const double ParallelWidth = 230;
    private readonly Action _markDirty;
    private readonly Action _validate;

    public FrontedNodeEditorViewModel(
        FrontedNode model,
        FrontedNodeTypeDescriptor? descriptor,
        Action markDirty,
        Action validate,
        Action<FrontedNode> refreshParallelNode,
        Func<string, string, string> localize,
        IReadOnlyList<FrontedNodeTargetOptionViewModel> targetOptions,
        IReadOnlyList<FrontedGraphConditionFieldOptionViewModel> conditionFieldOptions,
        Func<FrontedNode, string, bool> hasIncomingConnection)
    {
        Model = model;
        Descriptor = descriptor;
        _markDirty = markDirty;
        _validate = validate;
        DisplayName = descriptor is null ? model.NodeType : localize(descriptor.DisplayNameKey, NodeFallback(model.NodeType));
        Description = descriptor is null ? model.NodeType : localize(descriptor.DescriptionKey, model.NodeType);
        CardWidth = model.NodeType == "flow.parallel" ? ParallelWidth : Width;
        InputPorts = CreatePorts(descriptor?.InputPorts, localize);
        var outputDescriptors = descriptor?.OutputPorts;
        if (model.NodeType == "flow.parallel" && outputDescriptors is not null)
        {
            var branchCount = FrontedParallelNodePorts.GetBranchCount(model);
            outputDescriptors = outputDescriptors
                .Where(port => !FrontedParallelNodePorts.TryGetBranchIndex(port.Name, out var branchIndex) || branchIndex <= branchCount)
                .ToArray();
        }
        OutputPorts = CreatePorts(outputDescriptors, localize);
        var properties = descriptor?.Properties
            .Select(property => new FrontedNodePropertyEditorViewModel(model, property, markDirty, validate, localize, targetOptions, conditionFieldOptions, hasIncomingConnection))
            .ToArray() ?? [];
        Properties = properties;
        ConditionFieldOptions = conditionFieldOptions;
        _localize = localize;
        foreach (var property in properties)
        {
            property.SetRefreshRelatedProperties(() =>
            {
                foreach (var item in properties)
                {
                    item.RefreshEditorState();
                }
                OnPropertyChanged(nameof(Summary));
                OnPropertyChanged(nameof(RawSummary));
                OnPropertyChanged(nameof(HeaderText));
                if (property.Descriptor.Name == "BranchCount" && Model.NodeType == "flow.parallel")
                {
                    refreshParallelNode(Model);
                }
            });
        }
    }

    public FrontedNode Model { get; }
    public FrontedNodeTypeDescriptor? Descriptor { get; }
    public string DisplayName { get; }
    public string Description { get; }
    /// <summary>获取带有用户可编辑表达式节点的可读摘要。</summary>
    private readonly IReadOnlyList<FrontedGraphConditionFieldOptionViewModel> ConditionFieldOptions;
    private readonly Func<string, string, string> _localize;
    public string Summary => Model.NodeType == "flow.if"
        ? $"{_localize("Designer.Graph.Condition.If", "IF")} {ConditionFieldDisplayName(ReadProperty("Left"))} {OperatorSymbol(ReadProperty("Operator"))} {ReadProperty("Right")}".TrimEnd()
        : string.Empty;
    /// <summary>获取节点工具提示中显示的稳定原始表达式。</summary>
    public string RawSummary => Model.NodeType == "flow.if"
        ? $"{ReadProperty("Left")} {ReadProperty("Operator")} {ReadProperty("Right")}".TrimEnd()
        : Description;
    /// <summary>获取节点是否拥有可读表达式摘要。</summary>
    public bool HasSummary => !string.IsNullOrWhiteSpace(Summary);
    /// <summary>获取节点卡片标题文本。</summary>
    public string HeaderText => HasSummary ? Summary : DisplayName;
    /// <summary>获取节点卡片渲染宽度。</summary>
    public double CardWidth { get; }
    /// <summary>获取用于画布边界和选择计算的节点卡片近似渲染高度。</summary>
    public double CardHeight => Math.Max(
        80D,
        InputPorts.Concat(OutputPorts).Select(port => port.CenterOffsetY).DefaultIfEmpty(56D).Max() + 24D);
    public IReadOnlyList<FrontedNodePortViewModel> InputPorts { get; }
    public IReadOnlyList<FrontedNodePortViewModel> OutputPorts { get; }
    public IReadOnlyList<FrontedNodePropertyEditorViewModel> Properties { get; }

    /// <summary>刷新属性编辑器的外部值输入状态。</summary>
    public void RefreshExternalInputStates()
    {
        foreach (var property in Properties)
        {
            property.RefreshExternalInputState();
        }
    }

    public double X
    {
        get => Model.X;
        set => SetProperty(Model.X, value, Model, static (model, next) => model.X = next);
    }

    public double Y
    {
        get => Model.Y;
        set => SetProperty(Model.Y, value, Model, static (model, next) => model.Y = next);
    }

    [ObservableProperty]
    private bool _isSelected;

    private IReadOnlyList<FrontedNodePortViewModel> CreatePorts(
        IReadOnlyList<FrontedNodePortDescriptor>? descriptors,
        Func<string, string, string> localize)
    {
        if (descriptors is null || descriptors.Count == 0)
        {
            return [];
        }

        var ports = new List<FrontedNodePortViewModel>(descriptors.Count);
        var accumulatedExtraSpacing = 0D;
        for (var i = 0; i < descriptors.Count; i++)
        {
            var descriptor = descriptors[i];
            var beforeSpacing = IsParallelContinuationPort(descriptor) ? 14D : 0D;
            accumulatedExtraSpacing += beforeSpacing;
            ports.Add(new FrontedNodePortViewModel(this, descriptor, i, accumulatedExtraSpacing, beforeSpacing, localize));
        }

        return ports;
    }

    private bool IsParallelContinuationPort(FrontedNodePortDescriptor descriptor) =>
        Model.NodeType == "flow.parallel"
        && string.Equals(descriptor.Name, "Out", StringComparison.Ordinal);

    private static string NodeFallback(string nodeType) => nodeType.Split('.').LastOrDefault() ?? nodeType;

    private string ReadProperty(string name) =>
        Model.Properties.TryGetValue(name, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString()
            : string.Empty;

    private string ConditionFieldDisplayName(string path) =>
        ConditionFieldOptions.FirstOrDefault(option => string.Equals(option.ValuePath, path, StringComparison.Ordinal))?.LocalizedDisplayName
        ?? path;

    private static string OperatorSymbol(string value) =>
        Enum.TryParse<TriggerFilterOperator>(value, out var op)
            ? op switch
            {
                TriggerFilterOperator.Equals => "==",
                TriggerFilterOperator.NotEquals => "!=",
                TriggerFilterOperator.GreaterThan => ">",
                TriggerFilterOperator.GreaterThanOrEqual => ">=",
                TriggerFilterOperator.LessThan => "<",
                TriggerFilterOperator.LessThanOrEqual => "<=",
                TriggerFilterOperator.Contains => "contains",
                TriggerFilterOperator.NotContains => "not contains",
                TriggerFilterOperator.Exists => "exists",
                _ => value
            }
            : value;
}

/// <summary>
/// 描述节点端口仅供编辑器使用的语义角色。
/// </summary>
public enum FrontedNodePortRole
{
    /// <summary>没有特殊图语义的普通端口。</summary>
    Default,

    /// <summary>flow.parallel 节点上的分支输出端口。</summary>
    ParallelBranch,

    /// <summary>所有已连接 flow.parallel 分支完成后运行的继续输出端口。</summary>
    ParallelContinuation
}
