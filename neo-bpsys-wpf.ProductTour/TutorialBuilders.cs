namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// Fluent builder for <see cref="TutorialPackageDefinition"/>.
/// </summary>
public sealed class TutorialPackageBuilder
{
    private readonly string _packageId;
    private readonly List<ProductTourStep> _steps = [];
    private string _pageKey = string.Empty;
    private int _version = 1;
    private int _sequence;
    private string _kind = "ProductTour";

    private TutorialPackageBuilder(string packageId)
    {
        _packageId = packageId;
    }

    /// <summary>Creates a package builder.</summary>
    /// <param name="packageId">Stable package id.</param>
    /// <returns>A new package builder.</returns>
    public static TutorialPackageBuilder Create(string packageId) => new(packageId);

    /// <summary>Sets the page key.</summary>
    /// <param name="pageKey">Page key.</param>
    /// <returns>The same builder.</returns>
    public TutorialPackageBuilder ForPage(string pageKey)
    {
        _pageKey = pageKey;
        return this;
    }

    /// <summary>Sets the package version.</summary>
    /// <param name="version">Package version.</param>
    /// <returns>The same builder.</returns>
    public TutorialPackageBuilder Version(int version)
    {
        _version = version;
        return this;
    }

    /// <summary>Sets the package sequence.</summary>
    /// <param name="sequence">Package sequence within the page.</param>
    /// <returns>The same builder.</returns>
    public TutorialPackageBuilder Sequence(int sequence)
    {
        _sequence = sequence;
        return this;
    }

    /// <summary>Sets the package kind.</summary>
    /// <param name="kind">Package kind.</param>
    /// <returns>The same builder.</returns>
    public TutorialPackageBuilder Kind(string kind)
    {
        _kind = kind;
        return this;
    }

    /// <summary>Starts building a product tour step.</summary>
    /// <param name="targetName">Target element name.</param>
    /// <returns>A step builder.</returns>
    public ProductTourStepBuilder Step(string? targetName = null) => new(this, targetName);

    /// <summary>Adds a completed step to the package.</summary>
    /// <param name="step">Step to add.</param>
    /// <returns>The same builder.</returns>
    public TutorialPackageBuilder AddStep(ProductTourStep step)
    {
        _steps.Add(step);
        return this;
    }

    /// <summary>Builds the package definition.</summary>
    /// <returns>The package definition.</returns>
    public TutorialPackageDefinition Build() =>
        new()
        {
            PackageId = _packageId,
            PageKey = _pageKey,
            Version = _version,
            Sequence = _sequence,
            Kind = _kind,
            Steps = _steps.ToArray()
        };
}

/// <summary>
/// Fluent builder for <see cref="ProductTourStep"/>.
/// </summary>
public sealed class ProductTourStepBuilder
{
    private readonly TutorialPackageBuilder _packageBuilder;
    private readonly ProductTourStep _step;

    internal ProductTourStepBuilder(TutorialPackageBuilder packageBuilder, string? targetName)
    {
        _packageBuilder = packageBuilder;
        _step = new ProductTourStep { TargetName = targetName };
    }

    /// <summary>Sets the step title.</summary>
    /// <param name="title">Step title.</param>
    /// <returns>The same step builder.</returns>
    public ProductTourStepBuilder Title(string title)
    {
        _step.Title = title;
        return this;
    }

    /// <summary>Sets the step description.</summary>
    /// <param name="description">Step description.</param>
    /// <returns>The same step builder.</returns>
    public ProductTourStepBuilder Description(string description)
    {
        _step.Description = description;
        return this;
    }

    /// <summary>Sets the card placement.</summary>
    /// <param name="placement">Card placement.</param>
    /// <returns>The same step builder.</returns>
    public ProductTourStepBuilder Placement(ProductTourPlacement placement)
    {
        _step.Placement = placement;
        return this;
    }

    /// <summary>Sets the interaction mode.</summary>
    /// <param name="interactionMode">Interaction mode.</param>
    /// <returns>The same step builder.</returns>
    public ProductTourStepBuilder Interaction(ProductTourInteractionMode interactionMode)
    {
        _step.InteractionMode = interactionMode;
        return this;
    }

