using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Core.Models;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Threading;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Helpers;
using Game = neo_bpsys_wpf.Core.Models.Game;
using Team = neo_bpsys_wpf.Core.Models.Team;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 共享数据服务, 实现了 <see cref="ISharedDataService"/> 接口，负责全局的共享数据
/// </summary>
public partial class SharedDataService : ISharedDataService
{
    private readonly ISettingsHostService _settingsHostService;
    private readonly ILogger<SharedDataService> _logger;

    #region 初始化

    public SharedDataService(ISettingsHostService settingsHostService, ILogger<SharedDataService> logger)
    {
        _settingsHostService = settingsHostService;
        _logger = logger;

        MainTeam = new Team(Camp.Sur, TeamType.HomeTeam);
        AwayTeam = new Team(Camp.Hun, TeamType.AwayTeam);

        _currentGame = new Game(MainTeam, AwayTeam, GameProgress.Free);

        var comparer = StringComparer.Create(_settingsHostService.Settings.CultureInfo, false);
        SurCharaDict = new SortedDictionary<string, Character>(comparer);
        HunCharaDict = new SortedDictionary<string, Character>(comparer);
        ReadCharaListFromFile(_settingsHostService.Settings.CultureInfo);

        CanCurrentSurBannedList = [.. Enumerable.Repeat(true, AppConstants.CurrentBanSurCount)];
        CanCurrentHunBannedList = [.. Enumerable.Repeat(true, AppConstants.CurrentBanHunCount)];
        CanGlobalSurBannedList = [.. Enumerable.Repeat(false, AppConstants.GlobalBanSurCount)];
        CanGlobalHunBannedList = [.. Enumerable.Repeat(false, AppConstants.GlobalBanHunCount)];

        CanCurrentSurBannedList.CollectionChanged += (_, e) =>
            HandleBanCollectionChanged(BanListName.CanCurrentSurBanned, e);
        CanCurrentHunBannedList.CollectionChanged += (_, e) =>
            HandleBanCollectionChanged(BanListName.CanCurrentHunBanned, e);
        CanGlobalSurBannedList.CollectionChanged += (_, e) =>
            HandleBanCollectionChanged(BanListName.CanGlobalSurBanned, e);
        CanGlobalHunBannedList.CollectionChanged += (_, e) =>
            HandleBanCollectionChanged(BanListName.CanGlobalHunBanned, e);

        GlobalScoreTotalMargin = _settingsHostService.Settings.ScoreWindowSettings.GlobalScoreTotalMargin;


        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += Timer_Tick;
        _logger.LogInformation("SharedDataService initialized");

        CurrentGame.TeamSwapped += OnTeamSwapped;
        _settingsHostService.LanguageSettingChanged += (sender, args) => ReadCharaListFromFile(args.CultureInfo);
    }

    private void ReadCharaListFromFile(CultureInfo cultureInfo)
    {
        var filePaths = new[]
        {
            Path.Combine(AppConstants.ResourcesPath, "data", $"CharacterList-{cultureInfo.Name}.json"),
            Path.Combine(AppConstants.ResourcesPath, "data", "CharacterList.json")
        };

        var charaListFilePath = filePaths.FirstOrDefault(File.Exists);
        if (charaListFilePath == null)
            return;

        // 加载角色数据
        var characterFileContent = File.ReadAllText(charaListFilePath);

        JsonSerializerOptions options = new() { Converters = { new JsonStringEnumConverter() } };
        var characters = JsonSerializer.Deserialize<Dictionary<string, CharacterMini>>(
            characterFileContent,
            options
        );

        if (characters == null)
            return;

        var comparer = StringComparer.Create(cultureInfo, false);

        if (SurCharaDict.Count > 0) SurCharaDict = new SortedDictionary<string, Character>(comparer);
        if (HunCharaDict.Count > 0) HunCharaDict = new SortedDictionary<string, Character>(comparer);

        foreach (var i in characters)
        {
            var chara = new Character(i.Key, i.Value.Camp, i.Value.ImageFileName, i.Value.Abbrev);
            if (i.Value.Camp == Camp.Sur)
                SurCharaDict?.Add(i.Key, chara);
            else
                HunCharaDict?.Add(i.Key, chara);
        }

        WeakReferenceMessenger.Default.Send(new CharacterDictChangedMessage(this));

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
        private set
        {
            if (_currentGame == value) return;
            CurrentGame.TeamSwapped -= OnTeamSwapped;
            _currentGame = value;
            CurrentGame.TeamSwapped += OnTeamSwapped;
            CurrentGameChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void NewGame()
    {
        Team surTeam;
        Team hunTeam;
        if (MainTeam.Camp == Camp.Sur)
        {
            surTeam = MainTeam;
            hunTeam = AwayTeam;
        }
        else
        {
            surTeam = AwayTeam;
            hunTeam = MainTeam;
        }

        var pickedMap = CurrentGame.PickedMap;
        var bannedMap = CurrentGame.BannedMap;
        var mapV2Dictionary = CurrentGame.MapV2Dictionary;
        var gameProgress = CurrentGame.GameProgress;

        CurrentGame =
            new Game(surTeam, hunTeam, gameProgress, pickedMap, bannedMap, mapV2Dictionary);

        _ = MessageBoxHelper.ShowInfoAsync($"{I18nHelper.GetLocalizedString("NewGameHasBeenCreated")}\n{CurrentGame.Guid}", I18nHelper.GetLocalizedString("CreateTip"), I18nHelper.GetLocalizedString("Cancel"));
        _logger.LogInformation("New Game Created{CurrentGameGuid}", CurrentGame.Guid);
    }

    private void OnTeamSwapped(object? sender, EventArgs args) => TeamSwapped?.Invoke(this, EventArgs.Empty);

    #region 角色字典

    /// <summary>
    /// 求生者角色字典
    /// </summary>
    public SortedDictionary<string, Character> SurCharaDict { get; set; }

    /// <summary>
    /// 监管者角色字典
    /// </summary>
    public SortedDictionary<string, Character> HunCharaDict { get; set; }

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
        _remainingSeconds = -1;
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
    private double _globalScoreTotalMargin = 365;

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
            if (_isMapV2CampVisible == value) return;
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
        public string? Abbrev { get; set; }
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