using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace neo_bpsys_wpf.ViewModels;

/// <summary>
/// 用于选择角色的角色选择器行为的基类
/// 需要派生类所做的是: <br/>
/// 1.实现 <see cref="CharaDict"/> 更新的行为<br/>
/// 2.设置 <see cref="IsEnabled"/> 同步 <see cref="ISharedDataService"/> 的哪一个 CanCurrentBannedList 的值，通常需要搭配订阅 Ban 位数量变动的事件<br/>
/// 3.实现 <see cref="SyncCharaToSourceAsync"/> 将角色同步到前台的行为，建议使用 <see cref="ICharacterSelectionService"/> 中的API，也可以选择手动实现
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

    /// <summary>
    /// 正在监听的 Game 实例，用于取消旧事件订阅
    /// </summary>
    private Game? _subscribedGame;

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

    /// <summary>
    /// 已被禁用或选中的角色名称集合，用于在 UI 中禁用对应的下拉选项
    /// </summary>
    [ObservableProperty]
    [property: JsonIgnore]
    [property: FrontedBindingIgnore]
    private ISet<string> _disabledKeys = new HashSet<string>();

    #endregion

    #region Constructors

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="sharedDataService">共享数据服务</param>
    /// <param name="camp">当前控件的阵营</param>
    /// <param name="index">当前序号</param>
    protected CharaSelectViewModelBase(
        ISharedDataService sharedDataService,
        Camp camp,
        int index = 0)
    {
        SharedDataService = sharedDataService;
        _camp = camp;
        SetCharaDict();
        Index = index;
        SharedDataService.CurrentGameChanged += OnCurrentGameChanged;
        SharedDataService.TeamSwapped += OnTeamSwapped;

        // 当前生效的全局禁选列表是权威源；暂存 RecordList 不参与禁用状态计算。
        if (_camp == Camp.Sur)
        {
            SharedDataService.CanCurrentSurBannedList.CollectionChanged += OnBannedOrPickChanged;
            SharedDataService.HomeTeam.GlobalBannedSurList.CollectionChanged += OnBannedOrPickChanged;
            SharedDataService.AwayTeam.GlobalBannedSurList.CollectionChanged += OnBannedOrPickChanged;
            SharedDataService.CanGlobalSurBannedList.CollectionChanged += OnBannedOrPickChanged;
        }
        else
        {
            SharedDataService.CanCurrentHunBannedList.CollectionChanged += OnBannedOrPickChanged;
            SharedDataService.HomeTeam.GlobalBannedHunList.CollectionChanged += OnBannedOrPickChanged;
            SharedDataService.AwayTeam.GlobalBannedHunList.CollectionChanged += OnBannedOrPickChanged;
            SharedDataService.CanGlobalHunBannedList.CollectionChanged += OnBannedOrPickChanged;
        }

        SyncCharaFromSourceAsync();
        SubscribeCurrentGameEvents();
        UpdateDisabledKeys();
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
    private void OnCurrentGameChanged(object? sender, EventArgs args)
    {
        SubscribeCurrentGameEvents();
        UpdateDisabledKeys();
        SyncCharaFromSourceAsync();
    }

    private void OnBannedOrPickChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateDisabledKeys();

    private void OnPlayerCharacterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Player.Character))
            UpdateDisabledKeys();
    }

    private void OnTeamSwapped(object? sender, EventArgs e)
    {
        UpdateDisabledKeys();
    }

    #endregion

    #region Private Methods

    private void SetCharaDict()
    {
        CharaDict = _camp == Camp.Sur ? SharedDataService.SurCharaDict : SharedDataService.HunCharaDict;
    }

    /// <summary>
    /// 订阅当前 Game 级别的 Ban/Pick 变化事件，以便实时更新禁用列表
    /// </summary>
    private void SubscribeCurrentGameEvents()
    {
        var game = SharedDataService.CurrentGame;
        if (_subscribedGame == game)
            return;

        UnsubscribeCurrentGameEvents();
        _subscribedGame = game;

        if (_camp == Camp.Sur)
        {
            game.CurrentSurBannedList.CollectionChanged += OnBannedOrPickChanged;
            foreach (var p in game.SurPlayerList)
                p.PropertyChanged += OnPlayerCharacterChanged;
        }
        else
        {
            game.CurrentHunBannedList.CollectionChanged += OnBannedOrPickChanged;
            game.HunPlayer.PropertyChanged += OnPlayerCharacterChanged;
        }
    }

    /// <summary>
    /// 取消对旧 Game 的事件订阅
    /// </summary>
    private void UnsubscribeCurrentGameEvents()
    {
        if (_subscribedGame == null)
            return;

        if (_camp == Camp.Sur)
        {
            _subscribedGame.CurrentSurBannedList.CollectionChanged -= OnBannedOrPickChanged;
            foreach (var p in _subscribedGame.SurPlayerList)
                p.PropertyChanged -= OnPlayerCharacterChanged;
        }
        else
        {
            _subscribedGame.CurrentHunBannedList.CollectionChanged -= OnBannedOrPickChanged;
            _subscribedGame.HunPlayer.PropertyChanged -= OnPlayerCharacterChanged;
        }

        _subscribedGame = null;
    }

    /// <summary>
    /// 从当前 Game 的 Ban/Pick 列表中计算出应禁用的角色名集合
    /// 全局 Ban 以当前生效的 GlobalBannedList 为权威源，暂存 RecordList 不参与计算
    /// 同时忽略未启用的 Ban 位（CanXxxBannedList[i] == false）
    /// </summary>
    private void UpdateDisabledKeys()
    {
        var game = SharedDataService.CurrentGame;
        var result = new HashSet<string>();

        if (_camp == Camp.Sur)
        {
            // 当前局求生者 Ban 位：仅计入已启用的 Ban 位，忽略默认空角色
            var currentBanCount = Math.Min(game.CurrentSurBannedList.Count, SharedDataService.CanCurrentSurBannedList.Count);
            for (var i = 0; i < currentBanCount; i++)
            {
                if (!SharedDataService.CanCurrentSurBannedList[i]) continue;
                AddNameIfValid(result, game.CurrentSurBannedList[i]);
            }
            // 全局禁选只读取当前求生者队伍正在生效的列表
            // 加 Camp 校验防止 Swap 中间态（Camp 已换但引用未换时读到旧阵营记录）
            if (game.SurTeam.Camp == Camp.Sur)
            {
                var globalBanCount = Math.Min(game.SurTeam.GlobalBannedSurList.Count, SharedDataService.CanGlobalSurBannedList.Count);
                for (var i = 0; i < globalBanCount; i++)
                {
                    if (!SharedDataService.CanGlobalSurBannedList[i]) continue;
                    AddNameIfValid(result, game.SurTeam.GlobalBannedSurList[i]);
                }
            }
            // 已 Pick 的求生者
            foreach (var p in game.SurPlayerList)
                AddNameIfValid(result, p.Character);
        }
        else
        {
            // 当前局监管者 Ban 位：仅计入已启用的 Ban 位
            var currentBanCount = Math.Min(game.CurrentHunBannedList.Count, SharedDataService.CanCurrentHunBannedList.Count);
            for (var i = 0; i < currentBanCount; i++)
            {
                if (!SharedDataService.CanCurrentHunBannedList[i]) continue;
                AddNameIfValid(result, game.CurrentHunBannedList[i]);
            }
            // 全局禁选只读取当前监管者队伍正在生效的列表
            if (game.HunTeam.Camp == Camp.Hun)
            {
                var globalBanCount = Math.Min(game.HunTeam.GlobalBannedHunList.Count, SharedDataService.CanGlobalHunBannedList.Count);
                for (var i = 0; i < globalBanCount; i++)
                {
                    if (!SharedDataService.CanGlobalHunBannedList[i]) continue;
                    AddNameIfValid(result, game.HunTeam.GlobalBannedHunList[i]);
                }
            }
            // 已 Pick 的监管者
            AddNameIfValid(result, game.HunPlayer.Character);
        }

        DisabledKeys = result;
    }

    /// <summary>
    /// 如果角色的 Name 非空，则添加到结果集中
    /// </summary>
    private static void AddNameIfValid(ISet<string> result, Character? character)
    {
        var name = character?.Name;
        if (!string.IsNullOrEmpty(name))
            result.Add(name);
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
