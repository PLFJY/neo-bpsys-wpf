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
/// 用于编辑 Designer v3 行为节点图的视图模型。
/// </summary>
public sealed partial class FrontedNodeGraphEditorViewModel : ObservableObject
{
    private readonly FrontedNodeCatalog _catalog;
    private readonly FrontedNodeGraphValidator _validator;
    private readonly IFrontedNodeGraphRuntime _runtime;
    private readonly IFrontedAnimationRuntime? _animationRuntime;
    private readonly Func<FrontedAnimationExecutionContext?>? _createAnimationContext;
    private readonly Action _markDirty;
    private readonly Action _captureUndoSnapshot;
    private readonly Func<string, string, string> _localize;
    private readonly IReadOnlyList<FrontedNodeTargetOptionViewModel> _targetOptions;
    private readonly IReadOnlyList<FrontedGraphConditionFieldOptionViewModel> _conditionFieldOptions;
    private CancellationTokenSource? _previewCancellation;
    private FrontedNodePortViewModel? _pendingPort;
    private readonly Stack<string> _undoStack = new();
    private readonly Stack<string> _redoStack = new();
    private bool _isRestoring;
    private bool _isDragging;
    private const int UndoStackLimit = 50;
    private Func<Task<bool>>? _saveAsync;

    /// <summary>
    /// 为已有图模型初始化新的节点图编辑器。
    /// </summary>
    /// <param name="graph">正在编辑的图模型。</param>
    /// <param name="catalog">可选的节点目录覆盖。</param>
    /// <param name="validator">可选的图校验器覆盖。</param>
    /// <param name="runtime">可选的图运行时覆盖。</param>
    /// <param name="animationRuntime">预览操作使用的可选动画运行时。</param>
    /// <param name="createAnimationContext">动画预览上下文工厂。</param>
    /// <param name="markDirty">图变化时调用的回调。</param>
    /// <param name="localize">本地化回调。</param>
    /// <param name="save">同步保存兜底。</param>
    /// <param name="saveAsync">异步保存回调。</param>
    /// <param name="captureUndoSnapshot">捕获父级设计器撤销快照的回调。</param>
    /// <param name="targetOptions">动作节点可用的动画目标选项。</param>
    /// <param name="conditionFieldOptions">分支节点可用的条件字段选项。</param>
    public FrontedNodeGraphEditorViewModel(
        FrontedNodeGraph graph,
        FrontedNodeCatalog? catalog = null,
        FrontedNodeGraphValidator? validator = null,
        IFrontedNodeGraphRuntime? runtime = null,
        IFrontedAnimationRuntime? animationRuntime = null,
        Func<FrontedAnimationExecutionContext?>? createAnimationContext = null,
        Action? markDirty = null,
        Func<string, string, string>? localize = null,
        Action? save = null,
        Func<Task<bool>>? saveAsync = null,
        Action? captureUndoSnapshot = null,
        IReadOnlyList<FrontedNodeTargetOptionViewModel>? targetOptions = null,
        IReadOnlyList<FrontedGraphConditionFieldOptionViewModel>? conditionFieldOptions = null)
    {
        Graph = graph;
        _catalog = catalog ?? new FrontedNodeCatalog();
        _validator = validator ?? new FrontedNodeGraphValidator(_catalog);
        _runtime = runtime ?? new FrontedNodeGraphRuntime(_catalog, _validator);
        _animationRuntime = animationRuntime;
        _createAnimationContext = createAnimationContext;
        _markDirty = markDirty ?? (() => { });
        _captureUndoSnapshot = captureUndoSnapshot ?? (() => { });
        _localize = localize ?? ((_, fallback) => fallback);
        _saveAsync = saveAsync ?? (save is null
            ? (() => Task.FromResult(true))
            : (() =>
            {
                save();
                return Task.FromResult(true);
            }));
        _targetOptions = targetOptions ?? [new FrontedNodeTargetOptionViewModel("Self", _localize("Designer.Graph.Target.Self", "Self"))];
        _conditionFieldOptions = conditionFieldOptions ?? [];
        Catalog = _catalog.Nodes
            .Select(descriptor => new FrontedNodeCatalogItemViewModel(descriptor, _localize))
            .ToArray();
        if (RemoveDuplicateSingletonFlowNodes(Graph))
        {
            _markDirty();
            IsDirty = true;
        }
        Reload();
        ValidateGraph();
    }

    /// <summary>
    /// 获取正在编辑的图模型。
    /// </summary>
    public FrontedNodeGraph Graph { get; }

    /// <summary>
    /// 获取当前显示在画布上的节点视图模型。
    /// </summary>
    public ObservableCollection<FrontedNodeEditorViewModel> Nodes { get; } = [];

    /// <summary>
    /// 获取当前多选集合。
    /// </summary>
    public ObservableCollection<FrontedNodeEditorViewModel> SelectedNodes { get; } = [];

    /// <summary>
    /// 获取当前显示在画布上的连接视图模型。
    /// </summary>
    public ObservableCollection<FrontedNodeConnectionViewModel> Connections { get; } = [];

    /// <summary>
    /// 获取当前图的校验消息。
    /// </summary>
    public ObservableCollection<FrontedNodeGraphValidationMessage> ValidationMessages { get; } = [];

    /// <summary>
    /// 获取预览执行日志项。
    /// </summary>
    public ObservableCollection<FrontedGraphExecutionLogItem> ExecutionLog { get; } = [];

    /// <summary>
    /// 获取所有可插入的节点目录项。
    /// </summary>
    public IReadOnlyList<FrontedNodeCatalogItemViewModel> Catalog { get; }

    [ObservableProperty]
    public partial FrontedNodeEditorViewModel? SelectedNode { get; set; }

    /// <summary>是否可以撤销</summary>
    [ObservableProperty]
    public partial bool CanUndo { get; set; }

    /// <summary>是否可以重做</summary>
    [ObservableProperty]
    public partial bool CanRedo { get; set; }

    /// <summary>是否有未保存的更改</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsDirty { get; set; }

