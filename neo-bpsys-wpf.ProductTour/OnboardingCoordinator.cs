using System.Windows;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.ProductTour.Controls;

namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// 协调首次运行新手引导入口点。
/// </summary>
public interface IOnboardingCoordinator
{
    /// <summary>在需要时显示首次运行欢迎界面。</summary>
    /// <param name="owner">所有者窗口。</param>
    /// <param name="force">是否强制显示欢迎界面。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task ShowFirstRunWelcomeAsync(Window owner, bool force = false, CancellationToken cancellationToken = default);

    /// <summary>重新开始首次运行流程。</summary>
    /// <param name="owner">所有者窗口。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task RestartFirstRunFlowAsync(Window owner, CancellationToken cancellationToken = default);
}

/// <summary>
/// 默认的首次运行新手引导协调器。
/// </summary>
public sealed class OnboardingCoordinator : IOnboardingCoordinator
{
    /// <summary>标准首次运行流程 id。</summary>
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
    /// 初始化 <see cref="OnboardingCoordinator"/> 类的新实例。
    /// </summary>
    /// <param name="tutorialStateManager">教程状态管理器。</param>
    /// <param name="tutorialRunner">教程运行器。</param>
    /// <param name="stateStore">状态存储。</param>
    /// <param name="flowRegistry">教程流程注册表。</param>
    /// <param name="packageRegistry">教程包注册表。</param>
    /// <param name="languageService">教程语言服务。</param>
    /// <param name="textProvider">固定 UI 文本提供程序。</param>
    /// <param name="avatarProvider">教程头像提供程序。</param>
    /// <param name="options">产品导览显示选项。</param>
    /// <param name="logger">日志记录器。</param>
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
        var languageOptions = await _languageService.GetLanguageOptionsAsync(cancellationToken);
        var overlay = new FirstRunWelcomeOverlay(_textProvider, _options, _avatarProvider, languageOptions, _languageService);
        host.Children.Add(overlay);
        overlay.SkipConfirmed += async (_, _) =>
        {
            await MarkFirstRunHandledAsync(cancellationToken);
            host.Children.Remove(overlay);
        };
        overlay.StartRequested += async (_, languageOptionId) =>
        {
            host.Children.Remove(overlay);
            try
            {
                await _languageService.ApplyLanguageAsync(languageOptionId, cancellationToken);
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
