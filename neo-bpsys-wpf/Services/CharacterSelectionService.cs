using F23.StringSimilarity;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using System.Diagnostics;
using System.Text;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 角色选择服务的默认实现。
/// </summary>
public class CharacterSelectionService(
    ISharedDataService sharedDataService,
    IFrontedTransitionOrchestrator transitionOrchestrator,
    IFrontedLayoutService layoutService)
    : ICharacterSelectionService
{
    private const double CharacterMatchThreshold = 0.70;
    private readonly JaroWinkler _characterSimilarity = new();

#if DEBUG
    static CharacterSelectionService()
    {
        Debug.WriteLine($"[DIAG] CharacterSelectionService: static ctor at {DateTimeOffset.Now:HH:mm:ss.fff}");
    }
#endif
    /// <inheritdoc/>
    public Character? ResolveCharacter(string text, Camp camp)
    {
        var normalizedText = NormalizeCharacterName(text);
        if (string.IsNullOrEmpty(normalizedText))
            return null;

        var candidates = camp == Camp.Sur
            ? sharedDataService.SurCharaDict.Values
            : sharedDataService.HunCharaDict.Values;

        return candidates
            .Select(character => new
            {
                Character = character,
                Score = _characterSimilarity.Similarity(
                    NormalizeCharacterName(character.Name),
                    normalizedText)
            })
            .Where(candidate => candidate.Score >= CharacterMatchThreshold)
            .OrderByDescending(candidate => candidate.Score)
            .FirstOrDefault()
            ?.Character;
    }

    /// <inheritdoc/>
    public async Task SelectSurvivorAsync(int playerIndex, Character? character, bool playAnimation = true, bool isRecordGlobalBan = true)
    {
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
        // 更新数据
        if (camp == Camp.Sur)
            sharedDataService.CurrentGame.CurrentSurBannedList[index] = character;
        else
            sharedDataService.CurrentGame.CurrentHunBannedList[index] = character;
        
        CharacterBanned?.Invoke(this, new CharacterBannedEventArgs(camp, index));

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task SwapSurvivorsAsync(int sourceIndex, int targetIndex, bool playAnimation = true)
    {
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

    private void CommitSurvivorPick(int playerIndex, Character? character, bool isRecordGlobalBan)
    {
        sharedDataService.CurrentGame.SurPlayerList[playerIndex].Character = character;
        if (isRecordGlobalBan && sharedDataService.CurrentGame.GameProgress is > GameProgress.Free and < GameProgress.Game4FirstHalf)
        {
            var targetIndex = (int)sharedDataService.CurrentGame.GameProgress / 2 * 4 + playerIndex;
            sharedDataService.CurrentGame.SurTeam.GlobalBannedSurRecordList[targetIndex] = character;
        }

        CharacterSelected?.Invoke(this, new CharacterSelectedEventArgs(Camp.Sur, playerIndex));
    }

    private void CommitHunterPick(Character? character, bool isRecordGlobalBan)
    {
        sharedDataService.CurrentGame.HunPlayer.Character = character;
        if (isRecordGlobalBan && sharedDataService.CurrentGame.GameProgress is > GameProgress.Free and < GameProgress.Game4FirstHalf)
        {
            var targetIndex = (int)sharedDataService.CurrentGame.GameProgress / 2;
            sharedDataService.CurrentGame.HunTeam.GlobalBannedHunRecordList[targetIndex] = character;
        }

        CharacterSelected?.Invoke(this, new CharacterSelectedEventArgs(Camp.Hun, -1));
    }

    private void CommitSurvivorSwap(int sourceIndex, int targetIndex)
    {
        sharedDataService.CurrentGame.SwapCharactersInPlayers(sourceIndex, targetIndex);
        CharacterSelected?.Invoke(this, new CharacterSelectedEventArgs(Camp.Sur, sourceIndex));
        CharacterSelected?.Invoke(this, new CharacterSelectedEventArgs(Camp.Sur, targetIndex));
    }

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
        AddEventPayload(request.Payload, "OldCharacterId", GetCharacterId(oldCharacter));
        AddEventPayload(request.Payload, "NewCharacterId", GetCharacterId(newCharacter));
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

    private static string? GetCharacterId(Character? character) =>
        HasCharacter(character)
            ? !string.IsNullOrWhiteSpace(character!.ImageFileName) ? character.ImageFileName : character.Name
            : null;

    private static bool HasCharacter(Character? character) =>
        character is not null && !string.IsNullOrWhiteSpace(character.Name);

    private static string NormalizeCharacterName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var normalized = name.Normalize(NormalizationForm.FormKC).Trim().ToLowerInvariant();
        return new string(normalized.Where(char.IsLetterOrDigit).ToArray());
    }

    /// <inheritdoc/>
    public event EventHandler<CharacterBannedEventArgs>? CharacterBanned;
    
    /// <inheritdoc/>
    public event EventHandler<CharacterSelectedEventArgs>? CharacterSelected;
}
