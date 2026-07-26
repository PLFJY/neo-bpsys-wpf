using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;

namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// 注册由动态加载程序集拥有的教程的运行时贡献者。
/// </summary>
public interface ITutorialRegistrationContributor
{
    /// <summary>获取用于幂等注册的稳定注册 id。</summary>
    string RegistrationId { get; }

    /// <summary>
    /// 使用提供的构建器注册该贡献者拥有的教程。
    /// </summary>
    /// <param name="builder">教程构建器。</param>
    void RegisterTutorials(ITutorialBuilder builder);
}

/// <summary>
/// 宿主侧服务，接受来自动态加载贡献者的运行时教程注册。
/// </summary>
public interface ITutorialRegistrationService
{
    /// <summary>
    /// 注册贡献者的教程。通过 <see cref="ITutorialRegistrationContributor.RegistrationId"/> 保证幂等。
    /// </summary>
    /// <param name="contributor">要注册的贡献者。</param>
    /// <exception cref="ArgumentNullException"><paramref name="contributor"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentException"><paramref name="contributor.RegistrationId"/> 为空。</exception>
    /// <exception cref="InvalidOperationException">不同的贡献者已注册了重复的 PackageId。</exception>
    void RegisterContributor(ITutorialRegistrationContributor contributor);
}

/// <summary>
/// <see cref="ITutorialRegistrationService"/> 的默认实现。
/// </summary>
public sealed class TutorialRegistrationService : ITutorialRegistrationService
{
    private readonly ITutorialPackageRegistry _packageRegistry;
    private readonly ITutorialSequenceRegistry _sequenceRegistry;
    private readonly ITutorialFlowRegistry _flowRegistry;
    private readonly ILogger<TutorialRegistrationService> _logger;
    private readonly Dictionary<string, string> _registeredPackageOwners = new(StringComparer.Ordinal);
    private readonly HashSet<string> _registeredContributorIds = new(StringComparer.Ordinal);
    private readonly object _syncRoot = new();

    /// <summary>
    /// 初始化 <see cref="TutorialRegistrationService"/> 类的新实例。
    /// </summary>
    /// <param name="packageRegistry">包注册表。</param>
    /// <param name="sequenceRegistry">序列注册表。</param>
    /// <param name="flowRegistry">流程注册表。</param>
    /// <param name="logger">日志记录器。</param>
    public TutorialRegistrationService(
        ITutorialPackageRegistry packageRegistry,
        ITutorialSequenceRegistry sequenceRegistry,
        ITutorialFlowRegistry flowRegistry,
        ILogger<TutorialRegistrationService> logger)
    {
        _packageRegistry = packageRegistry;
        _sequenceRegistry = sequenceRegistry;
        _flowRegistry = flowRegistry;
        _logger = logger;
    }

    /// <inheritdoc />
    public void RegisterContributor(ITutorialRegistrationContributor contributor)
    {
        ArgumentNullException.ThrowIfNull(contributor);
        if (string.IsNullOrWhiteSpace(contributor.RegistrationId))
        {
            throw new ArgumentException("Contributor RegistrationId cannot be empty.", nameof(contributor));
        }

        lock (_syncRoot)
        {
            if (!_registeredContributorIds.Add(contributor.RegistrationId))
            {
                _logger.LogInformation(
                    "Tutorial contributor already registered. RegistrationId={RegistrationId}",
                    contributor.RegistrationId);
                return;
            }
        }

        var builder = new ContributorAwareBuilder(
            _packageRegistry,
            _sequenceRegistry,
            _flowRegistry,
            _registeredPackageOwners,
            contributor.RegistrationId,
            _syncRoot,
            _logger);

        contributor.RegisterTutorials(builder);
        _logger.LogInformation(
            "Tutorial contributor registered. RegistrationId={RegistrationId}",
            contributor.RegistrationId);
    }

    private sealed class ContributorAwareBuilder : ITutorialBuilder
    {
        private readonly ITutorialPackageRegistry _packageRegistry;
        private readonly ITutorialSequenceRegistry _sequenceRegistry;
        private readonly ITutorialFlowRegistry _flowRegistry;
        private readonly Dictionary<string, string> _registeredPackageOwners;
        private readonly string _contributorId;
        private readonly object _syncRoot;
        private readonly ILogger _logger;

