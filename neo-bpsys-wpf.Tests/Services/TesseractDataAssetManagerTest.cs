extern alias smartbp;

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using Xunit;
using ITesseractDataAssetManager = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ITesseractDataAssetManager;
using ISmartBpRecognitionSettingsService = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpRecognitionSettingsService;
using SmartBpDownloadState = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpDownloadState;
using SmartBpRecognitionSettings = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpRecognitionSettings;
using TesseractDataAssetManager = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.TesseractDataAssetManager;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class TesseractDataAssetManagerTest
{
    [Fact]
    public async Task StatusReportsBothDefaultLanguagesMissing()
    {
        using var directory = new TemporaryDirectory();
        var manager = Create(directory.Path);

        var status = await manager.GetStatusAsync();

        Assert.False(status.IsInstalled);
        Assert.Equal(["chi_sim", "eng"], status.MissingLanguages);
        Assert.Empty(status.InstalledLanguages);
    }

    [Fact]
    public async Task StatusReportsInstalledWhenBothFilesExist()
    {
        using var directory = new TemporaryDirectory();
        await File.WriteAllBytesAsync(Path.Combine(directory.Path, "chi_sim.traineddata"), [1]);
        await File.WriteAllBytesAsync(Path.Combine(directory.Path, "eng.traineddata"), [1]);
        var manager = Create(directory.Path);

        var status = await manager.GetStatusAsync();

        Assert.True(status.IsInstalled);
        Assert.Empty(status.MissingLanguages);
        Assert.Equal(["chi_sim", "eng"], status.InstalledLanguages);
    }

    [Fact]
    public async Task StatusUsesConfiguredLanguagesInsteadOfHardcodedDefaults()
    {
        using var directory = new TemporaryDirectory();
        await File.WriteAllBytesAsync(Path.Combine(directory.Path, "jpn.traineddata"), [1]);
        var manager = Create(directory.Path, "jpn");

        var status = await manager.GetStatusAsync();

        Assert.True(status.IsInstalled);
        Assert.Empty(status.MissingLanguages);
        Assert.Contains("jpn", status.InstalledLanguages);
    }

    [Fact]
    public async Task DeleteRemovesManagedLanguageFilesOnly()
    {
        using var directory = new TemporaryDirectory();
        await File.WriteAllBytesAsync(Path.Combine(directory.Path, "chi_sim.traineddata"), [1]);
        await File.WriteAllBytesAsync(Path.Combine(directory.Path, "eng.traineddata"), [1]);
        await File.WriteAllBytesAsync(Path.Combine(directory.Path, "jpn.traineddata"), [1]);
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "custom.traineddata"), "keep");
        var manager = Create(directory.Path);

        await manager.DeleteAsync(["chi_sim", "eng", "jpn"]);

        Assert.False(File.Exists(Path.Combine(directory.Path, "chi_sim.traineddata")));
        Assert.False(File.Exists(Path.Combine(directory.Path, "eng.traineddata")));
        Assert.False(File.Exists(Path.Combine(directory.Path, "jpn.traineddata")));
        Assert.True(File.Exists(Path.Combine(directory.Path, "custom.traineddata")));
    }

    [Fact]
    public void AvailableLanguagesIncludeSupportedTesseractAssets()
    {
        using var directory = new TemporaryDirectory();
        var manager = Create(directory.Path);

        var languages = manager.GetAvailableLanguages();

        Assert.Contains(languages, language => language.Language == "chi_sim");
        Assert.Contains(languages, language => language.Language == "eng");
        Assert.Contains(languages, language => language.Language == "jpn");
    }

    [Fact]
    public async Task DeleteSelectedLanguageDoesNotRemoveOtherInstalledLanguages()
    {
        using var directory = new TemporaryDirectory();
        await File.WriteAllBytesAsync(Path.Combine(directory.Path, "chi_sim.traineddata"), [1]);
        await File.WriteAllBytesAsync(Path.Combine(directory.Path, "eng.traineddata"), [1]);
        await File.WriteAllBytesAsync(Path.Combine(directory.Path, "jpn.traineddata"), [1]);
        var manager = Create(directory.Path);

        await manager.DeleteAsync(["eng"]);

        Assert.True(File.Exists(Path.Combine(directory.Path, "chi_sim.traineddata")));
        Assert.False(File.Exists(Path.Combine(directory.Path, "eng.traineddata")));
        Assert.True(File.Exists(Path.Combine(directory.Path, "jpn.traineddata")));
    }

    [Fact]
    public async Task InstallSelectedLanguagesClearsGithubMirrorCacheBeforeCheckingAssets()
    {
        using var directory = new TemporaryDirectory();
        await File.WriteAllBytesAsync(Path.Combine(directory.Path, "chi_sim.traineddata"), [1]);
        await File.WriteAllBytesAsync(Path.Combine(directory.Path, "eng.traineddata"), [1]);
        var resolver = new Mock<IGitHubDownloadUrlResolver>();
        var manager = new TesseractDataAssetManager(new FakeStorageProvider(directory.Path), new FakeSettingsService(), resolver.Object);

        await manager.InstallLanguagesAsync(["chi_sim", "eng"]);

        resolver.Verify(x => x.ResetCache(), Times.Once);
    }

    [Fact]
    public async Task InstallReportsDownloadFailureMessage()
    {
        using var directory = new TemporaryDirectory();
        var resolver = new Mock<IGitHubDownloadUrlResolver>();
        resolver.Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("resolver boom"));
        var manager = new TesseractDataAssetManager(new FakeStorageProvider(directory.Path), new FakeSettingsService(), resolver.Object);
        SmartBpDownloadState? lastState = null;
        manager.StateChanged += (_, state) => lastState = state;

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.InstallLanguagesAsync(["eng"]));

        Assert.NotNull(lastState);
        Assert.False(lastState.IsDownloading);
        Assert.Equal("SmartBpDownloadFailedSimple", lastState.Status);
        Assert.Contains("System.InvalidOperationException", lastState.ErrorMessage);
        Assert.Contains("resolver boom", lastState.ErrorMessage);
    }

    [Fact]
    public async Task InstallSelectedLanguageCommitsDownloadedTemporaryFile()
    {
        using var directory = new TemporaryDirectory();
        var bytes = Encoding.UTF8.GetBytes("traineddata");
        using var server = new SingleResponseHttpServer(bytes);
        var resolver = new Mock<IGitHubDownloadUrlResolver>();
        resolver.Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(server.Url);
        var manager = new TesseractDataAssetManager(new FakeStorageProvider(directory.Path), new FakeSettingsService(), resolver.Object);

        await manager.InstallLanguagesAsync(["eng"]);

        var destination = Path.Combine(directory.Path, "eng.traineddata");
        Assert.True(File.Exists(destination));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(destination));
        Assert.False(File.Exists(destination + ".download"));
    }

    private static ITesseractDataAssetManager Create(string path, string languages = "chi_sim+eng")
    {
        return new TesseractDataAssetManager(new FakeStorageProvider(path), new FakeSettingsService(languages), Mock.Of<IGitHubDownloadUrlResolver>());
    }

    private sealed class FakeSettingsService(string languages = "chi_sim+eng") : ISmartBpRecognitionSettingsService
    {
        public SmartBpRecognitionSettings Settings { get; } = new() { TesseractLanguages = languages };

        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeStorageProvider(string tesseractDataRoot) : ISmartBpModuleStorageProvider
    {
        public string ModuleRoot => Directory.GetParent(Directory.GetParent(tesseractDataRoot)!.FullName)!.FullName;
        public string OcrModelsRoot => Directory.GetParent(Directory.GetParent(tesseractDataRoot)!.FullName)!.FullName;
        public string TesseractDataRoot => tesseractDataRoot;
        public string AiRoot => System.IO.Path.Combine(ModuleRoot, "AI");
        public string QwenModelsRoot => System.IO.Path.Combine(AiRoot, "QwenModels");
        public string LlamaCppRoot => System.IO.Path.Combine(AiRoot, "LlamaCpp");
        public string RecognitionLogsRoot => System.IO.Path.Combine(AiRoot, "RecognitionLogs");
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"smartbp-tessdata-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, true);
    }

    private sealed class SingleResponseHttpServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task _serverTask;
        private readonly byte[] _body;
        private readonly CancellationTokenSource _cancellation = new();

        public SingleResponseHttpServer(byte[] body)
        {
            _body = body;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            var endpoint = (IPEndPoint)_listener.LocalEndpoint;
            Url = $"http://127.0.0.1:{endpoint.Port}/eng.traineddata";
            _serverTask = ServeAsync();
        }

        public string Url { get; }

        public void Dispose()
        {
            _cancellation.Cancel();
            _listener.Stop();
            try { _serverTask.Wait(TimeSpan.FromSeconds(2)); }
            catch { }
            _cancellation.Dispose();
        }

        private async Task ServeAsync()
        {
            while (!_cancellation.IsCancellationRequested)
            {
                try
                {
                    using var client = await _listener.AcceptTcpClientAsync(_cancellation.Token);
                    await using var stream = client.GetStream();
                    var buffer = new byte[4096];
                    var read = await stream.ReadAsync(buffer, _cancellation.Token);
                    var request = Encoding.ASCII.GetString(buffer, 0, read);
                    var isHead = request.StartsWith("HEAD ", StringComparison.OrdinalIgnoreCase);
                    var header = Encoding.ASCII.GetBytes(
                        $"HTTP/1.1 200 OK\r\nContent-Length: {_body.Length}\r\nContent-Type: application/octet-stream\r\nAccept-Ranges: bytes\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(header, _cancellation.Token);
                    if (!isHead)
                        await stream.WriteAsync(_body, _cancellation.Token);
                }
                catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
                {
                    return;
                }
                catch (SocketException) when (_cancellation.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }
}