    [ObservableProperty]
    public partial string CatalogSearchText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionStatus))]
    private bool _isConnecting;

    [ObservableProperty]
    private bool _isPreviewRunning;

    [ObservableProperty]
    private System.Windows.FrameworkElement? _previewRoot;

    [ObservableProperty]
    private double _canvasWidth = 2200;

    [ObservableProperty]
    private double _canvasHeight = 1400;

    /// <summary>
    /// 获取按当前搜索文本过滤后的目录项。
    /// </summary>
    public IEnumerable<FrontedNodeCatalogItemViewModel> FilteredCatalog =>
        string.IsNullOrWhiteSpace(CatalogSearchText)
            ? Catalog
            : Catalog.Where(item => item.DisplayName.Contains(CatalogSearchText, StringComparison.OrdinalIgnoreCase)
                                    || item.Category.Contains(CatalogSearchText, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 获取按行为与数据用途分组、且已应用搜索条件的节点目录。
    /// </summary>
    public IEnumerable<FrontedNodeCatalogGroupViewModel> GroupedCatalog
    {
        get
        {
            var items = FilteredCatalog.ToArray();
            var behaviorNodes = items
                .Where(item => !string.Equals(item.Descriptor.Category, "Value", StringComparison.Ordinal))
                .ToArray();
            if (behaviorNodes.Length > 0)
            {
                yield return new FrontedNodeCatalogGroupViewModel(
                    _localize("Designer.Graph.Group.Behavior", "Behavior nodes"),
                    behaviorNodes);
            }

            var dataNodes = items
                .Where(item => string.Equals(item.Descriptor.Category, "Value", StringComparison.Ordinal))
                .ToArray();
            if (dataNodes.Length > 0)
            {
                yield return new FrontedNodeCatalogGroupViewModel(
                    _localize("Designer.Graph.Group.Data", "Data nodes"),
                    dataNodes);
            }
        }
    }

    /// <summary>
    /// 获取当前连接模式状态文本。
    /// </summary>
    public string ConnectionStatus => IsConnecting
        ? _localize("Designer.Graph.Connection.SelectInput", "Select an input port.")
        : string.Empty;

    partial void OnCatalogSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredCatalog));
        OnPropertyChanged(nameof(GroupedCatalog));
    }

    /// <summary>
    /// 当选中节点变化时，若选中了动画属性节点则发布教程信号。
    /// 信号在无教程等待时为空操作，因此可安全地在任意上下文触发。
    /// </summary>
    /// <param name="value">新选中的节点视图模型；为 <see langword="null"/> 时不发布信号。</param>
    partial void OnSelectedNodeChanged(FrontedNodeEditorViewModel? value)
    {
        if (value is not null
            && string.Equals(value.Model.NodeType, "action.animateProperty", StringComparison.Ordinal))
        {
            TutorialSignalPublisher.Publish(TutorialSignalIds.AnimationPropertyNodeSelected);
        }
    }

    /// <summary>
    /// 在默认插入位置附近添加目录中的节点。
    /// </summary>
    /// <param name="nodeType">节点类型标识。</param>
    [RelayCommand]
    public void AddNode(string? nodeType)
    {
        var nodeCount = Nodes.Count;
        AddNodeAt(nodeType, 40 + Nodes.Count * 20, 40 + Nodes.Count * 20);
        if (Nodes.Count == nodeCount)
        {
            return;
        }

        SelectedNodes.Clear();
        if (Nodes.Count > 0)
        {
            SelectedNodes.Add(Nodes[^1]);
        }
    }

    /// <summary>
    /// 在指定图画布位置添加目录中的节点。
    /// </summary>
    /// <param name="nodeType">节点类型标识。</param>
    /// <param name="x">画布 X 坐标。</param>
    /// <param name="y">画布 Y 坐标。</param>
    public void AddNodeAt(string? nodeType, double x, double y)
    {
        if (string.IsNullOrWhiteSpace(nodeType)
            || _catalog.Find(nodeType) is null
            || !CanAddNodeType(nodeType))
        {
            return;
        }

        CreateSnapshot();
        var model = _catalog.CreateNode(nodeType, Math.Max(0, x), Math.Max(0, y));
        Graph.Nodes.Add(model);
        var viewModel = CreateNode(model);
        Nodes.Add(viewModel);
        ClearIsSelected();
        SelectedNodes.Clear();
        SelectedNodes.Add(viewModel);
        viewModel.IsSelected = true;
        SelectedNode = viewModel;
        UpdateCanvasSize();
        Changed();
    }

    /// <summary>
    /// 在图为空时创建最小的开始到结束图。
    /// </summary>
    [RelayCommand]
    public void AutoCreateStartEnd()
    {
        if (Graph.Nodes.Count > 0)
        {
            return;
        }

        CreateSnapshot();
        var start = _catalog.CreateNode("flow.start", 60, 100);
        var end = _catalog.CreateNode("flow.end", 360, 100);
        Graph.Nodes.AddRange([start, end]);
        Graph.Connections.Add(new FrontedNodeConnection { SourceNodeId = start.NodeId, SourcePort = "Out", TargetNodeId = end.NodeId, TargetPort = "In" });
        Reload();
        UpdateCanvasSize();
        Changed();
    }

    /// <summary>
    /// 删除选中节点或多选节点。
    /// </summary>
    [RelayCommand]
    public void DeleteSelectedNode()
    {
        if (SelectedNodes.Count > 0)
        {
            DeleteNodes([.. SelectedNodes]);
        }
        else
        {
            DeleteNode(SelectedNode);
        }
    }

    /// <summary>
    /// 删除多个节点及其附加连接。
    /// </summary>
    /// <param name="nodes">要删除的节点视图模型。</param>
    public void DeleteNodes(IReadOnlyList<FrontedNodeEditorViewModel> nodes)
    {
        if (nodes.Count == 0)
        {
            return;
        }

        CreateSnapshot();
        foreach (var node in nodes)
        {
            if (Graph.RemoveNode(node.Model.NodeId))
            {
                Nodes.Remove(node);
            }
        }
        ClearIsSelected();
        SelectedNodes.Clear();
        SelectedNode = null;
        ReloadConnections();
        UpdateCanvasSize();
        Changed();
    }

    /// <summary>
    /// 删除单个节点及其附加连接。
    /// </summary>
    /// <param name="node">要删除的节点视图模型。</param>
    public void DeleteNode(FrontedNodeEditorViewModel? node)
    {
        if (node is null || !Graph.RemoveNode(node.Model.NodeId))
        {
            return;
        }

        Nodes.Remove(node);
        SelectedNodes.Remove(node);
        if (SelectedNode == node)
        {
            SelectedNode = null;
        }
        ReloadConnections();
        UpdateCanvasSize();
        Changed();
    }

    /// <summary>
    /// 复制选中节点或多选节点。
    /// </summary>
    [RelayCommand]
    public void DuplicateSelectedNode()
    {
        var nodesToClone = SelectedNodes.Count > 0
            ? SelectedNodes.ToList()
            : SelectedNode is not null ? [SelectedNode] : [];
        if (nodesToClone.Count == 0)
        {
            return;
        }

        var models = new List<FrontedNode>();
        foreach (var node in nodesToClone)
        {
            if (!CanAddNodeType(node.Model.NodeType))
            {
                continue;
            }

            var source = node.Model;
            models.Add(new FrontedNode
            {
                NodeType = source.NodeType,
                DisplayName = source.DisplayName,
                X = source.X + 30,
                Y = source.Y + 30,
                Properties = source.Properties.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal)
            });
        }

        if (models.Count == 0)
        {
            return;
        }

        CreateSnapshot();
        var clones = new List<FrontedNodeEditorViewModel>();
        foreach (var clone in models)
        {
            Graph.Nodes.Add(clone);
            var viewModel = CreateNode(clone);
            Nodes.Add(viewModel);
            clones.Add(viewModel);
        }

        ClearIsSelected();
        SelectedNodes.Clear();
        foreach (var clone in clones)
        {
            SelectedNodes.Add(clone);
            clone.IsSelected = true;
        }
        SelectedNode = clones.FirstOrDefault();
        UpdateCanvasSize();
        Changed();
    }

    /// <summary>
    /// 将选中节点及其内部连接复制到应用级图剪贴板。
    /// </summary>
    [RelayCommand]
    public void CopySelectedNodes()
    {
        var selected = SelectedNodes.Count > 0
            ? SelectedNodes.Select(item => item.Model).ToArray()
            : SelectedNode is null ? [] : [SelectedNode.Model];
        if (selected.Length == 0)
        {
            return;
        }

        var selectedIds = selected.Select(node => node.NodeId).ToHashSet();
        FrontedNodeGraphClipboard.Payload = new FrontedNodeGraphClipboardPayload
        {
            Nodes = DeepClone(selected),
            Connections = DeepClone(Graph.Connections
                .Where(connection => selectedIds.Contains(connection.SourceNodeId)
                                     && selectedIds.Contains(connection.TargetNodeId))
                .ToArray())
        };
    }

    /// <summary>
    /// 从应用级图剪贴板粘贴节点，并重新映射所有复制出的标识。
    /// </summary>
    [RelayCommand]
    public void PasteNodes()
    {
        var payload = FrontedNodeGraphClipboard.Payload;
        if (payload is null || payload.Version != 1 || payload.Nodes.Count == 0)
        {
            return;
        }

        var nodes = DeepClone(payload.Nodes);
        RemovePastedDuplicateSingletonFlowNodes(nodes);

        if (nodes.Count == 0)
        {
            return;
        }

        CreateSnapshot();
        var copiedIds = nodes.Select(node => node.NodeId).ToHashSet();
        var connections = DeepClone(payload.Connections)
            .Where(connection => copiedIds.Contains(connection.SourceNodeId)
                                 && copiedIds.Contains(connection.TargetNodeId))
            .ToList();
        var idMap = nodes.ToDictionary(node => node.NodeId, _ => FrontedBehaviorGuidHelper.NewGuid());
        foreach (var node in nodes)
        {
            node.NodeId = idMap[node.NodeId];
            node.X += 32;
            node.Y += 32;
        }

        foreach (var connection in connections)
        {
            connection.ConnectionId = FrontedBehaviorGuidHelper.NewGuid();
            connection.SourceNodeId = idMap[connection.SourceNodeId];
            connection.TargetNodeId = idMap[connection.TargetNodeId];
        }

        Graph.Nodes.AddRange(nodes);
        Graph.Connections.AddRange(connections);
        Reload();
        ClearIsSelected();
        SelectedNodes.Clear();
        foreach (var node in Nodes.Where(item => nodes.Any(pasted => pasted.NodeId == item.Model.NodeId)))
        {
            node.IsSelected = true;
            SelectedNodes.Add(node);
        }
        SelectedNode = SelectedNodes.FirstOrDefault();
        Changed();
    }

    /// <summary>
    /// 选择单个节点，并在拖拽场景中保留多选状态。
    /// </summary>
    /// <param name="node">要选中的节点；传入 <see langword="null"/> 时清除选择。</param>
    [RelayCommand]
    public void SelectNode(FrontedNodeEditorViewModel? node)
    {
        SelectedNode = node;
        if (node is not null && !SelectedNodes.Contains(node))
        {
            // 点击不在多选中的节点：清空多选，单选该节点
            ClearIsSelected();
            SelectedNodes.Clear();
            SelectedNodes.Add(node);
            node.IsSelected = true;
        }
        else if (node is null)
        {
            ClearIsSelected();
            SelectedNodes.Clear();
        }
        // 如果 node 已在 SelectedNodes 中，保持多选（用于拖拽场景）
    }

    /// <summary>
    /// 通过更新可视选中标记预览框选结果，但不提交到 <see cref="SelectedNodes"/>。
    /// </summary>
    /// <param name="selectionRect">图画布坐标系中的选择矩形。</param>
    public void UpdateSelectionPreview(Rect selectionRect)
    {
        ClearIsSelected();
        foreach (var node in Nodes)
        {
            var nodeRect = new Rect(node.X, node.Y, node.CardWidth, node.CardHeight);
            if (selectionRect.IntersectsWith(nodeRect))
            {
                node.IsSelected = true;
            }
        }
    }

    /// <summary>
    /// 选中与选择矩形相交的所有节点。
    /// </summary>
    /// <param name="selectionRect">图画布坐标系中的选择矩形。</param>
    public void SelectNodes(Rect selectionRect)
    {
        ClearIsSelected();
        SelectedNodes.Clear();
        foreach (var node in Nodes)
        {
            var nodeRect = new Rect(node.X, node.Y, node.CardWidth, node.CardHeight);
            if (selectionRect.IntersectsWith(nodeRect))
            {
                SelectedNodes.Add(node);
                node.IsSelected = true;
            }
        }
        SelectedNode = SelectedNodes.FirstOrDefault();
    }

    /// <summary>
    /// 清除当前节点选择。
    /// </summary>
    [RelayCommand]
    public void DeselectAll()
    {
        ClearIsSelected();
        SelectedNode = null;
        SelectedNodes.Clear();
    }

    /// <summary>
    /// 清除每个节点的可视选中标记，但不改变已提交的选择集合。
    /// </summary>
    private void ClearIsSelected()
    {
        foreach (var node in Nodes)
        {
            node.IsSelected = false;
        }
    }

    /// <summary>
    /// 开始拖拽事务，并为整次拖拽创建一个撤销快照。
    /// </summary>
    public void BeginMoveNodes()
    {
        CreateSnapshot();
        _isDragging = true;
    }

    /// <summary>
    /// 移动节点；当被移动节点处于选中状态时移动整个多选集合。
    /// </summary>
    /// <param name="node">正在拖拽的节点。</param>
    /// <param name="x">拖拽节点的新 X 坐标。</param>
    /// <param name="y">拖拽节点的新 Y 坐标。</param>
    public void MoveNode(FrontedNodeEditorViewModel node, double x, double y)
    {
        var dx = x - node.X;
        var dy = y - node.Y;

        var nodesToMove = SelectedNodes.Contains(node) && SelectedNodes.Count > 1
            ? (IReadOnlyList<FrontedNodeEditorViewModel>)[.. SelectedNodes]
            : [node];

        foreach (var n in nodesToMove)
        {
            n.X = Math.Max(0, n.X + dx);
            n.Y = Math.Max(0, n.Y + dy);
            foreach (var connection in Connections.Where(connection => connection.Source == n || connection.Target == n))
            {
                connection.Refresh();
            }
        }
        UpdateCanvasSize();
        Changed();
    }

    /// <summary>
    /// 结束当前拖拽事务。
    /// </summary>
    public void EndMoveNodes()
    {
        _isDragging = false;
    }

    /// <summary>
    /// 从输出或输入端口开始一个待完成连接。
    /// </summary>
    /// <param name="port">连接手势开始的端口。</param>
    [RelayCommand]
    public void StartConnection(FrontedNodePortViewModel? port)
    {
        if (port is null)
        {
            return;
        }

        _pendingPort = port;
        IsConnecting = true;
        ApplyPortHighlights(port);
    }

    /// <summary>
    /// 在兼容目标端口上完成待完成连接。
    /// </summary>
    /// <param name="port">连接手势结束的端口。</param>
    [RelayCommand]
    public void CompleteConnection(FrontedNodePortViewModel? port)
    {
        if (_pendingPort is null || port is null)
        {
            return;
        }

        if (TryNormalizeConnection(_pendingPort, port, out var source, out var target)
            && IsTargetAvailable(source, target))
        {
            AddConnection(source, target);
        }
        else
        {
            var message = _localize("Designer.Graph.Connection.IncompatibleTypes", "Incompatible port types, cannot connect.");
            ExecutionLog.Add(new FrontedGraphExecutionLogItem
            {
                Level = FrontedGraphExecutionLogLevel.Warning,
                Message = $"[{port.Node.DisplayName}.{port.Name}] {message}"
            });
        }

        ClearPortHighlights();
        _pendingPort = null;
        IsConnecting = false;
    }

    /// <summary>
    /// 在两个兼容端口之间添加或替换图连接。
    /// </summary>
    /// <param name="source">候选源端口。</param>
    /// <param name="target">候选目标端口。</param>
    /// <returns><see langword="true"/> when the connection was added.</returns>
    public bool AddConnection(FrontedNodePortViewModel source, FrontedNodePortViewModel target)
    {
        if (!TryNormalizeConnection(source, target, out source, out target)
            || !IsTargetAvailable(source, target))
        {
            return false;
        }

        CreateSnapshot();
        var replaced = Graph.GetOutgoing(source.Node.Model.NodeId, source.Descriptor.Name).ToArray();
        foreach (var connection in replaced)
        {
            Graph.Connections.Remove(connection);
        }
        var model = new FrontedNodeConnection
        {
            SourceNodeId = source.Node.Model.NodeId,
            SourcePort = source.Descriptor.Name,
            TargetNodeId = target.Node.Model.NodeId,
            TargetPort = target.Descriptor.Name
        };
        Graph.Connections.Add(model);
        ReloadConnections();
        RefreshPortConnectionStates();
        Changed();
        return true;
    }

    /// <summary>
    /// 从图中删除连接。
    /// </summary>
    /// <param name="connection">要删除的连接。</param>
    [RelayCommand]
    public void DeleteConnection(FrontedNodeConnectionViewModel? connection)
    {
        if (connection is null || !Graph.Connections.Remove(connection.Model))
        {
            return;
        }
        CreateSnapshot();
        Connections.Remove(connection);
        RefreshPortConnectionStates();
        Changed();
    }

    /// <summary>
    /// 根据图连接列表刷新每个端口的连接状态。
    /// </summary>
    public void RefreshPortConnectionStates()
    {
        foreach (var node in Nodes)
        {
            foreach (var port in node.InputPorts)
            {
                port.IsConnected = Graph.Connections.Any(c =>
                    c.TargetNodeId == node.Model.NodeId && c.TargetPort == port.Name);
            }

            foreach (var port in node.OutputPorts)
            {
                port.IsConnected = Graph.Connections.Any(c =>
                    c.SourceNodeId == node.Model.NodeId && c.SourcePort == port.Name);
            }
        }

        foreach (var node in Nodes)
        {
            node.RefreshExternalInputStates();
        }
    }

    /// <summary>
    /// 用户拖拽连接时应用兼容/不兼容的可视状态。
    /// </summary>
    /// <param name="pendingPort">当前正在连接的端口。</param>
    public void ApplyPortHighlights(FrontedNodePortViewModel? pendingPort)
    {
        foreach (var node in Nodes)
        {
            foreach (var port in node.InputPorts)
            {
                var compatible = pendingPort is not null
                    && TryNormalizeConnection(pendingPort, port, out var source, out var target)
                    && IsTargetAvailable(source, target);
                port.IsHighlighted = compatible;
                port.IsDimmed = pendingPort is not null && !compatible;
            }

            foreach (var port in node.OutputPorts)
            {
                var compatible = pendingPort is not null
                    && TryNormalizeConnection(pendingPort, port, out var source, out var target)
                    && IsTargetAvailable(source, target);
                port.IsHighlighted = compatible;
                port.IsDimmed = pendingPort is not null && !compatible;
            }
        }
    }

    /// <summary>
    /// 清除所有端口上的连接高亮和弱化状态。
    /// </summary>
    public void ClearPortHighlights()
    {
        foreach (var node in Nodes)
        {
            foreach (var port in node.InputPorts)
            {
                port.IsHighlighted = false;
                port.IsDimmed = false;
            }

            foreach (var port in node.OutputPorts)
            {
                port.IsHighlighted = false;
                port.IsDimmed = false;
            }
        }
    }

    /// <summary>
    /// 取消当前连接手势，并重置连接模式 UI 状态。
    /// </summary>
    public void CancelConnection()
    {
        ClearPortHighlights();
        _pendingPort = null;
        IsConnecting = false;
    }

    /// <summary>
    /// 重新校验当前图，并替换校验消息集合。
    /// </summary>
    [RelayCommand]
    public void ValidateGraph()
    {
        ValidationMessages.Clear();
        foreach (var message in _validator.Validate(Graph))
        {
            ValidationMessages.Add(message);
        }
    }

    /// <summary>
    /// 以预览模式执行图，并将运行时日志输出写入 <see cref="ExecutionLog"/>。
    /// </summary>
    /// <returns>预览执行停止后结束的任务。</returns>
    [RelayCommand]
    public async Task RunGraphPreviewAsync()
    {
        if (IsPreviewRunning)
        {
            return;
        }

        ValidateGraph();
        if (ValidationMessages.Any(message => message.Code is "MissingStart" or "MultipleStarts"
                                              || message.Severity == FrontedNodeGraphValidationSeverity.Error))
        {
            ExecutionLog.Add(new FrontedGraphExecutionLogItem { Level = FrontedGraphExecutionLogLevel.Warning, Message = _localize("Designer.Graph.Preview.ValidationBlocked", "Preview blocked by graph validation.") });
            return;
        }

        _previewCancellation = new CancellationTokenSource();
        IsPreviewRunning = true;
        try
        {
            var animationContext = _createAnimationContext?.Invoke();
            if (_animationRuntime is not null && animationContext is null)
            {
                ExecutionLog.Add(new FrontedGraphExecutionLogItem
                {
                    Level = FrontedGraphExecutionLogLevel.Warning,
                    Message = _localize("Designer.Graph.Preview.NoTargetScope", "No preview target scope available.")
                });
            }

            var graphContext = new FrontedGraphExecutionContext
            {
                BehaviorGuid = animationContext?.SelfBehaviorGuid ?? Guid.Empty,
                CurrentControlDisplayName = animationContext?.SelfDisplayName ?? string.Empty,
                ActionExecutor = _animationRuntime is null || animationContext is null
                    ? null
                    : new AnimationRuntimeGraphActionExecutor(_animationRuntime, animationContext)
            };
            AddMissingEventContextWarning(Graph, graphContext);
            var result = await _runtime.ExecuteAsync(Graph, graphContext, _previewCancellation.Token);
            foreach (var item in result.LogItems)
            {
                ExecutionLog.Add(item);
            }
        }
        finally
        {
            IsPreviewRunning = false;
            _previewCancellation.Dispose();
            _previewCancellation = null;
        }
    }

    /// <summary>
    /// 取消当前正在运行的图预览（如果存在）。
    /// </summary>
    [RelayCommand]
    public void StopPreview() => _previewCancellation?.Cancel();

    /// <summary>
    /// 重置当前预览目标的动画值。
    /// </summary>
    [RelayCommand]
    public void ResetCurrentTarget()
    {
        var context = _createAnimationContext?.Invoke();
        if (_animationRuntime is null || context is null || context.SelfBehaviorGuid == Guid.Empty)
        {
            ExecutionLog.Add(new FrontedGraphExecutionLogItem
            {
                Level = FrontedGraphExecutionLogLevel.Warning,
                Message = _localize("Designer.Graph.Preview.NoTargetScope", "No preview target scope available.")
            });
            return;
        }

        _animationRuntime.ResetTarget(context.SelfBehaviorGuid, context);
        ExecutionLog.Add(new FrontedGraphExecutionLogItem
        {
            Level = FrontedGraphExecutionLogLevel.Information,
            Message = _localize("Designer.Graph.Preview.ResetCurrent", "Reset current preview target.")
        });
    }

    /// <summary>
    /// 重置当前预览作用域中的所有动画值。
    /// </summary>
    [RelayCommand]
    public void ResetAllPreview()
    {
        var context = _createAnimationContext?.Invoke();
        if (_animationRuntime is null || context is null)
        {
            ExecutionLog.Add(new FrontedGraphExecutionLogItem
            {
                Level = FrontedGraphExecutionLogLevel.Warning,
                Message = _localize("Designer.Graph.Preview.NoTargetScope", "No preview target scope available.")
            });
            return;
        }

        _animationRuntime.ResetAll(context);
        ExecutionLog.Add(new FrontedGraphExecutionLogItem
        {
            Level = FrontedGraphExecutionLogLevel.Information,
            Message = _localize("Designer.Graph.Preview.ResetAll", "Reset all preview animation values.")
        });
    }

    /// <summary>
    /// 清除预览执行日志行。
    /// </summary>
    [RelayCommand]
    public void ClearExecutionLog() => ExecutionLog.Clear();

    /// <summary>
    /// 当预览没有事件上下文导致事件 payload 条件无法解析时添加预览警告。
    /// </summary>
    /// <param name="graph">正在预览的图。</param>
    /// <param name="context">运行时执行上下文。</param>
    private void AddMissingEventContextWarning(FrontedNodeGraph graph, FrontedGraphExecutionContext context)
    {
        var missingPath = graph.Nodes
            .Where(node => node.NodeType == "flow.if")
            .Select(node => node.Properties.TryGetValue("Left", out var value)
                ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString()
                : null)
            .FirstOrDefault(path => path?.StartsWith("Event.", StringComparison.Ordinal) == true);
        if (missingPath is null || context.EventPayload.Count > 0)
        {
            return;
        }

        ExecutionLog.Add(new FrontedGraphExecutionLogItem
        {
            Level = FrontedGraphExecutionLogLevel.Warning,
            Message = string.Format(
                _localize(
                    "Designer.Graph.Preview.MissingEventContext",
                    "The current preview has no event context, so {0} cannot be resolved."),
                missingPath)
        });
    }

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

