using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 角色选择服务的默认实现
/// </summary>
public class CharacterSelectionService : ICharacterSelectionService
{
    private readonly ISharedDataService _sharedDataService;
    private const int TransitionDelayMs = 250;

    /// <inheritdoc/>
    public IAnimationService AnimationService { get; }

    public CharacterSelectionService(
        ISharedDataService sharedDataService,
        IAnimationService animationService)
    {
        _sharedDataService = sharedDataService;
        AnimationService = animationService;
    }

    /// <inheritdoc/>
    public async Task SelectSurvivorAsync(int playerIndex, Character? character, bool playAnimation = true)
    {
        if (playAnimation)
        {
            AnimationService.PlayPickFadeOut(Camp.Sur, playerIndex);
            await Task.Delay(TransitionDelayMs);
        }

        _sharedDataService.CurrentGame.SurPlayerList[playerIndex].Character = character;

        if (playAnimation)
        {
            AnimationService.PlayPickFadeIn(Camp.Sur, playerIndex);
        }
    }

    /// <inheritdoc/>
    public async Task SelectHunterAsync(Character? character, bool playAnimation = true)
    {
        if (playAnimation)
        {
            AnimationService.PlayPickFadeOut(Camp.Hun, -1);
            await Task.Delay(TransitionDelayMs);
        }

        _sharedDataService.CurrentGame.HunPlayer.Character = character;

        if (playAnimation)
        {
            AnimationService.PlayPickFadeIn(Camp.Hun, -1);
        }
    }

    /// <inheritdoc/>
    public async Task BanCharacterAsync(Camp camp, int index, Character? character, bool playAnimation = true)
    {
        // 更新数据
        if (camp == Camp.Sur)
            _sharedDataService.CurrentGame.CurrentSurBannedList[index] = character;
        else
            _sharedDataService.CurrentGame.CurrentHunBannedList[index] = character;
        
        if (playAnimation)
        {
            await AnimationService.PlayBanAnimationAsync(camp, index);
        }
    }

    /// <inheritdoc/>
    public async Task SwapSurvivorsAsync(int sourceIndex, int targetIndex, bool playAnimation = true)
    {
        if (playAnimation)
        {
            await AnimationService.PlaySwapCharacterAnimationAsync(sourceIndex, targetIndex);
        }

        // 在动画完成后（或不播放动画时）执行数据交换
        // 注意：动画中间已经等待了，所以交换在动画淡入前完成
        _sharedDataService.CurrentGame.SwapCharactersInPlayers(sourceIndex, targetIndex);
    }

    /// <inheritdoc/>
    public async Task StartPickingIndicatorAsync(Camp camp, int index)
    {
        await AnimationService.StartPickingBorderBreathingAsync(camp, index);
    }

    /// <inheritdoc/>
    public async Task StopPickingIndicatorAsync(Camp camp, int index)
    {
        await AnimationService.StopPickingBorderBreathingAsync(camp, index);
    }
}
