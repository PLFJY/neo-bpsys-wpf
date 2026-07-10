using System.Windows;

namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// Defines where a product tour card is placed relative to its target element.
/// </summary>
public enum ProductTourPlacement
{
    /// <summary>Automatically chooses a placement based on available space.</summary>
    Auto,
    /// <summary>Places the card on the left side of the target.</summary>
    Left,
    /// <summary>Places the card on the right side of the target.</summary>
    Right,
    /// <summary>Places the card above the target.</summary>
    Top,
    /// <summary>Places the card below the target.</summary>
    Bottom,
    /// <summary>Places the card on the left side aligned to the target top.</summary>
    LeftTop,
    /// <summary>Places the card on the left side aligned to the target bottom.</summary>
    LeftBottom,
    /// <summary>Places the card on the right side aligned to the target top.</summary>
    RightTop,
    /// <summary>Places the card on the right side aligned to the target bottom.</summary>
    RightBottom,
    /// <summary>Places the card above the target aligned to the target left.</summary>
    TopLeft,
    /// <summary>Places the card above the target aligned to the target right.</summary>
    TopRight,
    /// <summary>Places the card below the target aligned to the target left.</summary>
    BottomLeft,
    /// <summary>Places the card below the target aligned to the target right.</summary>
    BottomRight,
    /// <summary>Places the card in the center of the owner window.</summary>
    Center
}

/// <summary>
/// Defines how user input is handled while a product tour step is visible.
/// </summary>
public enum ProductTourInteractionMode
{
    /// <summary>Blocks all content interaction except the tour controls.</summary>
    BlockAll,
    /// <summary>Allows interaction with the highlighted target only.</summary>
    AllowTargetOnly,
    /// <summary>Allows interaction with the entire owner window.</summary>
    AllowAll
}

/// <summary>
/// Defines how the guide avatar is positioned for a product tour step.
/// </summary>
public enum ProductTourAvatarPlacement
{
    /// <summary>Uses the default placement near the product tour card.</summary>
    Auto,
    /// <summary>Places the avatar at the upper-left corner of the owner window.</summary>
    TopLeft,
    /// <summary>Places the avatar at the upper-right corner of the owner window.</summary>
    TopRight,
    /// <summary>Places the avatar at the lower-right corner of the owner window.</summary>
    BottomRight
}

/// <summary>
/// Defines how a product tour step resolves its target element.
/// </summary>
public enum TutorialTargetKind
{
    /// <summary>No target is resolved for this step.</summary>
    None,
    /// <summary>Resolves the target by WPF element name.</summary>
    Name,
    /// <summary>Resolves the target from a navigation item.</summary>
    NavigationItem,
    /// <summary>Resolves the first descendant element matching a type full name.</summary>
    DescendantType,
    /// <summary>Resolves the target by matching a framework element tag string.</summary>
    ElementTag
}

/// <summary>
/// Defines why a tutorial package is being started.
/// </summary>
public enum TutorialTriggerMode
{
    /// <summary>The package is automatically attempted when a page is loaded.</summary>
    AutoOnLoaded,
    /// <summary>The package is embedded inside a flow.</summary>
    EmbeddedInFlow,
    /// <summary>The package is explicitly requested by a user or developer action.</summary>
    Manual
}

/// <summary>
/// Defines how automatic page or window loading should advance pending tutorial packages.
/// </summary>
public enum TutorialAutoRunStrategy
{
    /// <summary>Runs only the first currently pending package for each automatic trigger.</summary>
    SinglePendingPackage,

    /// <summary>Continues running subsequent pending packages while the owner remains active.</summary>
    ContinueWhileActive,

    /// <summary>Continues running subsequent pending packages while each package completes normally.</summary>
    DrainSequence
}

/// <summary>
/// Represents the result of running a tutorial operation.
/// </summary>
public enum TutorialRunResult
{
    /// <summary>The tutorial completed normally.</summary>
    Completed,
    /// <summary>The tutorial did not run because the target item was already completed.</summary>
    CompletedAlready,
    /// <summary>The tutorial was skipped by the user.</summary>
    Skipped,
    /// <summary>The tutorial did not run because another tutorial was active.</summary>
    Suppressed,
    /// <summary>The requested target element could not be found.</summary>
    TargetMissing,
    /// <summary>The tutorial had no pending work.</summary>
    NotPending,
    /// <summary>The tutorial did not run because the package is not currently ready. Non-terminal: no completion state is written and future attempts are not suppressed.</summary>
    NotReady,
    /// <summary>The tutorial was canceled.</summary>
    Canceled,
    /// <summary>The tutorial failed with an error.</summary>
    Failed
}

/// <summary>
/// Identifies a tutorial package in public authoring APIs.
/// </summary>
/// <param name="Id">Stable package id.</param>
public readonly record struct TutorialPackageRef(string Id)
{
    /// <inheritdoc />
    public override string ToString() => Id;
}

/// <summary>
/// Represents how a tutorial or package completion was recorded.
/// </summary>
public enum TutorialCompletionKind
{
    /// <summary>The item was completed directly.</summary>
    Completed,
    /// <summary>The package was covered by a completed tutorial flow.</summary>
    CoveredByFlow
}

