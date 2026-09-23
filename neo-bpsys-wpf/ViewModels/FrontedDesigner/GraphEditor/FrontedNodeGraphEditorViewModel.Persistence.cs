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

/// <summary>
/// 节点图编辑器的Persistence逻辑。
/// </summary>
public sealed partial class FrontedNodeGraphEditorViewModel
{
    /// <summary>
    /// 根据当前图模型重建节点和连接视图模型。
    /// </summary>
    private void Reload()
    {
        Nodes.Clear();
        foreach (var node in Graph.Nodes)
        {
            Nodes.Add(CreateNode(node));
        }
        ReloadConnections();
        UpdateCanvasSize();
    }

    /// <summary>
    /// 为单个图节点创建编辑器视图模型。
    /// </summary>
    /// <param name="node">图节点模型。</param>
    /// <returns>节点编辑器视图模型。</returns>
    private FrontedNodeEditorViewModel CreateNode(FrontedNode node) =>
        new(node, _catalog.Find(node.NodeType), MarkDirtyAndSetIsDirty, ValidateGraph, RefreshParallelNode, _localize, _targetOptions, _conditionFieldOptions,
            (targetNode, port) => Graph.Connections.Any(connection => connection.TargetNodeId == targetNode.NodeId && connection.TargetPort == port));

    /// <summary>
    /// 并行节点分支数变化后重建该节点，并移除指向已删除分支端口的连接。
    /// </summary>
    /// <param name="node">发生变化的并行节点模型。</param>
    private void RefreshParallelNode(FrontedNode node)
    {
        var branchCount = FrontedParallelNodePorts.GetBranchCount(node);
        Graph.Connections.RemoveAll(connection =>
            connection.SourceNodeId == node.NodeId
            && FrontedParallelNodePorts.TryGetBranchIndex(connection.SourcePort, out var branchIndex)
            && branchIndex > branchCount);

        var wasSelected = SelectedNode?.Model.NodeId == node.NodeId;
        Reload();
        if (wasSelected)
        {
            ClearIsSelected();
            SelectedNodes.Clear();
            SelectedNode = Nodes.FirstOrDefault(item => item.Model.NodeId == node.NodeId);
            if (SelectedNode is not null)
            {
                SelectedNode.IsSelected = true;
                SelectedNodes.Add(SelectedNode);
            }
        }
        ValidateGraph();
    }

    /// <summary>
    /// 将父级行为文档和当前图编辑器都标记为已修改。
    /// </summary>
    private void MarkDirtyAndSetIsDirty()
    {
        _markDirty();
        IsDirty = true;
    }

    /// <summary>
    /// 根据图连接列表重建连接视图模型。
    /// </summary>
    private void ReloadConnections()
    {
        Connections.Clear();
        foreach (var model in Graph.Connections)
        {
            var source = Nodes.FirstOrDefault(node => node.Model.NodeId == model.SourceNodeId);
            var target = Nodes.FirstOrDefault(node => node.Model.NodeId == model.TargetNodeId);
            if (source is not null && target is not null)
            {
                Connections.Add(new FrontedNodeConnectionViewModel(model, source, target, _localize));
            }
        }
        RefreshPortConnectionStates();
    }

    /// <summary>
    /// 处理已提交的图变更：标记已修改状态、执行校验，并刷新过滤后的目录状态。
    /// </summary>
    private void Changed()
    {
        _markDirty();
        IsDirty = true;
        ValidateGraph();
        OnPropertyChanged(nameof(FilteredCatalog));
    }

    /// <summary>
    /// 扩展虚拟图画布，确保靠近边缘的节点仍可访问。
    /// </summary>
    private void UpdateCanvasSize()
    {
        CanvasWidth = Math.Max(2200, Nodes.Select(node => node.X).DefaultIfEmpty(0).Max() + 520);
        CanvasHeight = Math.Max(1400, Nodes.Select(node => node.Y + node.CardHeight).DefaultIfEmpty(0).Max() + 160);
    }

    /// <summary>创建当前图快照（用于撤销）</summary>
    private void CreateSnapshot()
    {
        if (_isRestoring || _isDragging)
        {
            return;
        }

        _captureUndoSnapshot();

        if (_undoStack.Count >= UndoStackLimit)
        {
            // 限制撤销栈大小：移除最旧的条目
            var items = _undoStack.ToArray();
            _undoStack.Clear();
            for (var i = items.Length - 1; i > 0; i--)
            {
                _undoStack.Push(items[i]);
            }
        }

        _undoStack.Push(JsonSerializer.Serialize(Graph));
        _redoStack.Clear();
        CanUndo = true;
        CanRedo = false;
    }

    /// <summary>撤销</summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    public void Undo()
    {
        if (_undoStack.Count == 0)
        {
            return;
        }
        _redoStack.Push(JsonSerializer.Serialize(Graph));
        var snapshot = JsonSerializer.Deserialize<FrontedNodeGraph>(_undoStack.Pop());
        _isRestoring = true;
        RestoreGraph(snapshot);
        _isRestoring = false;
        CanUndo = _undoStack.Count > 0;
        CanRedo = _redoStack.Count > 0;
    }

