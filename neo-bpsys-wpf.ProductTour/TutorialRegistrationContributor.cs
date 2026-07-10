using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;

namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// Runtime contributor that registers tutorials owned by a dynamically loaded assembly.
/// </summary>
public interface ITutorialRegistrationContributor
{
    /// <summary>Gets the stable registration id used for idempotent registration.</summary>
    string RegistrationId { get; }

    /// <summary>
    /// Registers tutorials owned by this contributor using the supplied builder.
    /// </summary>
    /// <param name="builder">Tutorial builder.</param>
    void RegisterTutorials(ITutorialBuilder builder);
}

/// <summary>
/// Host-side service that accepts runtime tutorial registrations from dynamically loaded contributors.
/// </summary>
public interface ITutorialRegistrationService
{
    /// <summary>
    /// Registers a contributor's tutorials. Idempotent by <see cref="ITutorialRegistrationContributor.RegistrationId"/>.
    /// </summary>
    /// <param name="contributor">Contributor to register.</param>
    /// <exception cref="ArgumentNullException"><paramref name="contributor"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="contributor.RegistrationId"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">A different contributor already registered a duplicate PackageId.</exception>
    void RegisterContributor(ITutorialRegistrationContributor contributor);
}

/// <summary>
/// Default implementation of <see cref="ITutorialRegistrationService"/>.
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
    /// Initializes a new instance of the <see cref="TutorialRegistrationService"/> class.
    /// </summary>
    /// <param name="packageRegistry">Package registry.</param>
    /// <param name="sequenceRegistry">Sequence registry.</param>
    /// <param name="flowRegistry">Flow registry.</param>
    /// <param name="logger">Logger.</param>
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
