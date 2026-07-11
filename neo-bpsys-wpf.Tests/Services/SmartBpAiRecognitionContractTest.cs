extern alias smartbp;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.Tests.Infrastructure;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;
using SmartBpRecognitionTask = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognitionTask;
using SmartBpRecognitionStrategy = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognitionStrategy;
using SmartBpHybridFusionMode = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpHybridFusionMode;
using SmartBpBusinessAiFusionOutputContract = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpBusinessAiFusionOutputContract;
using LocalVisionModelFamily = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.LocalVisionModelFamily;
using LocalVisionModelRole = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.LocalVisionModelRole;
using QwenMmprojMode = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.QwenMmprojMode;
using SmartBpRecognitionPromptBuilder = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpRecognitionPromptBuilder;
using SmartBpCharacterResolver = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpCharacterResolver;
using SmartBpRecognitionJsonSchemaProvider = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpRecognitionJsonSchemaProvider;
using QwenModelAssetManager = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.QwenModelAssetManager;
using QwenModelManifestProvider = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.QwenModelManifestProvider;
using SmartBpPromptProfileProvider = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpPromptProfileProvider;
using LlamaCppRuntimeManifestProvider = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.LlamaCppRuntimeManifestProvider;
using LlamaCppRuntimeAssetManager = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.LlamaCppRuntimeAssetManager;
using SmartBpAiRecognitionService = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpAiRecognitionService;
using ISmartBpImageEncoder = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpImageEncoder;
using ILlamaCppOpenAiClient = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ILlamaCppOpenAiClient;
using ISmartBpCharacterResolver = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpCharacterResolver;
using ISmartBpPlayerIdentityMatcher = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpPlayerIdentityMatcher;
using SmartBpPlayerIdentityMatcher = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpPlayerIdentityMatcher;
using SmartBpPlayerIdentityMatchResult = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpPlayerIdentityMatchResult;
using ISmartBpRecognitionSettingsService = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpRecognitionSettingsService;
using ISmartBpRecognitionLedger = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpRecognitionLedger;
using SmartBpRecognitionSettings = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognitionSettings;
using SmartBpStageDetectionResult = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpStageDetectionResult;
using SmartBpBusinessStateRecognitionResult = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpBusinessStateRecognitionResult;
using SmartBpRecognizedCharacterSlot = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognizedCharacterSlot;
using SmartBpRecognizedPlayerCharacterSlot = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognizedPlayerCharacterSlot;
using SmartBpBusinessStateParser = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpBusinessStateParser;
using SmartBpAutomaticParser = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpAutomaticParser;
using SmartBpAutomaticMapping = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpAutomaticMapping;
using SmartBpGuidanceSyncService = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpGuidanceSyncService;
using SmartBpCandidateOperationBuilder = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpCandidateOperationBuilder;
using SmartBpDetectedOperationApplier = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpDetectedOperationApplier;
using SmartBpDetectedOperation = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpDetectedOperation;
using SmartBpDetectedOperationKind = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpDetectedOperationKind;
using SmartBpRecognitionLayoutProfile = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognitionLayoutProfile;
using SmartBpRecognitionRegionRect = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognitionRegionRect;
using SmartBpRecognitionRegion = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognitionRegion;
using SmartBpRecognitionFrameCropper = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpRecognitionFrameCropper;
using ISmartBpRecognitionRegionProfileService = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpRecognitionRegionProfileService;
using SmartBpModuleContentViewModel = smartbp::neo_bpsys_wpf.ViewModels.Pages.SmartBpModuleContentViewModel;
using SmartBpPhaseRecognitionResult = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpPhaseRecognitionResult;
using SmartBpFocusedBusinessExtractionResult = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpFocusedBusinessExtractionResult;
using SmartBpBusinessStateMerger = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpBusinessStateMerger;
using SmartBpWorkflowBackfillService = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpWorkflowBackfillService;
using SmartBpRecognitionLedger = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpRecognitionLedger;
using SmartBpWorkflowOperationKey = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpWorkflowOperationKey;
using SmartBpSnapshotDeltaRequest = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpSnapshotDeltaRequest;
using SmartBpSnapshotDeltaResult = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpSnapshotDeltaResult;
using SmartBpSnapshotFieldUpdate = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpSnapshotFieldUpdate;
using SmartBpSnapshotDeltaSlot = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpSnapshotDeltaSlot;
using SmartBpRecognitionStateStore = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpRecognitionStateStore;
using SmartBpAiOcrTranscriptRecognitionService = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpAiOcrTranscriptRecognitionService;
using SmartBpBusinessAiFusionService = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpBusinessAiFusionService;
using SmartBpBusinessAiFusionValidator = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpBusinessAiFusionValidator;
using SmartBpAiOcrTranscriptRegionEvidence = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpAiOcrTranscriptRegionEvidence;
using AiStructuredOutputMode = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.AiStructuredOutputMode;
using LlamaCppOpenAiClient = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.LlamaCppOpenAiClient;
using ISmartBpBusinessAiFusionValidator = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpBusinessAiFusionValidator;
using ISmartBpDebugLog = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpDebugLog;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class SmartBpAiRecognitionContractTest
{
    [Fact]
    public void CharacterResolverUsesSafeNormalizedMatchWithoutFuzzyGuessing()
    {
        var shared = new Mock<ISharedDataService>();
        shared.SetupGet(x => x.SurCharaDict).Returns(new SortedDictionary<string, Character>
        {
            ["Test Name"] = new("Test Name", Camp.Sur, "test")
        });
        shared.SetupGet(x => x.HunCharaDict).Returns([]);
        var resolver = CreateResolverFromShared(shared.Object);

        var normalized = resolver.Resolve("test-name", Camp.Sur, 0, .9);
        var unresolved = resolver.Resolve("unknown", Camp.Sur, 1, .4);

        Assert.Equal("Test Name", normalized.ResolvedCharacterName);
        Assert.Null(unresolved.ResolvedCharacterName);
    }

    [Theory]
    [InlineData(SmartBpRecognitionTask.PickSur)]
    [InlineData(SmartBpRecognitionTask.FullBpScan)]
    public void RecognitionTasksHaveStrictBpOnlySchemas(SmartBpRecognitionTask task)
    {
        var schema = SmartBpRecognitionJsonSchemaProvider.Get(task, ["心理学家", "小说家"], ["厂长", "梦之女巫"]).ToJsonString();
        Assert.Contains("\"additionalProperties\":false", schema);
        Assert.Contains("\"phase\"", schema);
        Assert.Contains("\"banned_sur\"", schema);
        Assert.Contains("\"banned_hun\"", schema);
        Assert.Contains("\"picked_sur\"", schema);
        Assert.Contains("\"picked_hun\"", schema);
        Assert.DoesNotContain("\"all_player_ids\"", schema);
        Assert.DoesNotContain("\"raw_visible_text\"", schema);
        Assert.DoesNotContain("MapBP", schema, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SnapshotDeltaSchemaOnlyAllowsRequestedFields()
    {
        var schema = SmartBpRecognitionJsonSchemaProvider.GetSnapshotDelta(["picked_sur"], ["心理学家"], ["厂长"], true);
        var text = schema.ToJsonString();

        Assert.Contains("\"phase\"", text);
        Assert.Contains("\"updates\"", text);
        Assert.Contains("\"picked_sur\"", text);
        Assert.DoesNotContain("\"banned_sur\"", text);
        Assert.Contains("\"picked_hun\"", text);
    }

    [Fact]
    public void SnapshotDeltaSchemaCanUseFastStringCharacterNames()
    {
        var schema = SmartBpRecognitionJsonSchemaProvider.GetSnapshotDelta(["picked_sur"], ["心理学家"], ["厂长"], false).ToJsonString();

        Assert.Contains("\"character_name\":{\"type\":\"string\"}", schema);
        Assert.DoesNotContain("\"enum\":[\"心理学家\"", schema);
        Assert.Contains("\"slot_state\"", schema);
        Assert.Contains("\"enum\":[\"selected\",\"empty\",\"unknown\"]", schema);
        Assert.Contains("\"minItems\":4", schema);
        Assert.Contains("\"maxItems\":4", schema);
    }

    [Fact]
    public void SnapshotDeltaPromptDefinesStrictMultiImageResponsibilitiesAndLaterBanExamples()
    {
        var current = Business("屏蔽求生者");
        current.BannedSur[0].CharacterName = "小说家";
        current.BannedSur[1].CharacterName = "昆虫学者";
        var request = new SmartBpSnapshotDeltaRequest(
            [
                (SmartBpRecognitionRegion.RightTop, "banned_sur"),
                (SmartBpRecognitionRegion.LeftTop, "banned_hun"),
                (SmartBpRecognitionRegion.LeftBottom, "picked_sur"),
                (SmartBpRecognitionRegion.RightBottom, "picked_hun")
            ],
            [],
            current);

        var prompt = SmartBpRecognitionPromptBuilder.BuildSnapshotDelta(
            request,
            ["小说家", "昆虫学者", "入殓师", "祭司", "心理学家", "守墓人"],
            ["梦之女巫", "女王蜂"]);

        Assert.Contains("image_0 = phase_top", prompt);
        Assert.Contains("Only determine phase", prompt);
        Assert.Contains("Never output characters from phase_top", prompt);
        Assert.Contains("right_top -> banned_sur", prompt);
        Assert.Contains("left_top -> banned_hun", prompt);
        Assert.Contains("left_bottom -> picked_sur", prompt);
        Assert.Contains("right_bottom -> picked_hun", prompt);
        Assert.Contains("banned_sur has exactly 4 slots: index 0,1,2,3", prompt);
        Assert.Contains("Slot order is visual left-to-right", prompt);
        Assert.Contains("Index 2 may be a later-round ban", prompt);
        Assert.Contains("Index 3 may be a later-round ban", prompt);
        Assert.Contains("Dark/disabled/red-ban old slots are still selected bans, not empty", prompt);
        Assert.Contains("banned_sur[2] slot_state=selected character_name=入殓师", prompt);
        Assert.Contains("Output all four selected slots", prompt);
        Assert.Contains("current_known_state", prompt);
    }

    [Fact]
    public void SnapshotDeltaPromptCanOmitCandidateListsForSmallContextModels()
    {
        var request = new SmartBpSnapshotDeltaRequest([(SmartBpRecognitionRegion.RightTop, "banned_sur")], [], Business("屏蔽求生者"));

        var prompt = SmartBpRecognitionPromptBuilder.BuildSnapshotDelta(
            request,
            ["小说家", "昆虫学者", "入殓师"],
            ["梦之女巫"],
            includeCandidateLists: false);

        Assert.Contains("candidate_lists: omitted", prompt);
        Assert.Contains("Output the visible character name text", prompt);
        Assert.DoesNotContain("local resolver", prompt);
        Assert.DoesNotContain("survivor_candidates", prompt);
        Assert.DoesNotContain("hunter_candidates", prompt);
        Assert.DoesNotContain("\"小说家\"", prompt);
    }

    [Fact]
    public void SnapshotDeltaSchemaUsesFieldSpecificSlotShapesAndRequiresSlotState()
    {
        var schema = SmartBpRecognitionJsonSchemaProvider.GetSnapshotDelta(["banned_sur", "banned_hun", "picked_hun"], ["小说家"], ["梦之女巫"], true);
        var updateShapes = schema["properties"]?["updates"]?["items"]?["oneOf"]?.AsArray()
            ?? throw new InvalidDataException("Snapshot delta schema must use update oneOf.");

        var bannedSur = FindUpdateShape(updateShapes, "banned_sur");
        var bannedHun = FindUpdateShape(updateShapes, "banned_hun");
        var pickedHun = FindUpdateShape(updateShapes, "picked_hun");

        Assert.Equal(4, bannedSur["properties"]?["slots"]?["minItems"]?.GetValue<int>());
        Assert.Equal(4, bannedSur["properties"]?["slots"]?["maxItems"]?.GetValue<int>());
        Assert.Equal([0, 1, 2, 3], SlotIndexEnum(bannedSur));
        Assert.Contains("slot_state", RequiredProperties(bannedSur));

        Assert.Equal(2, bannedHun["properties"]?["slots"]?["minItems"]?.GetValue<int>());
        Assert.Equal(2, bannedHun["properties"]?["slots"]?["maxItems"]?.GetValue<int>());
        Assert.Equal([0, 1], SlotIndexEnum(bannedHun));
        Assert.Contains("slot_state", RequiredProperties(bannedHun));

        var pickedHunText = pickedHun.ToJsonString();
        Assert.Contains("\"slots\":{\"type\":\"null\"}", pickedHunText);
        Assert.Contains("\"picked_hun\"", pickedHunText);
        Assert.Contains("\"index\"", pickedHunText);
        Assert.Contains("\"enum\":[0]", pickedHunText);
    }

    [Fact]
    public void BusinessAiFusionSchemaLocksPhaseAndUsesStrictFieldShapes()
    {
        var schema = SmartBpRecognitionJsonSchemaProvider.GetBusinessAiFusionSnapshotDelta(
            "屏蔽求生者", ["banned_sur", "banned_hun", "picked_sur", "picked_hun"], ["小说家"], ["厂长"], true);
        var updateShapes = schema["properties"]?["updates"]?["items"]?["oneOf"]?.AsArray()
            ?? throw new InvalidDataException("Fusion schema must use oneOf update shapes.");

        Assert.Equal("屏蔽求生者", schema["properties"]?["phase"]?["const"]?.GetValue<string>());
        Assert.Equal(4, FindUpdateShape(updateShapes, "banned_sur")["properties"]?["slots"]?["minItems"]?.GetValue<int>());
        Assert.Equal(2, FindUpdateShape(updateShapes, "banned_hun")["properties"]?["slots"]?["minItems"]?.GetValue<int>());
        Assert.Equal(4, FindUpdateShape(updateShapes, "picked_sur")["properties"]?["slots"]?["minItems"]?.GetValue<int>());
        Assert.Equal("null", FindUpdateShape(updateShapes, "picked_hun")["properties"]?["slots"]?["type"]?.GetValue<string>());
        Assert.All(updateShapes, shape => Assert.False(shape?["additionalProperties"]?.GetValue<bool>()));
    }

    [Fact]
    public void BusinessAiFusionParserOverridesChangedPhaseAndWarns()
    {
        const string raw = """
        {"phase":"等待中","updates":[{"field":"banned_sur","slots":[
          {"index":0,"slot_state":"selected","character_name":"小说家","player_id":null},
          {"index":1,"slot_state":"empty","character_name":"未选择","player_id":null},
          {"index":2,"slot_state":"unknown","character_name":"未选择","player_id":null},
          {"index":3,"slot_state":"empty","character_name":"未选择","player_id":null}
        ],"picked_hun":null}]}
        """;

        var parsed = SmartBpAutomaticParser.ParseBusinessAiFusionSnapshotDelta(
            raw, "屏蔽求生者", ["banned_sur"], ["小说家"], ["厂长"], Mock.Of<ICharacterSelectionService>(), SmartBpBusinessAiFusionOutputContract.SnapshotDelta, out var diagnostics);

        Assert.Equal("屏蔽求生者", parsed.Phase);
        Assert.Contains("Business AI fusion changed phase from 屏蔽求生者 to 等待中; overridden to 屏蔽求生者.", diagnostics);
    }

    [Theory]
    [InlineData("picked_hun", "\"picked_hun\":{\"index\":0,\"slot_state\":\"empty\",\"character_name\":\"未选择\",\"player_id\":null}")]
    [InlineData("banned_hun", "\"picked_hun\":null,\"banned_hun\":[]")]
    public void BusinessAiFusionParserRejectsMixedFieldProperties(string unexpectedProperty, string updateTail)
    {
        var raw = $$"""
        {"phase":"屏蔽求生者","updates":[{"field":"banned_sur","slots":[
          {"index":0,"slot_state":"empty","character_name":"未选择","player_id":null},
          {"index":1,"slot_state":"empty","character_name":"未选择","player_id":null},
          {"index":2,"slot_state":"empty","character_name":"未选择","player_id":null},
          {"index":3,"slot_state":"empty","character_name":"未选择","player_id":null}
        ],{{updateTail}}}]}
        """;

        var error = Assert.Throws<InvalidDataException>(() => SmartBpAutomaticParser.ParseBusinessAiFusionSnapshotDelta(
            raw, "屏蔽求生者", ["banned_sur"], ["小说家"], ["厂长"], Mock.Of<ICharacterSelectionService>(), SmartBpBusinessAiFusionOutputContract.SnapshotDelta, out _));

        Assert.Contains($"contained unexpected property {unexpectedProperty}", error.Message);
    }

    [Fact]
    public void BusinessAiFusionDefaultsToPromptRepairAndStrictModeIsExplicit()
    {
        var settings = new SmartBpRecognitionSettings();
        var schema = new JsonObject { ["type"] = "object" };

        var defaultBody = LlamaCppOpenAiClient.CreateStructuredTextBody(
            "system", "prompt", schema, 512, settings.BusinessAiFusionStructuredOutputMode, "fusion");
        var strictBody = LlamaCppOpenAiClient.CreateStructuredTextBody(
            "system", "prompt", schema, 512, AiStructuredOutputMode.JsonSchemaStrict, "fusion");

        Assert.Equal(AiStructuredOutputMode.JsonPromptAndRepair, settings.BusinessAiFusionStructuredOutputMode);
        Assert.False(defaultBody.ContainsKey("response_format"));
        Assert.NotNull(strictBody["response_format"]);
    }

    [Fact]
    public void BusinessAiFusionValidatorAcceptsFourUpdatesAndNormalizesSlotNames()
    {
        const string raw = """
        {"phase":"等待中","updates":[
          {"field":"banned_sur","slots":[
            {"index":0,"slot_state":"selected","character_name":"小说家OCR","player_id":null},
            {"index":1,"slot_state":"empty","character_name":"noise","player_id":null},
            {"index":2,"slot_state":"unknown","character_name":"unknown","player_id":null},
            {"index":3,"slot_state":"empty","character_name":"未选择","player_id":null}],"picked_hun":null},
          {"field":"banned_hun","slots":[
            {"index":0,"slot_state":"empty","character_name":"未选择","player_id":null},
            {"index":1,"slot_state":"unknown","character_name":"noise","player_id":null}],"picked_hun":null},
          {"field":"picked_sur","slots":[
            {"index":0,"slot_state":"unknown","character_name":"未选择","player_id":null},
            {"index":1,"slot_state":"unknown","character_name":"未选择","player_id":null},
            {"index":2,"slot_state":"unknown","character_name":"未选择","player_id":null},
            {"index":3,"slot_state":"unknown","character_name":"未选择","player_id":null}],"picked_hun":null},
          {"field":"picked_hun","slots":null,"picked_hun":{"index":0,"slot_state":"empty","character_name":"noise","player_id":null}}
        ]}
        """;
        var shared = CreateShared(
            new Game(new Team(Camp.Sur, TeamType.HomeTeam), new Team(Camp.Hun, TeamType.AwayTeam), GameProgress.Free),
            new Character("小说家", Camp.Sur, "novelist"),
            new Character("厂长", Camp.Hun, "hell-ember"));
        var selection = new Mock<ICharacterSelectionService>();
        selection.Setup(service => service.ResolveCharacterDetailed("小说家OCR", Camp.Sur))
            .Returns(new CharacterResolveResult("小说家OCR", Camp.Sur, shared.Object.SurCharaDict["小说家"], "小说家", "novelist", 1, "test", true, "test"));
        ISmartBpBusinessAiFusionValidator validator = new SmartBpBusinessAiFusionValidator(shared.Object, selection.Object);

        var delta = validator.ValidateAndNormalize(
            raw,
            "屏蔽求生者",
            ["banned_sur", "banned_hun", "picked_sur", "picked_hun"],
            Business("屏蔽求生者"),
            SmartBpBusinessAiFusionOutputContract.SnapshotDelta,
            out var diagnostics);

        Assert.Equal("屏蔽求生者", delta.Phase);
        Assert.Equal(4, delta.Updates.Count);
        Assert.Equal("小说家", delta.Updates.Single(update => update.Field == "banned_sur").Slots![0].CharacterName);
        Assert.Equal("未选择", delta.Updates.Single(update => update.Field == "banned_sur").Slots![1].CharacterName);
        Assert.Equal("未选择", delta.Updates.Single(update => update.Field == "banned_hun").Slots![1].CharacterName);
        Assert.Equal("未选择", delta.Updates.Single(update => update.Field == "picked_hun").PickedHun!.CharacterName);
        Assert.Contains(diagnostics, message => message.Contains("overridden to 屏蔽求生者", StringComparison.Ordinal));
        selection.Verify(service => service.ResolveCharacterDetailed("小说家OCR", Camp.Sur), Times.Once);
    }

    [Fact]
    public void BusinessAiFusionValidatorAcceptsFullStateRootFieldsAndConvertsToDelta()
    {
        const string raw = """
        {
          "phase": "屏蔽求生者",
          "banned_sur": ["小说家", "昆虫学者", "未选择", "未选择"],
          "banned_hun": ["未选择", "未选择"],
          "picked_sur": ["未选择", "未选择", "未选择", "未选择"],
          "picked_hun": {"index":0,"character_name":"未选择","player_id":"null"}
        }
        """;
        var shared = CreateShared(
            new Game(new Team(Camp.Sur, TeamType.HomeTeam), new Team(Camp.Hun, TeamType.AwayTeam), GameProgress.Free),
            new Character("小说家", Camp.Sur, "novelist"),
            new Character("昆虫学者", Camp.Sur, "entomologist"),
            new Character("厂长", Camp.Hun, "hell-ember"));
        ISmartBpBusinessAiFusionValidator validator = new SmartBpBusinessAiFusionValidator(shared.Object, Mock.Of<ICharacterSelectionService>());

        var delta = validator.ValidateAndNormalize(
            raw,
            "屏蔽求生者",
            ["banned_sur", "banned_hun", "picked_sur", "picked_hun"],
            Business("屏蔽求生者"),
            SmartBpBusinessAiFusionOutputContract.FullBusinessState,
            out var diagnostics);

        Assert.Equal("屏蔽求生者", delta.Phase);
        Assert.Equal(["banned_sur", "banned_hun", "picked_sur", "picked_hun"], delta.Updates.Select(update => update.Field));
        var bannedSur = delta.Updates.Single(update => update.Field == "banned_sur").Slots!;
        Assert.Equal("selected", bannedSur[0].SlotState);
        Assert.Equal("小说家", bannedSur[0].CharacterName);
        Assert.Equal("selected", bannedSur[1].SlotState);
        Assert.Equal("昆虫学者", bannedSur[1].CharacterName);
        Assert.Equal("empty", bannedSur[2].SlotState);
        Assert.Equal("empty", delta.Updates.Single(update => update.Field == "picked_hun").PickedHun!.SlotState);
        Assert.Null(delta.Updates.Single(update => update.Field == "picked_hun").PickedHun!.PlayerId);
        Assert.Contains("full-state contract; normalized to snapshot delta", string.Join("\n", diagnostics));
    }

    [Fact]
    public void BusinessAiFusionValidatorNormalizesPickedSurAlternatingPlayerIdsAndMergesThroughStateStore()
    {
        const string raw = """
        {
          "phase": "屏蔽求生者",
          "banned_sur": ["小说家", "昆虫学者", "未选择", "未选择"],
          "banned_hun": ["未选择", "未选择"],
          "picked_sur": ["未选择", "IHiganbanal", "未选择", "夜风之缚", "未选择", "磁台小狗", "未选择", "叶落摘星"],
          "picked_hun": {"index":0,"character_name":"未选择","player_id":"NULL"}
        }
        """;
        var shared = CreateShared(
            new Game(new Team(Camp.Sur, TeamType.HomeTeam), new Team(Camp.Hun, TeamType.AwayTeam), GameProgress.Free),
            new Character("小说家", Camp.Sur, "novelist"),
            new Character("昆虫学者", Camp.Sur, "entomologist"),
            new Character("厂长", Camp.Hun, "hell-ember"));
        ISmartBpBusinessAiFusionValidator validator = new SmartBpBusinessAiFusionValidator(shared.Object, Mock.Of<ICharacterSelectionService>());

        var delta = validator.ValidateAndNormalize(
            raw,
            "屏蔽求生者",
            ["banned_sur", "banned_hun", "picked_sur", "picked_hun"],
            Business("屏蔽求生者"),
            SmartBpBusinessAiFusionOutputContract.FullBusinessState,
            out _);
        var stateStore = new SmartBpRecognitionStateStore();
        stateStore.ApplyDelta(delta, 1, DateTimeOffset.Now);
        var snapshot = stateStore.Snapshot;

        Assert.Equal("IHiganbanal", delta.Updates.Single(update => update.Field == "picked_sur").Slots![0].PlayerId);
        Assert.Equal("夜风之缚", delta.Updates.Single(update => update.Field == "picked_sur").Slots![1].PlayerId);
        Assert.Equal("磁台小狗", delta.Updates.Single(update => update.Field == "picked_sur").Slots![2].PlayerId);
        Assert.Equal("叶落摘星", delta.Updates.Single(update => update.Field == "picked_sur").Slots![3].PlayerId);
        Assert.Equal("小说家", snapshot.BannedSur[0].CharacterName);
        Assert.Equal("昆虫学者", snapshot.BannedSur[1].CharacterName);
        Assert.Equal("IHiganbanal", snapshot.PickedSur[0].PlayerId);
        Assert.Null(snapshot.PickedHun.PlayerId);
    }

    [Fact]
    public void BusinessAiFusionValidatorAcceptsShorthandUpdatesObjectAndConvertsToCanonicalDelta()
    {
        const string raw = """
        {
          "phase": "屏蔽求生者",
          "updates": {
            "banned_sur": ["小说家", "昆虫学者", "未选择", "未选择"],
            "banned_hun": ["未选择", "未选择"],
            "picked_sur": ["未选择", "IHiganbanal", "未选择", "夜风之缚", "未选择", "磁台小狗", "未选择", "叶落摘星"],
            "picked_hun": {"index":0,"character_name":"未选择","slots":null,"player_id":"null"}
          }
        }
        """;
        var shared = CreateShared(
            new Game(new Team(Camp.Sur, TeamType.HomeTeam), new Team(Camp.Hun, TeamType.AwayTeam), GameProgress.Free),
            new Character("小说家", Camp.Sur, "novelist"),
            new Character("昆虫学者", Camp.Sur, "entomologist"),
            new Character("厂长", Camp.Hun, "hell-ember"));
        ISmartBpBusinessAiFusionValidator validator = new SmartBpBusinessAiFusionValidator(shared.Object, Mock.Of<ICharacterSelectionService>());

        var delta = validator.ValidateAndNormalize(
            raw,
            "屏蔽求生者",
            ["banned_sur", "banned_hun", "picked_sur", "picked_hun"],
            Business("屏蔽求生者"),
            SmartBpBusinessAiFusionOutputContract.SnapshotDelta,
            out var diagnostics);
        var stateStore = new SmartBpRecognitionStateStore();
        stateStore.ApplyDelta(delta, 1, DateTimeOffset.Now);
        var snapshot = stateStore.Snapshot;

        Assert.Equal(["banned_sur", "banned_hun", "picked_sur", "picked_hun"], delta.Updates.Select(update => update.Field));
        Assert.Equal("小说家", delta.Updates.Single(update => update.Field == "banned_sur").Slots![0].CharacterName);
        Assert.Equal("昆虫学者", delta.Updates.Single(update => update.Field == "banned_sur").Slots![1].CharacterName);
        Assert.Equal("empty", delta.Updates.Single(update => update.Field == "banned_hun").Slots![0].SlotState);
        Assert.Equal("IHiganbanal", delta.Updates.Single(update => update.Field == "picked_sur").Slots![0].PlayerId);
        Assert.Null(delta.Updates.Single(update => update.Field == "picked_hun").PickedHun!.PlayerId);
        Assert.Equal("屏蔽求生者", snapshot.Phase);
        Assert.Equal("小说家", snapshot.BannedSur[0].CharacterName);
        Assert.Contains("shorthand updates object; normalized to canonical updates array", string.Join("\n", diagnostics));
    }

    [Fact]
    public async Task BusinessAiFusionFullStateRetryPromptDoesNotAskForUpdates()
    {
        const string invalid = """
        {"phase":"屏蔽求生者","banned_sur":["小说家"]}
        """;
        const string repaired = """
        {"phase":"屏蔽求生者","banned_sur":["小说家","未选择","未选择","未选择"],"banned_hun":["未选择","未选择"],"picked_sur":["未选择","未选择","未选择","未选择"],"picked_hun":{"index":0,"character_name":"未选择","player_id":"null"}}
        """;
        var game = new Game(new Team(Camp.Sur, TeamType.HomeTeam), new Team(Camp.Hun, TeamType.AwayTeam), GameProgress.Free);
        var shared = CreateShared(game, new Character("小说家", Camp.Sur, "novelist"), new Character("厂长", Camp.Hun, "hell-ember"));
        var validator = new SmartBpBusinessAiFusionValidator(shared.Object, Mock.Of<ICharacterSelectionService>());
        var settings = new Mock<ISmartBpRecognitionSettingsService>();
        settings.SetupGet(service => service.Settings).Returns(new SmartBpRecognitionSettings());
        var prompts = new List<string>();
        var call = 0;
        var client = new Mock<ILlamaCppOpenAiClient>();
        client.Setup(service => service.FuseTranscriptEvidenceAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .Returns((string prompt, string _, IReadOnlyCollection<string> _, CancellationToken _) =>
            {
                prompts.Add(prompt);
                return Task.FromResult(call++ == 0 ? invalid : repaired);
            });
        var service = new SmartBpBusinessAiFusionService(
            client.Object, shared.Object, validator, settings.Object, Mock.Of<ISmartBpDebugLog>());

        var result = await service.FuseAsync(
            new SmartBpPhaseRecognitionResult { Phase = "屏蔽求生者" },
            [new SmartBpAiOcrTranscriptRegionEvidence { Region = SmartBpRecognitionRegion.RightTop, Field = "banned_sur", AiOcrModel = "glm-ocr-q4km", RawOutput = "小说家", TechnicalLines = ["小说家"] }],
            ["banned_sur", "banned_hun", "picked_sur", "picked_hun"],
            Business("屏蔽求生者"),
            SmartBpBusinessAiFusionOutputContract.FullBusinessState,
            TestContext.Current.CancellationToken);

        Assert.Contains("complete SmartBP business state", prompts[0]);
        Assert.Contains("Return corrected full business state JSON only.", prompts[1]);
        Assert.Contains("Do not use updates[].", prompts[1]);
        Assert.DoesNotContain("Do not include any fields except phase and updates.", prompts[1]);
        Assert.Equal(4, result.Delta.Updates.Count);
    }

    [Fact]
    public void BusinessAiFusionDeltaRepairPromptExplicitlyRequiresUpdatesArray()
    {
        var root = FindRepositoryRoot();
        var services = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "Services", "Recognition", "RecognitionServices.cs"));

        Assert.Contains("updates MUST be an array, not an object.", services);
        Assert.Contains("Do not output \"updates\": { \"banned_sur\": ... }.", services);
    }

    [Fact]
    public async Task BusinessAiFusionRetriesOnlyTextFusionAfterLocalValidationFailure()
    {
        const string invalid = """
        {"phase":"屏蔽求生者","updates":[{"field":"banned_sur","slots":[],"picked_hun":null}]}
        """;
        const string repaired = """
        {"phase":"屏蔽求生者","updates":[{"field":"banned_sur","slots":[
          {"index":0,"slot_state":"empty","character_name":"未选择","player_id":null},
          {"index":1,"slot_state":"empty","character_name":"未选择","player_id":null},
          {"index":2,"slot_state":"empty","character_name":"未选择","player_id":null},
          {"index":3,"slot_state":"empty","character_name":"未选择","player_id":null}],"picked_hun":null}]}
        """;
        var game = new Game(new Team(Camp.Sur, TeamType.HomeTeam), new Team(Camp.Hun, TeamType.AwayTeam), GameProgress.Free);
        var shared = CreateShared(game, new Character("小说家", Camp.Sur, "novelist"));
        var validator = new SmartBpBusinessAiFusionValidator(shared.Object, Mock.Of<ICharacterSelectionService>());
        var settings = new Mock<ISmartBpRecognitionSettingsService>();
        settings.SetupGet(service => service.Settings).Returns(new SmartBpRecognitionSettings());
        var prompts = new List<string>();
        var call = 0;
        var client = new Mock<ILlamaCppOpenAiClient>();
        client.Setup(service => service.FuseTranscriptEvidenceAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .Returns((string prompt, string _, IReadOnlyCollection<string> _, CancellationToken _) =>
            {
                prompts.Add(prompt);
                return Task.FromResult(call++ == 0 ? invalid : repaired);
            });
        var service = new SmartBpBusinessAiFusionService(
            client.Object, shared.Object, validator, settings.Object, Mock.Of<ISmartBpDebugLog>());
        var evidence = new SmartBpAiOcrTranscriptRegionEvidence
        {
            Region = SmartBpRecognitionRegion.RightTop,
            Field = "banned_sur",
            AiOcrModel = "glm-ocr-q4km",
            RawOutput = "未选择 未选择 未选择 未选择",
            TechnicalLines = ["未选择 未选择 未选择 未选择"]
        };

        var result = await service.FuseAsync(
            new SmartBpPhaseRecognitionResult { Phase = "屏蔽求生者" },
            [evidence],
            ["banned_sur"],
            Business("屏蔽求生者"),
            SmartBpBusinessAiFusionOutputContract.SnapshotDelta,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, prompts.Count);
        Assert.Contains("Your previous output was invalid because", prompts[1]);
        Assert.Contains("未选择 未选择 未选择 未选择", prompts[1]);
        Assert.Contains("attempt_1 raw", result.RawJson);
        Assert.Contains("attempt_2 raw", result.RawJson);
        client.Verify(service => service.FuseTranscriptEvidenceAsync(
            It.IsAny<string>(), "屏蔽求生者", It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public void RecognitionStrategyIsOcrOnly()
    {
        Assert.Equal(
            [SmartBpRecognitionStrategy.PureOcr],
            Enum.GetValues<SmartBpRecognitionStrategy>());
    }

    [Fact]
    public void HybridFusionModesHaveStrategySpecificDefaults()
    {
        var settings = new SmartBpRecognitionSettings();

        Assert.Equal(SmartBpHybridFusionMode.LocalCSharp, settings.AiWithOcrFusionMode);
        Assert.Equal(SmartBpHybridFusionMode.BusinessAi, settings.AiWithAiOcrFusionMode);
        Assert.False(settings.EnableAutoApplyRecognition);
    }

    [Fact]
    public void SnapshotDeltaParserRejectsUnrequestedField()
    {
        var raw = """
{
  "phase": "选择求生者",
  "updates": [
    {
      "field": "banned_sur",
      "slots": [
        { "index": 0, "character_name": "小说家", "player_id": null },
        { "index": 1, "character_name": "未选择", "player_id": null },
        { "index": 2, "character_name": "未选择", "player_id": null },
        { "index": 3, "character_name": "未选择", "player_id": null }
      ],
      "picked_hun": null
    }
  ]
}
""";

        Assert.Throws<InvalidDataException>(() => SmartBpAutomaticParser.ParseSnapshotDelta(raw, ["picked_sur"], ["小说家"], ["厂长"]));
    }

    [Fact]
    public void SnapshotDeltaParserAcceptsSelectedEmptyUnknownAndLegacyMissingSlotState()
    {
        const string raw = """
{
  "phase": "屏蔽求生者",
  "updates": [
    {
      "field": "banned_sur",
      "slots": [
        { "index": 0, "slot_state": "selected", "character_name": "小说家", "player_id": null },
        { "index": 1, "slot_state": "empty", "character_name": "未选择", "player_id": null },
        { "index": 2, "slot_state": "unknown", "character_name": "未选择", "player_id": null },
        { "index": 3, "character_name": "入殓师", "player_id": null }
      ],
      "picked_hun": null
    }
  ]
}
""";

        var parsed = SmartBpAutomaticParser.ParseSnapshotDelta(raw, ["banned_sur"], ["小说家", "入殓师"], []);

        Assert.Equal("selected", parsed.Updates[0].Slots![0].SlotState);
        Assert.Equal("empty", parsed.Updates[0].Slots![1].SlotState);
        Assert.Equal("unknown", parsed.Updates[0].Slots![2].SlotState);
        Assert.Equal("selected", parsed.Updates[0].Slots![3].SlotState);
    }

    [Fact]
    public void SnapshotDeltaParserRejectsInvalidSlotStateCharacterPairs()
    {
        const string selectedUnselected = """
{"phase":"屏蔽求生者","updates":[{"field":"banned_sur","slots":[{"index":0,"slot_state":"selected","character_name":"未选择","player_id":null},{"index":1,"slot_state":"empty","character_name":"未选择","player_id":null},{"index":2,"slot_state":"empty","character_name":"未选择","player_id":null},{"index":3,"slot_state":"empty","character_name":"未选择","player_id":null}],"picked_hun":null}]}
""";
        const string emptySelected = """
{"phase":"屏蔽求生者","updates":[{"field":"banned_sur","slots":[{"index":0,"slot_state":"empty","character_name":"小说家","player_id":null},{"index":1,"slot_state":"empty","character_name":"未选择","player_id":null},{"index":2,"slot_state":"empty","character_name":"未选择","player_id":null},{"index":3,"slot_state":"empty","character_name":"未选择","player_id":null}],"picked_hun":null}]}
""";

        Assert.Throws<InvalidDataException>(() => SmartBpAutomaticParser.ParseSnapshotDelta(selectedUnselected, ["banned_sur"], ["小说家"], []));
        Assert.Throws<InvalidDataException>(() => SmartBpAutomaticParser.ParseSnapshotDelta(emptySelected, ["banned_sur"], ["小说家"], []));
    }

    [Fact]
    public void SnapshotDeltaParserRejectsMismatchedFieldSlotShapes()
    {
        const string bannedSurWithTwoSlots = """
{"phase":"屏蔽求生者","updates":[{"field":"banned_sur","slots":[{"index":0,"slot_state":"empty","character_name":"未选择","player_id":null},{"index":1,"slot_state":"empty","character_name":"未选择","player_id":null}],"picked_hun":null}]}
""";
        const string bannedHunWithFourSlots = """
{"phase":"屏蔽监管者","updates":[{"field":"banned_hun","slots":[{"index":0,"slot_state":"empty","character_name":"未选择","player_id":null},{"index":1,"slot_state":"empty","character_name":"未选择","player_id":null},{"index":2,"slot_state":"empty","character_name":"未选择","player_id":null},{"index":3,"slot_state":"empty","character_name":"未选择","player_id":null}],"picked_hun":null}]}
""";

        Assert.Throws<InvalidDataException>(() => SmartBpAutomaticParser.ParseSnapshotDelta(bannedSurWithTwoSlots, ["banned_sur"], [], []));
        Assert.Throws<InvalidDataException>(() => SmartBpAutomaticParser.ParseSnapshotDelta(bannedHunWithFourSlots, ["banned_hun"], [], []));
    }

    [Fact]
    public void RecognitionStateStorePreservesMissingFieldsAndIgnoresOlderFieldSequences()
    {
        var store = new SmartBpRecognitionStateStore();
        store.ApplyDelta(new SmartBpSnapshotDeltaResult
        {
            Phase = "选择求生者",
            Updates =
            [
                new SmartBpSnapshotFieldUpdate
                {
                    Field = "picked_sur",
                    Slots =
                    [
                        Slot(0, "selected", "心理学家", "A"),
                        Slot(1, "selected", "守墓人", "B"),
                        Slot(2, "empty"),
                        Slot(3, "empty")
                    ]
                }
            ]
        }, 2, DateTimeOffset.Now);

        store.ApplyDelta(new SmartBpSnapshotDeltaResult
        {
            Phase = "屏蔽求生者",
            Updates =
            [
                new SmartBpSnapshotFieldUpdate
                {
                    Field = "picked_sur",
                    Slots =
                    [
                        Slot(0, "empty"),
                        Slot(1, "empty"),
                        Slot(2, "empty"),
                        Slot(3, "empty")
                    ]
                },
                new SmartBpSnapshotFieldUpdate
                {
                    Field = "banned_sur",
                    Slots =
                    [
                        Slot(0, "selected", "小说家"),
                        Slot(1, "empty"),
                        Slot(2, "empty"),
                        Slot(3, "empty")
                    ]
                }
            ]
        }, 1, DateTimeOffset.Now);

        var snapshot = store.Snapshot;
        Assert.Equal("屏蔽求生者", snapshot.Phase);
        Assert.Equal("心理学家", snapshot.PickedSur[0].CharacterName);
        Assert.Equal("小说家", snapshot.BannedSur[0].CharacterName);
        Assert.Equal("未选择", snapshot.BannedHun[0].CharacterName);
    }

    [Fact]
    public void RecognitionStateStoreMergesBannedSurSlotsBySlotState()
    {
        var store = new SmartBpRecognitionStateStore();
        store.ApplyDelta(new SmartBpSnapshotDeltaResult
        {
            Phase = "屏蔽求生者",
            Updates =
            [
                new SmartBpSnapshotFieldUpdate
                {
                    Field = "banned_sur",
                    Slots =
                    [
                        Slot(0, "selected", "小说家"),
                        Slot(1, "selected", "昆虫学者"),
                        Slot(2, "empty"),
                        Slot(3, "empty")
                    ]
                }
            ]
        }, 1, DateTimeOffset.Now);

        var diagnostics = store.ApplyDelta(new SmartBpSnapshotDeltaResult
        {
            Phase = "屏蔽求生者",
            Updates =
            [
                new SmartBpSnapshotFieldUpdate
                {
                    Field = "banned_sur",
                    Slots =
                    [
                        Slot(0, "unknown"),
                        Slot(1, "selected", "昆虫学者"),
                        Slot(2, "selected", "入殓师"),
                        Slot(3, "empty")
                    ]
                }
            ]
        }, 2, DateTimeOffset.Now);

        var snapshot = store.Snapshot;
        Assert.Equal(["小说家", "昆虫学者", "入殓师", "未选择"], snapshot.BannedSur.Select(x => x.CharacterName).ToArray());
        Assert.Contains(diagnostics, message => message.Contains("Preserved banned_sur[0] because slot_state=unknown", StringComparison.Ordinal));
        Assert.Contains(diagnostics, message => message.Contains("Applied banned_sur[2] = 入殓师", StringComparison.Ordinal));
        Assert.Contains(diagnostics, message => message.Contains("Cleared banned_sur[3] because slot_state=empty", StringComparison.Ordinal));
    }

    [Fact]
    public void RecognitionStateStoreMergesThirdRoundBanAndPreservesPickedCharacters()
    {
        var store = new SmartBpRecognitionStateStore();
        store.ApplyDelta(new SmartBpSnapshotDeltaResult
        {
            Phase = "选择求生者",
            Updates =
            [
                new SmartBpSnapshotFieldUpdate
                {
                    Field = "picked_sur",
                    Slots =
                    [
                        Slot(0, "selected", "心理学家", "P1"),
                        Slot(1, "empty"),
                        Slot(2, "empty"),
                        Slot(3, "empty")
                    ]
                },
                new SmartBpSnapshotFieldUpdate
                {
                    Field = "picked_hun",
                    PickedHun = Slot(0, "selected", "梦之女巫", "H1")
                }
            ]
        }, 1, DateTimeOffset.Now);

        store.ApplyDelta(new SmartBpSnapshotDeltaResult
        {
            Phase = "屏蔽求生者",
            Updates =
            [
                new SmartBpSnapshotFieldUpdate
                {
                    Field = "banned_sur",
                    Slots =
                    [
                        Slot(0, "selected", "小说家"),
                        Slot(1, "selected", "昆虫学者"),
                        Slot(2, "selected", "入殓师"),
                        Slot(3, "selected", "祭司")
                    ]
                },
                new SmartBpSnapshotFieldUpdate
                {
                    Field = "picked_sur",
                    Slots =
                    [
                        Slot(0, "unknown"),
                        Slot(1, "empty"),
                        Slot(2, "empty"),
                        Slot(3, "empty")
                    ]
                },
                new SmartBpSnapshotFieldUpdate
                {
                    Field = "picked_hun",
                    PickedHun = Slot(0, "unknown")
                }
            ]
        }, 2, DateTimeOffset.Now);

        var snapshot = store.Snapshot;
        Assert.Equal(["小说家", "昆虫学者", "入殓师", "祭司"], snapshot.BannedSur.Select(x => x.CharacterName).ToArray());
        Assert.Equal("心理学家", snapshot.PickedSur[0].CharacterName);
        Assert.Equal("P1", snapshot.PickedSur[0].PlayerId);
        Assert.Equal("梦之女巫", snapshot.PickedHun.CharacterName);
        Assert.Equal("H1", snapshot.PickedHun.PlayerId);
    }

    [Fact]
    public void QwenDownloadsUseBrowserCompatibleHeaders()
    {
        using var client = new HttpClient();
        QwenModelAssetManager.ConfigureDownloadHeaders(client, new Uri("https://models.example.test/path/model.gguf"));

        Assert.Contains("Mozilla/5.0", client.DefaultRequestHeaders.UserAgent.ToString());
        Assert.Contains(client.DefaultRequestHeaders.Accept, value => value.MediaType == "application/octet-stream");
        Assert.Equal("https://models.example.test/", client.DefaultRequestHeaders.Referrer?.AbsoluteUri);
    }

    [Theory]
    [InlineData("BanSur", "right", "right_top", "hunter", "survivor")]
    [InlineData("BanHun", "left", "left_top", "survivor", "hunter")]
    [InlineData("PickSur", "left", "left_bottom", "survivor", "survivor")]
    [InlineData("DistributeChara", "left", "left_bottom", "survivor", "survivor")]
    [InlineData("PickHun", "right", "right_bottom", "hunter", "hunter")]
    public void StageParserAcceptsBpBusinessMappings(string action, string side, string region, string owner, string camp)
    {
        var json = $$"""
        {"schema_version":1,"recognized_action":"{{action}}","active_side":"{{side}}","operation_region":"{{region}}","operation_owner":"{{owner}}","target_camp":"{{camp}}","left_top_title":null,"right_top_title":null,"main_status":null,"confidence":0.95,"evidence":[],"warnings":[]}
        """;
        var parsed = SmartBpAutomaticParser.ParseStage(json);
        Assert.Equal(action, parsed.RecognizedAction);
    }

    [Fact]
    public void StageParserRejectsUnknownOperationRegion()
    {
        const string json = """
        {"schema_version":1,"recognized_action":"BanSur","active_side":"right","operation_region":"center","operation_owner":"hunter","target_camp":"survivor","left_top_title":null,"right_top_title":"屏蔽求生者","main_status":null,"confidence":0.95,"evidence":[],"warnings":[]}
        """;
        Assert.ThrowsAny<Exception>(() => SmartBpAutomaticParser.ParseStage(json));
    }

    [Theory]
    [InlineData(GameAction.BanSur, "right_top", "survivor")]
    [InlineData(GameAction.BanHun, "left_top", "hunter")]
    [InlineData(GameAction.PickSur, "left_bottom", "survivor")]
    [InlineData(GameAction.DistributeChara, "left_bottom", "survivor")]
    [InlineData(GameAction.PickHun, "right_bottom", "hunter")]
    public void FocusedMappingUsesBusinessRegion(GameAction action, string region, string camp)
    {
        var mapping = SmartBpAutomaticMapping.Get(action);
        Assert.Equal(region, mapping.Region);
        Assert.Equal(camp, mapping.Camp);
    }

    [Fact]
    public async Task GuidanceSyncChoosesNearestForwardStepAndNeverBackward()
    {
        var workflow = new GameGuidanceStepSnapshot[]
        {
            new(0, GameAction.BanSur, [0, 1], 30),
            new(1, GameAction.PickSur, [0], 30),
            new(2, GameAction.BanHun, [0], 30),
            new(3, GameAction.PickSur, [1], 30)
        };
        var guidance = new Mock<IGameGuidanceService>();
        guidance.Setup(x => x.GetRuntimeSnapshot()).Returns(new GameGuidanceRuntimeSnapshot(true, 1, GameAction.PickSur, [0], 30, workflow));
        guidance.Setup(x => x.MoveToStepAsync(2)).ReturnsAsync((string?)null);
        var settings = new Mock<ISmartBpRecognitionSettingsService>();
        settings.SetupGet(x => x.Settings).Returns(new SmartBpRecognitionSettings { GuidanceSyncLookAheadSteps = 4 });
        var service = new SmartBpGuidanceSyncService(guidance.Object, settings.Object);

        var moved = await service.SyncAsync(Business("屏蔽监管者"), TestContext.Current.CancellationToken);
        var refusedBackward = await service.SyncAsync(Business("屏蔽求生者"), TestContext.Current.CancellationToken);

        Assert.True(moved.Changed);
        Assert.Equal(2, moved.TargetStepIndex);
        Assert.False(refusedBackward.IsAccepted);
        guidance.Verify(x => x.MoveToStepAsync(0), Times.Never);
    }

    [Fact]
    public async Task GuidanceSync_PickSurFromBanHun_LogsTargetMoveAndFinalSnapshot()
    {
        var workflow = new GameGuidanceStepSnapshot[]
        {
            new(0, GameAction.BanSur, [0, 1], 30),
            new(1, GameAction.BanHun, [0, 1], 30),
            new(2, GameAction.PickSur, [0, 1], 30)
        };
        var before = new GameGuidanceRuntimeSnapshot(true, 1, GameAction.BanHun, [0, 1], 30, workflow);
        var after = new GameGuidanceRuntimeSnapshot(true, 2, GameAction.PickSur, [0, 1], 30, workflow);
        var guidance = new Mock<IGameGuidanceService>();
        guidance.SetupSequence(x => x.GetRuntimeSnapshot())
            .Returns(before)
            .Returns(after);
        guidance.Setup(x => x.MoveToStepAsync(2, false)).ReturnsAsync((string?)null);
        var settings = new Mock<ISmartBpRecognitionSettingsService>();
        settings.SetupGet(x => x.Settings).Returns(new SmartBpRecognitionSettings
        {
            GuidanceSyncLookAheadSteps = 4,
            EnableAutoGuidancePageNavigation = false
        });
        var diagnostics = new List<string>();
        var debugLog = new Mock<ISmartBpDebugLog>();
        debugLog.Setup(x => x.Write("GuidanceSync", It.IsAny<string>()))
            .Callback<string, string>((_, message) => diagnostics.Add(message));
        var service = new SmartBpGuidanceSyncService(guidance.Object, settings.Object, debugLog.Object);

        var result = await service.SyncAsync(Business("选择求生者"), TestContext.Current.CancellationToken);

        Assert.True(result.Changed);
        Assert.Equal(GameAction.PickSur, result.TargetAction);
        Assert.Equal(2, result.TargetStepIndex);
        Assert.Contains(diagnostics, x => x.Contains("phase=选择求生者 -> action=PickSur", StringComparison.Ordinal));
        Assert.Contains(diagnostics, x => x.Contains("Current guidance: step=1 action=BanHun indexes=[0, 1]", StringComparison.Ordinal));
        Assert.Contains(diagnostics, x => x.Contains("Target guidance: step=2 action=PickSur indexes=[0, 1]", StringComparison.Ordinal));
        Assert.Contains(diagnostics, x => x.Contains("MoveToStepAsync completed: moved=True", StringComparison.Ordinal));
        Assert.Contains(diagnostics, x => x.Contains("Final guidance: step=2 action=PickSur indexes=[0, 1]", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GuidanceSyncRejectsLowConfidence()
    {
        var guidance = new Mock<IGameGuidanceService>();
        var settings = new Mock<ISmartBpRecognitionSettingsService>();
        settings.SetupGet(x => x.Settings).Returns(new SmartBpRecognitionSettings { StageConfidenceThreshold = .8 });
        var result = await new SmartBpGuidanceSyncService(guidance.Object, settings.Object)
            .SyncAsync(Business("未知"), TestContext.Current.CancellationToken);
        Assert.False(result.IsAccepted);
        guidance.Verify(x => x.MoveToStepAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void BusinessSchemaUsesOnlyStateSnapshotFields()
    {
        var schema = SmartBpRecognitionJsonSchemaProvider.Get(SmartBpRecognitionTask.FullBpScan, ["心理学家"], ["厂长"]).ToJsonString();
        Assert.Contains("\"phase\"", schema);
        Assert.Contains("\"banned_sur\"", schema);
        Assert.Contains("\"banned_hun\"", schema);
        Assert.Contains("\"picked_sur\"", schema);
        Assert.Contains("\"picked_hun\"", schema);
        var phaseValues = SmartBpRecognitionJsonSchemaProvider.Get(SmartBpRecognitionTask.FullBpScan, ["心理学家"], ["厂长"])["properties"]?["phase"]?["enum"]?.AsArray().Select(x => x?.GetValue<string>()).ToArray();
        Assert.Contains("求生者选择天赋中", phaseValues);
        Assert.Contains("监管者选择天赋中", phaseValues);
        Assert.Contains("天赋已锁定", phaseValues);
        Assert.DoesNotContain("teams", schema);
        Assert.DoesNotContain("all_characters", schema);
        Assert.DoesNotContain("all_player_ids", schema);
        Assert.DoesNotContain("warnings", schema);
        Assert.DoesNotContain("raw_visible_text", schema);
        Assert.DoesNotContain("confidence", schema);
    }

    [Fact]
    public void BusinessSchemaUsesCandidateEnumsForCharacterNames()
    {
        var schema = SmartBpRecognitionJsonSchemaProvider.Get(SmartBpRecognitionTask.FullBpScan, ["心理学家", "小说家"], ["厂长", "梦之女巫"]);

        Assert.Equal(["心理学家", "小说家", "未选择"], CharacterNameEnum(schema, "banned_sur", isArray: true));
        Assert.Equal(["心理学家", "小说家", "未选择"], CharacterNameEnum(schema, "picked_sur", isArray: true));
        Assert.Equal(["厂长", "梦之女巫", "未选择"], CharacterNameEnum(schema, "banned_hun", isArray: true));
        Assert.Equal(["厂长", "梦之女巫", "未选择"], CharacterNameEnum(schema, "picked_hun", isArray: false));
        Assert.DoesNotContain("任意角色", schema.ToJsonString());
    }

    [Fact]
    public void BundledBpRecognitionLayoutProfileContainsCoarseRegions()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "SmartBp", "BpRecognitionLayoutProfile.json");
        var profile = JsonSerializer.Deserialize<SmartBpRecognitionLayoutProfile>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(profile);
        Assert.Contains("phase_top", profile!.Regions.Keys);
        Assert.Contains("top_center_status", profile.Regions.Keys);
        Assert.Contains("top_left_status", profile.Regions.Keys);
        Assert.Contains("left_top", profile.Regions.Keys);
        Assert.Contains("right_top", profile.Regions.Keys);
        Assert.Contains("left_bottom", profile.Regions.Keys);
        Assert.Contains("right_bottom", profile.Regions.Keys);
        Assert.All(profile.Regions.Values, rect =>
        {
            Assert.InRange(rect.X, 0, 1);
            Assert.InRange(rect.Y, 0, 1);
            Assert.InRange(rect.Width, double.Epsilon, 1);
            Assert.InRange(rect.Height, double.Epsilon, 1);
            Assert.InRange(rect.X + rect.Width, 0, 1);
            Assert.InRange(rect.Y + rect.Height, 0, 1);
        });
        Assert.False(Overlaps(profile.Regions["right_top"], profile.Regions["left_bottom"]));
        Assert.False(Overlaps(profile.Regions["left_top"], profile.Regions["right_bottom"]));
        var status = profile.Regions["top_left_status"];
        Assert.Equal(0, status.X);
        Assert.Equal(0, status.Y);
        Assert.InRange(status.Width, .32, .38);
        Assert.InRange(status.Height, .10, .12);
    }

    [Fact]
    public void VisualRecognitionRegionEditorIncludesLifecycleAndTopLeftStatus()
    {
        var field = typeof(SmartBpModuleContentViewModel).GetField(
            "AiRegionEditorNodes", BindingFlags.Static | BindingFlags.NonPublic);

        var nodes = Assert.IsType<(string Id, string LabelKey)[]>(field!.GetValue(null));

        Assert.Contains(nodes, node => node.Id == "top_center_status");
        Assert.Contains(nodes, node => node.Id == "top_left_status");
    }

    [Fact]
    public async Task RuntimeCropUsesEditedTopLeftStatusProfileAndReportsSource()
    {
        await WpfTestThread.RunAsync(() =>
        {
            var profile = new SmartBpRecognitionLayoutProfile
            {
                RuntimeSource = "user-layout",
                Regions = new Dictionary<string, SmartBpRecognitionRegionRect>
                {
                    ["top_left_status"] = new() { X = .2, Y = .1, Width = .3, Height = .2 }
                }
            };
            var profileService = new Mock<ISmartBpRecognitionRegionProfileService>();
            profileService.Setup(service => service.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(profile);
            var cropper = new SmartBpRecognitionFrameCropper(profileService.Object);
            var frame = new WriteableBitmap(100, 100, 96, 96, PixelFormats.Bgra32, null);

            var crop = cropper.CropWithInfo(frame, SmartBpRecognitionRegion.TopLeftStatus);

            Assert.Equal((20, 10, 30), (crop.X, crop.Y, crop.Width));
            Assert.InRange(crop.Height, 20, 21);
            Assert.Equal("user-layout", crop.LayoutSource);
            Assert.Equal("x=0.2, y=0.1, w=0.3, h=0.2", crop.NormalizedRectText);
            return Task.CompletedTask;
        });
    }

    [Theory]
    [InlineData("屏蔽求生者", GameAction.BanSur, SmartBpRecognitionRegion.RightTop, "banned_sur")]
    [InlineData("屏蔽监管者", GameAction.BanHun, SmartBpRecognitionRegion.LeftTop, "banned_hun")]
    [InlineData("选择求生者", GameAction.PickSur, SmartBpRecognitionRegion.LeftBottom, "picked_sur")]
    [InlineData("求生者选择角色中", GameAction.DistributeChara, SmartBpRecognitionRegion.LeftBottom, "picked_sur")]
    [InlineData("选择监管者", GameAction.PickHun, SmartBpRecognitionRegion.RightBottom, "picked_hun")]
    public void PhaseMapsToFocusedCropAndTargetField(string phase, GameAction action, SmartBpRecognitionRegion region, string targetField)
    {
        Assert.True(SmartBpAutomaticMapping.TryMapPhase(phase, out var mapped));
        Assert.Equal(action, mapped);
        var focused = SmartBpAutomaticMapping.GetFocusedTarget(mapped);
        Assert.Equal(region, focused.Region);
        Assert.Equal(targetField, focused.TargetField);
    }

    [Fact]
    public void PhaseOnlySchemaOnlyAllowsPhaseRootField()
    {
        var schema = SmartBpRecognitionJsonSchemaProvider.GetPhaseOnly();
        var properties = schema["properties"]!.AsObject();
        Assert.Equal(["phase"], properties.Select(x => x.Key).ToArray());
        Assert.Equal(false, schema["additionalProperties"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData("即将进入区域选择")]
    [InlineData("区域选择")]
    [InlineData("求生者选择区域中")]
    [InlineData("监管者选择区域中")]
    [InlineData("等待游戏开始")]
    [InlineData("加载中")]
    [InlineData("对局中")]
    public void PhaseParserAcceptsPostBpPhases(string phase)
    {
        Assert.Contains(phase, SmartBpAutomaticMapping.ValidPhases);

        var parsed = SmartBpAutomaticParser.ParsePhase($$"""{"phase":"{{phase}}"}""");

        Assert.Equal(phase, parsed.Phase);
    }

    [Theory]
    [InlineData(GameAction.BanSur, "banned_sur")]
    [InlineData(GameAction.BanHun, "banned_hun")]
    [InlineData(GameAction.PickSur, "picked_sur")]
    [InlineData(GameAction.DistributeChara, "picked_sur")]
    [InlineData(GameAction.PickHun, "picked_hun")]
    public void FocusedBusinessSchemaUsesExpectedTargetField(GameAction action, string targetField)
    {
        var schema = SmartBpRecognitionJsonSchemaProvider.GetFocusedBusiness(action, ["心理学家", "小说家"], ["厂长", "梦之女巫"]);
        Assert.Equal(false, schema["additionalProperties"]!.GetValue<bool>());
        Assert.Equal(targetField, schema["properties"]!["target_field"]!["const"]!.GetValue<string>());
        if (targetField == "picked_hun")
            Assert.Contains("picked_hun", schema["properties"]!.AsObject().Select(x => x.Key));
        else
            Assert.Contains("slots", schema["properties"]!.AsObject().Select(x => x.Key));
    }

    [Fact]
    public void FocusedBusinessParserRejectsExtraFieldsAndWrongTarget()
    {
        const string withExtra = """
        {"phase":"屏蔽求生者","target_field":"banned_sur","slots":[{"index":0,"character_name":"小说家"},{"index":1,"character_name":"未选择"},{"index":2,"character_name":"未选择"},{"index":3,"character_name":"未选择"}],"warnings":[]}
        """;
        const string wrongTarget = """
        {"phase":"屏蔽求生者","target_field":"picked_sur","slots":[{"index":0,"character_name":"小说家"},{"index":1,"character_name":"未选择"},{"index":2,"character_name":"未选择"},{"index":3,"character_name":"未选择"}]}
        """;

        Assert.ThrowsAny<Exception>(() => SmartBpAutomaticParser.ParseFocusedBusiness(withExtra, GameAction.BanSur, ["小说家"], ["厂长"]));
        Assert.ThrowsAny<Exception>(() => SmartBpAutomaticParser.ParseFocusedBusiness(wrongTarget, GameAction.BanSur, ["小说家"], ["厂长"]));
    }

    [Fact]
    public void BusinessParserAcceptsSampleAndNormalizesUnknownCharacters()
    {
        const string json = """
        {"phase":"选择监管者","banned_sur":[{"index":0,"character_name":"小说家"},{"index":1,"character_name":"昆虫学者"},{"index":2,"character_name":"未选择"},{"index":3,"character_name":"未选择"}],"banned_hun":[{"index":0,"character_name":"梦之女巫"},{"index":1,"character_name":"女王蜂"}],"picked_sur":[{"index":0,"character_name":"心理学家","player_id":"IHiganbanaI"},{"index":1,"character_name":"守墓人","player_id":"夜风之缚"},{"index":2,"character_name":"机械师","player_id":"磁兮小狗"},{"index":3,"character_name":"记者","player_id":"叶落摘星"}],"picked_hun":{"index":0,"character_name":"厂长","player_id":"导播PLFJY"}}
        """;

        var parsed = SmartBpBusinessStateParser.Parse(json);

        Assert.Equal("选择监管者", parsed.Phase);
        Assert.Equal("心理学家", parsed.PickedSur[0].CharacterName);
        Assert.Equal("磁兮小狗", parsed.PickedSur[2].PlayerId);
        Assert.Equal("机械师", parsed.PickedSur[2].CharacterName);
        Assert.Equal("记者", parsed.PickedSur[3].CharacterName);
        Assert.Equal("厂长", parsed.PickedHun.CharacterName);
        Assert.Equal("导播PLFJY", parsed.PickedHun.PlayerId);
    }

    [Fact]
    public void BusinessParserNormalizesEmptyUnknownAndNullCharactersToUnselected()
    {
        const string json = """
        {"phase":"选择求生者","banned_sur":[{"index":0,"character_name":null},{"index":1,"character_name":"unknown"},{"index":2,"character_name":""},{"index":3,"character_name":"未选择"}],"banned_hun":[{"index":0,"character_name":"unknown"},{"index":1,"character_name":null}],"picked_sur":[{"index":0,"character_name":null,"player_id":"P0"},{"index":1,"character_name":"unknown","player_id":"P1"},{"index":2,"character_name":"","player_id":"P2"},{"index":3,"character_name":"未选择","player_id":"P3"}],"picked_hun":{"index":0,"character_name":null,"player_id":"H"}}
        """;

        var parsed = SmartBpBusinessStateParser.Parse(json);

        Assert.All(parsed.BannedSur, slot => Assert.Equal("未选择", slot.CharacterName));
        Assert.All(parsed.BannedHun, slot => Assert.Equal("未选择", slot.CharacterName));
        Assert.All(parsed.PickedSur, slot => Assert.Equal("未选择", slot.CharacterName));
        Assert.Equal("未选择", parsed.PickedHun.CharacterName);
    }

    [Theory]
    [InlineData("屏蔽求生者", GameAction.BanSur)]
    [InlineData("屏蔽监管者", GameAction.BanHun)]
    [InlineData("选择求生者", GameAction.PickSur)]
    [InlineData("求生者选择角色中", GameAction.DistributeChara)]
    [InlineData("选择监管者", GameAction.PickHun)]
    [InlineData("求生者选择天赋中", GameAction.PickSurTalent)]
    [InlineData("监管者选择天赋中", GameAction.PickHunTalent)]
    public void BusinessPhaseMapsToGuidanceAction(string phase, GameAction expected)
    {
        Assert.True(SmartBpAutomaticMapping.TryMapPhase(phase, out var action));
        Assert.Equal(expected, action);
    }

    [Fact]
    public void CandidateBuilderGeneratesBanSurAndBanHunOperations()
    {
        var novelist = new Character("小说家", Camp.Sur, "novelist.png");
        var dreamWitch = new Character("梦之女巫", Camp.Hun, "dream-witch.png");
        var builder = new SmartBpCandidateOperationBuilder(CreateResolver(novelist, dreamWitch), CreateShared(new Game(new Team(Camp.Sur, TeamType.HomeTeam), new Team(Camp.Hun, TeamType.AwayTeam), GameProgress.Free), novelist, dreamWitch).Object, CreateMatcher(CreateShared(new Game(new Team(Camp.Sur, TeamType.HomeTeam), new Team(Camp.Hun, TeamType.AwayTeam), GameProgress.Free)).Object));

        var banSur = builder.BuildWithDiagnostics(Business("屏蔽求生者", bannedSur0: "小说家"), GameAction.BanSur, [0, 1]);
        var banHun = builder.BuildWithDiagnostics(Business("屏蔽监管者", bannedHun0: "梦之女巫"), GameAction.BanHun, [0]);

        Assert.Contains(banSur.Operations, op => op.Kind == SmartBpDetectedOperationKind.BanCharacter && op.Camp == Camp.Sur && op.SlotIndex == 0 && op.RawCharacterName == "小说家");
        Assert.Contains(banHun.Operations, op => op.Kind == SmartBpDetectedOperationKind.BanCharacter && op.Camp == Camp.Hun && op.SlotIndex == 0 && op.RawCharacterName == "梦之女巫");
    }

    [Fact]
    public void CandidateBuilderSkipsUnselectedAndMapsHunterPickToInternalSlot()
    {
        var hunter = new Character("厂长", Camp.Hun, "hell-ember.png");
        var game = new Game(new Team(Camp.Sur, TeamType.HomeTeam), new Team(Camp.Hun, TeamType.AwayTeam), GameProgress.Free);
        var builder = new SmartBpCandidateOperationBuilder(CreateResolver(hunter), CreateShared(game, hunter).Object, CreateMatcher(CreateShared(game).Object));

        var noBan = builder.BuildWithDiagnostics(Business("屏蔽求生者"), GameAction.BanSur, [0, 1]);
        var pickHun = builder.BuildWithDiagnostics(Business("选择监管者", pickedHun: "厂长", hunterPlayerId: "导播PLFJY"), GameAction.PickHun, []);

        Assert.Empty(noBan.Operations);
        var op = Assert.Single(pickHun.Operations);
        Assert.Equal(SmartBpDetectedOperationKind.PickHunter, op.Kind);
        Assert.Equal(-1, op.SlotIndex);
        Assert.Equal("导播PLFJY", op.PlayerId);
    }

    [Fact]
    public void CandidateBuilderReturnsNoCharacterOperationsForTalentPhase()
    {
        var game = new Game(new Team(Camp.Sur, TeamType.HomeTeam), new Team(Camp.Hun, TeamType.AwayTeam), GameProgress.Free);
        var builder = new SmartBpCandidateOperationBuilder(CreateResolver(), CreateShared(game).Object, CreateMatcher(CreateShared(game).Object));

        var result = builder.BuildWithDiagnostics(Business("求生者选择天赋中"), GameAction.PickSurTalent, [0]);

        Assert.Empty(result.Operations);
        Assert.Contains(result.Messages, message => message.Contains("talent/lock phase", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RegionSnapshotMergeUsesGlobalPhaseAndDefaultsMissingRegions()
    {
        var merger = new SmartBpBusinessStateMerger();
        var bannedSur = new SmartBpFocusedBusinessExtractionResult
        {
            Phase = "屏蔽求生者",
            TargetField = "banned_sur",
            Slots =
            [
                new() { Index = 0, CharacterName = "小说家" },
                new() { Index = 1, CharacterName = "未选择" },
                new() { Index = 2, CharacterName = "未选择" },
                new() { Index = 3, CharacterName = "未选择" }
            ]
        };

        var merged = merger.Merge(new SmartBpPhaseRecognitionResult { Phase = "求生者选择天赋中" }, bannedSur, null, null, null);

        Assert.Equal("求生者选择天赋中", merged.Phase);
        Assert.Equal("小说家", merged.BannedSur[0].CharacterName);
        Assert.Equal(2, merged.BannedHun.Count);
        Assert.Equal(4, merged.PickedSur.Count);
        Assert.All(merged.BannedHun, slot => Assert.Equal("未选择", slot.CharacterName));
        Assert.Equal("未选择", merged.PickedHun.CharacterName);
    }

    [Fact]
    public void BackfillPlanIncludesPreviousPickBeforeCurrentTalentStep()
    {
        var survivor = new Character("小说家", Camp.Sur, "novelist.png");
        var game = new Game(new Team(Camp.Sur, TeamType.HomeTeam), new Team(Camp.Hun, TeamType.AwayTeam), GameProgress.Free);
        var shared = CreateShared(game, survivor);
        var builder = new SmartBpCandidateOperationBuilder(CreateResolverFromShared(shared.Object), shared.Object, CreateMatcher(shared.Object));
        var ledger = new Mock<ISmartBpRecognitionLedger>();
        var service = new SmartBpWorkflowBackfillService(builder, ledger.Object, shared.Object);
        var state = Business("求生者选择天赋中");
        state.PickedSur[2].CharacterName = "小说家";
        var guidance = new GameGuidanceRuntimeSnapshot(true, 6, GameAction.PickSurTalent, [], 30,
        [
            new(5, GameAction.PickSur, [2, 3], 30),
            new(6, GameAction.PickSurTalent, [], 30)
        ]);

        var plan = service.BuildPlan(state, guidance);

        Assert.Equal([5, 6], plan.StepCandidates.Select(step => step.StepIndex));
        var operation = Assert.Single(plan.StepCandidates[0].Operations);
        Assert.Equal(2, operation.SlotIndex);
        Assert.Equal(5, operation.SourceWorkflowStepIndex);
        Assert.Equal(smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpDetectedOperationApplyMode.Backfill, operation.ApplyMode);
        Assert.Empty(plan.StepCandidates[1].Operations);
    }

    [Fact]
    public void RecognitionLedgerTracksCompletedKeysAndCanReset()
    {
        var game = new Game(new Team(Camp.Sur, TeamType.HomeTeam), new Team(Camp.Hun, TeamType.AwayTeam), GameProgress.Free);
        var ledger = new SmartBpRecognitionLedger(CreateShared(game).Object);
        var key = new SmartBpWorkflowOperationKey(GameProgress.Free, 5, GameAction.PickSur, 2, Camp.Sur, "小说家");

        ledger.MarkCompleted(key);
        Assert.True(ledger.IsStepOperationCompleted(key));

        ledger.ResetForCurrentGame();
        Assert.False(ledger.IsStepOperationCompleted(key));
    }

    [Fact]
    public async Task ApplierSkipsSameHunterSurvivorAndBanWithoutCallingSelection()
    {
        var survivor = new Character("小说家", Camp.Sur, "novelist.png");
        var hunter = new Character("厂长", Camp.Hun, "hell-ember.png");
        var game = new Game(new Team(Camp.Sur, TeamType.HomeTeam), new Team(Camp.Hun, TeamType.AwayTeam), GameProgress.Free);
        game.SurPlayerList[0].Character = survivor;
        game.HunPlayer.Character = hunter;
        game.CurrentSurBannedList[0] = survivor;
        var shared = CreateShared(game, survivor, hunter);
        var selection = new Mock<ICharacterSelectionService>();
        var guidance = Guidance(GameAction.PickHun, []);
        var recognitionSettings = new Mock<ISmartBpRecognitionSettingsService>();
        recognitionSettings.SetupGet(x => x.Settings).Returns(new SmartBpRecognitionSettings());
        var applier = new SmartBpDetectedOperationApplier(
            selection.Object,
            guidance.Object,
            shared.Object,
            recognitionSettings.Object,
            Mock.Of<ISmartBpRecognitionLedger>());

        var hunterResult = await applier.ApplyAsync([Operation(SmartBpDetectedOperationKind.PickHunter, GameAction.PickHun, Camp.Hun, -1, "厂长", "厂长", [])], TestContext.Current.CancellationToken);
        guidance.Setup(x => x.GetRuntimeSnapshot()).Returns(new GameGuidanceRuntimeSnapshot(true, 0, GameAction.PickSur, [0], 30, [new(0, GameAction.PickSur, [0], 30)]));
        var survivorResult = await applier.ApplyAsync([Operation(SmartBpDetectedOperationKind.PickSurvivor, GameAction.PickSur, Camp.Sur, 0, "小说家", "小说家", [0])], TestContext.Current.CancellationToken);
        guidance.Setup(x => x.GetRuntimeSnapshot()).Returns(new GameGuidanceRuntimeSnapshot(true, 0, GameAction.BanSur, [0], 30, [new(0, GameAction.BanSur, [0], 30)]));
        var banResult = await applier.ApplyAsync([Operation(SmartBpDetectedOperationKind.BanCharacter, GameAction.BanSur, Camp.Sur, 0, "小说家", "小说家", [0])], TestContext.Current.CancellationToken);

        Assert.Equal(0, hunterResult.AppliedCount + survivorResult.AppliedCount + banResult.AppliedCount);
        Assert.Contains(hunterResult.Messages.Concat(survivorResult.Messages).Concat(banResult.Messages), message => message.Contains("no-op same character", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(banResult.Messages, message => message.Contains("no-op same ban", StringComparison.OrdinalIgnoreCase));
        selection.Verify(x => x.SelectHunterAsync(It.IsAny<Character?>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
        selection.Verify(x => x.SelectSurvivorAsync(It.IsAny<int>(), It.IsAny<Character?>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
        selection.Verify(x => x.BanCharacterAsync(It.IsAny<Camp>(), It.IsAny<int>(), It.IsAny<Character?>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task BackfillApplyUsesSourceWorkflowStepAndDisablesAnimationByDefault()
    {
        var survivor = new Character("小说家", Camp.Sur, "novelist.png");
        var game = new Game(new Team(Camp.Sur, TeamType.HomeTeam), new Team(Camp.Hun, TeamType.AwayTeam), GameProgress.Free);
        var shared = CreateShared(game, survivor);
        var selection = new Mock<ICharacterSelectionService>();
        var guidance = new Mock<IGameGuidanceService>();
        guidance.Setup(x => x.GetRuntimeSnapshot()).Returns(new GameGuidanceRuntimeSnapshot(true, 6, GameAction.PickSurTalent, [], 30,
        [
            new(5, GameAction.PickSur, [0], 30),
            new(6, GameAction.PickSurTalent, [], 30)
        ]));
        var recognitionSettings = new Mock<ISmartBpRecognitionSettingsService>();
        recognitionSettings.SetupGet(x => x.Settings).Returns(new SmartBpRecognitionSettings { PlayBackfillAnimations = false });
        var ledger = new Mock<ISmartBpRecognitionLedger>();
        var applier = new SmartBpDetectedOperationApplier(selection.Object, guidance.Object, shared.Object, recognitionSettings.Object, ledger.Object);
        var operation = Operation(SmartBpDetectedOperationKind.PickSurvivor, GameAction.PickSur, Camp.Sur, 0, "小说家", "小说家", [0]) with
        {
            SourceWorkflowStepIndex = 5,
            ApplyMode = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpDetectedOperationApplyMode.Backfill
        };

        var result = await applier.ApplyAsync([operation], TestContext.Current.CancellationToken);

        Assert.Equal(1, result.AppliedCount);
        Assert.Contains(result.Messages, message => message.Contains("without animation", StringComparison.OrdinalIgnoreCase));
        selection.Verify(x => x.SelectSurvivorAsync(0, survivor, false, It.IsAny<bool>()), Times.Once);
        ledger.Verify(x => x.MarkCompleted(It.IsAny<SmartBpWorkflowOperationKey>()), Times.Once);
    }

    [Fact]
    public void X86ManagedRuntimeIsRejected() =>
        Assert.Throws<PlatformNotSupportedException>(() => LlamaCppRuntimeAssetManager.GetDefaultRuntimeId(Architecture.X86));

    [Fact]
    public void ParserPreservesBusinessPlayerIdsAndUnresolvedNames()
    {
        var shared = new Mock<ISharedDataService>();
        shared.SetupGet(x => x.SurCharaDict).Returns(new SortedDictionary<string, Character> { ["祭司"] = new("祭司", Camp.Sur, "priestess") });
        shared.SetupGet(x => x.HunCharaDict).Returns([]);
        var resolver = CreateResolverFromShared(shared.Object);
        var settings = new Mock<ISmartBpRecognitionSettingsService>(); settings.SetupGet(x => x.Settings).Returns(new SmartBpRecognitionSettings());
        var service = new SmartBpAiRecognitionService(Mock.Of<ISmartBpImageEncoder>(), Mock.Of<ILlamaCppOpenAiClient>(), resolver, settings.Object, NullLogger<SmartBpAiRecognitionService>.Instance);
        const string json = """
        {"phase":"选择求生者","banned_sur":[{"index":0,"character_name":"未选择"},{"index":1,"character_name":"未选择"},{"index":2,"character_name":"未选择"},{"index":3,"character_name":"未选择"}],"banned_hun":[{"index":0,"character_name":"未选择"},{"index":1,"character_name":"未选择"}],"picked_sur":[{"index":0,"character_name":"祭司","player_id":"玩家Ω"},{"index":1,"character_name":"未知角色","player_id":"P2"},{"index":2,"character_name":"未选择","player_id":null},{"index":3,"character_name":"未选择","player_id":null}],"picked_hun":{"index":0,"character_name":"未选择","player_id":null}}
        """;
        var (visual, resolved) = service.Parse(json, SmartBpRecognitionTask.PickSur);
        Assert.Contains("玩家Ω", visual);
        Assert.Contains("resolved=祭司", resolved);
        Assert.Contains("raw=未知角色; resolved=unresolved", resolved);
    }

    [Theory]
    [InlineData("心理学家")]
    [InlineData("\"心理学家\"")]
    [InlineData("“心理学家”")]
    [InlineData("『心理学家』")]
    [InlineData("「心理学家」")]
    public void ResolverHandlesDecorativeQuotedCanonicalCharacterNames(string rawName)
    {
        var shared = new Mock<ISharedDataService>();
        shared.SetupGet(x => x.SurCharaDict).Returns(new SortedDictionary<string, Character>
        {
            ["心理学家"] = new("心理学家", Camp.Sur, "psychologist")
        });
        shared.SetupGet(x => x.HunCharaDict).Returns([]);
        var resolver = CreateResolverFromShared(shared.Object);

        var result = resolver.Resolve(rawName, Camp.Sur, 0, .95);

        Assert.Equal("心理学家", result.ResolvedCharacterName);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ResolverDoesNotWarnForUnselected()
    {
        var shared = new Mock<ISharedDataService>();
        shared.SetupGet(x => x.SurCharaDict).Returns([]);
        shared.SetupGet(x => x.HunCharaDict).Returns([]);
        var resolver = CreateResolverFromShared(shared.Object);

        var result = resolver.Resolve("未选择", Camp.Sur, 0, .95);

        Assert.Null(result.ResolvedCharacterName);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void PreviewPreservesPickedHunterPlayerId()
    {
        var shared = new Mock<ISharedDataService>();
        shared.SetupGet(x => x.SurCharaDict).Returns([]);
        shared.SetupGet(x => x.HunCharaDict).Returns(new SortedDictionary<string, Character> { ["厂长"] = new("厂长", Camp.Hun, "hell-ember") });
        var resolver = CreateResolverFromShared(shared.Object);
        var settings = new Mock<ISmartBpRecognitionSettingsService>();
        settings.SetupGet(x => x.Settings).Returns(new SmartBpRecognitionSettings());
        var service = new SmartBpAiRecognitionService(Mock.Of<ISmartBpImageEncoder>(), Mock.Of<ILlamaCppOpenAiClient>(), resolver, settings.Object, NullLogger<SmartBpAiRecognitionService>.Instance);
        const string json = """
        {"phase":"选择监管者","banned_sur":[{"index":0,"character_name":"未选择"},{"index":1,"character_name":"未选择"},{"index":2,"character_name":"未选择"},{"index":3,"character_name":"未选择"}],"banned_hun":[{"index":0,"character_name":"未选择"},{"index":1,"character_name":"未选择"}],"picked_sur":[{"index":0,"character_name":"未选择","player_id":null},{"index":1,"character_name":"未选择","player_id":null},{"index":2,"character_name":"未选择","player_id":null},{"index":3,"character_name":"未选择","player_id":null}],"picked_hun":{"index":0,"character_name":"厂长","player_id":"导播PLFJY"}}
        """;

        var (visual, resolved) = service.Parse(json, SmartBpRecognitionTask.FullBpScan);

        Assert.Contains("[0] 厂长 / 导播PLFJY / resolved=厂长", visual);
        Assert.Contains("playerId=导播PLFJY", resolved);
    }

    [Fact]
    public void MainAiRecognitionUiDoesNotExposeForceTaskComboBox()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "Views", "SmartBpModuleContentView.xaml"));
        var viewModel = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "ViewModels", "SmartBpModuleContentViewModel.AiRecognition.cs"));

        Assert.DoesNotContain("SmartBpAiDebugForceTask", xaml);
        Assert.DoesNotContain("AiCaptureTasks", xaml);
        Assert.DoesNotContain("SelectedAiCaptureTask", xaml);
        Assert.DoesNotContain("AiCaptureTasks", viewModel);
        Assert.DoesNotContain("SelectedAiCaptureTask", viewModel);
    }

    [Fact]
    public void RecognitionSpeedTestDoesNotUseAutomaticGuidanceOrApplyPipeline()
    {
        var root = FindRepositoryRoot();
        var viewModel = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "ViewModels", "SmartBpModuleContentViewModel.AiRecognition.cs"));
        var speedMethodStart = viewModel.IndexOf("private async Task TestRecognitionSpeedAsync()", StringComparison.Ordinal);
        var speedMethodEnd = viewModel.IndexOf("private string GetRecognitionSpeedFingerprint()", StringComparison.Ordinal);
        Assert.True(speedMethodStart >= 0);
        Assert.True(speedMethodEnd > speedMethodStart);
        var speedMethod = viewModel[speedMethodStart..speedMethodEnd];

        Assert.Contains("_ocrBpRecognitionService.RecognizeAsync(frame", speedMethod);
        Assert.Contains("SelectedAiTestFrame ?? AiTestFrames.FirstOrDefault()", speedMethod);
        Assert.DoesNotContain("foreach (var testFrame in AiTestFrames)", speedMethod);
        Assert.DoesNotContain("_autoRecognitionCoordinator.RunOneTickDryRunAsync(frame)", speedMethod);
        Assert.DoesNotContain("_autoRecognitionCoordinator.RunOneTickAsync(frame)", speedMethod);
        Assert.DoesNotContain("_aiRecognitionService.RecognizeAsync(frame, testFrame.Task)", speedMethod);
        Assert.DoesNotContain("ApplyRegionGatedResult", speedMethod);
    }

    [Fact]
    public void SelectedTestFrameRecognitionUsesFullStrategyDebugPath()
    {
        var root = FindRepositoryRoot();
        var viewModel = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "ViewModels", "SmartBpModuleContentViewModel.AiRecognition.cs"));
        var methodStart = viewModel.IndexOf("[RelayCommand] private async Task RecognizeSelectedTestFrameAsync()", StringComparison.Ordinal);
        var methodEnd = viewModel.IndexOf("[RelayCommand] private Task RecognizeCurrentCaptureFrameAsync()", StringComparison.Ordinal);
        Assert.True(methodStart >= 0);
        Assert.True(methodEnd > methodStart);
        var method = viewModel[methodStart..methodEnd];

        Assert.Contains("RunFullStrategyRecognitionCoreAsync(frame)", method);
        Assert.Contains("_autoRecognitionCoordinator.RunFullRecognitionDebugAsync(frame)", viewModel);
        Assert.Contains("_autoRecognitionCoordinator.RunPhaseOnlyDebugAsync(frame)", viewModel);
        Assert.Contains("RunPhaseOnlyRecognitionCoreAsync(LoadTestFrame(SelectedAiTestFrame))", viewModel);
        Assert.DoesNotContain("SmartBpRecognitionStrategy.PureAi", viewModel);
    }

    [Fact]
    public void SmartBpDebugUiExposesOcrOnlyDebugControls()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "Views", "SmartBpModuleContentView.xaml"));
        var viewModel = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "ViewModels", "SmartBpModuleContentViewModel.AiRecognition.cs"));

        Assert.Contains("SmartBpFullRecognitionTest", xaml);
        Assert.Contains("SmartBpPhaseSceneOnlyTest", xaml);
        Assert.Contains("SmartBpIncrementalRecognitionTest", xaml);
        Assert.Contains("RecognizeSelectedTestFrameCommand", xaml);
        Assert.Contains("RecognizeIncrementalSelectedTestFrameCommand", xaml);
        Assert.Contains("DetectStageFromSelectedTestFrameCommand", xaml);
        Assert.DoesNotContain("StartBusinessAiServerCommand", xaml);
        Assert.DoesNotContain("StartAiOcrServerCommand", xaml);
        Assert.DoesNotContain("StartRequiredLlamaServersCommand", xaml);
        Assert.DoesNotContain("BusinessAiServerStatus", xaml);
        Assert.DoesNotContain("AiOcrServerReuseStatus", xaml);
        Assert.Contains("DebugFinalBusinessState", xaml);
        Assert.Contains("OpenRecognitionDebugLogWindowCommand", xaml);

        Assert.Contains("_autoRecognitionCoordinator.RunIncrementalRecognitionDebugAsync(frame)", viewModel);
        Assert.Contains("CurrentStageIncremental", viewModel);
        Assert.Contains("PhaseOnly", viewModel);
        Assert.DoesNotContain("SmartBpRecognitionStrategy.PureAi", viewModel);
    }

    [Fact]
    public void SmartBpSettingsUiDoesNotExposeAiRuntimeAndModelDownloadBindings()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "Views", "SmartBpModuleContentView.xaml"));
        var viewModel = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "ViewModels", "SmartBpModuleContentViewModel.AiRecognition.cs"));

        Assert.DoesNotContain("CheckLlamaRuntimeUpdateCommand", xaml);
        Assert.DoesNotContain("LlamaRuntimeUpdateStatus", xaml);
        Assert.DoesNotContain("BusinessAiModelDownloadProgress", xaml);
        Assert.DoesNotContain("BusinessAiModelDownloadDetail", xaml);
        Assert.DoesNotContain("AiOcrModelDownloadProgress", xaml);
        Assert.DoesNotContain("AiOcrModelDownloadDetail", xaml);
        Assert.DoesNotContain("CancelBusinessAiModelDownloadCommand", xaml);
        Assert.DoesNotContain("CancelAiOcrModelDownloadCommand", xaml);
        Assert.Contains("ApplyVisionModelDownloadState", viewModel);
        Assert.Contains("LocalVisionModelDownloadRole.BusinessAi", viewModel);
        Assert.Contains("LocalVisionModelDownloadRole.AiOcr", viewModel);
    }

    [Fact]
    public void SmartBpAutomaticCoordinatorDoesNotUseHybridAiFusion()
    {
        var root = FindRepositoryRoot();
        var coordinator = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "Services", "Recognition", "SmartBpAutomaticServices.cs"));
        var xaml = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "Views", "SmartBpModuleContentView.xaml"));

        Assert.DoesNotContain("AI + OCR fusion_mode=", coordinator);
        Assert.DoesNotContain("businessAiFusion.FuseAsync", coordinator);
        Assert.DoesNotContain("AI + AI OCR fusion_mode=", coordinator);
        Assert.DoesNotContain("aiOcrTranscriptInterpreter.Interpret", coordinator);
        Assert.DoesNotContain("SmartBpAiWithOcrFusionMode", xaml);
        Assert.DoesNotContain("SmartBpAiWithAiOcrFusionMode", xaml);
    }

    [Fact]
    public void SmartBpAutomaticCoordinatorDoesNotPackageBusinessAiFusionEvidence()
    {
        var root = FindRepositoryRoot();
        var coordinator = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "Services", "Recognition", "SmartBpAutomaticServices.cs"));

        Assert.DoesNotContain("pre-fusion raw evidence packaging", coordinator);
        Assert.DoesNotContain("post-fusion validation", coordinator);
        Assert.DoesNotContain("Business AI fusion validation failed", coordinator);
        Assert.Contains("ocrRecognition.RecognizeAsync", coordinator);
        var viewModel = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "ViewModels", "SmartBpModuleContentViewModel.AiRecognition.cs"));
        Assert.DoesNotContain("Recognition failed during Business AI fusion", viewModel);
    }

    [Fact]
    public void OcrContentPhaseDoesNotOverrideAuthoritativePhaseGate()
    {
        var root = FindRepositoryRoot();
        var coordinator = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "Services", "Recognition", "SmartBpAutomaticServices.cs"));

        Assert.Contains("OCR content phase=", coordinator);
        Assert.Contains("delta.Phase = phaseResult.Phase", coordinator);
        Assert.Contains("ocr.BusinessState.Phase = phaseResult.Phase", coordinator);
    }

    [Fact]
    public void AutomaticRecognitionStartShowsCaptureErrorAndEnsuresRequiredServers()
    {
        var root = FindRepositoryRoot();
        var viewModel = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "ViewModels", "SmartBpModuleContentViewModel.AiRecognition.cs"));

        Assert.Contains("GetValidatedCurrentFrameAsync(requireOcrReady: true)", viewModel);
        Assert.Contains("SmartBpValidationCaptureNotRunning", File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "ViewModels", "SmartBpModuleContentViewModel.cs")));
        Assert.Contains("EnsureRequiredLlamaServersForAutomaticRecognitionAsync", viewModel);
        Assert.DoesNotContain("await StartRequiredLlamaServersAsync()", viewModel);
        Assert.Contains("IsAiPreviewLoopRunning = false", viewModel);
        Assert.Contains("_autoRecognitionGlobalControl.Update(false)", viewModel);
    }

    [Fact]
    public void AiOcrTranscriptParsingFallsBackToPlainText()
    {
        var newline = SmartBpAiOcrTranscriptRecognitionService.ParseTechnicalLines("小说家\n昆虫学者\n入殓师");
        var spaces = SmartBpAiOcrTranscriptRecognitionService.ParseTechnicalLines("小说家 昆虫学者 未选择 未选择");
        var combinedNoise = SmartBpAiOcrTranscriptRecognitionService.ParseTechnicalLines("未选择导播PLFJY");

        Assert.Equal(["小说家", "昆虫学者", "入殓师"], newline.Lines.Select(line => line.Text));
        Assert.Equal("小说家 昆虫学者 未选择 未选择", Assert.Single(spaces.Lines).Text);
        Assert.Equal("未选择导播PLFJY", Assert.Single(combinedNoise.Lines).Text);
        Assert.Contains("AI OCR transcript parsed as plain text technical lines.", combinedNoise.Diagnostics);
    }

    [Theory]
    [InlineData("{\"lines\":[{\"text\":\"小说家\"},{\"text\":\"昆虫学者\"}]}")]
    [InlineData("```json\n{\"lines\":[{\"text\":\"小说家\"},{\"text\":\"昆虫学者\"}]}\n```")]
    [InlineData("{\"lines\":[\"小说家\",\"昆虫学者\"]}")]
    public void AiOcrTranscriptParsingAcceptsJsonAndFencedJson(string raw)
    {
        var parsed = SmartBpAiOcrTranscriptRecognitionService.ParseTechnicalLines(raw);

        Assert.Equal(["小说家", "昆虫学者"], parsed.Lines.Select(line => line.Text));
    }

    [Theory]
    [InlineData("{\"lines\":[{\"text\":\"小说家\\n昆虫学者\\n未选择\\n未选择\"}]}")]
    [InlineData("{\"lines\":[{\"text\":\"小说家 昆虫学者 未选择 未选择\"}]}")]
    public void AiOcrTranscriptJsonTextPreservesTechnicalLineGroups(string raw)
    {
        var parsed = SmartBpAiOcrTranscriptRecognitionService.ParseTechnicalLines(raw);

        Assert.Single(parsed.Lines);
        Assert.Contains("小说家", parsed.Lines[0].Text);
        Assert.Contains("未选择", parsed.Lines[0].Text);
    }

    [Fact]
    public void RecognitionRawLogsUseSeparateWindowCommandsAndBinding()
    {
        var root = FindRepositoryRoot();
        var mainXaml = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "Views", "SmartBpModuleContentView.xaml"));
        var logXaml = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "Views", "SmartBpRecognitionDebugLogWindow.xaml"));
        var logCodeBehind = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "Views", "SmartBpRecognitionDebugLogWindow.xaml.cs"));
        var viewModel = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "ViewModels", "SmartBpModuleContentViewModel.AiRecognition.cs"));

        Assert.DoesNotContain("DebugPureAiFullRaw", mainXaml);
        Assert.DoesNotContain("DebugAiOcrTranscript", mainXaml);
        Assert.Contains("DebugFinalBusinessState", mainXaml);
        Assert.DoesNotContain("{Binding DebugStrategySummary}", mainXaml);
        Assert.DoesNotContain("{Binding DebugPhaseScene}", mainXaml);
        Assert.DoesNotContain("{Binding DebugFusionSummary}", mainXaml);
        Assert.DoesNotContain("{Binding DebugCandidateOperations}", mainXaml);
        Assert.DoesNotContain("{Binding DebugServerStatus}", mainXaml);
        Assert.DoesNotContain("{Binding DebugTiming}", mainXaml);
        Assert.DoesNotContain("{Binding AiLastError}", mainXaml);
        Assert.Contains("RecognitionDebugLogText", logXaml);
        Assert.Contains("{Binding DebugStrategySummary}", logXaml);
        Assert.Contains("{Binding DebugPhaseScene}", logXaml);
        Assert.Contains("{Binding DebugFusionSummary}", logXaml);
        Assert.Contains("{Binding DebugCandidateOperations}", logXaml);
        Assert.Contains("{Binding DebugServerStatus}", logXaml);
        Assert.Contains("{Binding DebugTiming}", logXaml);
        Assert.Contains("{Binding AiLastError}", logXaml);
        Assert.Contains("CopyRecognitionDebugLogCommand", logXaml);
        Assert.Contains("ClearAiDebugLogCommand", logXaml);
        Assert.Contains("RefreshRecognitionDebugLogCommand", logXaml);
        Assert.Contains("<ui:FluentWindow", logXaml);
        Assert.Contains("<ui:TitleBar", logXaml);
        Assert.Contains("<ui:ToggleSwitch", logXaml);
        Assert.Contains("<ui:TextBox", logXaml);
        Assert.Contains("DataContext = viewModel", logCodeBehind);
        Assert.Contains("new SmartBpRecognitionDebugLogWindow(this)", viewModel);
    }

    [Fact]
    public void AiOcrTranscriptInterpreterUsesResolverAndDoesNotCreateFakeOcrBoxes()
    {
        var root = FindRepositoryRoot();
        var services = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "Services", "Recognition", "RecognitionServices.cs"));
        var coordinator = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "Services", "Recognition", "SmartBpAutomaticServices.cs"));

        Assert.Contains("SmartBpAiOcrTranscriptInterpreter", services);
        Assert.Contains("ResolveCharacterDetailed", services);
        Assert.DoesNotContain("aiOcrTranscriptInterpreter.Interpret", coordinator);
        Assert.DoesNotContain("new OcrTextLine(line.Text", coordinator);
        Assert.DoesNotContain("new OpenCvSharp.Rect", coordinator);
    }

    [Fact]
    public void SmartBpSettingsUiDoesNotShowLlamaServerBusyProgress()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "Views", "SmartBpModuleContentView.xaml"));

        Assert.DoesNotContain("IsBusinessAiServerStarting", xaml);
        Assert.DoesNotContain("IsAiOcrServerStarting", xaml);
        Assert.DoesNotContain("IsRequiredLlamaServersStarting", xaml);
        Assert.DoesNotContain("SmartBpBusinessAiServer", xaml);
        Assert.DoesNotContain("SmartBpAiOcrServer", xaml);
    }

    [Fact]
    public void SmartBpSettingsUiDoesNotShowModelSwitchServerRestartProgress()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "Views", "SmartBpModuleContentView.xaml"));
        var viewModel = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "ViewModels", "SmartBpModuleContentViewModel.AiRecognition.cs"));

        Assert.Contains("SetRoleServerStarting(LlamaVisionServerRole.BusinessAi, true, true)", viewModel);
        Assert.Contains("SetRoleServerStarting(LlamaVisionServerRole.AiOcr, true, true)", viewModel);
        Assert.Contains("var aiOcrRoleWasRunning = wasReusingBusiness ? business.IsRunning : aiOcr.IsRunning", viewModel);
        Assert.Contains("await business.StartAsync()", viewModel);
        Assert.Contains("await aiOcr.StartAsync()", viewModel);
        Assert.DoesNotContain("BusinessAiServerActivityText", xaml);
        Assert.DoesNotContain("AiOcrServerActivityText", xaml);
    }

    [Fact]
    public void DistributeCharaTargetsInternalPlayerByPlayerIdNotVisualSlot()
    {
        var prophet = new Character("先知", Camp.Sur, "prophet.png");
        var cheerleader = new Character("拉拉队员", Camp.Sur, "cheerleader.png");
        var magician = new Character("魔术师", Camp.Sur, "magician.png");
        var gravekeeper = new Character("守墓人", Camp.Sur, "gravekeeper.png");
        var game = CreateGameWithNamedSurvivors(["A", "B", "C", "D"], [prophet, cheerleader, magician, gravekeeper]);
        var shared = CreateShared(game, prophet, cheerleader, magician, gravekeeper);
        var builder = new SmartBpCandidateOperationBuilder(CreateResolverFromShared(shared.Object), shared.Object, CreateMatcher(shared.Object));

        var state = Business("求生者选择角色中");
        state.DistributionEvidence =
        [
            new() { Index = 0, CharacterName = "先知", PlayerId = "D" }
        ];

        var result = builder.BuildWithDiagnostics(state, GameAction.DistributeChara, []);

        var op = Assert.Single(result.Operations);
        Assert.Equal(SmartBpDetectedOperationKind.SwapSurvivors, op.Kind);
        Assert.Equal(3, op.SlotIndex);
        Assert.Contains(result.Messages, m => m.Contains("internal Sur[3]", StringComparison.Ordinal));
    }

    [Fact]
    public void DistributeCharaSkipsWhenPlayerIdDoesNotMatchAnySurvivor()
    {
        var prophet = new Character("先知", Camp.Sur, "prophet.png");
        var game = CreateGameWithNamedSurvivors(["A", "B", "C", "D"], [prophet, null, null, null]);
        var shared = CreateShared(game, prophet);
        var builder = new SmartBpCandidateOperationBuilder(CreateResolverFromShared(shared.Object), shared.Object, CreateMatcher(shared.Object));

        var state = Business("求生者选择角色中");
        state.DistributionEvidence =
        [
            new() { Index = 0, CharacterName = "先知", PlayerId = "不存在的玩家" }
        ];

        var result = builder.BuildWithDiagnostics(state, GameAction.DistributeChara, []);

        Assert.Empty(result.Operations);
        Assert.Contains(result.Messages, m => m.Contains("did not match", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DistributeCharaSkipsWhenPlayerIdIsAmbiguous()
    {
        var prophet = new Character("先知", Camp.Sur, "prophet.png");
        var game = CreateGameWithNamedSurvivors(["PlayerX", "PlayerY", "PlayerX1", "D"], [prophet, null, null, null]);
        var shared = CreateShared(game, prophet);
        var builder = new SmartBpCandidateOperationBuilder(CreateResolverFromShared(shared.Object), shared.Object, CreateMatcher(shared.Object));

        var state = Business("求生者选择角色中");
        state.DistributionEvidence =
        [
            new() { Index = 0, CharacterName = "先知", PlayerId = "PlayerX" }
        ];

        var result = builder.BuildWithDiagnostics(state, GameAction.DistributeChara, []);

        Assert.Empty(result.Operations);
    }

    [Fact]
    public void DistributeCharaDoesNotIntroduceNewCharacterNotCurrentlySelected()
    {
        var prophet = new Character("先知", Camp.Sur, "prophet.png");
        var adventurer = new Character("冒险家", Camp.Sur, "adventurer.png");
        var game = CreateGameWithNamedSurvivors(["A", "B", "C", "D"], [prophet, null, null, null]);
        var shared = CreateShared(game, prophet, adventurer);
        var builder = new SmartBpCandidateOperationBuilder(CreateResolverFromShared(shared.Object), shared.Object, CreateMatcher(shared.Object));

        var state = Business("求生者选择角色中");
        state.DistributionEvidence =
        [
            new() { Index = 0, CharacterName = "冒险家", PlayerId = "A" }
        ];

        var result = builder.BuildWithDiagnostics(state, GameAction.DistributeChara, []);

        Assert.Empty(result.Operations);
        Assert.Contains(result.Messages, m => m.Contains("not among currently selected", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DistributeCharaIsNoOpWhenCharacterAlreadyOnMatchedPlayerSlot()
    {
        var prophet = new Character("先知", Camp.Sur, "prophet.png");
        var game = CreateGameWithNamedSurvivors(["A", "B", "C", "D"], [prophet, null, null, null]);
        var shared = CreateShared(game, prophet);
        var builder = new SmartBpCandidateOperationBuilder(CreateResolverFromShared(shared.Object), shared.Object, CreateMatcher(shared.Object));

        var state = Business("求生者选择角色中");
        state.DistributionEvidence =
        [
            new() { Index = 0, CharacterName = "先知", PlayerId = "A" }
        ];

        var result = builder.BuildWithDiagnostics(state, GameAction.DistributeChara, []);

        Assert.Empty(result.Operations);
        Assert.Contains(result.Messages, m => m.Contains("no-op", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DistributeCharaMultipleSwapsUseSimulationToAvoidContradictions()
    {
        var prophet = new Character("先知", Camp.Sur, "prophet.png");
        var cheerleader = new Character("拉拉队员", Camp.Sur, "cheerleader.png");
        var magician = new Character("魔术师", Camp.Sur, "magician.png");
        var gravekeeper = new Character("守墓人", Camp.Sur, "gravekeeper.png");
        var game = CreateGameWithNamedSurvivors(["A", "B", "C", "D"], [prophet, cheerleader, magician, gravekeeper]);
        var shared = CreateShared(game, prophet, cheerleader, magician, gravekeeper);
        var builder = new SmartBpCandidateOperationBuilder(CreateResolverFromShared(shared.Object), shared.Object, CreateMatcher(shared.Object));

        var state = Business("求生者选择角色中");
        state.DistributionEvidence =
        [
            new() { Index = 0, CharacterName = "守墓人", PlayerId = "A" },
            new() { Index = 1, CharacterName = "魔术师", PlayerId = "B" }
        ];

        var result = builder.BuildWithDiagnostics(state, GameAction.DistributeChara, []);

        Assert.Equal(2, result.Operations.Count);
        Assert.All(result.Operations, op => Assert.Equal(SmartBpDetectedOperationKind.SwapSurvivors, op.Kind));
        Assert.Equal(0, result.Operations[0].SlotIndex);
        Assert.Equal(1, result.Operations[1].SlotIndex);
    }

    [Fact]
    public void DistributeCharaNeverGeneratesPickSurvivorOperation()
    {
        var prophet = new Character("先知", Camp.Sur, "prophet.png");
        var game = CreateGameWithNamedSurvivors(["A", "B", "C", "D"], [prophet, null, null, null]);
        var shared = CreateShared(game, prophet);
        var builder = new SmartBpCandidateOperationBuilder(CreateResolverFromShared(shared.Object), shared.Object, CreateMatcher(shared.Object));

        var state = Business("求生者选择角色中");
        state.DistributionEvidence =
        [
            new() { Index = 0, CharacterName = "先知", PlayerId = "B" }
        ];

        var result = builder.BuildWithDiagnostics(state, GameAction.DistributeChara, []);

        Assert.All(result.Operations, op => Assert.NotEqual(SmartBpDetectedOperationKind.PickSurvivor, op.Kind));
    }

    [Fact]
    public void DistributeCharaSkipsWhenPlayerIdMissing()
    {
        var prophet = new Character("先知", Camp.Sur, "prophet.png");
        var game = CreateGameWithNamedSurvivors(["A", "B", "C", "D"], [prophet, null, null, null]);
        var shared = CreateShared(game, prophet);
        var builder = new SmartBpCandidateOperationBuilder(CreateResolverFromShared(shared.Object), shared.Object, CreateMatcher(shared.Object));

        var state = Business("求生者选择角色中");
        state.DistributionEvidence =
        [
            new() { Index = 0, CharacterName = "先知", PlayerId = null }
        ];

        var result = builder.BuildWithDiagnostics(state, GameAction.DistributeChara, []);

        Assert.Empty(result.Operations);
        Assert.Contains(result.Messages, m => m.Contains("player_id missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IsSurvivorPickLockedReturnsFalseBeforeDistributeChara()
    {
        var snapshot = new GameGuidanceRuntimeSnapshot(true, 0, GameAction.PickSur, [0, 1], 30, [new(0, GameAction.PickSur, [0, 1], 30)]);
        Assert.False(SmartBpAutomaticMapping.IsSurvivorPickLocked(snapshot, "选择求生者"));
    }

    [Fact]
    public void IsSurvivorPickLockedReturnsFalseForPickSurEvenWhenPhaseIsSurvivorSelectingCharacter()
    {
        var snapshot = new GameGuidanceRuntimeSnapshot(true, 0, GameAction.PickSur, [0, 1], 30, [new(0, GameAction.PickSur, [0, 1], 30)]);
        Assert.False(SmartBpAutomaticMapping.IsSurvivorPickLocked(snapshot, "求生者选择角色中"));
    }

    [Fact]
    public void IsSurvivorPickLockedReturnsFalseWithoutGuidanceForSurvivorSelectingCharacterPhase()
    {
        var snapshot = new GameGuidanceRuntimeSnapshot(false, 0, null, [], null, []);
        Assert.False(SmartBpAutomaticMapping.IsSurvivorPickLocked(snapshot, "求生者选择角色中"));
        Assert.False(SmartBpAutomaticMapping.IsSurvivorPickLocked(snapshot, "选择求生者"));
        Assert.False(SmartBpAutomaticMapping.IsSurvivorPickLocked(snapshot, "屏蔽求生者"));
        Assert.False(SmartBpAutomaticMapping.IsSurvivorPickLocked(snapshot, "未知"));
    }

    [Fact]
    public void IsSurvivorPickLockedReturnsTrueWithoutGuidanceForPostPickPhases()
    {
        var snapshot = new GameGuidanceRuntimeSnapshot(false, 0, null, [], null, []);
        Assert.True(SmartBpAutomaticMapping.IsSurvivorPickLocked(snapshot, "求生者选择天赋中"));
        Assert.True(SmartBpAutomaticMapping.IsSurvivorPickLocked(snapshot, "选择监管者"));
        Assert.True(SmartBpAutomaticMapping.IsSurvivorPickLocked(snapshot, "监管者选择天赋中"));
    }

    [Fact]
    public void IsSurvivorPickLockedReturnsTrueDuringDistributeChara()
    {
        var snapshot = new GameGuidanceRuntimeSnapshot(true, 1, GameAction.DistributeChara, [], 30,
        [
            new(0, GameAction.PickSur, [0, 1], 30),
            new(1, GameAction.DistributeChara, [], 30)
        ]);
        Assert.True(SmartBpAutomaticMapping.IsSurvivorPickLocked(snapshot, "求生者选择角色中"));
    }

    [Fact]
    public void IsSurvivorPickLockedReturnsTrueDuringSurvivorTalentPhase()
    {
        var snapshot = new GameGuidanceRuntimeSnapshot(true, 2, GameAction.PickSurTalent, [], 30,
        [
            new(0, GameAction.PickSur, [0, 1], 30),
            new(1, GameAction.DistributeChara, [], 30),
            new(2, GameAction.PickSurTalent, [], 30)
        ]);
        Assert.True(SmartBpAutomaticMapping.IsSurvivorPickLocked(snapshot, "求生者选择天赋中"));
    }

    [Fact]
    public void IsSurvivorPickLockedReturnsTrueDuringHunterPickPhase()
    {
        var snapshot = new GameGuidanceRuntimeSnapshot(true, 3, GameAction.PickHun, [], 30,
        [
            new(0, GameAction.PickSur, [0, 1], 30),
            new(1, GameAction.DistributeChara, [], 30),
            new(3, GameAction.PickHun, [], 30)
        ]);
        Assert.True(SmartBpAutomaticMapping.IsSurvivorPickLocked(snapshot, "选择监管者"));
    }

    [Fact]
    public void IsSurvivorPickLockedReturnsTrueForLockedPhasesByAuthoritativePhase()
    {
        var snapshot = new GameGuidanceRuntimeSnapshot(false, 0, null, [], null, []);
        Assert.True(SmartBpAutomaticMapping.IsSurvivorPickLocked(snapshot, "天赋已锁定"));
        Assert.True(SmartBpAutomaticMapping.IsSurvivorPickLocked(snapshot, "等待游戏开始"));
        Assert.True(SmartBpAutomaticMapping.IsSurvivorPickLocked(snapshot, "对局中"));
    }

    [Fact]
    public void IsSurvivorPickLockedReturnsTrueAfterDistributeCharaLatchEvenIfCurrentActionChanged()
    {
        var snapshot = new GameGuidanceRuntimeSnapshot(true, 5, GameAction.PickHunTalent, [], 30,
        [
            new(0, GameAction.PickSur, [0, 1], 30),
            new(1, GameAction.DistributeChara, [], 30),
            new(5, GameAction.PickHunTalent, [], 30)
        ]);
        Assert.True(SmartBpAutomaticMapping.IsSurvivorPickLocked(snapshot, "监管者选择天赋中"));
    }

    [Fact]
    public void ApplyDistributionEvidenceReplacesEvidenceAndSkipsSlotIndexMerge()
    {
        var store = new SmartBpRecognitionStateStore();
        var update = new SmartBpSnapshotFieldUpdate
        {
            Field = "picked_sur",
            Slots =
            [
                Slot(0, "selected", "先知", "D"),
                Slot(1, "selected", "拉拉队员", "C")
            ]
        };
        var diagnostics = store.ApplyDistributionEvidence(update, frameSequence: 10, DateTimeOffset.Now);
        Assert.Contains(diagnostics, d => d.Contains("distribution evidence", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(diagnostics, d => d.Contains("authoritative picked_sur freshness unchanged", StringComparison.OrdinalIgnoreCase));
        var snapshot = store.Snapshot;
        Assert.Equal(2, snapshot.DistributionEvidence.Count);
        Assert.Equal("先知", snapshot.DistributionEvidence[0].CharacterName);
        Assert.Equal("D", snapshot.DistributionEvidence[0].PlayerId);
        Assert.Equal("拉拉队员", snapshot.DistributionEvidence[1].CharacterName);
        Assert.Equal("C", snapshot.DistributionEvidence[1].PlayerId);
    }

    [Fact]
    public void ApplyDistributionEvidenceDoesNotMarkAuthoritativePickedSurFresh()
    {
        var store = new SmartBpRecognitionStateStore();
        var before = DateTimeOffset.Now;
        store.ApplyDistributionEvidence(new SmartBpSnapshotFieldUpdate
        {
            Field = "picked_sur",
            Slots = [Slot(0, "selected", "先知", "D")]
        }, frameSequence: 10, before);
        var staleDiagnostics = store.GetStaleFieldDiagnostics(before.AddSeconds(1), staleMilliseconds: 500);
        Assert.Contains(staleDiagnostics, d => d.Contains("picked_sur", StringComparison.Ordinal) && d.Contains("stale", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ApplyDistributionEvidenceStaleProtectionUsesSeparateKey()
    {
        var store = new SmartBpRecognitionStateStore();
        store.ApplyDistributionEvidence(new SmartBpSnapshotFieldUpdate
        {
            Field = "picked_sur",
            Slots = [Slot(0, "selected", "先知", "D")]
        }, frameSequence: 10, DateTimeOffset.Now);
        var diagnostics = store.ApplyDistributionEvidence(new SmartBpSnapshotFieldUpdate
        {
            Field = "picked_sur",
            Slots = [Slot(0, "selected", "魔术师", "A")]
        }, frameSequence: 5, DateTimeOffset.Now);
        Assert.Contains(diagnostics, d => d.Contains("stale", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("先知", store.Snapshot.DistributionEvidence[0].CharacterName);
    }

    [Fact]
    public void DistributeCharaFallsBackToPickedSurWhenDistributionEvidenceEmpty()
    {
        var prophet = new Character("先知", Camp.Sur, "prophet.png");
        var game = CreateGameWithNamedSurvivors(["A", "B", "C", "D"], [prophet, null, null, null]);
        var shared = CreateShared(game, prophet);
        var builder = new SmartBpCandidateOperationBuilder(CreateResolverFromShared(shared.Object), shared.Object, CreateMatcher(shared.Object));

        var state = Business("求生者选择角色中");
        state.PickedSur[0].CharacterName = "先知";
        state.PickedSur[0].PlayerId = "B";
        state.DistributionEvidence = [];

        var result = builder.BuildWithDiagnostics(state, GameAction.DistributeChara, []);

        var op = Assert.Single(result.Operations);
        Assert.Equal(SmartBpDetectedOperationKind.SwapSurvivors, op.Kind);
        Assert.Equal(1, op.SlotIndex);
    }

    private static Game CreateGameWithNamedSurvivors(string[] names, Character?[]? characters = null)
    {
        var surTeam = new Team(Camp.Sur, TeamType.HomeTeam);
        var hunTeam = new Team(Camp.Hun, TeamType.AwayTeam);
        for (var i = 0; i < 4 && i < names.Length; i++)
        {
            var member = surTeam.SurMemberList[i];
            member.Name = names[i];
            member.IsOnField = true;
            surTeam.MemberOnField(member);
        }
        var hunMember = hunTeam.HunMemberList[0];
        hunMember.Name = "Hunter";
        hunMember.IsOnField = true;
        hunTeam.MemberOnField(hunMember);
        var game = new Game(surTeam, hunTeam, GameProgress.Free);
        if (characters != null)
        {
            for (var i = 0; i < 4 && i < characters.Length; i++)
            {
                if (characters[i] != null)
                    game.SurPlayerList[i].Character = characters[i];
            }
        }
        return game;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "neo-bpsys-wpf.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate neo-bpsys-wpf repository root.");
    }

    private static bool Overlaps(SmartBpRecognitionRegionRect left, SmartBpRecognitionRegionRect right) =>
        left.X < right.X + right.Width &&
        left.X + left.Width > right.X &&
        left.Y < right.Y + right.Height &&
        left.Y + left.Height > right.Y;

    private static string[] CharacterNameEnum(JsonObject schema, string propertyName, bool isArray)
    {
        var rootProperty = schema["properties"]?[propertyName] ?? throw new InvalidDataException($"Missing schema property {propertyName}.");
        var slot = isArray ? rootProperty["items"] : rootProperty;
        var values = slot?["properties"]?["character_name"]?["enum"]?.AsArray()
            ?? throw new InvalidDataException($"Missing character_name enum for {propertyName}.");
        return values.Select(x => x?.GetValue<string>() ?? "").ToArray();
    }

    private static JsonObject FindUpdateShape(JsonArray updateShapes, string field)
    {
        return updateShapes.Select(node => node?.AsObject())
            .FirstOrDefault(shape => shape?["properties"]?["field"]?["const"]?.GetValue<string>() == field)
            ?? throw new InvalidDataException($"Missing update shape for {field}.");
    }

    private static int[] SlotIndexEnum(JsonObject updateShape)
    {
        var values = updateShape["properties"]?["slots"]?["items"]?["properties"]?["index"]?["enum"]?.AsArray()
            ?? throw new InvalidDataException("Missing slot index enum.");
        return values.Select(x => x?.GetValue<int>() ?? -1).ToArray();
    }

    private static string[] RequiredProperties(JsonObject updateShape)
    {
        var values = updateShape["properties"]?["slots"]?["items"]?["required"]?.AsArray()
            ?? throw new InvalidDataException("Missing slot required properties.");
        return values.Select(x => x?.GetValue<string>() ?? "").ToArray();
    }

    private static SmartBpSnapshotDeltaSlot Slot(int index, string slotState, string characterName = "未选择", string? playerId = null) =>
        new() { Index = index, SlotState = slotState, CharacterName = characterName, PlayerId = playerId };

    private static SmartBpStageDetectionResult Stage(string action, double confidence)
    {
        var gameAction = Enum.Parse<GameAction>(action);
        var mapping = SmartBpAutomaticMapping.Get(gameAction);
        return new SmartBpStageDetectionResult
        {
            SchemaVersion = 1,
            RecognizedAction = action,
            ActiveSide = mapping.Region.StartsWith("left", StringComparison.Ordinal) ? "left" : "right",
            OperationRegion = mapping.Region,
            OperationOwner = action is "BanSur" or "PickHun" ? "hunter" : "survivor",
            TargetCamp = mapping.Camp,
            Confidence = confidence
        };
    }

    private static SmartBpBusinessStateRecognitionResult Business(string phase) => new()
    {
        Phase = phase,
        BannedSur = Enumerable.Range(0, 4).Select(x => new SmartBpRecognizedCharacterSlot { Index = x, CharacterName = "未选择" }).ToList(),
        BannedHun = Enumerable.Range(0, 2).Select(x => new SmartBpRecognizedCharacterSlot { Index = x, CharacterName = "未选择" }).ToList(),
        PickedSur = Enumerable.Range(0, 4).Select(x => new SmartBpRecognizedPlayerCharacterSlot { Index = x, CharacterName = "未选择" }).ToList(),
        PickedHun = new SmartBpRecognizedPlayerCharacterSlot { Index = 0, CharacterName = "未选择" }
    };

    private static SmartBpBusinessStateRecognitionResult Business(
        string phase,
        string? bannedSur0 = null,
        string? bannedHun0 = null,
        string? pickedHun = null,
        string? hunterPlayerId = null)
    {
        var state = Business(phase);
        if (bannedSur0 != null) state.BannedSur[0].CharacterName = bannedSur0;
        if (bannedHun0 != null) state.BannedHun[0].CharacterName = bannedHun0;
        if (pickedHun != null) state.PickedHun.CharacterName = pickedHun;
        if (hunterPlayerId != null) state.PickedHun.PlayerId = hunterPlayerId;
        return state;
    }

    private static ISmartBpCharacterResolver CreateResolver(params Character[] characters)
    {
        var shared = CreateShared(new Game(new Team(Camp.Sur, TeamType.HomeTeam), new Team(Camp.Hun, TeamType.AwayTeam), GameProgress.Free), characters);
        return CreateResolverFromShared(shared.Object);
    }

    private static ISmartBpCharacterResolver CreateResolverFromShared(ISharedDataService shared) =>
        new SmartBpCharacterResolver(new CharacterSelectionService(
            shared,
            Mock.Of<IFrontedTransitionOrchestrator>(),
            Mock.Of<IFrontedLayoutService>()));

    private static ISmartBpPlayerIdentityMatcher CreateMatcher(ISharedDataService shared) =>
        new SmartBpPlayerIdentityMatcher(shared);

    private static Mock<ISharedDataService> CreateShared(Game game, params Character[] characters)
    {
        var shared = new Mock<ISharedDataService>();
        shared.SetupGet(x => x.CurrentGame).Returns(game);
        shared.SetupGet(x => x.SurCharaDict).Returns(new SortedDictionary<string, Character>(characters.Where(x => x.Camp == Camp.Sur).ToDictionary(x => x.Name)));
        shared.SetupGet(x => x.HunCharaDict).Returns(new SortedDictionary<string, Character>(characters.Where(x => x.Camp == Camp.Hun).ToDictionary(x => x.Name)));
        return shared;
    }

    private static Mock<IGameGuidanceService> Guidance(GameAction action, IReadOnlyList<int> indexes)
    {
        var guidance = new Mock<IGameGuidanceService>();
        guidance.Setup(x => x.GetRuntimeSnapshot()).Returns(new GameGuidanceRuntimeSnapshot(true, 0, action, indexes, 30, [new(0, action, indexes, 30)]));
        return guidance;
    }

    private static SmartBpDetectedOperation Operation(SmartBpDetectedOperationKind kind, GameAction action, Camp camp, int slot, string rawName, string resolvedKey, IReadOnlyList<int> indexes) =>
        new(kind, action, indexes, camp, slot, rawName, resolvedKey, rawName, null, 1, "test");
}
