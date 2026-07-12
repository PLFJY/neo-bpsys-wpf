namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// 教程包定义的注册表。
/// </summary>
public interface ITutorialPackageRegistry
{
    /// <summary>注册一个教程包。</summary>
    /// <param name="definition">要注册的包定义。</param>
    void Register(TutorialPackageDefinition definition);

    /// <summary>按 id 获取包。</summary>
    /// <param name="packageId">包 id。</param>
    /// <returns>包定义；若不存在则返回 null。</returns>
    TutorialPackageDefinition? GetPackage(string packageId);

    /// <summary>获取所有已注册的包。</summary>
    /// <returns>已注册的包。</returns>
    IReadOnlyCollection<TutorialPackageDefinition> GetPackages();
}

/// <summary>
/// 默认的教程包注册表。
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
/// 页面包序列的注册表。
/// </summary>
public interface ITutorialSequenceRegistry
{
    /// <summary>为指定页面键注册包 id 序列。</summary>
    /// <param name="pageKey">页面键。</param>
    /// <param name="packageIds">按顺序排列的包 id。</param>
    void RegisterSequence(string pageKey, IEnumerable<string> packageIds);

    /// <summary>获取指定页面键的包 id 序列。</summary>
    /// <param name="pageKey">页面键。</param>
    /// <returns>包 id 列表。</returns>
    IReadOnlyList<string> GetSequence(string pageKey);

    /// <summary>获取指定页面键的序列定义。</summary>
    /// <param name="pageKey">页面键。</param>
    /// <returns>序列定义。</returns>
    TutorialSequenceDefinition GetSequenceDefinition(string pageKey);
}

/// <summary>
/// 默认的页面序列注册表。
/// </summary>
public sealed class TutorialSequenceRegistry : ITutorialSequenceRegistry
{
    private readonly Dictionary<string, TutorialSequenceDefinition> _sequences = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void RegisterSequence(string pageKey, IEnumerable<string> packageIds) =>
        _sequences[pageKey] = new TutorialSequenceDefinition
        {
            PageKey = pageKey,
            PackageIds = packageIds.ToArray()
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
/// 教程流程的注册表。
/// </summary>
public interface ITutorialFlowRegistry
{
    /// <summary>注册一个教程流程。</summary>
    /// <param name="definition">流程定义。</param>
    void Register(TutorialFlowDefinition definition);

    /// <summary>按 id 获取流程。</summary>
    /// <param name="flowId">流程 id。</param>
    /// <returns>流程定义；若不存在则返回 null。</returns>
    TutorialFlowDefinition? GetFlow(string flowId);

    /// <summary>获取所有已注册的流程。</summary>
    /// <returns>已注册的流程定义。</returns>
    IReadOnlyCollection<TutorialFlowDefinition> GetFlows();
}

/// <summary>
/// 默认的流程注册表。
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
