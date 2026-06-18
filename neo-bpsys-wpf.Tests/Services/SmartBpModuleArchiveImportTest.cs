using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using neo_bpsys_wpf.Services.SmartBpModule;
using neo_bpsys_wpf.Tests.Infrastructure;
using SharpCompress.Common;
using SharpCompress.Writers;
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
            new SharpCompressArchiveService());
    }

    private void CreateModuleArchive(string archivePath, ArchiveFormat format)
    {
        var moduleRoot = CreateTestModuleDirectory();
        var files = Directory.EnumerateFiles(moduleRoot, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.NotEmpty(files);

        if (format == ArchiveFormat.Zip)
        {
            System.IO.Compression.ZipFile.CreateFromDirectory(moduleRoot, archivePath);
            return;
        }

        using var output = File.Create(archivePath);
        using var writer = WriterFactory.OpenWriter(
            output,
            ArchiveType.SevenZip,
            new WriterOptions(CompressionType.LZMA));
        foreach (var file in files)
        {
            var entryName = Path.GetRelativePath(moduleRoot, file).Replace('\\', '/');
            writer.Write(entryName, file);
        }
    }

    private string CreateTestModuleDirectory()
    {
        var moduleRoot = Path.Combine(_root, "module-source", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(moduleRoot);
        var testAssemblyPath = typeof(TestSmartBpModuleEntryPoint).Assembly.Location;
        File.Copy(
            testAssemblyPath,
            Path.Combine(moduleRoot, SmartBpModuleConstants.EntryAssemblyName),
            overwrite: true);
        File.WriteAllText(
            Path.Combine(moduleRoot, "component.json"),
            """
            {
              "ComponentId": "SmartBpModule",
              "ModuleVersion": "1.0.0",
              "RuntimeAbiVersion": 1,
              "Rid": "win-x64"
            }
            """);
        return moduleRoot;
    }
}

public sealed class TestSmartBpModuleEntryPoint : ISmartBpModuleEntryPoint
{
    public object CreateSmartBpContent(IServiceProvider hostServices)
    {
        return new object();
    }

    public IReadOnlyList<SmartBpFeatureCommand> GetFeatureCommands() => [];
}
