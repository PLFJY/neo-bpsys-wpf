extern alias smartbp;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Services;
using Xunit;
using SmartBpRecognitionTask = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognitionTask;
using SmartBpRecognitionStrategy = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognitionStrategy;
using SmartBpHybridFusionMode = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpHybridFusionMode;
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
            raw, "屏蔽求生者", ["banned_sur"], ["小说家"], ["厂长"], Mock.Of<ICharacterSelectionService>(), out var diagnostics);

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
            raw, "屏蔽求生者", ["banned_sur"], ["小说家"], ["厂长"], Mock.Of<ICharacterSelectionService>(), out _));

        Assert.Contains($"contained unexpected property {unexpectedProperty}", error.Message);
    }

    [Fact]
    public void RecognitionStrategyContainsFourFirstClassStrategies()
    {
        Assert.Equal(
            [SmartBpRecognitionStrategy.PureOcr, SmartBpRecognitionStrategy.PureAi, SmartBpRecognitionStrategy.AiWithOcr, SmartBpRecognitionStrategy.AiWithAiOcr],
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
    public async Task QwenManifestContainsTwoBandPointModelProfiles()
    {
        var manifest = await new QwenModelManifestProvider(NullLogger<QwenModelManifestProvider>.Instance).LoadAsync(TestContext.Current.CancellationToken);

        Assert.Contains(manifest.Models, model => model.Id == "qwen3.5-2b-q4km");
        Assert.Contains(manifest.Models, model => model.Id == "qwen3.5-0.8b-q4km");
    }

    [Fact]
    public async Task LocalVisionManifestContainsPaddleOcrVlModelScopeProfile()
    {
        var manifest = await new QwenModelManifestProvider(NullLogger<QwenModelManifestProvider>.Instance).LoadAsync(TestContext.Current.CancellationToken);

        var profile = Assert.Single(manifest.Models.Where(model => model.Id == "paddleocr-vl-1.6-gguf"));
        Assert.Equal("PaddleOCR-VL 1.6 GGUF", profile.DisplayName);
        Assert.Equal(LocalVisionModelFamily.PaddleOcrVl, profile.Family);
        Assert.Equal(LocalVisionModelRole.AiOcrTextExtractor, profile.Role);
        Assert.False(profile.Recommended);
        Assert.True(profile.Experimental);
        Assert.Equal("https://www.modelscope.cn/models/PaddlePaddle/PaddleOCR-VL-1.6-GGUF/resolve/master/PaddleOCR-VL-1.6-GGUF.gguf", profile.ModelUrl);
        Assert.Equal("PaddleOCR-VL-1.6-GGUF.gguf", profile.ModelFileName);
        Assert.Equal("f3ae46ec885050acf4b3d31944431e1fd90d50664fb09126af4a3c050ba14ee8", profile.Sha256);
        Assert.Equal("https://www.modelscope.cn/models/PaddlePaddle/PaddleOCR-VL-1.6-GGUF/resolve/master/PaddleOCR-VL-1.6-GGUF-mmproj.gguf", profile.MmprojUrl);
        Assert.Equal("PaddleOCR-VL-1.6-GGUF-mmproj.gguf", profile.MmprojFileName);
        Assert.Equal("204d757d7610d9b3faab10d506d69e5b244e32bf765e2bab2d0167e65e0a058a", profile.MmprojSha256);
        Assert.Equal(QwenMmprojMode.Separate, profile.MmprojMode);
    }

    [Fact]
    public async Task LocalVisionManifestAssignsModelRoles()
    {
        var manifest = await new QwenModelManifestProvider(NullLogger<QwenModelManifestProvider>.Instance).LoadAsync(TestContext.Current.CancellationToken);

        Assert.Contains(manifest.Models, model => model.Id == "glm-ocr-q4km" &&
                                                  model.Role == LocalVisionModelRole.AiOcrTextExtractor &&
                                                  !model.Recommended);
        Assert.Contains(manifest.Models, model => model.Id == "qwen3.5-2b-q4km" &&
                                                  model.Role is LocalVisionModelRole.BusinessVlm or LocalVisionModelRole.Both);
        Assert.Contains(manifest.Models, model => model.Id == "qwen3.5-0.8b-q4km" &&
                                                  model.Role is LocalVisionModelRole.BusinessVlm or LocalVisionModelRole.AiOcrTextExtractor or LocalVisionModelRole.Both &&
                                                  model.Experimental);
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

    [Fact]
    public async Task BundledPromptProfilesLoadAndChineseProfileEnforcesJsonAndNoMapBp()
    {
        var provider = new SmartBpPromptProfileProvider();
        var profiles = await provider.GetAvailableProfilesAsync(TestContext.Current.CancellationToken);
        var chinese = await provider.LoadAsync("zh-CN", TestContext.Current.CancellationToken);
        Assert.Equal(3, profiles.Count);
        Assert.Contains("你只输出一个业务 JSON", chinese.SystemPrompt);
        Assert.Contains("非活动侧显示的“等待中”不能决定 phase", chinese.SystemPrompt);
        Assert.Contains("右上大标题包含“屏蔽求生者” => phase = \"屏蔽求生者\"", chinese.SystemPrompt);
        Assert.Contains("左上大标题包含“屏蔽监管者” => phase = \"屏蔽监管者\"", chinese.SystemPrompt);
        Assert.Contains("右下监管者头像下方第二行", chinese.SystemPrompt);
        Assert.Contains("不要因为低亮度、禁用符号、打勾、半透明、背景暗，就把可读角色输出为“未选择”", chinese.SystemPrompt);
        Assert.Contains("如果画面显示 “心理学家” 或 \"心理学家\"，但候选列表中是 心理学家，输出 \"心理学家\"", chinese.SystemPrompt);
        Assert.Contains("禁用符号不是未选择", chinese.SystemPrompt);
        Assert.Contains("红色禁止符号", chinese.SystemPrompt);
        Assert.Contains("banned_sur 来自右上区域", chinese.SystemPrompt);
        Assert.Contains("banned_hun 来自左上区域", chinese.SystemPrompt);
        Assert.Contains("求生者选择天赋中", chinese.SystemPrompt);
        Assert.Contains("监管者选择天赋中", chinese.SystemPrompt);
        Assert.Contains("天赋已锁定", chinese.SystemPrompt);
        Assert.Contains("MapBP 字段", chinese.SystemPrompt);
        Assert.Contains("左上 = 求生者方禁用监管者区域", chinese.SystemPrompt);
        Assert.Contains("右上 = 监管者方禁用求生者区域", chinese.SystemPrompt);
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
        var builder = new SmartBpCandidateOperationBuilder(CreateResolver(novelist, dreamWitch), CreateShared(new Game(new Team(Camp.Sur, TeamType.HomeTeam), new Team(Camp.Hun, TeamType.AwayTeam), GameProgress.Free), novelist, dreamWitch).Object);

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
        var builder = new SmartBpCandidateOperationBuilder(CreateResolver(hunter), CreateShared(game, hunter).Object);

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
        var builder = new SmartBpCandidateOperationBuilder(CreateResolver(), CreateShared(game).Object);

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
        var builder = new SmartBpCandidateOperationBuilder(CreateResolverFromShared(shared.Object), shared.Object);
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
    public async Task RuntimeManifestVersionMatchesReleaseAssets()
    {
        var manifest = await new LlamaCppRuntimeManifestProvider().LoadAsync(TestContext.Current.CancellationToken);
        Assert.EndsWith(manifest.RuntimeVersion, manifest.ReleasePage);
        Assert.All(manifest.Assets, asset => Assert.Contains($"/{manifest.RuntimeVersion}/", asset.Url));
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

        Assert.Contains("_autoRecognitionCoordinator.RunOneTickDryRunAsync(frame)", speedMethod);
        Assert.Contains("_ocrBpRecognitionService.RecognizeAsync(frame", speedMethod);
        Assert.Contains("SelectedAiTestFrame ?? AiTestFrames.FirstOrDefault()", speedMethod);
        Assert.DoesNotContain("foreach (var testFrame in AiTestFrames)", speedMethod);
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
        Assert.Contains("RunPureAiFullRecognitionDebugCoreAsync(frame)", viewModel);
        Assert.Contains("_aiRecognitionService.RecognizeAsync(frame, SmartBpRecognitionTask.FullBpScan)", viewModel);
        Assert.Contains("_autoRecognitionCoordinator.RunFullRecognitionDebugAsync(frame)", viewModel);
        Assert.Contains("_autoRecognitionCoordinator.RunPhaseOnlyDebugAsync(frame)", viewModel);
        Assert.Contains("RunPhaseOnlyRecognitionCoreAsync(LoadTestFrame(SelectedAiTestFrame))", viewModel);
        Assert.Contains("SmartBpRecognitionStrategy.PureAi", viewModel);
        Assert.Contains("FullBpScan", viewModel);
    }

    [Fact]
    public void SmartBpDebugUiExposesSeparatePhaseOnlyAndRoleServerControls()
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
        Assert.Contains("StartBusinessAiServerCommand", xaml);
        Assert.Contains("StartAiOcrServerCommand", xaml);
        Assert.Contains("StartRequiredLlamaServersCommand", xaml);
        Assert.Contains("BusinessAiServerStatus", xaml);
        Assert.Contains("AiOcrServerReuseStatus", xaml);
        Assert.Contains("DebugFinalBusinessState", xaml);
        Assert.Contains("OpenRecognitionDebugLogWindowCommand", xaml);

        Assert.Contains("_llamaServerManagers.Get(LlamaVisionServerRole.BusinessAi)", viewModel);
        Assert.Contains("_llamaServerManagers.Get(LlamaVisionServerRole.AiOcr)", viewModel);
        Assert.Contains("IsAiOcrReusingBusinessServer()", viewModel);
        Assert.Contains("_autoRecognitionCoordinator.RunIncrementalRecognitionDebugAsync(frame)", viewModel);
        Assert.Contains("CurrentStageIncremental", viewModel);
        Assert.Contains("PhaseOnly", viewModel);
        Assert.Contains("FullImage", viewModel);
    }

    [Fact]
    public void SmartBpRuntimeAndModelDownloadUiUsesRoleSpecificBindings()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "Views", "SmartBpModuleContentView.xaml"));
        var viewModel = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "ViewModels", "SmartBpModuleContentViewModel.AiRecognition.cs"));

        Assert.Contains("CheckLlamaRuntimeUpdateCommand", xaml);
        Assert.Contains("LlamaRuntimeUpdateStatus", xaml);
        Assert.Contains("BusinessAiModelDownloadProgress", xaml);
        Assert.Contains("BusinessAiModelDownloadDetail", xaml);
        Assert.Contains("AiOcrModelDownloadProgress", xaml);
        Assert.Contains("AiOcrModelDownloadDetail", xaml);
        Assert.Contains("CancelBusinessAiModelDownloadCommand", xaml);
        Assert.Contains("CancelAiOcrModelDownloadCommand", xaml);
        Assert.Contains("ApplyVisionModelDownloadState", viewModel);
        Assert.Contains("LocalVisionModelDownloadRole.BusinessAi", viewModel);
        Assert.Contains("LocalVisionModelDownloadRole.AiOcr", viewModel);
    }

    [Fact]
    public void HybridStrategyFusionUsesBusinessAiByDefaultForAiOcr()
    {
        var root = FindRepositoryRoot();
        var coordinator = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "Services", "Recognition", "SmartBpAutomaticServices.cs"));
        var services = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "Services", "Recognition", "RecognitionServices.cs"));
        var xaml = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "Views", "SmartBpModuleContentView.xaml"));

        Assert.Contains("AI + OCR fusion_mode=", coordinator);
        Assert.Contains("AiWithAiOcrFusionMode == SmartBpHybridFusionMode.BusinessAi", coordinator);
        Assert.Contains("businessAiFusion.FuseAsync", coordinator);
        Assert.Contains("AI + AI OCR fusion_mode=BusinessAi", coordinator);
        Assert.Contains("AI + AI OCR fusion_mode=LocalCSharp", coordinator);
        Assert.Contains("SmartBpBusinessAiFusionService", services);
        Assert.Contains("FuseTranscriptEvidenceAsync", services);
        Assert.Contains("SmartBpAiWithOcrFusionMode", xaml);
        Assert.Contains("SmartBpAiWithAiOcrFusionMode", xaml);
    }

    [Fact]
    public void BusinessAiFusionPromptCarriesRawEvidenceCandidatesAndStrictResponsibilities()
    {
        var root = FindRepositoryRoot();
        var services = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "Services", "Recognition", "RecognitionServices.cs"));
        var coordinator = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "Services", "Recognition", "SmartBpAutomaticServices.cs"));

        Assert.Contains("rawTranscript", services);
        Assert.Contains("survivorCandidates", services);
        Assert.Contains("hunterCandidates", services);
        Assert.Contains("phase is locked", services);
        Assert.Contains("right_top -> banned_sur", services);
        Assert.Contains("left_top -> banned_hun", services);
        Assert.Contains("left_bottom -> picked_sur", services);
        Assert.Contains("right_bottom -> picked_hun", services);
        Assert.Contains("小说家 昆虫学者 未选择 未选择", services);
        Assert.Contains("未选择导播PLFJY", services);
        Assert.Contains("未授权", services);
        Assert.Contains("未经授权的页面将无法识别出来。", services);
        Assert.Contains("CreateCurrentKnownStateJson(currentKnownState)", services);
        Assert.Contains("AiStructuredOutputMode.JsonSchemaStrict", services);
        Assert.Contains("Business AI fusion validation failed; corrupted updates were not merged.", coordinator);
        Assert.Contains("if (!isDryRun && delta != null)", coordinator);
    }

    [Fact]
    public void AiOcrTranscriptParsingFallsBackToPlainText()
    {
        var newline = SmartBpAiOcrTranscriptRecognitionService.ParseLines("小说家\n昆虫学者\n入殓师");
        var spaces = SmartBpAiOcrTranscriptRecognitionService.ParseLines("小说家 昆虫学者 未选择 未选择");
        var combinedNoise = SmartBpAiOcrTranscriptRecognitionService.ParseLines("未选择导播PLFJY");

        Assert.Equal(["小说家", "昆虫学者", "入殓师"], newline.Lines.Select(line => line.Text));
        Assert.Equal(["小说家", "昆虫学者", "未选择", "未选择"], spaces.Lines.Select(line => line.Text));
        Assert.Equal("未选择导播PLFJY", Assert.Single(combinedNoise.Lines).Text);
        Assert.Contains("AI OCR transcript parsed as plain text fallback.", combinedNoise.Diagnostics);
    }

    [Theory]
    [InlineData("{\"lines\":[{\"text\":\"小说家\"},{\"text\":\"昆虫学者\"}]}")]
    [InlineData("```json\n{\"lines\":[{\"text\":\"小说家\"},{\"text\":\"昆虫学者\"}]}\n```")]
    public void AiOcrTranscriptParsingAcceptsJsonAndFencedJson(string raw)
    {
        var parsed = SmartBpAiOcrTranscriptRecognitionService.ParseLines(raw);

        Assert.Equal(["小说家", "昆虫学者"], parsed.Lines.Select(line => line.Text));
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
        Assert.Contains("aiOcrTranscriptInterpreter.Interpret", coordinator);
        Assert.DoesNotContain("new OcrTextLine(line.Text", coordinator);
        Assert.DoesNotContain("new OpenCvSharp.Rect", coordinator);
    }

    [Fact]
    public void LlamaServerStartupShowsBusyProgressUntilStartCompletes()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "Views", "SmartBpModuleContentView.xaml"));
        var viewModel = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "ViewModels", "SmartBpModuleContentViewModel.AiRecognition.cs"));

        Assert.Contains("<ui:ProgressRing", xaml);
        Assert.Contains("IsBusinessAiServerStarting", xaml);
        Assert.Contains("IsAiOcrServerStarting", xaml);
        Assert.Contains("IsRequiredLlamaServersStarting", xaml);
        Assert.Contains("SetRoleServerStarting(role, true)", viewModel);
        Assert.Contains("finally", viewModel);
        Assert.Contains("SetRoleServerStarting(role, false)", viewModel);
        Assert.Contains("SmartBpAiStatusStarting", viewModel);
    }

    [Fact]
    public void ModelSwitchPreservesRoleServerAndShowsRestartProgress()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "Views", "SmartBpModuleContentView.xaml"));
        var viewModel = File.ReadAllText(Path.Combine(root, "neo-bpsys-wpf.SmartBp.Module", "ViewModels", "SmartBpModuleContentViewModel.AiRecognition.cs"));

        Assert.Contains("SetRoleServerStarting(LlamaVisionServerRole.BusinessAi, true, true)", viewModel);
        Assert.Contains("SetRoleServerStarting(LlamaVisionServerRole.AiOcr, true, true)", viewModel);
        Assert.Contains("var aiOcrRoleWasRunning = wasReusingBusiness ? business.IsRunning : aiOcr.IsRunning", viewModel);
        Assert.Contains("await business.StartAsync()", viewModel);
        Assert.Contains("await aiOcr.StartAsync()", viewModel);
        Assert.Contains("BusinessAiServerActivityText", xaml);
        Assert.Contains("AiOcrServerActivityText", xaml);
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
