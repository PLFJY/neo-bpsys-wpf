using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Core.Models;
using System.Windows.Media;

namespace neo_bpsys_wpf.ViewModels;

/// <summary>
/// 用于选择角色的角色选择器行为的基类
/// 需要派生类所做的是: <br/>
/// 1.实现 <see cref="CharaDict"/> 更新的行为<br/>
/// 2.设置 <see cref="IsEnabled"/> 同步 <see cref="ISharedDataService"/> 的哪一个 CanCurrentBannedList 的值，通常需要搭配订阅 Ban 位数量变动的事件<br/>
/// 3.实现 <see cref="SyncCharaToSourceAsync"/> 将角色同步到前台的行为
/// 4.实现 <see cref="SyncIsEnabled"/> 通过toggle button设置后同步状态到对应的 <see cref="ISharedDataService"/> 中 CanCurrentBannedList 的值的行为
/// 5.实现 <see cref="IsActionNameCorrect"/> 判断当前步骤引导的步骤是否符合当前控件的行为
/// 6.在 <see cref="OnCurrentGameChanged"/> 中更新 preview 的 image
/// </summary>
public abstract partial class CharaSelectViewModelBase :
    ViewModelBase,
    IRecipient<HighlightMessage>,
    IRecipient<CharacterDictChangedMessage>
{
    #region Fields

    protected readonly ISharedDataService SharedDataService;
    private readonly Camp _camp;

    private bool _isEnabled = true;

    #endregion

    #region Properties

    /// <summary>
    /// 当前的序号
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// 当前选中的角色
    /// </summary>
    [ObservableProperty] private Character? _selectedChara;

    /// <summary>
    /// 预览图片
    /// </summary>
    [ObservableProperty] private ImageSource? _previewImage;

    /// <summary>
    /// 当前选择器是否可用
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetPropertyWithAction(ref _isEnabled, value, _ => SyncIsEnabled());
    }

    /// <summary>
    /// 当前选择器是否高亮
    /// </summary>
    [ObservableProperty] private bool _isHighlighted;

    /// <summary>
    /// 对应的互换器是否高亮
    /// </summary>
    [ObservableProperty] private bool _isCharaChangerHighlighted;

    /// <summary>
    /// 角色列表
    /// </summary>
    [ObservableProperty] private SortedDictionary<string, Character> _charaDict = [];

    #endregion

    #region Constructors

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="sharedDataService">共享数据服务</param>
    /// <param name="camp">当前控件的阵营</param>
    /// <param name="index">当前序号</param>
    protected CharaSelectViewModelBase(ISharedDataService sharedDataService, Camp camp, int index = 0)
    {
        SharedDataService = sharedDataService;
        _camp = camp;
        SetCharaDict();
        Index = index;
        SharedDataService.CurrentGameChanged += OnCurrentGameChanged;
    }

    #endregion

    #region Commands

    /// <summary>
    /// 确认命令
    /// </summary>
    [RelayCommand]
    private async Task ConfirmAsync() => await SyncCharaToSourceAsync();

    #endregion

    #region Message Handlers

    /// <summary>
    /// 接收高亮消息
    /// </summary>
    /// <param name="message">消息</param>
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

    /// <summary>
    /// 接收角色字典更换的消息
    /// </summary>
    /// <param name="message">消息</param>
    public void Receive(CharacterDictChangedMessage message) => SetCharaDict();

    #endregion

    #region Event Handlers

    /// <summary>
    /// 新对局事件
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private void OnCurrentGameChanged(object? sender, EventArgs args) => SyncCharaFromSourceAsync();

    #endregion

    #region Private Methods

    private void SetCharaDict()
    {
        CharaDict = _camp == Camp.Sur ? SharedDataService.SurCharaDict : SharedDataService.HunCharaDict;
    }

    #endregion

    #region Abstract Methods

    /// <summary>
    /// 同步当前角色到源
    /// </summary>
    /// <returns></returns>
    protected abstract Task SyncCharaToSourceAsync();

    /// <summary>
    /// 从源同步当前角色
    /// </summary>
    /// <returns></returns>
    protected abstract void SyncCharaFromSourceAsync();

    /// <summary>
    /// 同步当前角色选择器是否启用状态
    /// </summary>
    protected abstract void SyncIsEnabled();

    /// <summary>
    /// 判断当前高亮步骤是否符合当前控件
    /// </summary>
    /// <param name="action">当前步骤</param>
    /// <returns>是否符合</returns>
    protected abstract bool IsActionNameCorrect(GameAction? action);

    #endregion
}