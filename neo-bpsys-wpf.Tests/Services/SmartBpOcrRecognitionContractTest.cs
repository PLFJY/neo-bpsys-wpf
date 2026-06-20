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
using SmartBpOcrRegionParser = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpOcrRegionParser;
using SmartBpOcrTextResolver = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpOcrTextResolver;
using SmartBpOcrCandidateMatcher = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpOcrCandidateMatcher;
using TesseractCoordinateMapper = smartbp::neo_bpsys_wpf.Services.TesseractCoordinateMapper;
using SmartBpOcrProviderSelector = smartbp::neo_bpsys_wpf.Services.SmartBpOcrProviderSelector;
using ISmartBpRecognitionSettingsService = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpRecognitionSettingsService;
using SmartBpRecognitionSettings = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognitionSettings;
using SmartBpOcrProviderMode = smartbp::neo_bpsys_wpf.Core.Abstractions.Services.SmartBpOcrProviderMode;
using SmartBpOcrProviderKind = smartbp::neo_bpsys_wpf.Core.Abstractions.Services.SmartBpOcrProviderKind;
using IOcrProvider = smartbp::neo_bpsys_wpf.Core.Abstractions.Services.IOcrProvider;
using OcrRecognitionOptions = smartbp::neo_bpsys_wpf.Core.Abstractions.Services.OcrRecognitionOptions;
using SmartBpRecognitionRegion = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognitionRegion;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class SmartBpOcrRecognitionContractTest
{
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
    public void RightTopLinesProduceSurvivorBans()
    {
        var parser = Parser(
            new Character("小说家", Camp.Sur, "novelist"),
            new Character("昆虫学者", Camp.Sur, "entomologist"));

        var result = parser.Parse(
            SmartBpRecognitionRegion.RightTop,
            [Line("小说家", 20, 40), Line("昆虫学者", 80, 40)],
            []);

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

        Assert.Equal("心理学家", result.ResolvedCharacterKey);
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
    public void ProviderSelectorUsesOnlyExplicitSelection(SmartBpOcrProviderMode mode, SmartBpOcrProviderKind expected)
    {
        var settings = new FakeRecognitionSettings { Settings = new SmartBpRecognitionSettings { OcrProviderMode = mode } };
        var paddle = new FakeProvider(SmartBpOcrProviderKind.Paddle, true);
        var tesseract = new FakeProvider(SmartBpOcrProviderKind.Tesseract, false);
        var selector = new SmartBpOcrProviderSelector([paddle, tesseract], settings);

        Assert.Equal(expected, selector.GetSelectedProvider().Kind);
        Assert.Equal(expected == SmartBpOcrProviderKind.Paddle, selector.GetSelectedProvider().IsReady);
    }

    [Fact]
    public void AliasMapsToCanonicalHunterAndQuotedNameStillResolves()
    {
        var resolver = Resolver(new Character("厂长", Camp.Hun, "hell-ember"));

        var alias = resolver.ResolveCharacterFromLine("广长", Camp.Hun, 0, "Tesseract");
        var quoted = resolver.ResolveCharacterFromLine("“厂长”", Camp.Hun, 0, "Paddle");

        Assert.Equal("厂长", alias.ResolvedCharacterKey);
        Assert.Equal("厂长", quoted.ResolvedCharacterKey);
        Assert.Contains(alias.Warnings, item => item.Contains("matchMode=alias", StringComparison.Ordinal));
    }

    [Fact]
    public void HunterAliasDoesNotResolveInSurvivorContext()
    {
        var resolver = Resolver(new Character("厂长", Camp.Hun, "hell-ember"));

        var result = resolver.ResolveCharacterFromLine("广长", Camp.Sur, 0, "Tesseract");

        Assert.Null(result.ResolvedCharacterKey);
        Assert.False(result.IsAutoApplySafe);
    }

    [Fact]
    public void PhaseTextIsFilteredBeforeCharacterMatching()
    {
        var resolver = Resolver(new Character("求生者", Camp.Sur, "survivor"));

        var result = resolver.ResolveCharacterFromLine("屏蔽求生者", Camp.Sur, 0, "Paddle");

        Assert.Null(result.ResolvedCharacterKey);
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

    private static SmartBpOcrRegionParser Parser(params Character[] characters) =>
        new(Resolver(characters));

    private static SmartBpOcrTextResolver Resolver(params Character[] characters)
    {
        var shared = new Mock<ISharedDataService>();
        shared.SetupGet(x => x.SurCharaDict).Returns(new SortedDictionary<string, Character>(characters.Where(x => x.Camp == Camp.Sur).ToDictionary(x => x.Name)));
        shared.SetupGet(x => x.HunCharaDict).Returns(new SortedDictionary<string, Character>(characters.Where(x => x.Camp == Camp.Hun).ToDictionary(x => x.Name)));
        var matcher = new SmartBpOcrCandidateMatcher(shared.Object, Mock.Of<ILogger<SmartBpOcrCandidateMatcher>>());
        return new SmartBpOcrTextResolver(matcher);
    }

    private static OcrTextLine Line(string text, double x, double y) =>
        new(text, .98, new Rect((int)x - 5, (int)y - 5, 10, 10), x, y);

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
