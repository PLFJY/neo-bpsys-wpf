using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Services;
using System;
using System.IO;
using System.Windows.Media.Imaging;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public class SmartBpServiceTest
{

    [Fact]
    public void FillGameData()
    {
        const string path = @"F:\Videos\OBS\Screenshot OBS-2026-02-18 23-35-53.png";
        if (!File.Exists(path)) throw new InvalidOperationException();

        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad; 
        bmp.UriSource = new Uri(path, UriKind.Absolute);
        bmp.EndInit();
        bmp.Freeze();

        var provider = new Mock<IWindowCaptureService>();
        provider.Setup(p => p.GetCurrentFrame()).Returns(bmp);
        var logger = NullLogger<SettingsHostService>.Instance;
        var smartBpService = new SmartBpService(provider.Object, new OcrService(new SettingsHostService(logger)));

        smartBpService.DebugExtractGameData();
    }
}