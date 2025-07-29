using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.Core.Abstractions.ViewModels;

/// <summary>
/// 用于选择角色的角色选择器行为的基类
/// 需要派生类所做的是: <br/>
/// 1.设置<see cref="CharaList"/><br/>
/// 2.设置<see cref="IsEnabled"/><br/>
/// 3.实现<see cref="SyncCharaAsync"/>
/// </summary>
public abstract partial class CharaSelectViewModelBase(ISharedDataService sharedDataService, int index = 0) :
    ViewModelBase,
    IRecipient<NewGameMessage>,
    IRecipient<BanCountChangedMessage>,
    IRecipient<HighlightMessage>
{
    protected readonly ISharedDataService SharedDataService = sharedDataService;

    public int Index { get; } = index;

    [ObservableProperty] private Character? _selectedChara;

    [ObservableProperty] private ImageSource? _previewImage;

    private bool _isEnabled = true;

    public bool IsEnabled
    {
        get => _isEnabled;
        set =>
            SetPropertyWithAction(ref _isEnabled, value, _ =>
            {
                SyncIsEnabled();
                OnPropertyChanged();
            });
    }

    [ObservableProperty] private bool _isHighlighted;

    [ObservableProperty] private bool _isCharaChangerHighlighted;

    public Dictionary<string, Character> CharaList { get; set; } = [];

    public abstract Task SyncCharaAsync();
    protected abstract void SyncIsEnabled();
    protected abstract bool IsActionNameCorrect(GameAction? action);

    [RelayCommand]
    private async Task ConfirmAsync() => await SyncCharaAsync();

    public virtual void Receive(NewGameMessage message)
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