using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class TalentPageViewModel : ObservableRecipient, IRecipient<NewGameMessage>, IRecipient<HighlightMessage>
    {
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public TalentPageViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        private readonly ISharedDataService _sharedDataService;

        public TalentPageViewModel(ISharedDataService sharedDataService)
        {
            _sharedDataService = sharedDataService;
            IsActive = true;
        }

        private Enums.Trait? _selectedTrait = null;

        public Enums.Trait? SelectedTrait
        {
            get => _selectedTrait;
            set
            {
                _selectedTrait = value;
                _sharedDataService.CurrentGame.HunPlayer.Trait = new(_selectedTrait);
                OnPropertyChanged();
            }
        }

        public Game CurrentGame => _sharedDataService.CurrentGame;

        public void Receive(NewGameMessage message)
        {
            if (message.IsNewGameCreated)
            {
                OnPropertyChanged(nameof(CurrentGame));
            }
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
        private bool _isSurTalentHighlighted = false;
        
        [ObservableProperty] 
        private bool _isHunTalentHighlighted = false;

        public void Receive(HighlightMessage message)
        {
            IsSurTalentHighlighted = message.GameAction == GameAction.PickSurTalent;
            IsHunTalentHighlighted = message.GameAction == GameAction.PickHunTalent;
        }
    }
}
