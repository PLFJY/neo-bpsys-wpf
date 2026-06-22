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

namespace neo_bpsys_wpf.ViewModels.FrontedDesigner.GraphEditor;

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

    public FrontedNodeGraph Graph { get; }
    public ObservableCollection<FrontedNodeEditorViewModel> Nodes { get; } = [];
    public ObservableCollection<FrontedNodeEditorViewModel> SelectedNodes { get; } = [];
    public ObservableCollection<FrontedNodeConnectionViewModel> Connections { get; } = [];
    public ObservableCollection<FrontedNodeGraphValidationMessage> ValidationMessages { get; } = [];
    public ObservableCollection<FrontedGraphExecutionLogItem> ExecutionLog { get; } = [];
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

    public IEnumerable<FrontedNodeCatalogItemViewModel> FilteredCatalog =>
        string.IsNullOrWhiteSpace(CatalogSearchText)
            ? Catalog
            : Catalog.Where(item => item.DisplayName.Contains(CatalogSearchText, StringComparison.OrdinalIgnoreCase)
                                    || item.Category.Contains(CatalogSearchText, StringComparison.OrdinalIgnoreCase));

    public string ConnectionStatus => IsConnecting
        ? _localize("Designer.Graph.Connection.SelectInput", "Select an input port.")
        : string.Empty;

    partial void OnCatalogSearchTextChanged(string value) => OnPropertyChanged(nameof(FilteredCatalog));

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

    /// <summary>删除多个节点</summary>
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

    /// <summary>Copies selected nodes and their internal connections to the app-level graph clipboard.</summary>
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

    /// <summary>Pastes nodes from the app-level graph clipboard and remaps all copied identifiers.</summary>
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

    /// <summary>框选过程中实时预览选中效果（仅更新 IsSelected，不修改 SelectedNodes）</summary>
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

    /// <summary>框选矩形内的所有节点</summary>
    /// <param name="selectionRect">选框（画布坐标系）</param>
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

    /// <summary>清除多选</summary>
    [RelayCommand]
    public void DeselectAll()
    {
        ClearIsSelected();
        SelectedNode = null;
        SelectedNodes.Clear();
    }

    private void ClearIsSelected()
    {
        foreach (var node in Nodes)
        {
            node.IsSelected = false;
        }
    }

    /// <summary>在拖拽开始前调用，创建一次快照（避免每次 DragDelta 都创建）</summary>
    public void BeginMoveNodes()
    {
        CreateSnapshot();
        _isDragging = true;
    }

    /// <summary>移动节点（支持多选同步移动）</summary>
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

    /// <summary>拖拽结束</summary>
    public void EndMoveNodes()
    {
        _isDragging = false;
    }

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
    /// 刷新所有端口的连接状态，标记已连接的端口。
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
    }

    /// <summary>
    /// 根据待连端口设置所有端口的兼容高亮/变灰状态。
    /// </summary>
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
    /// 清除所有端口的高亮/变灰状态。
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
    /// 取消当前正在进行的连接拖拽，清除高亮并重置状态。
    /// </summary>
    public void CancelConnection()
    {
        ClearPortHighlights();
        _pendingPort = null;
        IsConnecting = false;
    }

    [RelayCommand]
    public void ValidateGraph()
    {
        ValidationMessages.Clear();
        foreach (var message in _validator.Validate(Graph))
        {
            ValidationMessages.Add(message);
        }
    }

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

    [RelayCommand]
    public void StopPreview() => _previewCancellation?.Cancel();

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

    [RelayCommand]
    public void ClearExecutionLog() => ExecutionLog.Clear();

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

    private FrontedNodeEditorViewModel CreateNode(FrontedNode node) =>
        new(node, _catalog.Find(node.NodeType), MarkDirtyAndSetIsDirty, ValidateGraph, RefreshParallelNode, _localize, _targetOptions, _conditionFieldOptions);

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

    private void MarkDirtyAndSetIsDirty()
    {
        _markDirty();
        IsDirty = true;
    }

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

    private void Changed()
    {
        _markDirty();
        IsDirty = true;
        ValidateGraph();
        OnPropertyChanged(nameof(FilteredCatalog));
    }

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
    /// Sets the save action to be invoked when <see cref="SaveAsync"/> is called.
    /// This allows post-construction wiring of the save delegate (e.g. from an animation editor).
    /// </summary>
    /// <param name="saveAsync">The asynchronous save action to set.</param>
    public void SetSaveAction(Func<Task<bool>>? saveAsync)
    {
        _saveAsync = saveAsync;
    }

    /// <summary>
    /// Clears editor-local dirty state without saving the graph.
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
        IReadOnlyList<FrontedGraphConditionFieldOptionViewModel> conditionFieldOptions)
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
            .Select(property => new FrontedNodePropertyEditorViewModel(model, property, markDirty, validate, localize, targetOptions, conditionFieldOptions))
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
    /// <summary>Gets a readable summary for nodes with user-editable expressions.</summary>
    private readonly IReadOnlyList<FrontedGraphConditionFieldOptionViewModel> ConditionFieldOptions;
    private readonly Func<string, string, string> _localize;
    public string Summary => Model.NodeType == "flow.if"
        ? $"{_localize("Designer.Graph.Condition.If", "IF")} {ConditionFieldDisplayName(ReadProperty("Left"))} {OperatorSymbol(ReadProperty("Operator"))} {ReadProperty("Right")}".TrimEnd()
        : string.Empty;
    /// <summary>Gets the stable raw expression shown in the node tooltip.</summary>
    public string RawSummary => Model.NodeType == "flow.if"
        ? $"{ReadProperty("Left")} {ReadProperty("Operator")} {ReadProperty("Right")}".TrimEnd()
        : Description;
    /// <summary>Gets whether the node has a readable expression summary.</summary>
    public bool HasSummary => !string.IsNullOrWhiteSpace(Summary);
    /// <summary>Gets the node card header text.</summary>
    public string HeaderText => HasSummary ? Summary : DisplayName;
    /// <summary>Gets the rendered node card width.</summary>
    public double CardWidth { get; }
    /// <summary>Gets the approximate rendered node card height used for canvas bounds and selection.</summary>
    public double CardHeight => Math.Max(
        80D,
        InputPorts.Concat(OutputPorts).Select(port => port.CenterOffsetY).DefaultIfEmpty(56D).Max() + 24D);
    public IReadOnlyList<FrontedNodePortViewModel> InputPorts { get; }
    public IReadOnlyList<FrontedNodePortViewModel> OutputPorts { get; }
    public IReadOnlyList<FrontedNodePropertyEditorViewModel> Properties { get; }

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
/// Describes the editor-only semantic role of a node port.
/// </summary>
public enum FrontedNodePortRole
{
    /// <summary>A regular port without specialized graph meaning.</summary>
    Default,

