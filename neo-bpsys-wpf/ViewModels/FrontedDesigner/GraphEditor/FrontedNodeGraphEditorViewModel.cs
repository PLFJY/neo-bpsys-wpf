using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;

namespace neo_bpsys_wpf.ViewModels.FrontedDesigner.GraphEditor;

public sealed partial class FrontedNodeGraphEditorViewModel : ObservableObject
{
    private readonly FrontedNodeCatalog _catalog;
    private readonly FrontedNodeGraphValidator _validator;
    private readonly IFrontedNodeGraphRuntime _runtime;
    private readonly Action _markDirty;
    private readonly Func<string, string, string> _localize;
    private CancellationTokenSource? _previewCancellation;
    private FrontedNodePortViewModel? _pendingPort;

    public FrontedNodeGraphEditorViewModel(
        FrontedNodeGraph graph,
        FrontedNodeCatalog? catalog = null,
        FrontedNodeGraphValidator? validator = null,
        IFrontedNodeGraphRuntime? runtime = null,
        Action? markDirty = null,
        Func<string, string, string>? localize = null)
    {
        Graph = graph;
        _catalog = catalog ?? new FrontedNodeCatalog();
        _validator = validator ?? new FrontedNodeGraphValidator(_catalog);
        _runtime = runtime ?? new FrontedNodeGraphRuntime(_catalog, _validator);
        _markDirty = markDirty ?? (() => { });
        _localize = localize ?? ((_, fallback) => fallback);
        Catalog = _catalog.Nodes
            .Where(IsAnimationEditorCatalogNode)
            .Select(descriptor => new FrontedNodeCatalogItemViewModel(descriptor, _localize))
            .ToArray();
        Reload();
        ValidateGraph();
    }

    public FrontedNodeGraph Graph { get; }
    public ObservableCollection<FrontedNodeEditorViewModel> Nodes { get; } = [];
    public ObservableCollection<FrontedNodeConnectionViewModel> Connections { get; } = [];
    public ObservableCollection<FrontedNodeGraphValidationMessage> ValidationMessages { get; } = [];
    public ObservableCollection<FrontedGraphExecutionLogItem> ExecutionLog { get; } = [];
    public IReadOnlyList<FrontedNodeCatalogItemViewModel> Catalog { get; }

    [ObservableProperty]
    private FrontedNodeEditorViewModel? _selectedNode;

    [ObservableProperty]
    private string _catalogSearchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionStatus))]
    private bool _isConnecting;

    [ObservableProperty]
    private bool _isPreviewRunning;

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
    }

    public void AddNodeAt(string? nodeType, double x, double y)
    {
        if (string.IsNullOrWhiteSpace(nodeType) || _catalog.Find(nodeType) is null)
        {
            return;
        }

        var model = _catalog.CreateNode(nodeType, Math.Max(0, x), Math.Max(0, y));
        Graph.Nodes.Add(model);
        var viewModel = CreateNode(model);
        Nodes.Add(viewModel);
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
        DeleteNode(SelectedNode);
    }

    public void DeleteNode(FrontedNodeEditorViewModel? node)
    {
        if (node is null || !Graph.RemoveNode(node.Model.NodeId))
        {
            return;
        }

        Nodes.Remove(node);
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
        if (SelectedNode is null)
        {
            return;
        }

        var source = SelectedNode.Model;
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
        SelectedNode = viewModel;
        UpdateCanvasSize();
        Changed();
    }

    [RelayCommand]
    public void SelectNode(FrontedNodeEditorViewModel? node) => SelectedNode = node;

    public void MoveNode(FrontedNodeEditorViewModel node, double x, double y)
    {
        node.X = Math.Max(0, x);
        node.Y = Math.Max(0, y);
        foreach (var connection in Connections.Where(connection => connection.Source == node || connection.Target == node))
        {
            connection.Refresh();
        }
        UpdateCanvasSize();
        Changed();
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
    }

    [RelayCommand]
    public void CompleteConnection(FrontedNodePortViewModel? port)
    {
        if (_pendingPort is null || port is null)
        {
            return;
        }

        AddConnection(_pendingPort, port);
        _pendingPort = null;
        IsConnecting = false;
    }

    public bool AddConnection(FrontedNodePortViewModel source, FrontedNodePortViewModel target)
    {
        var compatible = source.Descriptor.PortKind == FrontedNodePortKind.FlowOut && target.Descriptor.PortKind == FrontedNodePortKind.FlowIn
                         || source.Descriptor.PortKind == FrontedNodePortKind.ValueOut && target.Descriptor.PortKind == FrontedNodePortKind.ValueIn;
        if (!compatible
            || Graph.GetOutgoing(source.Node.Model.NodeId, source.Descriptor.Name).Count > 0
            || Graph.GetIncoming(target.Node.Model.NodeId, target.Descriptor.Name).Count > 0)
        {
            return false;
        }

        var model = new FrontedNodeConnection
        {
            SourceNodeId = source.Node.Model.NodeId,
            SourcePort = source.Descriptor.Name,
            TargetNodeId = target.Node.Model.NodeId,
            TargetPort = target.Descriptor.Name
        };
        Graph.Connections.Add(model);
        Connections.Add(new FrontedNodeConnectionViewModel(model, source.Node, target.Node));
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
        Connections.Remove(connection);
        Changed();
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
            var result = await _runtime.ExecuteAsync(Graph, new FrontedGraphExecutionContext(), _previewCancellation.Token);
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
        new(node, _catalog.Find(node.NodeType), _markDirty, ValidateGraph, _localize);

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
    }

    private void Changed()
    {
        _markDirty();
        ValidateGraph();
        OnPropertyChanged(nameof(FilteredCatalog));
    }

    private void UpdateCanvasSize()
    {
        CanvasWidth = Math.Max(2200, Nodes.Select(node => node.X).DefaultIfEmpty(0).Max() + 520);
        CanvasHeight = Math.Max(1400, Nodes.Select(node => node.Y).DefaultIfEmpty(0).Max() + 360);
    }

    private static bool IsAnimationEditorCatalogNode(FrontedNodeTypeDescriptor descriptor) =>
        descriptor.NodeType is not ("value.eventValue" or "value.selfTag");
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
        Func<string, string, string> localize)
    {
        Model = model;
        Descriptor = descriptor;
        _markDirty = markDirty;
        _validate = validate;
        DisplayName = descriptor is null ? model.NodeType : localize(descriptor.DisplayNameKey, NodeFallback(model.NodeType));
        Description = descriptor is null ? model.NodeType : localize(descriptor.DescriptionKey, model.NodeType);
        InputPorts = descriptor?.InputPorts.Select((port, index) => new FrontedNodePortViewModel(this, port, index)).ToArray() ?? [];
        OutputPorts = descriptor?.OutputPorts.Select((port, index) => new FrontedNodePortViewModel(this, port, index)).ToArray() ?? [];
        Properties = descriptor?.Properties.Select(property => new FrontedNodePropertyEditorViewModel(model, property, markDirty, validate, localize)).ToArray() ?? [];
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

    private static string NodeFallback(string nodeType) => nodeType.Split('.').LastOrDefault() ?? nodeType;
}

