using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// Declares tutorials owned by a WPF element type.
/// </summary>
/// <typeparam name="TSelf">Owner element type.</typeparam>
public interface ITutorialOwner<TSelf>
    where TSelf : FrameworkElement, ITutorialOwner<TSelf>
{
    /// <summary>Gets the stable tutorial key for this owner.</summary>
    static abstract string TutorialKey { get; }

    /// <summary>
    /// Registers tutorials owned by this type.
    /// </summary>
    /// <param name="builder">Tutorial authoring builder.</param>
    static abstract void RegisterTutorials(ITutorialBuilder builder);
}

/// <summary>
/// Declares application-level tutorial flows.
/// </summary>
/// <typeparam name="TSelf">Application type.</typeparam>
public interface IAppTutorial<TSelf>
    where TSelf : Application, IAppTutorial<TSelf>
{
    /// <summary>
    /// Registers application-level tutorials.
    /// </summary>
    /// <param name="builder">Tutorial authoring builder.</param>
    static abstract void RegisterTutorials(ITutorialBuilder builder);
}

/// <summary>
/// High-level tutorial authoring entry point.
/// </summary>
public interface ITutorialBuilder
{
    /// <summary>
    /// Starts page-local tutorial authoring.
    /// </summary>
    /// <typeparam name="TOwner">Page owner type.</typeparam>
    /// <returns>Owner tutorial builder.</returns>
    ITutorialOwnerBuilder<TOwner> ForPage<TOwner>()
        where TOwner : Page, ITutorialOwner<TOwner>;

    /// <summary>
    /// Starts window-local tutorial authoring.
    /// </summary>
    /// <typeparam name="TOwner">Window owner type.</typeparam>
    /// <returns>Owner tutorial builder.</returns>
    ITutorialOwnerBuilder<TOwner> ForWindow<TOwner>()
        where TOwner : Window, ITutorialOwner<TOwner>;

    /// <summary>
    /// Starts region-local tutorial authoring.
    /// </summary>
    /// <typeparam name="TOwner">Region owner type.</typeparam>
    /// <returns>Owner tutorial builder.</returns>
    ITutorialOwnerBuilder<TOwner> ForRegion<TOwner>()
        where TOwner : FrameworkElement, ITutorialOwner<TOwner>;

    /// <summary>
    /// Starts authoring for a specific tutorial key owned by an element type.
    /// </summary>
    /// <typeparam name="TOwner">Owner type.</typeparam>
    /// <param name="tutorialKey">Tutorial key to register.</param>
    /// <returns>Owner tutorial builder.</returns>
    ITutorialOwnerBuilder<TOwner> ForKey<TOwner>(string tutorialKey)
        where TOwner : FrameworkElement, ITutorialOwner<TOwner>;

    /// <summary>
    /// Starts flow authoring.
    /// </summary>
    /// <param name="flowId">Stable flow id.</param>
    /// <returns>Flow builder.</returns>
    ITutorialFlowBuilder Flow(string flowId);

    /// <summary>
    /// Registers tutorials declared by an owner type.
    /// </summary>
    /// <typeparam name="TOwner">Owner type.</typeparam>
    void RegisterOwner<TOwner>()
        where TOwner : FrameworkElement, ITutorialOwner<TOwner>;

    /// <summary>
    /// Registers tutorials declared by the application.
    /// </summary>
    /// <typeparam name="TApp">Application type.</typeparam>
    void RegisterApp<TApp>()
        where TApp : Application, IAppTutorial<TApp>;
}

/// <summary>
/// High-level builder for one tutorial owner.
/// </summary>
/// <typeparam name="TOwner">Owner type.</typeparam>
public interface ITutorialOwnerBuilder<TOwner>
    where TOwner : FrameworkElement, ITutorialOwner<TOwner>
{
    /// <summary>
    /// Starts a package owned by the current owner.
    /// </summary>
    /// <param name="package">Package reference.</param>
    /// <returns>Package builder.</returns>
    ITutorialPackageBuilder<TOwner> Package(TutorialPackageRef package);

    /// <summary>
    /// Adds an existing package reference to this owner's run sequence.
    /// </summary>
    /// <param name="package">Existing package reference.</param>
    /// <returns>The same owner builder.</returns>
    ITutorialOwnerBuilder<TOwner> Use(TutorialPackageRef package);

}

