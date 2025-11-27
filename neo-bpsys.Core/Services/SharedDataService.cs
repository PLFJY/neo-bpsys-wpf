using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Microsoft.Extensions.Logging;
using neo_bpsys.Core.Abstractions.Services;
using neo_bpsys.Core.Enums;
using neo_bpsys.Core.Messages;
using neo_bpsys.Core.Models;

namespace neo_bpsys.Core.Services;

public class SharedDataService : ISharedDataService
{
    private readonly ILogger<SharedDataService> _logger;
    public SharedDataService(ILogger<SharedDataService> logger)
    {
        _logger = logger;
        MainTeam = new Team(Camp.Sur, TeamType.MainTeam);
        AwayTeam = new Team(Camp.Hun, TeamType.AwayTeam);
        _currentGame = new Game(MainTeam, AwayTeam, GameProgress.Free);
        ReadCharaListFromFile(Path.Combine(AppConstants.ResourcesPath, "CharacterList.json"));
        _timer.Elapsed += Timer_Elapsed;
        _timer.Interval = 1000;
    }

    private void ReadCharaListFromFile(string path)
    {
        if (!File.Exists(path)) return;
        var json = File.ReadAllText(path);
        var characters = JsonSerializer.Deserialize<Dictionary<string, CharacterMini>>(json, new JsonSerializerOptions { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } });
        if (characters == null) return;
        foreach (var kv in characters)
        {
            CharacterDict[kv.Key] = new Character(kv.Key, kv.Value.Camp, kv.Value.ImageFileName);
            if (kv.Value.Camp == Camp.Sur) SurCharaList[kv.Key] = CharacterDict[kv.Key]; else HunCharaList[kv.Key] = CharacterDict[kv.Key];
        }
        _logger.LogInformation("CharacterDict loaded");
    }

    public Team MainTeam { get; set; }
    public Team AwayTeam { get; set; }

    private Game _currentGame;
    public Game CurrentGame
    {
        get => _currentGame;
        set
        {
            if (_currentGame == value) return;
            _currentGame = value;
            CurrentGameChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public Dictionary<string, Character> CharacterDict { get; } = new();
    public Dictionary<string, Character> SurCharaList { get; } = new();
    public Dictionary<string, Character> HunCharaList { get; } = new();

    private readonly System.Timers.Timer _timer = new();
    private int _remainingSeconds = -1;
    public string RemainingSeconds
    {
        get => _remainingSeconds < 0 ? "VS" : _remainingSeconds.ToString();
        set
        {
            if (!int.TryParse(value, out _remainingSeconds)) _remainingSeconds = 0;
            CountDownValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    private void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
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

    private bool _isBo3Mode;
    public bool IsBo3Mode
    {
        get => _isBo3Mode;
        set
        {
            if (_isBo3Mode == value) return;
            var old = _isBo3Mode;
            _isBo3Mode = value;
            WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<bool>(this, nameof(IsBo3Mode), old, value));
            IsBo3ModeChanged?.Invoke(this, EventArgs.Empty);
            _logger.LogInformation("IsBo3Mode changed to {Value}", value);
        }
    }

    private double _globalScoreTotalMargin = 390;
    public double GlobalScoreTotalMargin
    {
        get => _globalScoreTotalMargin;
        set
        {
            if (Math.Abs(_globalScoreTotalMargin - value) < 0.01) return;
            _globalScoreTotalMargin = value;
        }
    }

    public event EventHandler? CurrentGameChanged;
    public event EventHandler? IsBo3ModeChanged;
    public event EventHandler? CountDownValueChanged;

    private class CharacterMini
    {
        public Camp Camp { get; set; }
        public string ImageFileName { get; set; } = string.Empty;
    }
}
