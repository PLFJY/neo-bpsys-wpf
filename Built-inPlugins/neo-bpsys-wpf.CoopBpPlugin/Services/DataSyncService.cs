using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.CoopBpPlugin.Models;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.CoopBpPlugin.Services;

/// <summary>
/// 数据同步服务接口
/// 负责在服务端和客户端之间同步游戏数据
/// </summary>
public interface IDataSyncService
{
    /// <summary>启动服务端数据同步</summary>
    void StartServerSync();

    /// <summary>停止服务端数据同步</summary>
    void StopServerSync();

    /// <summary>启动客户端数据同步</summary>
    void StartClientSync();

    /// <summary>停止客户端数据同步</summary>
    void StopClientSync();

    /// <summary>请求完整数据同步</summary>
    Task RequestFullSyncAsync();
}

/// <summary>
/// 数据同步服务实现
/// 监听游戏数据变化并通过网络同步到其他客户端
/// </summary>
public class DataSyncService : IDataSyncService
{
    private readonly ILogger<DataSyncService> _logger;
    private readonly ISharedDataService _sharedDataService;
    private readonly IServerService _serverService;
    private readonly IClientService _clientService;
    private readonly ICharacterSelectionService _characterSelectionService;
    private bool _isServerSyncing;
    private bool _isClientSyncing;
    private bool _isProcessingRemoteMessage;

    public DataSyncService(
        ILogger<DataSyncService> logger,
        ISharedDataService sharedDataService,
        IServerService serverService,
        IClientService clientService,
        ICharacterSelectionService characterSelectionService)
    {
        _logger = logger;
        _sharedDataService = sharedDataService;
        _serverService = serverService;
        _clientService = clientService;
        _characterSelectionService = characterSelectionService;
    }

    /// <summary>
    /// 启动服务端数据同步
    /// 订阅共享数据服务的事件，当数据变化时广播到所有客户端
    /// </summary>
    public void StartServerSync()
    {
        if (_isServerSyncing) return;

        _isServerSyncing = true;
        _serverService.MessageReceived += OnServerMessageReceived;

        // 订阅游戏数据变化事件
        _sharedDataService.CurrentGameChanged += OnCurrentGameChanged;
        _sharedDataService.TeamSwapped += OnTeamSwapped;
        _sharedDataService.BanCountChanged += OnBanCountChanged;

        // 订阅队员上场状态变化事件
        _sharedDataService.HomeTeam.MemberOnFieldChanged += OnMemberOnFieldChanged;
        _sharedDataService.AwayTeam.MemberOnFieldChanged += OnMemberOnFieldChanged;

        // 订阅游戏属性变化事件
        SubscribeToGamePropertyChangedEvents();

        // 订阅玩家属性变化事件
        SubscribeToPlayerPropertyChangedEvents();

        _logger.LogInformation("Server data sync started");
    }

    /// <summary>
    /// 停止服务端数据同步
    /// 取消订阅所有事件
    /// </summary>
    public void StopServerSync()
    {
        if (!_isServerSyncing) return;

        _isServerSyncing = false;
        _serverService.MessageReceived -= OnServerMessageReceived;

        _sharedDataService.CurrentGameChanged -= OnCurrentGameChanged;
        _sharedDataService.TeamSwapped -= OnTeamSwapped;
        _sharedDataService.BanCountChanged -= OnBanCountChanged;

        _sharedDataService.HomeTeam.MemberOnFieldChanged -= OnMemberOnFieldChanged;
        _sharedDataService.AwayTeam.MemberOnFieldChanged -= OnMemberOnFieldChanged;

        // 取消订阅游戏属性变化事件
        UnsubscribeFromGamePropertyChangedEvents();

        // 取消订阅玩家属性变化事件
        UnsubscribeFromPlayerPropertyChangedEvents();

        _logger.LogInformation("Server data sync stopped");
    }

    /// <summary>
    /// 启动客户端数据同步
    /// 订阅服务器消息，接收并应用远程数据变化
    /// </summary>
    public void StartClientSync()
    {
        if (_isClientSyncing) return;

        _isClientSyncing = true;
        _clientService.MessageReceived += OnClientMessageReceived;

        _logger.LogInformation("Client data sync started");
    }

