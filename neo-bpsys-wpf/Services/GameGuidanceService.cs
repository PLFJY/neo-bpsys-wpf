using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Exceptions;
using neo_bpsys_wpf.Views.Pages;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using Wpf.Ui;
using I18nHelper = neo_bpsys_wpf.Helpers.I18nHelper;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 对局引导服务, 实现了 <see cref="IGameGuidanceService"/> 接口，负责对局引导功能
/// </summary>
/// <param name="sharedDataService"></param>
/// <param name="navigationService"></param>
/// <param name="infoBarService"></param>
public class GameGuidanceService(
    ISharedDataService sharedDataService,
    INavigationService navigationService,
    IInfoBarService infoBarService,
    ILogger<GameGuidanceService> logger) : IGameGuidanceService
{
    private readonly ILogger<GameGuidanceService> _logger = logger;
    private readonly ISharedDataService _sharedDataService = sharedDataService;
    private readonly INavigationService _navigationService = navigationService;
    private readonly IInfoBarService _infoBarService = infoBarService;

    private readonly string _guidanceFilePath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GameRule.json");

    private GameProperty? _currentGameProperty = new();

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly Dictionary<GameAction, Type> _actionToPage = new()
    {
        { GameAction.BanMap, typeof(MapBpPage) },
        { GameAction.PickMap, typeof(MapBpPage) },
        { GameAction.BanSur, typeof(BanSurPage) },
        { GameAction.BanHun, typeof(BanHunPage) },
        { GameAction.PickSur, typeof(PickPage) },
        { GameAction.DistributeChara, typeof(PickPage) },
        { GameAction.PickHun, typeof(PickPage) },
        { GameAction.PickSurTalent, typeof(TalentPage) },
        { GameAction.PickHunTalent, typeof(TalentPage) }
    };

    private Dictionary<GameAction, Func<string>> ActionName { get; } = new()
    {
        { GameAction.BanMap, () => I18nHelper.GetLocalizedString("BanMap") },
        { GameAction.PickMap, () => I18nHelper.GetLocalizedString("PickMap") },
        { GameAction.PickCamp, () => I18nHelper.GetLocalizedString("PickCamp") },
        { GameAction.BanSur, () => I18nHelper.GetLocalizedString("BanSurvivor") },
        { GameAction.BanHun, () => I18nHelper.GetLocalizedString("BanHunter") },
        { GameAction.PickSur, () => I18nHelper.GetLocalizedString("PickSurvivor") },
        { GameAction.DistributeChara, () => I18nHelper.GetLocalizedString("DistributeCharacters") },
        { GameAction.PickHun, () => I18nHelper.GetLocalizedString("PickHunter") },
        { GameAction.PickSurTalent, () => I18nHelper.GetLocalizedString("PickSurTalent") },
        { GameAction.PickHunTalent, () => I18nHelper.GetLocalizedString("PickHunTalent") }
    };

    private int _currentStep = -1;

    private bool _isGuidanceStarted;
    private string? _pendingGuidanceStopReason;

    /// <inheritdoc/>
    public event EventHandler<GameGuidanceStateChangedEventArgs>? GuidanceStateChanged;

    /// <inheritdoc/>
    public event EventHandler<GameGuidanceStateChangedEventArgs>? GuidanceStarted;

    /// <inheritdoc/>
    public event EventHandler<GameGuidanceStateChangedEventArgs>? GuidanceStopped;

    /// <inheritdoc/>
    public event EventHandler<GameGuidanceStateChangedEventArgs>? GuidanceCancelled;

    /// <inheritdoc/>
    public event EventHandler<GameGuidanceStepChangedEventArgs>? GuidanceStepChanged;

    /// <inheritdoc/>
    public event EventHandler<GameGuidanceHighlightChangedEventArgs>? GuidanceHighlightChanged;

    /// <inheritdoc/>
    public event EventHandler<GameGuidanceHighlightChangedEventArgs>? GuidanceHighlightCleared;

    /// <inheritdoc/>
    public bool IsGuidanceStarted
    {
        get => _isGuidanceStarted;
        set
        {
            if (_isGuidanceStarted == value) return;
            var oldValue = _isGuidanceStarted;
            _isGuidanceStarted = value;
            WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<bool>(this, nameof(IsGuidanceStarted),
                oldValue, value));
            var args = CreateStateChangedArgs(value, _pendingGuidanceStopReason);
            _pendingGuidanceStopReason = null;
            GuidanceStateChanged?.Invoke(this, args);
            if (value)
            {
                GuidanceStarted?.Invoke(this, args);
            }
            else if (string.Equals(args.Reason, "Cancelled", StringComparison.Ordinal))
            {
                GuidanceCancelled?.Invoke(this, args);
            }
            else
            {
                GuidanceStopped?.Invoke(this, args);
            }
        }
    }

    /// <summary>
    /// 读取对局规则文件
    /// </summary>
    /// <param name="gameProgress"></param>
    /// <returns></returns>
    /// <exception cref="FileNotFoundException"></exception>
    /// <exception cref="GuidanceNotSupportedException"></exception>
    private GameProperty? ReadGamePropertyFromFileAsync(GameProgress gameProgress)
    {
        if (!File.Exists(_guidanceFilePath))
        {
            _logger.LogWarning("Game rule file not found at {Path}", _guidanceFilePath);
            _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("GameRuleFileNotFound"));
            throw new FileNotFoundException();
        }

        var gameRuleFileContent = File.ReadAllText(_guidanceFilePath);
        var content =
            JsonSerializer.Deserialize<Dictionary<GameProgress, GameProperty>>(gameRuleFileContent,
                _jsonSerializerOptions);
        if (content == null || gameProgress == GameProgress.Free)
        {
            _logger.LogWarning("Game guidance not supported for progress {Progress}", gameProgress);
            throw new GuidanceNotSupportedException();
        }

        return content[gameProgress];
    }

    /// <inheritdoc/>
    public async Task<string?> StartGuidance(bool isNavigatePageEnable = true)
    {
        if (TryGetDispatcher(out var dispatcher) && !dispatcher.CheckAccess())
        {
            return await dispatcher.InvokeAsync(() => StartGuidance(isNavigatePageEnable)).Task.Unwrap();
        }

        if (IsGuidanceStarted)
        {
            _infoBarService.ShowWarningInfoBar(I18nHelper.GetLocalizedString("GameAlreadyStarted"));
        }

        try
        {
            _currentGameProperty = ReadGamePropertyFromFileAsync(_sharedDataService.CurrentGame.GameProgress);
        }
        catch (GuidanceNotSupportedException)
        {
            _infoBarService.ShowWarningInfoBar(I18nHelper.GetLocalizedString("GuidanceNotAvailableInFree"));
            return null;
        }
        catch (Exception ex)
        {
            await MessageBoxHelper.ShowErrorAsync($"{I18nHelper.GetLocalizedString("GameRuleFileError")}\n{ex}");
            return null;
        }

        if (_currentGameProperty != null)
        {
            _currentStep = -1;
            _sharedDataService.SetBanCount(BanListName.CanCurrentSurBanned, _currentGameProperty.SurCurrentBan);
            _sharedDataService.SetBanCount(BanListName.CanCurrentHunBanned, _currentGameProperty.HunCurrentBan);
            _sharedDataService.SetBanCount(BanListName.CanGlobalSurBanned, _currentGameProperty.SurGlobalBan);
            _sharedDataService.SetBanCount(BanListName.CanGlobalHunBanned, _currentGameProperty.HunGlobalBan);
            _sharedDataService.CurrentGame.SurTeam.UpdateGlobalBanFromRecord();
            _sharedDataService.CurrentGame.HunTeam.UpdateGlobalBanFromRecord();
            IsGuidanceStarted = true;
            var nextStepResult = await NextStepAsync(isNavigatePageEnable);
            return nextStepResult;
        }

        await MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("GameRuleFileError"));

        return null;
    }

    /// <inheritdoc/>
    public void StopGuidance()
    {
        if (TryGetDispatcher(out var dispatcher) && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(StopGuidance);
            return;
        }

        if (!IsGuidanceStarted)
        {
            _infoBarService.ShowWarningInfoBar(I18nHelper.GetLocalizedString("PleaseStartGameFirst"));
            return;
        }

        _pendingGuidanceStopReason = "Cancelled";
        _infoBarService.CloseInfoBar();
        WeakReferenceMessenger.Default.Send(new HighlightMessage(null, null));
        PublishHighlight(null, null);
        IsGuidanceStarted = false;
    }

    /// <inheritdoc/>
    public void CompleteGuidance(string reason = "SmartBpCharacterBpEnded")
    {
        if (TryGetDispatcher(out var dispatcher) && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => CompleteGuidance(reason));
            return;
        }

        if (!IsGuidanceStarted)
            return;

        _pendingGuidanceStopReason = string.IsNullOrWhiteSpace(reason)
            ? "SmartBpCharacterBpEnded"
            : reason;
        _infoBarService.CloseInfoBar();
        WeakReferenceMessenger.Default.Send(new HighlightMessage(null, null));
        PublishHighlight(null, null);
        IsGuidanceStarted = false;
    }

    private GameGuidanceStateChangedEventArgs CreateStateChangedArgs(bool isStarted, string? reason)
    {
        var currentStepIndex = _currentStep;
        var currentStep = _currentGameProperty is not null
                          && currentStepIndex >= 0
                          && currentStepIndex < _currentGameProperty.WorkFlow.Count
            ? _currentGameProperty.WorkFlow[currentStepIndex]
            : null;

        return new GameGuidanceStateChangedEventArgs(
            isStarted,
            reason ?? (isStarted ? "Started" : "Stopped"),
            currentStep?.Time,
            currentStep is null ? null : currentStepIndex,
            currentStep?.Action,
            currentStep?.Index);
    }

    /// <inheritdoc/>
    public async Task<string?> NextStepAsync(bool isNavigatePageEnable = true)
    {
        if (TryGetDispatcher(out var dispatcher) && !dispatcher.CheckAccess())
        {
            return await dispatcher.InvokeAsync(() => NextStepAsync(isNavigatePageEnable)).Task.Unwrap();
        }

        if (!IsGuidanceStarted)
        {
            _infoBarService.ShowWarningInfoBar(I18nHelper.GetLocalizedString("PleaseStartGameFirst"));
            return null;
        }

        if (_currentGameProperty != null)
        {
            if (_currentStep + 1 < _currentGameProperty.WorkFlow.Count)
            {
                var result = await HandleStepChange(_currentStep + 1, isNavigatePageEnable);
                return result.Error ?? result.ActionName;
            }

            _infoBarService.ShowWarningInfoBar(I18nHelper.GetLocalizedString("AlreadyLastStep"));
            WeakReferenceMessenger.Default.Send(new HighlightMessage(GameAction.EndGuidance, null));
            PublishHighlight(GameAction.EndGuidance, null);
        }
        else
        {
            await MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("GameInfoError"));
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<string?> PrevStepAsync(bool isNavigatePageEnable = true)
    {
        if (TryGetDispatcher(out var dispatcher) && !dispatcher.CheckAccess())
        {
            return await dispatcher.InvokeAsync(() => PrevStepAsync(isNavigatePageEnable)).Task.Unwrap();
        }

        if (!IsGuidanceStarted)
        {
            _infoBarService.ShowWarningInfoBar(I18nHelper.GetLocalizedString("PleaseStartGameFirst"));
            return null;
        }

        if (_currentGameProperty != null)
        {
            if (_currentStep > 0)
            {
                var result = await HandleStepChange(_currentStep - 1, isNavigatePageEnable);
                return result.Error ?? result.ActionName;
            }

            _infoBarService.ShowWarningInfoBar(I18nHelper.GetLocalizedString("AlreadyFirstStep"));
        }
        else
        {
            await MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("GameInfoError"));
        }

        return null;
    }

    /// <inheritdoc />
    public GameGuidanceRuntimeSnapshot GetRuntimeSnapshot()
    {
        IReadOnlyList<GameGuidanceStepSnapshot> workflow = _currentGameProperty?.WorkFlow.Select((step, index) =>
            new GameGuidanceStepSnapshot(index, step.Action,
                Array.AsReadOnly(step.Index?.ToArray() ?? []), step.Time)).ToList().AsReadOnly() ?? [];
        var current = _currentStep >= 0 && _currentStep < workflow.Count ? workflow[_currentStep] : null;
        return new(IsGuidanceStarted, _currentStep, current?.Action,
            current?.Indexes ?? Array.AsReadOnly<int>([]), current?.Time, workflow);
    }

    /// <inheritdoc />
    public async Task<string?> MoveToStepAsync(int stepIndex, bool isNavigatePageEnable = true)
    {
        if (TryGetDispatcher(out var dispatcher) && !dispatcher.CheckAccess())
        {
            _logger.LogInformation(
                "GameGuidance MoveToStep requested: targetStep={TargetStep}, requestedAction={RequestedAction}, callerThread={CallerThread}, dispatcherAccess={DispatcherAccess}",
                stepIndex,
                GetActionForStep(stepIndex),
                Environment.CurrentManagedThreadId,
                false);
            return await dispatcher.InvokeAsync(() => MoveToStepAsync(stepIndex, isNavigatePageEnable)).Task.Unwrap();
        }

        _logger.LogInformation(
            "GameGuidance MoveToStep requested: targetStep={TargetStep}, requestedAction={RequestedAction}, callerThread={CallerThread}, dispatcherAccess={DispatcherAccess}",
            stepIndex,
            GetActionForStep(stepIndex),
            Environment.CurrentManagedThreadId,
            GetDispatcherAccess());

        if (!IsGuidanceStarted)
        {
            _infoBarService.ShowWarningInfoBar(I18nHelper.GetLocalizedString("PleaseStartGameFirst"));
            return null;
        }
        if (_currentGameProperty == null || stepIndex < 0 || stepIndex >= _currentGameProperty.WorkFlow.Count)
            return I18nHelper.GetLocalizedString("GameInfoError");
        var result = await HandleStepChange(stepIndex, isNavigatePageEnable);
        return result.Error;
    }

    private async Task<StepChangeResult> HandleStepChange(int newStepIndex, bool isNavigatePageEnable = true)
    {
        if (TryGetDispatcher(out var dispatcher) && !dispatcher.CheckAccess())
        {
            return await dispatcher.InvokeAsync(() => HandleStepChange(newStepIndex, isNavigatePageEnable)).Task.Unwrap();
        }

        if (_currentGameProperty == null) return new(null, "N/A");
        var previousStepIndex = _currentStep;
        var previousStep = previousStepIndex >= 0 && previousStepIndex < _currentGameProperty.WorkFlow.Count
            ? _currentGameProperty.WorkFlow[previousStepIndex]
            : null;
        var thisStep = _currentGameProperty.WorkFlow[newStepIndex];

        _logger.LogInformation(
            "GameGuidance HandleStepChange begin: previousStep={PreviousStep}, previousAction={PreviousAction}, currentStep={CurrentStep}, currentAction={CurrentAction}, dispatcherAccess={DispatcherAccess}",
            previousStep is null ? null : previousStepIndex,
            previousStep?.Action,
            newStepIndex,
            thisStep.Action,
            GetDispatcherAccess());

        var stepChangedPublished = false;
        try
        {
            _currentStep = newStepIndex;

            //切换页面
            if (thisStep.Action != GameAction.PickCamp && isNavigatePageEnable)
                _navigationService.Navigate(_actionToPage[thisStep.Action]);
            //设置计时器
            _sharedDataService.TimerStart(thisStep.Time);
            //等待待选框动画就位
            await Task.Delay(250);

            var actionName = ActionName[thisStep.Action].Invoke();
            var args = new GameGuidanceStepChangedEventArgs(
                stepIndex: _currentStep,
                action: thisStep.Action,
                index: thisStep.Index,
                time: thisStep.Time,
                previousStepIndex: previousStep is null ? null : previousStepIndex,
                previousAction: previousStep?.Action,
                previousIndex: previousStep?.Index,
                previousTime: previousStep?.Time);

            //触发步骤变化事件
            GuidanceStepChanged?.Invoke(this, args);
            stepChangedPublished = true;
            _logger.LogInformation(
                "GameGuidance GuidanceStepChanged published: step={Step}, action={Action}, indexes={Indexes}",
                args.StepIndex,
                args.Action,
                args.IndexesText);

            //触发高亮变化事件
            PublishHighlight(thisStep.Action, thisStep.Index);
            _logger.LogInformation(
                "GameGuidance GuidanceHighlightChanged published: action={Action}, indexes={Indexes}",
                thisStep.Action,
                FormatIndexes(thisStep.Index));

            //广播高亮消息
            WeakReferenceMessenger.Default.Send(new HighlightMessage(thisStep.Action, thisStep.Index));
            _logger.LogInformation(
                "GameGuidance HighlightMessage sent: action={Action}, indexes={Indexes}",
                thisStep.Action,
                FormatIndexes(thisStep.Index));

            return new(actionName, null);
        }
        catch (Exception ex)
        {
            if (!stepChangedPublished)
            {
                _currentStep = previousStepIndex;
            }

            _logger.LogError(
                ex,
                "GameGuidance HandleStepChange failed: exception={Exception}",
                ex.Message);
            return new(null, ex.Message);
        }
    }

    private void PublishHighlight(GameAction? action, List<int>? indexes)
    {
        var args = new GameGuidanceHighlightChangedEventArgs(action, indexes);
        GuidanceHighlightChanged?.Invoke(this, args);
        if (action is null)
        {
            GuidanceHighlightCleared?.Invoke(this, args);
        }
    }

    private static bool TryGetDispatcher(out System.Windows.Threading.Dispatcher dispatcher)
    {
        dispatcher = Application.Current?.Dispatcher!;
        return dispatcher is not null;
    }

    private static bool GetDispatcherAccess()
        => TryGetDispatcher(out var dispatcher) && dispatcher.CheckAccess();

    private GameAction? GetActionForStep(int stepIndex)
    {
        if (_currentGameProperty is null || stepIndex < 0 || stepIndex >= _currentGameProperty.WorkFlow.Count)
        {
            return null;
        }

        return _currentGameProperty.WorkFlow[stepIndex].Action;
    }

    private static string FormatIndexes(IEnumerable<int>? indexes)
        => indexes is null ? "[]" : $"[{string.Join(", ", indexes)}]";

    private readonly record struct StepChangeResult(string? ActionName, string? Error);

    /// <summary>
    /// 对局属性（包含各阶段禁选数量与工作流）。
    /// </summary>
    public class GameProperty
    {
        /// <summary>
        /// 求生者当局禁用数量。
        /// </summary>
        public int SurCurrentBan { get; set; } = 4;
        /// <summary>
        /// 监管者当局禁用数量。
        /// </summary>
        public int HunCurrentBan { get; set; } = 2;
        /// <summary>
        /// 求生者全局禁用数量。
        /// </summary>
        public int SurGlobalBan { get; set; } = 9;
        /// <summary>
        /// 监管者全局禁用数量。
        /// </summary>
        public int HunGlobalBan { get; set; } = 3;
        /// <summary>
        /// 对局工作流步骤列表。
        /// </summary>
        public List<Step> WorkFlow { get; set; } = [];
    }

    /// <summary>
    /// 对局引导工作流步骤。
    /// </summary>
    public class Step
    {
        /// <summary>
        /// 步骤对应的游戏动作。
        /// </summary>
        public GameAction Action { get; set; }
        /// <summary>
        /// 步骤关联的索引列表。
        /// </summary>
        public List<int> Index { get; set; } = [];
        /// <summary>
        /// 步骤时限（秒），可为 <see langword="null"/>。
        /// </summary>
        public int? Time { get; set; }
    }
}
