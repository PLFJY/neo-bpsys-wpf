using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.Core.Models;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Messages;
using Game = neo_bpsys_wpf.Core.Models.Game;
using Team = neo_bpsys_wpf.Core.Models.Team;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 共享数据服务, 实现了 <see cref="ISharedDataService"/> 接口，负责全局的共享数据
/// </summary>
public partial class SharedDataService : ISharedDataService
{
    private readonly ILogger<SharedDataService> _logger;

    #region 初始化

    public SharedDataService(ILogger<SharedDataService> logger)
    {
        _logger = logger;
        MainTeam = new Team(Camp.Sur, TeamType.MainTeam);
        AwayTeam = new Team(Camp.Hun, TeamType.AwayTeam);

        _currentGame = new Game(MainTeam, AwayTeam, GameProgress.Free);

        var charaListFilePath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "CharacterList.json");
        ReadCharaListFromFile(charaListFilePath);

        SurCharaList = SurCharaList
            ?.OrderBy(pair => pair.Key)
            .ToDictionary(pair => pair.Key, pair => pair.Value)!;
        HunCharaList = HunCharaList
            ?.OrderBy(pair => pair.Key)
            .ToDictionary(pair => pair.Key, pair => pair.Value)!;

        CanCurrentSurBannedList = [.. Enumerable.Repeat(true, 4)];
        CanCurrentHunBannedList = [.. Enumerable.Repeat(true, 2)];
        CanGlobalSurBannedList = [.. Enumerable.Repeat(false, 9)];
        CanGlobalHunBannedList = [.. Enumerable.Repeat(false, 3)];

        CanCurrentSurBannedList.CollectionChanged += (_, e) =>
            HandleBanCollectionChanged(BanListName.CanCurrentSurBanned, e);
        CanCurrentHunBannedList.CollectionChanged += (_, e) =>
            HandleBanCollectionChanged(BanListName.CanCurrentHunBanned, e);
        CanGlobalSurBannedList.CollectionChanged += (_, e) =>
            HandleBanCollectionChanged(BanListName.CanGlobalSurBanned, e);
        CanGlobalHunBannedList.CollectionChanged += (_, e) =>
            HandleBanCollectionChanged(BanListName.CanGlobalHunBanned, e);


        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += Timer_Tick;
        _logger.LogInformation("SharedDataService initialized");

