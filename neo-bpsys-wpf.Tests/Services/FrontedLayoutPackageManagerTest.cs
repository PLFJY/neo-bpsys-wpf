#nullable enable

using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System;
using System.IO;
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
            WriteManifest(Path.Combine(packageRoot, "package-a"), new
            {
                PackageId = "package-a",
                Name = "Package A"
            });
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
        Assert.Contains("ListBox.ContextMenu", text);
        Assert.Contains("Command=\"{Binding DeleteSelectedControlCommand}\" Header=\"{lex:Loc DeleteControl}\" InputGestureText=\"Del\"", text);
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
        Assert.Contains("<ui:CardExpander>", text);
        Assert.Contains("<ui:DynamicScrollViewer", text);
        Assert.DoesNotContain("<ui:DataGrid", text);
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
}
