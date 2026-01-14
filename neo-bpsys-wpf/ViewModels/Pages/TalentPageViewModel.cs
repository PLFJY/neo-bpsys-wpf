
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Messages;
using Game = neo_bpsys_wpf.Core.Models.Game;
using Trait = neo_bpsys_wpf.Core.Models.Trait;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class TalentPageViewModel : ViewModelBase, IRecipient<HighlightMessage>
{
#pragma warning disable CS8618 
    public TalentPageViewModel()
#pragma warning restore CS8618 
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    private readonly ISharedDataService _sharedDataService;
    private readonly ISettingsHostService _settingsHostService;

    public TalentPageViewModel(ISharedDataService sharedDataService, ISettingsHostService settingsHostService)
    {
        _sharedDataService = sharedDataService;
        _settingsHostService = settingsHostService;
        sharedDataService.IsTraitVisibleChanged += (_, _) => IsTraitVisible = sharedDataService.IsTraitVisible;
        sharedDataService.CurrentGameChanged += (_, _) =>
        {
            SelectedTrait = null;
            OnPropertyChanged(nameof(CurrentGame));
        };
    }

    private TraitType? _selectedTrait;

    public TraitType? SelectedTrait
    {
        get => _selectedTrait;
        set => SetPropertyWithAction(ref _selectedTrait, value,
            _ =>
            {
                _sharedDataService.CurrentGame.HunPlayer.Trait = new Trait(_selectedTrait,
                    _settingsHostService.Settings.CutSceneWindowSettings.IsBlackTalentAndTraitEnable);
            });
    }

    public Game CurrentGame => _sharedDataService.CurrentGame;

    private bool _isTraitVisible = true;

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