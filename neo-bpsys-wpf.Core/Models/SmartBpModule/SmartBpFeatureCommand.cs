using System.Threading;
using System.Threading.Tasks;

namespace neo_bpsys_wpf.Core.Models.SmartBpModule;

/// <summary>
/// Describes a SmartBP feature command exposed by a loaded module.
/// </summary>
/// <param name="CommandId">Stable command identifier.</param>
/// <param name="DisplayNameKey">Display name localization key.</param>
/// <param name="ExecuteAsync">Command execution delegate.</param>
public sealed record SmartBpFeatureCommand(
    string CommandId,
    string DisplayNameKey,
    Func<CancellationToken, Task> ExecuteAsync);