/// <summary>
/// Internal extension of <see cref="ITutorialOwnerBuilder{TOwner}"/> that supports package registration
/// with on-demand scheduling. Used by the authoring package builder and runtime contributor builder.
/// </summary>
/// <typeparam name="TOwner">Owner type.</typeparam>
internal interface ITutorialOwnerBuilderInternal<TOwner> : ITutorialOwnerBuilder<TOwner>
    where TOwner : FrameworkElement, ITutorialOwner<TOwner>
{
    /// <summary>
    /// Registers a finalized package definition with the package registry and optionally adds it to the sequence.
    /// </summary>
    /// <param name="package">Finalized package definition.</param>
    /// <param name="isOnDemand">Whether the package is on-demand and should not appear in the default sequence.</param>
    void RegisterPackage(TutorialPackageDefinition package, bool isOnDemand);
}

/// <summary>
/// High-level builder for one owner-local package.
/// </summary>
/// <typeparam name="TOwner">Owner type.</typeparam>
public interface ITutorialPackageBuilder<TOwner> : ITutorialOwnerBuilder<TOwner>
    where TOwner : FrameworkElement, ITutorialOwner<TOwner>
{
    /// <summary>
    /// Adds a tutorial step.
    /// </summary>
    /// <param name="title">Step title.</param>
    /// <returns>Step builder.</returns>
    ITutorialStepBuilder<TOwner> Step(string title);

    /// <summary>Adds dialogue at the current position in the package.</summary>
    /// <param name="dialogue">Dialogue to display.</param>
    /// <returns>The same package builder.</returns>
    ITutorialPackageBuilder<TOwner> Dialogue(DialogueFlowItem dialogue);

    /// <summary>
    /// Marks this package as on-demand: it is registered in the package registry
    /// but excluded from the owner's default automatic sequence. It can be started
    /// explicitly through <see cref="ITutorialRunner.RunPackageAsync"/>.
    /// </summary>
    /// <returns>The same package builder.</returns>
    ITutorialPackageBuilder<TOwner> OnDemand();

    /// <summary>
    /// Completes and registers the current package.
    /// </summary>
    /// <returns>The owner builder.</returns>
    ITutorialOwnerBuilder<TOwner> Build();
}

