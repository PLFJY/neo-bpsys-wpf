using System.Threading;
using System.Threading.Tasks;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 智能 BP 服务
/// </summary>
public interface ISmartBpService
{
    bool IsSmartBpRunning { get; }

    void StartSmartBp();

    void StopSmartBp();

    Task AutoFillGameDataAsync(CancellationToken cancellationToken = default);
}
