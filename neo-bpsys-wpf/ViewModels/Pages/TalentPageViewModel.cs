using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Abstractions.ViewModels;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Messages;
using Game = neo_bpsys_wpf.Core.Models.Game;
using Trait = neo_bpsys_wpf.Core.Models.Trait;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class TalentPageViewModel : ViewModelBase, IRecipient<NewGameMessage>, IRecipient<HighlightMessage>
{
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    public TalentPageViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    private readonly ISharedDataService _sharedDataService;
    private readonly ISettingsHostService _settingsHostService;

    public TalentPageViewModel(ISharedDataService sharedDataService, ISettingsHostService settingsHostService)
    {
        _sharedDataService = sharedDataService;
        _settingsHostService = settingsHostService;
    }

    private Core.Enums.Trait? _selectedTrait;

    public Core.Enums.Trait? SelectedTrait
    {
        get => _selectedTrait;
        set
        {
            SetProperty(ref _selectedTrait, value);
            _sharedDataService.CurrentGame.HunPlayer.Trait = new Trait(_selectedTrait, _settingsHostService.Settings.CutSceneWindowSettings.IsBlackTalentAndTraitEnable);
        }
    }

    public Game CurrentGame => _sharedDataService.CurrentGame;

    public void Receive(NewGameMessage message)
    {
        if (!message.IsNewGameCreated) return;
        OnPropertyChanged(nameof(CurrentGame));
        SelectedTrait = null;
    }

    private bool _isTraitVisible = true;

    public bool IsTraitVisible
    {
        get => _isTraitVisible;
        set
        {
            _isTraitVisible = value;
            _sharedDataService.IsTraitVisible = _isTraitVisible;
            OnPropertyChanged();
        }
    }

    [ObservableProperty]
    private bool _isSurTalentHighlighted;

    [ObservableProperty]
    private bool _isHunTalentHighlighted;

    public void Receive(HighlightMessage message)
    {
        IsSurTalentHighlighted = message.GameAction == GameAction.PickSurTalent;
        IsHunTalentHighlighted = message.GameAction == GameAction.PickHunTalent;
    }
}