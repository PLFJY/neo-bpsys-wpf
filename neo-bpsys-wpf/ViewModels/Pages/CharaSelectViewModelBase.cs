using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;
using System.Windows.Media;
using neo_bpsys_wpf.Enums;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    /// <summary>
    /// 用于选择角色的角色选择器行为的基类
    /// 需要派生类所做的是: <br/>
    /// 1.设置<see cref="CharaList"/><br/>
    /// 2.设置<see cref="IsEnabled"/><br/>
    /// 3.实现<see cref="SyncChara"/>
    /// </summary>
    public abstract partial class CharaSelectViewModelBase :
        ObservableRecipient,
        IRecipient<NewGameMessage>,
        IRecipient<BanCountChangedMessage>,
        IRecipient<HighlightMessage>
    {
        protected readonly ISharedDataService SharedDataService;

        public int Index { get; }

        [ObservableProperty] 
        private Character? _selectedChara;

        [ObservableProperty] 
        private ImageSource? _previewImage;

        private bool _isEnabled = true;

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                SyncIsEnabled();
                OnPropertyChanged();
            }
        }

        [ObservableProperty] 
        private bool _isHighlighted = false;
        
        [ObservableProperty] 
        private bool _isCharaChangerHighlighted = false;

        public Dictionary<string, Character> CharaList { get; set; } = [];

        public abstract void SyncChara();
        protected abstract void SyncIsEnabled();
        protected abstract bool IsActionNameCorrect(GameAction? action);

        [RelayCommand]
        private void Confirm() => SyncChara();

        protected CharaSelectViewModelBase(ISharedDataService sharedDataService, int index = 0)
        {
            IsActive = true;
            SharedDataService = sharedDataService;
            Index = index;
        }

        public void Receive(NewGameMessage message)
        {
            if (!message.IsNewGameCreated) return;
            SelectedChara = null;
            PreviewImage = null;
        }

        public virtual void Receive(BanCountChangedMessage message)
        {
            
        }

        public void Receive(HighlightMessage message)
        {
            if (IsActionNameCorrect(message.GameAction) && message.Index != null && message.Index.Contains(Index))
            {
                IsHighlighted = true;
            }
            else
            {
                IsHighlighted = false;
            }
            
            IsCharaChangerHighlighted = message.GameAction == GameAction.DistributeChara;
        }
    }
}