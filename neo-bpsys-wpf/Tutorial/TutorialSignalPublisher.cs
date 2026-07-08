using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.ProductTour;

namespace neo_bpsys_wpf.Tutorial;

internal static class TutorialSignalPublisher
{
    internal static void Publish(string signalId, object? payload = null)
    {
        IAppHost.Host?.Services.GetService<ITutorialSignalService>()?.Publish(signalId, payload);
    }
}