    /// <summary>
    /// 停止客户端数据同步
    /// </summary>
    public void StopClientSync()
    {
        if (!_isClientSyncing) return;

        _isClientSyncing = false;
        _clientService.MessageReceived -= OnClientMessageReceived;

        _logger.LogInformation("Client data sync stopped");
    }

    /// <summary>
    /// 请求完整数据同步
    /// 广播当前游戏状态到所有客户端
    /// </summary>
    public async Task RequestFullSyncAsync()
    {
        var syncData = CreateGameSyncData();
        var message = new NetworkMessage(MessageType.FullSync, JsonSerializer.Serialize(syncData));
        await _serverService.BroadcastAsync(message);
        _logger.LogInformation("Full game data broadcasted");
    }

    #region 订阅 Game PropertyChanged 事件

    /// <summary>
    /// 订阅游戏属性变化事件
    /// 包括对局进度、地图选择、禁用列表等
    /// </summary>
    private void SubscribeToGamePropertyChangedEvents()
    {
        var game = _sharedDataService.CurrentGame;

        // 订阅 Game 的 PropertyChanged 事件
        game.PropertyChanged += OnGamePropertyChanged;

        // 订阅禁用列表的集合变化事件
        game.CurrentSurBannedList.CollectionChanged += OnSurBannedListChanged;
        game.CurrentHunBannedList.CollectionChanged += OnHunBannedListChanged;

        _logger.LogDebug("Subscribed to Game property changed events");
    }

    /// <summary>
    /// 取消订阅游戏属性变化事件
    /// </summary>
    private void UnsubscribeFromGamePropertyChangedEvents()
    {
        var game = _sharedDataService.CurrentGame;

        game.PropertyChanged -= OnGamePropertyChanged;
        game.CurrentSurBannedList.CollectionChanged -= OnSurBannedListChanged;
        game.CurrentHunBannedList.CollectionChanged -= OnHunBannedListChanged;

        _logger.LogDebug("Unsubscribed from Game property changed events");
    }