/// <summary>
/// Fluent builder for a tutorial step.
/// </summary>
/// <typeparam name="TOwner">Owner type.</typeparam>
public interface ITutorialStepBuilder<TOwner> : ITutorialPackageBuilder<TOwner>
    where TOwner : FrameworkElement, ITutorialOwner<TOwner>
{
    /// <summary>
    /// Sets the step description text.
    /// </summary>
    /// <param name="description">Step description.</param>
    /// <returns>The same step builder.</returns>
    ITutorialStepBuilder<TOwner> Text(string description);

    /// <summary>
    /// Targets a named element.
    /// </summary>
    /// <param name="targetName">Target element name.</param>
    /// <returns>The same step builder.</returns>
    ITutorialStepBuilder<TOwner> TargetName(string targetName);

    /// <summary>
    /// Targets an element by tag.
    /// </summary>
    /// <param name="targetTag">Target tag.</param>
    /// <returns>The same step builder.</returns>
    ITutorialStepBuilder<TOwner> TargetTag(string targetTag);

    /// <summary>
    /// Targets a navigation item for a page.
    /// </summary>
    /// <typeparam name="TPage">Target page type.</typeparam>
    /// <returns>The same step builder.</returns>
    ITutorialStepBuilder<TOwner> TargetNavigation<TPage>()
        where TPage : Page;

    /// <summary>
    /// Targets the first descendant of a type under an optional host.
    /// </summary>
    /// <param name="hostTargetName">Optional host target element name.</param>
    /// <param name="targetType">Target descendant type.</param>
    /// <returns>The same step builder.</returns>
    ITutorialStepBuilder<TOwner> TargetDescendantType(string? hostTargetName, Type targetType);

    /// <summary>
    /// Clears target resolution and shows the step as a centered card.
    /// </summary>
    /// <returns>The same step builder.</returns>
    ITutorialStepBuilder<TOwner> NoTarget();

    /// <summary>
    /// Sets the interaction mode.
    /// </summary>
    /// <param name="interactionMode">Interaction mode.</param>
    /// <returns>The same step builder.</returns>
    ITutorialStepBuilder<TOwner> Interaction(ProductTourInteractionMode interactionMode);

    /// <summary>
    /// Sets the card placement.
    /// </summary>
    /// <param name="placement">Card placement.</param>
    /// <returns>The same step builder.</returns>
    ITutorialStepBuilder<TOwner> Placement(ProductTourPlacement placement);

    /// <summary>
    /// Sets the card offset.
    /// </summary>
    /// <param name="offset">Card offset.</param>
    /// <returns>The same step builder.</returns>
    ITutorialStepBuilder<TOwner> CardOffset(Point offset);

    /// <summary>
    /// Sets the avatar placement.
    /// </summary>
    /// <param name="placement">Avatar placement.</param>
    /// <returns>The same step builder.</returns>
    ITutorialStepBuilder<TOwner> AvatarPlacement(ProductTourAvatarPlacement placement);

    /// <summary>
    /// Sets the avatar pose.
    /// </summary>
    /// <param name="pose">Avatar pose.</param>
    /// <returns>The same step builder.</returns>
    ITutorialStepBuilder<TOwner> AvatarPose(TutorialAvatarPose pose);

    /// <summary>
    /// Requires a signal before the step can complete.
    /// </summary>
    /// <param name="signalId">Signal id.</param>
    /// <returns>The same step builder.</returns>
    ITutorialStepBuilder<TOwner> WaitFor(string signalId);

    /// <summary>
    /// Sets the target lookup and signal wait timeout.
    /// </summary>
    /// <param name="timeout">Timeout duration.</param>
    /// <returns>The same step builder.</returns>
    ITutorialStepBuilder<TOwner> Timeout(TimeSpan timeout);

    /// <summary>
    /// Allows this step to be skipped when the target is missing.
    /// </summary>
    /// <returns>The same step builder.</returns>
    ITutorialStepBuilder<TOwner> AllowMissingTarget();

    /// <summary>
    /// Appends an action invoked before target resolution and overlay display.
    /// </summary>
    /// <param name="action">Action to append.</param>
    /// <returns>The same step builder.</returns>
    ITutorialStepBuilder<TOwner> PreStepAction(TutorialStepAction action);

    /// <summary>
    /// Appends a named asynchronous action invoked before target resolution and overlay display.
    /// </summary>
    /// <param name="name">Diagnostic action name.</param>
    /// <param name="executeAsync">Asynchronous action body.</param>
    /// <returns>The same step builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="executeAsync"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    ITutorialStepBuilder<TOwner> PreStepAction(
        string name,
        Func<TutorialStepActionContext, CancellationToken, Task> executeAsync);

    /// <summary>
    /// Appends an asynchronous action invoked before target resolution and overlay display.
    /// The diagnostic name is captured automatically from the supplied lambda expression.
    /// </summary>
    /// <param name="executeAsync">Asynchronous action body.</param>
    /// <param name="name">Diagnostic action name, captured from <paramref name="executeAsync"/> by the compiler.</param>
    /// <returns>The same step builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="executeAsync"/> is <see langword="null"/>.</exception>
    ITutorialStepBuilder<TOwner> PreStepAction(
        Func<TutorialStepActionContext, CancellationToken, Task> executeAsync,
        [CallerArgumentExpression(nameof(executeAsync))] string name = "");

    /// <summary>
    /// Appends a named synchronous action invoked before target resolution and overlay display.
    /// </summary>
    /// <param name="name">Diagnostic action name.</param>
    /// <param name="action">Synchronous action body.</param>
    /// <returns>The same step builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    ITutorialStepBuilder<TOwner> PreStepAction(string name, Action<TutorialStepActionContext> action);

    /// <summary>
    /// Appends a synchronous action invoked before target resolution and overlay display.
    /// The diagnostic name is captured automatically from the supplied lambda expression.
    /// </summary>
    /// <param name="action">Synchronous action body.</param>
    /// <param name="name">Diagnostic action name, captured from <paramref name="action"/> by the compiler.</param>
    /// <returns>The same step builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    ITutorialStepBuilder<TOwner> PreStepAction(
        Action<TutorialStepActionContext> action,
        [CallerArgumentExpression(nameof(action))] string name = "");

    /// <summary>
    /// Appends an action invoked after step completion and overlay close.
    /// </summary>
    /// <param name="action">Action to append.</param>
    /// <returns>The same step builder.</returns>
    ITutorialStepBuilder<TOwner> PostStepAction(TutorialStepAction action);

    /// <summary>
    /// Appends a named asynchronous action invoked after step completion and overlay close.
    /// </summary>
    /// <param name="name">Diagnostic action name.</param>
    /// <param name="executeAsync">Asynchronous action body.</param>
    /// <returns>The same step builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="executeAsync"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    ITutorialStepBuilder<TOwner> PostStepAction(
        string name,
        Func<TutorialStepActionContext, CancellationToken, Task> executeAsync);

    /// <summary>
    /// Appends an asynchronous action invoked after step completion and overlay close.
    /// The diagnostic name is captured automatically from the supplied lambda expression.
    /// </summary>
    /// <param name="executeAsync">Asynchronous action body.</param>
    /// <param name="name">Diagnostic action name, captured from <paramref name="executeAsync"/> by the compiler.</param>
    /// <returns>The same step builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="executeAsync"/> is <see langword="null"/>.</exception>
    ITutorialStepBuilder<TOwner> PostStepAction(
        Func<TutorialStepActionContext, CancellationToken, Task> executeAsync,
        [CallerArgumentExpression(nameof(executeAsync))] string name = "");

    /// <summary>
    /// Appends a named synchronous action invoked after step completion and overlay close.
    /// </summary>
    /// <param name="name">Diagnostic action name.</param>
    /// <param name="action">Synchronous action body.</param>
    /// <returns>The same step builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    ITutorialStepBuilder<TOwner> PostStepAction(string name, Action<TutorialStepActionContext> action);

    /// <summary>
    /// Appends a synchronous action invoked after step completion and overlay close.
    /// The diagnostic name is captured automatically from the supplied lambda expression.
    /// </summary>
    /// <param name="action">Synchronous action body.</param>
    /// <param name="name">Diagnostic action name, captured from <paramref name="action"/> by the compiler.</param>
    /// <returns>The same step builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    ITutorialStepBuilder<TOwner> PostStepAction(
        Action<TutorialStepActionContext> action,
        [CallerArgumentExpression(nameof(action))] string name = "");
}

