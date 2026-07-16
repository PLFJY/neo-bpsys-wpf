using System.Windows;

namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// 定义产品导览卡片相对于其目标元素的放置位置。
/// </summary>
public enum ProductTourPlacement
{
    /// <summary>根据可用空间自动选择放置位置。</summary>
    Auto,
    /// <summary>将卡片放置在目标左侧。</summary>
    Left,
    /// <summary>将卡片放置在目标右侧。</summary>
    Right,
    /// <summary>将卡片放置在目标上方。</summary>
    Top,
    /// <summary>将卡片放置在目标下方。</summary>
    Bottom,
    /// <summary>将卡片放置在目标左侧并与目标顶部对齐。</summary>
    LeftTop,
    /// <summary>将卡片放置在目标左侧并与目标底部对齐。</summary>
    LeftBottom,
    /// <summary>将卡片放置在目标右侧并与目标顶部对齐。</summary>
    RightTop,
    /// <summary>将卡片放置在目标右侧并与目标底部对齐。</summary>
    RightBottom,
    /// <summary>将卡片放置在目标上方并与目标左侧对齐。</summary>
    TopLeft,
    /// <summary>将卡片放置在目标上方并与目标右侧对齐。</summary>
    TopRight,
    /// <summary>将卡片放置在目标下方并与目标左侧对齐。</summary>
    BottomLeft,
    /// <summary>将卡片放置在目标下方并与目标右侧对齐。</summary>
    BottomRight,
    /// <summary>将卡片放置在所有者窗口的中心。</summary>
    Center
}

/// <summary>
/// 定义产品导览步骤可见时用户输入的处理方式。
/// </summary>
public enum ProductTourInteractionMode
{
    /// <summary>阻止除导览控件以外的所有内容交互。</summary>
    BlockAll,
    /// <summary>仅允许与高亮目标交互。</summary>
    AllowTargetOnly,
    /// <summary>允许与整个所有者窗口交互。</summary>
    AllowAll
}

/// <summary>
/// 定义产品导览步骤中引导头像的定位方式。
/// </summary>
public enum ProductTourAvatarPlacement
{
    /// <summary>使用产品导览卡片附近的默认放置位置。</summary>
    Auto,
    /// <summary>将头像放置在所有者窗口的左上角。</summary>
    TopLeft,
    /// <summary>将头像放置在所有者窗口的右上角。</summary>
    TopRight,
    /// <summary>将头像放置在所有者窗口的右下角。</summary>
    BottomRight
}

/// <summary>
/// 定义产品导览步骤解析其目标元素的方式。
/// </summary>
public enum TutorialTargetKind
{
    /// <summary>此步骤不解析目标。</summary>
    None,
    /// <summary>通过 WPF 元素名称解析目标。</summary>
    Name,
    /// <summary>从导航项解析目标。</summary>
    NavigationItem,
    /// <summary>解析匹配类型全名的第一个后代元素。</summary>
    DescendantType,
    /// <summary>通过匹配框架元素标签字符串解析目标。</summary>
    ElementTag
}

/// <summary>
/// 定义教程包启动的原因。
/// </summary>
public enum TutorialTriggerMode
{
    /// <summary>页面加载时自动尝试该包。</summary>
    AutoOnLoaded,
    /// <summary>该包嵌入在流程中。</summary>
    EmbeddedInFlow,
    /// <summary>该包由用户或开发者操作显式请求。</summary>
    Manual
}

/// <summary>
/// 表示教程操作的运行结果。
/// </summary>
public enum TutorialRunResult
{
    /// <summary>教程正常完成。</summary>
    Completed,
    /// <summary>教程未运行，因为目标项已完成。</summary>
    CompletedAlready,
    /// <summary>教程被用户跳过。</summary>
    Skipped,
    /// <summary>用户选择永久跳过教程。</summary>
    SkippedPermanently,
    /// <summary>找不到请求的目标元素。</summary>
    TargetMissing,
    /// <summary>教程没有待处理的工作。</summary>
    NotPending,
    /// <summary>教程未运行，因为该包当前未就绪。非终止状态：不写入完成状态，且不抑制后续尝试。</summary>
    NotReady,
    /// <summary>教程已被取消。</summary>
    Canceled,
    /// <summary>当前包通过打开拥有教程的子窗口完成。</summary>
    ChildWindowHandoff,
    /// <summary>教程因错误失败。</summary>
    Failed
}

/// <summary>维护当前进程内的新手教程显示抑制状态。</summary>
public interface ITutorialSessionSuppression
{
    /// <summary>获取当前进程是否禁止自动显示新手教程。</summary>
    bool IsTutorialDisplaySuppressed { get; }

