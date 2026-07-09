namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// Registry for tutorial package definitions.
/// </summary>
public interface ITutorialPackageRegistry
{
    /// <summary>Registers a tutorial package.</summary>
    /// <param name="definition">Package definition to register.</param>
    void Register(TutorialPackageDefinition definition);

    /// <summary>Gets a package by id.</summary>
    /// <param name="packageId">Package id.</param>
    /// <returns>The package definition, or null when missing.</returns>
    TutorialPackageDefinition? GetPackage(string packageId);

    /// <summary>Gets all registered packages.</summary>
    /// <returns>Registered packages.</returns>
    IReadOnlyCollection<TutorialPackageDefinition> GetPackages();
}

/// <summary>
/// Default tutorial package registry.
/// </summary>
public sealed class TutorialPackageRegistry : ITutorialPackageRegistry
{
    private readonly Dictionary<string, TutorialPackageDefinition> _packages = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Register(TutorialPackageDefinition definition)
    {
        _packages[definition.PackageId] = definition;
    }

    /// <inheritdoc />
    public TutorialPackageDefinition? GetPackage(string packageId) =>
        _packages.TryGetValue(packageId, out var package) ? package : null;

    /// <inheritdoc />
    public IReadOnlyCollection<TutorialPackageDefinition> GetPackages() => _packages.Values;

}

/// <summary>
/// Registry for page package sequences.
/// </summary>
public interface ITutorialSequenceRegistry
{
    /// <summary>Registers package ids for a page key.</summary>
    /// <param name="pageKey">Page key.</param>
    /// <param name="packageIds">Package ids in sequence order.</param>
    void RegisterSequence(string pageKey, IEnumerable<string> packageIds);

    /// <summary>Registers package ids and automatic run strategy for a page key.</summary>
    /// <param name="pageKey">Page key.</param>
    /// <param name="packageIds">Package ids in sequence order.</param>
    /// <param name="autoRunStrategy">Automatic run strategy.</param>
    void RegisterSequence(
        string pageKey,
        IEnumerable<string> packageIds,
        TutorialAutoRunStrategy autoRunStrategy);

    /// <summary>Gets package ids for a page key.</summary>
    /// <param name="pageKey">Page key.</param>
    /// <returns>Package ids.</returns>
    IReadOnlyList<string> GetSequence(string pageKey);

    /// <summary>Gets the sequence definition for a page key.</summary>
    /// <param name="pageKey">Page key.</param>
    /// <returns>The sequence definition.</returns>
    TutorialSequenceDefinition GetSequenceDefinition(string pageKey);
}

/// <summary>
/// Default page sequence registry.
/// </summary>
public sealed class TutorialSequenceRegistry : ITutorialSequenceRegistry
{
    private readonly Dictionary<string, TutorialSequenceDefinition> _sequences = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void RegisterSequence(string pageKey, IEnumerable<string> packageIds) =>
        RegisterSequence(pageKey, packageIds, TutorialAutoRunStrategy.SinglePendingPackage);

    /// <inheritdoc />
    public void RegisterSequence(
        string pageKey,
        IEnumerable<string> packageIds,
        TutorialAutoRunStrategy autoRunStrategy) =>
        _sequences[pageKey] = new TutorialSequenceDefinition
        {
            PageKey = pageKey,
            PackageIds = packageIds.ToArray(),
            AutoRunStrategy = autoRunStrategy
        };

    /// <inheritdoc />
    public IReadOnlyList<string> GetSequence(string pageKey) =>
        GetSequenceDefinition(pageKey).PackageIds;

    /// <inheritdoc />
    public TutorialSequenceDefinition GetSequenceDefinition(string pageKey) =>
        _sequences.TryGetValue(pageKey, out var definition)
            ? definition
            : new TutorialSequenceDefinition { PageKey = pageKey };
}

/// <summary>
/// Registry for tutorial flows.
/// </summary>
public interface ITutorialFlowRegistry
{
    /// <summary>Registers a tutorial flow.</summary>
    /// <param name="definition">Flow definition.</param>
    void Register(TutorialFlowDefinition definition);

    /// <summary>Gets a flow by id.</summary>
    /// <param name="flowId">Flow id.</param>
    /// <returns>The flow definition, or null when missing.</returns>
    TutorialFlowDefinition? GetFlow(string flowId);

    /// <summary>Gets all registered flows.</summary>
    /// <returns>Registered flow definitions.</returns>
    IReadOnlyCollection<TutorialFlowDefinition> GetFlows();
}

/// <summary>
/// Default flow registry.
/// </summary>
public sealed class TutorialFlowRegistry : ITutorialFlowRegistry
{
    private readonly Dictionary<string, TutorialFlowDefinition> _flows = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Register(TutorialFlowDefinition definition) => _flows[definition.FlowId] = definition;

    /// <inheritdoc />
    public TutorialFlowDefinition? GetFlow(string flowId) =>
        _flows.TryGetValue(flowId, out var flow) ? flow : null;

    /// <inheritdoc />
    public IReadOnlyCollection<TutorialFlowDefinition> GetFlows() => _flows.Values;
}
