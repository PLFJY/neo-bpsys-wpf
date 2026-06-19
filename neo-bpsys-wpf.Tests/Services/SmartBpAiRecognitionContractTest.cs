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
}
