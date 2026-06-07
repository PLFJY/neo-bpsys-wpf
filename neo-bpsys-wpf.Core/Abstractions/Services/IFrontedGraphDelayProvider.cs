namespace neo_bpsys_wpf.Core.Abstractions.Services;

public interface IFrontedGraphDelayProvider
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}
