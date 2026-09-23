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

}