    /// <summary>禁止自动显示新手教程，直到下次应用启动。</summary>
    void SuppressUntilNextStartup();

    /// <summary>在当前进程内禁止显示指定页面队列的教程。</summary>
    /// <param name="pageKey">稳定的教程页面键。</param>
    void SuppressSequenceForCurrentSession(string pageKey);

    /// <summary>获取指定页面队列是否已在当前进程内被跳过。</summary>
    /// <param name="pageKey">稳定的教程页面键。</param>
    /// <returns>若当前进程内不应显示该页面队列则返回 <see langword="true"/>。</returns>
    bool IsSequenceSuppressedForCurrentSession(string pageKey);
}

/// <summary>默认的进程内新手教程显示抑制器。</summary>
public sealed class TutorialSessionSuppression : ITutorialSessionSuppression
{
    private readonly HashSet<string> _suppressedSequenceKeys = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public bool IsTutorialDisplaySuppressed { get; private set; }

    /// <inheritdoc />
    public void SuppressUntilNextStartup() => IsTutorialDisplaySuppressed = true;

    /// <inheritdoc />
    public void SuppressSequenceForCurrentSession(string pageKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pageKey);
        _suppressedSequenceKeys.Add(pageKey);
    }

    /// <inheritdoc />
    public bool IsSequenceSuppressedForCurrentSession(string pageKey) =>
        _suppressedSequenceKeys.Contains(pageKey);
}

/// <summary>
/// 在公共创作 API 中标识一个教程包。
/// </summary>
/// <param name="Id">稳定的包 id。</param>
public readonly record struct TutorialPackageRef(string Id)
{
    /// <inheritdoc />
    public override string ToString() => Id;
}

/// <summary>
/// 表示教程或包完成记录的方式。
/// </summary>
public enum TutorialCompletionKind
{
    /// <summary>该项已直接完成。</summary>
    Completed,
    /// <summary>该包由已完成的教程流程覆盖。</summary>
    CoveredByFlow
}

/// <summary>
/// 定义交互式导览步骤的预期用户操作。
/// </summary>
public enum TutorialExpectedAction
{
    /// <summary>不需要显式操作。</summary>
    None,
    /// <summary>预期用户点击目标元素。</summary>
    Click,
    /// <summary>预期用户输入文本。</summary>
    TextInput,
    /// <summary>预期用户执行命令。</summary>
    CommandExecuted,
    /// <summary>该步骤等待教程信号。</summary>
    SignalReceived
}

/// <summary>
/// 存储一个教程包或流程的完成信息。
/// </summary>
public sealed class TutorialCompletionRecord
{
    /// <summary>获取或设置已完成项的版本。</summary>
    public int Version { get; set; }

    /// <summary>获取或设置完成类型。</summary>
    public TutorialCompletionKind CompletionKind { get; set; }

    /// <summary>获取或设置覆盖该包的流程 id（如适用）。</summary>
    public string? SourceFlowId { get; set; }

    /// <summary>获取或设置 UTC 完成时间。</summary>
    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// 存储持久化的教程状态。
/// </summary>
public sealed class TutorialState
{
    /// <summary>获取或设置按流程 id 索引的已完成流程记录。</summary>
    public Dictionary<string, TutorialCompletionRecord> CompletedFlows { get; set; } = [];

    /// <summary>获取或设置按包 id 索引的已完成包记录。</summary>
    public Dictionary<string, TutorialCompletionRecord> CompletedPackages { get; set; } = [];
}

/// <summary>
/// 定义页面、标签页或窗口的包顺序。
/// </summary>
public sealed class TutorialSequenceDefinition
{
    /// <summary>获取或设置页面、标签页或窗口键。</summary>
    public required string PageKey { get; init; }

    /// <summary>获取或设置按顺序排列的包 id。</summary>
    public IReadOnlyList<string> PackageIds { get; init; } = [];

}

/// <summary>
/// 描述一个产品导览步骤。
/// </summary>
public sealed class ProductTourStep
{
    /// <summary>获取或设置目标元素名称。</summary>
    public string? TargetName { get; set; }

    /// <summary>获取或设置目标解析器类型。</summary>
    public TutorialTargetKind TargetKind { get; set; } = TutorialTargetKind.Name;

    /// <summary>获取或设置所选目标解析器使用的目标键。</summary>
    public string? TargetKey { get; set; }

    /// <summary>获取或设置本地化或字面标题。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>获取或设置本地化或字面描述。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置用于在运行时解析标题的资源键。
    /// 非 null 时，覆盖层通过 <see cref="ITutorialContentResolver"/> 解析标题，
    /// 而非直接使用 <see cref="Title"/>。
    /// </summary>
    public string? TitleKey { get; set; }

