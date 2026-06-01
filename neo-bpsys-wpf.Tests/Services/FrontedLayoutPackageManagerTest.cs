#nullable enable

using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.ViewModels.Windows;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public class FrontedLayoutPackageManagerTest
{
    [Fact]
    public async Task BuiltinPackageIsAlwaysListedAndActiveByDefault()
    {
        var root = CreateTempDirectory();
        try
        {
            var manager = new FrontedLayoutPackageManager(
                Path.Combine(root, "packages"),
                Path.Combine(root, "builtIn"));

            var packages = await manager.ListPackagesAsync(TestContext.Current.CancellationToken);

            var builtIn = Assert.Single(packages);
            Assert.Equal("builtin", builtIn.PackageId);
            Assert.True(builtIn.IsBuiltin);
            Assert.True(builtIn.IsActive);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task LocalDirectoryIsNotListedAsNormalInstalledPackage()
    {
        var root = CreateTempDirectory();
        try
        {
            var packageRoot = Path.Combine(root, "packages");
            Directory.CreateDirectory(Path.Combine(packageRoot, "local"));
            var manager = new FrontedLayoutPackageManager(packageRoot, Path.Combine(root, "builtIn"));

            var packages = await manager.ListPackagesAsync(TestContext.Current.CancellationToken);

            Assert.DoesNotContain(packages, package => package.PackageId == "local");
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task InstalledPackageWithValidManifestIsListedAndReadsRootMinVersion()
    {
        var root = CreateTempDirectory();
        try
        {
            var packageRoot = Path.Combine(root, "packages");
            WriteManifest(Path.Combine(packageRoot, "plfjy.default-layout.2026"), new
            {
                Format = "neo-bpsys-bpui",
                FormatVersion = 3,
                PackageId = "plfjy.default-layout.2026",
                Name = "Default Layout",
                Description = "Designer v3 defaults",
                Author = "PLFJY",
                CreatedAt = "2026-05-31T10:00:00Z",
                MinVersion = "3.0.0",
                Content = new
                {
                    Layouts = new[] { new { Window = "BpWindow", Canvas = "BaseCanvas", Path = "layouts/BpWindow/BaseCanvas.json" } },
                    Resources = Array.Empty<object>()
                },
                App = new { MinVersion = "ignored" }
            });
            var manager = new FrontedLayoutPackageManager(packageRoot, Path.Combine(root, "builtIn"));

            var packages = await manager.ListPackagesAsync(TestContext.Current.CancellationToken);

            var package = Assert.Single(packages, item => item.PackageId == "plfjy.default-layout.2026");
            Assert.Equal("Default Layout", package.Name);
            Assert.Equal("3.0.0", package.MinVersion);
            Assert.Equal(1, package.LayoutCount);
            Assert.Equal(FrontedLayoutPackageValidationStatus.Valid, package.ValidationStatus);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task MissingOrInvalidManifestIsListedWithValidationError()
    {
        var root = CreateTempDirectory();
        try
        {
            var packageRoot = Path.Combine(root, "packages");
            Directory.CreateDirectory(Path.Combine(packageRoot, "missing-manifest"));
            var invalidFolder = Path.Combine(packageRoot, "invalid-manifest");
            Directory.CreateDirectory(invalidFolder);
            File.WriteAllText(Path.Combine(invalidFolder, "manifest.json"), "{ invalid");
            var manager = new FrontedLayoutPackageManager(packageRoot, Path.Combine(root, "builtIn"));

            var packages = await manager.ListPackagesAsync(TestContext.Current.CancellationToken);

            Assert.Contains(packages, package =>
                package.PackageId == "missing-manifest"
                && package.ValidationStatus == FrontedLayoutPackageValidationStatus.Error);
            Assert.Contains(packages, package =>
                package.PackageId == "invalid-manifest"
                && package.ValidationStatus == FrontedLayoutPackageValidationStatus.Error);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task PackageIdSanitizationRejectsTraversalAndDeleteRefusesReservedPackages()
    {
        var root = CreateTempDirectory();
        try
        {
            var manager = new FrontedLayoutPackageManager(
                Path.Combine(root, "packages"),
                Path.Combine(root, "builtIn"));

            Assert.False(FrontedLayoutPackageManager.IsSafePackageId("../evil"));
            Assert.False(FrontedLayoutPackageManager.IsSafePackageId("evil%2fpackage"));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                manager.DeletePackageAsync("builtin", TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                manager.DeletePackageAsync("local", TestContext.Current.CancellationToken));
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Theory]
    [InlineData("plfjy.default-layout.2026")]
    [InlineData("package_id")]
    [InlineData("package-id")]
    public void ExporterPackageIdValidationAcceptsSafeIds(string packageId)
    {
        Assert.True(FrontedLayoutPackageExporter.IsSafePackageId(packageId));
    }

    [Theory]
    [InlineData("../evil")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a:b")]
    [InlineData("")]
    [InlineData(" ")]
    public void ExporterPackageIdValidationRejectsUnsafeIds(string packageId)
    {
        Assert.False(FrontedLayoutPackageExporter.IsSafePackageId(packageId));
    }

    [Fact]
    public void ManifestSerializesBpuiV3RootFieldsWithoutAppObject()
    {
        var manifest = new FrontedLayoutPackageManifest
        {
            PackageId = "plfjy.default-layout.2026",
            Name = "Default Layout",
            MinVersion = "3.0.0"
        };

        var json = JsonSerializer.Serialize(manifest);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("neo-bpsys-bpui", root.GetProperty("Format").GetString());
        Assert.Equal(3, root.GetProperty("FormatVersion").GetInt32());
        Assert.Equal(3, root.GetProperty("LayoutSchemaVersion").GetInt32());
        Assert.Equal("3.0.0", root.GetProperty("MinVersion").GetString());
        Assert.False(root.TryGetProperty("App", out _));
    }

    [Fact]
    public void ExportWindowRuntimeConstructorInjectsViewModelAsDataContext()
    {
        var text = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Windows",
            "FrontedLayoutPackageExportWindow.xaml.cs"));

        Assert.Contains(
            "public FrontedLayoutPackageExportWindow(FrontedLayoutPackageExportWindowViewModel viewModel)",
            text);
        Assert.Contains("DataContext = viewModel;", text);
    }

    [Fact]
    public void ExportWindowViewModelDefaultsAuthorAndMinVersionWithoutScopeOptions()
    {
        var viewModel = new FrontedLayoutPackageExportWindowViewModel(new FakeFilePickerService(null));

        Assert.Equal(Environment.UserName ?? string.Empty, viewModel.Author);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.MinVersion));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.PackageId));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.PackageName));
        Assert.True(FrontedLayoutPackageExporter.IsSafePackageId(viewModel.PackageId));

        var type = typeof(FrontedLayoutPackageExportWindowViewModel);
        Assert.Null(type.GetProperty("ScopeOptions"));
        Assert.Null(type.GetProperty("SelectedScopeOption"));
    }

    [Fact]
    public void BrowseOutputPathCommandCallsFilePickerAndUpdatesOutputPath()
    {
        var picker = new FakeFilePickerService(@"C:\exports\layout.bpui");
        var viewModel = new FrontedLayoutPackageExportWindowViewModel(picker)
        {
            PackageId = "package-id"
        };

        viewModel.BrowseOutputPathCommand.Execute(null);

        Assert.Equal(@"C:\exports\layout.bpui", viewModel.OutputPath);
        Assert.Equal(1, picker.SaveBpuiFileCallCount);
        Assert.Equal("package-id.bpui", picker.LastDefaultFileName);
    }

    [Fact]
    public void BrowseOutputPathCommandIsSafeWithoutFilePickerService()
    {
        var viewModel = new FrontedLayoutPackageExportWindowViewModel();

        viewModel.BrowseOutputPathCommand.Execute(null);

        Assert.Equal(string.Empty, viewModel.OutputPath);
    }

    [Fact]
    public void ExportWindowViewModelCreateRequestKeepsRootMinVersion()
    {
        var viewModel = new FrontedLayoutPackageExportWindowViewModel(new FakeFilePickerService(null))
        {
            PackageId = "package-id",
            PackageName = "Package",
            Author = "Author",
            MinVersion = "2.0.9",
            OutputPath = @"C:\exports\layout.bpui"
        };

        var request = viewModel.CreateRequest();

        Assert.NotNull(request);
        Assert.Equal("2.0.9", request.MinVersion);
        Assert.Equal(FrontedLayoutPackageExportScope.AllFrontendLayouts, request.ExportScope);
    }

    [Fact]
    public async Task ExportAllLayoutsCreatesSafeBpuiZipAndRewritesCopiedResources()
    {
        var root = CreateTempDirectory();
        try
        {
            var builtInRoot = Path.Combine(root, "builtIn");
            var userRoot = Path.Combine(root, "user");
            var packageRoot = Path.Combine(root, "packages");
            var tempRoot = Path.Combine(root, "temp");
            var outputPath = Path.Combine(root, "export.bpui");
            var localImagePath = Path.Combine(packageRoot, "local", "resources", "images", "local.png");
            var absoluteImagePath = Path.Combine(root, "absolute.png");
            Directory.CreateDirectory(Path.GetDirectoryName(localImagePath)!);
            WriteTinyPng(localImagePath);
            WriteTinyPng(absoluteImagePath);
            File.AppendAllBytes(absoluteImagePath, [0]);

            var catalog = new FrontedDesignerLayoutCatalog();
            WriteCatalogLayouts(
                catalog,
                builtInRoot,
                "Resources/foo.png",
                "bpui://local/resources/images/local.png",
                absoluteImagePath);
            var layoutService = new FrontedLayoutService(
                new FrontedUserLayoutStore(userRoot),
                builtInRoot,
                null);
            var optionsService = new FrontedWindowLayoutOptionsService(userRoot);
            var exporter = new FrontedLayoutPackageExporter(
                catalog,
                layoutService,
                optionsService,
                packageRoot,
                tempRoot);

            var result = await exporter.ExportAsync(new FrontedLayoutPackageExportRequest
            {
                PackageId = "plfjy.default-layout.2026",
                Name = "Default Layout",
                Author = "PLFJY",
                MinVersion = "3.0.0",
                OutputPath = outputPath,
                ExportScope = FrontedLayoutPackageExportScope.AllFrontendLayouts
            }, TestContext.Current.CancellationToken);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(File.Exists(outputPath));
            using var archive = ZipFile.OpenRead(outputPath);
            var entryNames = archive.Entries.Select(entry => entry.FullName.Replace('\\', '/')).ToArray();
            Assert.Contains("manifest.json", entryNames);
            Assert.Contains("layouts/ScoreSurWindow/BaseCanvas.json", entryNames);
            Assert.DoesNotContain("Config.json", entryNames);
            Assert.DoesNotContain(entryNames, name => name.StartsWith("CustomUi/", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(entryNames, name => name.StartsWith("FrontElementsConfig/", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(entryNames, name => Path.IsPathRooted(name) || name.Contains("..", StringComparison.Ordinal));
            Assert.Contains(entryNames, name => name.StartsWith("resources/images/local-", StringComparison.Ordinal));
            Assert.Contains(entryNames, name => name.StartsWith("resources/images/absolute-", StringComparison.Ordinal));

            var manifest = ReadManifest(archive);
            Assert.Equal(result.LayoutCount, manifest.Content.Layouts.Count);
            Assert.Equal(result.ResourceCount, manifest.Content.Resources.Count);
            Assert.Equal(2, manifest.Content.Resources.Count);
            Assert.All(manifest.Content.Resources, resource => Assert.False(string.IsNullOrWhiteSpace(resource.Sha256)));

            var builtInLayoutJson = ReadZipEntry(archive, "layouts/ScoreSurWindow/BaseCanvas.json");
            Assert.Contains("\"BackgroundImage\": \"Resources/foo.png\"", builtInLayoutJson);
            var localLayoutJson = ReadZipEntry(archive, "layouts/ScoreHunWindow/BaseCanvas.json");
            Assert.Contains("bpui://plfjy.default-layout.2026/resources/images/local-", localLayoutJson);
            var absoluteLayoutJson = ReadZipEntry(archive, "layouts/ScoreGlobalWindow/BaseCanvas.json");
            Assert.Contains("bpui://plfjy.default-layout.2026/resources/images/absolute-", absoluteLayoutJson);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task MissingAbsoluteImageCausesClearExportFailure()
    {
        var root = CreateTempDirectory();
        try
        {
            var builtInRoot = Path.Combine(root, "builtIn");
            var missingPath = Path.Combine(root, "missing.png");
            var catalog = new FrontedDesignerLayoutCatalog();
            WriteCatalogLayouts(catalog, builtInRoot, missingPath, "Resources/foo.png", "Resources/bar.png");
            var exporter = new FrontedLayoutPackageExporter(
                catalog,
                new FrontedLayoutService(new FrontedUserLayoutStore(Path.Combine(root, "user")), builtInRoot, null),
                new FrontedWindowLayoutOptionsService(Path.Combine(root, "user")),
                Path.Combine(root, "packages"),
                Path.Combine(root, "temp"));

            var result = await exporter.ExportAsync(new FrontedLayoutPackageExportRequest
            {
                PackageId = "package-id",
                Name = "Package",
                OutputPath = Path.Combine(root, "export.bpui"),
                ExportScope = FrontedLayoutPackageExportScope.AllFrontendLayouts
            }, TestContext.Current.CancellationToken);

            Assert.False(result.Success);
            Assert.Contains("Referenced resource file was not found", result.ErrorMessage);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task ValidV3PackageImportsResourcesUnderPackageDirectory()
    {
        var root = CreateTempDirectory();
        try
        {
            var packageRoot = Path.Combine(root, "packages");
            var archivePath = Path.Combine(root, "package.bpui");
            CreateBpuiArchive(archivePath, "package-a", "bpui://package-a/resources/images/bg.png");
            var importer = new FrontedLayoutPackageImporter(packageRoot, Path.Combine(root, "temp"));

            var result = await importer.ImportAsync(new FrontedLayoutPackageImportRequest
            {
                PackagePath = archivePath
            }, TestContext.Current.CancellationToken);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal("package-a", result.PackageId);
            Assert.True(File.Exists(Path.Combine(packageRoot, "package-a", "manifest.json")));
            Assert.True(File.Exists(Path.Combine(packageRoot, "package-a", "resources", "images", "bg.png")));
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task LegacyPackageIsDetectedWithoutInstalling()
    {
        var root = CreateTempDirectory();
        try
        {
            var archivePath = Path.Combine(root, "legacy.bpui");
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("Config.json");
                await using var stream = entry.Open();
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync("{}");
            }

            var importer = new FrontedLayoutPackageImporter(Path.Combine(root, "packages"), Path.Combine(root, "temp"));

            var result = await importer.ImportAsync(new FrontedLayoutPackageImportRequest
            {
                PackagePath = archivePath
            }, TestContext.Current.CancellationToken);

            Assert.False(result.Success);
            Assert.True(result.IsLegacyPackage);
            Assert.False(Directory.Exists(Path.Combine(root, "packages")));
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Theory]
    [InlineData("CustomUi/bg.png")]
    [InlineData("FrontElementsConfig/BpWindowConfig-BaseCanvas.json")]
    public async Task LegacyPackageIsDetectedFromLegacyFolders(string entryName)
    {
        var root = CreateTempDirectory();
        try
        {
            var archivePath = Path.Combine(root, "legacy.bpui");
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                WriteZipEntry(archive, entryName, "{}");
            }

            var importer = new FrontedLayoutPackageImporter(Path.Combine(root, "packages"), Path.Combine(root, "temp"));

            var result = await importer.ImportAsync(new FrontedLayoutPackageImportRequest
            {
                PackagePath = archivePath
            }, TestContext.Current.CancellationToken);

            Assert.False(result.Success);
            Assert.True(result.IsLegacyPackage);
            Assert.False(Directory.Exists(Path.Combine(root, "packages")));
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task LegacyConverterCreatesCleanV3ManifestCopiesResourcesAndGeometry()
    {
        var root = CreateTempDirectory();
        try
        {
            var builtInRoot = Path.Combine(root, "builtIn");
            WriteBuiltInLayoutForLegacyConversion(builtInRoot);
            var legacyArchive = Path.Combine(root, "legacy.bpui");
            CreateLegacyBpuiArchive(
                legacyArchive,
                includeConfig: true,
                includeResource: true,
                includeKnownLayout: true,
                includeUnknownLayout: true);
            var converter = new FrontedLayoutPackageLegacyConverter(
                builtInRoot,
                Path.Combine(root, "temp"));

            var result = await converter.ConvertAsync(new FrontedLayoutPackageLegacyConvertRequest
            {
                LegacyPackagePath = legacyArchive,
                PackageId = "converted.legacy.test",
                Name = "legacy",
                Description = "Converted package",
                Author = string.Empty,
                MinVersion = "3.0.0"
            }, TestContext.Current.CancellationToken);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(File.Exists(result.ConvertedPackagePath));
            Assert.Equal(1, result.LayoutCount);
            Assert.Equal(1, result.ResourceCount);
            Assert.Contains(result.Warnings, warning => warning.Contains("Unknown legacy layout file", StringComparison.OrdinalIgnoreCase));

            using var archive = ZipFile.OpenRead(result.ConvertedPackagePath!);
            var entryNames = archive.Entries.Select(entry => entry.FullName.Replace('\\', '/')).ToArray();
            Assert.Contains("manifest.json", entryNames);
            Assert.Contains("layouts/ScoreSurWindow/BaseCanvas.json", entryNames);
            Assert.Contains(entryNames, name => name.StartsWith("resources/images/bg-", StringComparison.Ordinal));
            Assert.DoesNotContain("Config.json", entryNames);
            Assert.DoesNotContain(entryNames, name => name.StartsWith("CustomUi/", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(entryNames, name => name.StartsWith("FrontElementsConfig/", StringComparison.OrdinalIgnoreCase));

            var manifestJson = ReadZipEntry(archive, "manifest.json");
            using var manifestDocument = JsonDocument.Parse(manifestJson);
            var manifestRoot = manifestDocument.RootElement;
            Assert.Equal("neo-bpsys-bpui", manifestRoot.GetProperty("Format").GetString());
            Assert.Equal(3, manifestRoot.GetProperty("FormatVersion").GetInt32());
            Assert.Equal(3, manifestRoot.GetProperty("LayoutSchemaVersion").GetInt32());
            Assert.Equal("legacy", manifestRoot.GetProperty("Name").GetString());
            Assert.Equal(string.Empty, manifestRoot.GetProperty("Author").GetString());
            Assert.Equal("3.0.0", manifestRoot.GetProperty("MinVersion").GetString());
            Assert.False(manifestRoot.TryGetProperty("App", out _));
            Assert.False(string.IsNullOrWhiteSpace(
                manifestRoot.GetProperty("Content").GetProperty("Resources")[0].GetProperty("Sha256").GetString()));

            var layoutJson = ReadZipEntry(archive, "layouts/ScoreSurWindow/BaseCanvas.json");
            var layout = JsonSerializer.Deserialize<neo_bpsys_wpf.Core.Models.FrontedLayout.FrontedCanvasConfig>(layoutJson)!;
            Assert.Equal(3, layout.Version);
            Assert.StartsWith("bpui://converted.legacy.test/resources/images/bg-", layout.BackgroundImage);
            var control = layout.Controls["SurTeamName"];
            Assert.Equal(11, control.Left);
            Assert.Equal(22, control.Top);
            Assert.Equal(33, control.Width);
            Assert.Equal(44, control.Height);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task ConvertedLegacyPackageImportsThroughV3ImporterAndCanActivate()
    {
        var root = CreateTempDirectory();
        try
        {
            var builtInRoot = Path.Combine(root, "builtIn");
            WriteBuiltInLayoutForLegacyConversion(builtInRoot);
            var legacyArchive = Path.Combine(root, "legacy.bpui");
            CreateLegacyBpuiArchive(legacyArchive, includeConfig: true, includeResource: true, includeKnownLayout: true);
            var packageRoot = Path.Combine(root, "packages");
            var userLayoutRoot = Path.Combine(root, "userLayouts");
            var manager = new FrontedLayoutPackageManager(packageRoot, builtInRoot, userLayoutRoot);
            var importer = new FrontedLayoutPackageImporter(packageRoot, Path.Combine(root, "importTemp"), manager);
            var converter = new FrontedLayoutPackageLegacyConverter(
                builtInRoot,
                Path.Combine(root, "convertTemp"));

            var convertResult = await converter.ConvertAsync(new FrontedLayoutPackageLegacyConvertRequest
            {
                LegacyPackagePath = legacyArchive,
                PackageId = "converted.legacy.test",
                Name = "Converted"
            }, TestContext.Current.CancellationToken);
            Assert.True(convertResult.Success, convertResult.ErrorMessage);

            var importResult = await importer.ImportAsync(new FrontedLayoutPackageImportRequest
            {
                PackagePath = convertResult.ConvertedPackagePath!,
                ActivateAfterImport = true
            }, TestContext.Current.CancellationToken);

            Assert.True(importResult.Success, importResult.ErrorMessage);
            Assert.True(File.Exists(Path.Combine(packageRoot, "converted.legacy.test", "manifest.json")));
            Assert.True(File.Exists(Path.Combine(userLayoutRoot, "ScoreSurWindow", "BaseCanvas.json")));
            var active = await manager.GetActivePackageStateAsync(TestContext.Current.CancellationToken);
            Assert.Equal("converted.legacy.test", active.PackageId);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task LegacyConversionDoesNotOverwriteAppDataConfigJson()
    {
        var root = CreateTempDirectory();
        var configPath = AppConstants.ConfigFilePath;
        var backupPath = configPath + ".phase9f-test-backup";
        var hadExisting = File.Exists(configPath);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            if (hadExisting)
            {
                File.Copy(configPath, backupPath, overwrite: true);
            }

            File.WriteAllText(configPath, "sentinel-current-config");
            var builtInRoot = Path.Combine(root, "builtIn");
            WriteBuiltInLayoutForLegacyConversion(builtInRoot);
            var legacyArchive = Path.Combine(root, "legacy.bpui");
            CreateLegacyBpuiArchive(legacyArchive, includeConfig: true, includeResource: false, includeKnownLayout: true);
            var converter = new FrontedLayoutPackageLegacyConverter(
                builtInRoot,
                Path.Combine(root, "temp"));

            var result = await converter.ConvertAsync(new FrontedLayoutPackageLegacyConvertRequest
            {
                LegacyPackagePath = legacyArchive,
                PackageId = "converted.legacy.config",
                Name = "Converted"
            }, TestContext.Current.CancellationToken);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal("sentinel-current-config", File.ReadAllText(configPath));
        }
        finally
        {
            if (hadExisting)
            {
                File.Copy(backupPath, configPath, overwrite: true);
                File.Delete(backupPath);
            }
            else if (File.Exists(configPath))
            {
                File.Delete(configPath);
            }

            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public void LegacyConverterAppliesZipSlipSafetyChecks()
    {
        var source = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf.Core",
            "Services",
            "FrontedLayout",
            "FrontedLayoutPackageLegacyConverter.cs"));

        Assert.Contains("Path.IsPathRooted(entryName)", source);
        Assert.Contains("segment is \".\" or \"..\"", source);
        Assert.Contains("Zip entry escaped staging directory", source);
        Assert.Contains("Unsafe zip entry", source);
    }

    [Fact]
    public async Task LegacyConverterRejectsUnsafePackageId()
    {
        var root = CreateTempDirectory();
        try
        {
            var archivePath = Path.Combine(root, "legacy.bpui");
            CreateLegacyBpuiArchive(archivePath, includeConfig: true, includeResource: false, includeKnownLayout: false);
            var converter = new FrontedLayoutPackageLegacyConverter(
                Path.Combine(root, "builtIn"),
                Path.Combine(root, "temp"));

            var result = await converter.ConvertAsync(new FrontedLayoutPackageLegacyConvertRequest
            {
                LegacyPackagePath = archivePath,
                PackageId = "../evil",
                Name = "Converted"
            }, TestContext.Current.CancellationToken);

            Assert.False(result.Success);
            Assert.Contains("PackageId", result.ErrorMessage);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task ImportRejectsCrossPackageLocalAndMissingResourceReferences()
    {
        var root = CreateTempDirectory();
        try
        {
            var importer = new FrontedLayoutPackageImporter(Path.Combine(root, "packages"), Path.Combine(root, "temp"));

            var crossPackage = Path.Combine(root, "cross.bpui");
            CreateBpuiArchive(crossPackage, "package-a", "bpui://package-b/resources/images/bg.png");
            var crossResult = await importer.ImportAsync(new FrontedLayoutPackageImportRequest
            {
                PackagePath = crossPackage
            }, TestContext.Current.CancellationToken);
            Assert.False(crossResult.Success);
            Assert.Contains("Cross-package", crossResult.ErrorMessage);

            var localPackage = Path.Combine(root, "local.bpui");
            CreateBpuiArchive(localPackage, "package-a", "bpui://local/resources/images/bg.png");
            var localResult = await importer.ImportAsync(new FrontedLayoutPackageImportRequest
            {
                PackagePath = localPackage
            }, TestContext.Current.CancellationToken);
            Assert.False(localResult.Success);
            Assert.Contains("bpui://local", localResult.ErrorMessage);

            var missingPackage = Path.Combine(root, "missing.bpui");
            CreateBpuiArchive(missingPackage, "package-a", "bpui://package-a/resources/images/missing.png", includeResource: false);
            var missingResult = await importer.ImportAsync(new FrontedLayoutPackageImportRequest
            {
                PackagePath = missingPackage
            }, TestContext.Current.CancellationToken);
            Assert.False(missingResult.Success);
            Assert.Contains("Missing package resource", missingResult.ErrorMessage);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task ExistingPackageRequiresReplaceAndFailedReplaceKeepsOldPackage()
    {
        var root = CreateTempDirectory();
        try
        {
            var packageRoot = Path.Combine(root, "packages");
            var archivePath = Path.Combine(root, "package.bpui");
            CreateBpuiArchive(archivePath, "package-a", "bpui://package-a/resources/images/bg.png");
            var importer = new FrontedLayoutPackageImporter(packageRoot, Path.Combine(root, "temp"));

            var first = await importer.ImportAsync(new FrontedLayoutPackageImportRequest
            {
                PackagePath = archivePath
            }, TestContext.Current.CancellationToken);
            Assert.True(first.Success, first.ErrorMessage);

            var duplicate = await importer.ImportAsync(new FrontedLayoutPackageImportRequest
            {
                PackagePath = archivePath
            }, TestContext.Current.CancellationToken);
            Assert.False(duplicate.Success);
            Assert.True(duplicate.PackageAlreadyExists);

            var oldMarker = Path.Combine(packageRoot, "package-a", "old.txt");
            File.WriteAllText(oldMarker, "old");
            var invalidReplacement = Path.Combine(root, "invalid.bpui");
            CreateBpuiArchive(invalidReplacement, "package-a", "bpui://package-b/resources/images/bg.png");
            var failedReplace = await importer.ImportAsync(new FrontedLayoutPackageImportRequest
            {
                PackagePath = invalidReplacement,
                ReplaceExisting = true
            }, TestContext.Current.CancellationToken);
            Assert.False(failedReplace.Success);
            Assert.True(File.Exists(oldMarker));
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task DeleteInstalledPackageDeletesOnlyPackageDirectory()
    {
        var root = CreateTempDirectory();
        try
        {
            var packageRoot = Path.Combine(root, "packages");
            var packageFolder = Path.Combine(packageRoot, "delete-me");
            WriteManifest(packageFolder, new
            {
                PackageId = "delete-me",
                Name = "Delete Me"
            });
            var siblingFolder = Path.Combine(packageRoot, "keep-me");
            Directory.CreateDirectory(siblingFolder);
            var manager = new FrontedLayoutPackageManager(packageRoot, Path.Combine(root, "builtIn"));

            await manager.DeletePackageAsync("delete-me", TestContext.Current.CancellationToken);

            Assert.False(Directory.Exists(packageFolder));
            Assert.True(Directory.Exists(siblingFolder));
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task ActivateBuiltinClearsActiveStateAndInstalledPackageWritesState()
    {
        var root = CreateTempDirectory();
        try
        {
            var packageRoot = Path.Combine(root, "packages");
            var packageFolder = Path.Combine(packageRoot, "package-a");
            WriteManifest(packageFolder, new
            {
                PackageId = "package-a",
                Name = "Package A"
            });
            var layoutPath = Path.Combine(packageFolder, "layouts", "BpWindow", "BaseCanvas.json");
            Directory.CreateDirectory(Path.GetDirectoryName(layoutPath)!);
            File.WriteAllText(layoutPath, """{"Version":3,"CanvasWidth":100,"CanvasHeight":100}""");
            var manager = new FrontedLayoutPackageManager(packageRoot, Path.Combine(root, "builtIn"));

            await manager.ActivatePackageAsync("package-a", TestContext.Current.CancellationToken);
            var active = await manager.GetActivePackageStateAsync(TestContext.Current.CancellationToken);
            Assert.Equal("package-a", active.PackageId);

            await manager.ActivatePackageAsync("builtin", TestContext.Current.CancellationToken);
            active = await manager.GetActivePackageStateAsync(TestContext.Current.CancellationToken);
            Assert.Equal("builtin", active.PackageId);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public void FrontedDesignerWindowXamlKeepsSingleDeleteAndShortcutHints()
    {
        var text = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Windows",
            "FrontedDesignerWindow.xaml"));

        Assert.Contains("InputGestureText=\"Ctrl+Z\"", text);
        Assert.Contains("InputGestureText=\"Ctrl+Y / Ctrl+Shift+Z\"", text);
        Assert.Contains("InputGestureText=\"Del\"", text);
        Assert.Contains("InputGestureText=\"Ctrl+S\"", text);
        Assert.Contains("ToolTip=\"{lex:Loc ShortcutUndo}\"", text);
        Assert.Contains("ToolTip=\"{lex:Loc ShortcutRedo}\"", text);
        Assert.Contains("ToolTip=\"{lex:Loc ShortcutSave}\"", text);
        Assert.Contains("LayerControlDeleteMenuItem_OnClick", text);
        Assert.Contains("ItemsSource=\"{Binding LayerGroups}\"", text);
        Assert.Contains("Command=\"{Binding DeleteSelectedControlCommand}\"", text);
        Assert.Contains("Header=\"{lex:Loc DeleteControl}\"", text);
        Assert.Contains("Content=\"{lex:Loc AllowTransparency}\"", text);
        Assert.DoesNotContain("Header=\"{lex:Loc AllowTransparency}\"", text);
        Assert.DoesNotContain("Header=\"{lex:Loc Window}\"", text);
        Assert.DoesNotContain("Grid.Row=\"5\"", text);
    }

    [Fact]
    public void FrontManagePageHasTabsAndLayoutPackageCommands()
    {
        var text = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Pages",
            "FrontManagePage.xaml"));

        Assert.Contains("Header=\"{lex:Loc FrontendWindows}\"", text);
        Assert.DoesNotContain("Header=\"{lex:Loc FrontendDesigner}\"", text);
        Assert.Contains("Header=\"{lex:Loc LayoutPackages}\"", text);
        Assert.Contains("ItemsSource=\"{Binding LayoutPackages}\"", text);
        Assert.Contains("OpenFrontedDesignerCommand", text);
        Assert.Contains("RefreshPackagesCommand", text);
        Assert.Contains("CompactPackageList", text);
        Assert.Contains("PackageBasicInfo", text);
        Assert.Contains("ExportPackageCommand", text);
        Assert.Contains("<ui:DynamicScrollViewer", text);
        Assert.DoesNotContain("<ui:DataGrid", text);
    }

    [Fact]
    public async Task ActivatingPackageCopiesLayoutsAndBuiltinClearsUserLayouts()
    {
        var root = CreateTempDirectory();
        try
        {
            var packageRoot = Path.Combine(root, "packages");
            var userRoot = Path.Combine(root, "userLayouts");
            var packageFolder = Path.Combine(packageRoot, "package-a");
            WriteManifest(packageFolder, new
            {
                PackageId = "package-a",
                Name = "Package A"
            });
            var layoutPath = Path.Combine(packageFolder, "layouts", "BpWindow", "BaseCanvas.json");
            Directory.CreateDirectory(Path.GetDirectoryName(layoutPath)!);
            File.WriteAllText(layoutPath, """{"Version":3,"CanvasWidth":100,"CanvasHeight":100}""");
            File.WriteAllText(Path.Combine(packageFolder, "layouts", "BpWindow", "window.json"), """{"Version":3,"AllowTransparency":true}""");
            var manager = new FrontedLayoutPackageManager(packageRoot, Path.Combine(root, "builtIn"), userRoot);

            await manager.ActivatePackageAsync("package-a", TestContext.Current.CancellationToken);

            Assert.True(File.Exists(Path.Combine(userRoot, "BpWindow", "BaseCanvas.json")));
            Assert.True(File.Exists(Path.Combine(userRoot, "BpWindow", "window.json")));
            var active = await manager.GetActivePackageStateAsync(TestContext.Current.CancellationToken);
            Assert.Equal("package-a", active.PackageId);

            await manager.ActivatePackageAsync("builtin", TestContext.Current.CancellationToken);

            Assert.False(Directory.Exists(userRoot));
            active = await manager.GetActivePackageStateAsync(TestContext.Current.CancellationToken);
            Assert.Equal("builtin", active.PackageId);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task DeletingActivePackageSwitchesBuiltinAndDoesNotDeleteSiblings()
    {
        var root = CreateTempDirectory();
        try
        {
            var packageRoot = Path.Combine(root, "packages");
            var userRoot = Path.Combine(root, "userLayouts");
            foreach (var id in new[] { "package-a", "package-b" })
            {
                var folder = Path.Combine(packageRoot, id);
                WriteManifest(folder, new { PackageId = id, Name = id });
                var layoutPath = Path.Combine(folder, "layouts", "BpWindow", "BaseCanvas.json");
                Directory.CreateDirectory(Path.GetDirectoryName(layoutPath)!);
                File.WriteAllText(layoutPath, """{"Version":3,"CanvasWidth":100,"CanvasHeight":100}""");
            }

            var manager = new FrontedLayoutPackageManager(packageRoot, Path.Combine(root, "builtIn"), userRoot);
            await manager.ActivatePackageAsync("package-a", TestContext.Current.CancellationToken);

            await manager.DeletePackageAsync("package-a", TestContext.Current.CancellationToken);

            Assert.False(Directory.Exists(Path.Combine(packageRoot, "package-a")));
            Assert.True(Directory.Exists(Path.Combine(packageRoot, "package-b")));
            Assert.False(Directory.Exists(userRoot));
            var active = await manager.GetActivePackageStateAsync(TestContext.Current.CancellationToken);
            Assert.Equal("builtin", active.PackageId);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public void FrontManagePageViewModelExposesPackageListAndRefreshCommand()
    {
        var text = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "ViewModels",
            "Pages",
            "FrontManagePageViewModel.cs"));

        Assert.Contains("ObservableCollection<FrontedLayoutPackageInfo> LayoutPackages", text);
        Assert.Contains("SelectedPackage", text);
        Assert.Contains("ActivePackageDisplay", text);
        Assert.Contains("RefreshPackagesAsync", text);
        Assert.Contains("OpenFrontedDesigner", text);
    }

    private static void WriteManifest(string folder, object manifest)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            Path.Combine(folder, "manifest.json"),
            JsonSerializer.Serialize(manifest));
    }

    private static void WriteCatalogLayouts(
        FrontedDesignerLayoutCatalog catalog,
        string builtInRoot,
        string firstBackgroundImage,
        string secondBackgroundImage,
        string thirdBackgroundImage)
    {
        var index = 0;
        foreach (var entry in catalog.GetEntries())
        {
            var background = index switch
            {
                0 => firstBackgroundImage,
                1 => secondBackgroundImage,
                2 => thirdBackgroundImage,
                _ => "Resources/foo.png"
            };
            var path = Path.Combine(builtInRoot, entry.WindowTypeName, $"{entry.CanvasName}.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, $$"""
                                      {
                                        "Version": 3,
                                        "CanvasWidth": 100,
                                        "CanvasHeight": 100,
                                        "BackgroundImage": "{{JsonEncodedText(background)}}",
                                        "Image1": {
                                          "ControlType": "Image",
                                          "Left": 0,
                                          "Top": 0,
                                          "Width": 10,
                                          "Height": 10,
                                          "BanLockImagePath": "Resources/lock.png"
                                        }
                                      }
                                      """);
            index++;
        }
    }

    private static void CreateBpuiArchive(
        string archivePath,
        string packageId,
        string backgroundImage,
        bool includeResource = true)
    {
        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }

        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        WriteZipEntry(archive, "manifest.json", JsonSerializer.Serialize(new FrontedLayoutPackageManifest
        {
            PackageId = packageId,
            Name = packageId,
            MinVersion = "0.0.1",
            Content = new FrontedLayoutPackageManifestContent
            {
                Layouts =
                [
                    new FrontedLayoutPackageLayoutEntry
                    {
                        Window = "BpWindow",
                        Canvas = "BaseCanvas",
                        Path = "layouts/BpWindow/BaseCanvas.json"
                    }
                ],
                Resources = includeResource
                    ?
                    [
                        new FrontedLayoutPackageResourceEntry
                        {
                            Id = "bg",
                            Kind = "Image",
                            Path = "resources/images/bg.png",
                            Uri = $"bpui://{packageId}/resources/images/bg.png"
                        }
                    ]
                    : []
            }
        }));
        WriteZipEntry(
            archive,
            "layouts/BpWindow/BaseCanvas.json",
            $$"""
              {
                "Version": 3,
                "CanvasWidth": 100,
                "CanvasHeight": 100,
                "BackgroundImage": "{{JsonEncodedText(backgroundImage)}}"
              }
              """);
        if (includeResource)
        {
            var resource = archive.CreateEntry("resources/images/bg.png");
            using var stream = resource.Open();
            stream.Write(TinyPngBytes);
        }
    }

    private static void CreateLegacyBpuiArchive(
        string archivePath,
        bool includeConfig,
        bool includeResource,
        bool includeKnownLayout,
        bool includeUnknownLayout = false)
    {
        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }

        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        if (includeConfig)
        {
            WriteZipEntry(
                archive,
                "Config.json",
                """
                {
                  "ScoreWindowSettings": {
                    "SurScoreBgImageUri": "C:\\legacy\\bg.png"
                  }
                }
                """);
        }

        if (includeResource)
        {
            var resource = archive.CreateEntry("CustomUi/bg.png");
            using var stream = resource.Open();
            stream.Write(TinyPngBytes);
        }

        if (includeKnownLayout)
        {
            WriteZipEntry(
                archive,
                "FrontElementsConfig/ScoreSurWindowConfig-BaseCanvas.json",
                """
                {
                  "SurTeamName": {
                    "Width": 33,
                    "Height": 44,
                    "Left": 11,
                    "Top": 22
                  },
                  "LegacyOnly": {
                    "Width": 1,
                    "Height": 2,
                    "Left": 3,
                    "Top": 4
                  }
                }
                """);
        }

        if (includeUnknownLayout)
        {
            WriteZipEntry(
                archive,
                "FrontElementsConfig/UnknownWindowConfig-BaseCanvas.json",
                "{}");
        }
    }

    private static void WriteBuiltInLayoutForLegacyConversion(string builtInRoot)
    {
        var layoutPath = Path.Combine(builtInRoot, "ScoreSurWindow", "BaseCanvas.json");
        Directory.CreateDirectory(Path.GetDirectoryName(layoutPath)!);
        File.WriteAllText(
            layoutPath,
            """
            {
              "Version": 3,
              "CanvasWidth": 1440,
              "CanvasHeight": 810,
              "BackgroundImage": "Resources/bp.png",
              "SurTeamName": {
                "ControlType": "Text",
                "Left": 1,
                "Top": 2,
                "Width": 3,
                "Height": 4,
                "Text": "Team"
              }
            }
            """);
    }

    private static void WriteZipEntry(ZipArchive archive, string entryName, string text)
    {
        var entry = archive.CreateEntry(entryName);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write(text);
    }

    private static string JsonEncodedText(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static byte[] TinyPngBytes =>
        Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");

    private static void WriteTinyPng(string path)
    {
        File.WriteAllBytes(path, TinyPngBytes);
    }

    private static FrontedLayoutPackageManifest ReadManifest(ZipArchive archive)
    {
        var json = ReadZipEntry(archive, "manifest.json");
        return JsonSerializer.Deserialize<FrontedLayoutPackageManifest>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;
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
        var path = Path.Combine(
            Path.GetTempPath(),
            "neo-bpsys-wpf-tests",
            Guid.NewGuid().ToString("N"));
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

    private static string GetRepositoryPath(
        string first,
        string second,
        string third,
        string? fourth = null,
        [CallerFilePath] string sourceFilePath = "")
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "..", ".."));
        return fourth is null
            ? Path.Combine(repositoryRoot, first, second, third)
            : Path.Combine(repositoryRoot, first, second, third, fourth);
    }

    private sealed class FakeFilePickerService(string? bpuiSavePath) : IFilePickerService
    {
        public int SaveBpuiFileCallCount { get; private set; }

        public string? LastDefaultFileName { get; private set; }

        public string? PickBpuiFile() => null;

        public string? PickImage() => null;

        public string? PickJsonFile() => null;

        public string? PickZipFile() => null;

        public string? SaveJsonFile(string defaultFileName) => null;

        public string? SaveBpuiFile(string defaultFileName)
        {
            SaveBpuiFileCallCount++;
            LastDefaultFileName = defaultFileName;
            return bpuiSavePath;
        }
    }
}