        public ContributorAwareBuilder(
            ITutorialPackageRegistry packageRegistry,
            ITutorialSequenceRegistry sequenceRegistry,
            ITutorialFlowRegistry flowRegistry,
            Dictionary<string, string> registeredPackageOwners,
            string contributorId,
            object syncRoot,
            ILogger logger)
        {
            _packageRegistry = packageRegistry;
            _sequenceRegistry = sequenceRegistry;
            _flowRegistry = flowRegistry;
            _registeredPackageOwners = registeredPackageOwners;
            _contributorId = contributorId;
            _syncRoot = syncRoot;
            _logger = logger;
        }

        public ITutorialOwnerBuilder<TOwner> ForPage<TOwner>()
            where TOwner : Page, ITutorialOwner<TOwner> =>
            ForKey<TOwner>(TOwner.TutorialKey);

        public ITutorialOwnerBuilder<TOwner> ForWindow<TOwner>()
            where TOwner : Window, ITutorialOwner<TOwner> =>
            ForKey<TOwner>(TOwner.TutorialKey);

        public ITutorialOwnerBuilder<TOwner> ForRegion<TOwner>()
            where TOwner : FrameworkElement, ITutorialOwner<TOwner> =>
            ForKey<TOwner>(TOwner.TutorialKey);

        public ITutorialOwnerBuilder<TOwner> ForKey<TOwner>(string tutorialKey)
            where TOwner : FrameworkElement, ITutorialOwner<TOwner> =>
            new ContributorAwareOwnerBuilder<TOwner>(
                _packageRegistry,
                _sequenceRegistry,
                tutorialKey,
                _registeredPackageOwners,
                _contributorId,
                _syncRoot,
                _logger);

        public ITutorialFlowBuilder Flow(string flowId) =>
            new TutorialAuthoringFlowBuilder(_packageRegistry, _flowRegistry, flowId);

        public void RegisterOwner<TOwner>()
            where TOwner : FrameworkElement, ITutorialOwner<TOwner> =>
            TOwner.RegisterTutorials(this);

        public void RegisterApp<TApp>()
            where TApp : Application, IAppTutorial<TApp> =>
            TApp.RegisterTutorials(this);
    }

    private sealed class ContributorAwareOwnerBuilder<TOwner> : ITutorialOwnerBuilderInternal<TOwner>
        where TOwner : FrameworkElement, ITutorialOwner<TOwner>
    {
        private readonly ITutorialPackageRegistry _packageRegistry;
        private readonly ITutorialSequenceRegistry _sequenceRegistry;
        private readonly string _tutorialKey;
        private readonly Dictionary<string, string> _registeredPackageOwners;
        private readonly string _contributorId;
        private readonly object _syncRoot;
        private readonly ILogger _logger;
        private readonly List<string> _packageIds = [];

        public ContributorAwareOwnerBuilder(
            ITutorialPackageRegistry packageRegistry,
            ITutorialSequenceRegistry sequenceRegistry,
            string tutorialKey,
            Dictionary<string, string> registeredPackageOwners,
            string contributorId,
            object syncRoot,
            ILogger logger)
        {
            _packageRegistry = packageRegistry;
            _sequenceRegistry = sequenceRegistry;
            _tutorialKey = tutorialKey;
            _registeredPackageOwners = registeredPackageOwners;
            _contributorId = contributorId;
            _syncRoot = syncRoot;
            _logger = logger;
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
            lock (_syncRoot)
            {
                if (_registeredPackageOwners.TryGetValue(package.PackageId, out var existingOwner))
                {
                    throw new InvalidOperationException(
                        $"Tutorial package '{package.PackageId}' is already registered by contributor '{existingOwner}'. "
                        + $"Contributor '{_contributorId}' cannot register the same package id.");
                }

                _registeredPackageOwners[package.PackageId] = _contributorId;
            }

            _packageRegistry.Register(package);
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
            _sequenceRegistry.RegisterSequence(_tutorialKey, _packageIds);
        }
    }
}