    /// <summary>
    /// 获取或设置用于在运行时解析描述的资源键。
    /// 非 null 时，覆盖层通过 <see cref="ITutorialContentResolver"/> 解析描述，
    /// 而非直接使用 <see cref="Description"/>。
    /// </summary>
    public string? DescriptionKey { get; set; }

    /// <summary>获取或设置首选卡片放置位置。</summary>
    public ProductTourPlacement Placement { get; set; } = ProductTourPlacement.Auto;

    /// <summary>获取或设置在放置位置计算后应用的卡片偏移量。</summary>
    public Point CardOffset { get; set; }

    /// <summary>获取或设置交互模式。</summary>
    public ProductTourInteractionMode InteractionMode { get; set; } = ProductTourInteractionMode.BlockAll;

    /// <summary>获取或设置目标缺失时是否跳过此步骤。</summary>
    public bool AllowMissingTarget { get; set; }

    /// <summary>获取或设置步骤继续前所需的信号。</summary>
    public string? WaitForSignalId { get; set; }

    /// <summary>获取或设置目标查找和信号等待的超时时间。</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>获取或设置预期用户操作。</summary>
    public TutorialExpectedAction ExpectedAction { get; set; }

    /// <summary>获取或设置此步骤的头像放置位置。</summary>
    public ProductTourAvatarPlacement AvatarPlacement { get; set; } = ProductTourAvatarPlacement.Auto;

    /// <summary>获取或设置头像姿势，或 <see langword="null" /> 以自动选择姿势。</summary>
    public TutorialAvatarPose? AvatarPose { get; set; }

    /// <summary>获取在目标解析和覆盖层显示之前调用的操作。</summary>
    public IList<TutorialStepAction> PreStepActions { get; } = [];

    /// <summary>获取在步骤完成且其覆盖层关闭后调用的操作。</summary>
    public IList<TutorialStepAction> PostStepActions { get; } = [];
}

/// <summary>
/// 描述在教程步骤之前或之后执行的可复用代码。
/// </summary>
public sealed class TutorialStepAction
{
    /// <summary>
    /// 初始化 <see cref="TutorialStepAction"/> 类的新实例。
    /// </summary>
    /// <param name="name">诊断操作名称。</param>
    /// <param name="executeAsync">操作体。</param>
    public TutorialStepAction(
        string name,
        Func<TutorialStepActionContext, CancellationToken, Task> executeAsync)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Action name cannot be empty.", nameof(name))
            : name;
        ExecuteAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
    }

    /// <summary>获取诊断操作名称。</summary>
    public string Name { get; }

    /// <summary>获取操作体。</summary>
    public Func<TutorialStepActionContext, CancellationToken, Task> ExecuteAsync { get; }

    /// <summary>获取或设置是否记录失败并忽略。</summary>
    public bool IsOptional { get; init; }
}

/// <summary>
/// 提供在拥有教程的子窗口打开时让出当前可见教程步骤的机制。
/// </summary>
public interface ITutorialStepCancellation
{
    /// <summary>
    /// 强制当前可见教程步骤以子窗口交接操作完成。
    /// 如果当前没有可见步骤，则不执行任何操作。
    /// </summary>
    void YieldCurrentStepForChildWindow();
}

/// <summary>
/// 向教程步骤操作提供运行时信息。
/// </summary>
public sealed class TutorialStepActionContext
{
    /// <summary>获取应用服务提供程序。</summary>
    public required IServiceProvider Services { get; init; }

    /// <summary>获取教程所有者元素。</summary>
    public required FrameworkElement Owner { get; init; }

    /// <summary>获取当前步骤。</summary>
    public required ProductTourStep Step { get; init; }

    /// <summary>获取最近解析的目标元素（如可用）。</summary>
    public FrameworkElement? LastResolvedTarget { get; init; }
}

/// <summary>
/// 定义为页面、窗口或功能注册的教程包。
/// </summary>
public sealed class TutorialPackageDefinition
{
    private IReadOnlyList<TutorialPackageItem> _items = [];
    /// <summary>获取或设置稳定的包 id。</summary>
    public required string PackageId { get; init; }

    /// <summary>获取或设置包版本。</summary>
    public int Version { get; init; } = 1;

    /// <summary>获取或设置页面或功能键。</summary>
    public required string PageKey { get; init; }

    /// <summary>获取或设置在其页面内的序列值。</summary>
    public int Sequence { get; init; }

    /// <summary>获取或设置包类型标签。</summary>
    public string Kind { get; init; } = "ProductTour";

