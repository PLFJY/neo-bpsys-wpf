using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>在权威角色状态提交后协调 BP 前台的角色过渡。</summary>
public interface ICharacterTransitionQueue
{
    /// <summary>在修改权威角色状态前初始化当前对局的展示快照。</summary>
    void EnsureCurrentGame();

    /// <summary>排入单槽角色变化。</summary>
    /// <param name="camp">角色阵营。</param>
    /// <param name="index">求生者槽位；监管者使用 -1。</param>
    /// <param name="oldCharacter">提交前的角色。</param>
    /// <param name="newCharacter">提交后的角色。</param>
    void EnqueuePick(Camp camp, int index, Character? oldCharacter, Character? newCharacter);

    /// <summary>排入双槽角色交换。</summary>
    /// <param name="source">来源求生者槽位。</param>
    /// <param name="target">目标求生者槽位。</param>
    /// <param name="sourceCharacter">交换前来源槽位的角色。</param>
    /// <param name="targetCharacter">交换前目标槽位的角色。</param>
    void EnqueueSwap(int source, int target, Character? sourceCharacter, Character? targetCharacter);

    /// <summary>取消当前局的展示队列并直接同步权威角色状态。</summary>
    void SynchronizeImmediately();

    /// <summary>批量提交手动同步操作，丢弃中间交换，并为最终槽位变化排入选角动画。</summary>
    /// <typeparam name="T">批量操作结果类型。</typeparam>
    /// <param name="applyAsync">通过角色服务提交识别结果的操作。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>批量操作结果；动画在后台继续执行。</returns>
    Task<T> RunManualSyncAsync<T>(Func<Task<T>> applyAsync, CancellationToken cancellationToken = default);
}
