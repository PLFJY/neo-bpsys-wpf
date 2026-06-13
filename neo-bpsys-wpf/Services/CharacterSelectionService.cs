using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using System.Diagnostics;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 角色选择服务的默认实现。
/// 通过 <see cref="IServiceProvider"/> 延迟解析 <see cref="IAnimationService"/>，
/// 避免在启动时触发 <see cref="IAnimationService"/> → <see cref="IFrontedWindowService"/> 的构造链。
/// </summary>
public class CharacterSelectionService(
    ISharedDataService sharedDataService,
    IServiceProvider serviceProvider)
    : ICharacterSelectionService
{
#if DEBUG
    static CharacterSelectionService()
    {
        Debug.WriteLine($"[DIAG] CharacterSelectionService: static ctor at {DateTimeOffset.Now:HH:mm:ss.fff}");
    }
#endif
    private IAnimationService? _animationService;
    private IFrontedTransitionOrchestrator? _transitionOrchestrator;
    private IFrontedLayoutService? _layoutService;

    /// <summary>
    /// 延迟解析 <see cref="IAnimationService"/>。
    /// 仅在 <c>playAnimation == true</c> 时访问，避免启动时过早构造 <see cref="IFrontedWindowService"/>。
    /// </summary>
    private IAnimationService AnimationService =>
        _animationService ??= serviceProvider.GetRequiredService<IAnimationService>();

    private IFrontedTransitionOrchestrator TransitionOrchestrator =>
        _transitionOrchestrator ??= serviceProvider.GetRequiredService<IFrontedTransitionOrchestrator>();

    private IFrontedLayoutService LayoutService =>
        _layoutService ??= serviceProvider.GetRequiredService<IFrontedLayoutService>();

    /// <inheritdoc/>
    public async Task SelectSurvivorAsync(int playerIndex, Character? character, bool playAnimation = true, bool isRecordGlobalBan = true)
    {
        if (!playAnimation)
        {
            CommitSurvivorPick(playerIndex, character, isRecordGlobalBan);
            return;
        }

        var oldCharacter = sharedDataService.CurrentGame.SurPlayerList[playerIndex].Character;
        await TransitionOrchestrator.RunTransitionAsync(
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
        await TransitionOrchestrator.RunTransitionAsync(
            await CreateCharacterPickRequestAsync(Camp.Hun, -1, oldCharacter, character),
            () =>
            {
                CommitHunterPick(character, isRecordGlobalBan);
                return Task.CompletedTask;
            });
    }

    /// <inheritdoc/>
    public async Task BanCharacterAsync(Camp camp, int index, Character? character, bool playAnimation = true)
    {
        // 更新数据
        if (camp == Camp.Sur)
            sharedDataService.CurrentGame.CurrentSurBannedList[index] = character;
        else
            sharedDataService.CurrentGame.CurrentHunBannedList[index] = character;
        
        CharacterBanned?.Invoke(this, new CharacterBannedEventArgs(camp, index));

        if (playAnimation)
        {
            await AnimationService.PlayBanAnimationAsync(camp, index);
        }
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
        var payload = new Dictionary<string, object?>
        {
            ["Event.SourceIndex"] = sourceIndex,
            ["Event.TargetIndex"] = targetIndex,
            ["Event.SourceBehaviorGuid"] = sourceGuid,
            ["Event.TargetBehaviorGuid"] = targetGuid
        };

        await TransitionOrchestrator.RunMultiTargetTransitionAsync(
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
        return new FrontedTransitionRequest
        {
            WindowType = nameof(FrontedWindowType.BpWindow),
            TransitionType = "Selection.CharacterPick",
            TargetBehaviorGuid = targetGuid,
            TargetDisplayName = targetDisplayName,
            Payload =
            {
                ["Event.Camp"] = camp.ToString(),
                ["Event.PlayerIndex"] = playerIndex,
                ["Event.TargetBehaviorGuid"] = targetGuid,
                ["Event.OldCharacterId"] = GetCharacterId(oldCharacter),
                ["Event.NewCharacterId"] = GetCharacterId(newCharacter),
                ["Event.HasOldCharacter"] = HasCharacter(oldCharacter),
                ["Event.HasNewCharacter"] = HasCharacter(newCharacter)
            }
        };
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
        var config = await LayoutService.LoadWindowConfigAsync(nameof(FrontedWindowType.BpWindow));
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

    /// <inheritdoc/>
    public event EventHandler<CharacterBannedEventArgs>? CharacterBanned;
    
    /// <inheritdoc/>
    public event EventHandler<CharacterSelectedEventArgs>? CharacterSelected;
}
