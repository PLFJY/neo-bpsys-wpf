
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Messages;
using Game = neo_bpsys_wpf.Core.Models.Game;
using Trait = neo_bpsys_wpf.Core.Models.Trait;

namespace neo_bpsys_wpf.ViewModels.Pages;

/// <summary>
/// 天赋页面视图模型，管理监管者天赋选择和天赋可见性。
/// </summary>
public partial class TalentPageViewModel : ViewModelBase, IRecipient<HighlightMessage>
{
#pragma warning disable CS8618 
    /// <summary>
    /// 用于设计时预览的无参构造函数。
    /// </summary>
    public TalentPageViewModel()
#pragma warning restore CS8618 
    {
        // Decorative constructor for design-time only.
    }

    private readonly ISharedDataService _sharedDataService;

    /// <summary>
    /// 初始化天赋页面视图模型。
    /// </summary>
    /// <param name="sharedDataService">共享数据服务</param>
    /// <param name="settingsHostService">设置宿主服务</param>
    public TalentPageViewModel(ISharedDataService sharedDataService, ISettingsHostService settingsHostService)
    {
        _sharedDataService = sharedDataService;
        sharedDataService.IsTraitVisibleChanged += (_, _) => IsTraitVisible = sharedDataService.IsTraitVisible;
        sharedDataService.CurrentGameChanged += (_, _) =>
        {
            SelectedTrait = null;
            OnPropertyChanged(nameof(CurrentGame));
        };
    }

    private TraitType? _selectedTrait;

    /// <summary>
    /// 获取或设置当前选中的天赋类型。
    /// </summary>
    public TraitType? SelectedTrait
    {
        get => _selectedTrait;
        set => SetPropertyWithAction(ref _selectedTrait, value,
            _ =>
            {
                _sharedDataService.CurrentGame.HunPlayer.Trait = new Trait(_selectedTrait, false);
            });
    }

    /// <summary>
    /// 获取当前比赛数据。
    /// </summary>
    public Game CurrentGame => _sharedDataService.CurrentGame;

    private bool _isTraitVisible = true;

    /// <summary>
    /// 获取或设置天赋是否可见。
    /// </summary>
    public bool IsTraitVisible
    {
        get => _isTraitVisible;
        set => SetPropertyWithAction(ref _isTraitVisible, value, _ =>
        {
            _sharedDataService.IsTraitVisible = _isTraitVisible;
        });
    }

    [ObservableProperty] private bool _isSurTalentHighlighted;

    [ObservableProperty] private bool _isHunTalentHighlighted;

    public void Receive(HighlightMessage message)
    {
        IsSurTalentHighlighted = message.GameAction == GameAction.PickSurTalent;
        IsHunTalentHighlighted = message.GameAction == GameAction.PickHunTalent;
    }
}
