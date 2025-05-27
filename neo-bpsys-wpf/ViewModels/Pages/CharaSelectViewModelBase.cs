using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;
using System.Windows.Media;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    /// <summary>
    /// 用于选择角色的角色选择器行为的基类
    /// 需要派生类所做的是: <br/>
    /// 1.设置<see cref="CharaList"/><br/>
    /// 2.设置<see cref="IsEnabled"/><br/>
    /// 3.实现<see cref="SyncChara"/>
    /// </summary>
    public abstract partial class CharaSelectViewModelBase : ObservableRecipient, IRecipient<NewGameMessage>
    {
        protected readonly ISharedDataService _sharedDataService;

        private readonly int _index;
        public int Index { get => _index; }

        [ObservableProperty]
        private Character? _selectedChara;

        [ObservableProperty]
        private ImageSource? _previewImage;

        [ObservableProperty]
        private bool _isEnabled = true;

        public Dictionary<string, Character> CharaList { get; set; } = [];
        public abstract void SyncChara();

        [RelayCommand]
        private void Confirm()
        {
            SyncChara();
            OnPropertyChanged();
        }

        protected CharaSelectViewModelBase(ISharedDataService sharedDataService, int index = 0)
        {
            IsActive = true;
            _sharedDataService = sharedDataService;
            _index = index;
        }

        public void Receive(NewGameMessage message)
        {
            if (message.IsNewGameCreated)
            {
                SelectedChara = null;
                PreviewImage = null;
            }
        }
    }
}
