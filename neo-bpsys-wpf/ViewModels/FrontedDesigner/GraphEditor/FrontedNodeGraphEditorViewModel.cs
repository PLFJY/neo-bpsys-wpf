using System.Collections.ObjectModel;
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
    private readonly Func<string, string, string> _localize;
    private readonly IReadOnlyList<FrontedNodeTargetOptionViewModel> _targetOptions;
    private CancellationTokenSource? _previewCancellation;
    private FrontedNodePortViewModel? _pendingPort;
    private readonly Stack<string> _undoStack = new();
    private readonly Stack<string> _redoStack = new();
    private bool _isRestoring;
    private bool _isDragging;
    private const int UndoStackLimit = 50;
    private readonly Action? _save;

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
        IReadOnlyList<FrontedNodeTargetOptionViewModel>? targetOptions = null)
    {
        Graph = graph;
        _catalog = catalog ?? new FrontedNodeCatalog();
        _validator = validator ?? new FrontedNodeGraphValidator(_catalog);
        _runtime = runtime ?? new FrontedNodeGraphRuntime(_catalog, _validator);
        _animationRuntime = animationRuntime;
        _createAnimationContext = createAnimationContext;
        _markDirty = markDirty ?? (() => { });
        _localize = localize ?? ((_, fallback) => fallback);
        _save = save ?? (() => { });
        _targetOptions = targetOptions ?? [new FrontedNodeTargetOptionViewModel("Self", _localize("Designer.Graph.Target.Self", "Self"))];
        Catalog = _catalog.Nodes
            .Where(IsAnimationEditorCatalogNode)
            .Select(descriptor => new FrontedNodeCatalogItemViewModel(descriptor, _localize))
            .ToArray();
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
    private FrontedNodeEditorViewModel? _selectedNode;

    /// <summary>是否可以撤销</summary>
    [ObservableProperty]
    private bool _canUndo;

    /// <summary>是否可以重做</summary>
    [ObservableProperty]
    private bool _canRedo;

    /// <summary>是否有未保存的更改</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isDirty;

    [ObservableProperty]
    private string _catalogSearchText = string.Empty;

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
        AddNodeAt(nodeType, 40 + Nodes.Count * 20, 40 + Nodes.Count * 20);
        SelectedNodes.Clear();
        if (Nodes.Count > 0)
        {
            SelectedNodes.Add(Nodes[^1]);
        }
    }

    public void AddNodeAt(string? nodeType, double x, double y)
    {
        if (string.IsNullOrWhiteSpace(nodeType) || _catalog.Find(nodeType) is null)
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

        CreateSnapshot();
        var clones = new List<FrontedNodeEditorViewModel>();
        foreach (var node in nodesToClone)
        {
            var source = node.Model;
            var clone = new FrontedNode
            {
                NodeType = source.NodeType,
                DisplayName = source.DisplayName,
                X = source.X + 30,
                Y = source.Y + 30,
                Properties = source.Properties.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal)
            };
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
        const double nodeHeight = 80;
        foreach (var node in Nodes)
        {
            var nodeRect = new Rect(node.X, node.Y, FrontedNodeEditorViewModel.Width, nodeHeight);
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
        const double nodeHeight = 80;
        foreach (var node in Nodes)
        {
            var nodeRect = new Rect(node.X, node.Y, FrontedNodeEditorViewModel.Width, nodeHeight);
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
        if (port?.Descriptor.PortKind is not (FrontedNodePortKind.FlowOut or FrontedNodePortKind.ValueOut))
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

        var compatible = FrontedNodePortViewModel.ArePortsCompatible(_pendingPort.Descriptor, port.Descriptor)
                         && Graph.GetIncoming(port.Node.Model.NodeId, port.Descriptor.Name).Count == 0;
        if (compatible)
        {
            AddConnection(_pendingPort, port);
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
        if (!FrontedNodePortViewModel.ArePortsCompatible(source.Descriptor, target.Descriptor)
            || Graph.GetOutgoing(source.Node.Model.NodeId, source.Descriptor.Name).Count > 0
            || Graph.GetIncoming(target.Node.Model.NodeId, target.Descriptor.Name).Count > 0)
        {
            return false;
        }

        CreateSnapshot();
        var model = new FrontedNodeConnection
        {
            SourceNodeId = source.Node.Model.NodeId,
            SourcePort = source.Descriptor.Name,
            TargetNodeId = target.Node.Model.NodeId,
            TargetPort = target.Descriptor.Name
        };
        Graph.Connections.Add(model);
        Connections.Add(new FrontedNodeConnectionViewModel(model, source.Node, target.Node));
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
                    && FrontedNodePortViewModel.ArePortsCompatible(pendingPort.Descriptor, port.Descriptor)
                    && Graph.GetIncoming(node.Model.NodeId, port.Descriptor.Name).Count == 0;
                port.IsHighlighted = compatible;
                port.IsDimmed = pendingPort is not null && !compatible;
            }

            foreach (var port in node.OutputPorts)
            {
                var compatible = pendingPort is not null
                    && FrontedNodePortViewModel.ArePortsCompatible(pendingPort.Descriptor, port.Descriptor)
                    && Graph.GetOutgoing(node.Model.NodeId, port.Descriptor.Name).Count == 0;
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
        new(node, _catalog.Find(node.NodeType), _markDirty, ValidateGraph, _localize, _targetOptions);

    private void ReloadConnections()
    {
        Connections.Clear();
        foreach (var model in Graph.Connections)
        {
            var source = Nodes.FirstOrDefault(node => node.Model.NodeId == model.SourceNodeId);
            var target = Nodes.FirstOrDefault(node => node.Model.NodeId == model.TargetNodeId);
            if (source is not null && target is not null)
            {
                Connections.Add(new FrontedNodeConnectionViewModel(model, source, target));
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
        CanvasHeight = Math.Max(1400, Nodes.Select(node => node.Y).DefaultIfEmpty(0).Max() + 360);
    }

    /// <summary>创建当前图快照（用于撤销）</summary>
    private void CreateSnapshot()
    {
        if (_isRestoring || _isDragging)
        {
            return;
        }

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
    public void Save()
    {
        _save();
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
        Reload();
        ClearIsSelected();
        SelectedNodes.Clear();
        SelectedNode = null;
        Changed();
    }

    private static bool IsAnimationEditorCatalogNode(FrontedNodeTypeDescriptor descriptor) =>
        descriptor.NodeType is not ("value.eventValue" or "value.selfTag");

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
    private readonly Action _markDirty;
    private readonly Action _validate;

    public FrontedNodeEditorViewModel(
        FrontedNode model,
        FrontedNodeTypeDescriptor? descriptor,
        Action markDirty,
        Action validate,
        Func<string, string, string> localize,
        IReadOnlyList<FrontedNodeTargetOptionViewModel> targetOptions)
    {
        Model = model;
        Descriptor = descriptor;
        _markDirty = markDirty;
        _validate = validate;
        DisplayName = descriptor is null ? model.NodeType : localize(descriptor.DisplayNameKey, NodeFallback(model.NodeType));
        Description = descriptor is null ? model.NodeType : localize(descriptor.DescriptionKey, model.NodeType);
        InputPorts = descriptor?.InputPorts.Select((port, index) => new FrontedNodePortViewModel(this, port, index, localize)).ToArray() ?? [];
        OutputPorts = descriptor?.OutputPorts.Select((port, index) => new FrontedNodePortViewModel(this, port, index, localize)).ToArray() ?? [];
        var properties = descriptor?.Properties
            .Select(property => new FrontedNodePropertyEditorViewModel(model, property, markDirty, validate, localize, targetOptions))
            .ToArray() ?? [];
        Properties = properties;
        foreach (var property in properties)
        {
            property.SetRefreshRelatedProperties(() =>
            {
                foreach (var item in properties)
                {
                    item.RefreshEditorState();
                }
            });
        }
    }

    public FrontedNode Model { get; }
    public FrontedNodeTypeDescriptor? Descriptor { get; }
    public string DisplayName { get; }
    public string Description { get; }
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

    private static string NodeFallback(string nodeType) => nodeType.Split('.').LastOrDefault() ?? nodeType;
}

public sealed partial class FrontedNodePortViewModel : ObservableObject
{
    private readonly Func<string, string, string>? _localize;

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

    public FrontedNodePortViewModel(FrontedNodeEditorViewModel node, FrontedNodePortDescriptor descriptor, int index, Func<string, string, string>? localize = null)
    {
        Node = node;
        Descriptor = descriptor;
        Index = index;
        _localize = localize;

        IsFlowPort = descriptor.PortKind is FrontedNodePortKind.FlowIn or FrontedNodePortKind.FlowOut;

        PortKindName = IsFlowPort
            ? Localize("Designer.Graph.PortKind.Flow", "Flow")
            : Localize("Designer.Graph.PortKind.Value", "Value");

        ValueTypeName = GetValueTypeDisplayName(descriptor.ValueType);
        PortColorHex = GetPortColor(descriptor);
    }

    /// <summary>
    /// 判断两个端口是否可以建立连接。委托到 <see cref="FrontedNodePortDescriptor.AreCompatible"/>。
    /// </summary>
    public static bool ArePortsCompatible(FrontedNodePortDescriptor source, FrontedNodePortDescriptor target) =>
        FrontedNodePortDescriptor.AreCompatible(source, target);

    /// <summary>获取端口在端口类型维度上的颜色映射</summary>
    public static string GetPortColor(FrontedNodePortDescriptor descriptor)
    {
        if (descriptor.PortKind is FrontedNodePortKind.FlowIn or FrontedNodePortKind.FlowOut)
            return "#4FC3F7"; // Blue

        return descriptor.ValueType switch
        {
            FrontedNodePortValueType.Number => "#66BB6A",  // Green
            FrontedNodePortValueType.String => "#AB47BC",  // Purple
            FrontedNodePortValueType.Boolean => "#FFA726", // Orange
            FrontedNodePortValueType.Color => "#EC407A",   // Pink
            FrontedNodePortValueType.Control => "#26C6DA", // Cyan
            FrontedNodePortValueType.Object => "#9E9E9E",  // Gray
            _ => "#9E9E9E"                                  // Gray (unknown)
        };
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
    FrontedNodeEditorViewModel target) : ObservableObject
{
    public FrontedNodeConnection Model { get; } = model;
    public FrontedNodeEditorViewModel Source { get; } = source;
    public FrontedNodeEditorViewModel Target { get; } = target;
    public string Summary => $"{Source.DisplayName}.{Model.SourcePort} -> {Target.DisplayName}.{Model.TargetPort}";
    public double X1 => Source.X + FrontedNodeEditorViewModel.Width;
    public double Y1 => Source.Y + 54 + Math.Max(0, Source.OutputPorts.ToList().FindIndex(port => port.Name == Model.SourcePort)) * 24;
    public double X2 => Target.X;
    public double Y2 => Target.Y + 54 + Math.Max(0, Target.InputPorts.ToList().FindIndex(port => port.Name == Model.TargetPort)) * 24;

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
    }
}

public sealed partial class FrontedNodePropertyEditorViewModel : ObservableObject
{
    private readonly FrontedNode _node;
    private readonly Action _markDirty;
    private readonly Action _validate;
    private readonly IReadOnlyList<FrontedNodeTargetOptionViewModel> _targetOptions;
    private readonly Func<string, string, string> _localize;
    private readonly IReadOnlyList<FrontedNodePropertyOptionViewModel> _localizedOptions;
    private readonly IReadOnlyList<FrontedNodePropertyOptionViewModel> _visibilityOptions;
    private Action? _refreshRelatedProperties;
    private string? _validationError;
    private Color _colorValue = Colors.White;

    public FrontedNodePropertyEditorViewModel(
        FrontedNode node,
        FrontedNodePropertyDescriptor descriptor,
        Action markDirty,
        Action validate,
        Func<string, string, string> localize,
        IReadOnlyList<FrontedNodeTargetOptionViewModel> targetOptions)
    {
        _node = node;
        Descriptor = descriptor;
        _markDirty = markDirty;
        _validate = validate;
        _targetOptions = targetOptions;
        _localize = localize;
        DisplayName = localize(descriptor.DisplayNameKey, descriptor.Name);
        Description = localize($"{descriptor.DisplayNameKey}.Description", descriptor.Name);
        _localizedOptions = descriptor.Options
            .Select(option => new FrontedNodePropertyOptionViewModel(option, LocalizeOption(option)))
            .ToArray();
        _visibilityOptions = FrontedBehaviorPropertyMetadata.VisibilityOptions
            .Select(option => new FrontedNodePropertyOptionViewModel(option, localize($"Designer.Option.Visibility.{option}", option)))
            .ToArray();
        if (ColorHelper.TryParseColor(TextValue, out var color))
        {
            _colorValue = color;
        }
    }

    public FrontedNodePropertyDescriptor Descriptor { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public bool IsBoolean => Descriptor.EditorKind == FrontedNodePropertyEditorKind.Boolean;
    public bool IsEnum => Descriptor.EditorKind == FrontedNodePropertyEditorKind.Enum;
    public bool IsNumber => Descriptor.EditorKind == FrontedNodePropertyEditorKind.Number || IsNumericDynamicValue;
    public bool IsColor => Descriptor.EditorKind == FrontedNodePropertyEditorKind.Color || IsColorDynamicValue;
    public bool IsControlReference => Descriptor.EditorKind == FrontedNodePropertyEditorKind.ControlReference;
    public bool IsPropertyName => Descriptor.EditorKind == FrontedNodePropertyEditorKind.PropertyName;
    public bool IsVisibilityValue => IsDynamicValue && FrontedBehaviorPropertyMetadata.IsVisibilityProperty(CurrentBehaviorPropertyName);
    public bool HasTextSuggestions => !IsBoolean && !IsEnum && !IsNumber && !IsColor && !IsControlReference && !IsPropertyName && Descriptor.Options.Count > 0;
    public bool IsText => !IsBoolean && !IsEnum && !IsNumber && !IsColor && !IsControlReference && !IsPropertyName && !HasTextSuggestions && !IsVisibilityValue;
    public IReadOnlyList<string> Options => Descriptor.Options;
    public IReadOnlyList<FrontedNodePropertyOptionViewModel> LocalizedOptions => _localizedOptions;
    public IReadOnlyList<FrontedNodePropertyOptionViewModel> VisibilityOptions => _visibilityOptions;
    public IReadOnlyList<FrontedNodeTargetOptionViewModel> TargetOptions => EnsureCurrentTargetOption();
    public string? Unit => IsRotation ? "°" : Descriptor.Unit;
    public bool HasUnit => !string.IsNullOrWhiteSpace(Unit);
    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationError);
    public string? ValidationError
    {
        get => _validationError;
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
            if (!ValidateTextValue(value, out var normalized))
            {
                OnPropertyChanged(nameof(TextValue));
                return;
            }

            Write(Descriptor.PropertyType == FrontedNodePropertyType.Number && double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                ? JsonSerializer.SerializeToElement(number)
                : JsonSerializer.SerializeToElement(normalized));
        }
    }

    public bool BooleanValue
    {
        get => Read().ValueKind == JsonValueKind.True;
        set => Write(JsonSerializer.SerializeToElement(value));
    }

    public string EnumValue
    {
        get => TextValue;
        set => Write(JsonSerializer.SerializeToElement(value));
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
        OnPropertyChanged(nameof(IsColor));
        OnPropertyChanged(nameof(IsVisibilityValue));
        OnPropertyChanged(nameof(IsText));
        OnPropertyChanged(nameof(HasTextSuggestions));
        OnPropertyChanged(nameof(Unit));
        OnPropertyChanged(nameof(HasUnit));
    }

    private JsonElement Read() => _node.Properties.TryGetValue(Descriptor.Name, out var value) ? value : Descriptor.DefaultValue;

    private void Write(JsonElement value)
    {
        _node.Properties[Descriptor.Name] = value;
        _markDirty();
        _validate();
        ValidationError = null;
        if (IsColor && ColorHelper.TryParseColor(TextValue, out var color))
        {
            SetProperty(ref _colorValue, color, nameof(ColorValue));
        }
        OnPropertyChanged(nameof(TextValue));
        OnPropertyChanged(nameof(BooleanValue));
        OnPropertyChanged(nameof(EnumValue));
        OnPropertyChanged(nameof(TargetValue));
        OnPropertyChanged(nameof(PropertyNameValue));
        OnPropertyChanged(nameof(PropertyNameText));
        OnPropertyChanged(nameof(SuggestionText));
        OnPropertyChanged(nameof(VisibilityValue));
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
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
            {
                ValidationError = "Value must be a finite number.";
                return false;
            }

            if (!FrontedBehaviorPropertyMetadata.TryValidateValue(EffectivePropertyNameForValidation(), value, out var message))
            {
                ValidationError = message;
                return false;
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
        _localizedOptions.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.Ordinal))?.DisplayName
        ?? value;

    private string ValueForDisplay(string? display)
    {
        var value = display ?? string.Empty;
        return _localizedOptions.FirstOrDefault(option =>
                   string.Equals(option.DisplayName, value, StringComparison.Ordinal)
                   || string.Equals(option.Value, value, StringComparison.Ordinal))?.Value
               ?? value;
    }

    private string? CurrentBehaviorPropertyName =>
        _node.Properties.TryGetValue("PropertyName", out var property)
            ? property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString()
            : null;

    private bool IsDynamicValue => Descriptor.Name is "Value" or "From" or "To";
    private bool IsColorDynamicValue => IsDynamicValue && FrontedBehaviorPropertyMetadata.IsColorProperty(CurrentBehaviorPropertyName);
    private bool IsNumericDynamicValue => IsDynamicValue && FrontedBehaviorPropertyMetadata.IsNumericProperty(CurrentBehaviorPropertyName);
    private bool IsRotation => string.Equals(Descriptor.Name, "Rotation", StringComparison.OrdinalIgnoreCase)
        || string.Equals(CurrentBehaviorPropertyName, "Rotation", StringComparison.OrdinalIgnoreCase);
    private string? EffectivePropertyNameForValidation() =>
        IsDynamicValue ? CurrentBehaviorPropertyName : Descriptor.Name;
}

/// <summary>
/// Target option displayed by behavior graph target editors.
/// </summary>
/// <param name="Value">The persisted target reference value.</param>
/// <param name="DisplayName">The user-facing target display name.</param>
public sealed record FrontedNodeTargetOptionViewModel(string Value, string DisplayName);

/// <summary>
/// Option displayed by node property editors while preserving a stable stored value.
/// </summary>
/// <param name="Value">The value stored in node JSON.</param>
/// <param name="DisplayName">The localized option label shown to the user.</param>
public sealed record FrontedNodePropertyOptionViewModel(string Value, string DisplayName);

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
