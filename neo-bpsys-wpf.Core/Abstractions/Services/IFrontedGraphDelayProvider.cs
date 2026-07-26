namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 前台行为图延迟提供器接口，用于在行为图执行过程中引入可控的等待。
/// </summary>
public interface IFrontedGraphDelayProvider
{
    /// <summary>
    /// 执行指定时间长度的异步延迟。
    /// </summary>
    /// <param name="delay">延迟时间长度。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}