/// <summary>
/// Defines the expected user action for an interactive tour step.
/// </summary>
public enum TutorialExpectedAction
{
    /// <summary>No explicit action is required.</summary>
    None,
    /// <summary>The user is expected to click the target element.</summary>
    Click,
    /// <summary>The user is expected to enter text.</summary>
    TextInput,
    /// <summary>The user is expected to execute a command.</summary>
    CommandExecuted,
    /// <summary>The step waits for a tutorial signal.</summary>
    SignalReceived
}

/// <summary>
/// Defines the default arrow shape used by a product tour card.
/// </summary>
public enum ProductTourArrowKind
{
    /// <summary>No arrow is shown.</summary>
    None,
    /// <summary>A triangular arrow is shown.</summary>
    Triangle,
    /// <summary>A line arrow is reserved for future use.</summary>
    Line,
    /// <summary>A curved arrow is reserved for future use.</summary>
    Curved
}

/// <summary>
/// Stores completion information for one tutorial package or flow.
/// </summary>
public sealed class TutorialCompletionRecord
{
    /// <summary>Gets or sets the completed item version.</summary>
    public int Version { get; set; }

    /// <summary>Gets or sets the completion kind.</summary>
    public TutorialCompletionKind CompletionKind { get; set; }

    /// <summary>Gets or sets the flow id that covered the package, when applicable.</summary>
    public string? SourceFlowId { get; set; }

    /// <summary>Gets or sets the UTC completion time.</summary>
    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Stores persistent tutorial state.
/// </summary>
public sealed class TutorialState
{
    /// <summary>Gets or sets completed flow records keyed by flow id.</summary>
    public Dictionary<string, TutorialCompletionRecord> CompletedFlows { get; set; } = [];

    /// <summary>Gets or sets completed package records keyed by package id.</summary>
    public Dictionary<string, TutorialCompletionRecord> CompletedPackages { get; set; } = [];
}

/// <summary>
/// Defines the package order and automatic run strategy for a page, tab, or window.
/// </summary>
public sealed class TutorialSequenceDefinition
{
    /// <summary>Gets or sets the page, tab, or window key.</summary>
    public required string PageKey { get; init; }

    /// <summary>Gets or sets package ids in sequence order.</summary>
    public IReadOnlyList<string> PackageIds { get; init; } = [];

    /// <summary>Gets or sets the automatic run strategy.</summary>
    public TutorialAutoRunStrategy AutoRunStrategy { get; init; } = TutorialAutoRunStrategy.SinglePendingPackage;
}

/// <summary>
/// Describes one product tour step.
/// </summary>
public sealed class ProductTourStep
{
    /// <summary>Gets or sets the target element name.</summary>
    public string? TargetName { get; set; }

    /// <summary>Gets or sets the target resolver kind.</summary>
    public TutorialTargetKind TargetKind { get; set; } = TutorialTargetKind.Name;

    /// <summary>Gets or sets the target key used by the selected target resolver.</summary>
    public string? TargetKey { get; set; }

    /// <summary>Gets or sets the localized or literal title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the localized or literal description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the preferred card placement.</summary>
    public ProductTourPlacement Placement { get; set; } = ProductTourPlacement.Auto;

    /// <summary>Gets or sets the card offset applied after placement is calculated.</summary>
    public Point CardOffset { get; set; }

    /// <summary>Gets or sets the interaction mode.</summary>
    public ProductTourInteractionMode InteractionMode { get; set; } = ProductTourInteractionMode.BlockAll;

    /// <summary>Gets or sets whether a missing target should skip this step.</summary>
    public bool AllowMissingTarget { get; set; }

    /// <summary>Gets or sets the signal required before the step can continue.</summary>
    public string? WaitForSignalId { get; set; }

    /// <summary>Gets or sets the timeout for target lookup and signal waits.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>Gets or sets the expected user action.</summary>
    public TutorialExpectedAction ExpectedAction { get; set; }

    /// <summary>Gets or sets the arrow kind, or <see langword="null" /> to use the configured default.</summary>
    public ProductTourArrowKind? ArrowKind { get; set; }

    /// <summary>Gets or sets the avatar placement for this step.</summary>
    public ProductTourAvatarPlacement AvatarPlacement { get; set; } = ProductTourAvatarPlacement.Auto;

    /// <summary>Gets or sets the avatar pose, or <see langword="null" /> to choose the pose automatically.</summary>
    public TutorialAvatarPose? AvatarPose { get; set; }

    /// <summary>Gets the actions invoked before target resolution and overlay display.</summary>
    public IList<TutorialStepAction> PreStepActions { get; } = [];