/// <summary>
/// High-level builder for tutorial flows.
/// </summary>
public interface ITutorialFlowBuilder
{
    /// <summary>
    /// Sets the flow version.
    /// </summary>
    /// <param name="version">Flow version.</param>
    /// <returns>The same flow builder.</returns>
    ITutorialFlowBuilder Version(int version);

    /// <summary>
    /// Adds a package step and automatically covers the package.
    /// </summary>
    /// <param name="package">Package reference.</param>
    /// <returns>The same flow builder.</returns>
    ITutorialFlowBuilder Step(TutorialPackageRef package);

    /// <summary>
    /// Adds a flow item.
    /// </summary>
    /// <param name="item">Flow item.</param>
    /// <returns>The same flow builder.</returns>
    ITutorialFlowBuilder Step(TutorialFlowItem item);

    /// <summary>
    /// Builds and registers the flow.
    /// </summary>
    /// <returns>The flow definition.</returns>
    TutorialFlowDefinition Build();
}

/// <summary>
/// Default high-level tutorial authoring builder.
/// </summary>
public sealed class TutorialBuilder : ITutorialBuilder
{
    private readonly ITutorialPackageRegistry _packageRegistry;
    private readonly ITutorialSequenceRegistry _sequenceRegistry;
    private readonly ITutorialFlowRegistry _flowRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="TutorialBuilder"/> class.
    /// </summary>
    /// <param name="packageRegistry">Package registry.</param>
    /// <param name="sequenceRegistry">Sequence registry.</param>
    /// <param name="flowRegistry">Flow registry.</param>
    public TutorialBuilder(
        ITutorialPackageRegistry packageRegistry,
        ITutorialSequenceRegistry sequenceRegistry,
        ITutorialFlowRegistry flowRegistry)
    {
        _packageRegistry = packageRegistry;
        _sequenceRegistry = sequenceRegistry;
        _flowRegistry = flowRegistry;
    }

