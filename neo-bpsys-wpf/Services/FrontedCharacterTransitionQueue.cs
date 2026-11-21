using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

namespace neo_bpsys_wpf.Services;

/// <summary>维护 BP 前台的临时角色展示，并按所涉及槽位调度过渡动画。</summary>
public sealed class FrontedCharacterTransitionQueue : ICharacterTransitionQueue, IFrontedCharacterPresentationService
{
    private static readonly Regex SurvivorPath = new(
        @"^CurrentGame\.SurPlayerList\[(?<index>[0-3])\]\.(?<tail>Character(?:\..+)?|PictureShown(?:WithFullCharacter|Header)?)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex HunterPath = new(
        @"^CurrentGame\.HunPlayer\.(?<tail>Character(?:\..+)?|PictureShown(?:WithFullCharacter|Header)?)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly ISharedDataService _shared;
    private readonly IFrontedTransitionOrchestrator _orchestrator;
    private readonly IFrontedLayoutService _layout;
    private readonly ILogger<FrontedCharacterTransitionQueue> _logger;
    private readonly Dispatcher _dispatcher;
    private readonly Player[] _survivors = new Player[4];
    private Player? _hunter;
    private Game? _game;
    private GameProgress _progress;
    private CancellationTokenSource _generation = new();
    private readonly Task[] _tails = new Task[5];
    private readonly HashSet<Task> _running = [];
    private bool _manualBatch;

    /// <summary>创建角色展示队列。</summary>
    /// <param name="shared">权威比赛状态。</param>
    /// <param name="orchestrator">前台过渡编排器。</param>
    /// <param name="hostManager">前台宿主生命周期管理器。</param>
    /// <param name="layout">活动窗口布局。</param>
    /// <param name="logger">诊断日志。</param>
    public FrontedCharacterTransitionQueue(
        ISharedDataService shared,
        IFrontedTransitionOrchestrator orchestrator,
        FrontedBehaviorRuntimeHostManager hostManager,
        IFrontedLayoutService layout,
        ILogger<FrontedCharacterTransitionQueue> logger)
    {
        _shared = shared;
        _orchestrator = orchestrator;
        _layout = layout;
        _logger = logger;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        Array.Fill(_tails, Task.CompletedTask);
        _shared.CurrentGameChanged += (_, _) => OnGameContextChanged();
        _shared.GameProgressChanged += (_, _) => OnGameContextChanged();
        hostManager.HostDetached += windowType =>
        {
            if (!string.Equals(windowType, "BpWindow", StringComparison.Ordinal)) return;
            if (_dispatcher.CheckAccess()) SynchronizeImmediately();
            else _dispatcher.BeginInvoke(SynchronizeImmediately);
        };
    }

    /// <inheritdoc />
    public void EnsureCurrentGame()
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(EnsureCurrentGame);
            return;
        }
        var game = _shared.CurrentGame;
        if (ReferenceEquals(_game, game) && _progress == game.GameProgress)
            return;
        if (_game is not null)
        {
            foreach (var player in _game.SurPlayerList)
                PropertyChangedEventManager.RemoveHandler(player, OnActualPlayerMemberChanged, nameof(Player.Member));
            PropertyChangedEventManager.RemoveHandler(_game.HunPlayer, OnActualPlayerMemberChanged, nameof(Player.Member));
        }
        CancelGeneration();
        _game = game;
        _progress = game.GameProgress;
        for (var i = 0; i < 4; i++)
        {
            var actual = game.SurPlayerList[i];
            if (_survivors[i] is null)
                _survivors[i] = new Player(actual.Member);
            _survivors[i].Member = actual.Member;
            _survivors[i].Character = actual.Character;
            PropertyChangedEventManager.AddHandler(actual, OnActualPlayerMemberChanged, nameof(Player.Member));
        }
        _hunter ??= new Player(game.HunPlayer.Member);
        _hunter.Member = game.HunPlayer.Member;
        _hunter.Character = game.HunPlayer.Character;
        PropertyChangedEventManager.AddHandler(game.HunPlayer, OnActualPlayerMemberChanged, nameof(Player.Member));
    }

    /// <inheritdoc />
    public bool TryCreateBinding(string path, ISharedDataService source, out Binding? binding)
    {
        binding = null;
        if (!ReferenceEquals(source, _shared)) return false;
        var survivor = SurvivorPath.Match(path);
        if (survivor.Success)
        {
            EnsureCurrentGame();
            binding = new Binding(survivor.Groups["tail"].Value)
            {
                Source = _survivors[int.Parse(survivor.Groups["index"].Value)],
                Mode = BindingMode.OneWay
            };
            return true;
        }
        var hunter = HunterPath.Match(path);
        if (!hunter.Success) return false;
        EnsureCurrentGame();
        binding = new Binding(hunter.Groups["tail"].Value) { Source = _hunter, Mode = BindingMode.OneWay };
        return true;
    }

    /// <inheritdoc />
    public IReadOnlyList<Player> GetSurvivorPlayers()
    {
        EnsureCurrentGame();
        return _survivors;
    }

    /// <inheritdoc />
    public Player GetHunterPlayer()
    {
        EnsureCurrentGame();
        return _hunter!;
    }

    /// <inheritdoc />
    public void EnqueuePick(Camp camp, int index, Character? oldCharacter, Character? newCharacter)
    {
        EnsureCurrentGame();
        if (_manualBatch) return;
        var slot = camp == Camp.Sur ? index : 4;
        Enqueue([slot], [newCharacter], () => CreatePickRequestsAsync(camp, index, oldCharacter, newCharacter));
    }

    /// <inheritdoc />
    public void EnqueueSwap(int source, int target, Character? sourceCharacter, Character? targetCharacter)
    {
        EnsureCurrentGame();
        if (_manualBatch || source == target) return;
        Enqueue([source, target], [targetCharacter, sourceCharacter], () => CreateSwapRequestsAsync(source, target));
    }

    /// <inheritdoc />
    public void SynchronizeImmediately()
    {
        EnsureCurrentGame();
        if (_manualBatch) return;
        CancelGeneration();
        SynchronizePlayers();
    }

    /// <inheritdoc />
    public async Task<T> RunManualSyncAsync<T>(Func<Task<T>> applyAsync, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applyAsync);
        if (!_dispatcher.CheckAccess())
            return await _dispatcher.InvokeAsync(() => RunManualSyncAsync(applyAsync, cancellationToken)).Task.Unwrap();

        EnsureCurrentGame();
        cancellationToken.ThrowIfCancellationRequested();
        CancelGeneration();
        var previous = _running.ToArray();
        if (previous.Length > 0)
        {
            try
            {
                await Task.WhenAll(previous).WaitAsync(TimeSpan.FromMilliseconds(1500), cancellationToken);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Timed out waiting for cancelled character transitions before manual sync.");
            }
        }
        EnsureCurrentGame();
        var manualGame = _game;
        var before = SnapshotPresented();
        _manualBatch = true;
        T result;
        try
        {
            result = await applyAsync();
        }
        catch
        {
            _manualBatch = false;
            SynchronizeImmediately();
            throw;
        }
        finally
        {
            _manualBatch = false;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            SynchronizeImmediately();
            cancellationToken.ThrowIfCancellationRequested();
        }
        EnsureCurrentGame();
        if (!ReferenceEquals(_game, manualGame)) return result;
        var final = SnapshotAuthoritative();
        for (var slot = 0; slot < final.Length; slot++)
        {
            if (string.Equals(before[slot]?.Name, final[slot]?.Name, StringComparison.Ordinal))
            {
                ApplyPresented([slot], [final[slot]]);
                continue;
            }
            var camp = slot == 4 ? Camp.Hun : Camp.Sur;
            EnqueuePick(camp, slot == 4 ? -1 : slot, before[slot], final[slot]);
        }
        return result;
    }

    private void Enqueue(int[] slots, Character?[] final, Func<Task<IReadOnlyList<FrontedTransitionRequest>>> requestFactory)
    {
        var token = _generation.Token;
        var game = _game;
        var predecessor = Task.WhenAll(slots.Select(slot => _tails[slot]));
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        foreach (var slot in slots) _tails[slot] = completion.Task;
        var run = RunQueuedAsync(predecessor, slots, final, requestFactory, game, token, completion);
        _running.Add(run);
        _ = run.ContinueWith(_ => _dispatcher.BeginInvoke(() => _running.Remove(run)), TaskScheduler.Default);
    }

    private async Task RunQueuedAsync(
        Task predecessor,
        int[] slots,
        Character?[] final,
        Func<Task<IReadOnlyList<FrontedTransitionRequest>>> requestFactory,
        Game? game,
        CancellationToken token,
        TaskCompletionSource completion)
    {
        var presented = false;
        try
        {
            await predecessor;
            token.ThrowIfCancellationRequested();
            var requests = await requestFactory();
            token.ThrowIfCancellationRequested();
            await _orchestrator.RunMultiTargetTransitionAsync(requests, () =>
            {
                if (!token.IsCancellationRequested && ReferenceEquals(_game, game))
                {
                    ApplyPresented(slots, final);
                    presented = true;
                }
                return Task.CompletedTask;
            }, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Character transition failed; presentation will be synchronized for affected slots.");
        }
        finally
        {
            if (!presented && !token.IsCancellationRequested && ReferenceEquals(_game, game))
                ApplyPresented(slots, final);
            completion.TrySetResult();
        }
    }

    private async Task<IReadOnlyList<FrontedTransitionRequest>> CreatePickRequestsAsync(
        Camp camp, int index, Character? oldCharacter, Character? newCharacter)
    {
        var name = camp == Camp.Sur ? $"SurPick{index}" : "HunPick";
        var guid = await ResolveGuidAsync(name);
        var request = new FrontedTransitionRequest
        {
            WindowType = "BpWindow",
            TransitionType = "Selection.CharacterPick",
            TargetBehaviorGuid = guid,
            TargetDisplayName = name
        };
        AddPayload(request.Payload, "Camp", camp.ToString());
        AddPayload(request.Payload, "PlayerIndex", index);
        AddPayload(request.Payload, "TargetBehaviorGuid", guid);
        AddPayload(request.Payload, "OldCharacterName", oldCharacter?.Name);
        AddPayload(request.Payload, "NewCharacterName", newCharacter?.Name);
        AddPayload(request.Payload, "OldCharacterId", oldCharacter?.Name);
        AddPayload(request.Payload, "NewCharacterId", newCharacter?.Name);
        AddPayload(request.Payload, "HasOldCharacter", !string.IsNullOrWhiteSpace(oldCharacter?.Name));
        AddPayload(request.Payload, "HasNewCharacter", !string.IsNullOrWhiteSpace(newCharacter?.Name));
        return [request];
    }

    private async Task<IReadOnlyList<FrontedTransitionRequest>> CreateSwapRequestsAsync(int source, int target)
    {
        var sourceGuid = await ResolveGuidAsync($"SurPick{source}");
        var targetGuid = await ResolveGuidAsync($"SurPick{target}");
        var payload = new Dictionary<string, object?>();
        AddPayload(payload, "SourceIndex", source);
        AddPayload(payload, "TargetIndex", target);
        AddPayload(payload, "SourceBehaviorGuid", sourceGuid);
        AddPayload(payload, "TargetBehaviorGuid", targetGuid);
        return
        [
            new FrontedTransitionRequest { WindowType = "BpWindow", TransitionType = "Selection.CharacterSwap", TargetBehaviorGuid = sourceGuid, TargetDisplayName = $"SurPick{source}", Payload = new(payload) },
            new FrontedTransitionRequest { WindowType = "BpWindow", TransitionType = "Selection.CharacterSwap", TargetBehaviorGuid = targetGuid, TargetDisplayName = $"SurPick{target}", Payload = new(payload) }
        ];
    }

    private async Task<Guid> ResolveGuidAsync(string name)
    {
        var config = await _layout.LoadWindowConfigAsync("BpWindow");
        return config?.ControlLayout.Controls.TryGetValue(name, out var control) == true
            ? control.BehaviorGuid : Guid.Empty;
    }

    private static void AddPayload(IDictionary<string, object?> payload, string key, object? value)
    {
        payload[key] = value;
        payload[$"Event.{key}"] = value;
    }

    private void OnGameContextChanged()
    {
        if (_dispatcher.CheckAccess()) EnsureCurrentGame();
        else _dispatcher.BeginInvoke(EnsureCurrentGame);
    }

    private void OnActualPlayerMemberChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_game is null || sender is not Player player) return;
        if (ReferenceEquals(player, _game.HunPlayer))
        {
            _hunter!.Member = player.Member;
            return;
        }
        for (var index = 0; index < 4; index++)
        {
            if (!ReferenceEquals(player, _game.SurPlayerList[index])) continue;
            _survivors[index].Member = player.Member;
            return;
        }
    }

    private void CancelGeneration()
    {
        var previous = _generation;
        previous.Cancel();
        var running = _running.ToArray();
        if (running.Length == 0) previous.Dispose();
        else _ = Task.WhenAll(running).ContinueWith(_ => previous.Dispose(), TaskScheduler.Default);
        _generation = new CancellationTokenSource();
        Array.Fill(_tails, Task.CompletedTask);
    }

    private void SynchronizePlayers()
    {
        if (_game is null) return;
        for (var i = 0; i < 4; i++)
        {
            _survivors[i].Member = _game.SurPlayerList[i].Member;
            _survivors[i].Character = _game.SurPlayerList[i].Character;
        }
        _hunter!.Member = _game.HunPlayer.Member;
        _hunter.Character = _game.HunPlayer.Character;
    }

    private Character?[] SnapshotPresented() => [.. _survivors.Select(player => player.Character), _hunter!.Character];
    private Character?[] SnapshotAuthoritative() => [.. _game!.SurPlayerList.Select(player => player.Character), _game.HunPlayer.Character];

    private void ApplyPresented(int[] slots, Character?[] final)
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(() => ApplyPresented(slots, final));
            return;
        }
        for (var i = 0; i < slots.Length; i++)
        {
            if (slots[i] == 4) _hunter!.Character = final[i];
            else _survivors[slots[i]].Character = final[i];
        }
    }
}