    /// <summary>Gets the actions invoked after the step completes and its overlay is closed.</summary>
    public IList<TutorialStepAction> PostStepActions { get; } = [];
}

/// <summary>
/// Describes reusable code executed before or after a tutorial step.
/// </summary>
public sealed class TutorialStepAction
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TutorialStepAction"/> class.
    /// </summary>
    /// <param name="name">Diagnostic action name.</param>
    /// <param name="executeAsync">Action body.</param>
    public TutorialStepAction(
        string name,
        Func<TutorialStepActionContext, CancellationToken, Task> executeAsync)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Action name cannot be empty.", nameof(name))
            : name;
        ExecuteAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
    }

    /// <summary>Gets the diagnostic action name.</summary>
    public string Name { get; }

    /// <summary>Gets the action body.</summary>
    public Func<TutorialStepActionContext, CancellationToken, Task> ExecuteAsync { get; }

    /// <summary>Gets or sets whether failures should be logged and ignored.</summary>
    public bool IsOptional { get; init; }
}

/// <summary>
/// Provides runtime information to a tutorial step action.
/// </summary>
public sealed class TutorialStepActionContext
{
    /// <summary>Gets the application service provider.</summary>
    public required IServiceProvider Services { get; init; }

    /// <summary>Gets the tutorial owner element.</summary>
    public required FrameworkElement Owner { get; init; }

    /// <summary>Gets the current step.</summary>
    public required ProductTourStep Step { get; init; }

    /// <summary>Gets the most recent resolved target element when one is available.</summary>
    public FrameworkElement? LastResolvedTarget { get; init; }
}

/// <summary>
/// Defines a tutorial package registered for a page, window, or feature.
/// </summary>
public sealed class TutorialPackageDefinition
{
    /// <summary>Gets or sets the stable package id.</summary>
    public required string PackageId { get; init; }

    /// <summary>Gets or sets the package version.</summary>
    public int Version { get; init; } = 1;

    /// <summary>Gets or sets the page or feature key.</summary>
    public required string PageKey { get; init; }

    /// <summary>Gets or sets the sequence value within its page.</summary>
    public int Sequence { get; init; }

    /// <summary>Gets or sets a package kind label.</summary>
    public string Kind { get; init; } = "ProductTour";

    /// <summary>Gets or sets the package steps.</summary>
    public IReadOnlyList<ProductTourStep> Steps { get; init; } = [];

    /// <summary>Gets or sets an optional condition that determines whether the package can run.</summary>
    public Func<IServiceProvider, bool>? CanRun { get; init; }

    /// <summary>Gets or sets an optional owner-aware condition that determines whether the package can run.</summary>
    public Func<IServiceProvider, FrameworkElement?, bool>? CanRunWithOwner { get; init; }
}

/// <summary>
/// Defines a tutorial flow.
/// </summary>
public sealed class TutorialFlowDefinition
{
    /// <summary>Gets or sets the stable flow id.</summary>
    public required string FlowId { get; init; }

    /// <summary>Gets or sets the flow version.</summary>
    public int Version { get; init; } = 1;

    /// <summary>Gets or sets package ids covered when this flow completes.</summary>
    public IReadOnlyList<string> IncludedPackageIds { get; init; } = [];

    /// <summary>Gets or sets flow items.</summary>
    public IReadOnlyList<TutorialFlowItem> Items { get; init; } = [];
}

/// <summary>
/// Base type for tutorial flow items.
/// </summary>
public abstract class TutorialFlowItem
{
    /// <summary>Gets or sets an optional item id.</summary>
    public string? ItemId { get; init; }
}

/// <summary>
/// A flow item that runs a registered package.
/// </summary>
public sealed class PackageFlowItem : TutorialFlowItem
{
    /// <summary>Gets or sets the referenced package id.</summary>
    public required string PackageId { get; init; }
}

/// <summary>
/// A flow item that shows dialogue lines.
/// </summary>
public sealed class DialogueFlowItem : TutorialFlowItem
{
    /// <summary>Gets or sets the speaker name.</summary>
    public string Speaker { get; init; } = "Product tour";

    /// <summary>Gets or sets dialogue lines.</summary>
    public IReadOnlyList<string> Lines { get; init; } = [];
}

/// <summary>
/// A flow item that invokes custom code.
/// </summary>
public sealed class ActionFlowItem : TutorialFlowItem
{
    /// <summary>Gets or sets the action to invoke.</summary>
    public Func<IServiceProvider, CancellationToken, Task>? ActionAsync { get; init; }
}

/// <summary>
/// A flow item that shows ad hoc product tour steps.
/// </summary>
public sealed class CustomStepFlowItem : TutorialFlowItem
{
    /// <summary>Gets or sets the steps shown by this item.</summary>
    public IReadOnlyList<ProductTourStep> Steps { get; init; } = [];
}

/// <summary>
/// Provides context about a running tutorial step.
/// </summary>
public sealed class ProductTourStepContext
{
    /// <summary>Gets or sets the current flow id.</summary>
    public string? FlowId { get; init; }

    /// <summary>Gets or sets the current package id.</summary>
    public string? PackageId { get; init; }

    /// <summary>Gets or sets the zero-based step index.</summary>
    public int StepIndex { get; init; }

    /// <summary>Gets or sets the total step count.</summary>
    public int StepCount { get; init; }

    /// <summary>Gets or sets the owner element.</summary>
    public required FrameworkElement Owner { get; init; }
}