    /// <summary>A branch output on a flow.parallel node.</summary>
    ParallelBranch,

    /// <summary>The continuation output that runs after all connected flow.parallel branches finish.</summary>
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

    /// <summary>Gets the visible port label.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the semantic role used for editor styling and help text.</summary>
    public FrontedNodePortRole Role { get; }

    /// <summary>Gets a value indicating whether this port is a parallel continuation port.</summary>
    public bool IsParallelContinuation => Role == FrontedNodePortRole.ParallelContinuation;

    /// <summary>Gets a value indicating whether this port is a parallel branch port.</summary>
    public bool IsParallelBranch => Role == FrontedNodePortRole.ParallelBranch;

    /// <summary>Gets the margin applied before this port row.</summary>
    public Thickness RowMargin { get; }

    /// <summary>Gets the port center offset from the node card top.</summary>
    public double CenterOffsetY { get; }

    /// <summary>Gets tooltip text that explains the port semantics.</summary>
    public string TooltipText { get; }

    /// <summary>Gets a short meaning description for connection inspection.</summary>
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
            return "#1976D2"; // Blue（更饱和，与 Control 明显区分）

        return descriptor.ValueType switch
        {
            FrontedNodePortValueType.Number => "#43A047",  // Green
            FrontedNodePortValueType.String => "#8E24AA",  // Purple
            FrontedNodePortValueType.Boolean => "#FB8C00", // Orange
            FrontedNodePortValueType.Color => "#E53935",   // Red（从粉色改为红色，与 String 区分）
            FrontedNodePortValueType.Control => "#00897B", // Teal（从青色改为青绿，与 Flow 明显区分）
            FrontedNodePortValueType.Object => "#757575",  // Gray
            _ => "#757575"                                  // Gray (unknown)
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
    /// <summary>Gets the source port view model for this connection.</summary>
    public FrontedNodePortViewModel? SourcePort => Source.OutputPorts.FirstOrDefault(port => port.Name == Model.SourcePort);
    /// <summary>Gets the target port view model for this connection.</summary>
    public FrontedNodePortViewModel? TargetPort => Target.InputPorts.FirstOrDefault(port => port.Name == Model.TargetPort);
    /// <summary>Gets the visible source port name.</summary>
    public string SourcePortDisplayName => SourcePort?.DisplayName ?? Model.SourcePort;
    /// <summary>Gets the visible target port name.</summary>
    public string TargetPortDisplayName => TargetPort?.DisplayName ?? Model.TargetPort;
    /// <summary>Gets the semantic meaning of the source side of the connection.</summary>
    public string Meaning => SourcePort?.Meaning ?? SourcePortDisplayName;
    /// <summary>Gets the connection stroke color derived from the source port role.</summary>
    public string StrokeColorHex => SourcePort?.PortColorHex ?? "#1976D2";
    /// <summary>Gets the connection stroke thickness derived from the source port role.</summary>
    public double StrokeThickness => SourcePort?.IsParallelContinuation == true ? 4D : 3D;
    /// <summary>Gets the connection inspection text shown while hovering the connection.</summary>
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
        IReadOnlyList<FrontedGraphConditionFieldOptionViewModel>? conditionFieldOptions = null)
    {
        _node = node;
        Descriptor = descriptor;
        _markDirty = markDirty;
        _validate = validate;
        _targetOptions = targetOptions;
        _conditionFieldOptions = conditionFieldOptions ?? [];
        _localize = localize;
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
    /// <summary>Gets contextual input guidance for the current property.</summary>
    public string Placeholder => DynamicMetadata?.Placeholder ?? string.Empty;
    /// <summary>Gets contextual help for the current property.</summary>
    public string HelpText => DynamicMetadata is null
        ? Description
        : _localize(DynamicMetadata.DescriptionKey, $"{DynamicMetadata.Placeholder}; example: {DynamicMetadata.Example}");
    public bool IsBoolean => Descriptor.EditorKind == FrontedNodePropertyEditorKind.Boolean;
    public bool IsEnum => Descriptor.EditorKind == FrontedNodePropertyEditorKind.Enum && !IsConditionOperator;
    public bool IsNumber => Descriptor.EditorKind == FrontedNodePropertyEditorKind.Number || IsNumericDynamicValue || IsNumericConditionValue;
    /// <summary>Gets whether this numeric property can use a NumberBox without losing percentage expressions.</summary>
    public bool IsNumberBox => IsNumber && !FrontedBehaviorPropertyMetadata.SupportsPercentage(EffectivePropertyNameForValidation());
    /// <summary>Gets whether this numeric property must retain text editing for percentage expressions.</summary>
    public bool IsPercentageNumberText => IsNumber && !IsNumberBox;
    public bool IsColor => Descriptor.EditorKind == FrontedNodePropertyEditorKind.Color || IsColorDynamicValue;
    public bool IsControlReference => Descriptor.EditorKind == FrontedNodePropertyEditorKind.ControlReference;
    public bool IsPropertyName => Descriptor.EditorKind == FrontedNodePropertyEditorKind.PropertyName;
    public bool IsVisibilityValue => IsDynamicValue && FrontedBehaviorPropertyMetadata.IsVisibilityProperty(CurrentBehaviorPropertyName);
    public bool HasTextSuggestions => !IsConditionProperty && !IsBoolean && !IsEnum && !IsNumber && !IsColor && !IsControlReference && !IsPropertyName && Descriptor.Options.Count > 0;
    public bool IsText => !IsConditionProperty && !IsBoolean && !IsEnum && !IsNumber && !IsColor && !IsControlReference && !IsPropertyName && !HasTextSuggestions && !IsVisibilityValue;
    /// <summary>Gets whether this property selects the left-side condition field.</summary>
    public bool IsConditionField => _node.NodeType == "flow.if" && Descriptor.Name == "Left";
    /// <summary>Gets whether this property selects the condition operator.</summary>
    public bool IsConditionOperator => _node.NodeType == "flow.if" && Descriptor.Name == "Operator";
    /// <summary>Gets whether this property edits the right-side condition value.</summary>
    public bool IsConditionValue => _node.NodeType == "flow.if" && Descriptor.Name == "Right";
    /// <summary>Gets whether the selected condition value is boolean-like.</summary>
    public bool IsBooleanConditionValue => IsConditionValue && IsBooleanType(SelectedConditionField?.TypeName);
    /// <summary>Gets whether the selected condition value is enum-like.</summary>
    public bool IsEnumConditionValue => IsConditionValue && SelectedConditionField?.EnumValues.Count > 0;
    /// <summary>Gets whether the selected condition value is numeric.</summary>
    public bool IsNumericConditionValue => IsConditionValue && IsNumericType(SelectedConditionField?.TypeName);
    /// <summary>Gets whether the condition value should use free text.</summary>
    public bool IsTextConditionValue => IsConditionValue && !IsBooleanConditionValue && !IsEnumConditionValue && !IsNumericConditionValue;
    /// <summary>Gets context-aware event fields available to this condition.</summary>
    public IReadOnlyList<FrontedGraphConditionFieldOptionViewModel> ConditionFieldOptions => EnsureCurrentConditionFieldOption();
    /// <summary>Gets context-aware operators available for the selected field type.</summary>
    public IReadOnlyList<FrontedNodePropertyOptionViewModel> ConditionOperatorOptions => ResolveConditionOperatorOptions();
    /// <summary>Gets stable bool or enum values available for the selected field.</summary>
    public IReadOnlyList<FrontedNodePropertyOptionViewModel> ConditionValueOptions => ResolveConditionValueOptions();
    public IReadOnlyList<string> Options => Descriptor.Options;
    public IReadOnlyList<FrontedNodePropertyOptionViewModel> LocalizedOptions => _localizedOptions;
    /// <summary>Gets the stable boolean choices for boolean property editors.</summary>
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

    /// <summary>Gets or sets the numeric value edited by WPF-UI NumberBox.</summary>
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

    /// <summary>Gets the minimum value displayed by NumberBox.</summary>
    public double NumberMinimum => _node.NodeType == "flow.parallel" && Descriptor.Name == "BranchCount"
        ? FrontedParallelNodePorts.MinBranchCount
        : Descriptor.Name == "DurationMs"
            ? 0D
            : DynamicMetadata?.Min ?? double.MinValue;

    /// <summary>Gets the maximum value displayed by NumberBox.</summary>
    public double NumberMaximum => _node.NodeType == "flow.parallel" && Descriptor.Name == "BranchCount"
        ? FrontedParallelNodePorts.MaxBranchCount
        : DynamicMetadata?.Max ?? double.MaxValue;

    /// <summary>Gets the NumberBox decimal-place limit.</summary>
    public int NumberMaxDecimalPlaces => RequiresIntegerNumber ? 0 : 6;

    /// <summary>Gets or sets the boolean value as a stable lowercase string for ComboBox editing.</summary>
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

    /// <summary>Gets or sets a stable condition field path.</summary>
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

    /// <summary>Gets or sets a stable condition operator name.</summary>
    public string ConditionOperatorValue
    {
        get => TextValue;
        set => TextValue = value;
    }

    /// <summary>Gets or sets a stable typed condition choice.</summary>
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
        var current = IsConditionField ? TextValue : ReadNodeString("Left");
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

    private bool IsConditionProperty => IsConditionField || IsConditionOperator || IsConditionValue;

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
    /// Validates a NumberBox value against graph property metadata.
    /// </summary>
    /// <param name="value">Candidate numeric value.</param>
    /// <param name="context">Validation context containing the property editor.</param>
    /// <returns>The validation result.</returns>
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
/// Target option displayed by behavior graph target editors.
/// </summary>
/// <param name="Value">The persisted target reference value.</param>
/// <param name="DisplayName">The user-facing target display name.</param>
public sealed record FrontedNodeTargetOptionViewModel(string Value, string DisplayName);

/// <summary>
/// Event payload field available to a context-aware graph condition editor.
/// </summary>
/// <param name="ValuePath">Stable condition path persisted in the graph.</param>
/// <param name="DisplayText">User-facing localized field label plus stable path.</param>
/// <param name="Description">Localized field description.</param>
/// <param name="TypeName">Payload value type name.</param>
/// <param name="EnumValues">Stable enum names accepted by the field.</param>
/// <param name="EventType">Event type that contributes the field, when useful for disambiguation.</param>
/// <param name="LocalizedDisplayName">Localized field label without the stable path.</param>
public sealed record FrontedGraphConditionFieldOptionViewModel(
    string ValuePath,
    string DisplayText,
    string Description,
    string TypeName,
    IReadOnlyList<string> EnumValues,
    string? EventType,
    string LocalizedDisplayName);

/// <summary>
/// Option displayed by node property editors while preserving a stable stored value.
/// </summary>
/// <param name="Value">The value stored in node JSON.</param>
/// <param name="DisplayName">The localized option label shown to the user.</param>
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
