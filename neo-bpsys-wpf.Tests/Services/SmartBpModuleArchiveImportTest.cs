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
    public async Task ImportArchiveAsync_WhenLoadedModuleMovesToDownloadTarget_StagesRestartAndTracksOldRoot()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var sourceRoot = Path.Combine(_root, "loaded-source");
            var targetRoot = Path.Combine(_root, "new-download-target");
            var initialArchivePath = Path.Combine(_root, "SmartBpModule-initial-move.zip");
            var updateArchivePath = Path.Combine(_root, "SmartBpModule-update-move.7z");
            CreateModuleArchive(initialArchivePath, ArchiveFormat.Zip, "1.0.0");
            CreateModuleArchive(updateArchivePath, ArchiveFormat.SevenZip, "2.0.0");
            var manager = CreateManager();
            Assert.True(await manager.ImportArchiveAsync(initialArchivePath, sourceRoot));
            Assert.True(manager.SetDownloadTargetRootPreference(targetRoot));

            Assert.True(await manager.ImportArchiveAsync(updateArchivePath, targetRoot, "LiteDownload"));

            Assert.True(manager.IsRestartRequiredForPendingModuleImport);
            var pending = JsonSerializer.Deserialize<SmartBpModuleMovePendingState>(
                File.ReadAllText(SmartBpModuleManager.MovePendingFilePath));
            Assert.Equal(Path.GetFullPath(sourceRoot), Path.GetFullPath(pending!.SourceRoot));
            Assert.Equal(Path.GetFullPath(targetRoot), Path.GetFullPath(pending.TargetRoot));
            Assert.True(Directory.Exists(pending.PreparedRoot));
            Assert.False(Directory.Exists(targetRoot));
            Assert.Equal(Path.GetFullPath(sourceRoot), Path.GetFullPath(manager.ModuleRoot));
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
            var cudaDll = Path.Combine(targetRoot, "Runtime", "Paddle", "cuda", "1.0", "package", "native", "dependency.dll");
            var cudaCache = Path.Combine(targetRoot, "Runtime", "Paddle", "Downloads", "NVIDIA", "installer.exe");
            var cudaDependency = Path.Combine(targetRoot, "Runtime", "Paddle", "Dependencies", "cuda11.8-cudnn8.9.6.50", "native", "cudart64_110.dll");
            var cudnnDependency = Path.Combine(targetRoot, "Runtime", "Paddle", "Dependencies", "cuda11.8-cudnn8.9.6.50", "native", "cudnn64_8.dll");
            var partialPackage = Path.Combine(targetRoot, "PaddleRuntimeDownload", "package.nupkg.download.part");
            foreach (var path in new[] { cudaDll, cudaCache, cudaDependency, cudnnDependency, partialPackage })
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, "user-downloaded-content");
            }

            Assert.True(await CreateManager().ImportArchiveAsync(updateArchivePath, targetRoot));

            Assert.Contains("\"ModuleVersion\": \"2.0.0\"", await File.ReadAllTextAsync(Path.Combine(targetRoot, "component.json")));
            Assert.Equal("downloaded-paddle", await File.ReadAllTextAsync(paddleModel));
            foreach (var path in new[] { cudaDll, cudaCache, cudaDependency, cudnnDependency, partialPackage })
                Assert.Equal("user-downloaded-content", await File.ReadAllTextAsync(path));
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
            var cudaDependency = Path.Combine(targetRoot, "Runtime", "Paddle", "Dependencies", "cuda11.8-cudnn8.9.6.50", "native", "cudart64_110.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(cudaDependency)!);
            await File.WriteAllTextAsync(cudaDependency, "downloaded-cuda");
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
            Assert.Equal("downloaded-cuda", await File.ReadAllTextAsync(cudaDependency));
            Assert.False(File.Exists(SmartBpModuleManager.MovePendingFilePath));
        }, TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task PendingArchiveImport_ReplacesPartialTarget_WhenUnrelatedOldFileCannotBeDeleted()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var targetRoot = Path.Combine(_root, "partial-target");
            var preparedRoot = Path.Combine(_root, "prepared-complete-module");
            CopyTestDirectory(CreateTestModuleDirectory("2.0.0", includePackagedAssetDirectories: false), preparedRoot);
            Directory.CreateDirectory(targetRoot);
            await File.WriteAllTextAsync(Path.Combine(targetRoot, "component.json"), "old partial install");
            await File.WriteAllTextAsync(Path.Combine(targetRoot, "Clipper2Lib.dll"), "old partial install");
            var protectedFile = Path.Combine(targetRoot, "AweSun.exe");
            await File.WriteAllTextAsync(protectedFile, "unrelated file");
            File.SetAttributes(protectedFile, FileAttributes.ReadOnly);

            try
            {
                Directory.CreateDirectory(AppConstants.AppDataPath);
                await File.WriteAllTextAsync(
                    SmartBpModuleManager.MovePendingFilePath,
                    JsonSerializer.Serialize(new SmartBpModuleMovePendingState
                    {
                        SourceRoot = targetRoot,
                        TargetRoot = targetRoot,
                        PreparedRoot = preparedRoot,
                        InstallKind = "LiteDownload"
                    }));

                await CreateManager().TryLoadPersistedModuleAsync();
                Assert.True(File.Exists(Path.Combine(targetRoot, SmartBpModuleConstants.EntryAssemblyName)));
                Assert.Contains("\"ModuleVersion\": \"2.0.0\"", await File.ReadAllTextAsync(Path.Combine(targetRoot, "component.json")));
                Assert.True(File.Exists(Path.Combine(targetRoot, "AweSun.exe")) ||
                            Directory.EnumerateDirectories(_root, ".partial-target.backup-*", SearchOption.TopDirectoryOnly)
                                .Any(path => File.Exists(Path.Combine(path, "AweSun.exe"))));
                Assert.False(File.Exists(SmartBpModuleManager.MovePendingFilePath));
            }
            finally
            {
                foreach (var file in Directory.EnumerateFiles(_root, "AweSun.exe", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
            }
        }, TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task PendingPathChange_MigratesManagedModelsAndDeletesOldModuleAfterTargetLoads()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var sourceRoot = CreateTestModuleDirectory("1.0.0", includePackagedAssetDirectories: false);
            var targetRoot = CreateTestModuleDirectory("2.0.0", includePackagedAssetDirectories: false);
            var sourceModel = Path.Combine(sourceRoot, "OCRModels", "RapidOCR", "Models", "profile", "model.onnx");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceModel)!);
            await File.WriteAllTextAsync(sourceModel, "downloaded-model");
            Directory.CreateDirectory(AppConstants.AppDataPath);
            await File.WriteAllTextAsync(
                SmartBpModuleManager.StateFilePath,
                JsonSerializer.Serialize(new SmartBpModuleState { ModuleRoot = targetRoot }));
            await File.WriteAllTextAsync(
                SmartBpModuleManager.MovePendingFilePath,
                JsonSerializer.Serialize(new SmartBpModuleMovePendingState
                {
                    SourceRoot = sourceRoot,
                    TargetRoot = targetRoot,
                    InstallKind = "DownloadDirectoryChange"
                }));

            Assert.True(await CreateManager().TryLoadPersistedModuleAsync());

            Assert.Equal(
                "downloaded-model",
                await File.ReadAllTextAsync(Path.Combine(targetRoot, "OCRModels", "RapidOCR", "Models", "profile", "model.onnx")));
            Assert.False(Directory.Exists(sourceRoot));
            Assert.False(File.Exists(SmartBpModuleManager.MovePendingFilePath));
        }, TimeSpan.FromSeconds(30));
    }

    /// <summary>验证暂存包丢失不阻塞完整目标加载，也不丢弃原目录的用户模型。</summary>
    /// <param name="useSeparateSource">是否从另一个原目录迁移用户模型。</param>
    /// <returns>丢失暂存包后的启动恢复验证任务。</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingPreparedDirectory_DoesNotBlockLoadingExistingTarget(bool useSeparateSource)
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var targetRoot = CreateTestModuleDirectory("1.0.0", includePackagedAssetDirectories: false);
            var sourceRoot = useSeparateSource ? CreateTestModuleDirectory("1.0.0", false) : targetRoot;
            var relativeModel = Path.Combine("OCRModels", "user", "model.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(sourceRoot, relativeModel))!);
            await File.WriteAllTextAsync(Path.Combine(sourceRoot, relativeModel), "keep-user-model");
            var missingPreparedRoot = Path.Combine(
                AppConstants.AppDataPath,
                "SmartBpModulePending",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(AppConstants.AppDataPath);
            await File.WriteAllTextAsync(
                SmartBpModuleManager.StateFilePath,
                JsonSerializer.Serialize(new SmartBpModuleState { ModuleRoot = targetRoot }));
            await File.WriteAllTextAsync(
                SmartBpModuleManager.MovePendingFilePath,
                JsonSerializer.Serialize(new SmartBpModuleMovePendingState
                {
                    SourceRoot = sourceRoot,
                    TargetRoot = targetRoot,
                    PreparedRoot = missingPreparedRoot,
                    InstallKind = "LiteDownload"
                }));

            Assert.True(await CreateManager().TryLoadPersistedModuleAsync());

            Assert.Equal("keep-user-model", await File.ReadAllTextAsync(Path.Combine(targetRoot, relativeModel)));
            Assert.False(File.Exists(SmartBpModuleManager.MovePendingFilePath));
        }, TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void DownloadTargetChange_ReplacesStalePendingMigrationAndKeepsOriginalSource()
    {
        var sourceRoot = CreateTestModuleDirectory("1.0.0", includePackagedAssetDirectories: false);
        var staleTarget = Path.Combine(_root, "stale-target");
        var nextTarget = Path.Combine(_root, "next-target");
        Directory.CreateDirectory(AppConstants.AppDataPath);
        File.WriteAllText(
            SmartBpModuleManager.StateFilePath,
            JsonSerializer.Serialize(new SmartBpModuleState { ModuleRoot = staleTarget }));
        File.WriteAllText(
            SmartBpModuleManager.MovePendingFilePath,
            JsonSerializer.Serialize(new SmartBpModuleMovePendingState
            {
                SourceRoot = sourceRoot,
                TargetRoot = staleTarget,
                LastCleanupError = "previous migration failed"
            }));

        Assert.True(CreateManager().SetDownloadTargetRootPreference(nextTarget));

        var state = JsonSerializer.Deserialize<SmartBpModuleState>(
            File.ReadAllText(SmartBpModuleManager.StateFilePath));
        var pending = JsonSerializer.Deserialize<SmartBpModuleMovePendingState>(
            File.ReadAllText(SmartBpModuleManager.MovePendingFilePath));
        Assert.Equal(Path.GetFullPath(nextTarget), Path.GetFullPath(state!.ModuleRoot));
        Assert.Equal(Path.GetFullPath(sourceRoot), Path.GetFullPath(pending!.SourceRoot));
        Assert.Equal(Path.GetFullPath(nextTarget), Path.GetFullPath(pending.TargetRoot));
        Assert.Null(pending.PreparedRoot);
        Assert.Null(pending.LastCleanupError);
    }

    /// <summary>验证重启安装受阻时保留原模块和暂存包，释放占用后可重试成功。</summary>
    /// <returns>重启安装恢复验证任务。</returns>
    [Fact]
    public async Task PendingArchiveImport_WhenTargetIsLocked_PreservesPendingAndRetriesBeforeLoading()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var targetRoot = CreateTestModuleDirectory("1.0.0", includePackagedAssetDirectories: false);
            var preparedRoot = CreateTestModuleDirectory("2.0.0", includePackagedAssetDirectories: false);
            Directory.CreateDirectory(AppConstants.AppDataPath);
            await File.WriteAllTextAsync(SmartBpModuleManager.StateFilePath,
                JsonSerializer.Serialize(new SmartBpModuleState { ModuleRoot = targetRoot }));
            await File.WriteAllTextAsync(SmartBpModuleManager.MovePendingFilePath,
                JsonSerializer.Serialize(new SmartBpModuleMovePendingState
                {
                    SourceRoot = targetRoot, TargetRoot = targetRoot, PreparedRoot = preparedRoot, InstallKind = "LiteDownload"
                }));
            using var manager = CreateManager();
            using (File.Open(Path.Combine(targetRoot, "component.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.False(await manager.TryLoadPersistedModuleAsync());
                Assert.False(manager.IsModuleLoaded);
                Assert.False(manager.IsRestartRequiredForPendingModuleImport);
                Assert.True(manager.IsModuleRepairRequired);
                Assert.NotEmpty(manager.LastFailureMessage);
                Assert.True(Directory.Exists(preparedRoot));
                Assert.True(File.Exists(SmartBpModuleManager.MovePendingFilePath));
                Assert.Equal("1.0.0", await File.ReadAllTextAsync(Path.Combine(targetRoot, "module-version.txt")));
            }
            Assert.True(await manager.TryLoadPersistedModuleAsync());
            Assert.False(manager.IsRestartRequiredForPendingModuleImport);
            Assert.Equal("2.0.0", await File.ReadAllTextAsync(Path.Combine(targetRoot, "module-version.txt")));
            Assert.False(File.Exists(SmartBpModuleManager.MovePendingFilePath));
        }, TimeSpan.FromSeconds(30));
    }

    /// <summary>验证显式迁移保留完整依赖树，并更新 CUDA 路径后才在下次加载时清理源目录。</summary>
    /// <returns>迁移和重启加载验证任务。</returns>
    [Fact]
    public async Task MigrateModuleRoot_PreservesDependenciesAndRelocatesCudaManifestBeforeSourceCleanup()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var sourceRoot = CreateTestModuleDirectory("1.0.0", includePackagedAssetDirectories: false);
            var targetRoot = Path.Combine(_root, "migrated-module");
            var nativeRelative = Path.Combine("Runtime", "Paddle", "cuda", "1.0", "package", "native");
            var sourceNative = Path.Combine(sourceRoot, nativeRelative);
            Directory.CreateDirectory(sourceNative);
            await File.WriteAllTextAsync(Path.Combine(sourceNative, "dependency.dll"), "cuda-dependency");
            var manifestRelative = Path.Combine("Runtime", "Paddle", "cuda", "1.0", "package", "install.json");
            await File.WriteAllTextAsync(Path.Combine(sourceRoot, manifestRelative),
                JsonSerializer.Serialize(new { NativeDirectory = sourceNative, Verified = true }));
            var ridDependency = Path.Combine(sourceRoot, "runtimes", "win-x64", "native", "dependency.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(ridDependency)!);
            await File.WriteAllTextAsync(ridDependency, "rid-dependency");
            Directory.CreateDirectory(AppConstants.AppDataPath);
            await File.WriteAllTextAsync(SmartBpModuleManager.StateFilePath,
                JsonSerializer.Serialize(new SmartBpModuleState { ModuleRoot = sourceRoot }));
            if (File.Exists(SmartBpModuleManager.MovePendingFilePath)) File.Delete(SmartBpModuleManager.MovePendingFilePath);

            Assert.True(await CreateManager().MigrateModuleRootPreferenceAsync(targetRoot));

            Assert.True(Directory.Exists(sourceRoot));
            Assert.Equal("cuda-dependency", await File.ReadAllTextAsync(Path.Combine(targetRoot, nativeRelative, "dependency.dll")));
            Assert.Equal("rid-dependency", await File.ReadAllTextAsync(Path.Combine(targetRoot, "runtimes", "win-x64", "native", "dependency.dll")));
            using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(targetRoot, manifestRelative)));
            Assert.Equal(Path.Combine(targetRoot, nativeRelative), manifest.RootElement.GetProperty("NativeDirectory").GetString());
            Assert.True(await CreateManager().TryLoadPersistedModuleAsync());
            Assert.False(Directory.Exists(sourceRoot));
            Assert.False(File.Exists(SmartBpModuleManager.MovePendingFilePath));
        }, TimeSpan.FromSeconds(30));
    }

    /// <summary>验证缺失、截断、等长损坏和坏清单均在加载前被拒绝，并可保留模型原地修复。</summary>
    /// <param name="damage">文件损坏方式。</param>
    /// <returns>修复验证任务。</returns>
    [Theory]
    [InlineData("missing")]
    [InlineData("truncated")]
    [InlineData("same-size-corruption")]
    [InlineData("invalid-manifest")]
    [InlineData("oversized-manifest")]
    public async Task DamagedInstallation_IsRejectedBeforeLoadingAndCanBeRepairedWithoutRestart(string damage)
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var targetRoot = CreateTestModuleDirectory("1.0.0", false);
            var versionFile = Path.Combine(targetRoot, "module-version.txt");
            switch (damage)
            {
                case "missing": File.Delete(versionFile); break;
                case "truncated": await File.WriteAllTextAsync(versionFile, "1"); break;
                case "same-size-corruption": await File.WriteAllTextAsync(versionFile, "9.9.9"); break;
                case "invalid-manifest": await File.WriteAllTextAsync(Path.Combine(targetRoot, "component.json"), "{"); break;
                case "oversized-manifest":
                    using (var stream = File.OpenWrite(Path.Combine(targetRoot, "component.json")))
                        stream.SetLength(SmartBpModuleConstants.MaxManifestBytes + 1L);
                    break;
            }
            var modelFile = Path.Combine(targetRoot, "OCRModels", "user", "model.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(modelFile)!);
            await File.WriteAllTextAsync(modelFile, "user-model");
            var manager = CreateManager();
            Assert.False(await manager.LoadModuleFromDirectoryAsync(targetRoot));
            Assert.True(manager.IsModuleRepairRequired);
            Assert.NotEmpty(manager.LastFailureMessage);

            var archive = Path.Combine(_root, "repair.zip");
            CreateModuleArchive(archive, ArchiveFormat.Zip);
            Assert.True(await manager.ImportArchiveAsync(archive, targetRoot));
            Assert.False(manager.IsRestartRequiredForPendingModuleImport);
            Assert.False(manager.IsModuleRepairRequired);
            Assert.Equal("1.0.0", manager.LoadedModuleVersion);
            Assert.Equal("1.0.0", await File.ReadAllTextAsync(versionFile));
            Assert.Equal("user-model", await File.ReadAllTextAsync(modelFile));
        }, TimeSpan.FromSeconds(30));
    }

    /// <summary>验证新旧迁移标记均能从完整来源恢复模块和截断模型，成功后再清理来源。</summary>
    /// <param name="installKind">迁移标记的来源标签。</param>
    /// <param name="damageStaticFile">是否同时损坏模块发布文件。</param>
    /// <returns>启动恢复验证任务。</returns>
    [Theory]
    [InlineData(null, true)]
    [InlineData("PathMigrationPending", true)]
    [InlineData("PathMigrationPending", false)]
    public async Task InterruptedMigration_RestoresStaticFilesAndTruncatedModelsFromSource(string? installKind, bool damageStaticFile)
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var sourceRoot = CreateTestModuleDirectory("1.0.0", false);
            var targetRoot = Path.Combine(_root, "interrupted-target");
            CopyTestDirectory(sourceRoot, targetRoot);
            if (damageStaticFile) await File.WriteAllTextAsync(Path.Combine(targetRoot, "module-version.txt"), "9.9.9");
            var relativeModel = Path.Combine("OCRModels", "user", "model.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(sourceRoot, relativeModel))!);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(targetRoot, relativeModel))!);
            await File.WriteAllTextAsync(Path.Combine(sourceRoot, relativeModel), "complete-model");
            await File.WriteAllTextAsync(Path.Combine(targetRoot, relativeModel), "broken");
            Directory.CreateDirectory(AppConstants.AppDataPath);
            await File.WriteAllTextAsync(SmartBpModuleManager.StateFilePath,
                JsonSerializer.Serialize(new SmartBpModuleState { ModuleRoot = sourceRoot }));
            await File.WriteAllTextAsync(SmartBpModuleManager.MovePendingFilePath,
                JsonSerializer.Serialize(new SmartBpModuleMovePendingState
                {
                    SourceRoot = sourceRoot, TargetRoot = targetRoot, InstallKind = installKind
                }));

            var manager = CreateManager();
            Assert.True(await manager.TryLoadPersistedModuleAsync());
            Assert.Equal("1.0.0", await File.ReadAllTextAsync(Path.Combine(targetRoot, "module-version.txt")));
            Assert.Equal("complete-model", await File.ReadAllTextAsync(Path.Combine(targetRoot, relativeModel)));
            Assert.False(Directory.Exists(sourceRoot));
            Assert.False(File.Exists(SmartBpModuleManager.MovePendingFilePath));
            Assert.Equal(targetRoot, manager.ReadState()!.ModuleRoot);
        }, TimeSpan.FromSeconds(30));
    }

    /// <summary>验证双方残缺时不循环要求重启，仍可通过导入包修复并保留已有模型。</summary>
    /// <returns>无完整来源的修复验证任务。</returns>
    [Fact]
    public async Task InterruptedMigration_WithoutCompleteSourceAllowsArchiveRepairAndRetainsModels()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var sourceRoot = CreateTestModuleDirectory("1.0.0", false);
            var targetRoot = Path.Combine(_root, "broken-target");
            CopyTestDirectory(sourceRoot, targetRoot);
            File.Delete(Path.Combine(sourceRoot, "module-version.txt"));
            File.Delete(Path.Combine(targetRoot, "module-version.txt"));
            var sourceModel = Path.Combine(sourceRoot, "OCRModels", "user", "model.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceModel)!);
            await File.WriteAllTextAsync(sourceModel, "keep-model");
            Directory.CreateDirectory(AppConstants.AppDataPath);
            await File.WriteAllTextAsync(SmartBpModuleManager.MovePendingFilePath,
                JsonSerializer.Serialize(new SmartBpModuleMovePendingState { SourceRoot = sourceRoot, TargetRoot = targetRoot }));
            var manager = CreateManager();
            Assert.False(await manager.TryLoadPersistedModuleAsync());
            Assert.True(manager.IsModuleRepairRequired);
            Assert.False(manager.IsRestartRequiredForPendingModuleImport);
            Assert.True(Directory.Exists(sourceRoot));

            var archive = Path.Combine(_root, "repair.zip");
            CreateModuleArchive(archive, ArchiveFormat.Zip);
            Assert.True(await manager.ImportArchiveAsync(archive, targetRoot));
            Assert.Equal("keep-model", await File.ReadAllTextAsync(Path.Combine(targetRoot, "OCRModels", "user", "model.bin")));
            Assert.False(File.Exists(SmartBpModuleManager.MovePendingFilePath));
        }, TimeSpan.FromSeconds(30));
    }

    /// <summary>验证旧包缺少关键组件时被拒绝，且旧清单不被自动改写。</summary>
    [Fact]
    public void LegacyModuleWithoutInventory_UsesProbesAndDoesNotRewriteManifest()
    {
        var moduleRoot = CreateTestModuleDirectory("1.0.0", false);
        var manifestPath = Path.Combine(moduleRoot, "component.json");
        var manifest = JsonSerializer.Deserialize<SmartBpModuleManifest>(File.ReadAllText(manifestPath))!;
        manifest.Integrity = null;
        var json = JsonSerializer.Serialize(manifest);
        File.WriteAllText(manifestPath, json);
        // 旧格式的入口和清单都在，但缺少原生 CPU 运行库和资源时不能误判为完整。
        Assert.False(CreateManager().ValidateModuleDirectory(moduleRoot, false, out _, out var error));
        Assert.NotEmpty(error);
        Assert.Equal(json, File.ReadAllText(manifestPath));
    }

    /// <summary>验证不完整的归档不能替换现有安装或覆盖用户模型。</summary>
    /// <returns>安装前完整性校验任务。</returns>
    [Fact]
    public async Task IncompleteArchive_DoesNotReplaceExistingModule()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var targetRoot = CreateTestModuleDirectory("1.0.0", false);
            var candidateRoot = CreateTestModuleDirectory("2.0.0", false);
            File.Delete(Path.Combine(candidateRoot, "module-version.txt"));
            var archive = Path.Combine(_root, "incomplete.zip");
            System.IO.Compression.ZipFile.CreateFromDirectory(candidateRoot, archive);

            var manager = CreateManager();
            Assert.False(await manager.ImportArchiveAsync(archive, targetRoot));
            Assert.False(manager.IsRestartRequiredForPendingModuleImport);
            Assert.Equal("1.0.0", await File.ReadAllTextAsync(Path.Combine(targetRoot, "module-version.txt")));
            Assert.True(manager.ValidateModuleDirectory(targetRoot, false, out _, out _));
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
            // 同名归档内容不能覆盖用户已有的 CUDA/Paddle 运行库或下载缓存。
            foreach (var relativePath in new[]
            {
                "Runtime/Paddle/cuda/1.0/package/native/dependency.dll",
                "Runtime/Paddle/Dependencies/cuda11.8-cudnn8.9.6.50/native/cudart64_110.dll",
                "Runtime/Paddle/Dependencies/cuda11.8-cudnn8.9.6.50/native/cudnn64_8.dll",
                "Runtime/Paddle/Downloads/NVIDIA/installer.exe",
                "PaddleRuntimeDownload/package.nupkg.download.part"
            })
            {
                var packagedAsset = Path.Combine(moduleRoot, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(packagedAsset)!);
                File.WriteAllText(packagedAsset, "packaged-runtime");
            }
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
        WriteTestIntegrity(moduleRoot);
        return moduleRoot;
    }

    private static void WriteTestIntegrity(string moduleRoot)
    {
        var manifestPath = Path.Combine(moduleRoot, "component.json");
        var manifest = JsonSerializer.Deserialize<SmartBpModuleManifest>(File.ReadAllText(manifestPath))!;
        manifest.Integrity = new SmartBpModuleIntegrity
        {
            Version = 1,
            Files = Directory.EnumerateFiles(moduleRoot, "*", SearchOption.AllDirectories)
                .Where(file => file != manifestPath && !new[] { "OCRModels/", "Runtime/Paddle/cuda/", "Runtime/Paddle/Dependencies/", "Runtime/Paddle/Downloads/", "PaddleRuntimeDownload/" }
                    .Any(root => Path.GetRelativePath(moduleRoot, file).Replace('\\', '/').StartsWith(root, StringComparison.OrdinalIgnoreCase)))
                .Select(file => new SmartBpModuleFile
                {
                    Path = Path.GetRelativePath(moduleRoot, file).Replace('\\', '/'),
                    Size = new FileInfo(file).Length,
                    Sha256 = SmartBpModuleManager.ComputeSha256(file)
                }).ToList()
        };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
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
