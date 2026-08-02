extern alias smartbp;

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Services;
using OpenCvSharp;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;
using Microsoft.Extensions.Logging;
using ISmartBpOcrBpRecognitionService = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpOcrBpRecognitionService;
using ISmartBpRecognitionFrameCropper = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpRecognitionFrameCropper;
using SmartBpBusinessStateRecognitionResult = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpBusinessStateRecognitionResult;
using SmartBpCroppedFrame = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpCroppedFrame;
using SmartBpOcrRecognitionRequest = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpOcrRecognitionRequest;
using SmartBpOcrRecognitionResult = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpOcrRecognitionResult;
using SmartBpOcrSnapshotDeltaRecognitionService = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpOcrSnapshotDeltaRecognitionService;
using OcrTextBlockResult = smartbp::neo_bpsys_wpf.Core.Abstractions.Services.OcrTextBlockResult;
using OcrTextLine = smartbp::neo_bpsys_wpf.Core.Abstractions.Services.OcrTextLine;
using SmartBpRecognizedCharacterSlot = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognizedCharacterSlot;
using SmartBpRecognizedPlayerCharacterSlot = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognizedPlayerCharacterSlot;
using SmartBpSnapshotDeltaRequest = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpSnapshotDeltaRequest;
using SmartBpOcrContactSheetMapper = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpOcrContactSheetMapper;
using SmartBpOcrContactSheetRegion = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpOcrContactSheetRegion;
using SmartBpOcrPhaseClassifier = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpOcrPhaseClassifier;
using SmartBpPostBpStatusDetector = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpPostBpStatusDetector;
using SmartBpLifecycleStatusDetector = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpLifecycleStatusDetector;
using SmartBpLifecycleCategory = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpLifecycleCategory;
using SmartBpOcrRegionParser = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpOcrRegionParser;
using SmartBpOcrTextResolver = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpOcrTextResolver;
using SmartBpOcrFieldParseContext = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpOcrFieldParseContext;
using SmartBpPickedSurOcrParseMode = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpPickedSurOcrParseMode;
using SmartBpSnapshotFieldUpdate = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpSnapshotFieldUpdate;
using SmartBpSnapshotDeltaSlot = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpSnapshotDeltaSlot;
using TesseractCoordinateMapper = smartbp::neo_bpsys_wpf.Services.TesseractCoordinateMapper;
using SmartBpOcrProviderSelector = smartbp::neo_bpsys_wpf.Services.SmartBpOcrProviderSelector;
using ISmartBpRecognitionSettingsService = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpRecognitionSettingsService;
using SmartBpRecognitionSettings = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognitionSettings;
using SmartBpOcrProviderMode = smartbp::neo_bpsys_wpf.Core.Abstractions.Services.SmartBpOcrProviderMode;
using SmartBpOcrProviderKind = smartbp::neo_bpsys_wpf.Core.Abstractions.Services.SmartBpOcrProviderKind;
using IOcrProvider = smartbp::neo_bpsys_wpf.Core.Abstractions.Services.IOcrProvider;
using OcrRecognitionOptions = smartbp::neo_bpsys_wpf.Core.Abstractions.Services.OcrRecognitionOptions;
using SmartBpRecognitionRegion = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognitionRegion;
using SmartBpRecognizedSlotState = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognizedSlotState;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class SmartBpOcrRecognitionContractTest
{
    [Theory]
    [InlineData("阵营选择中", SmartBpLifecycleCategory.CharacterBpActive)]
    [InlineData("求生者天赋特质调整", SmartBpLifecycleCategory.SurvivorTalentAdjust)]
    [InlineData("监管者天赋特质调整", SmartBpLifecycleCategory.HunterTalentAdjust)]
    [InlineData("即将进入区域选择", SmartBpLifecycleCategory.TransitionToAreaSelection)]
    [InlineData("即将进人区域选择", SmartBpLifecycleCategory.TransitionToAreaSelection)]
    public void TopCenterLifecycleDetectorUsesFuzzyScoring(string text, SmartBpLifecycleCategory expected)
    {
        var result = new SmartBpLifecycleStatusDetector().Detect([Line(text, 50, 20)]);

        Assert.True(result.IsRecognized);
        Assert.Equal(expected, result.Category);
        Assert.InRange(result.Score, .65, 1);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Contains("phrase similarity", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("前往【永眠镇】")]
    [InlineData("区域")]
    [InlineData("随机字幕噪声")]
    public void TopCenterLifecycleDetectorDoesNotInferStateFromWeakOrAuxiliaryText(string text)
    {
        var result = new SmartBpLifecycleStatusDetector().Detect([Line(text, 50, 20)]);

        Assert.False(result.IsRecognized);
        Assert.Equal(SmartBpLifecycleCategory.Unknown, result.Category);
    }

    [Theory]
    [InlineData("屏蔽求生者", 150, "屏蔽求生者")]
    [InlineData("屏蔽监管者", 50, "屏蔽监管者")]
    [InlineData("选择求生者", 50, "选择求生者")]
    [InlineData("求生者选择角色中", 50, "求生者选择角色中")]
    [InlineData("选择监管者", 150, "选择监管者")]
    [InlineData("选择天赋中", 50, "求生者选择天赋中")]
    [InlineData("选择天赋中", 150, "监管者选择天赋中")]
    [InlineData("天赋已锁定", 50, "天赋已锁定")]
    public void PhaseClassifierUsesTextAndSideRules(string text, double x, string expected)
    {
        var diagnostics = new List<string>();

        var phase = SmartBpOcrPhaseClassifier.Classify([Line(text, x, 20)], 200, diagnostics);

        Assert.Equal(expected, phase.Phase);
        Assert.Contains(diagnostics, item => item.Contains("final phase", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PhaseClassifierDoesNotLetInactiveWaitingOverrideActiveText()
    {
        var diagnostics = new List<string>();

        var phase = SmartBpOcrPhaseClassifier.Classify(
            [Line("等待中", 150, 20), Line("选择求生者", 50, 20)],
            200,
            diagnostics);

        Assert.Equal("选择求生者", phase.Phase);
    }

    [Fact]
    public void PhaseClassifierDoesNotStopOnBareMapName()
    {
        var diagnostics = new List<string>();

        var phase = SmartBpOcrPhaseClassifier.Classify([Line("永眠镇", 50, 20)], 200, diagnostics);

        Assert.Equal("未知", phase.Phase);
    }

    [Theory]
    [InlineData("求生者选择区域中，剩余24秒", "求生者选择区域中")]
    [InlineData("监管者选择区域中，剩余10秒", "监管者选择区域中")]
    [InlineData("等待游戏开始，剩余58秒", "等待游戏开始")]
    [InlineData("等侍游戏开始", "等待游戏开始")]
    [InlineData("等待游戏升始", "等待游戏开始")]
    public void TopLeftStatusDetectorMatchesPrimaryTitles(string text, string expected)
    {
        var result = SmartBpPostBpStatusDetector.Detect([Line(text, 50, 20)]);

        Assert.True(result.IsPostBp);
        Assert.Equal(expected, result.Phase);
        Assert.Contains(text, result.Evidence);
        Assert.NotEmpty(result.NormalizedText);
    }

    [Theory]
    [InlineData("永眠镇")]
    [InlineData("剩余28秒")]
    [InlineData("前往【永眠镇】")]
    [InlineData("屏蔽求生者")]
    [InlineData("选择监管者")]
    public void TopLeftStatusDetectorDoesNotFalseTrigger(string text)
    {
        var result = SmartBpPostBpStatusDetector.Detect([Line(text, 50, 20)]);

        Assert.False(result.IsPostBp);
    }

    [Fact]
    public void TopLeftStatusDetectorUsesCombinedAuxiliaryEvidenceWithoutMapList()
    {
        var result = SmartBpPostBpStatusDetector.Detect(
            [Line("剩余28秒", 50, 20), Line("前往【永眠镇】", 50, 40)]);

        Assert.True(result.IsPostBp);
        Assert.Equal("等待游戏开始", result.Phase);
        Assert.Equal(["剩余秒", "前往地图"], result.AuxiliaryEvidence);
    }

    [Fact]
    public void RightTopLinesProduceSurvivorBans()
    {
        var parser = Parser(
            new Character("小说家", Camp.Sur, "novelist"),
            new Character("昆虫学者", Camp.Sur, "entomologist"));

        var result = parser.Parse(
            SmartBpRecognitionRegion.RightTop,
            [Line("小说家", 20, 40), Line("昆虫学者", 80, 40)],
            [],
            regionWidth: 200);

        Assert.Equal("banned_sur", result.TargetField);
        Assert.Equal("小说家", result.Slots[0].CharacterName);
        Assert.Equal("昆虫学者", result.Slots[1].CharacterName);
        Assert.Equal("未选择", result.Slots[2].CharacterName);
    }

    [Fact]
    public void LeftTopLinesProduceHunterBans()
    {
        var parser = Parser(
            new Character("梦之女巫", Camp.Hun, "dream-witch"),
            new Character("女王蜂", Camp.Hun, "queen-bee"));

        var result = parser.Parse(
            SmartBpRecognitionRegion.LeftTop,
            [Line("梦之女巫", 20, 40), Line("女王蜂", 80, 40)],
            []);

        Assert.Equal("banned_hun", result.TargetField);
        Assert.Equal("梦之女巫", result.Slots[0].CharacterName);
        Assert.Equal("女王蜂", result.Slots[1].CharacterName);
    }

    [Fact]
    public void LeftBottomGroupsSurvivorPickCharacterAndPlayerIdByYOrder()
    {
        var parser = Parser(
            new Character("心理学家", Camp.Sur, "psychologist"),
            new Character("守墓人", Camp.Sur, "grave-keeper"));

        var result = parser.Parse(
            SmartBpRecognitionRegion.LeftBottom,
            [
                Line("心理学家", 20, 20),
                Line("选手A", 20, 42),
                Line("飞轮效应", 20, 80),
                Line("守墓人", 90, 20),
                Line("选手B", 90, 42)
            ],
            []);

        Assert.Equal("picked_sur", result.TargetField);
        Assert.Equal("心理学家", result.Slots[0].CharacterName);
        Assert.Equal("选手A", result.Slots[0].PlayerId);
        Assert.Equal("守墓人", result.Slots[1].CharacterName);
        Assert.Equal("选手B", result.Slots[1].PlayerId);
    }

    [Fact]
    public void LeftBottomIgnoresTalentRowEvenWhenTalentTextIsSurvivorName()
    {
        var parser = Parser(
            new Character("拉拉队员", Camp.Sur, "cheerleader"),
            new Character("魔术师", Camp.Sur, "magician"),
            new Character("守墓人", Camp.Sur, "grave-keeper"),
            new Character("先知", Camp.Sur, "seer"),
            new Character("冒险家", Camp.Sur, "explorer"));
        var diagnostics = new List<string>();

        var result = parser.Parse(
            SmartBpRecognitionRegion.LeftBottom,
            [
                Line("拉拉队员", 83, 152),
                Line("魔术师", 207, 152),
                Line("守墓人", 327, 152),
                Line("先知", 451, 152),
                Line("不满绩不改名", 83, 176),
                Line("冥归处", 207, 176),
                Line("袁宇梦男", 327, 176),
                Line("特芯糖0v0", 451, 176),
                Line("冒险家", 83, 199),
                Line("博命心", 207, 199),
                Line("救", 327, 199),
                Line("双弹飞轮", 451, 199)
            ],
            diagnostics);

        Assert.Equal("picked_sur", result.TargetField);
        Assert.Equal("拉拉队员", result.Slots[0].CharacterName);
        Assert.Equal("不满绩不改名", result.Slots[0].PlayerId);
        Assert.Equal("魔术师", result.Slots[1].CharacterName);
        Assert.Equal("冥归处", result.Slots[1].PlayerId);
        Assert.Equal("守墓人", result.Slots[2].CharacterName);
        Assert.Equal("袁宇梦男", result.Slots[2].PlayerId);
        Assert.Equal("先知", result.Slots[3].CharacterName);
        Assert.Equal("特芯糖0v0", result.Slots[3].PlayerId);
        Assert.DoesNotContain(result.Slots, slot => slot.CharacterName == "冒险家");
        Assert.Contains(diagnostics, item => item.Contains("picked_sur row clustering", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Contains("row 2", StringComparison.Ordinal) && item.Contains("冒险家", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Contains("ignored lower-row character candidate", StringComparison.Ordinal) && item.Contains("冒险家", StringComparison.Ordinal));
    }

    [Fact]
    public void LeftBottomMissingPlayerRowKeepsCharactersWithoutPlayerIds()
    {
        var parser = Parser(
            new Character("拉拉队员", Camp.Sur, "cheerleader"),
            new Character("魔术师", Camp.Sur, "magician"));

        var result = parser.Parse(
            SmartBpRecognitionRegion.LeftBottom,
            [Line("拉拉队员", 83, 152), Line("魔术师", 207, 152)],
            []);

        Assert.Equal("拉拉队员", result.Slots[0].CharacterName);
        Assert.Null(result.Slots[0].PlayerId);
        Assert.Equal("魔术师", result.Slots[1].CharacterName);
        Assert.Null(result.Slots[1].PlayerId);
    }

    [Fact]
    public void LeftBottomDoesNotFillMissingCharacterSlotsFromTalentRows()
    {
        var parser = Parser(
            new Character("拉拉队员", Camp.Sur, "cheerleader"),
            new Character("守墓人", Camp.Sur, "grave-keeper"),
            new Character("先知", Camp.Sur, "seer"),
            new Character("冒险家", Camp.Sur, "explorer"));

        var result = parser.Parse(
            SmartBpRecognitionRegion.LeftBottom,
            [
                Line("拉拉队员", 83, 152),
                Line("守墓人", 327, 152),
                Line("先知", 451, 152),
                Line("不满绩不改名", 83, 176),
                Line("冥归处", 207, 176),
                Line("袁宇梦男", 327, 176),
                Line("特芯糖0v0", 451, 176),
                Line("冒险家", 207, 199)
            ],
            []);

        Assert.Equal("拉拉队员", result.Slots[0].CharacterName);
        Assert.Equal("未选择", result.Slots[1].CharacterName);
        Assert.Equal("守墓人", result.Slots[2].CharacterName);
        Assert.Equal("先知", result.Slots[3].CharacterName);
        Assert.Equal("冥归处", result.Slots[1].PlayerId);
        Assert.DoesNotContain(result.Slots, slot => slot.CharacterName == "冒险家");
    }

    [Fact]
    public void RightBottomLinesProduceHunterPickAndPlayerId()
    {
        var parser = Parser(new Character("厂长", Camp.Hun, "hell-ember"));

        var result = parser.Parse(
            SmartBpRecognitionRegion.RightBottom,
            [Line("厂长", 50, 20), Line("导播PLFJY", 50, 45)],
            []);

        Assert.Equal("picked_hun", result.TargetField);
        Assert.NotNull(result.PickedHun);
        Assert.Equal("厂长", result.PickedHun!.CharacterName);
        Assert.Equal("导播PLFJY", result.PickedHun.PlayerId);
    }

    [Theory]
    [InlineData("已选择")]
    [InlineData("等待选择...")]
    public void RightBottomStatusTextIsNotUsedAsPlayerId(string statusText)
    {
        var parser = Parser(new Character("厂长", Camp.Hun, "hell-ember"));

        var result = parser.Parse(
            SmartBpRecognitionRegion.RightBottom,
            [Line("厂长", 50, 20), Line(statusText, 50, 45)],
            []);

        Assert.Equal("厂长", result.PickedHun!.CharacterName);
        Assert.Null(result.PickedHun.PlayerId);
    }

    [Fact]
    public void UnresolvedOcrTextIsNotAppliedAsCharacter()
    {
        var parser = Parser(new Character("小说家", Camp.Sur, "novelist"));

        var result = parser.Parse(SmartBpRecognitionRegion.RightTop, [Line("说书人", 20, 40)], []);

        Assert.All(result.Slots, slot => Assert.Equal("未选择", slot.CharacterName));
    }

    [Theory]
    [InlineData("\"心理学家\"")]
    [InlineData("“心理学家”")]
    [InlineData("『心理学家』")]
    [InlineData("「心理学家」")]
    public void QuotedOfficialNameResolvesToCanonicalCandidate(string text)
    {
        var resolver = Resolver(new Character("心理学家", Camp.Sur, "psychologist"));

        var result = resolver.ResolveCharacterFromLine(text, Camp.Sur, 0);

        Assert.Equal("心理学家", result.ResolvedCharacterName);
    }

    [Fact]
    public void ContactSheetMapsLinesBackToCorrectRegionAndIgnoresPadding()
    {
        var mappings = new[]
        {
            new SmartBpOcrContactSheetRegion(SmartBpRecognitionRegion.LeftTop, new Rect(0, 0, 100, 50), new Rect(0, 0, 100, 50)),
            new SmartBpOcrContactSheetRegion(SmartBpRecognitionRegion.RightTop, new Rect(0, 80, 100, 50), new Rect(100, 0, 100, 50))
        };
        var block = new OcrTextBlockResult(
            [Line("梦之女巫", 20, 20), Line("gap-noise", 20, 65), Line("小说家", 20, 100)],
            "梦之女巫\ngap-noise\n小说家");

        var grouped = SmartBpOcrContactSheetMapper.MapLinesToRegions(block, mappings, out var unmapped);

        Assert.Equal(1, unmapped);
        Assert.Equal("梦之女巫", grouped.Single(item => item.Region == SmartBpRecognitionRegion.LeftTop).Lines[0].Text);
        var right = grouped.Single(item => item.Region == SmartBpRecognitionRegion.RightTop).Lines[0];
        Assert.Equal("小说家", right.Text);
        Assert.Equal(20, right.CenterY);
    }

    [Fact]
    public void TesseractScaledCoordinatesMapBackToInputCoordinates()
    {
        var mapped = TesseractCoordinateMapper.MapToOriginal(new Rect(100, 40, 80, 24), 2, 2, 400, 200);

        Assert.Equal(new Rect(50, 20, 40, 12), mapped);
    }

    [Theory]
    [InlineData(SmartBpOcrProviderMode.Paddle, SmartBpOcrProviderKind.Paddle)]
    [InlineData(SmartBpOcrProviderMode.Tesseract, SmartBpOcrProviderKind.Tesseract)]
    [InlineData(SmartBpOcrProviderMode.Rapid, SmartBpOcrProviderKind.Rapid)]
    public void ProviderSelectorUsesOnlyExplicitSelection(SmartBpOcrProviderMode mode, SmartBpOcrProviderKind expected)
    {
        var settings = new FakeRecognitionSettings { Settings = new SmartBpRecognitionSettings { OcrProviderMode = mode } };
        var paddle = new FakeProvider(SmartBpOcrProviderKind.Paddle, true);
        var tesseract = new FakeProvider(SmartBpOcrProviderKind.Tesseract, false);
        var rapid = new FakeProvider(SmartBpOcrProviderKind.Rapid, true);
        var selector = new SmartBpOcrProviderSelector([paddle, tesseract, rapid], settings);

        Assert.Equal(expected, selector.GetSelectedProvider().Kind);
        Assert.Equal(expected != SmartBpOcrProviderKind.Tesseract, selector.GetSelectedProvider().IsReady);
    }

    [Fact]
    public void GlobalResolverMapsHunterOcrTypoAndQuotedNameStillResolves()
    {
        var resolver = Resolver(new Character("厂长", Camp.Hun, "hell-ember"));

        var alias = resolver.ResolveCharacterFromLine("广长", Camp.Hun, 0, "Tesseract");
        var quoted = resolver.ResolveCharacterFromLine("“厂长”", Camp.Hun, 0, "Paddle");

        Assert.Equal("厂长", alias.ResolvedCharacterName);
        Assert.Equal("厂长", quoted.ResolvedCharacterName);
        Assert.Contains(alias.Warnings, item => item.Contains("matchMode=short-name-correction", StringComparison.Ordinal));
    }

    [Fact]
    public void HunterAliasDoesNotResolveInSurvivorContext()
    {
        var resolver = Resolver(new Character("厂长", Camp.Hun, "hell-ember"));

        var result = resolver.ResolveCharacterFromLine("广长", Camp.Sur, 0, "Tesseract");

        Assert.Null(result.ResolvedCharacterName);
        Assert.False(result.IsAutoApplySafe);
    }

    [Fact]
    public void PhaseTextIsFilteredBeforeCharacterMatching()
    {
        var resolver = Resolver(new Character("求生者", Camp.Sur, "survivor"));

        var result = resolver.ResolveCharacterFromLine("屏蔽求生者", Camp.Sur, 0, "Paddle");

        Assert.Null(result.ResolvedCharacterName);
        Assert.Equal("filtered-status", result.MatchMode);
    }

    [Fact]
    public void OneCharacterHunterHintRemainsUnsafeForAutoApply()
    {
        var parser = Parser(new Character("厂长", Camp.Hun, "hell-ember"));

        var result = parser.Parse(SmartBpRecognitionRegion.RightBottom, [Line("厂", 50, 20)], []);

        Assert.Equal("厂长", result.PickedHun!.CharacterName);
        Assert.False(result.PickedHun.IsAutoApplySafe);
        Assert.True(result.PickedHun.RecognitionConfidence < .90);
    }

    [Fact]
    public void RightTopOcrTypoUsesGlobalResolverAndPreservesSlotOrder()
    {
        var parser = Parser(
            new Character("小说家", Camp.Sur, "novelist"),
            new Character("昆虫学者", Camp.Sur, "entomologist"),
            new Character("入殓师", Camp.Sur, "embalmer"),
            new Character("祭司", Camp.Sur, "priestess"));
        var diagnostics = new List<string>();

        var result = parser.Parse(
            SmartBpRecognitionRegion.RightTop,
            [
                Line("小说家", 118, 40, 1.00),
                Line("昆虫学者", 228, 40, 1.00),
                Line("入验师", 336, 40, .98),
                Line("祭司", 445, 40, .90)
            ],
            diagnostics,
            regionWidth: 563);

        Assert.Equal(["小说家", "昆虫学者", "入殓师", "祭司"], result.Slots.Select(slot => slot.CharacterName));
        Assert.Contains(diagnostics, item =>
            item.Contains("ocr-match region=right_top", StringComparison.Ordinal) &&
            item.Contains("raw=入验师", StringComparison.Ordinal) &&
            item.Contains("result=入殓师", StringComparison.Ordinal) &&
            item.Contains("matchMode=short-name-correction", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OcrDeltaServiceProducesExistingSnapshotDeltaShape()
    {
        var frame = CreateFrame();
        var state = new SmartBpBusinessStateRecognitionResult
        {
            Phase = "屏蔽求生者",
            BannedSur =
            [
                new() { Index = 0, CharacterName = "小说家" },
                new() { Index = 1, CharacterName = "昆虫学者" },
                new() { Index = 2, CharacterName = "未选择" },
                new() { Index = 3, CharacterName = "未选择" }
            ],
            BannedHun = Enumerable.Range(0, 2).Select(index => new SmartBpRecognizedCharacterSlot { Index = index, CharacterName = "未选择" }).ToList(),
            PickedSur = Enumerable.Range(0, 4).Select(index => new SmartBpRecognizedPlayerCharacterSlot { Index = index, CharacterName = "未选择" }).ToList(),
            PickedHun = new() { Index = 0, CharacterName = "未选择" }
        };
        var service = new SmartBpOcrSnapshotDeltaRecognitionService(
            new FakeOcrRecognition(state),
            new FakeCropper(frame));

        var result = await service.RecognizeDeltaAsync(
            frame,
            new SmartBpSnapshotDeltaRequest([(SmartBpRecognitionRegion.RightTop, "banned_sur")], []),
            7,
            TestContext.Current.CancellationToken);

        Assert.Equal("屏蔽求生者", result.Delta.Phase);
        var update = Assert.Single(result.Delta.Updates);
        Assert.Equal("banned_sur", update.Field);
        Assert.NotNull(update.Slots);
        Assert.Equal("小说家", update.Slots![0].CharacterName);
        Assert.Contains(result.Diagnostics, item => item.Contains("OCR elapsed time", StringComparison.Ordinal));
    }

    [Fact]
    public void PickedSurPickSurModeNoiseRowDoesNotShiftSemanticRows()
    {
        var parser = Parser(
            new Character("先知", Camp.Sur, "prophet"),
            new Character("拉拉队员", Camp.Sur, "cheerleader"));
        var context = new SmartBpOcrFieldParseContext
        {
            AuthoritativePhase = "求生者选择角色中",
            CurrentGuidanceAction = GameAction.PickSur,
            SurvivorPickLocked = false
        };
        // row 0: [P] noise; row 1: [未选择×4] character; row 2: [player names×4] player-id
        var lines = new[]
        {
            Line("P", 89, 65, 0.44),
            Line("未选择", 89, 180), Line("未选择", 216, 180), Line("未选择", 344, 180), Line("未选择", 472, 180),
            Line("IHiganbanal", 89, 203), Line("夜风之缚", 216, 203), Line("磁兮小狗", 344, 203), Line("叶落摘星", 472, 203)
        };
        var result = parser.Parse(SmartBpRecognitionRegion.LeftBottom, lines, [], context);
        Assert.Equal("picked_sur", result.TargetField);
        Assert.Equal("未选择", result.Slots[0].CharacterName);
        Assert.Equal("未选择", result.Slots[1].CharacterName);
        Assert.Equal("未选择", result.Slots[2].CharacterName);
        Assert.Equal("未选择", result.Slots[3].CharacterName);
        Assert.Equal("IHiganbanal", result.Slots[0].PlayerId);
        Assert.Equal("夜风之缚", result.Slots[1].PlayerId);
        Assert.Equal("磁兮小狗", result.Slots[2].PlayerId);
        Assert.Equal("叶落摘星", result.Slots[3].PlayerId);
    }

    [Fact]
    public void PickedSurPickSurModeDifferentNoiseFragmentAlsoClassifiedAsNoise()
    {
        var parser = Parser();
        var context = new SmartBpOcrFieldParseContext
        {
            AuthoritativePhase = "选择求生者",
            CurrentGuidanceAction = GameAction.PickSur,
            SurvivorPickLocked = false
        };
        var lines = new[]
        {
            Line("!", 89, 65, 0.40),
            Line("未选择", 89, 180), Line("未选择", 216, 180), Line("未选择", 344, 180), Line("未选择", 472, 180),
            Line("PlayerA", 89, 203), Line("PlayerB", 216, 203), Line("PlayerC", 344, 203), Line("PlayerD", 472, 203)
        };
        var result = parser.Parse(SmartBpRecognitionRegion.LeftBottom, lines, [], context);
        Assert.Equal("PlayerA", result.Slots[0].PlayerId);
        Assert.Equal("PlayerB", result.Slots[1].PlayerId);
        Assert.Equal("PlayerC", result.Slots[2].PlayerId);
        Assert.Equal("PlayerD", result.Slots[3].PlayerId);
    }

    [Fact]
    public void PickedSurPickSurModeDoesNotLogPlayerIdRowAsTalentExtra()
    {
        var parser = Parser();
        var context = new SmartBpOcrFieldParseContext
        {
            AuthoritativePhase = "求生者选择角色中",
            CurrentGuidanceAction = GameAction.PickSur,
            SurvivorPickLocked = false
        };
        var diagnostics = new List<string>();
        var lines = new[]
        {
            Line("P", 89, 65, 0.44),
            Line("未选择", 89, 180), Line("未选择", 216, 180), Line("未选择", 344, 180), Line("未选择", 472, 180),
            Line("IHiganbanal", 89, 203), Line("夜风之缚", 216, 203), Line("磁兮小狗", 344, 203), Line("叶落摘星", 472, 203)
        };
        parser.Parse(SmartBpRecognitionRegion.LeftBottom, lines, diagnostics, context);
        Assert.DoesNotContain(diagnostics, d => d.Contains("ignored talent/extra", StringComparison.Ordinal));
    }

    [Fact]
    public void PickedSurPickSurModeParsesSelectedCharactersBySlot()
    {
        var parser = Parser(
            new Character("先知", Camp.Sur, "prophet"),
            new Character("拉拉队员", Camp.Sur, "cheerleader"),
            new Character("魔术师", Camp.Sur, "magician"),
            new Character("守墓人", Camp.Sur, "gravekeeper"));
        var context = new SmartBpOcrFieldParseContext
        {
            AuthoritativePhase = "求生者选择角色中",
            CurrentGuidanceAction = GameAction.PickSur,
            SurvivorPickLocked = false
        };
        var lines = new[]
        {
            Line("先知", 89, 180), Line("拉拉队员", 216, 180), Line("魔术师", 344, 180), Line("守墓人", 472, 180),
            Line("PlayerA", 89, 203), Line("PlayerB", 216, 203), Line("PlayerC", 344, 203), Line("PlayerD", 472, 203)
        };
        var result = parser.Parse(SmartBpRecognitionRegion.LeftBottom, lines, [], context);
        Assert.Equal("先知", result.Slots[0].CharacterName);
        Assert.Equal("拉拉队员", result.Slots[1].CharacterName);
        Assert.Equal("魔术师", result.Slots[2].CharacterName);
        Assert.Equal("守墓人", result.Slots[3].CharacterName);
        Assert.Equal("PlayerA", result.Slots[0].PlayerId);
        Assert.Equal("PlayerB", result.Slots[1].PlayerId);
        Assert.Equal("PlayerC", result.Slots[2].PlayerId);
        Assert.Equal("PlayerD", result.Slots[3].PlayerId);
    }

    [Fact]
    public void PickedSurShortPlayerIdNotDroppedInStrongPlayerIdRow()
    {
        var parser = Parser();
        var context = new SmartBpOcrFieldParseContext
        {
            AuthoritativePhase = "选择求生者",
            CurrentGuidanceAction = GameAction.PickSur,
            SurvivorPickLocked = false
        };
        var lines = new[]
        {
            Line("未选择", 89, 180), Line("未选择", 216, 180), Line("未选择", 344, 180), Line("未选择", 472, 180),
            Line("A", 89, 203), Line("B", 216, 203), Line("C", 344, 203), Line("D", 472, 203)
        };
        var result = parser.Parse(SmartBpRecognitionRegion.LeftBottom, lines, [], context);
        Assert.Equal("A", result.Slots[0].PlayerId);
        Assert.Equal("B", result.Slots[1].PlayerId);
        Assert.Equal("C", result.Slots[2].PlayerId);
        Assert.Equal("D", result.Slots[3].PlayerId);
    }

    [Fact]
    public void PickedSurSingleLowConfidenceFragmentRowClassifiedAsNoise()
    {
        var parser = Parser();
        var context = new SmartBpOcrFieldParseContext
        {
            AuthoritativePhase = "选择求生者",
            CurrentGuidanceAction = GameAction.PickSur,
            SurvivorPickLocked = false
        };
        var diagnostics = new List<string>();
        var lines = new[]
        {
            Line("藏", 89, 65, 0.30),
            Line("未选择", 89, 180), Line("未选择", 216, 180), Line("未选择", 344, 180), Line("未选择", 472, 180),
            Line("PlayerA", 89, 203), Line("PlayerB", 216, 203), Line("PlayerC", 344, 203), Line("PlayerD", 472, 203)
        };
        var result = parser.Parse(SmartBpRecognitionRegion.LeftBottom, lines, diagnostics, context);
        Assert.Contains(diagnostics, d => d.Contains("row 0 => Noise", StringComparison.Ordinal));
        Assert.Equal("PlayerA", result.Slots[0].PlayerId);
    }

    [Fact]
    public void PickedSurSurvivorTalentModeTalentRowIgnored()
    {
        var parser = Parser(
            new Character("拉拉队员", Camp.Sur, "cheerleader"),
            new Character("魔术师", Camp.Sur, "magician"),
            new Character("守墓人", Camp.Sur, "gravekeeper"),
            new Character("先知", Camp.Sur, "prophet"),
            new Character("冒险家", Camp.Sur, "explorer"));
        var context = new SmartBpOcrFieldParseContext
        {
            AuthoritativePhase = "求生者选择天赋中",
            CurrentGuidanceAction = GameAction.PickSurTalent,
            SurvivorPickLocked = true
        };
        var lines = new[]
        {
            Line("拉拉队员", 89, 180), Line("魔术师", 216, 180), Line("守墓人", 344, 180), Line("先知", 472, 180),
            Line("PlayerA", 89, 203), Line("PlayerB", 216, 203), Line("PlayerC", 344, 203), Line("PlayerD", 472, 203),
            Line("冒险家", 89, 230), Line("博命心", 216, 230), Line("救", 344, 230), Line("双弹飞轮", 472, 230)
        };
        var diagnostics = new List<string>();
        var result = parser.Parse(SmartBpRecognitionRegion.LeftBottom, lines, diagnostics, context);
        Assert.Equal("拉拉队员", result.Slots[0].CharacterName);
        Assert.Equal("魔术师", result.Slots[1].CharacterName);
        Assert.Equal("守墓人", result.Slots[2].CharacterName);
        Assert.Equal("先知", result.Slots[3].CharacterName);
        Assert.Contains(diagnostics, d => d.Contains("ignored talent/extra", StringComparison.Ordinal) && d.Contains("冒险家", StringComparison.Ordinal));
    }

    [Fact]
    public void PickedSurSurvivorTalentModeTalentRowCharacterDoesNotOverwriteCharacterRow()
    {
        var parser = Parser(
            new Character("拉拉队员", Camp.Sur, "cheerleader"),
            new Character("冒险家", Camp.Sur, "explorer"));
        var context = new SmartBpOcrFieldParseContext
        {
            AuthoritativePhase = "求生者选择天赋中",
            CurrentGuidanceAction = GameAction.PickSurTalent,
            SurvivorPickLocked = true
        };
        var lines = new[]
        {
            Line("拉拉队员", 89, 180),
            Line("PlayerA", 89, 203),
            Line("冒险家", 89, 230)
        };
        var result = parser.Parse(SmartBpRecognitionRegion.LeftBottom, lines, [], context);
        Assert.Equal("拉拉队员", result.Slots[0].CharacterName);
    }

    [Fact]
    public void PickedSurDistributeCharaModeParsesCharacterAndPlayerIdRows()
    {
        var parser = Parser(
            new Character("拉拉队员", Camp.Sur, "cheerleader"),
            new Character("魔术师", Camp.Sur, "magician"));
        var context = new SmartBpOcrFieldParseContext
        {
            AuthoritativePhase = "角色分配",
            CurrentGuidanceAction = GameAction.DistributeChara,
            SurvivorPickLocked = true
        };
        var lines = new[]
        {
            Line("拉拉队员", 89, 180), Line("魔术师", 216, 180), Line("未选择", 344, 180), Line("未选择", 472, 180),
            Line("PlayerA", 89, 203), Line("PlayerB", 216, 203), Line("PlayerC", 344, 203), Line("PlayerD", 472, 203)
        };
        var result = parser.Parse(SmartBpRecognitionRegion.LeftBottom, lines, [], context);
        Assert.Equal("拉拉队员", result.Slots[0].CharacterName);
        Assert.Equal("魔术师", result.Slots[1].CharacterName);
        Assert.Equal("PlayerA", result.Slots[0].PlayerId);
        Assert.Equal("PlayerB", result.Slots[1].PlayerId);
    }

    [Fact]
    public void PickedSurUnknownModeFallsBackToLegacyBehavior()
    {
        var parser = Parser(
            new Character("先知", Camp.Sur, "prophet"));
        // No context => Unknown mode => legacy physical-row-index semantics
        var lines = new[]
        {
            Line("先知", 89, 180),
            Line("PlayerA", 89, 203)
        };
        var result = parser.Parse(SmartBpRecognitionRegion.LeftBottom, lines, []);
        Assert.Equal("先知", result.Slots[0].CharacterName);
        Assert.Equal("PlayerA", result.Slots[0].PlayerId);
    }

    [Fact]
    public void GlobalSnapshotParsesPickedSurvivorsWhileCurrentPhaseIsBanSur()
    {
        var parser = Parser(
            new Character("幻灯师", Camp.Sur, "illusionist.png"),
            new Character("守墓人", Camp.Sur, "gravekeeper.png"),
            new Character("冒险家", Camp.Sur, "explorer.png"));
        var context = new SmartBpOcrFieldParseContext
        {
            AuthoritativePhase = "屏蔽求生者",
            CurrentGuidanceAction = GameAction.BanSur,
            SurvivorPickLocked = false,
            IsGlobalSnapshot = true
        };
        var diagnostics = new List<string>();

        var result = parser.Parse(
            SmartBpRecognitionRegion.LeftBottom,
            [
                Line("P", 89, 65, .30),
                Line("幻灯师", 89, 180), Line("守墓人", 216, 180),
                Line("未选择", 344, 180), Line("未选择", 472, 180),
                Line("PlayerA", 89, 203), Line("PlayerB", 216, 203),
                Line("PlayerC", 344, 203), Line("PlayerD", 472, 203),
                Line("冒险家", 89, 230)
            ],
            diagnostics,
            context);

        Assert.Equal(SmartBpPickedSurOcrParseMode.GlobalSnapshot, context.ResolvePickedSurParseMode());
        Assert.Equal("幻灯师", result.Slots[0].CharacterName);
        Assert.Equal("守墓人", result.Slots[1].CharacterName);
        Assert.Equal("PlayerA", result.Slots[0].PlayerId);
        Assert.Equal("PlayerB", result.Slots[1].PlayerId);
        Assert.Contains(diagnostics, item => item.Contains("parse mode=GlobalSnapshot", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Contains("ignored talent/extra", StringComparison.Ordinal));
    }

    [Fact]
    public void EmptyFirstBanPreservesLaterVisualSlotIndexes()
    {
        var parser = Parser(
            new Character("小说家", Camp.Sur, "novelist"),
            new Character("昆虫学者", Camp.Sur, "entomologist"));

        var result = parser.ParseDetailed(
            SmartBpRecognitionRegion.RightTop,
            [Line("小说家", 150, 30), Line("昆虫学者", 250, 30)],
            regionWidth: 400).Result;

        Assert.Equal(SmartBpRecognizedSlotState.Unknown, result.Slots[0].SlotState);
        Assert.Equal("小说家", result.Slots[1].CharacterName);
        Assert.Equal("昆虫学者", result.Slots[2].CharacterName);
    }

    [Fact]
    public void EmptyMiddleBanPreservesHole()
    {
        var parser = Parser(
            new Character("小说家", Camp.Sur, "novelist"),
            new Character("昆虫学者", Camp.Sur, "entomologist"),
            new Character("先知", Camp.Sur, "seer"));

        var result = parser.ParseDetailed(
            SmartBpRecognitionRegion.RightTop,
            [Line("小说家", 50, 30), Line("昆虫学者", 250, 30), Line("先知", 350, 30)],
            regionWidth: 400).Result;

        Assert.Equal("小说家", result.Slots[0].CharacterName);
        Assert.Equal(SmartBpRecognizedSlotState.Unknown, result.Slots[1].SlotState);
        Assert.Equal("昆虫学者", result.Slots[2].CharacterName);
        Assert.Equal("先知", result.Slots[3].CharacterName);
    }

    [Fact]
    public void TwoCandidatesInSameBanSlotAreNotShifted()
    {
        var parser = Parser(
            new Character("小说家", Camp.Sur, "novelist"),
            new Character("昆虫学者", Camp.Sur, "entomologist"));

        var result = parser.ParseDetailed(
            SmartBpRecognitionRegion.RightTop,
            [Line("小说家", 45, 30), Line("昆虫学者", 55, 30)],
            regionWidth: 400).Result;

        Assert.Equal(SmartBpRecognizedSlotState.Unknown, result.Slots[0].SlotState);
        Assert.All(result.Slots.Skip(1), slot => Assert.Equal(SmartBpRecognizedSlotState.Unknown, slot.SlotState));
    }

    [Fact]
    public void BanParserUsesFixedGeometryForSurvivorAndHunterSlots()
    {
        var parser = Parser(
            new Character("小说家", Camp.Sur, "novelist"),
            new Character("先知", Camp.Sur, "seer"),
            new Character("厂长", Camp.Hun, "hell-ember"));

        var survivor = parser.ParseDetailed(
            SmartBpRecognitionRegion.RightTop,
            [Line("小说家", 50, 30), Line("先知", 350, 30)],
            regionWidth: 400).Result;
        var hunter = parser.ParseDetailed(
            SmartBpRecognitionRegion.LeftTop,
            [Line("厂长", 150, 30)],
            regionWidth: 200).Result;

        Assert.Equal("小说家", survivor.Slots[0].CharacterName);
        Assert.Equal("先知", survivor.Slots[3].CharacterName);
        Assert.Equal(SmartBpRecognizedSlotState.Unknown, hunter.Slots[0].SlotState);
        Assert.Equal("厂长", hunter.Slots[1].CharacterName);
    }

    [Fact]
    public void CompactSurvivorBanRowMapsAllFourFixedVisualSlots()
    {
        var parser = Parser(
            new Character("医生", Camp.Sur, "doctor.png"),
            new Character("弓箭手", Camp.Sur, "archer.png"),
            new Character("木偶师", Camp.Sur, "puppeteer.png"),
            new Character("幸运儿", Camp.Sur, "lucky-guy.png"));

        var result = parser.ParseDetailed(
            SmartBpRecognitionRegion.RightTop,
            [
                Line("T", 393, 68, .05),
                Line("医生", 107.5, 102.5),
                Line("弓箭手", 204.5, 102.5),
                Line("木偶师", 300.5, 103),
                Line("幸运儿", 397.5, 103)
            ],
            regionWidth: 540).Result;

        Assert.Equal(["医生", "弓箭手", "木偶师", "幸运儿"], result.Slots.Select(slot => slot.CharacterName));
        Assert.All(result.Slots, slot => Assert.Equal(SmartBpRecognizedSlotState.Selected, slot.SlotState));
    }

    [Fact]
    public void MissingOcrTextProducesUnknownNotEmpty()
    {
        var result = Parser().ParseDetailed(
            SmartBpRecognitionRegion.RightTop,
            [],
            regionWidth: 400).Result;

        Assert.All(result.Slots, slot =>
        {
            Assert.Equal(SmartBpRecognizedSlotState.Unknown, slot.SlotState);
        });
    }

    private static SmartBpOcrRegionParser Parser(params Character[] characters) =>
        new(Resolver(characters));

    private static SmartBpOcrTextResolver Resolver(params Character[] characters)
    {
        var shared = new Mock<ISharedDataService>();
        shared.SetupGet(x => x.SurCharaDict).Returns(new SortedDictionary<string, Character>(characters.Where(x => x.Camp == Camp.Sur).ToDictionary(x => x.Name)));
        shared.SetupGet(x => x.HunCharaDict).Returns(new SortedDictionary<string, Character>(characters.Where(x => x.Camp == Camp.Hun).ToDictionary(x => x.Name)));
        var service = new CharacterSelectionService(
            shared.Object,
            Mock.Of<IFrontedTransitionOrchestrator>(),
            Mock.Of<IFrontedLayoutService>());
        return new SmartBpOcrTextResolver(service);
    }

    private static OcrTextLine Line(string text, double x, double y, double confidence = .98) =>
        new(text, confidence, new Rect((int)x - 5, (int)y - 5, 10, 10), x, y);

    private static BitmapSource CreateFrame()
    {
        byte[] pixels = [255, 255, 255];
        var frame = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgr24, null, pixels, 3);
        frame.Freeze();
        return frame;
    }

    private sealed class FakeOcrRecognition(SmartBpBusinessStateRecognitionResult state) : ISmartBpOcrBpRecognitionService
    {
        public Task<SmartBpOcrRecognitionResult> RecognizeAsync(
            BitmapSource frame,
            SmartBpOcrRecognitionRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SmartBpOcrRecognitionResult
            {
                Phase = new() { Phase = state.Phase },
                BusinessState = state,
                Diagnostics = ["fake ocr"]
            });
    }

    private sealed class FakeCropper(BitmapSource frame) : ISmartBpRecognitionFrameCropper
    {
        public SmartBpCroppedFrame CropWithInfo(BitmapSource source, SmartBpRecognitionRegion region) =>
            new(region, frame, 0, 0, frame.PixelWidth, frame.PixelHeight);

        public BitmapSource Crop(BitmapSource source, SmartBpRecognitionRegion region) => frame;
    }

    private sealed class FakeProvider(SmartBpOcrProviderKind kind, bool ready) : IOcrProvider
    {
        public SmartBpOcrProviderKind Kind { get; } = kind;
        public bool IsReady { get; } = ready;
        public OcrTextBlockResult RecognizeTextLines(Mat img, OcrRecognitionOptions? options = null) => OcrTextBlockResult.Empty;
    }

    private sealed class FakeRecognitionSettings : ISmartBpRecognitionSettingsService
    {
        public required SmartBpRecognitionSettings Settings { get; init; }
        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