public sealed class FrontedNodePortViewModel(FrontedNodeEditorViewModel node, FrontedNodePortDescriptor descriptor, int index)
{
    public FrontedNodeEditorViewModel Node { get; } = node;
    public FrontedNodePortDescriptor Descriptor { get; } = descriptor;
    public int Index { get; } = index;
    public string Name => Descriptor.Name;
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
    public double MidX => (X1 + X2) / 2D - 12D;
    public double MidY => (Y1 + Y2) / 2D - 12D;

    public void Refresh()
    {
        OnPropertyChanged(nameof(X1));
        OnPropertyChanged(nameof(Y1));
        OnPropertyChanged(nameof(X2));
        OnPropertyChanged(nameof(Y2));
        OnPropertyChanged(nameof(MidX));
        OnPropertyChanged(nameof(MidY));
    }
}

public sealed partial class FrontedNodePropertyEditorViewModel : ObservableObject
{
    private readonly FrontedNode _node;
    private readonly Action _markDirty;
    private readonly Action _validate;

    public FrontedNodePropertyEditorViewModel(
        FrontedNode node,
        FrontedNodePropertyDescriptor descriptor,
        Action markDirty,
        Action validate,
        Func<string, string, string> localize)
    {
        _node = node;
        Descriptor = descriptor;
        _markDirty = markDirty;
        _validate = validate;
        DisplayName = localize(descriptor.DisplayNameKey, descriptor.Name);
        Description = localize($"{descriptor.DisplayNameKey}.Description", descriptor.Name);
    }

    public FrontedNodePropertyDescriptor Descriptor { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public bool IsBoolean => Descriptor.EditorKind == FrontedNodePropertyEditorKind.Boolean;
    public bool IsEnum => Descriptor.EditorKind == FrontedNodePropertyEditorKind.Enum;
    public bool IsText => !IsBoolean && !IsEnum;
    public IReadOnlyList<string> Options => Descriptor.Options;

    public string TextValue
    {
        get => Read().ValueKind == JsonValueKind.String ? Read().GetString() ?? string.Empty : Read().ToString();
        set => Write(Descriptor.PropertyType == FrontedNodePropertyType.Number && double.TryParse(value, out var number)
            ? JsonSerializer.SerializeToElement(number)
            : JsonSerializer.SerializeToElement(value));
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

    private JsonElement Read() => _node.Properties.TryGetValue(Descriptor.Name, out var value) ? value : Descriptor.DefaultValue;

    private void Write(JsonElement value)
    {
        _node.Properties[Descriptor.Name] = value;
        _markDirty();
        _validate();
        OnPropertyChanged(nameof(TextValue));
        OnPropertyChanged(nameof(BooleanValue));
        OnPropertyChanged(nameof(EnumValue));
    }
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
