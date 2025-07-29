namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 对局引导服务接口
/// </summary>
public interface IGameGuidanceService
{
    /// <summary>
    /// 是否启动引导
    /// </summary>
    bool IsGuidanceStarted { get; set; }
    /// <summary>
    /// 启动对局引导
    /// </summary>
    /// <returns></returns>
    Task<string?> StartGuidance();
    /// <summary>
    /// 下一步
    /// </summary>
    /// <returns></returns>
    Task<string> NextStepAsync();
    /// <summary>
    /// 上一步
    /// </summary>
    /// <returns></returns>
    Task<string> PrevStepAsync();
    /// <summary>
    /// 停止对局引导
    /// </summary>
    void StopGuidance();
}