    /// <inheritdoc />
    public ITutorialOwnerBuilder<TOwner> ForPage<TOwner>()
        where TOwner : Page, ITutorialOwner<TOwner> =>
        ForKey<TOwner>(TOwner.TutorialKey);

    /// <inheritdoc />
    public ITutorialOwnerBuilder<TOwner> ForWindow<TOwner>()
        where TOwner : Window, ITutorialOwner<TOwner> =>
        ForKey<TOwner>(TOwner.TutorialKey);

    /// <inheritdoc />
    public ITutorialOwnerBuilder<TOwner> ForRegion<TOwner>()
        where TOwner : FrameworkElement, ITutorialOwner<TOwner> =>
        ForKey<TOwner>(TOwner.TutorialKey);

    /// <inheritdoc />
    public ITutorialOwnerBuilder<TOwner> ForKey<TOwner>(string tutorialKey)
        where TOwner : FrameworkElement, ITutorialOwner<TOwner> =>
        new TutorialOwnerBuilder<TOwner>(_packageRegistry, _sequenceRegistry, tutorialKey);

    /// <inheritdoc />
    public ITutorialFlowBuilder Flow(string flowId) => new TutorialAuthoringFlowBuilder(_packageRegistry, _flowRegistry, flowId);

    /// <inheritdoc />
    public void RegisterOwner<TOwner>()
        where TOwner : FrameworkElement, ITutorialOwner<TOwner> =>
        TOwner.RegisterTutorials(this);

    /// <inheritdoc />
    public void RegisterApp<TApp>()
        where TApp : Application, IAppTutorial<TApp> =>
        TApp.RegisterTutorials(this);
}

