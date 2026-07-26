using System;
using System.Collections.Generic;
using System.Text;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 全局重启服务
/// </summary>
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