    /// <summary>
    /// 游戏属性变化事件处理
    /// 处理对局进度、地图选择等属性变化
    /// </summary>
    private void OnGamePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isServerSyncing || _isProcessingRemoteMessage) return;

        var game = _sharedDataService.CurrentGame;

        switch (e.PropertyName)
        {
            case nameof(Game.GameProgress):
                // 广播对局进度变化
                var progressMessage = new NetworkMessage(
                    MessageType.GameProgressChanged,
                    JsonSerializer.Serialize(game.GameProgress));
                _ = _serverService.BroadcastAsync(progressMessage);
                _logger.LogDebug("Game progress changed: {Progress}", game.GameProgress);
                break;

            case nameof(Game.PickedMap):
                // 广播地图选择变化
                var mapData = new MapPickData { Map = game.PickedMap };
                var mapMessage = new NetworkMessage(
                    MessageType.MapPicked,
                    JsonSerializer.Serialize(mapData));
                _ = _serverService.BroadcastAsync(mapMessage);
                _logger.LogDebug("Map picked: {Map}", game.PickedMap);
                break;

            case nameof(Game.BannedMap):
                // 广播地图禁用变化
                var bannedMapData = new MapPickData { Map = game.BannedMap };
                var bannedMapMessage = new NetworkMessage(
                    MessageType.MapBanned,
                    JsonSerializer.Serialize(bannedMapData));
                _ = _serverService.BroadcastAsync(bannedMapMessage);
                _logger.LogDebug("Map banned: {Map}", game.BannedMap);
                break;
        }
    }

    /// <summary>
    /// 求生者禁用列表变化事件处理
    /// </summary>
    private void OnSurBannedListChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_isServerSyncing || _isProcessingRemoteMessage) return;

        // 禁用列表变化时发送完整同步
        _ = RequestFullSyncAsync();
        _logger.LogDebug("Survivor banned list changed");
    }

    /// <summary>
    /// 监管者禁用列表变化事件处理
    /// </summary>
    private void OnHunBannedListChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_isServerSyncing || _isProcessingRemoteMessage) return;

        // 禁用列表变化时发送完整同步
        _ = RequestFullSyncAsync();
        _logger.LogDebug("Hunter banned list changed");
    }

    #endregion

    #region 订阅 Player PropertyChanged 事件

    /// <summary>
    /// 订阅所有玩家的 PropertyChanged 事件
    /// 用于检测角色选择、天赋、特质等变化
    /// </summary>
    private void SubscribeToPlayerPropertyChangedEvents()
    {
        var game = _sharedDataService.CurrentGame;

        // 订阅求生者玩家的 PropertyChanged 事件
        foreach (var player in game.SurPlayerList)
        {
            player.PropertyChanged += OnPlayerPropertyChanged;
        }

        // 订阅监管者玩家的 PropertyChanged 事件
        game.HunPlayer.PropertyChanged += OnPlayerPropertyChanged;

        _logger.LogDebug("Subscribed to Player property changed events");
    }

    /// <summary>
    /// 取消订阅所有玩家的 PropertyChanged 事件
    /// </summary>
    private void UnsubscribeFromPlayerPropertyChangedEvents()
    {
        var game = _sharedDataService.CurrentGame;

        foreach (var player in game.SurPlayerList)
        {
            player.PropertyChanged -= OnPlayerPropertyChanged;
        }

        game.HunPlayer.PropertyChanged -= OnPlayerPropertyChanged;

        _logger.LogDebug("Unsubscribed from Player property changed events");
    }

    /// <summary>
    /// 玩家属性变化事件处理
    /// 检测角色选择、天赋、特质等变化并广播到其他客户端
    /// </summary>
    private void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isServerSyncing || _isProcessingRemoteMessage) return;

        var player = sender as Player;
        if (player == null) return;

        var game = _sharedDataService.CurrentGame;
        var isSurPlayer = game.SurPlayerList.Contains(player);
        var playerIndex = isSurPlayer ? game.SurPlayerList.IndexOf(player) : -1;
        var camp = isSurPlayer ? Camp.Sur : Camp.Hun;

        switch (e.PropertyName)
        {
            case nameof(Player.Character):
                // 广播角色选择变化
                BroadcastCharacterPicked(player, isSurPlayer, playerIndex, camp);
                break;

            case nameof(Player.Talent):
                // 广播天赋变化
                BroadcastTalentChanged(player, isSurPlayer, playerIndex, camp);
                break;

            case nameof(Player.Trait):
                // 广播特质变化
                BroadcastTraitChanged(player, isSurPlayer, playerIndex, camp);
                break;
        }
    }

    /// <summary>
    /// 广播角色选择变化
    /// </summary>
    private void BroadcastCharacterPicked(Player player, bool isSurPlayer, int playerIndex, Camp camp)
    {
        var pickData = new CharacterPickData
        {
            CharacterName = player.Character?.Name ?? string.Empty,
            Camp = camp,
            IsSurPlayer = isSurPlayer,
            PlayerIndex = playerIndex
        };

        var message = new NetworkMessage(
            MessageType.CharacterPicked,
            JsonSerializer.Serialize(pickData));

        _ = _serverService.BroadcastAsync(message);

        _logger.LogDebug("Character picked: {CharacterName} for player {PlayerIndex}",
            pickData.CharacterName, playerIndex);
    }

    /// <summary>
    /// 广播天赋变化
    /// </summary>
    private void BroadcastTalentChanged(Player player, bool isSurPlayer, int playerIndex, Camp camp)
    {
        var talentData = new TalentChangedData
        {
            PlayerIndex = playerIndex,
            IsSurPlayer = isSurPlayer,
            Camp = camp,
            Talent = player.Talent
        };

        var message = new NetworkMessage(
            MessageType.TalentChanged,
            JsonSerializer.Serialize(talentData));

        _ = _serverService.BroadcastAsync(message);

        _logger.LogDebug("Talent changed for player {PlayerIndex}", playerIndex);
    }

    /// <summary>
    /// 广播特质变化
    /// </summary>
    private void BroadcastTraitChanged(Player player, bool isSurPlayer, int playerIndex, Camp camp)
    {
        var traitData = new TraitChangedData
        {
            PlayerIndex = playerIndex,
            IsSurPlayer = isSurPlayer,
            Camp = camp,
            Trait = player.Trait
        };

        var message = new NetworkMessage(
            MessageType.TraitChanged,
            JsonSerializer.Serialize(traitData));

        _ = _serverService.BroadcastAsync(message);

        _logger.LogDebug("Trait changed for player {PlayerIndex}", playerIndex);
    }

    #endregion

    /// <summary>
    /// 处理服务端收到的消息
    /// </summary>
    private void OnServerMessageReceived(object? sender, NetworkMessage message)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                switch (message.Type)
                {
                    case MessageType.FullSync:
                        await RequestFullSyncAsync();
                        break;
                    case MessageType.NewGame:
                        _sharedDataService.NewGame();
                        break;
                    case MessageType.TeamSwapped:
                        _sharedDataService.CurrentGame.Swap();
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process server message");
            }
        });
    }

    /// <summary>
    /// 处理客户端收到的消息
    /// 应用远程数据变化到本地
    /// </summary>
    private void OnClientMessageReceived(object? sender, NetworkMessage message)
    {
        if (_isProcessingRemoteMessage) return;

        _ = Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            _isProcessingRemoteMessage = true;
            try
            {
                switch (message.Type)
                {
                    case MessageType.FullSync:
                        ApplyFullSync(message.Data);
                        break;
                    case MessageType.GameProgressChanged:
                        ApplyGameProgressChanged(message.Data);
                        break;
                    case MessageType.TeamSwapped:
                        _sharedDataService.CurrentGame.Swap();
                        break;
                    case MessageType.NewGame:
                        _sharedDataService.NewGame();
                        break;
                    case MessageType.CharacterPicked:
                        await ApplyCharacterPickedAsync(message.Data);
                        break;
                    case MessageType.CharacterBanned:
                        await ApplyCharacterBannedAsync(message.Data);
                        break;
                    case MessageType.TalentChanged:
                        ApplyTalentChanged(message.Data);
                        break;
                    case MessageType.TraitChanged:
                        ApplyTraitChanged(message.Data);
                        break;
                    case MessageType.MapPicked:
                        ApplyMapPicked(message.Data);
                        break;
                    case MessageType.MapBanned:
                        ApplyMapBanned(message.Data);
                        break;
                    case MessageType.TimerStart:
                        ApplyTimerStart(message.Data);
                        break;
                    case MessageType.TimerStop:
                        _sharedDataService.TimerStop();
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process client message");
            }
            finally
            {
                _isProcessingRemoteMessage = false;
            }
        });
    }

    /// <summary>
    /// 创建游戏同步数据
    /// </summary>
    private GameSyncData CreateGameSyncData()
    {
        var game = _sharedDataService.CurrentGame;
        return new GameSyncData
        {
            Game = SerializableGame.FromGame(game),
            IsBo3Mode = _sharedDataService.IsBo3Mode,
            IsTraitVisible = _sharedDataService.IsTraitVisible,
            RemainingSeconds = _sharedDataService.RemainingSeconds
        };
    }

    /// <summary>
    /// 应用完整数据同步
    /// </summary>
    private void ApplyFullSync(string? data)
    {
        if (string.IsNullOrEmpty(data)) return;

        var syncData = JsonSerializer.Deserialize<GameSyncData>(data);
        if (syncData == null) return;

        _sharedDataService.IsBo3Mode = syncData.IsBo3Mode;
        _sharedDataService.IsTraitVisible = syncData.IsTraitVisible;
        _sharedDataService.RemainingSeconds = syncData.RemainingSeconds;

        ApplySerializableGame(syncData.Game);

        _logger.LogInformation("Full game data sync applied");
    }

    /// <summary>
    /// 应用可序列化游戏数据到当前游戏
    /// </summary>
    private void ApplySerializableGame(SerializableGame serializableGame)
    {
        var game = _sharedDataService.CurrentGame;

        game.GameProgress = serializableGame.GameProgress;

        // 应用队伍数据
        ApplyTeamData(_sharedDataService.HomeTeam, serializableGame.SurTeam);
        ApplyTeamData(_sharedDataService.AwayTeam, serializableGame.HunTeam);

        // 应用玩家数据
        ApplyPlayerData(game, serializableGame);

        // 应用禁用数据
        ApplyBanData(game, serializableGame);

        // 应用地图数据
        game.PickedMap = serializableGame.PickedMap;
        game.BannedMap = serializableGame.BannedMap;

        // 应用MapV2数据
        if (serializableGame.MapV2Data != null)
        {
            foreach (var kvp in serializableGame.MapV2Data)
            {
                if (game.MapV2Dictionary.TryGetValue(kvp.Key, out var mapV2))
                {
                    mapV2.IsPicked = kvp.Value.IsPicked;
                    mapV2.IsBanned = kvp.Value.IsBanned;
                }
            }
        }
    }

    /// <summary>
    /// 应用队伍数据
    /// </summary>
    private void ApplyTeamData(Team team, SerializableTeam serializableTeam)
    {
        team.Name = serializableTeam.Name;
        team.ImageUri = serializableTeam.ImageUri;

        // 应用求生者队员数据
        for (var i = 0; i < Math.Min(4, serializableTeam.SurMemberList.Count); i++)
        {
            if (i < team.SurMemberList.Count)
            {
                var member = team.SurMemberList[i];
                var serialMember = serializableTeam.SurMemberList[i];
                member.Name = serialMember.Name;
                member.ImageUri = serialMember.ImageUri;
                member.IsOnField = serialMember.IsOnField;
            }
        }

        // 应用监管者队员数据
        for (var i = 0; i < Math.Min(1, serializableTeam.HunMemberList.Count); i++)
        {
            if (i < team.HunMemberList.Count)
            {
                var member = team.HunMemberList[i];
                var serialMember = serializableTeam.HunMemberList[i];
                member.Name = serialMember.Name;
                member.ImageUri = serialMember.ImageUri;
                member.IsOnField = serialMember.IsOnField;
            }
        }

        // 应用全局禁用数据
        ApplyGlobalBanData(team, serializableTeam);
    }

    /// <summary>
    /// 应用全局禁用数据
    /// </summary>
    private void ApplyGlobalBanData(Team team, SerializableTeam serializableTeam)
    {
        // 应用求生者全局禁用
        for (var i = 0; i < Math.Min(serializableTeam.GlobalBannedSurNames.Count, team.GlobalBannedSurRecordList.Count); i++)
        {
            var name = serializableTeam.GlobalBannedSurNames[i];
            if (string.IsNullOrEmpty(name))
            {
                team.GlobalBannedSurRecordList[i] = null;
            }
            else
            {
                team.GlobalBannedSurRecordList[i] = _sharedDataService.SurCharaDict.TryGetValue(name, out var c) ? c : null;
            }
        }

        // 应用监管者全局禁用
        for (var i = 0; i < Math.Min(serializableTeam.GlobalBannedHunNames.Count, team.GlobalBannedHunRecordList.Count); i++)
        {
            var name = serializableTeam.GlobalBannedHunNames[i];
            if (string.IsNullOrEmpty(name))
            {
                team.GlobalBannedHunRecordList[i] = null;
            }
            else
            {
                team.GlobalBannedHunRecordList[i] = _sharedDataService.HunCharaDict.TryGetValue(name, out var c) ? c : null;
            }
        }

        team.UpdateGlobalBanFromRecord();
    }

    /// <summary>
    /// 应用玩家数据
    /// </summary>
    private void ApplyPlayerData(Game game, SerializableGame serializableGame)
    {
        // 应用求生者玩家数据
        for (var i = 0; i < Math.Min(4, serializableGame.SurPlayers.Count); i++)
        {
            var player = game.SurPlayerList[i];
            var serialPlayer = serializableGame.SurPlayers[i];

            player.Member.Name = serialPlayer.Member.Name;
            player.Member.ImageUri = serialPlayer.Member.ImageUri;

            if (serialPlayer.Character != null)
            {
                player.Character = serialPlayer.Character.ToCharacter(
                    _sharedDataService.SurCharaDict, _sharedDataService.HunCharaDict);
            }
            else
            {
                player.Character = null;
            }

            player.Talent = serialPlayer.Talent.ToTalent();
            player.Trait = serialPlayer.Trait.ToTrait();
        }

        // 应用监管者玩家数据
        if (serializableGame.HunPlayer != null)
        {
            var hunPlayer = game.HunPlayer;
            var serialPlayer = serializableGame.HunPlayer;

            hunPlayer.Member.Name = serialPlayer.Member.Name;
            hunPlayer.Member.ImageUri = serialPlayer.Member.ImageUri;

            if (serialPlayer.Character != null)
            {
                hunPlayer.Character = serialPlayer.Character.ToCharacter(
                    _sharedDataService.SurCharaDict, _sharedDataService.HunCharaDict);
            }
            else
            {
                hunPlayer.Character = null;
            }

            hunPlayer.Talent = serialPlayer.Talent.ToTalent();
            hunPlayer.Trait = serialPlayer.Trait.ToTrait();
        }
    }

    /// <summary>
    /// 应用禁用数据
    /// </summary>
    private void ApplyBanData(Game game, SerializableGame serializableGame)
    {
        // 调整求生者禁用列表大小
        while (game.CurrentSurBannedList.Count < serializableGame.CurrentSurBannedNames.Count)
        {
            game.CurrentSurBannedList.Add(null);
        }
        while (game.CurrentSurBannedList.Count > serializableGame.CurrentSurBannedNames.Count)
        {
            game.CurrentSurBannedList.RemoveAt(game.CurrentSurBannedList.Count - 1);
        }

        // 应用求生者禁用数据
        for (var i = 0; i < serializableGame.CurrentSurBannedNames.Count; i++)
        {
            var name = serializableGame.CurrentSurBannedNames[i];
            game.CurrentSurBannedList[i] = string.IsNullOrEmpty(name)
                ? null
                : _sharedDataService.SurCharaDict.TryGetValue(name, out var c) ? c : null;
        }

        // 调整监管者禁用列表大小
        while (game.CurrentHunBannedList.Count < serializableGame.CurrentHunBannedNames.Count)
        {
            game.CurrentHunBannedList.Add(null);
        }
        while (game.CurrentHunBannedList.Count > serializableGame.CurrentHunBannedNames.Count)
        {
            game.CurrentHunBannedList.RemoveAt(game.CurrentHunBannedList.Count - 1);
        }

        // 应用监管者禁用数据
        for (var i = 0; i < serializableGame.CurrentHunBannedNames.Count; i++)
        {
            var name = serializableGame.CurrentHunBannedNames[i];
            game.CurrentHunBannedList[i] = string.IsNullOrEmpty(name)
                ? null
                : _sharedDataService.HunCharaDict.TryGetValue(name, out var c) ? c : null;
        }
    }

    /// <summary>
    /// 应用游戏进度变化
    /// </summary>
    private void ApplyGameProgressChanged(string? data)
    {
        if (string.IsNullOrEmpty(data)) return;
        try
        {
            var progress = JsonSerializer.Deserialize<GameProgress>(data);
            _sharedDataService.CurrentGame.GameProgress = progress;
            _logger.LogDebug("Applied game progress: {Progress}", progress);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize game progress");
        }
    }

    /// <summary>
    /// 应用角色选择（使用 ICharacterSelectionService）
    /// </summary>
    private async Task ApplyCharacterPickedAsync(string? data)
    {
        if (string.IsNullOrEmpty(data)) return;
        var pickData = JsonSerializer.Deserialize<CharacterPickData>(data);
        if (pickData == null) return;

        var dict = pickData.Camp == Camp.Sur ? _sharedDataService.SurCharaDict : _sharedDataService.HunCharaDict;

        Character? character = null;
        if (!string.IsNullOrEmpty(pickData.CharacterName) && dict.TryGetValue(pickData.CharacterName, out var c))
        {
            character = c;
        }

        // 使用 ICharacterSelectionService 进行角色选择
        if (pickData.IsSurPlayer && pickData.PlayerIndex >= 0 && pickData.PlayerIndex < 4)
        {
            await _characterSelectionService.SelectSurvivorAsync(pickData.PlayerIndex, character, playAnimation: false);
            _logger.LogDebug("Applied survivor character selection: {CharacterName} for player {PlayerIndex}",
                pickData.CharacterName, pickData.PlayerIndex);
        }
        else if (!pickData.IsSurPlayer)
        {
            await _characterSelectionService.SelectHunterAsync(character, playAnimation: false);
            _logger.LogDebug("Applied hunter character selection: {CharacterName}", pickData.CharacterName);
        }
    }

    /// <summary>
    /// 应用角色禁用（使用 ICharacterSelectionService）
    /// </summary>
    private async Task ApplyCharacterBannedAsync(string? data)
    {
        if (string.IsNullOrEmpty(data)) return;
        var banData = JsonSerializer.Deserialize<CharacterBanData>(data);
        if (banData == null) return;

        var dict = banData.Camp == Camp.Sur ? _sharedDataService.SurCharaDict : _sharedDataService.HunCharaDict;

        Character? character = null;
        if (!string.IsNullOrEmpty(banData.CharacterName) && dict.TryGetValue(banData.CharacterName, out var c))
        {
            character = c;
        }

        if (banData.IsCurrentBan)
        {
            await _characterSelectionService.BanCharacterAsync(banData.Camp, banData.Index, character, playAnimation: false);
            _logger.LogDebug("Applied character ban: {CharacterName} at index {Index}",
                banData.CharacterName, banData.Index);
        }
    }

    /// <summary>
    /// 应用天赋变化
    /// </summary>
    private void ApplyTalentChanged(string? data)
    {
        if (string.IsNullOrEmpty(data)) return;
        var talentData = JsonSerializer.Deserialize<TalentChangedData>(data);
        if (talentData == null) return;

        var game = _sharedDataService.CurrentGame;

        if (talentData.IsSurPlayer && talentData.PlayerIndex >= 0 && talentData.PlayerIndex < 4)
        {
            game.SurPlayerList[talentData.PlayerIndex].Talent = talentData.Talent;
            _logger.LogDebug("Applied talent change for player {PlayerIndex}", talentData.PlayerIndex);
        }
        else if (!talentData.IsSurPlayer)
        {
            game.HunPlayer.Talent = talentData.Talent;
            _logger.LogDebug("Applied talent change for hunter");
        }
    }

    /// <summary>
    /// 应用特质变化
    /// </summary>
    private void ApplyTraitChanged(string? data)
    {
        if (string.IsNullOrEmpty(data)) return;
        var traitData = JsonSerializer.Deserialize<TraitChangedData>(data);
        if (traitData == null) return;

        var game = _sharedDataService.CurrentGame;

        if (traitData.IsSurPlayer && traitData.PlayerIndex >= 0 && traitData.PlayerIndex < 4)
        {
            game.SurPlayerList[traitData.PlayerIndex].Trait = traitData.Trait;
            _logger.LogDebug("Applied trait change for player {PlayerIndex}", traitData.PlayerIndex);
        }
        else if (!traitData.IsSurPlayer)
        {
            game.HunPlayer.Trait = traitData.Trait;
            _logger.LogDebug("Applied trait change for hunter");
        }
    }

    /// <summary>
    /// 应用地图选择
    /// </summary>
    private void ApplyMapPicked(string? data)
    {
        if (string.IsNullOrEmpty(data)) return;
        var mapData = JsonSerializer.Deserialize<MapPickData>(data);
        if (mapData == null) return;

        _sharedDataService.CurrentGame.PickedMap = mapData.Map;
        _logger.LogDebug("Applied map pick: {Map}", mapData.Map);
    }

    /// <summary>
    /// 应用地图禁用
    /// </summary>
    private void ApplyMapBanned(string? data)
    {
        if (string.IsNullOrEmpty(data)) return;
        var mapData = JsonSerializer.Deserialize<MapPickData>(data);
        if (mapData == null) return;

        _sharedDataService.CurrentGame.BannedMap = mapData.Map;
        _logger.LogDebug("Applied map ban: {Map}", mapData.Map);
    }

    /// <summary>
    /// 应用计时器启动
    /// </summary>
    private void ApplyTimerStart(string? data)
    {
        if (string.IsNullOrEmpty(data)) return;
        if (int.TryParse(data, out var seconds))
        {
            _sharedDataService.TimerStart(seconds);
        }
    }

    /// <summary>
    /// 当前游戏变化事件处理
    /// </summary>
    private void OnCurrentGameChanged(object? sender, EventArgs e)
    {
        if (!_isServerSyncing) return;

        // 重新订阅新游戏的属性变化事件
        UnsubscribeFromGamePropertyChangedEvents();
        UnsubscribeFromPlayerPropertyChangedEvents();
        SubscribeToGamePropertyChangedEvents();
        SubscribeToPlayerPropertyChangedEvents();

        _ = RequestFullSyncAsync();
    }

    /// <summary>
    /// 队伍交换事件处理
    /// </summary>
    private void OnTeamSwapped(object? sender, EventArgs e)
    {
        if (!_isServerSyncing) return;
        _ = _serverService.BroadcastAsync(new NetworkMessage(MessageType.TeamSwapped));
    }

    /// <summary>
    /// 禁用数量变化事件处理
    /// </summary>
    private void OnBanCountChanged(object? sender, EventArgs e)
    {
        if (!_isServerSyncing) return;
        _ = RequestFullSyncAsync();
    }

    /// <summary>
    /// 队员上场状态变化事件处理
    /// </summary>
    private void OnMemberOnFieldChanged(object? sender, EventArgs e)
    {
        if (!_isServerSyncing) return;
        _ = RequestFullSyncAsync();
    }
}

