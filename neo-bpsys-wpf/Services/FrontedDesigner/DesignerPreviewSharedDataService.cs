using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Core.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace neo_bpsys_wpf.Services.FrontedDesigner;

/// <summary>
/// Isolated placeholder state for the Fronted Designer preview.
/// </summary>
public sealed class DesignerPreviewSharedDataService : ISharedDataService
{
    public const string MainTeamName = "MainTeam";
    public const string AwayTeamName = "AwayTeam";
    public const string SurvivorCharacterName = "幸运儿";
    public const string HunterCharacterName = "厂长";

    public DesignerPreviewSharedDataService()
    {
        var icon = LoadAppIcon();
        var survivor = new Character(SurvivorCharacterName, Camp.Sur, "幸运儿.png");
        var hunter = new Character(HunterCharacterName, Camp.Hun, "厂长.png");

        SurCharaDict = new SortedDictionary<string, Character>(StringComparer.CurrentCulture)
        {
            [survivor.Name] = survivor
        };
        HunCharaDict = new SortedDictionary<string, Character>(StringComparer.CurrentCulture)
        {
            [hunter.Name] = hunter
        };

        HomeTeam = CreateTeam(Camp.Sur, TeamType.HomeTeam, MainTeamName, icon, 1);
        AwayTeam = CreateTeam(Camp.Hun, TeamType.AwayTeam, AwayTeamName, icon, 5);

        CurrentGame = new Game(
            HomeTeam,
            AwayTeam,
            GameProgress.Game1FirstHalf,
            pickedMap: Map.EversleepingTown,
            bannedMap: Map.TheRedChurch);

        for (var i = 0; i < CurrentGame.SurPlayerList.Count; i++)
        {
            CurrentGame.SurPlayerList[i].Character = survivor;
            CurrentGame.SurPlayerList[i].Talent = new Talent
            {
                BorrowedTime = true,
                FlywheelEffect = true
            };
            CurrentGame.SurPlayerList[i].Data = CreateZeroPlayerData();
        }

        CurrentGame.HunPlayer.Character = hunter;
        CurrentGame.HunPlayer.Talent = new Talent
        {
            Detention = true,
            TrumpCard = true
        };
        CurrentGame.HunPlayer.Trait = new Trait(TraitType.Blink);
        CurrentGame.HunPlayer.Data = CreateZeroPlayerData();

        FillCharacters(CurrentGame.CurrentSurBannedList, survivor);
        FillCharacters(CurrentGame.CurrentHunBannedList, hunter);
        FillCharacters(HomeTeam.GlobalBannedSurList, survivor);
        FillCharacters(HomeTeam.GlobalBannedHunList, hunter);
        FillCharacters(AwayTeam.GlobalBannedSurList, survivor);
        FillCharacters(AwayTeam.GlobalBannedHunList, hunter);

        CurrentGame.MapV2Dictionary[Map.EversleepingTown.ToString()].IsPicked = true;
        CurrentGame.MapV2Dictionary[Map.EversleepingTown.ToString()].OperationTeam = HomeTeam;
        CurrentGame.MapV2Dictionary[Map.TheRedChurch.ToString()].IsBanned = true;
        CurrentGame.MatchScore.RefreshCurrentDisplay(
            CurrentGame.GameProgress,
            CurrentGame.SurTeam.TeamType,
            CurrentGame.HunTeam.TeamType,
            IsBo3Mode);

        CanCurrentSurBannedList = CreateBooleanList(AppConstants.CurrentBanSurCount, true);
        CanCurrentHunBannedList = CreateBooleanList(AppConstants.CurrentBanHunCount, true);
        CanGlobalSurBannedList = CreateBooleanList(AppConstants.GlobalBanSurCount, true);
        CanGlobalHunBannedList = CreateBooleanList(AppConstants.GlobalBanHunCount, true);
    }

    public Team HomeTeam { get; }

    public Team AwayTeam { get; }

    public Game CurrentGame { get; }

    public SortedDictionary<string, Character> SurCharaDict { get; set; }

    public SortedDictionary<string, Character> HunCharaDict { get; set; }

    public ObservableCollection<bool> CanCurrentSurBannedList { get; }

    public ObservableCollection<bool> CanCurrentHunBannedList { get; }

    public ObservableCollection<bool> CanGlobalSurBannedList { get; }

    public ObservableCollection<bool> CanGlobalHunBannedList { get; }

