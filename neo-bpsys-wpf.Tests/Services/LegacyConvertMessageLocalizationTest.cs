using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System;
using System.Collections.Generic;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class LegacyConvertMessageLocalizationTest : IDisposable
{
    private readonly Func<string, string>? _previousLocalizeTemplate;

    public LegacyConvertMessageLocalizationTest()
    {
        _previousLocalizeTemplate = LegacyConvertMessageHelper.LocalizeTemplate;
        LegacyConvertMessageHelper.LocalizeTemplate = null;
    }

    public void Dispose()
    {
        LegacyConvertMessageHelper.LocalizeTemplate = _previousLocalizeTemplate;
    }

    [Fact]
    public void MapBpV1Skipped_HasCorrectCodeAndSeverity()
    {
        var message = LegacyConvertMessageHelper.Compat(
            LegacyConvertMessageHelper.CodeMapBpV1Skipped,
            LegacyConvertMessageHelper.Args(new { SourceWindow = "WidgetsWindow", SourceCanvas = "MapBpCanvas" }));

        Assert.Equal(LegacyConvertMessageHelper.CodeMapBpV1Skipped, message.Code);
        Assert.Equal(FrontedLayoutPackageLegacyConvertMessageSeverity.CompatibilityNote, message.Severity);
        Assert.NotEmpty(message.Message);
        Assert.DoesNotContain("error", message.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MapBpV1Skipped_ChineseLocalization()
    {
        // Set up a simple dictionary-based localizer for testing.
        LegacyConvertMessageHelper.LocalizeTemplate = key => key switch
        {
            LegacyConvertMessageHelper.CodeMapBpV1Skipped =>
                "旧版\"地图 BP V1\"窗口已在 Designer v3 中移除，因此不会转换。其他支持的窗口会继续转换。",
            _ => key
        };

        try
        {
            var message = LegacyConvertMessageHelper.Compat(
                LegacyConvertMessageHelper.CodeMapBpV1Skipped,
                LegacyConvertMessageHelper.Args(new { SourceWindow = "WidgetsWindow", SourceCanvas = "MapBpCanvas" }));

            Assert.Contains("旧版", message.Message);
            Assert.Contains("地图 BP V1", message.Message);
            Assert.Contains("Designer v3", message.Message);
            Assert.DoesNotContain("error", message.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("错误", message.Message, StringComparison.Ordinal);
        }
        finally
        {
            LegacyConvertMessageHelper.LocalizeTemplate = null;
        }
    }

    [Fact]
    public void MapBpV1Skipped_EnglishLocalization()
    {
        LegacyConvertMessageHelper.LocalizeTemplate = key => key switch
        {
            LegacyConvertMessageHelper.CodeMapBpV1Skipped =>
                "The legacy \"Map BP V1\" window has been removed in Designer v3, so it will not be converted. Other supported windows will continue to be converted.",
            _ => key
        };

        try
        {
            var message = LegacyConvertMessageHelper.Compat(
                LegacyConvertMessageHelper.CodeMapBpV1Skipped,
                LegacyConvertMessageHelper.Args(new { SourceWindow = "WidgetsWindow", SourceCanvas = "MapBpCanvas" }));

            Assert.Contains("Map BP V1", message.Message);
            Assert.Contains("Designer v3", message.Message);
            Assert.Contains("removed", message.Message);
            Assert.DoesNotContain("error", message.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            LegacyConvertMessageHelper.LocalizeTemplate = null;
        }
    }

    [Fact]
    public void UnknownLayoutFileSkipped_HasWarningSeverity()
    {
        var message = LegacyConvertMessageHelper.Warning(
            LegacyConvertMessageHelper.CodeUnknownLayoutFileSkipped,
            LegacyConvertMessageHelper.Args(new { FileName = "test.json" }));

        Assert.Equal(LegacyConvertMessageHelper.CodeUnknownLayoutFileSkipped, message.Code);
        Assert.Equal(FrontedLayoutPackageLegacyConvertMessageSeverity.Warning, message.Severity);
        Assert.Contains("test.json", message.Message);
    }

    [Fact]
    public void ControlNotInBlueprintMap_IsError()
    {
        var message = LegacyConvertMessageHelper.Error(
            LegacyConvertMessageHelper.CodeControlNotInBlueprintMap,
            LegacyConvertMessageHelper.Args(new { SourceWindow = "BpWindow", SourceCanvas = "BaseCanvas", ControlName = "UnknownCtrl" }));

        Assert.Equal(FrontedLayoutPackageLegacyConvertMessageSeverity.Error, message.Severity);
        Assert.Contains("BpWindow", message.Message);
        Assert.Contains("UnknownCtrl", message.Message);
    }

    [Fact]
    public void ResourceMissing_IsCompatibilityNote()
    {
        var message = LegacyConvertMessageHelper.Info(
            LegacyConvertMessageHelper.CodeResourceMissing,
            LegacyConvertMessageHelper.Args(new { Field = "TestField", Value = "test.png" }));

        Assert.Equal(FrontedLayoutPackageLegacyConvertMessageSeverity.Info, message.Severity);
        Assert.Contains("TestField", message.Message);
        Assert.Contains("test.png", message.Message);
    }

    [Fact]
    public void FrontElementsFolderMissing_IsError()
    {
        var message = LegacyConvertMessageHelper.Error(LegacyConvertMessageHelper.CodeFrontElementsFolderMissing);

        Assert.Equal(FrontedLayoutPackageLegacyConvertMessageSeverity.Error, message.Severity);
        Assert.NotEmpty(message.Message);
    }

    [Fact]
    public void PopulateFromMessages_FillsOldArrays()
    {
        var messages = new List<FrontedLayoutPackageLegacyConvertMessage>
        {
            LegacyConvertMessageHelper.Info("Test.Info", LegacyConvertMessageHelper.Args(new { Key = "value" })),
            LegacyConvertMessageHelper.Compat("Test.Compat", LegacyConvertMessageHelper.Args(new { Key = "value" })),
            LegacyConvertMessageHelper.Warning("Test.Warning", LegacyConvertMessageHelper.Args(new { Key = "value" })),
            LegacyConvertMessageHelper.Error("Test.Error", LegacyConvertMessageHelper.Args(new { Key = "value" })),
        };

        var result = new FrontedLayoutPackageLegacyConvertResult();
        FrontedLayoutPackageLegacyConvertResult.PopulateFromMessages(result, messages);

        Assert.Equal(4, result.Messages.Count);
        Assert.Equal(2, result.Infos.Count);  // Info + Compat
        Assert.Equal(2, result.Diagnostics.Count);  // Info + Compat
        Assert.Equal(2, result.Warnings.Count);  // Warning + Error
    }

    [Fact]
    public void Args_CreatesDictionaryFromAnonymousObject()
    {
        var args = LegacyConvertMessageHelper.Args(new { FileName = "test.json", Reason = "File not found" });

        Assert.Equal(2, args.Count);
        Assert.Equal("test.json", args["FileName"]);
        Assert.Equal("File not found", args["Reason"]);
    }

    [Fact]
    public void Args_NullReturnsEmptyDictionary()
    {
        var args = LegacyConvertMessageHelper.Args(null);

        Assert.Empty(args);
    }

    [Fact]
    public void BuildLocalizedMessage_SubstitutesArgs()
    {
        LegacyConvertMessageHelper.LocalizeTemplate = key => key switch
        {
            "Test.Template" => "Hello {Name}, you have {Count} items.",
            _ => key
        };

        try
        {
            var message = LegacyConvertMessageHelper.BuildLocalizedMessage(
                "Test.Template",
                LegacyConvertMessageHelper.Args(new { Name = "World", Count = "42" }));

            Assert.Equal("Hello World, you have 42 items.", message);
        }
        finally
        {
            LegacyConvertMessageHelper.LocalizeTemplate = null;
        }
    }

    [Fact]
    public void BuildLocalizedMessage_FallsBackToCode()
    {
        var message = LegacyConvertMessageHelper.BuildLocalizedMessage(
            "No.Such.Key",
            LegacyConvertMessageHelper.Args(new { Arg = "val" }));

        Assert.Equal("No.Such.Key (Arg=val)", message);
    }

    [Fact]
    public void MessagesAreGroupedBySeverityInTechnicalDetails()
    {
        var result = new FrontedLayoutPackageLegacyConvertResult();
        var messages = new List<FrontedLayoutPackageLegacyConvertMessage>
        {
            LegacyConvertMessageHelper.Error(LegacyConvertMessageHelper.CodeFrontElementsFolderMissing),
            LegacyConvertMessageHelper.Warning(LegacyConvertMessageHelper.CodeUnknownLayoutFileSkipped,
                LegacyConvertMessageHelper.Args(new { FileName = "x.json" })),
            LegacyConvertMessageHelper.Compat(LegacyConvertMessageHelper.CodeMapBpV1Skipped,
                LegacyConvertMessageHelper.Args(new { SourceWindow = "W", SourceCanvas = "C" })),
            LegacyConvertMessageHelper.Info(LegacyConvertMessageHelper.CodeResourceCopied,
                LegacyConvertMessageHelper.Args(new { FileName = "r.png" })),
        };
        FrontedLayoutPackageLegacyConvertResult.PopulateFromMessages(result, messages);

        var details = LegacyConversionMessageFormatter.BuildTechnicalDetails(result);

        Assert.Contains("Errors:", details);
        Assert.Contains("Warnings:", details);
        Assert.Contains("CompatibilityNotes:", details);
        Assert.Contains("Info:", details);
    }
}
