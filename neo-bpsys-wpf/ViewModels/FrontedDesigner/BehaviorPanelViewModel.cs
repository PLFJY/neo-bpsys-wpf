using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Services.FrontedDesigner;
using neo_bpsys_wpf.ViewModels.FrontedDesigner.GraphEditor;
using System.Collections.ObjectModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;

namespace neo_bpsys_wpf.ViewModels.FrontedDesigner;

/// <summary>
/// 设计器 v3 行为面板视图模型，绑定到当前选中的布局控件。
/// </summary>
public sealed partial class BehaviorPanelViewModel : ViewModelBase
{
    private readonly IFrontedDesignerLocalizationService _localizationService;
    private readonly Action _markLayoutDirty;
    private readonly Action _markBehaviorsDirty;
    private readonly Action _captureUndoSnapshot;
    private readonly FrontedNodeCatalog _nodeCatalog;
    private readonly FrontedBehaviorEventCatalog _eventCatalog;
    private readonly FrontedNodeGraphValidator _graphValidator;
    private readonly IFrontedNodeGraphRuntime _graphRuntime;
    private readonly IFrontedAnimationRuntime? _animationRuntime;
    private readonly FrontedDesignerPreviewAnimationScope? _previewAnimationScope;
    private readonly IFrontedBehaviorClipboard _behaviorClipboard;
    private readonly FrontedBehaviorCopyPasteService _copyPasteService;
    private readonly JsonSerializerOptions _cloneJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        MaxDepth = FrontedLayoutLimits.MaxJsonDepth
    };

    private ControlBehaviorSet? _currentSet;

    private readonly Func<Task<bool>>? _saveBehaviorAsync;

    /// <summary>
    /// 初始化设计时行为面板视图模型。
    /// </summary>
    public BehaviorPanelViewModel()
        : this(
            new FrontedDesignerLocalizationService(),
            new FrontedBehaviorEventCatalog(),
            static () => { },
            static () => { })
    {
    }

    /// <summary>
    /// 初始化新的行为面板视图模型。
    /// </summary>
    /// <param name="localizationService">设计器本地化服务。</param>
    /// <param name="eventCatalog">支持的行为触发事件目录。</param>
    /// <param name="markLayoutDirty">当行为编辑同时修改布局拥有的控件元数据时使用的回调。</param>
    /// <param name="markBehaviorsDirty">当行为文档变化时使用的回调。</param>
    /// <param name="nodeCatalog">可选的节点目录覆盖。</param>
    /// <param name="graphValidator">可选的图校验器覆盖。</param>
    /// <param name="graphRuntime">可选的图运行时覆盖。</param>
    /// <param name="animationRuntime">预览操作使用的可选动画运行时。</param>
    /// <param name="previewAnimationScope">提供当前预览根的可选预览作用域。</param>
    /// <param name="saveBehaviorAsync">动画编辑器全部保存时使用的可选保存回调。</param>
    /// <param name="behaviorClipboard">可选的共享行为剪贴板。</param>
    /// <param name="copyPasteService">可选的行为复制/粘贴服务。</param>
    /// <param name="captureUndoSnapshot">用于捕获外层设计器撤销快照的可选回调。</param>
    public BehaviorPanelViewModel(
        IFrontedDesignerLocalizationService localizationService,
        FrontedBehaviorEventCatalog eventCatalog,
        Action markLayoutDirty,
        Action markBehaviorsDirty,
        FrontedNodeCatalog? nodeCatalog = null,
        FrontedNodeGraphValidator? graphValidator = null,
        IFrontedNodeGraphRuntime? graphRuntime = null,
        IFrontedAnimationRuntime? animationRuntime = null,
        FrontedDesignerPreviewAnimationScope? previewAnimationScope = null,
        Func<Task<bool>>? saveBehaviorAsync = null,
        IFrontedBehaviorClipboard? behaviorClipboard = null,
        FrontedBehaviorCopyPasteService? copyPasteService = null,
        Action? captureUndoSnapshot = null)
    {
        _localizationService = localizationService;
        _eventCatalog = eventCatalog;
        _markLayoutDirty = markLayoutDirty;
        _markBehaviorsDirty = markBehaviorsDirty;
        _captureUndoSnapshot = captureUndoSnapshot ?? (() => { });
        _saveBehaviorAsync = saveBehaviorAsync;
        _nodeCatalog = nodeCatalog ?? new FrontedNodeCatalog();
        _graphValidator = graphValidator ?? new FrontedNodeGraphValidator(_nodeCatalog);
        _graphRuntime = graphRuntime ?? new FrontedNodeGraphRuntime(_nodeCatalog, _graphValidator);
        _animationRuntime = animationRuntime;
        _previewAnimationScope = previewAnimationScope;
        _behaviorClipboard = behaviorClipboard ?? new FrontedBehaviorClipboard();
        _copyPasteService = copyPasteService
            ?? new FrontedBehaviorCopyPasteService(new FrontedBehaviorControlSemanticResolver(), localizationService);
        EventOptions = [.. eventCatalog.Events.Select(CreateEventOption)];
        EventBusEventOptions = FilterEventOptions(FrontedBehaviorEventUsage.EventBus);
        TransitionEventOptions = FilterEventOptions(FrontedBehaviorEventUsage.Transition);
        OperatorOptions = CreateOperatorOptions();
        StopModeOptions = CreateEnumOptions<FrontedLoopStopMode>("Designer.Behaviors.StopMode");
        ReentryPolicyOptions = CreateEnumOptions<FrontedReentryPolicy>("Designer.Behaviors.ReentryPolicy");
    }

    /// <summary>
    /// 获取为选中控件显示的行为行。
    /// </summary>
    public ObservableCollection<BehaviorEditorViewModel> Behaviors { get; } = [];

    /// <summary>
    /// 获取事件选择器中显示的触发事件选项。
    /// </summary>
    public IReadOnlyList<BehaviorEventOptionViewModel> EventOptions { get; }

    /// <summary>
    /// 获取统一事件总线触发器可用的事件选项。
    /// </summary>
    public IReadOnlyList<BehaviorEventOptionViewModel> EventBusEventOptions { get; }

    /// <summary>
    /// 获取 Transition 触发器可用的事件选项。
    /// </summary>
    public IReadOnlyList<BehaviorEventOptionViewModel> TransitionEventOptions { get; }

    /// <summary>
    /// 获取过滤条件行中显示的触发过滤运算符选项。
    /// </summary>
    public IReadOnlyList<BehaviorOptionViewModel> OperatorOptions { get; }

    /// <summary>
    /// 获取循环行为中显示的循环停止模式选项。
    /// </summary>
    public IReadOnlyList<BehaviorOptionViewModel> StopModeOptions { get; }

    /// <summary>
    /// 获取行为执行设置中显示的重入策略选项。
    /// </summary>
    public IReadOnlyList<BehaviorOptionViewModel> ReentryPolicyOptions { get; }

    /// <summary>
    /// 获取当前正在编辑的行为文档。
    /// </summary>
    public FrontedBehaviorDocument CurrentDocument { get; private set; } = new();

    /// <summary>
    /// 需要为行为打开完整动画图编辑器时触发。
    /// </summary>
    public event Action<FrontedBehaviorAnimationEditorViewModel>? AnimationEditorRequested;

    /// <summary>
    /// 需要打开复制到目标控件对话框时触发。
    /// </summary>
    public event Action<FrontedBehaviorCopyToRequest>? CopyBehaviorToRequested;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedControl))]
    [NotifyPropertyChangedFor(nameof(EmptyText))]
    public partial FrontedControlDesignItem? SelectedControl { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedBehavior))]
    public partial BehaviorEditorViewModel? SelectedBehavior { get; set; }

    [ObservableProperty]
    public partial string PasteStatus { get; set; } = string.Empty;

    /// <summary>
    /// 获取是否已选中设计器控件。
    /// </summary>
    public bool HasSelectedControl => SelectedControl is not null;

    /// <summary>
    /// 获取是否已选中行为行。
    /// </summary>
    public bool HasSelectedBehavior => SelectedBehavior is not null;

    /// <summary>
    /// 获取是否可以向选中控件粘贴行为。
    /// </summary>
    public bool CanPasteBehavior => SelectedControl is not null && _behaviorClipboard.Payload is not null;

    /// <summary>
    /// 获取选中控件是否拥有行为。
    /// </summary>
    public bool HasBehaviors => Behaviors.Count > 0;

    /// <summary>
    /// 获取行为列表的空状态文本。
    /// </summary>
    public string EmptyText => HasSelectedControl
        ? Localize("Designer.Behaviors.Empty", "No behaviors yet.")
        : Localize("NoSelectedControl", "No selected control.");

    /// <summary>
    /// 获取内联图预览未打开时显示的占位文本。
    /// </summary>
    public string GraphPlaceholder => Localize(
        "Designer.Behaviors.GraphPlaceholder",
        "Node graph editor is available from the animation editor.");

    /// <summary>
    /// 替换正在编辑的行为文档，并根据当前选择刷新行。
    /// </summary>
    /// <param name="document">要编辑的行为文档。</param>
    public void SetDocument(FrontedBehaviorDocument document)
    {
        CurrentDocument = document;
        RefreshForSelectedControl();
    }

    /// <summary>
    /// 更新选中的布局控件并重新加载其行为集合。
    /// </summary>
    /// <param name="selectedControl">选中的设计器控件；没有选中控件时为 <see langword="null"/>。</param>
    public void SetSelectedControl(FrontedControlDesignItem? selectedControl)
    {
        SelectedControl = selectedControl;
        RefreshForSelectedControl();
    }

    /// <summary>
    /// 设置当前窗口以及可用的行为粘贴目标。
    /// </summary>
    /// <param name="windowType">当前窗口类型。</param>
    /// <param name="controls">当前设计文档中可用的控件。</param>
    public void SetCopyContext(string? windowType, IEnumerable<FrontedControlDesignItem>? controls)
    {
        CurrentWindowType = windowType ?? string.Empty;
        AvailableControls = controls as IReadOnlyList<FrontedControlDesignItem>
            ?? controls?.ToArray()
            ?? [];
    }

    /// <summary>
    /// 获取行为剪贴板 payload 使用的当前窗口类型。
    /// </summary>
    public string CurrentWindowType { get; private set; } = string.Empty;

    /// <summary>
    /// 获取可用于多目标行为粘贴的控件。
    /// </summary>
    public IReadOnlyList<FrontedControlDesignItem> AvailableControls { get; private set; } = [];

    /// <summary>
    /// 移除与控件行为 GUID 关联的行为集合。
    /// </summary>
    /// <param name="behaviorGuid">布局控件配置上存储的行为 GUID。</param>
    /// <returns><see langword="true"/> when a set was removed.</returns>
}