    /// <summary>Sets a required signal for the step.</summary>
    /// <param name="signalId">Signal id.</param>
    /// <returns>The same step builder.</returns>
    public ProductTourStepBuilder WaitForSignal(string signalId)
    {
        _step.WaitForSignalId = signalId;
        _step.ExpectedAction = TutorialExpectedAction.SignalReceived;
        return this;
    }

    /// <summary>Sets the timeout used by target lookup and signal waits.</summary>
    /// <param name="timeout">Timeout value.</param>
    /// <returns>The same step builder.</returns>
    public ProductTourStepBuilder Timeout(TimeSpan timeout)
    {
        _step.Timeout = timeout;
        return this;
    }

    /// <summary>Allows this step to be skipped when the target is missing.</summary>
    /// <returns>The same step builder.</returns>
    public ProductTourStepBuilder AllowMissingTarget()
    {
        _step.AllowMissingTarget = true;
        return this;
    }

    /// <summary>Sets the expected action.</summary>
    /// <param name="expectedAction">Expected action.</param>
    /// <returns>The same step builder.</returns>
    public ProductTourStepBuilder ExpectedAction(TutorialExpectedAction expectedAction)
    {
        _step.ExpectedAction = expectedAction;
        return this;
    }

    /// <summary>Completes the current step and returns to the package builder.</summary>
    /// <returns>The parent package builder.</returns>
    public TutorialPackageBuilder EndStep() => _packageBuilder.AddStep(_step);
}

/// <summary>
/// Fluent builder for <see cref="TutorialFlowDefinition"/>.
/// </summary>
public sealed class TutorialFlowBuilder
{
    private readonly string _flowId;
    private readonly List<string> _includedPackageIds = [];
    private readonly List<TutorialFlowItem> _items = [];
    private int _version = 1;

    private TutorialFlowBuilder(string flowId)
    {
        _flowId = flowId;
    }

    /// <summary>Creates a flow builder.</summary>
    /// <param name="flowId">Stable flow id.</param>
    /// <returns>A new flow builder.</returns>
    public static TutorialFlowBuilder Create(string flowId) => new(flowId);

    /// <summary>Sets the flow version.</summary>
    /// <param name="version">Flow version.</param>
    /// <returns>The same builder.</returns>
    public TutorialFlowBuilder Version(int version)
    {
        _version = version;
        return this;
    }

    /// <summary>Adds an included package id.</summary>
    /// <param name="packageId">Package id included by the flow.</param>
    /// <returns>The same builder.</returns>
    public TutorialFlowBuilder Include(string packageId)
    {
        _includedPackageIds.Add(packageId);
        return this;
    }

    /// <summary>Adds a package flow item.</summary>
    /// <param name="packageId">Referenced package id.</param>
    /// <returns>The same builder.</returns>
    public TutorialFlowBuilder Package(string packageId)
    {
        _items.Add(new PackageFlowItem { PackageId = packageId });
        return this;
    }

    /// <summary>Adds a dialogue flow item.</summary>
    /// <param name="speaker">Speaker name.</param>
    /// <param name="lines">Dialogue lines.</param>
    /// <returns>The same builder.</returns>
    public TutorialFlowBuilder Dialogue(string speaker, params string[] lines)
    {
        _items.Add(new DialogueFlowItem { Speaker = speaker, Lines = lines });
        return this;
    }

    /// <summary>Adds a custom flow item.</summary>
    /// <param name="item">Flow item.</param>
    /// <returns>The same builder.</returns>
    public TutorialFlowBuilder Item(TutorialFlowItem item)
    {
        _items.Add(item);
        return this;
    }

    /// <summary>Builds the flow definition.</summary>
    /// <returns>The flow definition.</returns>
    public TutorialFlowDefinition Build() =>
        new()
        {
            FlowId = _flowId,
            Version = _version,
            IncludedPackageIds = _includedPackageIds.ToArray(),
            Items = _items.ToArray()
        };
}
