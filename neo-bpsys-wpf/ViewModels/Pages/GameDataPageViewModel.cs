using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.SmartBpModule;
using Player = neo_bpsys_wpf.Core.Models.Player;

namespace neo_bpsys_wpf.ViewModels.Pages;

/// <summary>
/// 比赛数据页面视图模型，展示选手列表并支持 SmartBP 自动填充比赛数据。
/// </summary>
public partial class GameDataPageViewModel : ViewModelBase
{
#pragma warning disable CS8618
    /// <summary>
    /// 用于设计时预览的无参构造函数。
    /// </summary>
    public GameDataPageViewModel()
#pragma warning restore CS8618
    {
        // Decorative constructor for design-time only.
    }

    private readonly ISharedDataService _sharedDataService;
    private readonly ISmartBpFeatureService _smartBpFeatureService;

    /// <summary>
    /// 初始化比赛数据页面视图模型。
    /// </summary>
    /// <param name="sharedDataService">共享数据服务</param>
    /// <param name="smartBpFeatureService">SmartBP 功能服务</param>
    public GameDataPageViewModel(ISharedDataService sharedDataService, ISmartBpFeatureService smartBpFeatureService)
    {
        _sharedDataService = sharedDataService;
        _smartBpFeatureService = smartBpFeatureService;
        _smartBpFeatureService.ModuleStateChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsSmartBpAutoFillVisible));
            AutoFillGameDataCommand.NotifyCanExecuteChanged();
        };
        _smartBpFeatureService.PostGameRecognitionProgressChanged += (_, e) =>
            System.Windows.Application.Current?.Dispatcher.Invoke(() => ApplyProgress(e.Progress));
        sharedDataService.CurrentGameChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(SurPlayerList));
            OnPropertyChanged(nameof(HunPlayer));
        };
        ApplyProgress(_smartBpFeatureService.CurrentPostGameRecognitionProgress);
    }

    /// <summary>
    /// 获取当前局求生者玩家列表。
    /// </summary>
    public IReadOnlyCollection<Player> SurPlayerList => _sharedDataService.CurrentGame.SurPlayerList;

    /// <summary>
    /// 获取当前局监管者玩家。
    /// </summary>
    public Player HunPlayer => _sharedDataService.CurrentGame.HunPlayer;

    /// <summary>
    /// 是否显示 SmartBP 自动回填入口。
    /// </summary>
    public bool IsSmartBpAutoFillVisible => _smartBpFeatureService.IsModuleLoaded;

    /// <summary>
    /// 获取赛后数据识别是否正在进行。
    /// </summary>
    [ObservableProperty]
    public partial bool IsRecognizingPostGameData { get; set; }

    /// <summary>
    /// 获取赛后数据识别的非线性进度百分比（0~100）。
    /// </summary>
    [ObservableProperty]
    public partial int PostGameRecognitionProgressPercent { get; set; }

    /// <summary>
    /// 获取赛后数据识别的当前阶段提示文本。
    /// </summary>
    [ObservableProperty]
    public partial string PostGameRecognitionStageText { get; set; } = string.Empty;

    [RelayCommand(AllowConcurrentExecutions = false, CanExecute = nameof(CanAutoFillGameData))]
    private async Task AutoFillGameDataAsync()
    {
        IsRecognizingPostGameData = true;
        try
        {
            await _smartBpFeatureService.AutoFillGameDataAsync();
        }
        finally
        {
            IsRecognizingPostGameData = false;
        }
    }

    private bool CanAutoFillGameData() => IsSmartBpAutoFillVisible;

    /// <summary>
    /// 将赛后数据识别进度快照应用到可观察属性，驱动进度条与阶段文本。
    /// 必须在 UI 线程调用。
    /// </summary>
    /// <param name="progress">进度快照。</param>
    private void ApplyProgress(SmartBpPostGameRecognitionProgress progress)
    {
        PostGameRecognitionProgressPercent = progress.Percent;
        PostGameRecognitionStageText = progress.StageText;
    }
}
