using System.Threading;
using System.Threading.Tasks;

namespace neo_bpsys_wpf.Core.Models.SmartBpModule;

/// <summary>
/// 描述已加载模块所暴露的 SmartBP 功能命令。
/// </summary>
/// <param name="CommandId">稳定的命令标识符。</param>
/// <param name="DisplayNameKey">显示名称的本地化键。</param>
/// <param name="ExecuteAsync">命令执行委托。</param>
public sealed record SmartBpFeatureCommand(
    string CommandId,
    string DisplayNameKey,
    Func<CancellationToken, Task> ExecuteAsync);
