using F23.StringSimilarity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 角色选择服务的默认实现。
/// </summary>
public partial class CharacterSelectionService(
    ISharedDataService sharedDataService,
    IFrontedTransitionOrchestrator transitionOrchestrator,
    IFrontedLayoutService layoutService,
    ILogger<CharacterSelectionService>? logger = null)
    : ICharacterSelectionService
{
    private const double CharacterMatchThreshold = 0.70;
    private const double SafeFuzzyThreshold = 0.88;
    private const double ClearFuzzyGap = 0.08;
    private readonly JaroWinkler _characterSimilarity = new();
    private readonly Dispatcher _uiDispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    private readonly ILogger<CharacterSelectionService> _logger = logger ?? NullLogger<CharacterSelectionService>.Instance;

#if DEBUG
    static CharacterSelectionService()
    {
        Debug.WriteLine($"[DIAG] CharacterSelectionService: static ctor at {DateTimeOffset.Now:HH:mm:ss.fff}");
    }
#endif
    /// <inheritdoc/>
    public BpSlotCommitStateSnapshot GetCurrentBpSlotCommitState() =>
        sharedDataService.CurrentGame.BpSlotCommitState.CreateSnapshot();

    /// <inheritdoc/>
    public CharacterResolveResult ResolveCharacterDetailed(string text, Camp camp)
    {
        var rawText = text ?? string.Empty;
        var normalizedText = NormalizeCharacterName(rawText);
        if (string.IsNullOrEmpty(normalizedText))
            return Unresolved(rawText, camp, 0, "unresolved", "empty text");

        var candidates = GetCandidates(camp).ToArray();
        if (candidates.Length == 0)
            return Unresolved(rawText, camp, 0, "unresolved", "no candidates in camp");

        var exact = candidates.Where(candidate => string.Equals(candidate.Name, rawText.Trim(), StringComparison.Ordinal)).ToArray();
        if (exact.Length == 1)
            return Resolved(rawText, camp, exact[0], 1, "exact", true, "exact camp-scoped match");
        if (exact.Length > 1)
            return Ambiguous(rawText, camp, 1, "exact", exact.Select(item => item.Name));

        var normalizedExact = candidates.Where(candidate => candidate.Normalized == normalizedText).ToArray();
        if (normalizedExact.Length == 1)
            return Resolved(rawText, camp, normalizedExact[0], 1, "normalized-exact", true, "normalized exact camp-scoped match");
        if (normalizedExact.Length > 1)
            return Ambiguous(rawText, camp, 1, "normalized-exact", normalizedExact.Select(item => item.Name));

        var oppositeCamp = camp == Camp.Sur ? Camp.Hun : Camp.Sur;
        var oppositeExact = GetCandidates(oppositeCamp)
            .Where(candidate => string.Equals(candidate.Name, rawText.Trim(), StringComparison.Ordinal))
            .ToArray();
        if (oppositeExact.Length > 0)
            return Unresolved(rawText, camp, 1, "opposite-camp-exact-veto",
                $"opposite-camp exact-name veto: {rawText.Trim()} is canonical in {oppositeCamp}");

        var contains = candidates
            .Where(candidate =>
                normalizedText.Length >= 2 &&
                candidate.Normalized.Length >= 2 &&
                (normalizedText.Contains(candidate.Normalized, StringComparison.Ordinal) ||
                 candidate.Normalized.Contains(normalizedText, StringComparison.Ordinal)))
            .ToArray();
        if (contains.Length == 1)
            return Resolved(rawText, camp, contains[0], .95, "contains", true, "unique contains camp-scoped match");
        if (contains.Length > 1)
            return Ambiguous(rawText, camp, .95, "ambiguous", contains.Select(item => item.Name));

        var shortCorrection = FindShortNameCorrection(candidates, normalizedText);
        if (shortCorrection.Length == 1)
            return Resolved(rawText, camp, shortCorrection[0], .92, "short-name-correction", true, "unique short Chinese OCR correction");
        if (shortCorrection.Length > 1)
            return Ambiguous(rawText, camp, .92, "ambiguous", shortCorrection.Select(item => item.Name));

        var scored = candidates
            .Select(candidate => new ScoredCandidate(
                candidate,
                _characterSimilarity.Similarity(candidate.Normalized, normalizedText)))
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Candidate.Name, StringComparer.Ordinal)
            .ToArray();

        var best = scored.FirstOrDefault();
        if (best != null)
        {
            var second = scored.Length > 1 ? scored[1].Score : 0;
            var gap = best.Score - second;
            if (best.Score >= CharacterMatchThreshold && gap >= ClearFuzzyGap)
            {
                var safe = best.Score >= SafeFuzzyThreshold;
                return Resolved(
                    rawText,
                    camp,
                    best.Candidate,
                    best.Score,
                    "jaro-winkler",
                    safe,
                    $"best score {best.Score:0.00}; second {second:0.00}; gap {gap:0.00}");
            }

            if (best.Score >= CharacterMatchThreshold)
                return Ambiguous(rawText, camp, best.Score, "ambiguous", scored.Take(3).Select(item => item.Candidate.Name));
        }

        return Unresolved(rawText, camp, best?.Score ?? 0, "unresolved", "no unique candidate above threshold");
    }

    /// <inheritdoc/>
    public string? ResolveCharacterName(string text, Camp camp) =>
        ResolveCharacterDetailed(text, camp).CanonicalName;

    /// <inheritdoc/>
    public Character? ResolveCharacter(string text, Camp camp) =>
        ResolveCharacterDetailed(text, camp).Character;

    /// <inheritdoc/>
    public async Task<CharacterResolveApplyResult> SelectSurvivorFromTextAsync(
        int playerIndex,
        string text,
        bool playAnimation = true,
        bool isRecordGlobalBan = true)
    {
        var result = ResolveCharacterDetailed(text, Camp.Sur);
        if (result.Character == null || !result.IsAutoApplySafe)
            return new(result, false, $"Not applied: {result.Reason}");

        await SelectSurvivorAsync(playerIndex, result.Character, playAnimation, isRecordGlobalBan);
        return new(result, true, $"Applied survivor[{playerIndex}] = {result.CanonicalName}.");
    }

    /// <inheritdoc/>
    public async Task<CharacterResolveApplyResult> SelectHunterFromTextAsync(
        string text,
        bool playAnimation = true,
        bool isRecordGlobalBan = true)
    {
        var result = ResolveCharacterDetailed(text, Camp.Hun);
        if (result.Character == null || !result.IsAutoApplySafe)
            return new(result, false, $"Not applied: {result.Reason}");

        await SelectHunterAsync(result.Character, playAnimation, isRecordGlobalBan);
        return new(result, true, $"Applied hunter = {result.CanonicalName}.");
    }

    /// <inheritdoc/>
    public async Task<CharacterResolveApplyResult> BanCharacterFromTextAsync(
        Camp camp,
        int index,
        string text,
        bool playAnimation = true)
    {
        var result = ResolveCharacterDetailed(text, camp);
        if (result.Character == null || !result.IsAutoApplySafe)
            return new(result, false, $"Not applied: {result.Reason}");

        await BanCharacterAsync(camp, index, result.Character, playAnimation);
        return new(result, true, $"Applied {camp} ban[{index}] = {result.CanonicalName}.");
    }

    /// <inheritdoc/>
    public async Task SelectSurvivorAsync(int playerIndex, Character? character, bool playAnimation = true, bool isRecordGlobalBan = true)
    {
        _logger.LogInformation(
            "CharacterSelection SelectSurvivorAsync requested: dispatcherAccess={DispatcherAccess}, playerIndex={PlayerIndex}, characterName={CharacterName}, playAnimation={PlayAnimation}",
            HasUiAccess(),
            playerIndex,
            GetCharacterName(character),
            playAnimation);
        await InvokeOnUiAsync(() => SelectSurvivorOnUiAsync(playerIndex, character, playAnimation, isRecordGlobalBan));
    }

    private async Task SelectSurvivorOnUiAsync(int playerIndex, Character? character, bool playAnimation, bool isRecordGlobalBan)
    {
        _logger.LogInformation(
            "CharacterSelection SelectSurvivorAsync executing: dispatcherAccess={DispatcherAccess}",
            HasUiAccess());
        if (!playAnimation)
        {
            CommitSurvivorPick(playerIndex, character, isRecordGlobalBan);
            return;
        }

        var oldCharacter = sharedDataService.CurrentGame.SurPlayerList[playerIndex].Character;
        await transitionOrchestrator.RunTransitionAsync(
            await CreateCharacterPickRequestAsync(Camp.Sur, playerIndex, oldCharacter, character),
            () =>
            {
                CommitSurvivorPick(playerIndex, character, isRecordGlobalBan);
                return Task.CompletedTask;
            });
    }

    /// <inheritdoc/>
    public async Task SelectHunterAsync(Character? character, bool playAnimation = true, bool isRecordGlobalBan = true)
    {
        _logger.LogInformation(
            "CharacterSelection SelectHunterAsync requested: dispatcherAccess={DispatcherAccess}, characterName={CharacterName}, playAnimation={PlayAnimation}",
            HasUiAccess(),
            GetCharacterName(character),
            playAnimation);
        await InvokeOnUiAsync(() => SelectHunterOnUiAsync(character, playAnimation, isRecordGlobalBan));
    }

    private async Task SelectHunterOnUiAsync(Character? character, bool playAnimation, bool isRecordGlobalBan)
    {
        _logger.LogInformation(
            "CharacterSelection SelectHunterAsync executing: dispatcherAccess={DispatcherAccess}",
            HasUiAccess());
        if (!playAnimation)
        {
            CommitHunterPick(character, isRecordGlobalBan);
            return;
        }

        var oldCharacter = sharedDataService.CurrentGame.HunPlayer.Character;
        await transitionOrchestrator.RunTransitionAsync(
            await CreateCharacterPickRequestAsync(Camp.Hun, -1, oldCharacter, character),
            () =>
            {
                CommitHunterPick(character, isRecordGlobalBan);
                return Task.CompletedTask;
            });
    }

    /// <inheritdoc/>
    public Task BanCharacterAsync(Camp camp, int index, Character? character, bool playAnimation = true)
    {
        _logger.LogInformation(
            "CharacterSelection BanCharacterAsync requested: dispatcherAccess={DispatcherAccess}, camp={Camp}, index={Index}, characterName={CharacterName}",
            HasUiAccess(),
            camp,
            index,
            GetCharacterName(character));
        return InvokeOnUiAsync(() => BanCharacterOnUi(camp, index, character));
    }

    private void BanCharacterOnUi(Camp camp, int index, Character? character)
    {
        _logger.LogInformation(
            "CharacterSelection BanCharacterAsync executing: dispatcherAccess={DispatcherAccess}",
            HasUiAccess());
        // 更新数据
        if (camp == Camp.Sur)
        {
            sharedDataService.CurrentGame.CurrentSurBannedList[index] = character;
            sharedDataService.CurrentGame.BpSlotCommitState.SurvivorBans[index] = character is null
                ? BpSlotCommitState.Pending
                : BpSlotCommitState.CommittedCharacter;
        }
        else
        {
            sharedDataService.CurrentGame.CurrentHunBannedList[index] = character;
            sharedDataService.CurrentGame.BpSlotCommitState.HunterBans[index] = character is null
                ? BpSlotCommitState.Pending
                : BpSlotCommitState.CommittedCharacter;
        }
        
        CharacterBanned?.Invoke(this, new CharacterBannedEventArgs(camp, index));
    }

    /// <inheritdoc/>
    public Task CommitEmptyBanAsync(Camp camp, int index, bool playAnimation = true) =>
        InvokeOnUiAsync(() => CommitEmptyBanOnUi(camp, index));

    private void CommitEmptyBanOnUi(Camp camp, int index)
    {
        if (camp == Camp.Sur)
        {
            sharedDataService.CurrentGame.CurrentSurBannedList[index] = null;
            sharedDataService.CurrentGame.BpSlotCommitState.SurvivorBans[index] = BpSlotCommitState.CommittedEmpty;
        }
        else
        {
            sharedDataService.CurrentGame.CurrentHunBannedList[index] = null;
            sharedDataService.CurrentGame.BpSlotCommitState.HunterBans[index] = BpSlotCommitState.CommittedEmpty;
        }

        CharacterBanned?.Invoke(this, new CharacterBannedEventArgs(camp, index));
    }

    /// <inheritdoc/>
    public Task CommitEmptySurvivorPickAsync(int playerIndex, bool playAnimation = true) =>
        InvokeOnUiAsync(() =>
        {
            CommitSurvivorPick(playerIndex, null, isRecordGlobalBan: true, BpSlotCommitState.CommittedEmpty);
        });

    /// <inheritdoc/>
    public Task CommitEmptyHunterPickAsync(bool playAnimation = true) =>
        InvokeOnUiAsync(() =>
        {
            CommitHunterPick(null, isRecordGlobalBan: true, BpSlotCommitState.CommittedEmpty);
        });

    /// <inheritdoc/>
    public async Task SwapSurvivorsAsync(int sourceIndex, int targetIndex, bool playAnimation = true)
    {
        _logger.LogInformation(
            "CharacterSelection SwapSurvivorsAsync requested: dispatcherAccess={DispatcherAccess}, source={Source}, target={Target}, playAnimation={PlayAnimation}",
            HasUiAccess(),
            sourceIndex,
            targetIndex,
            playAnimation);
        await InvokeOnUiAsync(() => SwapSurvivorsOnUiAsync(sourceIndex, targetIndex, playAnimation));
    }

    private async Task SwapSurvivorsOnUiAsync(int sourceIndex, int targetIndex, bool playAnimation)
    {
        _logger.LogInformation(
            "CharacterSelection SwapSurvivorsAsync executing: dispatcherAccess={DispatcherAccess}",
            HasUiAccess());
        if (!playAnimation)
        {
            CommitSurvivorSwap(sourceIndex, targetIndex);
            return;
        }

        var sourceGuid = await ResolveBpPickBehaviorGuidAsync(Camp.Sur, sourceIndex);
        var targetGuid = await ResolveBpPickBehaviorGuidAsync(Camp.Sur, targetIndex);
        var payload = new Dictionary<string, object?>();
        AddEventPayload(payload, "SourceIndex", sourceIndex);
        AddEventPayload(payload, "TargetIndex", targetIndex);
        AddEventPayload(payload, "SourceBehaviorGuid", sourceGuid);
        AddEventPayload(payload, "TargetBehaviorGuid", targetGuid);

        await transitionOrchestrator.RunMultiTargetTransitionAsync(
            [
                CreateSwapRequest(sourceIndex, sourceGuid, payload),
                CreateSwapRequest(targetIndex, targetGuid, payload)
            ],
            () =>
            {
                CommitSurvivorSwap(sourceIndex, targetIndex);
                return Task.CompletedTask;
            });
    }

    private void CommitSurvivorPick(
        int playerIndex,
        Character? character,
        bool isRecordGlobalBan,
        BpSlotCommitState? explicitCommitState = null)
    {
        _logger.LogInformation(
            "CharacterSelection CommitSurvivorPick: dispatcherAccess={DispatcherAccess}, playerIndex={PlayerIndex}, characterName={CharacterName}",
            HasUiAccess(),
            playerIndex,
            GetCharacterName(character));
        sharedDataService.CurrentGame.SurPlayerList[playerIndex].Character = character;
        sharedDataService.CurrentGame.BpSlotCommitState.SurvivorPicks[playerIndex] = explicitCommitState ??
            (character is null ? BpSlotCommitState.Pending : BpSlotCommitState.CommittedCharacter);
        if (isRecordGlobalBan && sharedDataService.CurrentGame.GameProgress is > GameProgress.Free and < GameProgress.Game4FirstHalf)
        {
            var targetIndex = (int)sharedDataService.CurrentGame.GameProgress / 2 * 4 + playerIndex;
            sharedDataService.CurrentGame.SurTeam.GlobalBannedSurRecordList[targetIndex] = character;
        }

        CharacterSelected?.Invoke(this, new CharacterSelectedEventArgs(Camp.Sur, playerIndex));
    }

    private void CommitHunterPick(
        Character? character,
        bool isRecordGlobalBan,
        BpSlotCommitState? explicitCommitState = null)
    {
        _logger.LogInformation(
            "CharacterSelection CommitHunterPick: dispatcherAccess={DispatcherAccess}, characterName={CharacterName}",
            HasUiAccess(),
            GetCharacterName(character));
        sharedDataService.CurrentGame.HunPlayer.Character = character;
        sharedDataService.CurrentGame.BpSlotCommitState.HunterPick = explicitCommitState ??
            (character is null ? BpSlotCommitState.Pending : BpSlotCommitState.CommittedCharacter);
        if (isRecordGlobalBan && sharedDataService.CurrentGame.GameProgress is > GameProgress.Free and < GameProgress.Game4FirstHalf)
        {
            var targetIndex = (int)sharedDataService.CurrentGame.GameProgress / 2;
            sharedDataService.CurrentGame.HunTeam.GlobalBannedHunRecordList[targetIndex] = character;
        }

        CharacterSelected?.Invoke(this, new CharacterSelectedEventArgs(Camp.Hun, -1));
    }

    private void CommitSurvivorSwap(int sourceIndex, int targetIndex)
    {
        _logger.LogInformation(
            "CharacterSelection CommitSurvivorSwap: dispatcherAccess={DispatcherAccess}, source={Source}, target={Target}",
            HasUiAccess(),
            sourceIndex,
            targetIndex);
        sharedDataService.CurrentGame.SwapCharactersInPlayers(sourceIndex, targetIndex);
        (sharedDataService.CurrentGame.BpSlotCommitState.SurvivorPicks[sourceIndex],
            sharedDataService.CurrentGame.BpSlotCommitState.SurvivorPicks[targetIndex]) =
            (sharedDataService.CurrentGame.BpSlotCommitState.SurvivorPicks[targetIndex],
                sharedDataService.CurrentGame.BpSlotCommitState.SurvivorPicks[sourceIndex]);
        CharacterSelected?.Invoke(this, new CharacterSelectedEventArgs(Camp.Sur, sourceIndex));
        CharacterSelected?.Invoke(this, new CharacterSelectedEventArgs(Camp.Sur, targetIndex));
    }

    private Task InvokeOnUiAsync(Action action)
    {
        if (_uiDispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return _uiDispatcher.InvokeAsync(action).Task;
    }

    private Task InvokeOnUiAsync(Func<Task> action)
    {
        if (_uiDispatcher.CheckAccess())
        {
            return action();
        }

        return _uiDispatcher.InvokeAsync(action).Task.Unwrap();
    }

    private bool HasUiAccess() => _uiDispatcher.CheckAccess();

    private async Task<FrontedTransitionRequest> CreateCharacterPickRequestAsync(
        Camp camp,
        int playerIndex,
        Character? oldCharacter,
        Character? newCharacter)
    {
        var targetGuid = await ResolveBpPickBehaviorGuidAsync(camp, playerIndex);
        var targetDisplayName = GetBpPickControlName(camp, playerIndex);
        var request = new FrontedTransitionRequest
        {
            WindowType = nameof(FrontedWindowType.BpWindow),
            TransitionType = "Selection.CharacterPick",
            TargetBehaviorGuid = targetGuid,
            TargetDisplayName = targetDisplayName
        };
        AddEventPayload(request.Payload, "Camp", camp.ToString());
        AddEventPayload(request.Payload, "PlayerIndex", playerIndex);
        AddEventPayload(request.Payload, "TargetBehaviorGuid", targetGuid);
        AddEventPayload(request.Payload, "OldCharacterName", GetCharacterName(oldCharacter));
        AddEventPayload(request.Payload, "NewCharacterName", GetCharacterName(newCharacter));
        // Legacy aliases remain readable by existing behavior graphs, but now carry the same canonical name.
        AddEventPayload(request.Payload, "OldCharacterId", GetCharacterName(oldCharacter));
        AddEventPayload(request.Payload, "NewCharacterId", GetCharacterName(newCharacter));
        AddEventPayload(request.Payload, "HasOldCharacter", HasCharacter(oldCharacter));
        AddEventPayload(request.Payload, "HasNewCharacter", HasCharacter(newCharacter));
        return request;
    }

    private static void AddEventPayload(IDictionary<string, object?> payload, string key, object? value)
    {
        payload[key] = value;
        payload[$"Event.{key}"] = value;
    }

    private static FrontedTransitionRequest CreateSwapRequest(
        int playerIndex,
        Guid targetGuid,
        Dictionary<string, object?> payload)
    {
        return new FrontedTransitionRequest
        {
            WindowType = nameof(FrontedWindowType.BpWindow),
            TransitionType = "Selection.CharacterSwap",
            TargetBehaviorGuid = targetGuid,
            TargetDisplayName = GetBpPickControlName(Camp.Sur, playerIndex),
            Payload = new Dictionary<string, object?>(payload)
        };
    }

    private async Task<Guid> ResolveBpPickBehaviorGuidAsync(Camp camp, int playerIndex)
    {
        var config = await layoutService.LoadWindowConfigAsync(nameof(FrontedWindowType.BpWindow));
        if (config?.ControlLayout.Controls.TryGetValue(GetBpPickControlName(camp, playerIndex), out var control) == true)
        {
            return control.BehaviorGuid;
        }

        return Guid.Empty;
    }

    private static string GetBpPickControlName(Camp camp, int playerIndex) =>
        camp == Camp.Sur ? $"SurPick{playerIndex}" : "HunPick";

    private static string? GetCharacterName(Character? character) =>
        HasCharacter(character) ? character!.Name : null;

    private static bool HasCharacter(Character? character) =>
        character is not null && !string.IsNullOrWhiteSpace(character.Name);

    private IEnumerable<CharacterCandidate> GetCandidates(Camp camp)
    {
        var values = camp == Camp.Sur
            ? sharedDataService.SurCharaDict.Values
            : sharedDataService.HunCharaDict.Values;
        return values
            .Where(HasCharacter)
            .Select(character => new CharacterCandidate(
                character,
                character.Name,
                NormalizeCharacterName(character.Name)))
            .Where(candidate => candidate.Normalized.Length > 0)
            .DistinctBy(candidate => candidate.Name, StringComparer.Ordinal);
    }

    private static CharacterResolveResult Resolved(
        string rawText,
        Camp camp,
        CharacterCandidate candidate,
        double score,
        string matchMode,
        bool isAutoApplySafe,
        string reason) =>
        new(
            rawText,
            camp,
            candidate.Character,
            candidate.Name,
            score,
            matchMode,
            isAutoApplySafe,
            reason);

    private static CharacterResolveResult Unresolved(
        string rawText,
        Camp camp,
        double score,
        string matchMode,
        string reason) =>
        new(rawText, camp, null, null, score, matchMode, false, reason);

    private static CharacterResolveResult Ambiguous(
        string rawText,
        Camp camp,
        double score,
        string matchMode,
        IEnumerable<string> candidates) =>
        new(rawText, camp, null, null, Math.Min(score, .89), matchMode, false, $"ambiguous candidates: {string.Join(", ", candidates)}");

    private static CharacterCandidate[] FindShortNameCorrection(
        IReadOnlyList<CharacterCandidate> candidates,
        string normalizedText)
    {
        if (normalizedText.Length is < 2 or > 3 || !normalizedText.All(IsCjk))
            return [];

        return candidates
            .Where(candidate => candidate.Normalized.Length == normalizedText.Length)
            .Where(candidate => candidate.Normalized.All(IsCjk))
            .Where(candidate => CharacterDistance(candidate.Normalized, normalizedText) == 1)
            .ToArray();
    }

    private static int CharacterDistance(string left, string right)
    {
        if (left.Length != right.Length)
            return int.MaxValue;
        var distance = 0;
        for (var i = 0; i < left.Length; i++)
            if (left[i] != right[i])
                distance++;
        return distance;
    }

    private static bool IsCjk(char value) =>
        value is >= '\u3400' and <= '\u9fff';

    private static string NormalizeCharacterName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var normalized = StripDecorativeQuotes(name.Normalize(NormalizationForm.FormKC).Trim()).ToLowerInvariant();
        return CharacterNameNoiseRegex().Replace(normalized, string.Empty);
    }

    private static string StripDecorativeQuotes(string value)
    {
        var trimmed = value.Trim();
        var changed = true;
        while (changed && trimmed.Length >= 2)
        {
            changed = false;
            foreach (var (left, right) in QuotePairs)
            {
                if (trimmed[0] != left || trimmed[^1] != right) continue;
                trimmed = trimmed[1..^1].Trim();
                changed = true;
                break;
            }
        }

        return trimmed;
    }

    private sealed record CharacterCandidate(Character Character, string Name, string Normalized);
    private sealed record ScoredCandidate(CharacterCandidate Candidate, double Score);

    private static readonly (char Left, char Right)[] QuotePairs =
    [
        ('"', '"'), ('“', '”'), ('”', '“'), ('『', '』'), ('「', '」'), ('《', '》'), ('〈', '〉'), ('‘', '’'), ('\'', '\'')
    ];

    [GeneratedRegex(@"[\s\p{P}\p{S}]+", RegexOptions.CultureInvariant)]
    private static partial Regex CharacterNameNoiseRegex();

    /// <inheritdoc/>
    public event EventHandler<CharacterBannedEventArgs>? CharacterBanned;
    
    /// <inheritdoc/>
    public event EventHandler<CharacterSelectedEventArgs>? CharacterSelected;
}