internal sealed class TutorialOwnerBuilder<TOwner> : ITutorialOwnerBuilderInternal<TOwner>
    where TOwner : FrameworkElement, ITutorialOwner<TOwner>
{
    private readonly ITutorialPackageRegistry _packageRegistry;
    private readonly ITutorialSequenceRegistry _sequenceRegistry;
    private readonly string _tutorialKey;
    private readonly List<string> _packageIds = [];
    private readonly List<TutorialPackageDefinition> _packages = [];

    public TutorialOwnerBuilder(
        ITutorialPackageRegistry packageRegistry,
        ITutorialSequenceRegistry sequenceRegistry,
        string tutorialKey)
    {
        _packageRegistry = packageRegistry;
        _sequenceRegistry = sequenceRegistry;
        _tutorialKey = tutorialKey;
    }

    public ITutorialPackageBuilder<TOwner> Package(TutorialPackageRef package) =>
        new TutorialAuthoringPackageBuilder<TOwner>(this, _tutorialKey, package, _packageIds.Count + 1);

    public ITutorialOwnerBuilder<TOwner> Use(TutorialPackageRef package)
    {
        AddSequencePackage(package);
        return this;
    }

    internal void RegisterPackage(TutorialPackageDefinition package) =>
        RegisterPackage(package, isOnDemand: false);

    /// <inheritdoc />
    public void RegisterPackage(TutorialPackageDefinition package, bool isOnDemand)
    {
        ValidateDuplicateContent(package);
        _packageRegistry.Register(package);
        _packages.Add(package);
        if (!isOnDemand)
        {
            AddSequencePackage(new TutorialPackageRef(package.PackageId));
        }
    }

    private void AddSequencePackage(TutorialPackageRef package)
    {
        if (string.IsNullOrWhiteSpace(package.Id))
        {
            throw new ArgumentException("Package id cannot be empty.", nameof(package));
        }

        _packageIds.Add(package.Id);
        RegisterSequence();
    }

    private void RegisterSequence() =>
        _sequenceRegistry.RegisterSequence(_tutorialKey, _packageIds);

    private void ValidateDuplicateContent(TutorialPackageDefinition package)
    {
        var mainStep = package.Steps.FirstOrDefault();
        if (mainStep == null
            || mainStep.TargetKind == TutorialTargetKind.NavigationItem
            || string.IsNullOrWhiteSpace(mainStep.TargetName)
            || string.IsNullOrWhiteSpace(mainStep.Title))
        {
            return;
        }

        foreach (var existing in _packages)
        {
            var existingMainStep = existing.Steps.FirstOrDefault();
            if (existingMainStep == null
                || existingMainStep.TargetKind == TutorialTargetKind.NavigationItem
                || string.IsNullOrWhiteSpace(existingMainStep.TargetName))
            {
                continue;
            }

            if (string.Equals(existingMainStep.TargetName, mainStep.TargetName, StringComparison.Ordinal)
                && string.Equals(existingMainStep.Title, mainStep.Title, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Tutorial package '{package.PackageId}' duplicates the main content of '{existing.PackageId}'.");
            }
        }
    }
}

internal sealed class TutorialAuthoringPackageBuilder<TOwner> :
    ITutorialPackageBuilder<TOwner>,
    ITutorialStepBuilder<TOwner>
    where TOwner : FrameworkElement, ITutorialOwner<TOwner>
{
    private readonly ITutorialOwnerBuilderInternal<TOwner> _ownerBuilder;
    private readonly string _tutorialKey;
    private readonly TutorialPackageRef _package;
    private readonly int _sequence;
    private readonly List<TutorialPackageItem> _items = [];
    private ProductTourStep? _currentStep;
    private bool _isOnDemand;

    public TutorialAuthoringPackageBuilder(
        ITutorialOwnerBuilderInternal<TOwner> ownerBuilder,
        string tutorialKey,
        TutorialPackageRef package,
        int sequence)
    {
        _ownerBuilder = ownerBuilder;
        _tutorialKey = tutorialKey;
        _package = package;
        _sequence = sequence;
    }

    public ITutorialStepBuilder<TOwner> Step(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Step title cannot be empty.", nameof(title));
        }

        var step = new ProductTourStep
        {
            Title = title,
            Timeout = TimeSpan.FromSeconds(30)
        };
        _items.Add(new TutorialPackageStepItem { Step = step });
        _currentStep = step;
        return this;
    }

    public ITutorialPackageBuilder<TOwner> Dialogue(DialogueFlowItem dialogue)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        _items.Add(new TutorialPackageDialogueItem { Dialogue = dialogue });
        _currentStep = null;
        return this;
    }

    public ITutorialPackageBuilder<TOwner> OnDemand()
    {
        _isOnDemand = true;
        return this;
    }

    public ITutorialStepBuilder<TOwner> Text(string description)
    {
        EnsureCurrentStep().Description = description;
        return this;
    }

    public ITutorialStepBuilder<TOwner> TargetName(string targetName)
    {
        var step = EnsureCurrentStep();
        step.TargetKind = TutorialTargetKind.Name;
        step.TargetName = targetName;
        step.TargetKey = null;
        return this;
    }

    public ITutorialStepBuilder<TOwner> TargetTag(string targetTag)
    {
        var step = EnsureCurrentStep();
        step.TargetKind = TutorialTargetKind.ElementTag;
        step.TargetName = null;
        step.TargetKey = targetTag;
        return this;
    }

    public ITutorialStepBuilder<TOwner> TargetNavigation<TPage>()
        where TPage : Page
    {
        var step = EnsureCurrentStep();
        step.TargetKind = TutorialTargetKind.NavigationItem;
        step.TargetName = null;
        step.TargetKey = typeof(TPage).FullName;
        return this;
    }

    public ITutorialStepBuilder<TOwner> TargetDescendantType(string? hostTargetName, Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        var step = EnsureCurrentStep();
        step.TargetKind = TutorialTargetKind.DescendantType;
        step.TargetName = hostTargetName;
        step.TargetKey = targetType.FullName;
        return this;
    }

    public ITutorialStepBuilder<TOwner> NoTarget()
    {
        var step = EnsureCurrentStep();
        step.TargetKind = TutorialTargetKind.None;
        step.TargetName = null;
        step.TargetKey = null;
        return this;
    }

    public ITutorialStepBuilder<TOwner> Interaction(ProductTourInteractionMode interactionMode)
    {
        EnsureCurrentStep().InteractionMode = interactionMode;
        return this;
    }

    public ITutorialStepBuilder<TOwner> Placement(ProductTourPlacement placement)
    {
        EnsureCurrentStep().Placement = placement;
        return this;
    }

    public ITutorialStepBuilder<TOwner> CardOffset(Point offset)
    {
        EnsureCurrentStep().CardOffset = offset;
        return this;
    }

    public ITutorialStepBuilder<TOwner> AvatarPlacement(ProductTourAvatarPlacement placement)
    {
        EnsureCurrentStep().AvatarPlacement = placement;
        return this;
    }

    public ITutorialStepBuilder<TOwner> AvatarPose(TutorialAvatarPose pose)
    {
        EnsureCurrentStep().AvatarPose = pose;
        return this;
    }

    public ITutorialStepBuilder<TOwner> WaitFor(string signalId)
    {
        var step = EnsureCurrentStep();
        step.WaitForSignalId = signalId;
        step.ExpectedAction = TutorialExpectedAction.SignalReceived;
        step.InteractionMode = step.InteractionMode == ProductTourInteractionMode.BlockAll
            ? ProductTourInteractionMode.AllowTargetOnly
            : step.InteractionMode;
        return this;
    }

    public ITutorialStepBuilder<TOwner> Timeout(TimeSpan timeout)
    {
        EnsureCurrentStep().Timeout = timeout;
        return this;
    }

    public ITutorialStepBuilder<TOwner> AllowMissingTarget()
    {
        EnsureCurrentStep().AllowMissingTarget = true;
        return this;
    }

    public ITutorialStepBuilder<TOwner> PreStepAction(TutorialStepAction action)
    {
        EnsureCurrentStep().PreStepActions.Add(action);
        return this;
    }

    public ITutorialStepBuilder<TOwner> PreStepAction(
        string name,
        Func<TutorialStepActionContext, CancellationToken, Task> executeAsync)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
        EnsureCurrentStep().PreStepActions.Add(new TutorialStepAction(name, executeAsync));
        return this;
    }

    public ITutorialStepBuilder<TOwner> PreStepAction(
        Func<TutorialStepActionContext, CancellationToken, Task> executeAsync,
        [CallerArgumentExpression(nameof(executeAsync))] string name = "")
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
        EnsureCurrentStep().PreStepActions.Add(
            new TutorialStepAction(NormalizeActionName(name), executeAsync));
        return this;
    }

    public ITutorialStepBuilder<TOwner> PreStepAction(string name, Action<TutorialStepActionContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        EnsureCurrentStep().PreStepActions.Add(WrapSynchronous(name, action));
        return this;
    }

    public ITutorialStepBuilder<TOwner> PreStepAction(
        Action<TutorialStepActionContext> action,
        [CallerArgumentExpression(nameof(action))] string name = "")
    {
        ArgumentNullException.ThrowIfNull(action);
        EnsureCurrentStep().PreStepActions.Add(WrapSynchronous(NormalizeActionName(name), action));
        return this;
    }

    public ITutorialStepBuilder<TOwner> PostStepAction(TutorialStepAction action)
    {
        EnsureCurrentStep().PostStepActions.Add(action);
        return this;
    }

    public ITutorialStepBuilder<TOwner> PostStepAction(
        string name,
        Func<TutorialStepActionContext, CancellationToken, Task> executeAsync)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
        EnsureCurrentStep().PostStepActions.Add(new TutorialStepAction(name, executeAsync));
        return this;
    }

    public ITutorialStepBuilder<TOwner> PostStepAction(
        Func<TutorialStepActionContext, CancellationToken, Task> executeAsync,
        [CallerArgumentExpression(nameof(executeAsync))] string name = "")
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
        EnsureCurrentStep().PostStepActions.Add(
            new TutorialStepAction(NormalizeActionName(name), executeAsync));
        return this;
    }

    public ITutorialStepBuilder<TOwner> PostStepAction(string name, Action<TutorialStepActionContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        EnsureCurrentStep().PostStepActions.Add(WrapSynchronous(name, action));
        return this;
    }

    public ITutorialStepBuilder<TOwner> PostStepAction(
        Action<TutorialStepActionContext> action,
        [CallerArgumentExpression(nameof(action))] string name = "")
    {
        ArgumentNullException.ThrowIfNull(action);
        EnsureCurrentStep().PostStepActions.Add(WrapSynchronous(NormalizeActionName(name), action));
        return this;
    }

    private static TutorialStepAction WrapSynchronous(string name, Action<TutorialStepActionContext> action) =>
        new(name, (context, _) =>
        {
            action(context);
            return Task.CompletedTask;
        });

    private static string NormalizeActionName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Lambda";
        }

        var compressed = string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return compressed.Length <= 80 ? compressed : compressed[..77] + "...";
    }

    public ITutorialOwnerBuilder<TOwner> Build()
    {
        _ownerBuilder.RegisterPackage(new TutorialPackageDefinition
        {
            PackageId = _package.Id,
            PageKey = _tutorialKey,
            Sequence = _sequence,
            Items = _items.ToArray()
        }, _isOnDemand);
        return _ownerBuilder;
    }

    public ITutorialPackageBuilder<TOwner> Package(TutorialPackageRef package)
    {
        Build();
        return _ownerBuilder.Package(package);
    }

    public ITutorialOwnerBuilder<TOwner> Use(TutorialPackageRef package)
    {
        Build();
        return _ownerBuilder.Use(package);
    }

    private ProductTourStep EnsureCurrentStep()
    {
        if (_currentStep == null)
        {
            throw new InvalidOperationException("No tutorial step is being configured.");
        }

        return _currentStep;
    }
}