/// <summary>
/// 角色选择数据
/// </summary>
public class CharacterPickData
{
    /// <summary>角色名称</summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>阵营</summary>
    public Camp Camp { get; set; }

    /// <summary>是否为求生者玩家</summary>
    public bool IsSurPlayer { get; set; }

    /// <summary>玩家索引</summary>
    public int PlayerIndex { get; set; }
}

/// <summary>
/// 角色禁用数据
/// </summary>
public class CharacterBanData
{
    /// <summary>角色名称</summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>阵营</summary>
    public Camp Camp { get; set; }

    /// <summary>是否为当前局禁用</summary>
    public bool IsCurrentBan { get; set; }

    /// <summary>禁用索引</summary>
    public int Index { get; set; }
}

/// <summary>
/// 天赋变化数据
/// </summary>
public class TalentChangedData
{
    /// <summary>玩家索引</summary>
    public int PlayerIndex { get; set; }

    /// <summary>是否为求生者玩家</summary>
    public bool IsSurPlayer { get; set; }

    /// <summary>阵营</summary>
    public Camp Camp { get; set; }

    /// <summary>天赋</summary>
    public Talent Talent { get; set; } = new();
}

/// <summary>
/// 特质变化数据
/// </summary>
public class TraitChangedData
{
    /// <summary>玩家索引</summary>
    public int PlayerIndex { get; set; }

    /// <summary>是否为求生者玩家</summary>
    public bool IsSurPlayer { get; set; }

    /// <summary>阵营</summary>
    public Camp Camp { get; set; }

    /// <summary>特质</summary>
    public Trait Trait { get; set; } = new(null);
}

/// <summary>
/// 地图选择数据
/// </summary>
public class MapPickData
{
    /// <summary>地图</summary>
    public Map? Map { get; set; }
}