    public bool IsTraitVisible
    {
        get => _isTraitVisible;
        set
        {
            if (_isTraitVisible == value)
            {
                return;
            }

            _isTraitVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsTraitVisible)));
            IsTraitVisibleChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string RemainingSeconds
    {
        get => _remainingSeconds;
        set
        {
            if (_remainingSeconds == value)
            {
                return;
            }

            _remainingSeconds = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RemainingSeconds)));
            CountDownValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsBo3Mode
    {
        get => _isBo3Mode;
        set
        {
            if (_isBo3Mode == value)
            {
                return;
            }

            _isBo3Mode = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBo3Mode)));
            IsBo3ModeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public double GlobalScoreTotalMargin
    {
        get => _globalScoreTotalMargin;
        set
        {
            if (Math.Abs(_globalScoreTotalMargin - value) < 0.01D)
            {
                return;
            }

            _globalScoreTotalMargin = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GlobalScoreTotalMargin)));
            GlobalScoreTotalMarginChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsMapV2Breathing
    {
        get => _isMapV2Breathing;
        set
        {
            if (_isMapV2Breathing == value)
            {
                return;
            }

            _isMapV2Breathing = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMapV2Breathing)));
            IsMapV2BreathingChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsMapV2CampVisible
    {
        get => _isMapV2CampVisible;
        set
        {
            if (_isMapV2CampVisible == value)
            {
                return;
            }

            _isMapV2CampVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMapV2CampVisible)));
            IsMapV2CampVisibleChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool _isTraitVisible = true;
    private string _remainingSeconds = "30";
    private bool _isBo3Mode;
    private double _globalScoreTotalMargin = 370D;
    private bool _isMapV2Breathing = true;
    private bool _isMapV2CampVisible = true;

    public void NewGame()
    {
    }

    public Task ImportGameAsync(string filePath) => Task.CompletedTask;

    public void SetBanCount(BanListName listName, int count)
    {
        var list = listName switch
        {
            BanListName.CanCurrentSurBanned => CanCurrentSurBannedList,
            BanListName.CanCurrentHunBanned => CanCurrentHunBannedList,
            BanListName.CanGlobalSurBanned => CanGlobalSurBannedList,
            BanListName.CanGlobalHunBanned => CanGlobalHunBannedList,
            _ => throw new ArgumentOutOfRangeException(nameof(listName), listName, null)
        };

        for (var i = 0; i < list.Count; i++)
        {
            list[i] = i < count;
        }

        BanCountChanged?.Invoke(this, new BanCountChangedEventArgs(listName, Math.Max(0, count - 1)));
    }

    public void TimerStart(int? seconds)
    {
        if (seconds.HasValue)
        {
            RemainingSeconds = seconds.Value.ToString();
        }
    }

    public void TimerStop()
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? CurrentGameChanged;

    public event EventHandler? GlobalScoreTotalMarginChanged;

    public event EventHandler<BanCountChangedEventArgs>? BanCountChanged;

    public event EventHandler? IsTraitVisibleChanged;

    public event EventHandler? IsBo3ModeChanged;

    public event EventHandler? CountDownValueChanged;

    public event EventHandler? TeamSwapped;

    public event EventHandler? IsMapV2BreathingChanged;

    public event EventHandler? IsMapV2CampVisibleChanged;

    public event EventHandler? PickedMapChanged;

    public event EventHandler? MapV2BannedChanged;

    private static Team CreateTeam(Camp camp, TeamType teamType, string name, ImageSource? logo, int firstPlayerNumber)
    {
        var team = new Team(camp, teamType)
        {
            Name = name,
            Logo = logo
        };

        if (camp == Camp.Sur)
        {
            for (var i = 0; i < 4; i++)
            {
                var member = team.SurMemberList[i];
                member.Name = $"Player {firstPlayerNumber + i}";
                member.IsOnField = true;
                team.MemberOnField(member);
            }
        }
        else
        {
            var member = team.HunMemberList[0];
            member.Name = $"Player {firstPlayerNumber}";
            member.IsOnField = true;
            team.MemberOnField(member);
        }

        return team;
    }

    private static PlayerData CreateZeroPlayerData() =>
        new()
        {
            DecodingProgress = "0",
            PalletStrikes = "0",
            Rescues = "0",
            Heals = "0",
            ContainmentTime = "0",
            RemainingCipher = "0",
            PalletsDestroyed = "0",
            SurvivorHits = "0",
            TerrorShocks = "0",
            Knockdowns = "0"
        };

    private static ObservableCollection<bool> CreateBooleanList(int count, bool value) =>
        [.. Enumerable.Repeat(value, count)];

    private static void FillCharacters(ObservableCollection<Character?> list, Character character)
    {
        for (var i = 0; i < list.Count; i++)
        {
            list[i] = character;
        }
    }

    private static ImageSource? LoadAppIcon()
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri("pack://application:,,,/Assets/icon.png", UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }
}
