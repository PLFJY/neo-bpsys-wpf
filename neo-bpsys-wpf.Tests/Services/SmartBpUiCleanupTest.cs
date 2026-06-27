extern alias smartbp;

using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
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

    [Fact]
    public void SmartBpPageUsesHiddenVisibilityForRecognizingIndicator()
    {
        var xaml = ReadRepoFile("neo-bpsys-wpf.SmartBp.Module/Views/SmartBpModuleContentView.xaml");

        Assert.Contains("BooleanToHiddenVisibilityConverter", xaml);
        Assert.Contains("Visibility=\"{Binding IsAiRecognizing, Converter={StaticResource BooleanToHiddenVisibilityConverter}}\"", xaml);
    }

    [Fact]
    public void RecognitionAreaConfigurationCardOwnsNormalRegionEditors()
    {
        var xaml = ReadRepoFile("neo-bpsys-wpf.SmartBp.Module/Views/SmartBpModuleContentView.xaml");

        var recognitionCardIndex = xaml.IndexOf("SmartBpRecognitionAreaConfiguration", StringComparison.Ordinal);
        var debugIndex = xaml.IndexOf("SmartBpDebugOptions", StringComparison.Ordinal);
        var bpEditorIndex = xaml.IndexOf("OpenAiRecognitionRegionEditorCommand", StringComparison.Ordinal);
        var gameDataEditorIndex = xaml.IndexOf("OpenGameDataRegionEditorCommand", StringComparison.Ordinal);
        var importIndex = xaml.IndexOf("ImportGameDataRegionConfigCommand", StringComparison.Ordinal);
        var exportIndex = xaml.IndexOf("ExportGameDataRegionConfigCommand", StringComparison.Ordinal);
        var resetIndex = xaml.IndexOf("ResetGameDataRegionConfigCommand", StringComparison.Ordinal);

        Assert.True(recognitionCardIndex >= 0);
        Assert.True(debugIndex > recognitionCardIndex);
        Assert.InRange(bpEditorIndex, recognitionCardIndex, debugIndex);
        Assert.InRange(gameDataEditorIndex, recognitionCardIndex, debugIndex);
        Assert.InRange(importIndex, recognitionCardIndex, debugIndex);
        Assert.InRange(exportIndex, recognitionCardIndex, debugIndex);
        Assert.InRange(resetIndex, recognitionCardIndex, debugIndex);
    }

    [Fact]
    public void MainAutoApplyCopyDoesNotUseCautionWording()
    {
        var value = ReadResxValue("neo-bpsys-wpf/Locales/Lang.resx", "SmartBpAiEnableAutoApply");

        Assert.DoesNotContain("慎用", value);
        Assert.DoesNotContain("谨慎", value);
        Assert.Equal("自动应用识别结果", value);
    }

    [Fact]
    public void PaddleOcrRecommendedMarkerIsLocalized()
    {
        var value = ReadResxValue("neo-bpsys-wpf/Locales/Lang.resx", "SmartBpRecommendedProviderFormat");

        Assert.Contains("推荐", value);
    }

    private static string ReadResxValue(string relativePath, string key, [CallerFilePath] string sourceFilePath = "")
    {
        var document = XDocument.Parse(ReadRepoFile(relativePath, sourceFilePath));
        return document.Root?
            .Elements("data")
            .FirstOrDefault(element => string.Equals((string?)element.Attribute("name"), key, StringComparison.Ordinal))?
            .Element("value")?
            .Value
            ?? throw new InvalidDataException($"Resource key '{key}' was not found in '{relativePath}'.");
    }

    private static string ReadRepoFile(string relativePath, [CallerFilePath] string sourceFilePath = "")
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "..", ".."));
        return File.ReadAllText(Path.Combine(repositoryRoot, relativePath));
    }
}
