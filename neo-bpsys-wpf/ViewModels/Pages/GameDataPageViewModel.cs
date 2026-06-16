using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
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
        sharedDataService.CurrentGameChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(SurPlayerList));
            OnPropertyChanged(nameof(HunPlayer));
        };
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

    [RelayCommand(AllowConcurrentExecutions = false, CanExecute = nameof(CanAutoFillGameData))]
    private async Task AutoFillGameDataAsync()
    {
        await _smartBpFeatureService.AutoFillGameDataAsync();
    }

    private bool CanAutoFillGameData() => IsSmartBpAutoFillVisible;
}
