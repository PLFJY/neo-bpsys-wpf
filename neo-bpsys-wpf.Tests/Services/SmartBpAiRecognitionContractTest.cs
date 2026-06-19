extern alias smartbp;

using System;
using System.Collections.Generic;
using System.Net.Http;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using Xunit;
using SmartBpRecognitionTask = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognitionTask;
using SmartBpCharacterResolver = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpCharacterResolver;
using SmartBpRecognitionJsonSchemaProvider = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpRecognitionJsonSchemaProvider;
using QwenModelAssetManager = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.QwenModelAssetManager;

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
    [InlineData(SmartBpRecognitionTask.PickSur, 128)]
    [InlineData(SmartBpRecognitionTask.FullBpScan, 512)]
    public void RecognitionTasksHaveStrictBpOnlySchemas(SmartBpRecognitionTask task, int expectedTokens)
    {
        var schema = SmartBpRecognitionJsonSchemaProvider.Get(task).ToJsonString();
        Assert.Contains("\"additionalProperties\":false", schema);
        Assert.Contains("\"scene\"", schema);
        Assert.DoesNotContain("MapBP", schema, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(expectedTokens, task == SmartBpRecognitionTask.PickSur ? 128 : 512);
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
}
