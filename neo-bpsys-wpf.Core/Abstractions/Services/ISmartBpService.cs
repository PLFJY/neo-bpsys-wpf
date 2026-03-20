using System.Threading;
using System.Threading.Tasks;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 智能 BP 服务
/// </summary>
public interface ISmartBpService
{
    /// <summary>
    /// 当前智能 BP 是否处于运行状态。
    /// </summary>
    bool IsSmartBpRunning { get; }

    /// <summary>
    /// 启动智能 BP。
    /// 启动时会检查 OCR 模型可用性，不满足条件时不会进入运行状态。
    /// </summary>
    void StartSmartBp();

    /// <summary>
    /// 停止智能 BP。
    /// </summary>
    void StopSmartBp();

    /// <summary>
    /// 从当前捕获画面识别赛后数据并自动回填到当前对局数据。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>异步任务。</returns>
    Task AutoFillGameDataAsync(CancellationToken cancellationToken = default);
}
