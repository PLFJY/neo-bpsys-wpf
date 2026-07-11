using System.Windows;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.ProductTour.Controls;

namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// Coordinates first-run onboarding entry points.
/// </summary>
public interface IOnboardingCoordinator
{
    /// <summary>Shows first-run welcome when needed.</summary>
    /// <param name="owner">Owner window.</param>
    /// <param name="force">Whether the welcome should be forced.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ShowFirstRunWelcomeAsync(Window owner, bool force = false, CancellationToken cancellationToken = default);

    /// <summary>Restarts the first-run flow.</summary>
    /// <param name="owner">Owner window.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RestartFirstRunFlowAsync(Window owner, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default first-run onboarding coordinator.
/// </summary>
public sealed class OnboardingCoordinator : IOnboardingCoordinator
{
    /// <summary>The standard first-run flow id.</summary>
    public const string FirstRunFlowId = "Flow.FirstRun.StandardBp";

    private readonly ITutorialStateManager _tutorialStateManager;
    private readonly ITutorialRunner _tutorialRunner;
    private readonly ITutorialStateStore _stateStore;
    private readonly ITutorialFlowRegistry _flowRegistry;
    private readonly ITutorialPackageRegistry _packageRegistry;
    private readonly ITutorialLanguageService _languageService;
    private readonly ITutorialTextProvider _textProvider;
    private readonly ITutorialAvatarProvider _avatarProvider;
    private readonly ProductTourOptions _options;
    private readonly ILogger<OnboardingCoordinator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OnboardingCoordinator"/> class.
    /// </summary>
    /// <param name="tutorialStateManager">Tutorial state manager.</param>
    /// <param name="tutorialRunner">Tutorial runner.</param>
    /// <param name="stateStore">State store.</param>
    /// <param name="flowRegistry">Tutorial flow registry.</param>
    /// <param name="packageRegistry">Tutorial package registry.</param>
    /// <param name="languageService">Tutorial language service.</param>
    /// <param name="textProvider">Fixed UI text provider.</param>
    /// <param name="avatarProvider">Tutorial avatar provider.</param>
    /// <param name="options">Product tour display options.</param>
    /// <param name="logger">Logger.</param>
    public OnboardingCoordinator(
        ITutorialStateManager tutorialStateManager,
        ITutorialRunner tutorialRunner,
        ITutorialStateStore stateStore,
        ITutorialFlowRegistry flowRegistry,
        ITutorialPackageRegistry packageRegistry,
        ITutorialLanguageService languageService,
        ITutorialTextProvider textProvider,
        ITutorialAvatarProvider avatarProvider,
        ProductTourOptions options,
        ILogger<OnboardingCoordinator> logger)
    {
        _tutorialStateManager = tutorialStateManager;
        _tutorialRunner = tutorialRunner;
        _stateStore = stateStore;
        _flowRegistry = flowRegistry;
        _packageRegistry = packageRegistry;
        _languageService = languageService;
        _textProvider = textProvider;
        _avatarProvider = avatarProvider;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ShowFirstRunWelcomeAsync(Window owner, bool force = false, CancellationToken cancellationToken = default)
    {
        var state = await _stateStore.LoadAsync(cancellationToken);
        if (!force
            && state.CompletedFlows.TryGetValue(FirstRunFlowId, out var record)
            && record.Version >= (_flowRegistry.GetFlow(FirstRunFlowId)?.Version ?? 1)
            && record.CompletionKind == TutorialCompletionKind.Completed)
        {
            return;
        }

        var host = OverlayHost.GetHostPanel(owner);
        var overlay = new FirstRunWelcomeOverlay(_textProvider, _options, _avatarProvider, _languageService);
        host.Children.Add(overlay);
        overlay.SkipConfirmed += async (_, _) =>
        {
            await MarkFirstRunHandledAsync(cancellationToken);
            host.Children.Remove(overlay);
        };
        overlay.StartRequested += async (_, _) =>
        {
            host.Children.Remove(overlay);
            try
            {
                await _tutorialRunner.RunFlowAsync(owner, FirstRunFlowId, force: true, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to run first-run tutorial flow.");
            }
        };
        await overlay.FadeInAsync();
    }

    /// <inheritdoc />
    public async Task RestartFirstRunFlowAsync(Window owner, CancellationToken cancellationToken = default)
    {
        await _tutorialStateManager.ClearFlowStateAsync(FirstRunFlowId, cancellationToken);
        await ShowFirstRunWelcomeAsync(owner, force: true, cancellationToken);
    }

    private async Task MarkFirstRunHandledAsync(CancellationToken cancellationToken)
    {
        var state = await _stateStore.LoadAsync(cancellationToken);
        var flow = _flowRegistry.GetFlow(FirstRunFlowId);
        state.CompletedFlows[FirstRunFlowId] = new TutorialCompletionRecord
        {
            Version = flow?.Version ?? 1,
            CompletionKind = TutorialCompletionKind.Completed
        };

        if (flow is not null)
        {
            foreach (var packageId in flow.IncludedPackageIds)
            {
                var package = _packageRegistry.GetPackage(packageId);
                state.CompletedPackages[packageId] = new TutorialCompletionRecord
                {
                    Version = package?.Version ?? flow.Version,
                    CompletionKind = TutorialCompletionKind.Completed,
                    SourceFlowId = flow.FlowId
                };
            }
        }

        await _stateStore.SaveAsync(state, cancellationToken);
    }
}
