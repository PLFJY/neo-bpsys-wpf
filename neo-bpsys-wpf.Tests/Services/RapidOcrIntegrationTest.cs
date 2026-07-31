extern alias smartbp;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Services;
using OpenCvSharp;
using RapidOcrNet;
using SkiaSharp;
using Xunit;
using IRapidOcrModelManifestProvider = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.IRapidOcrModelManifestProvider;
using IRapidOcrModelAssetManager = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.IRapidOcrModelAssetManager;
using ISmartBpRecognitionSettingsService = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpRecognitionSettingsService;
using ISmartBpModuleStorageProvider = neo_bpsys_wpf.Core.Abstractions.Services.ISmartBpModuleStorageProvider;
using IRapidOcrEngine = smartbp::neo_bpsys_wpf.Services.IRapidOcrEngine;
using RapidOcrModelAssetManager = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.RapidOcrModelAssetManager;
using RapidOcrModelManifest = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.RapidOcrModelManifest;
using RapidOcrModelProfile = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.RapidOcrModelProfile;
using RapidOcrModelAsset = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.RapidOcrModelAsset;
using RapidOcrInstalledPaths = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.RapidOcrInstalledPaths;
using RapidOcrModelStatus = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.RapidOcrModelStatus;
using RapidOcrModelUpdateCheckResult = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.RapidOcrModelUpdateCheckResult;
using RapidOcrRawLine = smartbp::neo_bpsys_wpf.Services.RapidOcrRawLine;
using RapidOcrNetProvider = smartbp::neo_bpsys_wpf.Services.RapidOcrNetProvider;
using SmartBpDownloadState = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpDownloadState;
using SmartBpRecognitionSettings = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognitionSettings;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class RapidOcrIntegrationTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "neo-bpsys-rapidocr-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void RapidProfileUsesOfficialDirectModelScopeUrl()
    {
        var profile = Profile();
        Assert.Equal(
            "https://www.modelscope.cn/models/RapidAI/RapidOCR/resolve/v3.8.0/onnx/PP-OCRv5/det/ch_PP-OCRv5_det_mobile.onnx",
            profile.Det.DownloadUrl);
    }

    [Fact]
    public async Task BundledManifestContainsChineseJapaneseAndEnglishOfficialProfiles()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "SmartBp", "RapidOcrModelManifest.json");
        await using var stream = File.OpenRead(path);
        var manifest = await JsonSerializer.DeserializeAsync<RapidOcrModelManifest>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, TestContext.Current.CancellationToken);

        Assert.NotNull(manifest);
        Assert.Equal(["ppocr-v5-zh-mobile", "ppocr-v4-ja-mobile", "ppocr-v5-en-mobile"],
            manifest.Models.Select(profile => profile.Id));
        Assert.All(manifest.Models, profile => Assert.Equal("v3.8.0", profile.Version));
        Assert.All(manifest.Models.SelectMany(profile => new[] { profile.Det, profile.Cls, profile.Rec, profile.Dict }),
            asset => Assert.StartsWith("https://www.modelscope.cn/models/RapidAI/RapidOCR/resolve/v3.8.0/", asset.DownloadUrl));
    }

    [Fact]
    public void PaddleYamlDictionaryTransformProducesUtf8Dictionary()
    {
        var characters = Enumerable.Range(0, 101).Select(index => ((char)(0x4e00 + index)).ToString()).ToArray();
        var yaml = "PostProcess:\n  name: CTCLabelDecode\n  character_dict:\n" +
                   string.Join('\n', characters.Select(character => $"  - {character}")) + "\n  use_space_char: true\n";

        var bytes = RapidOcrModelAssetManager.ExtractPaddleCharacterDictionary(System.Text.Encoding.UTF8.GetBytes(yaml));

        var result = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.StartsWith(characters[0] + "\n", result);
        Assert.EndsWith(characters[^1] + "\n", result);
    }

    [Fact]
    public async Task ManagedStatusRequiresAllFourFilesAndDeleteRemovesProfile()
    {
        var profile = Profile();
        var storage = new FakeStorage(_root);
        var settings = new FakeSettings();
        var manager = new RapidOcrModelAssetManager(new FakeManifest(profile), settings, storage,
            NullLogger<RapidOcrModelAssetManager>.Instance,
            new FileDownloadService(() => new HttpClient()));

        var missing = await manager.GetStatusAsync(TestContext.Current.CancellationToken);
        Assert.False(missing.IsInstalled);
        Assert.Equal(4, missing.MissingFiles.Count);

        var directory = Path.Combine(storage.RapidOcrModelsRoot, profile.Id);
        Directory.CreateDirectory(directory);
        foreach (var asset in new[] { profile.Det, profile.Cls, profile.Rec, profile.Dict })
            await File.WriteAllTextAsync(Path.Combine(directory, asset.FileName), "test", TestContext.Current.CancellationToken);
        var legacyStatus = await manager.GetStatusAsync(TestContext.Current.CancellationToken);
        Assert.True(legacyStatus.IsInstalled);
        Assert.True(legacyStatus.HasUpdate);
        Assert.Null(legacyStatus.InstalledVersion);

        await File.WriteAllTextAsync(
            Path.Combine(directory, ".smartbp-install.json"),
            JsonSerializer.Serialize(new
            {
                ProfileId = profile.Id,
                Version = profile.Version,
                ManifestFingerprint = RapidOcrModelAssetManager.ComputeProfileFingerprint(profile),
                InstalledAt = DateTimeOffset.UtcNow
            }),
            TestContext.Current.CancellationToken);
        var currentStatus = await manager.GetStatusAsync(TestContext.Current.CancellationToken);
        Assert.False(currentStatus.HasUpdate);
        Assert.Equal("v3.8.0", currentStatus.InstalledVersion);

        profile.Rec.Sha256 = "changed";
        Assert.True((await manager.GetStatusAsync(TestContext.Current.CancellationToken)).HasUpdate);

        await manager.DeleteAsync(profile.Id, TestContext.Current.CancellationToken);
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public void RapidOutputIsClampedSortedAndDeduplicated()
    {
        RapidOcrRawLine[] raw =
        [
            new("下面", .8, [new Point(50, 70), new Point(90, 70), new Point(90, 90), new Point(50, 90)]),
            new("上面", .6, [new Point(-5, -4), new Point(40, 0), new Point(40, 20), new Point(0, 20)]),
            new("上面", .95, [new Point(0, 0), new Point(42, 0), new Point(42, 21), new Point(0, 21)]),
            new("右侧", .7, [new Point(70, 0), new Point(140, 0), new Point(140, 20), new Point(70, 20)])
        ];

        var lines = RapidOcrNetProvider.NormalizeAndMerge(raw, 100, 100);

        Assert.Equal(3, lines.Count);
        Assert.Equal("上面", lines[0].Text);
        Assert.Equal(.95, lines[0].Confidence, 3);
        Assert.Equal("右侧", lines[1].Text);
        Assert.Equal(new Rect(70, 0, 30, 20), lines[1].BoundingBox);
        Assert.Equal("下面", lines[2].Text);
        Assert.All(lines, line => Assert.Equal("RapidOCR", line.Provider));
    }

    [Fact]
    public void RapidProviderReadinessDoesNotInitializeNativeRuntime()
    {
        var status = new RapidOcrModelStatus("ppocr-v5-zh-mobile", _root, true, []);
        var paths = new RapidOcrInstalledPaths("ppocr-v5-zh-mobile", _root,
            Path.Combine(_root, "det.onnx"),
            Path.Combine(_root, "cls.onnx"),
            Path.Combine(_root, "rec.onnx"),
            Path.Combine(_root, "dict.txt"));
        var engine = new CountingRapidOcrEngine();
        var provider = new RapidOcrNetProvider(new FakeRapidAssetManager(status, paths), new FakeSettings(),
            NullLogger<RapidOcrNetProvider>.Instance, engine);

        Assert.True(provider.IsReady);
        Assert.Equal(0, engine.InitializeCount);

        using var image = new Mat(4, 4, MatType.CV_8UC3, Scalar.Black);
        provider.RecognizeTextLines(image);

        Assert.Equal(1, engine.InitializeCount);
    }

    [Fact]
    public void OfficialManifestVersionIsReadFromMatchingOnnxRecognizer()
    {
        const string yaml = """
            onnxruntime:
              PP-OCRv5:
                rec:
                  ch_PP-OCRv5_rec_mobile:
                    model_dir: https://www.modelscope.cn/models/RapidAI/RapidOCR/resolve/v3.9.1/onnx/PP-OCRv5/rec/ch_PP-OCRv5_rec_mobile.onnx
                    SHA256: abc
            """;

        var version = RapidOcrModelAssetManager.ExtractOfficialVersion(
            yaml,
            "https://www.modelscope.cn/models/RapidAI/RapidOCR/resolve/v3.8.0/onnx/PP-OCRv5/rec/ch_PP-OCRv5_rec_mobile.onnx");

        Assert.Equal("v3.9.1", version);
    }

    private static RapidOcrModelProfile Profile() => new()
    {
        Id = "ppocr-v5-zh-mobile",
        Version = "v3.8.0",
        Det = new RapidOcrModelAsset
        {
            FileName = "det.onnx",
            RemotePath = "det.onnx",
            DownloadUrl = "https://www.modelscope.cn/models/RapidAI/RapidOCR/resolve/v3.8.0/onnx/PP-OCRv5/det/ch_PP-OCRv5_det_mobile.onnx"
        },
        Cls = new RapidOcrModelAsset { FileName = "cls.onnx", RemotePath = "cls.onnx", DownloadUrl = "https://example.test/cls.onnx" },
        Rec = new RapidOcrModelAsset { FileName = "rec.onnx", RemotePath = "rec.onnx", DownloadUrl = "https://example.test/rec.onnx" },
        Dict = new RapidOcrModelAsset { FileName = "dict.txt", RemotePath = "dict.txt", DownloadUrl = "https://example.test/dict.txt" }
    };

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private sealed class FakeSettings : ISmartBpRecognitionSettingsService
    {
        public SmartBpRecognitionSettings Settings { get; } = new();
        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeManifest(RapidOcrModelProfile profile) : IRapidOcrModelManifestProvider
    {
        public Task<RapidOcrModelManifest> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new RapidOcrModelManifest { Models = [profile] });
    }

    private sealed class FakeStorage(string root) : ISmartBpModuleStorageProvider
    {
        public string ModuleRoot => root;
        public string PaddleRuntimeRoot => Path.Combine(root, "Runtime", "Paddle");
        public string OcrModelsRoot => Path.Combine(root, "OCRModels");
        public string TesseractDataRoot => Path.Combine(OcrModelsRoot, "Tesseract", "tessdata");
        public string RapidOcrModelsRoot => Path.Combine(OcrModelsRoot, "RapidOCR", "Models");
        public string RecognitionLogsRoot => Path.Combine(root, "RecognitionLogs");
    }

    private sealed class FakeRapidAssetManager(
        RapidOcrModelStatus status,
        RapidOcrInstalledPaths paths) : IRapidOcrModelAssetManager
    {
        public event EventHandler<SmartBpDownloadState> StateChanged;
        public RapidOcrModelStatus Status => status;
        public Task<RapidOcrModelStatus> GetStatusAsync(CancellationToken cancellationToken = default) => Task.FromResult(status);
        public Task<IReadOnlyList<RapidOcrModelProfile>> GetAvailableProfilesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RapidOcrModelProfile>>([]);
        public Task<RapidOcrModelUpdateCheckResult> CheckForUpdatesAsync(string profileId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RapidOcrModelUpdateCheckResult(null, "v3.8.0", "v3.8.0", false, true));
        public Task InstallAsync(string profileId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(string profileId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Pause() => StateChanged?.Invoke(this, new SmartBpDownloadState(true, null, "paused", IsPaused: true));
        public void Resume() => StateChanged?.Invoke(this, new SmartBpDownloadState(true, null, "resumed", null));
        public void Cancel() => StateChanged?.Invoke(this, new SmartBpDownloadState(false, null, "cancelled", null));
        public Task<RapidOcrInstalledPaths> GetInstalledPathsAsync(CancellationToken cancellationToken = default) => Task.FromResult(paths);
    }

    private sealed class CountingRapidOcrEngine : IRapidOcrEngine
    {
        public int InitializeCount { get; private set; }
        public void Initialize(string detPath, string clsPath, string recPath, string dictPath) => InitializeCount++;
        public IReadOnlyList<RapidOcrRawLine> Detect(SKBitmap bitmap, RapidOcrOptions options) => [];
        public void Dispose() { }
    }
}
