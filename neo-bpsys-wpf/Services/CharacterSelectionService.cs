using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 角色选择服务的默认实现
/// </summary>
public class CharacterSelectionService(
    ISharedDataService sharedDataService,
    IAnimationService animationService)
    : ICharacterSelectionService
{
    private const int TransitionDelayMs = 250;

    private readonly IAnimationService _animationService = animationService;

    /// <inheritdoc/>
    public async Task SelectSurvivorAsync(int playerIndex, Character? character, bool playAnimation = true, bool isRecordGlobalBan = true)
    {
        if (playAnimation)
        {
            _animationService.PlayPickFadeOut(Camp.Sur, playerIndex);
            await Task.Delay(TransitionDelayMs);
        }

        sharedDataService.CurrentGame.SurPlayerList[playerIndex].Character = character;
        if (isRecordGlobalBan && sharedDataService.CurrentGame.GameProgress is > GameProgress.Free and < GameProgress.Game4FirstHalf)
        {
            var targetIndex = (int)sharedDataService.CurrentGame.GameProgress / 2 * 4 + playerIndex;
            sharedDataService.CurrentGame.SurTeam.GlobalBannedSurRecordList[targetIndex] = character;
        }
        
        CharacterSelected?.Invoke(this, new CharacterSelectedEventArgs(Camp.Sur, playerIndex));
        
        if (playAnimation)
        {
            _animationService.PlayPickFadeIn(Camp.Sur, playerIndex);
        }
    }

    /// <inheritdoc/>
    public async Task SelectHunterAsync(Character? character, bool playAnimation = true, bool isRecordGlobalBan = true)
    {
        if (playAnimation)
        {
            _animationService.PlayPickFadeOut(Camp.Hun, -1);
            await Task.Delay(TransitionDelayMs);
        }

        sharedDataService.CurrentGame.HunPlayer.Character = character;
        if (isRecordGlobalBan && sharedDataService.CurrentGame.GameProgress is > GameProgress.Free and < GameProgress.Game4FirstHalf)
        {
            var targetIndex = (int)sharedDataService.CurrentGame.GameProgress / 2;
            sharedDataService.CurrentGame.HunTeam.GlobalBannedHunRecordList[targetIndex] = character;
        }
        
        CharacterSelected?.Invoke(this, new CharacterSelectedEventArgs(Camp.Hun, -1));

        if (playAnimation)
        {
            _animationService.PlayPickFadeIn(Camp.Hun, -1);
        }
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
            await _animationService.PlayBanAnimationAsync(camp, index);
        }
    }

    /// <inheritdoc/>
    public async Task SwapSurvivorsAsync(int sourceIndex, int targetIndex, bool playAnimation = true)
    {
        if (playAnimation)
        {
            await _animationService.PlaySwapCharacterAnimationAsync(sourceIndex, targetIndex);
        }
        
        // 在动画完成后（或不播放动画时）执行数据交换
        // 注意：动画中间已经等待了250ms，所以交换在动画淡入前完成
        sharedDataService.CurrentGame.SwapCharactersInPlayers(sourceIndex, targetIndex);
        CharacterSelected?.Invoke(this, new CharacterSelectedEventArgs(Camp.Sur, sourceIndex));
        CharacterSelected?.Invoke(this, new CharacterSelectedEventArgs(Camp.Sur, targetIndex));
    }

    /// <inheritdoc/>
    public event EventHandler<CharacterBannedEventArgs>? CharacterBanned;
    
    /// <inheritdoc/>
    public event EventHandler<CharacterSelectedEventArgs>? CharacterSelected;
}