    /// <summary>重做</summary>
    [RelayCommand(CanExecute = nameof(CanRedo))]
    public void Redo()
    {
        if (_redoStack.Count == 0)
        {
            return;
        }
        _undoStack.Push(JsonSerializer.Serialize(Graph));
        var snapshot = JsonSerializer.Deserialize<FrontedNodeGraph>(_redoStack.Pop());
        _isRestoring = true;
        RestoreGraph(snapshot);
        _isRestoring = false;
        CanUndo = _undoStack.Count > 0;
        CanRedo = _redoStack.Count > 0;
    }

    /// <summary>保存当前图</summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    public async Task SaveAsync()
    {
        if (_saveAsync is null)
        {
            IsDirty = false;
            return;
        }

        if (await _saveAsync())
        {
            IsDirty = false;
        }
    }

    /// <summary>
    /// 设置调用 <see cref="SaveAsync"/> 时要执行的保存动作。
    /// 这允许在构造后接入保存委托（例如从动画编辑器接入）。
    /// </summary>
    /// <param name="saveAsync">要设置的异步保存动作。</param>
    public void SetSaveAction(Func<Task<bool>>? saveAsync)
    {
        _saveAsync = saveAsync;
    }

    /// <summary>
    /// 在不保存图的情况下清除编辑器本地已修改状态。
    /// </summary>
    public void DiscardLocalDirtyState()
    {
        IsDirty = false;
    }

    private bool CanSave() => IsDirty;

    private void RestoreGraph(FrontedNodeGraph? snapshot)
    {
        if (snapshot is null)
        {
            return;
        }
        Graph.Nodes.Clear();
        Graph.Nodes.AddRange(snapshot.Nodes);
        Graph.Connections.Clear();
        Graph.Connections.AddRange(snapshot.Connections);
        RemoveDuplicateSingletonFlowNodes(Graph);
        Reload();
        ClearIsSelected();
        SelectedNodes.Clear();
        SelectedNode = null;
        Changed();
    }

    private static bool TryNormalizeConnection(
        FrontedNodePortViewModel first,
        FrontedNodePortViewModel second,
        out FrontedNodePortViewModel source,
        out FrontedNodePortViewModel target)
    {
        source = first;
        target = second;
        if (FrontedNodePortViewModel.ArePortsCompatible(first.Descriptor, second.Descriptor))
        {
            return first.Node != second.Node;
        }

        source = second;
        target = first;
        return first.Node != second.Node
               && FrontedNodePortViewModel.ArePortsCompatible(second.Descriptor, first.Descriptor);
    }

    private bool IsTargetAvailable(FrontedNodePortViewModel source, FrontedNodePortViewModel target) =>
        IsEndInputPort(target)
        || Graph.GetIncoming(target.Node.Model.NodeId, target.Descriptor.Name)
            .All(connection => connection.SourceNodeId == source.Node.Model.NodeId
                               && string.Equals(connection.SourcePort, source.Descriptor.Name, StringComparison.Ordinal));

    private bool CanAddNodeType(string nodeType) =>
        !IsSingletonFlowNodeType(nodeType) || !HasNodeType(nodeType);

    private bool HasNodeType(string nodeType) =>
        Graph.Nodes.Any(node => node.NodeType == nodeType);

    private static bool IsEndInputPort(FrontedNodePortViewModel port) =>
        port.Node.Model.NodeType == "flow.end"
        && string.Equals(port.Descriptor.Name, "In", StringComparison.Ordinal);

    private void RemovePastedDuplicateSingletonFlowNodes(List<FrontedNode> nodes)
    {
        foreach (var nodeType in SingletonFlowNodeTypes)
        {
            var hasNode = HasNodeType(nodeType);
            nodes.RemoveAll(node =>
            {
                if (node.NodeType != nodeType)
                {
                    return false;
                }

                if (hasNode)
                {
                    return true;
                }

                hasNode = true;
                return false;
            });
        }
    }

    private static bool RemoveDuplicateSingletonFlowNodes(FrontedNodeGraph graph)
    {
        var removedNodeIds = new HashSet<Guid>();
        foreach (var nodeType in SingletonFlowNodeTypes)
        {
            var first = true;
            foreach (var node in graph.Nodes.Where(node => node.NodeType == nodeType).ToArray())
            {
                if (first)
                {
                    first = false;
                    continue;
                }

                removedNodeIds.Add(node.NodeId);
                graph.Nodes.Remove(node);
            }
        }

        if (removedNodeIds.Count == 0)
        {
            return false;
        }

        graph.Connections.RemoveAll(connection =>
            removedNodeIds.Contains(connection.SourceNodeId)
            || removedNodeIds.Contains(connection.TargetNodeId));
        return true;
    }

    private static bool IsSingletonFlowNodeType(string nodeType) =>
        SingletonFlowNodeTypes.Contains(nodeType, StringComparer.Ordinal);

    private static readonly string[] SingletonFlowNodeTypes = ["flow.start", "flow.end"];

    private static List<T> DeepClone<T>(IReadOnlyCollection<T> values) =>
        JsonSerializer.Deserialize<List<T>>(JsonSerializer.Serialize(values)) ?? [];

    private sealed class AnimationRuntimeGraphActionExecutor(
        IFrontedAnimationRuntime animationRuntime,
        FrontedAnimationExecutionContext animationContext) : IFrontedGraphActionExecutor
    {
        public Task ExecuteAsync(FrontedGraphActionRequest request, CancellationToken cancellationToken) =>
            animationRuntime.ExecuteAsync(request, animationContext, cancellationToken);
    }
}
