#nullable enable

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Services.Registry;
using neo_bpsys_wpf.ExamplePlugin;
using neo_bpsys_wpf.Models.Plugins;
using neo_bpsys_wpf.Services.Abstractions;
using neo_bpsys_wpf.Tests.Infrastructure;
using neo_bpsys_wpf.ViewModels.Pages;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class FrontedLayoutPluginDependencyPackageTest
{
    [Fact]
    public void DebugCsprojIncludesExamplePluginOnlyForDebug()
    {
        var text = File.ReadAllText(GetRepositoryPath("neo-bpsys-wpf", "neo-bpsys-wpf.csproj"));

        Assert.Contains("Condition=\"'$(Configuration)'=='Debug'\"", text);
        Assert.Contains("neo-bpsys-wpf.ExamplePlugin.csproj", text);
        Assert.Contains("<FolderName>plfjy.ExamplePlugin</FolderName>", text);
    }

    [Fact]
    public void ExamplePluginManifestAndContributorRegisterTeamCard()
    {
        var manifest = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf.ExamplePlugin",
            "manifest.yml"));
        Assert.Contains("id: plfjy.ExamplePlugin", manifest);

        var registry = CreateRegistryWithExamplePlugin();
        var descriptor = registry.GetPluginDescriptor(TeamCardFrontedControlContributor.FullControlType);

        Assert.NotNull(descriptor);
        Assert.Equal("plugin:plfjy.ExamplePlugin/TeamCard", descriptor.FullControlType);
        Assert.Equal(typeof(TeamCardFrontedControlConfig), descriptor.ConfigType);
        Assert.Contains(descriptor.Properties ?? [], property =>
            property.PropertyName == nameof(TeamCardFrontedControlConfig.TeamNameBindingPath)
            && property.BindingTargetKind == FrontedBindingTargetKind.Text);
        Assert.Contains(descriptor.Properties ?? [], property =>
            property.PropertyName == nameof(TeamCardFrontedControlConfig.BackgroundColor)
            && property.EditorKind == FrontedPropertyEditorKind.Color);
        Assert.Contains(descriptor.Properties ?? [], property =>
            property.PropertyName == nameof(TeamCardFrontedControlConfig.FontSize)
            && property.EditorKind == FrontedPropertyEditorKind.Number);
        Assert.Contains(descriptor.Properties ?? [], property =>
            property.PropertyName == nameof(TeamCardFrontedControlConfig.FontWeight)
            && property.EditorKind == FrontedPropertyEditorKind.Enum);

        var factory = new FrontedControlDefaultConfigFactory(registry);
        var created = factory.Create(
            TeamCardFrontedControlContributor.FullControlType,
            new FrontedCanvasDesignDocument
            {
                CanvasConfig = new FrontedCanvasConfig { CanvasWidth = 400, CanvasHeight = 300 }
            });

        var config = Assert.IsType<TeamCardFrontedControlConfig>(created);
    }

    [Fact]
    public void ExampleTeamCardDefaultBindingPathsExistInBindingCatalog()
    {
        var config = new TeamCardFrontedControlConfig();
        var provider = new FrontedBindingBrowserProvider();
        var allPaths = provider.BuildTree()
            .SelectMany(node => node.Flatten())
            .Select(node => node.FullPath)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(config.TeamNameBindingPath, allPaths);
        Assert.Contains(config.LogoBindingPath, allPaths);
    }

    [Fact]
    public void DesignConverterWritesRequiredPluginMinVersionFromInstalledPluginManifest()
    {
        var registry = CreateRegistryWithExamplePlugin();
        var converter = new FrontedLayoutDesignConverter(
            registry,
            new FakePluginMetadataProvider(("plfjy.ExamplePlugin", "1.0.0.0", "ExamplePlugin")));
        var document = new FrontedCanvasDesignDocument
        {
            WindowTypeName = "BpWindow",
            CanvasName = "BaseCanvas",
            CanvasConfig = new FrontedCanvasConfig
            {
                RequiredPlugins =
                [
                    new FrontedPluginDependency
                    {
                        PackageId = "plfjy.ExamplePlugin",
                        MinVersion = "0.9.0"
                    }
                ]
            },
            Controls =
            [
                new FrontedControlDesignItem
                {
                    Name = "TeamCard1",
                    Config = new TeamCardFrontedControlConfig()
                }
            ]
        };

        var config = converter.ToConfig(document);

        var dependency = Assert.Single(config.RequiredPlugins);
        Assert.Equal("1.0.0.0", dependency.MinVersion);
        Assert.Equal("ExamplePlugin", dependency.DisplayName);
    }

    [Fact]
    public void ExampleTeamCardRuntimeControlCanBeCreated()
    {
        RunOnStaThread(() =>
        {
            var registry = CreateRegistryWithExamplePlugin();
            var control = registry.GetControl(TeamCardFrontedControlContributor.FullControlType);
            Assert.NotNull(control);

            var element = control.Create(
                "TeamCard1",
                new TeamCardFrontedControlConfig(),
                new FrontedControlBuildContext
                {
                    Services = new ServiceCollection().BuildServiceProvider(),
                    SharedDataService = new Mock<ISharedDataService>().Object,
                    ResourceResolver = new Mock<IFrontedResourceResolver>().Object,
                    WindowId = "TestWindow",
                    CanvasName = "BaseCanvas",
                    Logger = NullLogger.Instance
                });

            Assert.IsType<Border>(element);
            Assert.Equal("TeamCard1", element.Name);
        });
    }

    [Fact]
    public async Task ExportScansPluginControlsWritesDependenciesAndDoesNotIncludePluginBinaries()
    {
        var root = CreateTempDirectory();
        try
        {
            var builtInRoot = Path.Combine(root, "builtIn");
            var packageRoot = Path.Combine(root, "packages");
            var tempRoot = Path.Combine(root, "temp");
            var outputPath = Path.Combine(root, "package.bpui");
            WriteAllCatalogLayouts(builtInRoot, includePluginOnFirstLayout: true);

            var exporter = new FrontedLayoutPackageExporter(
                new FrontedLayoutPackageManager(
                    packageRoot,
                    builtInRoot,
                    logger: NullLogger<FrontedLayoutPackageManager>.Instance),
                packageRoot,
                tempRoot,
                controlRegistry: CreateRegistryWithExamplePlugin());

            var result = await exporter.ExportAsync(new FrontedLayoutPackageExportRequest
            {
                PackageId = "package-with-plugin",
                Name = "Package With Plugin",
                OutputPath = outputPath
            }, TestContext.Current.CancellationToken);

            Assert.True(result.Success, result.ErrorMessage);
            using var archive = ZipFile.OpenRead(outputPath);
            Assert.DoesNotContain(archive.Entries, entry =>
                entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                || entry.FullName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

            var manifest = ReadManifest(archive);
            var dependency = Assert.Single(manifest.PluginDependencies);
            Assert.Equal("plfjy.ExamplePlugin", dependency.PackageId);
            Assert.Null(dependency.MinVersion);
            Assert.Contains("plugin:plfjy.ExamplePlugin/TeamCard", dependency.Controls);
            Assert.Contains("ScoreSurWindow", dependency.RequiredBy);

            var layout = JsonSerializer.Deserialize<FrontedWindowConfig>(
                ReadZipEntry(archive, "FrontedLayouts/ScoreSurWindow.json"))!.ToCanvasConfig();
            var canvasDependency = Assert.Single(layout.RequiredPlugins);
            Assert.Equal("plfjy.ExamplePlugin", canvasDependency.PackageId);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task ExportWritesPluginDependencyMinVersionFromInstalledPluginManifest()
    {
        var root = CreateTempDirectory();
        try
        {
            var builtInRoot = Path.Combine(root, "builtIn");
            var outputPath = Path.Combine(root, "package.bpui");
            WriteAllCatalogLayouts(builtInRoot, includePluginOnFirstLayout: true);

            var exporter = new FrontedLayoutPackageExporter(
                new FrontedLayoutPackageManager(
                    Path.Combine(root, "packages"),
                    builtInRoot,
                    logger: NullLogger<FrontedLayoutPackageManager>.Instance),
                Path.Combine(root, "packages"),
                Path.Combine(root, "temp"),
                controlRegistry: CreateRegistryWithExamplePlugin(),
                pluginMetadataProvider: new FakePluginMetadataProvider(("plfjy.ExamplePlugin", "1.0.0.0", "ExamplePlugin")));

            var result = await exporter.ExportAsync(new FrontedLayoutPackageExportRequest
            {
                PackageId = "package-with-plugin-version",
                Name = "Package With Plugin Version",
                OutputPath = outputPath
            }, TestContext.Current.CancellationToken);

            Assert.True(result.Success, result.ErrorMessage);
            using var archive = ZipFile.OpenRead(outputPath);
            var manifestDependency = Assert.Single(ReadManifest(archive).PluginDependencies);
            Assert.Equal("1.0.0.0", manifestDependency.MinVersion);

            var layout = JsonSerializer.Deserialize<FrontedWindowConfig>(
                ReadZipEntry(archive, "FrontedLayouts/ScoreSurWindow.json"))!.ToCanvasConfig();
            Assert.Equal("1.0.0.0", Assert.Single(layout.RequiredPlugins).MinVersion);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task ImportInstalledPluginLowerThanMinVersionRequiresAction()
    {
        var root = CreateTempDirectory();
        try
        {
            var archivePath = Path.Combine(root, "needs-update.bpui");
            CreatePluginBpuiArchive(archivePath, includeActualPluginControl: true, minVersion: "1.2.0");
            var importer = new FrontedLayoutPackageImporter(
                Path.Combine(root, "packages"),
                Path.Combine(root, "temp"),
                controlRegistry: CreateRegistryWithExamplePlugin(),
                pluginMetadataProvider: new FakePluginMetadataProvider(("plfjy.ExamplePlugin", "1.0.0", "ExamplePlugin")));

            var result = await importer.ImportAsync(new FrontedLayoutPackageImportRequest
            {
                PackagePath = archivePath
            }, TestContext.Current.CancellationToken);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Empty(result.MissingPluginControls);
            var dependency = Assert.Single(result.UnsatisfiedPluginDependencies);
            Assert.True(dependency.IsInstalled);
            Assert.False(dependency.IsVersionSatisfied);
            Assert.Equal("1.2.0", dependency.MinVersion);
            Assert.Equal("1.0.0", dependency.InstalledVersion);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task ImportInstalledPluginSatisfyingMinVersionPasses()
    {
        var root = CreateTempDirectory();
        try
        {
            var archivePath = Path.Combine(root, "satisfied.bpui");
            CreatePluginBpuiArchive(archivePath, includeActualPluginControl: true, minVersion: "1.0.0");
            var importer = new FrontedLayoutPackageImporter(
                Path.Combine(root, "packages"),
                Path.Combine(root, "temp"),
                controlRegistry: CreateRegistryWithExamplePlugin(),
                pluginMetadataProvider: new FakePluginMetadataProvider(("plfjy.ExamplePlugin", "1.0.0.0", "ExamplePlugin")));

            var result = await importer.ImportAsync(new FrontedLayoutPackageImportRequest
            {
                PackagePath = archivePath
            }, TestContext.Current.CancellationToken);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Empty(result.UnsatisfiedPluginDependencies);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task ImportMissingPluginSucceedsAndPreservesPluginControls()
    {
        var root = CreateTempDirectory();
        try
        {
            var archivePath = Path.Combine(root, "missing-plugin.bpui");
            CreatePluginBpuiArchive(archivePath, includeActualPluginControl: true);
            var packageRoot = Path.Combine(root, "packages");
            var importer = new FrontedLayoutPackageImporter(
                packageRoot,
                Path.Combine(root, "temp"),
                controlRegistry: new FrontedControlRegistry([new TextFrontedControl()]));

            var result = await importer.ImportAsync(new FrontedLayoutPackageImportRequest
            {
                PackagePath = archivePath
            }, TestContext.Current.CancellationToken);

            Assert.True(result.Success, result.ErrorMessage);
            var missing = Assert.Single(result.MissingPluginControls);
            Assert.Equal("TeamCard1", missing.ControlName);
            var importedLayoutPath = Path.Combine(packageRoot, "package-with-plugin", "FrontedLayouts", "BpWindow.json");
            var imported = JsonSerializer.Deserialize<FrontedWindowConfig>(File.ReadAllText(importedLayoutPath))!.ToCanvasConfig();
            Assert.True(imported.Controls.ContainsKey("TeamCard1"));
            Assert.True(imported.Controls.ContainsKey("Title"));
            Assert.NotEmpty(imported.RequiredPlugins);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task ManifestOnlyDependencyWithoutPluginControlsDoesNotBlockImport()
    {
        var root = CreateTempDirectory();
        try
        {
            var archivePath = Path.Combine(root, "manifest-only.bpui");
            CreatePluginBpuiArchive(archivePath, includeActualPluginControl: false);
            var importer = new FrontedLayoutPackageImporter(
                Path.Combine(root, "packages"),
                Path.Combine(root, "temp"),
                controlRegistry: new FrontedControlRegistry([new TextFrontedControl()]));

            var result = await importer.ImportAsync(new FrontedLayoutPackageImportRequest
            {
                PackagePath = archivePath
            }, TestContext.Current.CancellationToken);

            Assert.True(result.Success, result.ErrorMessage);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task ImportRejectsBpuiWithPluginDllPayload()
    {
        var root = CreateTempDirectory();
        try
        {
            var archivePath = Path.Combine(root, "plugin-dll.bpui");
            CreateBasicBpuiArchive(archivePath, archive =>
                WriteZipEntry(archive, "Plugins/plfjy.ExamplePlugin/foo.dll", "binary"));

            var result = await CreateImporter(root).ImportAsync(new FrontedLayoutPackageImportRequest
            {
                PackagePath = archivePath
            }, TestContext.Current.CancellationToken);

            Assert.False(result.Success);
            Assert.Contains("Forbidden plugin or executable payload", result.ErrorMessage);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task ImportRejectsBpuiWithExecutableScript()
    {
        var root = CreateTempDirectory();
        try
        {
            var archivePath = Path.Combine(root, "script.bpui");
            CreateBasicBpuiArchive(archivePath, archive =>
                WriteZipEntry(archive, "docs/install.ps1", "Write-Host unsafe"));

            var result = await CreateImporter(root).ImportAsync(new FrontedLayoutPackageImportRequest
            {
                PackagePath = archivePath
            }, TestContext.Current.CancellationToken);

            Assert.False(result.Success);
            Assert.Contains("Forbidden plugin or executable payload", result.ErrorMessage);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task ImportAllowsNormalLayoutsAndUnlistedImageResources()
    {
        var root = CreateTempDirectory();
        try
        {
            var archivePath = Path.Combine(root, "resources.bpui");
            CreateBasicBpuiArchive(archivePath, archive =>
                WriteZipEntry(archive, "resources/images/a.png", Convert.FromBase64String(
                    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=")));

            var result = await CreateImporter(root).ImportAsync(new FrontedLayoutPackageImportRequest
            {
                PackagePath = archivePath
            }, TestContext.Current.CancellationToken);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(File.Exists(Path.Combine(root, "packages", "basic-package", "resources", "images", "a.png")));
            Assert.True(File.Exists(Path.Combine(root, "packages", "basic-package", "FrontedLayouts", "BpWindow.json")));
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task ImportZipSlipCheckStillRejectsEscapedEntry()
    {
        var root = CreateTempDirectory();
        try
        {
            var archivePath = Path.Combine(root, "zip-slip.bpui");
            CreateBasicBpuiArchive(archivePath, archive =>
                WriteZipEntry(archive, "layouts/../evil.json", "{}"));

            var result = await CreateImporter(root).ImportAsync(new FrontedLayoutPackageImportRequest
            {
                PackagePath = archivePath
            }, TestContext.Current.CancellationToken);

            Assert.False(result.Success);
            Assert.Contains("Unsafe zip entry", result.ErrorMessage);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task InstallMarketDependenciesCompletedDownloadRemovesPendingId()
    {
        var root = CreateTempDirectory();
        try
        {
            var market = new FakePluginMarketService(root, completeDownloads: true);
            var install = new FakePluginInstallService();
            var viewModel = CreateFrontManagePageViewModel(market, install);

            await InvokeInstallMarketDependenciesAsync(viewModel, [new PluginMarketItem { Id = "plugin.ok", Name = "Plugin OK" }]);

            Assert.Equal(["plugin.ok"], install.InstalledPluginIds);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task InstallMarketDependenciesFailedDownloadThrowsPluginIdAndError()
    {
        var root = CreateTempDirectory();
        try
        {
            var market = new FakePluginMarketService(root, failDownloads: true, failureMessage: "network failed");
            var viewModel = CreateFrontManagePageViewModel(market, new FakePluginInstallService());

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                InvokeInstallMarketDependenciesAsync(viewModel, [new PluginMarketItem { Id = "plugin.fail", Name = "Plugin Fail" }]));

            Assert.Contains("plugin.fail", ex.Message);
            Assert.Contains("network failed", ex.Message);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task InstallMarketDependenciesQueueEndsWithPendingIdThrows()
    {
        var root = CreateTempDirectory();
        try
        {
            var market = new FakePluginMarketService(root);
            var viewModel = CreateFrontManagePageViewModel(market, new FakePluginInstallService());

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                InvokeInstallMarketDependenciesAsync(viewModel, [new PluginMarketItem { Id = "plugin.pending", Name = "Plugin Pending" }]));

            Assert.Contains("plugin.pending", ex.Message);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task InstallMarketDependenciesInstallExceptionSurfacesPluginIdAndMessage()
    {
        var root = CreateTempDirectory();
        try
        {
            var market = new FakePluginMarketService(root, completeDownloads: true);
            var viewModel = CreateFrontManagePageViewModel(
                market,
                new FakePluginInstallService(new InvalidOperationException("manifest invalid")));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                InvokeInstallMarketDependenciesAsync(viewModel, [new PluginMarketItem { Id = "plugin.bad", Name = "Plugin Bad" }]));

            Assert.Contains("plugin.bad", ex.Message);
            Assert.Contains("manifest invalid", ex.Message);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    private static FrontedControlRegistry CreateRegistryWithExamplePlugin()
    {
        return new FrontedControlRegistry(
            [new TextFrontedControl()],
            [new TeamCardFrontedControlContributor()],
            NullLogger<FrontedControlRegistry>.Instance);
    }

    private static FrontedControlRegistry CreateTextOnlyRegistry()
    {
        return new FrontedControlRegistry([new TextFrontedControl()]);
    }

    private static void CreateUnknownPluginBpuiArchive(string archivePath)
    {
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        WriteZipEntry(archive, "manifest.json", JsonSerializer.Serialize(new FrontedLayoutPackageManifest
        {
            PackageId = "unknown-plugin-package",
            Name = "Unknown Plugin Package",
            Content = new FrontedLayoutPackageManifestContent
            {
                Layouts =
                [
                    new FrontedLayoutPackageLayoutEntry
                    {
                        Window = "plugin:missing.plugin/Overlay",
                        Path = "FrontedLayouts/plugin/missing.plugin/Overlay.json"
                    }
                ]
            }
        }));
        WriteZipEntry(
            archive,
            "FrontedLayouts/plugin/missing.plugin/Overlay.json",
            """
            {
              "Version": 3,
              "CanvasSettings": {
                "CanvasWidth": 100,
                "CanvasHeight": 100
              },
              "ControlLayout": {
                "RequiredPlugins": [],
                "Controls": {
                  "Title": {
                    "ControlType": "Text",
                    "Text": "Unknown Plugin"
                  }
                }
              },
              "VendorExtension": {
                "KeepMe": true
              }
            }
            """);
        WriteZipEntry(
            archive,
            "FrontedBehaviors/plugin/missing.plugin/Overlay.behaviors.json",
            """
            {
              "Version": 1,
              "WindowType": "plugin:missing.plugin/Overlay",
              "CanvasName": "BaseCanvas",
              "ControlBehaviorSets": [
                {
                  "BehaviorGuid": "11111111-1111-1111-1111-111111111111",
                  "Behaviors": []
                }
              ]
            }
            """);
    }

    private static async Task<string> ImportActivateExportAsync(string root, string archivePath)
    {
        var packageRoot = Path.Combine(root, "packages");
        var builtInRoot = Path.Combine(root, "builtIn");
        var tempRoot = Path.Combine(root, "temp");
        Directory.CreateDirectory(builtInRoot);

        var packageManager = new FrontedLayoutPackageManager(
            packageRoot,
            builtInRoot,
            logger: NullLogger<FrontedLayoutPackageManager>.Instance);

        var importer = new FrontedLayoutPackageImporter(
            packageRoot,
            tempRoot,
            packageManager: packageManager,
            controlRegistry: CreateTextOnlyRegistry());

        var importResult = await importer.ImportAsync(new FrontedLayoutPackageImportRequest
        {
            PackagePath = archivePath,
            ReplaceExisting = true,
            ActivateAfterImport = true
        }, TestContext.Current.CancellationToken);
        Assert.True(importResult.Success, importResult.ErrorMessage);

        var outputPath = Path.Combine(root, "exported.bpui");
        var exporter = new FrontedLayoutPackageExporter(
            packageManager,
            packageRoot,
            tempRoot,
            controlRegistry: CreateTextOnlyRegistry());

        var exportResult = await exporter.ExportAsync(new FrontedLayoutPackageExportRequest
        {
            PackageId = "exported-package",
            Name = "Exported Package",
            OutputPath = outputPath
        }, TestContext.Current.CancellationToken);
        Assert.True(exportResult.Success, exportResult.ErrorMessage);
        return outputPath;
    }

    /// <summary>
    /// Task 5.4：导入包含未安装插件窗口（plugin:missing.plugin/Overlay）的 .bpui 包应成功，
    /// 不因当前 Registry 中缺少该插件而阻止导入。
    /// </summary>
    [Fact]
    public async Task UnknownPluginWindow_ImportSucceeds()
    {
        var root = CreateTempDirectory();
        try
        {
            var archivePath = Path.Combine(root, "unknown-plugin.bpui");
            CreateUnknownPluginBpuiArchive(archivePath);

            var packageRoot = Path.Combine(root, "packages");
            var builtInRoot = Path.Combine(root, "builtIn");
            var tempRoot = Path.Combine(root, "temp");
            Directory.CreateDirectory(builtInRoot);

            var packageManager = new FrontedLayoutPackageManager(
                packageRoot,
                builtInRoot,
                logger: NullLogger<FrontedLayoutPackageManager>.Instance);

            var importer = new FrontedLayoutPackageImporter(
                packageRoot,
                tempRoot,
                packageManager: packageManager,
                controlRegistry: CreateTextOnlyRegistry());

            var importResult = await importer.ImportAsync(new FrontedLayoutPackageImportRequest
            {
                PackagePath = archivePath,
                ReplaceExisting = true,
                ActivateAfterImport = true
            }, TestContext.Current.CancellationToken);

            Assert.True(importResult.Success, importResult.ErrorMessage);

            // 导入后布局文件应存在于包目录中。
            var layoutPath = Path.Combine(packageRoot, "unknown-plugin-package", "FrontedLayouts", "plugin", "missing.plugin", "Overlay.json");
            Assert.True(File.Exists(layoutPath), "导入后未知插件窗口的布局文件应存在");
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task UnknownPluginWindow_ImportExportPreservesEntry()
    {
        var root = CreateTempDirectory();
        try
        {
            var archivePath = Path.Combine(root, "unknown-plugin.bpui");
            CreateUnknownPluginBpuiArchive(archivePath);
            var outputPath = await ImportActivateExportAsync(root, archivePath);

            using var archive = ZipFile.OpenRead(outputPath);
            var manifest = ReadManifest(archive);
            Assert.Contains(manifest.Content.Layouts, entry =>
                string.Equals(entry.Window, "plugin:missing.plugin/Overlay", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task UnknownPluginWindow_ImportExportPreservesPath()
    {
        var root = CreateTempDirectory();
        try
        {
            var archivePath = Path.Combine(root, "unknown-plugin.bpui");
            CreateUnknownPluginBpuiArchive(archivePath);
            var outputPath = await ImportActivateExportAsync(root, archivePath);

            using var archive = ZipFile.OpenRead(outputPath);
            var manifest = ReadManifest(archive);
            var entry = Assert.Single(manifest.Content.Layouts);
            Assert.Equal("plugin:missing.plugin/Overlay", entry.Window);
            Assert.Equal("FrontedLayouts/plugin/missing.plugin/Overlay.json", entry.Path);
            Assert.Contains(archive.Entries, e =>
                string.Equals(e.FullName, "FrontedLayouts/plugin/missing.plugin/Overlay.json", StringComparison.OrdinalIgnoreCase));
            var layoutJson = ReadZipEntry(archive, "FrontedLayouts/plugin/missing.plugin/Overlay.json");
            Assert.Contains("VendorExtension", layoutJson);
            Assert.Contains("KeepMe", layoutJson);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task UnknownPluginWindow_ImportExportPreservesBehavior()
    {
        var root = CreateTempDirectory();
        try
        {
            var archivePath = Path.Combine(root, "unknown-plugin.bpui");
            CreateUnknownPluginBpuiArchive(archivePath);
            var outputPath = await ImportActivateExportAsync(root, archivePath);

            using var archive = ZipFile.OpenRead(outputPath);
            Assert.Contains(archive.Entries, e =>
                string.Equals(e.FullName, "FrontedBehaviors/plugin/missing.plugin/Overlay.behaviors.json", StringComparison.OrdinalIgnoreCase));
            var behaviorJson = ReadZipEntry(archive, "FrontedBehaviors/plugin/missing.plugin/Overlay.behaviors.json");
            Assert.Contains("plugin:missing.plugin/Overlay", behaviorJson);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task Exporter_DoesNotSynthesizeUnsavedEmptyLayouts()
    {
        var root = CreateTempDirectory();
        try
        {
            var builtInRoot = Path.Combine(root, "builtIn");
            var packageRoot = Path.Combine(root, "packages");
            var tempRoot = Path.Combine(root, "temp");
            var outputPath = Path.Combine(root, "exported.bpui");
            Directory.CreateDirectory(builtInRoot);
            File.WriteAllText(
                Path.Combine(builtInRoot, "BpWindow.json"),
                """
                {
                  "Version": 3,
                  "CanvasSettings": {
                    "CanvasWidth": 100,
                    "CanvasHeight": 100
                  },
                  "ControlLayout": {
                    "RequiredPlugins": [],
                    "Controls": {
                      "Title": {
                        "ControlType": "Text",
                        "Text": "Built-in"
                      }
                    }
                  }
                }
                """);

            var packageManager = new FrontedLayoutPackageManager(
                packageRoot,
                builtInRoot,
                logger: NullLogger<FrontedLayoutPackageManager>.Instance);
            var exporter = new FrontedLayoutPackageExporter(
                packageManager,
                packageRoot,
                tempRoot,
                controlRegistry: CreateTextOnlyRegistry());

            var result = await exporter.ExportAsync(new FrontedLayoutPackageExportRequest
            {
                PackageId = "single-window-export",
                Name = "Single Window Export",
                OutputPath = outputPath
            }, TestContext.Current.CancellationToken);

            Assert.True(result.Success, result.ErrorMessage);
            using var archive = ZipFile.OpenRead(outputPath);
            var manifest = ReadManifest(archive);
            var entry = Assert.Single(manifest.Content.Layouts);
            Assert.Equal("BpWindow", entry.Window);
            Assert.DoesNotContain(archive.Entries, e =>
                e.FullName.StartsWith("FrontedLayouts/", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(e.FullName, "FrontedLayouts/BpWindow.json", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    private static void WriteAllCatalogLayouts(string builtInRoot, bool includePluginOnFirstLayout)
    {
        var first = true;
        foreach (var entry in new FrontedDesignerLayoutCatalog(CreateBuiltInV3Registry()).GetEntries())
        {
            var path = Path.Combine(builtInRoot, $"{entry.CanonicalWindowId}.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var pluginJson = first && includePluginOnFirstLayout
                ? """
                  ,
                    "TeamCard1": {
                      "ControlType": "plugin:plfjy.ExamplePlugin/TeamCard",
                      "Left": 12,
                      "Top": 24,
                      "Width": 260,
                      "Height": 96
                    }
                  """
                : string.Empty;
            File.WriteAllText(
                path,
                $$"""
                  {
                    "Version": 3,
                    "CanvasSettings": {
                      "CanvasWidth": 100,
                      "CanvasHeight": 100
                    },
                    "ControlLayout": {
                      "RequiredPlugins": [],
                      "Controls": {
                        "Title": {
                          "ControlType": "Text",
                          "Text": "Built-in"
                        }{{pluginJson}}
                      }
                    }
                  }
                  """);
            first = false;
        }
    }

    private static void CreatePluginBpuiArchive(
        string archivePath,
        bool includeActualPluginControl,
        string? minVersion = null)
    {
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        WriteZipEntry(archive, "manifest.json", JsonSerializer.Serialize(new FrontedLayoutPackageManifest
        {
            PackageId = "package-with-plugin",
            Name = "Package With Plugin",
            PluginDependencies =
            [
                new FrontedPluginDependency
                {
                    PackageId = "plfjy.ExamplePlugin",
                    MinVersion = minVersion,
                    DisplayName = "ExamplePlugin",
                    MarketplaceId = "plfjy.ExamplePlugin",
                    Controls = includeActualPluginControl
                        ? ["plugin:plfjy.ExamplePlugin/TeamCard"]
                        : [],
                    RequiredBy = ["BpWindow"]
                }
            ],
            Content = new FrontedLayoutPackageManifestContent
            {
                Layouts =
                [
                    new FrontedLayoutPackageLayoutEntry
                    {
                        Window = "BpWindow",
                        Path = "FrontedLayouts/BpWindow.json"
                    }
                ]
            }
        }));

        var pluginJson = includeActualPluginControl
            ? """
              ,
                "TeamCard1": {
                  "ControlType": "plugin:plfjy.ExamplePlugin/TeamCard",
                  "Left": 10,
                  "Top": 10,
                  "Width": 260,
                  "Height": 96
                }
              """
            : string.Empty;
        WriteZipEntry(
            archive,
            "FrontedLayouts/BpWindow.json",
            $$"""
              {
                "Version": 3,
                "CanvasSettings": {
                  "CanvasWidth": 100,
                  "CanvasHeight": 100
                },
                "ControlLayout": {
                  "RequiredPlugins": [
                    {
                      "PackageId": "plfjy.ExamplePlugin",
                      "MinVersion": {{JsonSerializer.Serialize(minVersion)}},
                      "Controls": {{(includeActualPluginControl ? "[\"plugin:plfjy.ExamplePlugin/TeamCard\"]" : "[]")}}
                    }
                  ],
                  "Controls": {
                    "Title": {
                      "ControlType": "Text",
                      "Text": "Built-in"
                    }{{pluginJson}}
                  }
                }
              }
              """);
    }

    private static void CreateBasicBpuiArchive(string archivePath, Action<ZipArchive>? addExtraEntries = null)
    {
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        WriteZipEntry(archive, "manifest.json", JsonSerializer.Serialize(new FrontedLayoutPackageManifest
        {
            PackageId = "basic-package",
            Name = "Basic Package",
            Content = new FrontedLayoutPackageManifestContent
            {
                Layouts =
                [
                    new FrontedLayoutPackageLayoutEntry
                    {
                        Window = "BpWindow",
                        Path = "FrontedLayouts/BpWindow.json"
                    }
                ]
            }
        }));
        WriteZipEntry(
            archive,
            "FrontedLayouts/BpWindow.json",
            """
            {
              "Version": 3,
              "CanvasSettings": {
                "CanvasWidth": 100,
                "CanvasHeight": 100
              },
              "ControlLayout": {
                "RequiredPlugins": [],
                "Controls": {
                  "Title": {
                    "ControlType": "Text",
                    "Text": "Built-in"
                  }
                }
              }
            }
            """);
        addExtraEntries?.Invoke(archive);
    }

    private static void WriteZipEntry(ZipArchive archive, string entryName, string text)
    {
        var entry = archive.CreateEntry(entryName);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write(text);
    }

    private static void WriteZipEntry(ZipArchive archive, string entryName, byte[] bytes)
    {
        var entry = archive.CreateEntry(entryName);
        using var stream = entry.Open();
        stream.Write(bytes);
    }

    private static FrontedLayoutPackageImporter CreateImporter(string root)
    {
        return new FrontedLayoutPackageImporter(
            Path.Combine(root, "packages"),
            Path.Combine(root, "temp"),
            controlRegistry: new FrontedControlRegistry([new TextFrontedControl()]));
    }

    private static FrontManagePageViewModel CreateFrontManagePageViewModel(
        IPluginMarketService pluginMarketService,
        IPluginInstallService pluginInstallService)
    {
        return new FrontManagePageViewModel(
            Mock.Of<IFrontedWindowService>(),
            Mock.Of<ISharedDataService>(),
            Mock.Of<IFilePickerService>(),
            Mock.Of<IFrontedLayoutPackageManager>(),
            Mock.Of<IFrontedLayoutPackageExporter>(),
            Mock.Of<IFrontedLayoutPackageImporter>(),
            Mock.Of<IFrontedLayoutPackageLegacyConverter>(),
            pluginMarketService,
            pluginInstallService,
            Mock.Of<IFrontedWindowRegistry>(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<FrontManagePageViewModel>.Instance);
    }

    private static async Task InvokeInstallMarketDependenciesAsync(
        FrontManagePageViewModel viewModel,
        IReadOnlyList<PluginMarketItem> marketItems)
    {
        var method = typeof(FrontManagePageViewModel).GetMethod(
            "InstallMarketDependenciesAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(FrontManagePageViewModel), "InstallMarketDependenciesAsync");

        var task = (Task)method.Invoke(viewModel, [marketItems])!;
        await task;
    }

    private static FrontedLayoutPackageManifest ReadManifest(ZipArchive archive)
    {
        return JsonSerializer.Deserialize<FrontedLayoutPackageManifest>(
            ReadZipEntry(archive, "manifest.json"),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    private static string ReadZipEntry(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName) ?? throw new InvalidOperationException($"Missing zip entry {entryName}.");
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "neo-bpsys-wpf-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTempDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static void RunOnStaThread(Action action)
    {
        WpfTestThread.Run(action);
    }

    private static string GetRepositoryPath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                && File.Exists(Path.Combine(directory.FullName, "neo-bpsys-wpf.slnx")))
            {
                return Path.Combine([directory.FullName, .. parts]);
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    /// <summary>
    /// 构造包含 8 个内置 v3 窗口 registration 的注册表，用于测试目录遍历。
    /// 该辅助方法仅用于让测试拥有一个稳定的注册表集合；生产代码不再硬编码这些窗口。
    /// </summary>
    private static FrontedWindowRegistryService CreateBuiltInV3Registry()
    {
        var localIds = new[]
        {
            "ScoreSurWindow",
            "ScoreHunWindow",
            "ScoreGlobalWindow",
            "CutSceneWindow",
            "GameDataWindow",
            "BpOverviewWindow",
            "MapV2Window",
            "BpWindow"
        };

        var registrations = localIds
            .Select(localId => new FrontedV3LayoutWindowRegistration
            {
                Id = localId,
                LocalId = localId,
                IsBuiltIn = true,
                DisplayName = localId
            })
            .Cast<FrontedWindowRegistration>()
            .ToArray();

        return new FrontedWindowRegistryService(registrations);
    }

    private sealed class FakePluginMarketService : IPluginMarketService
    {
        private readonly string _root;
        private readonly bool _completeDownloads;
        private readonly bool _failDownloads;
        private readonly string _failureMessage;
        private readonly ObservableCollection<PluginDownloadQueueItem> _queue = [];
        private readonly Queue<PluginPackageDownloadResult> _completedDownloads = [];

        public FakePluginMarketService(
            string root,
            bool completeDownloads = false,
            bool failDownloads = false,
            string failureMessage = "")
        {
            _root = root;
            _completeDownloads = completeDownloads;
            _failDownloads = failDownloads;
            _failureMessage = failureMessage;
            DownloadQueue = new ReadOnlyObservableCollection<PluginDownloadQueueItem>(_queue);
        }

        public ReadOnlyObservableCollection<PluginDownloadQueueItem> DownloadQueue { get; }

        public bool IsDownloading => false;

        public bool IsDownloadFinished => _completedDownloads.Count > 0;

        public double DownloadProgress => 0;

        public double DownloadBytesPerSecond => 0;

        public string CurrentDownloadPluginId => string.Empty;

        public event EventHandler? DownloadStateChanged;

        public Task<IReadOnlyList<PluginMarketItem>> GetMarketPluginsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PluginMarketItem>>([]);
        }

        public Task<string> GetReadmeMarkdownAsync(PluginMarketItem item, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(string.Empty);
        }

        public Task<bool> QueuePluginDownloadAsync(PluginMarketItem item, CancellationToken cancellationToken = default)
        {
            var queueItem = new PluginDownloadQueueItem
            {
                PluginId = item.Id,
                PluginName = item.Name,
                PluginVersion = item.Version
            };
            _queue.Add(queueItem);

            if (_failDownloads)
            {
                queueItem.Status = PluginDownloadQueueStatus.QueueFailed;
                queueItem.ErrorMessage = _failureMessage;
            }
            else if (_completeDownloads)
            {
                queueItem.Status = PluginDownloadQueueStatus.QueueDownloaded;
                var extractedDirectory = Path.Combine(_root, "download", item.Id);
                Directory.CreateDirectory(extractedDirectory);
                _completedDownloads.Enqueue(new PluginPackageDownloadResult
                {
                    ExtractedDirectoryPath = extractedDirectory,
                    QueueItem = queueItem
                });
            }
            else
            {
                queueItem.Status = PluginDownloadQueueStatus.QueueCanceled;
            }

            DownloadStateChanged?.Invoke(this, EventArgs.Empty);
            return Task.FromResult(true);
        }

        public PluginPackageDownloadResult? ConsumeCompletedDownload()
        {
            return _completedDownloads.Count == 0 ? null : _completedDownloads.Dequeue();
        }

        public void CancelDownload()
        {
        }

        public void CancelDownload(string queueId)
        {
        }

        public void ResetMirrorCache()
        {
        }
    }

    private sealed class FakePluginInstallService(Exception? exception = null) : IPluginInstallService
    {
        public List<string> InstalledPluginIds { get; } = [];

        public PluginInstallResult InstallFromArchive(string archivePath, string extractedDirectoryPath)
        {
            return InstallFromExtractedDirectory(extractedDirectoryPath);
        }

        public Task<PluginInstallResult> InstallFromArchiveAsync(
            string archivePath,
            string extractedDirectoryPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(InstallFromArchive(archivePath, extractedDirectoryPath));
        }

        public PluginInstallResult InstallFromExtractedDirectory(string extractedDirectoryPath)
        {
            if (exception is not null)
            {
                throw exception;
            }

            var id = Path.GetFileName(extractedDirectoryPath);
            InstalledPluginIds.Add(id);
            return new PluginInstallResult
            {
                Manifest = new neo_bpsys_wpf.Core.Models.PluginManifest
                {
                    Id = id,
                    Version = "1.0.0"
                },
                RestartRequired = true
            };
        }
    }

    private sealed class FakePluginMetadataProvider(params (string Id, string Version, string DisplayName)[] plugins)
        : IFrontedPluginMetadataProvider
    {
        public bool IsPluginInstalled(string packageId)
        {
            return plugins.Any(plugin => string.Equals(plugin.Id, packageId, StringComparison.OrdinalIgnoreCase));
        }

        public bool TryGetPluginVersion(string packageId, out string version)
        {
            var plugin = plugins.FirstOrDefault(plugin => string.Equals(plugin.Id, packageId, StringComparison.OrdinalIgnoreCase));
            version = plugin.Version ?? string.Empty;
            return !string.IsNullOrWhiteSpace(version);
        }

        public bool TryGetPluginDisplayName(string packageId, out string displayName)
        {
            var plugin = plugins.FirstOrDefault(plugin => string.Equals(plugin.Id, packageId, StringComparison.OrdinalIgnoreCase));
            displayName = plugin.DisplayName ?? string.Empty;
            return !string.IsNullOrWhiteSpace(displayName);
        }

        public bool TryGetPluginFolder(string packageId, out string folder)
        {
            folder = string.Empty;
            return false;
        }
    }
}