internal sealed class TutorialAuthoringFlowBuilder : ITutorialFlowBuilder
{
    private readonly ITutorialPackageRegistry _packageRegistry;
    private readonly ITutorialFlowRegistry _flowRegistry;
    private readonly string _flowId;
    private readonly List<TutorialFlowItem> _items = [];
    private readonly List<string> _coveredPackageIds = [];
    private int _version = 1;

    public TutorialAuthoringFlowBuilder(
        ITutorialPackageRegistry packageRegistry,
        ITutorialFlowRegistry flowRegistry,
        string flowId)
    {
        _packageRegistry = packageRegistry;
        _flowRegistry = flowRegistry;
        _flowId = flowId;
    }

    public ITutorialFlowBuilder Version(int version)
    {
        _version = version;
        return this;
    }

    public ITutorialFlowBuilder Step(TutorialPackageRef package)
    {
        if (string.IsNullOrWhiteSpace(package.Id))
        {
            throw new ArgumentException("Package id cannot be empty.", nameof(package));
        }

        _items.Add(new PackageFlowItem { PackageId = package.Id });
        if (!_coveredPackageIds.Contains(package.Id, StringComparer.Ordinal))
        {
            _coveredPackageIds.Add(package.Id);
        }

        return this;
    }

    public ITutorialFlowBuilder Step(TutorialFlowItem item)
    {
        _items.Add(item);
        return this;
    }

    public TutorialFlowDefinition Build()
    {
        ValidatePackageReferences();
        var flow = new TutorialFlowDefinition
        {
            FlowId = _flowId,
            Version = _version,
            IncludedPackageIds = _coveredPackageIds.ToArray(),
            Items = _items.ToArray()
        };
        _flowRegistry.Register(flow);
        return flow;
    }

    private void ValidatePackageReferences()
    {
        foreach (var packageId in _coveredPackageIds)
        {
            var package = _packageRegistry.GetPackage(packageId);
            if (package == null)
            {
                throw new InvalidOperationException($"Tutorial flow '{_flowId}' references missing package '{packageId}'.");
            }

            if (package.Steps.Any(step =>
                    string.Equals(step.Title, "功能教学", StringComparison.Ordinal)
                    || step.Description.Contains("详细教学将在", StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"Tutorial flow '{_flowId}' references fallback package '{packageId}'.");
            }
        }
    }
}