public sealed partial class FrontedNodeConnectionViewModel(
    FrontedNodeConnection model,
    FrontedNodeEditorViewModel source,
    FrontedNodeEditorViewModel target,
    Func<string, string, string>? localize = null) : ObservableObject
{
    private readonly Func<string, string, string>? _localize = localize;

    public FrontedNodeConnection Model { get; } = model;
    public FrontedNodeEditorViewModel Source { get; } = source;
    public FrontedNodeEditorViewModel Target { get; } = target;
    public string Summary => $"{Source.DisplayName}.{Model.SourcePort} -> {Target.DisplayName}.{Model.TargetPort}";
    /// <summary>获取该连接的源端口视图模型。</summary>
    public FrontedNodePortViewModel? SourcePort => Source.OutputPorts.FirstOrDefault(port => port.Name == Model.SourcePort);
    /// <summary>获取该连接的目标端口视图模型。</summary>
    public FrontedNodePortViewModel? TargetPort => Target.InputPorts.FirstOrDefault(port => port.Name == Model.TargetPort);
    /// <summary>获取可见源端口名称。</summary>
    public string SourcePortDisplayName => SourcePort?.DisplayName ?? Model.SourcePort;
    /// <summary>获取可见目标端口名称。</summary>
    public string TargetPortDisplayName => TargetPort?.DisplayName ?? Model.TargetPort;
    /// <summary>获取连接源侧的语义含义。</summary>
    public string Meaning => SourcePort?.Meaning ?? SourcePortDisplayName;
    /// <summary>获取根据源端口角色派生的连接描边颜色。</summary>
    public string StrokeColorHex => SourcePort?.PortColorHex ?? "#1976D2";
    /// <summary>获取根据源端口角色派生的连接描边粗细。</summary>
    public double StrokeThickness => SourcePort?.IsParallelContinuation == true ? 4D : 3D;
    /// <summary>获取悬停连接时显示的连接检查文本。</summary>
    public string InspectionText => $"{Source.DisplayName}.{Model.SourcePort} -> {Target.DisplayName}.{Model.TargetPort}{Environment.NewLine}"
                                    + $"{Source.DisplayName}: {SourcePortDisplayName}{Environment.NewLine}"
                                    + $"{Target.DisplayName}: {TargetPortDisplayName}{Environment.NewLine}"
                                    + $"{Localize("Designer.Graph.Connection.MeaningLabel", "Meaning")}: {Meaning}";
    public double X1 => Source.X + Source.CardWidth;
    public double Y1 => Source.Y + (SourcePort?.CenterOffsetY ?? 56D);
    public double X2 => Target.X;
    public double Y2 => Target.Y + (TargetPort?.CenterOffsetY ?? 56D);

    /// <summary>贝塞尔曲线控制点 1 X（从起点水平向右延伸）</summary>
    public double CP1X => X1 + CurveOffset;
    /// <summary>贝塞尔曲线控制点 1 Y</summary>
    public double CP1Y => Y1;
    /// <summary>贝塞尔曲线控制点 2 X（从终点水平向左延伸）</summary>
    public double CP2X => X2 - CurveOffset;
    /// <summary>贝塞尔曲线控制点 2 Y</summary>
    public double CP2Y => Y2;

    private double CurveOffset => Math.Max(60, Math.Abs(X2 - X1) * 0.45);

    /// <summary>贝塞尔曲线 Path 数据（StreamGeometry 小语言格式）</summary>
    public string PathData => $"M {X1:F1},{Y1:F1} C {CP1X:F1},{CP1Y:F1} {CP2X:F1},{CP2Y:F1} {X2:F1},{Y2:F1}";

    public double MidX => (X1 + X2) / 2D - 12D;
    public double MidY => (Y1 + Y2) / 2D - 12D;

    public void Refresh()
    {
        OnPropertyChanged(nameof(X1));
        OnPropertyChanged(nameof(Y1));
        OnPropertyChanged(nameof(X2));
        OnPropertyChanged(nameof(Y2));
        OnPropertyChanged(nameof(CP1X));
        OnPropertyChanged(nameof(CP1Y));
        OnPropertyChanged(nameof(CP2X));
        OnPropertyChanged(nameof(CP2Y));
        OnPropertyChanged(nameof(PathData));
        OnPropertyChanged(nameof(MidX));
        OnPropertyChanged(nameof(MidY));
        OnPropertyChanged(nameof(InspectionText));
    }

    private string Localize(string key, string fallback) =>
        _localize?.Invoke(key, fallback) ?? fallback;
}

