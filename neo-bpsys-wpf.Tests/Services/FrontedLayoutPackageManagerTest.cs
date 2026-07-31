#nullable enable

using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Services.Registry;
using neo_bpsys_wpf.Services.Abstractions;
using neo_bpsys_wpf.ViewModels.Pages;
using neo_bpsys_wpf.ViewModels.Windows;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public class FrontedLayoutPackageManagerTest : IDisposable
{
    private readonly Func<string, string>? _previousLocalizeTemplate;

    public FrontedLayoutPackageManagerTest()
    {
        _previousLocalizeTemplate = LegacyConvertMessageHelper.LocalizeTemplate;
        LegacyConvertMessageHelper.LocalizeTemplate = null;
    }

    public void Dispose()
    {
        LegacyConvertMessageHelper.LocalizeTemplate = _previousLocalizeTemplate;
    }

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
            Assert.True(builtIn.IsActivePackage);
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
                    Layouts = new[] { new { Window = "BpWindow", Path = "FrontedLayouts/BpWindow.json" } },
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
    public void PackageInfoUsesExplicitActivePackagePropertyName()
    {
        var properties = typeof(FrontedLayoutPackageInfo)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("IsActive", properties);
        Assert.Contains("IsActivePackage", properties);
    }

    [Fact]
    public void FrontedLayoutPackagesViewBindsActiveBadgeToExplicitPackageProperty()
    {
        var xaml = File.ReadAllText(Path.Combine(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Pages"),
            "FrontManage",
            "FrontedLayoutPackagesView.xaml"));

        Assert.DoesNotMatch("Visibility=\"\\{Binding\\s+IsActive\\b", xaml);
        Assert.Contains("Visibility=\"{Binding IsActivePackage", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ForbiddenLayoutAndPackageSurfacesDoNotUseGenericIsActive()
    {
        var forbiddenRoots = new[]
        {
            GetRepositoryPath("neo-bpsys-wpf.Core", "Models", "FrontedLayout"),
            GetRepositoryPath("neo-bpsys-wpf.Core", "Models", "Legacy"),
            Path.Combine(GetRepositoryPath("neo-bpsys-wpf", "Views", "Pages"), "FrontManage")
        };
        var repositoryRoot = Path.GetFullPath(Path.Combine(forbiddenRoots[0], "..", "..", ".."));
        var forbiddenToken = new Regex(@"(?<![A-Za-z0-9_])IsActive(?![A-Za-z0-9_])", RegexOptions.CultureInvariant);
        var offenders = forbiddenRoots
            .Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                           || path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new { path, line, lineNumber = index + 1 }))
            .Where(item => forbiddenToken.IsMatch(item.line))
            .Select(item => $"{Path.GetRelativePath(repositoryRoot, item.path)}:{item.lineNumber}: {item.line.Trim()}")
            .ToArray();

        Assert.Empty(offenders);
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
            var localFontPath = Path.Combine(packageRoot, "local", "resources", "fonts", "NotoSans-Regular.ttf");
            var absoluteImagePath = Path.Combine(root, "absolute.png");
            Directory.CreateDirectory(Path.GetDirectoryName(localImagePath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(localFontPath)!);
            WriteTinyPng(localImagePath);
            File.Copy(GetRepositoryPath("neo-bpsys-wpf", "Assets", "Fonts", "NotoSans-Regular.ttf"), localFontPath);
            WriteTinyPng(absoluteImagePath);
            File.AppendAllBytes(absoluteImagePath, [0]);

            var catalog = new FrontedDesignerLayoutCatalog(CreateBuiltInV3Registry());
            WriteCatalogLayouts(
                catalog,
                builtInRoot,
                "Resources/foo.png",
                "bpui://local/resources/images/local.png",
                absoluteImagePath,
                "bpui://local/resources/fonts/NotoSans-Regular.ttf#Noto Sans");
            var packageManager = new FrontedLayoutPackageManager(
                packageRoot,
                builtInRoot,
                logger: NullLogger<FrontedLayoutPackageManager>.Instance);
            var exporter = new FrontedLayoutPackageExporter(
                packageManager,
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
            Assert.Contains("FrontedLayouts/ScoreSurWindow.json", entryNames);
            Assert.DoesNotContain("Config.json", entryNames);
            Assert.DoesNotContain(entryNames, name => name.StartsWith("CustomUi/", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(entryNames, name => name.StartsWith("FrontElementsConfig/", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(entryNames, name => Path.IsPathRooted(name) || name.Contains("..", StringComparison.Ordinal));
            Assert.Contains(entryNames, name => name.StartsWith("resources/images/local-", StringComparison.Ordinal));
            Assert.Contains(entryNames, name => name.StartsWith("resources/images/absolute-", StringComparison.Ordinal));

            var manifest = ReadManifest(archive);
            Assert.Equal(result.LayoutCount, manifest.Content.Layouts.Count);
            Assert.Equal(result.ResourceCount, manifest.Content.Resources.Count);
            Assert.Equal(3, manifest.Content.Resources.Count);
            Assert.All(manifest.Content.Resources, resource => Assert.False(string.IsNullOrWhiteSpace(resource.Sha256)));

            var builtInLayoutJson = ReadZipEntry(archive, "FrontedLayouts/ScoreSurWindow.json");
            var localLayoutJson = ReadZipEntry(archive, "FrontedLayouts/ScoreHunWindow.json");
            Assert.Contains("bpui://plfjy.default-layout.2026/resources/images/local-", localLayoutJson);
            Assert.Contains("bpui://plfjy.default-layout.2026/resources/fonts/NotoSans-Regular-", localLayoutJson);
            Assert.Contains("#Noto Sans", localLayoutJson);
            var absoluteLayoutJson = ReadZipEntry(archive, "FrontedLayouts/ScoreGlobalWindow.json");
            Assert.Contains("bpui://plfjy.default-layout.2026/resources/images/absolute-", absoluteLayoutJson);
            Assert.Contains(entryNames, name => name.StartsWith("resources/fonts/NotoSans-Regular-", StringComparison.Ordinal));
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
            var catalog = new FrontedDesignerLayoutCatalog(CreateBuiltInV3Registry());
            WriteCatalogLayouts(catalog, builtInRoot, missingPath, "Resources/foo.png", "Resources/bar.png");
            var packageManager = new FrontedLayoutPackageManager(
                Path.Combine(root, "packages"),
                builtInRoot,
                logger: NullLogger<FrontedLayoutPackageManager>.Instance);
            var exporter = new FrontedLayoutPackageExporter(
                packageManager,
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
            Assert.Contains(result.Infos, info => info.Contains("ResourceCopied", StringComparison.Ordinal));
            Assert.DoesNotContain(result.Warnings, warning => warning.Contains("Legacy resource copied", StringComparison.Ordinal));
            Assert.Contains(result.Warnings, warning => warning.Contains("UnknownLayoutFileSkipped", StringComparison.Ordinal));

            using var archive = ZipFile.OpenRead(result.ConvertedPackagePath!);
            var entryNames = archive.Entries.Select(entry => entry.FullName.Replace('\\', '/')).ToArray();
            Assert.Contains("manifest.json", entryNames);
            Assert.Contains("FrontedLayouts/ScoreSurWindow.json", entryNames);
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
            Assert.Equal("WindowCentric", manifestRoot.GetProperty("LayoutModel").GetString());
            foreach (var layoutEntry in manifestRoot.GetProperty("Content").GetProperty("Layouts").EnumerateArray())
            {
                var window = layoutEntry.GetProperty("Window").GetString();
                Assert.Equal($"FrontedLayouts/{window}.json", layoutEntry.GetProperty("Path").GetString());
                Assert.False(layoutEntry.TryGetProperty("Canvas", out _));
            }

            var layoutJson = ReadZipEntry(archive, "FrontedLayouts/ScoreSurWindow.json");
            var layout = JsonSerializer.Deserialize<FrontedWindowConfig>(layoutJson)!;
            Assert.Equal(3, layout.Version);
            var control = layout.ControlLayout.Controls["SurTeamName"];
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task LegacyConverterMapsScoreGlobalAliasesAndAggregatesCells()
    {
        var root = CreateTempDirectory();
        try
        {
            var builtInRoot = Path.Combine(root, "builtIn");
            WriteBuiltInScoreGlobalLayoutForLegacyConversion(builtInRoot);
            var legacyArchive = Path.Combine(root, "legacy-score-global.bpui");
            CreateLegacyScoreGlobalBpuiArchive(legacyArchive);
            var converter = new FrontedLayoutPackageLegacyConverter(
                builtInRoot,
                Path.Combine(root, "temp"));

            var result = await converter.ConvertAsync(new FrontedLayoutPackageLegacyConvertRequest
            {
                LegacyPackagePath = legacyArchive,
                PackageId = "converted.legacy.score-global",
                Name = "score global"
            }, TestContext.Current.CancellationToken);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Empty(result.Warnings);
            Assert.Contains(result.Infos, info => info.Contains("ResourceCopied", StringComparison.Ordinal));
            Assert.Contains(result.Infos, info => info.Contains("GlobalScoreCellsAggregated", StringComparison.Ordinal)
                && info.Contains("Team=Home", StringComparison.Ordinal));
            Assert.Contains(result.Infos, info => info.Contains("GlobalScoreCellsAggregated", StringComparison.Ordinal)
                && info.Contains("Team=Away", StringComparison.Ordinal));

            using var archive = ZipFile.OpenRead(result.ConvertedPackagePath!);
            var layoutJson = ReadZipEntry(archive, "FrontedLayouts/ScoreGlobalWindow.json");
            var layout = JsonSerializer.Deserialize<FrontedWindowConfig>(layoutJson)!.ToCanvasConfig();

            var homeName = layout.Controls["HomeTeamName"];
            var homeTotal = layout.Controls["HomeScoreTotal"];
            var awayName = layout.Controls["AwayTeamName"];

            var homeRow = Assert.IsType<neo_bpsys_wpf.Core.Models.FrontedLayout.GlobalScoreRowControlConfig>(
                layout.Controls["HomeGlobalScoreRow"]);
            Assert.Contains(homeRow.Cells, cell => cell.Id == "Game1FirstHalf" && cell.X == 0 && cell.Y == 0);
            Assert.Contains(homeRow.Cells, cell => cell.Id == "Game1SecondHalf" && cell.X == 90 && cell.Y == 0);

            var awayRow = Assert.IsType<neo_bpsys_wpf.Core.Models.FrontedLayout.GlobalScoreRowControlConfig>(
                layout.Controls["AwayGlobalScoreRow"]);
            Assert.Contains(awayRow.Cells, cell => cell.Id == "Game1FirstHalf" && cell.X == 0 && cell.Y == 0);
            Assert.Contains(awayRow.Cells, cell => cell.Id == "Game1SecondHalf" && cell.X == 90 && cell.Y == 0);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task LegacyConverterMapsMinorPointsAliasesAndCutSceneMapMask()
    {
        var root = CreateTempDirectory();
        try
        {
            var legacyArchive = Path.Combine(root, "legacy-minor-points.bpui");
            CreateLegacyMinorPointsAliasBpuiArchive(legacyArchive);
            var converter = new FrontedLayoutPackageLegacyConverter(
                Path.Combine(root, "builtIn"),
                Path.Combine(root, "temp"));

            var result = await converter.ConvertAsync(new FrontedLayoutPackageLegacyConvertRequest
            {
                LegacyPackagePath = legacyArchive,
                PackageId = "converted.legacy.minor-points-aliases",
                Name = "minor-points aliases"
            }, TestContext.Current.CancellationToken);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(3, result.LayoutCount);
            using var archive = ZipFile.OpenRead(result.ConvertedPackagePath!);
            var cutSceneLayout = JsonSerializer.Deserialize<FrontedWindowConfig>(
                ReadZipEntry(archive, "FrontedLayouts/CutSceneWindow.json"))!
                .ToCanvasConfig();
            var mapMask = Assert.IsType<RectangleFrontedControlConfig>(cutSceneLayout.Controls["MapMask"]);
            Assert.Equal("#FF000000", mapMask.FillColor);
            Assert.Contains(result.Infos, info => info.Contains("ControlGeometryFuzzyMatched", StringComparison.Ordinal)
                && info.Contains("BpWindow", StringComparison.Ordinal)
                && info.Contains("MinorPointsSur", StringComparison.Ordinal));
            Assert.Contains(result.Infos, info => info.Contains("ControlGeometryFuzzyMatched", StringComparison.Ordinal)
                && info.Contains("GameDataWindow", StringComparison.Ordinal)
                && info.Contains("MinorPointsHun", StringComparison.Ordinal));
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
            Assert.False(Directory.Exists(userLayoutRoot));
            Assert.True(File.Exists(Path.Combine(packageRoot, "converted.legacy.test", "FrontedLayouts", "ScoreSurWindow.json")));
            var active = await manager.GetActivePackageStateAsync(TestContext.Current.CancellationToken);
            Assert.Equal("converted.legacy.test", active.PackageId);

            var manifest = ReadManifestFromPath(Path.Combine(packageRoot, "converted.legacy.test", "manifest.json"));
            var layoutService = new FrontedLayoutService(
                manager,
                NullLogger<FrontedLayoutService>.Instance);
            foreach (var layout in manifest.Content.Layouts)
            {
                Assert.Equal($"FrontedLayouts/{layout.Window}.json", layout.Path);
                var json = File.ReadAllText(Path.Combine(packageRoot, "converted.legacy.test", layout.Path.Replace('/', Path.DirectorySeparatorChar)));
                Assert.NotNull(JsonSerializer.Deserialize<FrontedWindowConfig>(json));
                var loaded = await layoutService.LoadWindowConfigAsync(layout.Window, TestContext.Current.CancellationToken);
                Assert.NotNull(loaded);
            }
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
    public async Task RenameInstalledPackageUpdatesDisplayNameWithoutChangingStableIdentity()
    {
        var root = CreateTempDirectory();
        try
        {
            var packageRoot = Path.Combine(root, "packages");
            var packageFolder = Path.Combine(packageRoot, "package-a");
            WriteManifest(packageFolder, new
            {
                PackageId = "package-a",
                Name = "Original Name",
                CustomMetadata = "preserve-me"
            });
            var manager = new FrontedLayoutPackageManager(packageRoot, Path.Combine(root, "builtIn"));

            await manager.ActivatePackageAsync("package-a", TestContext.Current.CancellationToken);
            var renamed = await manager.RenamePackageAsync("package-a", "  Updated Name  ", TestContext.Current.CancellationToken);
            var descriptionUpdated = await manager.UpdatePackageDescriptionAsync(
                "package-a",
                "  Updated description  ",
                TestContext.Current.CancellationToken);

            Assert.Equal("package-a", renamed.PackageId);
            Assert.Equal("Updated Name", renamed.Name);
            Assert.Equal("Updated description", descriptionUpdated.Description);
            var active = await manager.GetActivePackageStateAsync(TestContext.Current.CancellationToken);
            Assert.Equal("package-a", active.PackageId);
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(packageFolder, "manifest.json")));
            Assert.Equal("package-a", manifest.RootElement.GetProperty("PackageId").GetString());
            Assert.Equal("Updated Name", manifest.RootElement.GetProperty("Name").GetString());
            Assert.Equal("Updated description", manifest.RootElement.GetProperty("Description").GetString());
            Assert.Equal("preserve-me", manifest.RootElement.GetProperty("CustomMetadata").GetString());
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task RenamePackageRejectsReservedPackagesAndBlankNames()
    {
        var root = CreateTempDirectory();
        try
        {
            var packageRoot = Path.Combine(root, "packages");
            WriteManifest(Path.Combine(packageRoot, "package-a"), new { PackageId = "package-a", Name = "Package A" });
            var manager = new FrontedLayoutPackageManager(packageRoot, Path.Combine(root, "builtIn"));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                manager.RenamePackageAsync("builtin", "Built-in", TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                manager.RenamePackageAsync("local", "Local", TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentException>(() =>
                manager.RenamePackageAsync("package-a", "   ", TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                manager.UpdatePackageDescriptionAsync("builtin", "Built-in", TestContext.Current.CancellationToken));
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
    public async Task ExportingSelectedSourcePackageDoesNotRequireActivatingIt()
    {
        var root = CreateTempDirectory();
        try
        {
            var packageRoot = Path.Combine(root, "packages");
            var activeFolder = Path.Combine(packageRoot, "package-a");
            var sourceFolder = Path.Combine(packageRoot, "package-b");
            WriteManifest(activeFolder, new { PackageId = "package-a", Name = "Package A" });
            WriteManifest(sourceFolder, new { PackageId = "package-b", Name = "Package B" });
            WriteMinimalV3Layout(Path.Combine(activeFolder, "FrontedLayouts", "BpWindow.json"), "Active package");
            WriteMinimalV3Layout(Path.Combine(sourceFolder, "FrontedLayouts", "BpWindow.json"), "Selected package");
            var manager = new FrontedLayoutPackageManager(packageRoot, Path.Combine(root, "builtIn"));
            await manager.ActivatePackageAsync("package-a", TestContext.Current.CancellationToken);
            var exporter = new FrontedLayoutPackageExporter(
                manager,
                packageRoot,
                Path.Combine(root, "temp"));
            var outputPath = Path.Combine(root, "selected.bpui");

            var result = await exporter.ExportAsync(new FrontedLayoutPackageExportRequest
            {
                PackageId = "exported-package",
                Name = "Selected Package",
                SourcePackageId = "package-b",
                OutputPath = outputPath
            }, TestContext.Current.CancellationToken);

            Assert.True(result.Success, result.ErrorMessage);
            using var archive = ZipFile.OpenRead(outputPath);
            Assert.Contains("Selected package", ReadZipEntry(archive, "FrontedLayouts/BpWindow.json"));
            var active = await manager.GetActivePackageStateAsync(TestContext.Current.CancellationToken);
            Assert.Equal("package-a", active.PackageId);
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
    }

    [Fact]
    public void FrontManagePageUsesTopLocalTabsAndLayoutPackageCommands()
    {
        var pageText = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Pages",
            "FrontManagePage.xaml"));
        var pageCode = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Pages",
            "FrontManagePage.xaml.cs"));
        var frontManageFolder = Path.Combine(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Pages"),
            "FrontManage");
        var windowsText = File.ReadAllText(Path.Combine(frontManageFolder, "FrontedWindowsView.xaml"));
        var packagesText = File.ReadAllText(Path.Combine(frontManageFolder, "FrontedLayoutPackagesView.xaml"));

        Assert.Contains("x:Name=\"FrontManageTabs\"", pageText);
        Assert.Contains("NavigationBehavior=\"LocalTabs\"", pageText);
        Assert.Contains("\"FrontendWindows\"", pageCode);
        Assert.Contains("typeof(FrontedWindowsView)", pageCode);
        Assert.Contains("\"LayoutPackages\"", pageCode);
        Assert.Contains("typeof(FrontedLayoutPackagesView)", pageCode);
        Assert.DoesNotContain("Header=\"{lex:Loc FrontendDesigner}\"", windowsText);
        Assert.Contains("OpenFrontedDesignerCommand", windowsText);
        Assert.Contains("ItemsSource=\"{Binding LayoutPackages}\"", packagesText);
        Assert.Contains("RefreshPackagesCommand", packagesText);
        Assert.Contains("CompactPackageList", packagesText);
        Assert.Contains("RequestBringIntoView=\"PackageListBox_OnRequestBringIntoView\"", packagesText);
        Assert.Contains("PackageBasicInfo", packagesText);
        Assert.Contains("ExportPackageCommand", packagesText);
        Assert.Contains("DuplicatePackageCommand", packagesText);
        Assert.Contains("RenamePackageCommand", packagesText);
        Assert.Contains("EditPackageDescriptionCommand", packagesText);
        Assert.Contains("PackageListBox_OnPreviewMouseRightButtonDown", packagesText);
        Assert.Contains("MouseDoubleClick=\"PackageListBox_OnMouseDoubleClick\"", packagesText);

        var packagesCode = File.ReadAllText(Path.Combine(frontManageFolder, "FrontedLayoutPackagesView.xaml.cs"));
        Assert.Contains("PackageListBox_OnRequestBringIntoView", packagesCode);
        Assert.Contains("if (sender == LayoutPackageList)", packagesCode);
        Assert.Contains("e.Handled = true", packagesCode);
        Assert.Contains("PackageListBox_OnPreviewMouseRightButtonDown", packagesCode);
    }

    [Fact]
    public async Task ActivatingPackageDoesNotCopyOrClearGlobalUserLayouts()
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
            var legacyUserLayout = Path.Combine(userRoot, "LegacyWindow", "BaseCanvas.json");
            Directory.CreateDirectory(Path.GetDirectoryName(legacyUserLayout)!);
            File.WriteAllText(legacyUserLayout, "legacy");
            var manager = new FrontedLayoutPackageManager(packageRoot, Path.Combine(root, "builtIn"), userRoot);

            await manager.ActivatePackageAsync("package-a", TestContext.Current.CancellationToken);

            Assert.False(File.Exists(Path.Combine(userRoot, "BpWindow", "BaseCanvas.json")));
            Assert.False(File.Exists(Path.Combine(userRoot, "BpWindow", "window.json")));
            Assert.True(File.Exists(legacyUserLayout));
            var active = await manager.GetActivePackageStateAsync(TestContext.Current.CancellationToken);
            Assert.Equal("package-a", active.PackageId);

            await manager.ActivatePackageAsync("builtin", TestContext.Current.CancellationToken);

            Assert.True(File.Exists(legacyUserLayout));
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
    public async Task SavingWhileBuiltinIsActiveCreatesEditablePackageAndWritesThere()
    {
        var root = CreateTempDirectory();
        try
        {
            var packageRoot = Path.Combine(root, "packages");
            var builtInRoot = Path.Combine(root, "builtIn");
            var builtInLayout = Path.Combine(builtInRoot, "BpWindow.json");
            Directory.CreateDirectory(builtInRoot);
            File.WriteAllText(builtInLayout, """{"Version":3,"CanvasWidth":100,"CanvasHeight":100}""");
            var userRoot = Path.Combine(root, "userLayouts");
            var manager = new FrontedLayoutPackageManager(
                packageRoot,
                builtInRoot,
                userRoot,
                localize: key => key == "UserLayoutSchemeNameFormat" ? "User Layout Scheme {0}" : key);
            var service = new FrontedLayoutService(
                manager,
                NullLogger<FrontedLayoutService>.Instance);

            await service.SaveWindowConfigAsync(
                "BpWindow",
                neo_bpsys_wpf.Core.Services.FrontedLayout.FrontedWindowConfigCanvasAdapter.FromCanvasConfig(new FrontedCanvasConfig { Version = 3, CanvasWidth = 200, CanvasHeight = 120 }),
                TestContext.Current.CancellationToken);

            var active = await manager.GetActivePackageStateAsync(TestContext.Current.CancellationToken);
            Assert.Equal("user-layout-scheme-1", active.PackageId);
            Assert.True(File.Exists(Path.Combine(packageRoot, "user-layout-scheme-1", "FrontedLayouts", "BpWindow.json")));
            Assert.False(File.Exists(Path.Combine(userRoot, "BpWindow.json")));
            var packages = await manager.ListPackagesAsync(TestContext.Current.CancellationToken);
            Assert.Contains(packages, package => package.PackageId == "user-layout-scheme-1" && package.Name == "User Layout Scheme 1");
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task DuplicatePackageCreatesSeparateEditableCopyAndIncrementsGeneratedIdentity()
    {
        var root = CreateTempDirectory();
        try
        {
            var packageRoot = Path.Combine(root, "packages");
            var builtInRoot = Path.Combine(root, "builtIn");
            var firstFolder = Path.Combine(packageRoot, "user-layout-scheme-1");
            WriteManifest(firstFolder, new { PackageId = "user-layout-scheme-1", Name = "User Layout Scheme 1" });
            var sourceFolder = Path.Combine(packageRoot, "package-a");
            WriteManifest(sourceFolder, new { PackageId = "package-a", Name = "Package A" });
            var sourceLayout = Path.Combine(sourceFolder, "FrontedLayouts", "plugin", "ExamplePlugin", "Overlay.json");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceLayout)!);
            File.WriteAllText(sourceLayout, """{"Version":3,"CanvasWidth":100,"CanvasHeight":100}""");
            var manager = new FrontedLayoutPackageManager(
                packageRoot,
                builtInRoot,
                localize: key => key == "UserLayoutSchemeNameFormat" ? "User Layout Scheme {0}" : key);

            var duplicate = await manager.DuplicatePackageAsync("package-a", cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal("user-layout-scheme-2", duplicate.PackageId);
            Assert.Equal("User Layout Scheme 2", duplicate.Name);
            Assert.True(File.Exists(Path.Combine(packageRoot, "user-layout-scheme-2", "FrontedLayouts", "plugin", "ExamplePlugin", "Overlay.json")));
            Assert.True(File.Exists(sourceLayout));
            var active = await manager.GetActivePackageStateAsync(TestContext.Current.CancellationToken);
            Assert.Equal("user-layout-scheme-2", active.PackageId);
            Assert.Throws<InvalidOperationException>(() =>
                manager.GetPackageLayoutPath(FrontedLayoutPackageManager.LocalPackageId, "BpWindow"));
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
        Assert.Contains("ActivateSelectedPackageByDoubleClickAsync", text);
        Assert.Contains("DuplicatePackageAsync", text);
        Assert.Contains("RenamePackageAsync", text);
        Assert.Contains("EditPackageDescriptionAsync", text);
    }

    [Fact]
    public async Task ActivatingPackageKeepsActivatedPackageSelectedAfterRefresh()
    {
        var activePackageId = FrontedLayoutPackageManager.BuiltInPackageId;
        var packages = new[]
        {
            CreatePackage(FrontedLayoutPackageManager.BuiltInPackageId, "Built-in"),
            CreatePackage("package-a", "Package A"),
            CreatePackage("package-b", "Package B")
        };
        var packageManager = new Mock<IFrontedLayoutPackageManager>();
        packageManager
            .Setup(manager => manager.ListPackagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => packages
                .Select(package => CreatePackage(
                    package.PackageId,
                    package.Name,
                    string.Equals(package.PackageId, activePackageId, StringComparison.OrdinalIgnoreCase)))
                .ToArray());
        packageManager
            .Setup(manager => manager.ActivatePackageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((packageId, _) =>
            {
                activePackageId = packageId;
                return Task.CompletedTask;
            });

        var frontedWindowService = new Mock<IFrontedWindowService>();
        frontedWindowService
            .Setup(service => service.ReloadFrontedLayoutsAsync())
            .Returns(Task.CompletedTask);

        var viewModel = new FrontManagePageViewModel(
            frontedWindowService.Object,
            Mock.Of<ISharedDataService>(),
            Mock.Of<IFilePickerService>(),
            packageManager.Object,
            Mock.Of<IFrontedLayoutPackageExporter>(),
            Mock.Of<IFrontedLayoutPackageImporter>(),
            Mock.Of<IFrontedLayoutPackageLegacyConverter>(),
            Mock.Of<IPluginMarketService>(),
            Mock.Of<IPluginInstallService>(),
            Mock.Of<IFrontedWindowRegistry>(),
            Mock.Of<IServiceProvider>(),
            NullLogger<FrontManagePageViewModel>.Instance);

        await viewModel.RefreshPackagesCommand.ExecuteAsync(null);
        viewModel.SelectedPackage = viewModel.LayoutPackages.First(package => package.PackageId == "package-b");

        await viewModel.ActivateSelectedPackageByDoubleClickCommand.ExecuteAsync(null);

        Assert.Equal("package-b", activePackageId);
        Assert.Equal("package-b", viewModel.SelectedPackage?.PackageId);
        Assert.True(viewModel.SelectedPackage?.IsActivePackage);
        Assert.Equal("package-b", viewModel.LayoutPackages.First(package => package.IsActivePackage).PackageId);
    }

    private static FrontedLayoutPackageInfo CreatePackage(
        string packageId,
        string name,
        bool isActive = false)
    {
        return new FrontedLayoutPackageInfo
        {
            PackageId = packageId,
            Name = name,
            Source = string.Equals(packageId, FrontedLayoutPackageManager.BuiltInPackageId, StringComparison.OrdinalIgnoreCase)
                ? FrontedLayoutPackageSource.BuiltIn
                : FrontedLayoutPackageSource.Installed,
            IsBuiltin = string.Equals(packageId, FrontedLayoutPackageManager.BuiltInPackageId, StringComparison.OrdinalIgnoreCase),
            IsActivePackage = isActive,
            InstallPath = packageId
        };
    }

    private static void WriteManifest(string folder, object manifest)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            Path.Combine(folder, "manifest.json"),
            JsonSerializer.Serialize(manifest));
    }

    private static void WriteMinimalV3Layout(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $$"""
                                  {
                                    "Version": 3,
                                    "WindowSettings": {
                                      "WindowWidth": 100,
                                      "WindowHeight": 100
                                    },
                                    "CanvasSettings": {
                                      "CanvasWidth": 100,
                                      "CanvasHeight": 100
                                    },
                                    "ControlLayout": {
                                      "RequiredPlugins": [],
                                      "Controls": {
                                        "Text1": {
                                          "ControlType": "Text",
                                          "Text": "{{JsonEncodedText(text)}}"
                                        }
                                      }
                                    }
                                  }
                                  """);
    }

    private static void WriteCatalogLayouts(
        FrontedDesignerLayoutCatalog catalog,
        string builtInRoot,
        string firstBackgroundImage,
        string secondBackgroundImage,
        string thirdBackgroundImage,
        string fontFamily = "Arial")
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
            var path = Path.Combine(builtInRoot, $"{entry.CanonicalWindowId}.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, $$"""
                                      {
                                        "Version": 3,
                                        "CanvasSettings": {
                                          "CanvasWidth": 100,
                                          "CanvasHeight": 100,
                                          "BackgroundImage": "{{JsonEncodedText(background)}}"
                                        },
                                        "ControlLayout": {
                                          "RequiredPlugins": [],
                                          "Controls": {
                                            "Image1": {
                                              "ControlType": "Image",
                                              "Left": 0,
                                              "Top": 0,
                                              "Width": 10,
                                              "Height": 10,
                                              "BanLockImagePath": "Resources/lock.png"
                                            },
                                            "Text1": {
                                              "ControlType": "Text",
                                              "Left": 0,
                                              "Top": 20,
                                              "Text": "Text",
                                              "FontFamily": "{{JsonEncodedText(fontFamily)}}",
                                              "FontSize": 12
                                            }
                                          }
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
                        Path = "FrontedLayouts/BpWindow.json"
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
            "FrontedLayouts/BpWindow.json",
            $$"""
              {
                "Version": 3,
                "CanvasSettings": {
                  "CanvasWidth": 100,
                  "CanvasHeight": 100,
                  "BackgroundImage": "{{JsonEncodedText(backgroundImage)}}"
                },
                "ControlLayout": {
                  "RequiredPlugins": [],
                  "Controls": {}
                }
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

    private static void CreateLegacyScoreGlobalBpuiArchive(string archivePath)
    {
        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }

        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        WriteZipEntry(
            archive,
            "Config.json",
            """
            {
              "ScoreWindowSettings": {
                "GlobalScoreBgImageUri": "C:\\legacy\\global.png"
              }
            }
            """);

        var resource = archive.CreateEntry("CustomUi/global.png");
        using (var stream = resource.Open())
        {
            stream.Write(TinyPngBytes);
        }

        WriteZipEntry(
            archive,
            "FrontElementsConfig/ScoreGlobalWindowConfig-BaseCanvas.json",
            """
            {
              "MainTeamName": {
                "Width": 30,
                "Height": 40,
                "Left": 10,
                "Top": 20
              },
              "MainScoreTotal": {
                "Left": 1300,
                "Top": 21
              },
              "AwayTeamName": {
                "Left": 12,
                "Top": 150
              },
              "AwayScoreTotal": {
                "Left": 1302,
                "Top": 151
              },
              "HomeTeamGame1FirstHalf": {
                "Left": 180,
                "Top": 90
              },
              "HomeTeamGame1SecondHalf": {
                "Left": 270,
                "Top": 90
              },
              "HomeTeamGame2FirstHalf": {
                "Left": 360,
                "Top": 90
              },
              "HomeTeamGame2SecondHalf": {
                "Left": 450,
                "Top": 90
              },
              "AwayTeamGame1FirstHalf": {
                "Left": 180,
                "Top": 150
              },
              "AwayTeamGame1SecondHalf": {
                "Left": 270,
                "Top": 150
              },
              "AwayTeamGame2FirstHalf": {
                "Left": 360,
                "Top": 150
              },
              "AwayTeamGame2SecondHalf": {
                "Left": 450,
                "Top": 150
              }
            }
            """);
    }

    private static void CreateLegacyMinorPointsAliasBpuiArchive(string archivePath)
    {
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        WriteZipEntry(archive, "Config.json", "{}");
        WriteZipEntry(
            archive,
            "FrontElementsConfig/BpWindowConfig-BaseCanvas.json",
            """
            {
              "MinorPointsSur": {
                "Left": 622,
                "Top": 746
              },
              "MinorPointsHun": {
                "Left": 784,
                "Top": 746
              }
            }
            """);
        WriteZipEntry(
            archive,
            "FrontElementsConfig/GameDataWindowConfig-BaseCanvas.json",
            """
            {
              "MinorPointsSur": {
                "Left": 476,
                "Top": 182
              },
              "MinorPointsHun": {
                "Left": 919,
                "Top": 182
              }
            }
            """);
        WriteZipEntry(
            archive,
            "FrontElementsConfig/CutSceneWindowConfig-BaseCanvas.json",
            """
            {
              "MapMask": {
                "Left": 488,
                "Top": 0,
                "Width": 463,
                "Height": 112
              }
            }
            """);
    }

    private static void WriteBuiltInLayoutForLegacyConversion(string builtInRoot)
    {
        var layoutPath = Path.Combine(builtInRoot, "ScoreSurWindow.json");
        Directory.CreateDirectory(Path.GetDirectoryName(layoutPath)!);
        var canvasConfig = JsonSerializer.Deserialize<FrontedCanvasConfig>(
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
            """)!;
        File.WriteAllText(layoutPath, JsonSerializer.Serialize(neo_bpsys_wpf.Core.Services.FrontedLayout.FrontedWindowConfigCanvasAdapter.FromCanvasConfig(canvasConfig)));
    }

    private static void WriteBuiltInScoreGlobalLayoutForLegacyConversion(string builtInRoot)
    {
        var layoutPath = Path.Combine(builtInRoot, "ScoreGlobalWindow.json");
        Directory.CreateDirectory(Path.GetDirectoryName(layoutPath)!);
        var canvasConfig = JsonSerializer.Deserialize<FrontedCanvasConfig>(
            """
            {
              "Version": 3,
              "CanvasWidth": 1440,
              "CanvasHeight": 195,
              "BackgroundImage": "Resources/scoreGlobal.png",
              "HomeTeamName": {
                "ControlType": "Text",
                "Left": 1,
                "Top": 2,
                "Width": 3,
                "Height": 4,
                "BindingPath": "HomeTeam.Name"
              },
              "AwayTeamName": {
                "ControlType": "Text",
                "Left": 1,
                "Top": 2,
                "Width": 3,
                "Height": 4,
                "BindingPath": "AwayTeam.Name"
              },
              "HomeScoreTotal": {
                "ControlType": "Text",
                "Left": 1,
                "Top": 2,
                "Width": 3,
                "Height": 4,
                "BindingPath": "CurrentGame.MatchScore.HomeTotalMinorScore"
              },
              "AwayScoreTotal": {
                "ControlType": "Text",
                "Left": 1,
                "Top": 2,
                "Width": 3,
                "Height": 4,
                "BindingPath": "CurrentGame.MatchScore.AwayTotalMinorScore"
              },
              "HomeGlobalScoreRow": {
                "ControlType": "GlobalScoreRow",
                "Left": 175,
                "Top": 93,
                "TeamType": "HomeTeam",
                "MajorGameGap": 180,
                "HalfGameGap": 90
              },
              "AwayGlobalScoreRow": {
                "ControlType": "GlobalScoreRow",
                "Left": 175,
                "Top": 150,
                "TeamType": "AwayTeam",
                "MajorGameGap": 180,
                "HalfGameGap": 90
              }
            }
            """)!;
        File.WriteAllText(layoutPath, JsonSerializer.Serialize(neo_bpsys_wpf.Core.Services.FrontedLayout.FrontedWindowConfigCanvasAdapter.FromCanvasConfig(canvasConfig)));
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

    private static FrontedLayoutPackageManifest ReadManifestFromPath(string path)
    {
        return JsonSerializer.Deserialize<FrontedLayoutPackageManifest>(File.ReadAllText(path), new JsonSerializerOptions
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

    private sealed class FakeFilePickerService(string? bpuiSavePath) : IFilePickerService
    {
        public int SaveBpuiFileCallCount { get; private set; }

        public string? LastDefaultFileName { get; private set; }

        public string? PickBpuiFile() => null;

        public string? PickImage() => null;

        public string? PickFontFile() => null;

        public string? PickJsonFile(string? initialDirectory = null) => null;

        public string? PickZipFile() => null;

        public string? PickSmartBpModuleArchiveFile() => null;
        public string? PickExecutableFile() => null;

        public string? PickPluginPackageFile() => null;

        public string? PickFolder() => null;

        public string? SaveJsonFile(string defaultFileName) => null;

        public string? SaveBpuiFile(string defaultFileName)
        {
            SaveBpuiFileCallCount++;
            LastDefaultFileName = defaultFileName;
            return bpuiSavePath;
        }
    }
}
