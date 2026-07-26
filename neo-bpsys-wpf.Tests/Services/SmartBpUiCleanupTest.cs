extern alias smartbp;

using Xunit;
using SmartBpRecognitionSettings = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognitionSettings;

namespace neo_bpsys_wpf.Tests.Services;

public class SmartBpUiCleanupTest
{
    [Fact]
    public void DefaultOcrRecognitionIntervalIsProductionCadence()
    {
        var settings = new SmartBpRecognitionSettings();

        Assert.Equal(3000, settings.OcrRecognitionIntervalMs);
    }

}