public sealed partial class FrontedNodePropertyEditorViewModel : ObservableValidator
{
    private readonly FrontedNode _node;
    private readonly Action _markDirty;
    private readonly Action _validate;
    private readonly IReadOnlyList<FrontedNodeTargetOptionViewModel> _targetOptions;
    private readonly IReadOnlyList<FrontedGraphConditionFieldOptionViewModel> _conditionFieldOptions;
    private readonly Func<string, string, string> _localize;
    private readonly Func<FrontedNode, string, bool> _hasIncomingConnection;
    private readonly IReadOnlyList<FrontedNodePropertyOptionViewModel> _localizedOptions;
    private readonly IReadOnlyList<FrontedNodePropertyOptionViewModel> _booleanOptions;
    private readonly IReadOnlyList<FrontedNodePropertyOptionViewModel> _visibilityOptions;
    private Action? _refreshRelatedProperties;
    private string? _validationError;
    private Color _colorValue = Colors.White;
    private double? _numberValue;

    public FrontedNodePropertyEditorViewModel(
        FrontedNode node,
        FrontedNodePropertyDescriptor descriptor,
        Action markDirty,
        Action validate,
        Func<string, string, string> localize,
        IReadOnlyList<FrontedNodeTargetOptionViewModel> targetOptions,
        IReadOnlyList<FrontedGraphConditionFieldOptionViewModel>? conditionFieldOptions = null,
        Func<FrontedNode, string, bool>? hasIncomingConnection = null)
    {
        _node = node;
        Descriptor = descriptor;
        _markDirty = markDirty;
        _validate = validate;
        _targetOptions = targetOptions;
        _conditionFieldOptions = conditionFieldOptions ?? [];
        _localize = localize;
        _hasIncomingConnection = hasIncomingConnection ?? ((_, _) => false);
        DisplayName = localize(descriptor.DisplayNameKey, descriptor.Name);
        Description = localize($"{descriptor.DisplayNameKey}.Description", descriptor.Name);
        _localizedOptions = descriptor.Options
            .Select(option => new FrontedNodePropertyOptionViewModel(option, LocalizeOption(option)))
            .ToArray();
        _booleanOptions =
        [
            new FrontedNodePropertyOptionViewModel("true", "true"),
            new FrontedNodePropertyOptionViewModel("false", "false")
        ];
        _visibilityOptions = FrontedBehaviorPropertyMetadata.VisibilityOptions
            .Select(option => new FrontedNodePropertyOptionViewModel(option, localize($"Designer.Option.Visibility.{option}", option)))
            .ToArray();
        if (ColorHelper.TryParseColor(TextValue, out var color))
        {
            _colorValue = color;
        }
        _numberValue = ParseNumberValue();
        ErrorsChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(NumberValue))
            {
                OnPropertyChanged(nameof(ValidationError));
                OnPropertyChanged(nameof(HasValidationError));
            }
        };
        ValidateProperty(NumberValue, nameof(NumberValue));
    }

    public FrontedNodePropertyDescriptor Descriptor { get; }
    public string DisplayName { get; }
    public string Description { get; }
    /// <summary>获取指示当前手动值是否可编辑的值。</summary>
    public bool IsManualValueEnabled => !HasExternalValueInput;
    /// <summary>获取指示当前属性是否由外部数值输入提供的值。</summary>
    public bool HasExternalValueInput => ResolveExternalInputPort() is { } port && _hasIncomingConnection(_node, port);
    /// <summary>获取外部数值输入说明。</summary>
    public string ExternalValueInputNotice => _localize("Designer.Graph.ExternalValueInput", "An external numeric input is connected; the manual value is disabled.");
    /// <summary>获取当前属性的上下文输入提示。</summary>
    public string Placeholder => DynamicMetadata?.Placeholder ?? string.Empty;
    /// <summary>获取当前属性的上下文帮助。</summary>
    public string HelpText => DynamicMetadata is null
        ? Description
        : _localize(DynamicMetadata.DescriptionKey, $"{DynamicMetadata.Placeholder}; example: {DynamicMetadata.Example}");
    public bool IsBoolean => Descriptor.EditorKind == FrontedNodePropertyEditorKind.Boolean;
    public bool IsEnum => Descriptor.EditorKind == FrontedNodePropertyEditorKind.Enum && !IsConditionOperator;
    public bool IsNumber => Descriptor.EditorKind == FrontedNodePropertyEditorKind.Number || IsNumericDynamicValue || IsNumericConditionValue;
    /// <summary>获取该数值属性是否可在不丢失百分比表达式的情况下使用 NumberBox。</summary>
    public bool IsNumberBox => IsNumber && !FrontedBehaviorPropertyMetadata.SupportsPercentage(EffectivePropertyNameForValidation());
    /// <summary>获取该数值属性是否必须保留文本编辑以支持百分比表达式。</summary>
    public bool IsPercentageNumberText => IsNumber && !IsNumberBox;
    public bool IsColor => Descriptor.EditorKind == FrontedNodePropertyEditorKind.Color || IsColorDynamicValue;
    public bool IsControlReference => Descriptor.EditorKind == FrontedNodePropertyEditorKind.ControlReference;
    public bool IsPropertyName => Descriptor.EditorKind == FrontedNodePropertyEditorKind.PropertyName;
    public bool IsVisibilityValue => IsDynamicValue && FrontedBehaviorPropertyMetadata.IsVisibilityProperty(CurrentBehaviorPropertyName);
    public bool HasTextSuggestions => !IsConditionProperty && !IsBoolean && !IsEnum && !IsNumber && !IsColor && !IsControlReference && !IsPropertyName && Descriptor.Options.Count > 0;
    public bool IsText => !IsConditionProperty && !IsBoolean && !IsEnum && !IsNumber && !IsColor && !IsControlReference && !IsPropertyName && !HasTextSuggestions && !IsVisibilityValue;
    /// <summary>获取该属性是否选择左侧条件字段。</summary>
    public bool IsConditionField => _node.NodeType == "flow.if" && Descriptor.Name == "Left";
    /// <summary>获取该属性是否选择数值事件上下文字段。</summary>
    public bool IsEventContextField => _node.NodeType == "value.eventContext" && Descriptor.Name == "Path";
    /// <summary>获取该属性是否选择条件运算符。</summary>
    public bool IsConditionOperator => _node.NodeType == "flow.if" && Descriptor.Name == "Operator";
    /// <summary>获取该属性是否编辑右侧条件值。</summary>
    public bool IsConditionValue => _node.NodeType == "flow.if" && Descriptor.Name == "Right";
    /// <summary>获取选中的条件值是否类似布尔值。</summary>
    public bool IsBooleanConditionValue => IsConditionValue && IsBooleanType(SelectedConditionField?.TypeName);
    /// <summary>获取选中的条件值是否类似枚举。</summary>
    public bool IsEnumConditionValue => IsConditionValue && SelectedConditionField?.EnumValues.Count > 0;
    /// <summary>获取选中的条件值是否为数值。</summary>
    public bool IsNumericConditionValue => IsConditionValue && IsNumericType(SelectedConditionField?.TypeName);
    /// <summary>获取条件值是否应使用自由文本。</summary>
    public bool IsTextConditionValue => IsConditionValue && !IsBooleanConditionValue && !IsEnumConditionValue && !IsNumericConditionValue;
    /// <summary>获取该条件可用的上下文感知事件字段。</summary>
    public IReadOnlyList<FrontedGraphConditionFieldOptionViewModel> ConditionFieldOptions => EnsureCurrentConditionFieldOption();
    /// <summary>获取当前阶段可用于数值计算的事件上下文字段。</summary>
    public IReadOnlyList<FrontedGraphConditionFieldOptionViewModel> EventContextFieldOptions =>
        EnsureCurrentConditionFieldOption();
    /// <summary>获取选中字段类型可用的上下文感知运算符。</summary>
    public IReadOnlyList<FrontedNodePropertyOptionViewModel> ConditionOperatorOptions => ResolveConditionOperatorOptions();
    /// <summary>获取选中字段可用的稳定布尔值或枚举值。</summary>
    public IReadOnlyList<FrontedNodePropertyOptionViewModel> ConditionValueOptions => ResolveConditionValueOptions();
    public IReadOnlyList<string> Options => Descriptor.Options;
    public IReadOnlyList<FrontedNodePropertyOptionViewModel> LocalizedOptions => _localizedOptions;
    /// <summary>获取布尔属性编辑器可用的稳定布尔选项。</summary>
    public IReadOnlyList<FrontedNodePropertyOptionViewModel> BooleanOptions => _booleanOptions;
    public IReadOnlyList<FrontedNodePropertyOptionViewModel> DisplayedOptions => ResolveDisplayedOptions();
    public IReadOnlyList<FrontedNodePropertyOptionViewModel> VisibilityOptions => _visibilityOptions;
    public IReadOnlyList<FrontedNodeTargetOptionViewModel> TargetOptions => EnsureCurrentTargetOption();
    public string? Unit => IsRotation ? "°" : Descriptor.Unit;
    public bool HasUnit => !string.IsNullOrWhiteSpace(Unit);
    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationError);
    public string? ValidationError
    {
        get => _validationError ?? GetErrors(nameof(NumberValue)).Cast<object>().FirstOrDefault()?.ToString();
        private set
        {
            if (SetProperty(ref _validationError, value))
            {
                OnPropertyChanged(nameof(HasValidationError));
            }
        }
    }
    public Color ColorValue
    {
        get => _colorValue;
        set
        {
            if (!SetProperty(ref _colorValue, value))
            {
                return;
            }

            TextValue = value.ToArgbHexString();
        }
    }

    public string TextValue
    {
        get => Read().ValueKind == JsonValueKind.String ? Read().GetString() ?? string.Empty : Read().ToString();
        set
        {
            if (IsNumberBox)
            {
                NumberValue = double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                    && double.IsFinite(number)
                    ? number
                    : null;
                return;
            }

            if (!ValidateTextValue(value, out var normalized))
            {
                OnPropertyChanged(nameof(TextValue));
                return;
            }

            Write(Descriptor.PropertyType == FrontedNodePropertyType.Number && double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedNumber)
                ? JsonSerializer.SerializeToElement(parsedNumber)
                : JsonSerializer.SerializeToElement(normalized));
        }
    }

    public bool BooleanValue
    {
        get => Read().ValueKind == JsonValueKind.True;
        set => Write(JsonSerializer.SerializeToElement(value));
    }

    /// <summary>获取或设置由 WPF-UI NumberBox 编辑的数值。</summary>
    [CustomValidation(typeof(FrontedNodePropertyEditorViewModel), nameof(ValidateNumberBoxValue))]
    public double? NumberValue
    {
        get => _numberValue;
        set
        {
            if (!SetProperty(ref _numberValue, value, true))
            {
                return;
            }

            if (!GetErrors(nameof(NumberValue)).Cast<object>().Any() && value is { } number)
            {
                Write(JsonSerializer.SerializeToElement(number));
            }
        }
    }

    /// <summary>获取 NumberBox 显示的最小值。</summary>
    public double NumberMinimum => _node.NodeType == "flow.parallel" && Descriptor.Name == "BranchCount"
        ? FrontedParallelNodePorts.MinBranchCount
        : Descriptor.Name == "DurationMs"
            ? 0D
            : DynamicMetadata?.Min ?? double.MinValue;

    /// <summary>获取 NumberBox 显示的最大值。</summary>
    public double NumberMaximum => _node.NodeType == "flow.parallel" && Descriptor.Name == "BranchCount"
        ? FrontedParallelNodePorts.MaxBranchCount
        : DynamicMetadata?.Max ?? double.MaxValue;

    /// <summary>获取 NumberBox 的小数位限制。</summary>
    public int NumberMaxDecimalPlaces => RequiresIntegerNumber ? 0 : 6;

    /// <summary>获取或设置用于 ComboBox 编辑的稳定小写布尔字符串。</summary>
    public string BooleanTextValue
    {
        get => BooleanValue ? "true" : "false";
        set => BooleanValue = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    public string EnumValue
    {
        get => TextValue;
        set => Write(JsonSerializer.SerializeToElement(value));
    }

    /// <summary>获取或设置稳定的条件字段路径。</summary>
    public string ConditionFieldValue
    {
        get => TextValue;
        set
        {
            TextValue = value;
            var allowed = ResolveConditionOperatorOptions().Select(option => option.Value).ToHashSet(StringComparer.Ordinal);
            var currentOperator = ReadNodeString("Operator");
            if (!allowed.Contains(currentOperator))
            {
                _node.Properties["Operator"] = JsonSerializer.SerializeToElement(TriggerFilterOperator.Equals.ToString());
                _markDirty();
                _validate();
                _refreshRelatedProperties?.Invoke();
            }
        }
    }

    /// <summary>获取或设置数值事件上下文的稳定字段路径。</summary>
    public string EventContextFieldValue
    {
        get => TextValue;
        set => TextValue = value;
    }

    /// <summary>获取或设置稳定的条件运算符名称。</summary>
    public string ConditionOperatorValue
    {
        get => TextValue;
        set => TextValue = value;
    }

    /// <summary>获取或设置稳定的类型化条件选项。</summary>
    public string ConditionChoiceValue
    {
        get => TextValue;
        set => TextValue = value;
    }

    public string TargetValue
    {
        get => TextValue;
        set => TextValue = value;
    }

    public string PropertyNameValue
    {
        get => TextValue;
        set => TextValue = value;
    }

    public string PropertyNameText
    {
        get => DisplayForValue(TextValue);
        set => TextValue = ValueForDisplay(value);
    }

    public string SuggestionText
    {
        get => DisplayForValue(TextValue);
        set => TextValue = ValueForDisplay(value);
    }

    public string VisibilityValue
    {
        get => TextValue;
        set => TextValue = value;
    }

    public void SetRefreshRelatedProperties(Action refreshRelatedProperties)
    {
        _refreshRelatedProperties = refreshRelatedProperties;
    }

    public void RefreshEditorState()
    {
        OnPropertyChanged(nameof(IsNumber));
        OnPropertyChanged(nameof(IsNumberBox));
        OnPropertyChanged(nameof(IsPercentageNumberText));
        OnPropertyChanged(nameof(IsColor));
        OnPropertyChanged(nameof(IsVisibilityValue));
        OnPropertyChanged(nameof(IsText));
        OnPropertyChanged(nameof(HasTextSuggestions));
        OnPropertyChanged(nameof(Unit));
        OnPropertyChanged(nameof(HasUnit));
        OnPropertyChanged(nameof(DisplayedOptions));
        if (_node.NodeType == "flow.if")
        {
            OnPropertyChanged(nameof(IsConditionField));
            OnPropertyChanged(nameof(IsConditionOperator));
            OnPropertyChanged(nameof(IsConditionValue));
            OnPropertyChanged(nameof(IsBooleanConditionValue));
            OnPropertyChanged(nameof(IsEnumConditionValue));
            OnPropertyChanged(nameof(IsNumericConditionValue));
            OnPropertyChanged(nameof(IsTextConditionValue));
            OnPropertyChanged(nameof(ConditionFieldOptions));
            OnPropertyChanged(nameof(ConditionOperatorOptions));
            OnPropertyChanged(nameof(ConditionValueOptions));
            OnPropertyChanged(nameof(ConditionFieldValue));
            OnPropertyChanged(nameof(ConditionOperatorValue));
            OnPropertyChanged(nameof(ConditionChoiceValue));
        }
        if (_node.NodeType == "value.eventContext")
        {
            OnPropertyChanged(nameof(IsEventContextField));
            OnPropertyChanged(nameof(EventContextFieldOptions));
            OnPropertyChanged(nameof(EventContextFieldValue));
        }
        OnPropertyChanged(nameof(PropertyNameText));
        OnPropertyChanged(nameof(Placeholder));
        OnPropertyChanged(nameof(HelpText));
        if (IsNumber)
        {
            OnPropertyChanged(nameof(NumberMinimum));
            OnPropertyChanged(nameof(NumberMaximum));
            OnPropertyChanged(nameof(NumberMaxDecimalPlaces));
        }
        _numberValue = ParseNumberValue();
        OnPropertyChanged(nameof(NumberValue));
        ValidateProperty(NumberValue, nameof(NumberValue));
    }

    private JsonElement Read() => _node.Properties.TryGetValue(Descriptor.Name, out var value) ? value : Descriptor.DefaultValue;

    private void Write(JsonElement value)
    {
        if (JsonElement.DeepEquals(Read(), value))
        {
            return;
        }

        _node.Properties[Descriptor.Name] = value;
        _markDirty();
        _validate();
        ValidationError = null;
        if (IsColor && ColorHelper.TryParseColor(TextValue, out var color))
        {
            SetProperty(ref _colorValue, color, nameof(ColorValue));
        }
        OnPropertyChanged(nameof(TextValue));
        _numberValue = ParseNumberValue();
        OnPropertyChanged(nameof(NumberValue));
        OnPropertyChanged(nameof(BooleanValue));
        OnPropertyChanged(nameof(BooleanTextValue));
        OnPropertyChanged(nameof(EnumValue));
        OnPropertyChanged(nameof(TargetValue));
        OnPropertyChanged(nameof(PropertyNameValue));
        OnPropertyChanged(nameof(PropertyNameText));
        OnPropertyChanged(nameof(SuggestionText));
        OnPropertyChanged(nameof(VisibilityValue));
        OnPropertyChanged(nameof(DisplayedOptions));
        if (_node.NodeType == "flow.if")
        {
            OnPropertyChanged(nameof(ConditionFieldValue));
            OnPropertyChanged(nameof(ConditionOperatorValue));
            OnPropertyChanged(nameof(ConditionChoiceValue));
        }
        if (_node.NodeType == "value.eventContext")
        {
            OnPropertyChanged(nameof(EventContextFieldValue));
        }
        _refreshRelatedProperties?.Invoke();
    }

    private bool ValidateTextValue(string? value, out string normalized)
    {
        normalized = value ?? string.Empty;
        if (IsColor)
        {
            if (!ColorHelper.TryNormalizeHex(value, out normalized))
            {
                ValidationError = "Invalid color. Use #RRGGBB, #AARRGGBB, or a WPF color name.";
                return false;
            }

            return true;
        }

        if (IsNumber)
        {
            var propertyName = EffectivePropertyNameForValidation();
            if (!FrontedBehaviorPropertyMetadata.TryValidateValue(propertyName, value, out var message))
            {
                ValidationError = message;
                return false;
            }

            var trimmed = value?.Trim() ?? string.Empty;
            var isPercentage = FrontedBehaviorPropertyMetadata.SupportsPercentage(propertyName)
                               && trimmed.EndsWith('%');
            var numericText = isPercentage ? trimmed[..^1] : trimmed;
            if (!double.TryParse(numericText, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
            {
                ValidationError = "Value must be a finite number.";
                return false;
            }

            if (isPercentage)
            {
                normalized = $"{number.ToString(CultureInfo.InvariantCulture)}%";
                return true;
            }

            normalized = number.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        if (IsVisibilityValue
            && !FrontedBehaviorPropertyMetadata.VisibilityOptions.Any(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase)))
        {
            ValidationError = "Visibility must be Visible, Hidden, or Collapsed.";
            return false;
        }

        ValidationError = null;
        return true;
    }

    private IReadOnlyList<FrontedNodeTargetOptionViewModel> EnsureCurrentTargetOption()
    {
        if (!IsControlReference || string.IsNullOrWhiteSpace(TextValue)
            || _targetOptions.Any(option => string.Equals(option.Value, TextValue, StringComparison.Ordinal)))
        {
            return _targetOptions;
        }

        return [.. _targetOptions, new FrontedNodeTargetOptionViewModel(TextValue, $"Unknown target ({TextValue})")];
    }

    private IReadOnlyList<FrontedGraphConditionFieldOptionViewModel> EnsureCurrentConditionFieldOption()
    {
        var current = IsConditionField || IsEventContextField ? TextValue : ReadNodeString("Left");
        if (string.IsNullOrWhiteSpace(current)
            || _conditionFieldOptions.Any(option => string.Equals(option.ValuePath, current, StringComparison.Ordinal)))
        {
            return _conditionFieldOptions;
        }

        return [.. _conditionFieldOptions, new FrontedGraphConditionFieldOptionViewModel(current, current, current, "string", [], null, current)];
    }

    private IReadOnlyList<FrontedNodePropertyOptionViewModel> ResolveConditionOperatorOptions()
    {
        var allowed = IsBooleanType(SelectedConditionField?.TypeName) || SelectedConditionField?.EnumValues.Count > 0
            ? new[] { TriggerFilterOperator.Equals, TriggerFilterOperator.NotEquals, TriggerFilterOperator.Exists }
            : IsNumericType(SelectedConditionField?.TypeName)
                ? new[]
                {
                    TriggerFilterOperator.Equals, TriggerFilterOperator.NotEquals, TriggerFilterOperator.GreaterThan,
                    TriggerFilterOperator.GreaterThanOrEqual, TriggerFilterOperator.LessThan,
                    TriggerFilterOperator.LessThanOrEqual, TriggerFilterOperator.Exists
                }
                : Enum.GetValues<TriggerFilterOperator>();
        return allowed.Select(value => new FrontedNodePropertyOptionViewModel(value.ToString(), LocalizeOption(value.ToString()))).ToArray();
    }

    private IReadOnlyList<FrontedNodePropertyOptionViewModel> ResolveConditionValueOptions()
    {
        if (IsBooleanType(SelectedConditionField?.TypeName))
        {
            return _booleanOptions;
        }

        return SelectedConditionField?.EnumValues
            .Select(value => new FrontedNodePropertyOptionViewModel(value, value))
            .ToArray() ?? [];
    }

    private string LocalizeOption(string value)
    {
        if (IsPropertyName)
        {
            return _localize(
                string.Equals(value, "All", StringComparison.OrdinalIgnoreCase)
                    ? "Designer.Graph.PropertyName.All"
                    : $"Designer.Property.{value}",
                value);
        }

        return _localize($"Designer.Option.{Descriptor.Name}.{value}", value);
    }

    private string DisplayForValue(string value) =>
        DisplayedOptions.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.Ordinal))?.DisplayName
        ?? value;

    private string ValueForDisplay(string? display)
    {
        var value = display ?? string.Empty;
        return DisplayedOptions.FirstOrDefault(option =>
                   string.Equals(option.DisplayName, value, StringComparison.Ordinal)
                   || string.Equals(option.Value, value, StringComparison.Ordinal))?.Value
               ?? value;
    }

    private IReadOnlyList<FrontedNodePropertyOptionViewModel> ResolveDisplayedOptions()
    {
        if (!IsPropertyName)
        {
            return _localizedOptions;
        }

        var names = FrontedBehaviorPropertyMetadata.GetPropertyNamesForLayer(
            CurrentTargetLayer,
            Descriptor.Options.Any(option => string.Equals(option, "All", StringComparison.OrdinalIgnoreCase)));
        return names
            .Select(option => new FrontedNodePropertyOptionViewModel(option, LocalizeOption(option)))
            .ToArray();
    }

    private string? CurrentBehaviorPropertyName =>
        _node.Properties.TryGetValue("PropertyName", out var property)
            ? property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString()
            : null;

    private FrontedGraphConditionFieldOptionViewModel? SelectedConditionField =>
        EnsureCurrentConditionFieldOption().FirstOrDefault(option =>
            string.Equals(option.ValuePath, ReadNodeString("Left"), StringComparison.Ordinal));

    private bool IsConditionProperty => IsConditionField || IsEventContextField || IsConditionOperator || IsConditionValue;

    /// <summary>刷新外部值输入连接导致的可编辑状态。</summary>
    public void RefreshExternalInputState()
    {
        OnPropertyChanged(nameof(HasExternalValueInput));
        OnPropertyChanged(nameof(IsManualValueEnabled));
        OnPropertyChanged(nameof(ExternalValueInputNotice));
    }

    private string? ResolveExternalInputPort() => (_node.NodeType, Descriptor.Name) switch
    {
        ("action.setProperty", "Value") => "ValueInput",
        ("action.animateProperty", "From") => "FromInput",
        ("action.animateProperty", "To") => "ToInput",
        _ => null
    };

    private string ReadNodeString(string name) =>
        _node.Properties.TryGetValue(name, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString()
            : string.Empty;

    private static bool IsBooleanType(string? typeName) =>
        string.Equals(typeName?.TrimEnd('?'), "bool", StringComparison.OrdinalIgnoreCase)
        || string.Equals(typeName?.TrimEnd('?'), "Boolean", StringComparison.OrdinalIgnoreCase);

    private static bool IsNumericType(string? typeName) =>
        typeName?.TrimEnd('?').ToLowerInvariant() is "byte" or "short" or "int" or "long" or "float" or "double" or "decimal";

    private FrontedAnimationTargetLayer CurrentTargetLayer =>
        _node.Properties.TryGetValue("TargetLayer", out var value)
        && Enum.TryParse<FrontedAnimationTargetLayer>(
            value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString(),
            true,
            out var layer)
            ? layer
            : FrontedAnimationTargetLayer.Auto;

    private bool IsDynamicValue => Descriptor.Name is "Value" or "From" or "To";
    private bool IsColorDynamicValue => IsDynamicValue && FrontedBehaviorPropertyMetadata.IsColorProperty(CurrentBehaviorPropertyName);
    private bool IsNumericDynamicValue => IsDynamicValue && FrontedBehaviorPropertyMetadata.IsNumericProperty(CurrentBehaviorPropertyName);
    private bool IsRotation => string.Equals(Descriptor.Name, "Rotation", StringComparison.OrdinalIgnoreCase)
        || string.Equals(CurrentBehaviorPropertyName, "Rotation", StringComparison.OrdinalIgnoreCase);
    private FrontedAnimatablePropertyMetadata? DynamicMetadata =>
        IsDynamicValue ? FrontedBehaviorPropertyMetadata.Find(CurrentBehaviorPropertyName) : FrontedBehaviorPropertyMetadata.Find(Descriptor.Name);
    private string? EffectivePropertyNameForValidation() =>
        IsDynamicValue ? CurrentBehaviorPropertyName : Descriptor.Name;

    private bool RequiresIntegerNumber =>
        _node.NodeType == "flow.parallel" && Descriptor.Name == "BranchCount"
        || Descriptor.Name == "DurationMs";

    private double? ParseNumberValue()
    {
        var value = TextValue;
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)
            ? number
            : null;
    }

    /// <summary>
    /// 根据图属性元数据校验 NumberBox 值。
    /// </summary>
    /// <param name="value">候选数值。</param>
    /// <param name="context">包含属性编辑器的校验上下文。</param>
    /// <returns>校验结果。</returns>
    public static ValidationResult? ValidateNumberBoxValue(double? value, ValidationContext context)
    {
        var editor = (FrontedNodePropertyEditorViewModel)context.ObjectInstance;
        if (!editor.IsNumberBox)
        {
            return ValidationResult.Success;
        }

        if (value is null || !double.IsFinite(value.Value))
        {
            return new ValidationResult(editor._localize("Designer.Graph.Validation.NumberFinite", "Value must be a finite number."));
        }

        if (editor.RequiresIntegerNumber && value.Value != Math.Truncate(value.Value))
        {
            return new ValidationResult(editor._localize("Designer.Graph.Validation.NumberInteger", "Value must be an integer."));
        }

        if (value.Value < editor.NumberMinimum || value.Value > editor.NumberMaximum)
        {
            return new ValidationResult(string.Format(
                editor._localize("Designer.Graph.Validation.NumberRange", "Value must be between {0} and {1}."),
                editor.NumberMinimum,
                editor.NumberMaximum));
        }

        var text = value.Value.ToString(CultureInfo.InvariantCulture);
        return FrontedBehaviorPropertyMetadata.TryValidateValue(editor.EffectivePropertyNameForValidation(), text, out var message)
            ? ValidationResult.Success
            : new ValidationResult(message);
    }
}

/// <summary>
/// 行为图目标编辑器显示的目标选项。
/// </summary>
/// <param name="Value">已持久化的目标引用值。</param>
/// <param name="DisplayName">面向用户的目标显示名称。</param>
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
