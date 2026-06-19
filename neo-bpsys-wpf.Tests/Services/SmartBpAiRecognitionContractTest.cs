extern alias smartbp;

using System;
using System.Collections.Generic;
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
        Assert.Contains("\"schema_version\"", schema);
        Assert.Contains("\"all_player_ids\"", schema);
        Assert.Contains("\"raw_visible_text\"", schema);
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

        var moved = await service.SyncAsync(Stage("BanHun", .95), TestContext.Current.CancellationToken);
        var refusedBackward = await service.SyncAsync(Stage("BanSur", .95), TestContext.Current.CancellationToken);

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
            .SyncAsync(Stage("BanSur", .79), TestContext.Current.CancellationToken);
        Assert.False(result.IsAccepted);
        guidance.Verify(x => x.MoveToStepAsync(It.IsAny<int>()), Times.Never);
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
    public void ParserPreservesVisualOcrContextAndUnresolvedNames()
    {
        var shared = new Mock<ISharedDataService>();
        shared.SetupGet(x => x.SurCharaDict).Returns(new SortedDictionary<string, Character> { ["祭司"] = new("祭司", Camp.Sur, "priestess") });
        shared.SetupGet(x => x.HunCharaDict).Returns([]);
        var resolver = new SmartBpCharacterResolver(shared.Object);
        var settings = new Mock<ISmartBpRecognitionSettingsService>(); settings.SetupGet(x => x.Settings).Returns(new SmartBpRecognitionSettings());
        var service = new SmartBpAiRecognitionService(Mock.Of<ISmartBpImageEncoder>(), Mock.Of<ILlamaCppOpenAiClient>(), resolver, settings.Object, NullLogger<SmartBpAiRecognitionService>.Instance);
        const string json = """
        {"schema_version":1,"scene":{"game":"Identity V","interface_type":"ban_pick_or_lineup_selection","task":"PickSur","main_status":"选择中","pause_status":null,"pause_remaining_seconds":null},"teams":[{"side":"left","faction":"survivor","title_text":null,"subtitle_text":null,"slots":[{"slot_index":0,"slot_state":"selected","character_name":"祭司","player_id":"玩家Ω","is_banned_or_unavailable":false,"raw_visible_text":"玩家Ω / 祭司","confidence":0.98},{"slot_index":1,"slot_state":"selected","character_name":"未知角色","player_id":"P2","is_banned_or_unavailable":false,"raw_visible_text":"P2 / 未知角色","confidence":0.6}]}],"all_characters":[],"all_player_ids":[{"player_id":"玩家Ω","character_name":"祭司","side":"left","slot_index":0,"confidence":0.98}],"warnings":[]}
        """;
        var (visual, resolved) = service.Parse(json, SmartBpRecognitionTask.PickSur);
        Assert.Contains("playerId=玩家Ω", visual);
        Assert.Contains("rawText=玩家Ω / 祭司", visual);
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
}
