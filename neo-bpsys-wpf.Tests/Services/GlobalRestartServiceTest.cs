using neo_bpsys_wpf.Services;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class GlobalRestartServiceTest
{
    [Fact]
    public void IsRestartRequired_WhenChanged_RaisesStateChanged()
    {
        var service = new GlobalRestartService();
        var eventCount = 0;
        service.RestartRequiredStateChanged += (_, _) => eventCount++;

        service.IsRestartRequired = true;
        service.IsRestartRequired = true;
        service.IsRestartRequired = false;

        Assert.False(service.IsRestartRequired);
        Assert.Equal(2, eventCount);
    }
}