    /// <summary>获取或设置有序的包项。</summary>
    public IReadOnlyList<TutorialPackageItem> Items
    {
        get => _items;
        init => _items = value ?? [];
    }

    /// <summary>获取此包包含的聚光灯步骤。</summary>
    public IReadOnlyList<ProductTourStep> Steps
    {
        get => Items
            .OfType<TutorialPackageStepItem>()
            .Select(item => item.Step)
            .ToArray();
        init => _items = (value ?? [])
            .Select(step => (TutorialPackageItem)new TutorialPackageStepItem { Step = step })
            .ToArray();
    }

    /// <summary>获取或设置决定该包是否可运行的可选条件。</summary>
    public Func<IServiceProvider, bool>? CanRun { get; init; }

    /// <summary>获取或设置决定该包是否可运行的可选所有者感知条件。</summary>
    public Func<IServiceProvider, FrameworkElement?, bool>? CanRunWithOwner { get; init; }
}

/// <summary>教程包内允许的显式项的基类型。</summary>
public abstract class TutorialPackageItem;

/// <summary>显示聚光灯步骤的包项。</summary>
public sealed class TutorialPackageStepItem : TutorialPackageItem
{
    /// <summary>获取聚光灯步骤。</summary>
    public required ProductTourStep Step { get; init; }
}

/// <summary>通过对话覆盖层显示对话的包项。</summary>
public sealed class TutorialPackageDialogueItem : TutorialPackageItem
{
    /// <summary>获取对话定义。</summary>
    public required DialogueFlowItem Dialogue { get; init; }
}

/// <summary>
/// 定义一个教程流程。
/// </summary>
public sealed class TutorialFlowDefinition
{
    /// <summary>获取或设置稳定的流程 id。</summary>
    public required string FlowId { get; init; }

    /// <summary>获取或设置流程版本。</summary>
    public int Version { get; init; } = 1;

    /// <summary>获取或设置此流程完成时覆盖的包 id。</summary>
    public IReadOnlyList<string> IncludedPackageIds { get; init; } = [];

    /// <summary>获取或设置流程项。</summary>
    public IReadOnlyList<TutorialFlowItem> Items { get; init; } = [];
}

/// <summary>
/// 教程流程项的基类型。
/// </summary>
public abstract class TutorialFlowItem
{
    /// <summary>获取或设置可选的项 id。</summary>
    public string? ItemId { get; init; }
}

/// <summary>
/// 运行已注册包的流程项。
/// </summary>
public sealed class PackageFlowItem : TutorialFlowItem
{
    /// <summary>获取或设置引用的包 id。</summary>
    public required string PackageId { get; init; }
}

/// <summary>
/// 显示对话台词的流程项。
/// </summary>
public sealed class DialogueFlowItem : TutorialFlowItem
{
    /// <summary>获取或设置说话者名称。</summary>
    public string Speaker { get; init; } = "Product tour";

    /// <summary>获取或设置对话台词。</summary>
    public IReadOnlyList<string> Lines { get; init; } = [];

    /// <summary>
    /// 获取或设置用于在运行时通过 <see cref="ITutorialContentResolver"/> 解析对话台词的资源键。
    /// 非 null 时，从资源解析台词；键为 null 时使用 <see cref="Lines"/> 作为回退。
    /// </summary>
    public string? LinesKey { get; init; }
}

/// <summary>
/// 调用自定义代码的流程项。
/// </summary>
public sealed class ActionFlowItem : TutorialFlowItem
{
    /// <summary>获取或设置要调用的操作。</summary>
    public Func<IServiceProvider, CancellationToken, Task>? ActionAsync { get; init; }
}

/// <summary>
/// 显示临时产品导览步骤的流程项。
/// </summary>
public sealed class CustomStepFlowItem : TutorialFlowItem
{
    /// <summary>获取或设置此项显示的步骤。</summary>
    public IReadOnlyList<ProductTourStep> Steps { get; init; } = [];
}

/// <summary>
/// 提供有关正在运行的教程步骤的上下文。
/// </summary>
public sealed class ProductTourStepContext
{
    /// <summary>获取或设置当前流程 id。</summary>
    public string? FlowId { get; init; }

    /// <summary>获取或设置当前包 id。</summary>
    public string? PackageId { get; init; }

    /// <summary>获取或设置从零开始的步骤索引。</summary>
    public int StepIndex { get; init; }

    /// <summary>获取或设置总步骤数。</summary>
    public int StepCount { get; init; }

    /// <summary>获取或设置所有者元素。</summary>
    public required FrameworkElement Owner { get; init; }
}
