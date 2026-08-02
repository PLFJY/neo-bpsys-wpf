using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.Archives;
using neo_bpsys_wpf.Core.Models.SmartBpModule;
using neo_bpsys_wpf.Core.Services.Archives;
using neo_bpsys_wpf.Core.Services;
using neo_bpsys_wpf.Services.SmartBpModule;
using neo_bpsys_wpf.Tests.Infrastructure;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class SmartBpModuleArchiveImportTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "neo-bpsys-wpf-tests", Guid.NewGuid().ToString("N"));
    private readonly string? _stateBackup;
    private readonly string? _movePendingBackup;
    private readonly bool _hadState;
    private readonly bool _hadMovePending;

    public SmartBpModuleArchiveImportTest()
    {
        Directory.CreateDirectory(_root);
        _hadState = File.Exists(SmartBpModuleManager.StateFilePath);
        if (_hadState)
        {
            _stateBackup = File.ReadAllText(SmartBpModuleManager.StateFilePath);
        }

        _hadMovePending = File.Exists(SmartBpModuleManager.MovePendingFilePath);
        if (_hadMovePending)
        {
            _movePendingBackup = File.ReadAllText(SmartBpModuleManager.MovePendingFilePath);
        }
    }

    [Fact]
    public async Task ImportArchiveAsync_AcceptsZipModuleArchive()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var archivePath = Path.Combine(_root, "SmartBpModule.zip");
            CreateModuleArchive(archivePath, ArchiveFormat.Zip);
            var targetRoot = Path.Combine(_root, "installed-zip");

            var imported = await CreateManager().ImportArchiveAsync(archivePath, targetRoot);

            Assert.True(imported);
            Assert.True(File.Exists(Path.Combine(targetRoot, "component.json")));
        }, TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task ImportArchiveAsync_AcceptsSevenZipModuleArchive()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var archivePath = Path.Combine(_root, "SmartBpModule.7z");
            CreateModuleArchive(archivePath, ArchiveFormat.SevenZip);
            var targetRoot = Path.Combine(_root, "installed-7z");

            var imported = await CreateManager().ImportArchiveAsync(archivePath, targetRoot);

            Assert.True(imported);
            Assert.True(File.Exists(Path.Combine(targetRoot, "component.json")));
        }, TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task ImportArchiveAsync_WhenCurrentTargetIsLoaded_StagesImportForRestart()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var targetRoot = Path.Combine(_root, "loaded-target");
            var initialArchivePath = Path.Combine(_root, "SmartBpModule-initial.zip");
            var updateArchivePath = Path.Combine(_root, "SmartBpModule-update.7z");
            CreateModuleArchive(initialArchivePath, ArchiveFormat.Zip);
            CreateModuleArchive(updateArchivePath, ArchiveFormat.SevenZip);
            var manager = CreateManager();
            Assert.True(await manager.ImportArchiveAsync(initialArchivePath, targetRoot));

            var imported = await manager.ImportArchiveAsync(updateArchivePath, targetRoot, "SettingsArchiveImport");

            Assert.True(imported);
            Assert.True(manager.IsRestartRequiredForPendingModuleImport);
            var pending = JsonSerializer.Deserialize<SmartBpModuleMovePendingState>(
                File.ReadAllText(SmartBpModuleManager.MovePendingFilePath));
            Assert.NotNull(pending);
            Assert.Equal(Path.GetFullPath(targetRoot), Path.GetFullPath(pending!.TargetRoot));
            Assert.True(Directory.Exists(pending.PreparedRoot));
            Assert.True(File.Exists(Path.Combine(targetRoot, "component.json")));
        }, TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task ImportArchiveAsync_PreservesDownloadedModelDirectories_WhenReplacingExistingTarget()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var updateArchivePath = Path.Combine(_root, "SmartBpModule-update.7z");
            var targetRoot = Path.Combine(_root, "installed-with-models");
            CreateModuleArchive(updateArchivePath, ArchiveFormat.SevenZip, "2.0.0", includePackagedAssetDirectories: true);
            CopyTestDirectory(CreateTestModuleDirectory("1.0.0", includePackagedAssetDirectories: false), targetRoot);
            var paddleModel = Path.Combine(targetRoot, "OCRModels", "zh-cn-v5-mobile", "det", "inference.pdiparams");
            Directory.CreateDirectory(Path.GetDirectoryName(paddleModel)!);
            await File.WriteAllTextAsync(paddleModel, "downloaded-paddle");

            Assert.True(await CreateManager().ImportArchiveAsync(updateArchivePath, targetRoot));

            Assert.Contains("\"ModuleVersion\": \"2.0.0\"", await File.ReadAllTextAsync(Path.Combine(targetRoot, "component.json")));
            Assert.Equal("downloaded-paddle", await File.ReadAllTextAsync(paddleModel));
        }, TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task PendingArchiveImport_PreservesDownloadedModelDirectories_WhenCompletingAfterRestart()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var targetRoot = Path.Combine(_root, "pending-with-models");
            var preparedRoot = Path.Combine(_root, "prepared-update");
            CopyTestDirectory(CreateTestModuleDirectory("1.0.0", includePackagedAssetDirectories: false), targetRoot);
            CopyTestDirectory(CreateTestModuleDirectory("2.0.0", includePackagedAssetDirectories: true), preparedRoot);
            var tesseractData = Path.Combine(targetRoot, "OCRModels", "Tesseract", "tessdata", "chi_sim.traineddata");
            Directory.CreateDirectory(Path.GetDirectoryName(tesseractData)!);
            await File.WriteAllTextAsync(tesseractData, "downloaded-tesseract");
            Directory.CreateDirectory(AppConstants.AppDataPath);
            await File.WriteAllTextAsync(
                SmartBpModuleManager.StateFilePath,
                JsonSerializer.Serialize(
                    new SmartBpModuleState { ModuleRoot = targetRoot, InstallKind = "LocalDirectory" },
                    new JsonSerializerOptions { WriteIndented = true }));
            await File.WriteAllTextAsync(
                SmartBpModuleManager.MovePendingFilePath,
                JsonSerializer.Serialize(
                    new SmartBpModuleMovePendingState
                    {
                        SourceRoot = targetRoot,
                        TargetRoot = targetRoot,
                        PreparedRoot = preparedRoot,
                        InstallKind = "SettingsArchiveImport",
                        CreatedAt = DateTimeOffset.UtcNow
                    },
                    new JsonSerializerOptions { WriteIndented = true }));

            Assert.True(await CreateManager().TryLoadPersistedModuleAsync());

            Assert.Contains("\"ModuleVersion\": \"2.0.0\"", await File.ReadAllTextAsync(Path.Combine(targetRoot, "component.json")));
            Assert.Equal("downloaded-tesseract", await File.ReadAllTextAsync(tesseractData));
            Assert.False(File.Exists(SmartBpModuleManager.MovePendingFilePath));
        }, TimeSpan.FromSeconds(30));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        Directory.CreateDirectory(AppConstants.AppDataPath);
        if (_hadState && _stateBackup != null)
        {
            File.WriteAllText(SmartBpModuleManager.StateFilePath, _stateBackup);
        }
        else if (File.Exists(SmartBpModuleManager.StateFilePath))
        {
            File.Delete(SmartBpModuleManager.StateFilePath);
        }

        DeleteCurrentPreparedRoot();
        if (_hadMovePending && _movePendingBackup != null)
        {
            File.WriteAllText(SmartBpModuleManager.MovePendingFilePath, _movePendingBackup);
        }
        else if (File.Exists(SmartBpModuleManager.MovePendingFilePath))
        {
            File.Delete(SmartBpModuleManager.MovePendingFilePath);
        }
    }

    private static void DeleteCurrentPreparedRoot()
    {
        if (!File.Exists(SmartBpModuleManager.MovePendingFilePath))
        {
            return;
        }

        try
        {
            var pending = JsonSerializer.Deserialize<SmartBpModuleMovePendingState>(
                File.ReadAllText(SmartBpModuleManager.MovePendingFilePath));
            if (!string.IsNullOrWhiteSpace(pending?.PreparedRoot) &&
                Directory.Exists(pending.PreparedRoot))
            {
                Directory.Delete(pending.PreparedRoot, recursive: true);
            }
        }
        catch (Exception)
        {
        }
    }

    private SmartBpModuleManager CreateManager()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(Mock.Of<ISharedDataService>());
        services.AddSingleton(Mock.Of<IWindowCaptureService>());
        services.AddSingleton(Mock.Of<IFilePickerService>());
        services.AddSingleton(Mock.Of<ISettingsHostService>(service => service.Settings == new Settings()));
        services.AddSingleton(Mock.Of<ISmartBpOcrModelPathProvider>(provider => provider.RootDirectory == _root));
        var provider = services.BuildServiceProvider();

        return new SmartBpModuleManager(
            provider,
            NullLogger<SmartBpModuleManager>.Instance,
            provider.GetRequiredService<ISettingsHostService>(),
            new SevenZipArchiveService(),
            new FileDownloadService(() => new HttpClient()));
    }

    private void CreateModuleArchive(
        string archivePath,
        ArchiveFormat format,
        string moduleVersion = "1.0.0",
        bool includePackagedAssetDirectories = false)
    {
        var moduleRoot = CreateTestModuleDirectory(moduleVersion, includePackagedAssetDirectories);
        var files = Directory.EnumerateFiles(moduleRoot, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.NotEmpty(files);

        if (format == ArchiveFormat.Zip)
        {
            System.IO.Compression.ZipFile.CreateFromDirectory(moduleRoot, archivePath);
            return;
        }

        var sevenZipExe = Path.Combine(AppContext.BaseDirectory, "Tools", "7Zip", "7z.exe");
        if (!File.Exists(sevenZipExe))
            throw new FileNotFoundException("Test requires 7z.exe at: " + sevenZipExe, sevenZipExe);

        var psi = new ProcessStartInfo(sevenZipExe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = moduleRoot
        };
        psi.ArgumentList.Add("a");
        psi.ArgumentList.Add("-t7z");
        psi.ArgumentList.Add(archivePath);
        psi.ArgumentList.Add(".");
        psi.ArgumentList.Add("-y");
        var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start 7z.exe");
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"7z.exe packing failed with exit code {process.ExitCode}");
    }

    private string CreateTestModuleDirectory(string moduleVersion, bool includePackagedAssetDirectories)
    {
        var moduleRoot = Path.Combine(_root, "module-source", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(moduleRoot);
        var testAssemblyPath = typeof(TestSmartBpModuleEntryPoint).Assembly.Location;
        File.Copy(
            testAssemblyPath,
            Path.Combine(moduleRoot, SmartBpModuleConstants.EntryAssemblyName),
            overwrite: true);
        File.WriteAllText(Path.Combine(moduleRoot, "module-version.txt"), moduleVersion);
        if (includePackagedAssetDirectories)
        {
            var packagedOcrAsset = Path.Combine(moduleRoot, "OCRModels", "packaged", "model.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(packagedOcrAsset)!);
            File.WriteAllText(packagedOcrAsset, "packaged-ocr");
        }

        File.WriteAllText(
            Path.Combine(moduleRoot, "component.json"),
            $$"""
            {
              "ComponentId": "SmartBpModule",
              "ModuleVersion": "{{moduleVersion}}",
              "RuntimeAbiVersion": 1,
              "Rid": "win-x64"
            }
            """);
        return moduleRoot;
    }

    private static void CopyTestDirectory(string source, string target)
    {
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }
}

public sealed class TestSmartBpModuleEntryPoint : ISmartBpModuleEntryPoint
{
    public object CreateSmartBpContent(IServiceProvider hostServices)
    {
        return new object();
    }

    public IReadOnlyList<SmartBpFeatureCommand> GetFeatureCommands() => [];

    public ISmartBpPostGameRecognitionProgressSource? GetPostGameRecognitionProgressSource() => null;
}
