using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public class SmartBpServiceTest
{
    private readonly ITestOutputHelper _output;
    
    public SmartBpServiceTest(ITestOutputHelper output)
    {
        _output = output;
    }
    
    [Fact]
    public void GetHunPlayerNameAndCharacterName()
    {
        //const string path = @"F:\Videos\OBS\Screenshot OBS-2026-02-18 23-35-53.png";
        //if (!File.Exists(path)) throw new InvalidOperationException();

        //var bmp = new BitmapImage();
        //bmp.BeginInit();
        //bmp.CacheOption = BitmapCacheOption.OnLoad;
        //bmp.UriSource = new Uri(path, UriKind.Absolute);
        //bmp.EndInit();
        //bmp.Freeze();

        //var provider = new Mock<IWindowCaptureService>();
        //provider.Setup(p => p.GetCurrentFrame()).Returns(bmp);
        //var logger = NullLogger<SettingsHostService>.Instance;
        //var settingsHost = new SettingsHostService(NullLogger<SettingsHostService>.Instance); // 如果你项目里能 new
        //var ocrService = new OcrService(settingsHost);
        //ocrService.TrySwitchOcrModel("zh-cn-v5-mobile", out var err);
        //var smartBpService = new SmartBpService(new SharedDataService(settingsHost, NullLogger<SharedDataService>.Instance), provider.Object, ocrService);
        //var table = smartBpService.GetTable();

        //// === Step 2: 裁剪 Hunter 行 ===
        //var hunterRect = new SmartBpService.RelativeRect(0, 0, 1, 0.16)
        //    .ToPixelRect(table.Width, table.Height);
        //using var hunter = new Mat(table, hunterRect);
        //var name = smartBpService.GetHunPlayerNameAndCharacterName(hunter);
        //_output.WriteLine(name.Item1);
        //_output.WriteLine(name.Item2);
        //Assert.Equal("IsPLFJY", name.Item1);
        //Assert.Equal("蜘蛛", name.Item2);
    }
    
    [Fact]
    public void GetHunPlayerData()
    {
        //const string path = @"E:\_PersonalStuff\ASG\bpsys\neo-bpsys-wpf\neo-bpsys-wpf.Tests\bin\Debug\net9.0-windows10.0.20348\debug\03_hunter_row.png";
        //if (!File.Exists(path)) throw new InvalidOperationException();

        //var img = Cv2.ImRead(path);

        //var provider = new Mock<IWindowCaptureService>();
        //var settingsHost = new SettingsHostService(NullLogger<SettingsHostService>.Instance); // 如果你项目里能 new
        //var ocrService = new OcrService(settingsHost);
        //ocrService.TrySwitchOcrModel("zh-cn-v5-mobile", out var err);
        //var smartBpService = new SmartBpService(new SharedDataService(settingsHost, NullLogger<SharedDataService>.Instance), provider.Object, ocrService);
        
        //var data = smartBpService.GetHunPlayerData(img);
        //_output.WriteLine(data.RemainingCipher);
        //Assert.Equal("0", data.RemainingCipher);
        //_output.WriteLine(data.PalletsDestroyed);
        //Assert.Equal("7", data.PalletsDestroyed);
        //_output.WriteLine(data.SurvivorHits);
        //Assert.Equal("27", data.SurvivorHits);
        //_output.WriteLine(data.TerrorShocks);
        //Assert.Equal("1", data.TerrorShocks);
        //_output.WriteLine(data.Knockdowns);
        //Assert.Equal("11", data.Knockdowns);
    }

    [Fact]
    public void GetSurPlayerData()
    {
        //const string path = @"E:\_PersonalStuff\ASG\bpsys\neo-bpsys-wpf\neo-bpsys-wpf.Tests\bin\Debug\net9.0-windows10.0.20348\debug\02_table.png";
        //if (!File.Exists(path)) throw new InvalidOperationException();

        //var img = Cv2.ImRead(path);

        //var provider = new Mock<IWindowCaptureService>();
        //var settingsHost = new SettingsHostService(NullLogger<SettingsHostService>.Instance); // 如果你项目里能 new
        //var ocrService = new OcrService(settingsHost);
        //ocrService.TrySwitchOcrModel("zh-cn-v5-mobile", out var err);
        //var smartBpService = new SmartBpService(new SharedDataService(settingsHost, NullLogger<SharedDataService>.Instance), provider.Object, ocrService);
        //var infos = smartBpService.GetSurInfos(img);
        //foreach (var playerInfo in infos)
        //{
        //    _output.Write(playerInfo.PlayerName + " ");
        //    _output.Write(playerInfo.CharacterName+ " ");
        //    _output.Write(playerInfo.PlayerData.DecodingProgress+ " ");
        //    _output.Write(playerInfo.PlayerData.PalletStrikes+ " ");
        //    _output.Write(playerInfo.PlayerData.Rescues+ " ");
        //    _output.Write(playerInfo.PlayerData.Heals+ " ");
        //    _output.WriteLine(playerInfo.PlayerData.ContainmentTime);
        //}
    }
    
    [Fact]
    public void GetFistSurRow()
    {
        //const string path = @"E:\_PersonalStuff\ASG\bpsys\neo-bpsys-wpf\neo-bpsys-wpf.Tests\bin\Debug\net9.0-windows10.0.20348\debug\03_sur_first_data_row.png";
        //if (!File.Exists(path)) throw new InvalidOperationException();

        //var img = Cv2.ImRead(path);

        //var provider = new Mock<IWindowCaptureService>();
        //var settingsHost = new SettingsHostService(NullLogger<SettingsHostService>.Instance); // 如果你项目里能 new
        //var ocrService = new OcrService(settingsHost);
        //ocrService.TrySwitchOcrModel("zh-cn-v5-mobile", out var err);
        //var smartBpService = new SmartBpService(new SharedDataService(settingsHost, NullLogger<SharedDataService>.Instance), provider.Object, ocrService);
        //_output.WriteLine(smartBpService.GetSurPlayerNameAndCharacterName(img).Item1);
        //_output.WriteLine(smartBpService.GetSurPlayerNameAndCharacterName(img).Item2);
    }
}