        CurrentGame.TeamSwapped += OnTeamSwapped;
    }

    /// <summary>
    /// 从文件读取角色数据
    /// </summary>
    /// <param name="charaListFilePath"></param>
    private void ReadCharaListFromFile(string charaListFilePath)
    {
        if (!File.Exists(charaListFilePath))
            return;

        // 加载角色数据
        var characterFileContent = File.ReadAllText(charaListFilePath);

        var characters = JsonSerializer.Deserialize<Dictionary<string, CharacterMini>>(
            characterFileContent,
            new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } }
        );

        if (characters == null)
            return;

        foreach (var i in characters)
        {
            CharacterDict.Add(
                i.Key,
                new Character(i.Key, i.Value.Camp, i.Value.ImageFileName)
            );

            if (i.Value.Camp == Camp.Sur)
                SurCharaList?.Add(i.Key, CharacterDict[i.Key]);
            else
                HunCharaList?.Add(i.Key, CharacterDict[i.Key]);
        }

        _logger.LogInformation("CharacterDict loaded");
    }

    #endregion

    #region 队伍

    /// <summary>
    /// 主队
    /// </summary>
    public Team MainTeam { get; set; }

    /// <summary>
    /// 客队
    /// </summary>
    public Team AwayTeam { get; set; }

    #endregion

    private Game _currentGame;

    /// <summary>
    /// 当前对局
    /// </summary>
    public Game CurrentGame
    {
        get => _currentGame;
        set
        {
            if (_currentGame == value) return;
            CurrentGame.TeamSwapped -= OnTeamSwapped;
            _currentGame = value;
            CurrentGame.TeamSwapped += OnTeamSwapped;
            CurrentGameChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnTeamSwapped(object? sender, EventArgs args) => TeamSwapped?.Invoke(this, EventArgs.Empty);

    #region 角色字典

    /// <summary>
    /// 所有角色
    /// </summary>
    public Dictionary<string, Character> CharacterDict { get; } = [];

    /// <summary>
    /// 求生者角色列表
    /// </summary>
    public Dictionary<string, Character> SurCharaList { get; } = [];

    /// <summary>
    /// 监管者角色列表
    /// </summary>
    public Dictionary<string, Character> HunCharaList { get; } = [];

    #endregion

    #region Ban 位相关

    /// <summary>
    /// 当局禁用(求生者)是否可禁用
    /// </summary>
    public ObservableCollection<bool> CanCurrentSurBannedList { get; set; }

    /// <summary>
    /// 当局禁用(监管者)是否可禁用
    /// </summary>
    public ObservableCollection<bool> CanCurrentHunBannedList { get; set; }

    /// <summary>
    /// 全局禁用(求生者)是否可禁用
    /// </summary>
    public ObservableCollection<bool> CanGlobalSurBannedList { get; set; }

    /// <summary>
    /// 全局禁用(监管者)是否可禁用
    /// </summary>
    public ObservableCollection<bool> CanGlobalHunBannedList { get; set; }

    /// <summary>
    /// 设置Ban位数量，第一个参数传入列表名称<br/>
    /// </summary>
    /// <param name="listName">传入的列表名称</param>
    /// <param name="count">Ban位数量</param>
    /// <exception cref="ArgumentException"></exception>
    public void SetBanCount(BanListName listName, int count)
    {
        switch (listName)
        {
            case BanListName.CanCurrentSurBanned:
                for (var i = 0; i < CanCurrentSurBannedList.Count; i++)
                    CanCurrentSurBannedList[i] = i < count;
                break;
            case BanListName.CanCurrentHunBanned:
                for (var i = 0; i < CanCurrentHunBannedList.Count; i++)
                    CanCurrentHunBannedList[i] = i < count;
                break;
            case BanListName.CanGlobalSurBanned:
                for (var i = 0; i < CanGlobalSurBannedList.Count; i++)
                    CanGlobalSurBannedList[i] = i < count;
                break;
            case BanListName.CanGlobalHunBanned:
                for (var i = 0; i < CanGlobalHunBannedList.Count; i++)
                    CanGlobalHunBannedList[i] = i < count;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(listName), listName, null);
        }

        _logger.LogInformation("{BanListName} set ban count to {Count}", listName, count);
    }

    /// <summary>
    /// 处理Ban位数量改变并触发事件通知
    /// </summary>
    /// <param name="banListName">ban 位列表名称</param>
    /// <param name="e">事件参数</param>
    private void HandleBanCollectionChanged(BanListName banListName, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Replace) return;
        var index = e.NewStartingIndex;
        BanCountChanged?.Invoke(this, new BanCountChangedEventArgs(banListName, index));
    }

    #endregion

    #region 倒计时

    private readonly DispatcherTimer _timer = new();

    private int _remainingSeconds = -1;

    /// <summary>
    /// 倒计时剩余时间
    /// </summary>
    public string RemainingSeconds
    {
        get => _remainingSeconds < 0 ? "VS" : _remainingSeconds.ToString();
        set
        {
            if (!int.TryParse(value, out _remainingSeconds))
                _remainingSeconds = 0;

            CountDownValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_remainingSeconds >= 0)
        {
            _remainingSeconds--;
            CountDownValueChanged?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            _timer.Stop();
        }
    }

    public void TimerStart(int? seconds)
    {
        if (seconds == null) return;
        _remainingSeconds = (int)seconds;
        _timer.Start();
        CountDownValueChanged?.Invoke(this, EventArgs.Empty);
        _logger.LogInformation("Timer started with {Seconds} seconds", seconds);
    }

    public void TimerStop()
    {
        _remainingSeconds = 0;
        _timer.Stop();
        CountDownValueChanged?.Invoke(this, EventArgs.Empty);
        _logger.LogInformation("Timer stopped");
    }

    #endregion

    /// <summary>
    /// 是否显示辅助特质
    /// </summary>
    private bool _isTraitVisible = true;

    public bool IsTraitVisible
    {
        get => _isTraitVisible;
        set
        {
            if (_isTraitVisible == value) return;
            var oldValue = _isTraitVisible;
            _isTraitVisible = value;
            IsTraitVisibleChanged?.Invoke(this, EventArgs.Empty);
            _logger.LogInformation("IsTraitVisible changed to {Value}", value);
        }
    }

    /// <summary>
    /// 是否是BO3模式
    /// </summary>
    private bool _isBo3Mode;

    public bool IsBo3Mode
    {
        get => _isBo3Mode;
        set
        {
            if (_isBo3Mode == value) return;
            var oldValue = _isBo3Mode;
            _isBo3Mode = value;
            WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<bool>(this, nameof(IsBo3Mode), oldValue,
                value));
            IsBo3ModeChanged?.Invoke(this, EventArgs.Empty);
            _logger.LogInformation("IsBo3Mode changed to {Value}", value);
        }
    }

    /// <summary>
    /// 分数统计界面 BO3 和 BO5之间"Total"相差的距离
    /// </summary>
    private double _globalScoreTotalMargin = 390;

    public double GlobalScoreTotalMargin
    {
        get => _globalScoreTotalMargin;
        set
        {
            var oldValue = _globalScoreTotalMargin;
            if (Math.Abs(_globalScoreTotalMargin - value) < 0.01) return;
            _globalScoreTotalMargin = value;

            GlobalScoreTotalMarginChanged?.Invoke(this, EventArgs.Empty);
            _logger.LogInformation("GlobalScoreTotalMargin changed to {Value}", value);
        }
    }

    private bool _isMapV2Breathing;

    /// <summary>
    /// 地图V2呼吸灯是否开启
    /// </summary>
    public bool IsMapV2Breathing
    {
        get => _isMapV2Breathing;
        set
        {
            if (_isMapV2Breathing == value) return;
            _isMapV2Breathing = value;
            IsMapV2BreathingChanged?.Invoke(this, EventArgs.Empty);
            foreach (var mapValue in CurrentGame.MapV2Dictionary.Values)
            {
                mapValue.IsBreathing = value;
            }
        }
    }

    private bool _isMapV2CampVisible;

    /// <summary>
    /// 地图V2阵营是否可见
    /// </summary>
    public bool IsMapV2CampVisible
    {
        get => _isMapV2CampVisible;
        set
        {
            if(_isMapV2CampVisible == value) return;
            _isMapV2CampVisible = value;
            IsMapV2CampVisibleChanged?.Invoke(this, EventArgs.Empty);
            foreach (var mapValue in CurrentGame.MapV2Dictionary.Values)
            {
                mapValue.IsCampVisible = value;
            }
        }
    }

    private class CharacterMini
    {
        public Camp Camp { get; set; }
        public string ImageFileName { get; set; } = string.Empty;
    }

    #region 事件

    /// <summary>
    /// 当前对局改变事件
    /// </summary>
    public event EventHandler? CurrentGameChanged;

    /// <summary>
    /// 分数统计界面 BO3 和 BO5之间"Total"相差的距离改变事件
    /// </summary>
    public event EventHandler? GlobalScoreTotalMarginChanged;

    /// <summary>
    /// Ban位数量改变事件
    /// </summary>
    public event EventHandler<BanCountChangedEventArgs>? BanCountChanged;

    /// <summary>
    /// 辅助特质可见性改变事件
    /// </summary>
    public event EventHandler? IsTraitVisibleChanged;

    /// <summary>
    /// BO3模式改变事件
    /// </summary>
    public event EventHandler? IsBo3ModeChanged;

    /// <summary>
    /// 倒计时剩余秒数改变事件
    /// </summary>
    public event EventHandler? CountDownValueChanged;

    /// <summary>
    /// 队伍换边事件
    /// </summary>
    public event EventHandler? TeamSwapped;

    /// <summary>
    /// 地图V2呼吸灯改变事件
    /// </summary>
    public event EventHandler? IsMapV2BreathingChanged;

    /// <summary>
    /// 地图V2阵营是否可见改变事件
    /// </summary>
    public event EventHandler? IsMapV2CampVisibleChanged;

    #endregion
}