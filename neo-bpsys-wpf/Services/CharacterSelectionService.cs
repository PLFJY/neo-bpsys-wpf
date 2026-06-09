using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Core.Models;
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
    private const int TransitionDelayMs = 250;

    private IAnimationService? _animationService;

    /// <summary>
    /// 延迟解析 <see cref="IAnimationService"/>。
    /// 仅在 <c>playAnimation == true</c> 时访问，避免启动时过早构造 <see cref="IFrontedWindowService"/>。
    /// </summary>
    private IAnimationService AnimationService =>
        _animationService ??= serviceProvider.GetRequiredService<IAnimationService>();

    /// <inheritdoc/>
    public async Task SelectSurvivorAsync(int playerIndex, Character? character, bool playAnimation = true, bool isRecordGlobalBan = true)
    {
        if (playAnimation)
        {
            AnimationService.PlayPickFadeOut(Camp.Sur, playerIndex);
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
            AnimationService.PlayPickFadeIn(Camp.Sur, playerIndex);
        }
    }

    /// <inheritdoc/>
    public async Task SelectHunterAsync(Character? character, bool playAnimation = true, bool isRecordGlobalBan = true)
    {
        if (playAnimation)
        {
            AnimationService.PlayPickFadeOut(Camp.Hun, -1);
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
            AnimationService.PlayPickFadeIn(Camp.Hun, -1);
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