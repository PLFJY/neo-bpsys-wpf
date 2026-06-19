extern alias smartbp;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using Xunit;
using SmartBpRecognitionTask = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognitionTask;
using SmartBpCharacterResolver = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpCharacterResolver;
using SmartBpRecognitionJsonSchemaProvider = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpRecognitionJsonSchemaProvider;
using QwenModelAssetManager = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.QwenModelAssetManager;
using SmartBpPromptProfileProvider = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpPromptProfileProvider;
using LlamaCppRuntimeManifestProvider = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.LlamaCppRuntimeManifestProvider;
using LlamaCppRuntimeAssetManager = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.LlamaCppRuntimeAssetManager;
using SmartBpAiRecognitionService = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpAiRecognitionService;
using ISmartBpImageEncoder = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpImageEncoder;
using ILlamaCppOpenAiClient = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ILlamaCppOpenAiClient;
using ISmartBpCharacterResolver = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpCharacterResolver;
using ISmartBpRecognitionSettingsService = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpRecognitionSettingsService;
using SmartBpRecognitionSettings = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognitionSettings;
using SmartBpStageDetectionResult = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpStageDetectionResult;
using SmartBpBusinessStateRecognitionResult = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpBusinessStateRecognitionResult;
using SmartBpRecognizedCharacterSlot = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognizedCharacterSlot;
using SmartBpRecognizedPlayerCharacterSlot = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognizedPlayerCharacterSlot;
using SmartBpBusinessStateParser = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpBusinessStateParser;
using SmartBpAutomaticParser = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpAutomaticParser;
using SmartBpAutomaticMapping = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpAutomaticMapping;
using SmartBpGuidanceSyncService = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpGuidanceSyncService;

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
        var resolver = new SmartBpCharacterResolver(shared.Object);

        var normalized = resolver.Resolve("test-name", Camp.Sur, 0, .9);
        var unresolved = resolver.Resolve("test", Camp.Sur, 1, .4);

        Assert.Equal("Test Name", normalized.ResolvedCharacterName);
        Assert.Null(unresolved.ResolvedCharacterName);
    }

    [Theory]
    [InlineData(SmartBpRecognitionTask.PickSur)]
    [InlineData(SmartBpRecognitionTask.FullBpScan)]
    public void RecognitionTasksHaveStrictBpOnlySchemas(SmartBpRecognitionTask task)
    {
        var schema = SmartBpRecognitionJsonSchemaProvider.Get(task).ToJsonString();
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
        Assert.Contains("只输出合法 JSON", chinese.SystemPrompt);
        Assert.Contains("不要输出 MapBP", chinese.SystemPrompt);
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
        var schema = SmartBpRecognitionJsonSchemaProvider.Get(SmartBpRecognitionTask.FullBpScan).ToJsonString();
        Assert.Contains("\"phase\"", schema);
        Assert.Contains("\"banned_sur\"", schema);
        Assert.Contains("\"banned_hun\"", schema);
        Assert.Contains("\"picked_sur\"", schema);
        Assert.Contains("\"picked_hun\"", schema);
        Assert.DoesNotContain("teams", schema);
        Assert.DoesNotContain("all_characters", schema);
        Assert.DoesNotContain("all_player_ids", schema);
        Assert.DoesNotContain("warnings", schema);
        Assert.DoesNotContain("raw_visible_text", schema);
        Assert.DoesNotContain("confidence", schema);
    }

    [Fact]
    public void BusinessParserAcceptsSampleAndNormalizesUnknownCharacters()
    {
        const string json = """
        {"phase":"选择求生者","banned_sur":[{"index":0,"character_name":"小说家"},{"index":1,"character_name":"昆虫学者"},{"index":2,"character_name":"未选择"},{"index":3,"character_name":"未选择"}],"banned_hun":[{"index":0,"character_name":"梦之女巫"},{"index":1,"character_name":"女王蜂"}],"picked_sur":[{"index":0,"character_name":"\"心理学家\"","player_id":"IHiganbanaI"},{"index":1,"character_name":"守墓人","player_id":"夜风之缚"},{"index":2,"character_name":"unknown","player_id":"磁兮小狗"},{"index":3,"character_name":"","player_id":"叶落摘星"}],"picked_hun":{"index":0,"character_name":null,"player_id":"导播PLFJY"}}
        """;

        var parsed = SmartBpBusinessStateParser.Parse(json);

        Assert.Equal("选择求生者", parsed.Phase);
        Assert.Equal("\"心理学家\"", parsed.PickedSur[0].CharacterName);
        Assert.Equal("磁兮小狗", parsed.PickedSur[2].PlayerId);
        Assert.Equal("未选择", parsed.PickedSur[2].CharacterName);
        Assert.Equal("未选择", parsed.PickedSur[3].CharacterName);
        Assert.Equal("未选择", parsed.PickedHun.CharacterName);
    }

    [Theory]
    [InlineData("屏蔽求生者", GameAction.BanSur)]
    [InlineData("屏蔽监管者", GameAction.BanHun)]
    [InlineData("选择求生者", GameAction.PickSur)]
    [InlineData("求生者选择角色中", GameAction.DistributeChara)]
    [InlineData("选择监管者", GameAction.PickHun)]
    public void BusinessPhaseMapsToGuidanceAction(string phase, GameAction expected)
    {
        Assert.True(SmartBpAutomaticMapping.TryMapPhase(phase, out var action));
        Assert.Equal(expected, action);
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
        var resolver = new SmartBpCharacterResolver(shared.Object);
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
}
