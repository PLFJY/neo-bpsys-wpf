using System;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 全局重启服务
/// </summary>
/// <remarks>
/// 实现必须在 <see cref="IsRestartRequired"/> 的有效值发生改变时触发
/// <see cref="RestartRequiredStateChanged"/>；默认单例实现已保证状态与事件同步。
/// </remarks>
public interface IGlobalRestartService
{
    /// <summary>
    /// 是否需要重启
    /// </summary>
    bool IsRestartRequired { get; set; }

    /// <summary>
    /// 需要重启状态变化事件
    /// </summary>
    event EventHandler? RestartRequiredStateChanged;
}
