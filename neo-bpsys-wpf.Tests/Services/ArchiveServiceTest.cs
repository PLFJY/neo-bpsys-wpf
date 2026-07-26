using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.Archives;
using neo_bpsys_wpf.Core.Services.Archives;
using neo_bpsys_wpf.Services;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class ArchiveServiceTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "neo-bpsys-wpf-tests", Guid.NewGuid().ToString("N"));
    private readonly SevenZipArchiveService _archiveService = new();

    public ArchiveServiceTest()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task DetectFormat_ZipArchive_ReturnsZip()
    {
        var archivePath = Path.Combine(_root, "package.zip");
        CreateZipArchive(archivePath, ("manifest.yml", "id: test.plugin\nversion: 1.0.0\napiVersion: 3.0.0.0\n"));

        var format = await _archiveService.DetectFormatAsync(archivePath);

        Assert.Equal(ArchiveFormat.Zip, format);
    }

    [Fact]
    public async Task DetectFormat_SevenZipArchive_ReturnsSevenZip()
    {
        var archivePath = Path.Combine(_root, "package.7z");
        CreateSevenZipArchive(archivePath, ("manifest.yml", "id: test.plugin\nversion: 1.0.0\napiVersion: 3.0.0.0\n"));

        var format = await _archiveService.DetectFormatAsync(archivePath);

        Assert.Equal(ArchiveFormat.SevenZip, format);
    }

    [Fact]
    public async Task ExtractToDirectory_RejectsTraversalEntry()
    {
        var archivePath = Path.Combine(_root, "traversal.zip");
        CreateZipArchive(archivePath, ("../escape.txt", "bad"));

        await Assert.ThrowsAsync<IOException>(() =>
            _archiveService.ExtractToDirectoryAsync(archivePath, Path.Combine(_root, "extract")));
    }

    [Fact]
    public async Task ExtractToDirectory_RejectsAbsoluteEntry()
    {
        var archivePath = Path.Combine(_root, "absolute.zip");
        CreateZipArchive(archivePath, ("/absolute.txt", "bad"));

        await Assert.ThrowsAsync<IOException>(() =>
            _archiveService.ExtractToDirectoryAsync(archivePath, Path.Combine(_root, "extract")));
    }

    [Fact]
    public async Task ExtractToDirectory_RejectsNormalizedEscape()
    {
        var archivePath = Path.Combine(_root, "escape.zip");
        CreateZipArchive(archivePath, ("safe/../../escape.txt", "bad"));

        await Assert.ThrowsAsync<IOException>(() =>
            _archiveService.ExtractToDirectoryAsync(archivePath, Path.Combine(_root, "extract")));
    }

    [Fact]
    public async Task ExtractToDirectory_PreservesNestedStructure()
    {
        var archivePath = Path.Combine(_root, "nested.7z");
        CreateSevenZipArchive(archivePath, ("a/b/c.txt", "ok"));
        var extractPath = Path.Combine(_root, "extract");

        await _archiveService.ExtractToDirectoryAsync(archivePath, extractPath);

        Assert.Equal("ok", File.ReadAllText(Path.Combine(extractPath, "a", "b", "c.txt")));
    }

    [Fact]
    public async Task ExtractToDirectory_ReportsProgress()
    {
        var archivePath = Path.Combine(_root, "progress.zip");
        CreateZipArchive(
            archivePath,
            ("a.txt", "aaa"),
            ("b.txt", "bbb"),
            ("c.txt", "ccc"));
        var extractPath = Path.Combine(_root, "extract-progress");

        var reports = new System.Collections.Generic.List<ArchiveProgress>();
        var progress = new Progress<ArchiveProgress>(p => reports.Add(p));

        await _archiveService.ExtractToDirectoryAsync(archivePath, extractPath, progress);

        Assert.NotEmpty(reports);
        Assert.Equal(100, reports[^1].Percentage);
    }

    [Fact]
    public async Task ExtractToDirectory_FinalProgressReaches100()
    {
        var archivePath = Path.Combine(_root, "final-progress.7z");
        CreateSevenZipArchive(
            archivePath,
            ("file1.txt", "content1"),
            ("file2.txt", "content2"),
            ("file3.txt", "content3"));
        var extractPath = Path.Combine(_root, "extract-final-progress");

        var reports = new System.Collections.Generic.List<ArchiveProgress>();
        var progress = new Progress<ArchiveProgress>(p => reports.Add(p));

        await _archiveService.ExtractToDirectoryAsync(archivePath, extractPath, progress);

        Assert.NotEmpty(reports);
        Assert.Equal(100, reports[^1].Percentage);
    }

    [Fact]
    public async Task ExtractToDirectory_Cancelled_ThrowsOperationCanceledException()
    {
        var archivePath = Path.Combine(_root, "cancel.7z");
        CreateSevenZipArchive(archivePath, ("a.txt", "ok"));
        var extractPath = Path.Combine(_root, "extract-cancel");

        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _archiveService.ExtractToDirectoryAsync(archivePath, extractPath, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task ExtractToDirectory_CorruptedArchiveThrows()
    {
        var archivePath = Path.Combine(_root, "corrupted.7z");
        var bytes = new byte[128];
        Random.Shared.NextBytes(bytes);
        File.WriteAllBytes(archivePath, bytes);
        var extractPath = Path.Combine(_root, "extract-corrupted");

        await Assert.ThrowsAnyAsync<Exception>(() =>
            _archiveService.ExtractToDirectoryAsync(archivePath, extractPath));
    }

    [Fact]
    public async Task ExtractToDirectory_UnknownFormatRejected()
    {
        var archivePath = Path.Combine(_root, "not-an-archive.7z");
        File.WriteAllText(archivePath, "this is not a 7z archive");
        var extractPath = Path.Combine(_root, "extract-unknown");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            _archiveService.ExtractToDirectoryAsync(archivePath, extractPath));
    }

    [Fact]
    public async Task ExtractToDirectory_WithoutProgress_PreservesExistingBehavior()
    {
        var archivePath = Path.Combine(_root, "noprogress.7z");
        CreateSevenZipArchive(archivePath, ("a.txt", "ok"));
        var extractPath = Path.Combine(_root, "extract-noprogress");

        var format = await _archiveService.ExtractToDirectoryAsync(archivePath, extractPath);

        Assert.Equal(ArchiveFormat.SevenZip, format);
        Assert.Equal("ok", File.ReadAllText(Path.Combine(extractPath, "a.txt")));
    }

    [Fact]
    public void PluginInstallService_InstallFromArchive_AcceptsZip()
    {
        var archivePath = Path.Combine(_root, "plugin.zip");
        CreatePluginArchive(archivePath, ArchiveFormat.Zip, "test.plugin.zip");
        var extractPath = Path.Combine(_root, "plugin-zip");

        var result = CreatePluginInstallService().InstallFromArchive(archivePath, extractPath);

        Assert.Equal("test.plugin.zip", result.Manifest.Id);
        Assert.True(Directory.Exists(Path.Combine(neo_bpsys_wpf.Core.AppConstants.PluginPath, "test.plugin.zip")));
    }

    [Fact]
    public void PluginInstallService_InstallFromArchive_AcceptsSevenZip()
    {
        var archivePath = Path.Combine(_root, "plugin.7z");
        CreatePluginArchive(archivePath, ArchiveFormat.SevenZip, "test.plugin.7z");
        var extractPath = Path.Combine(_root, "plugin-7z");

        var result = CreatePluginInstallService().InstallFromArchive(archivePath, extractPath);

        Assert.Equal("test.plugin.7z", result.Manifest.Id);
        Assert.True(Directory.Exists(Path.Combine(neo_bpsys_wpf.Core.AppConstants.PluginPath, "test.plugin.7z")));
    }

    public void Dispose()
    {
        foreach (var pluginId in new[] { "test.plugin.zip", "test.plugin.7z" })
        {
            var pluginPath = Path.Combine(neo_bpsys_wpf.Core.AppConstants.PluginPath, pluginId);
            if (Directory.Exists(pluginPath))
            {
                Directory.Delete(pluginPath, recursive: true);
            }
        }

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private PluginInstallService CreatePluginInstallService()
    {
        return new PluginInstallService(
            NullLogger<PluginInstallService>.Instance,
            _archiveService,
            new GlobalRestartService());
    }

    private static void CreatePluginArchive(string archivePath, ArchiveFormat format, string pluginId)
    {
        var manifest = $"""
                       id: {pluginId}
                       name: Test Plugin
                       version: 1.0.0
                       apiVersion: 3.0.0.0
                       entranceAssembly: TestPlugin.dll
                       """;
        if (format == ArchiveFormat.Zip)
        {
            CreateZipArchive(archivePath, ("manifest.yml", manifest));
            return;
        }

        CreateSevenZipArchive(archivePath, ("manifest.yml", manifest));
    }

    private static void CreateZipArchive(string archivePath, params (string EntryName, string Text)[] entries)
    {
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        foreach (var (entryName, text) in entries)
        {
            var entry = archive.CreateEntry(entryName);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(text);
        }
    }

    private static void CreateSevenZipArchive(string archivePath, params (string EntryName, string Text)[] entries)
    {
        var sevenZipExe = Path.Combine(AppContext.BaseDirectory, "Tools", "7Zip", "7z.exe");
        if (!File.Exists(sevenZipExe))
            throw new FileNotFoundException("Test requires 7z.exe at: " + sevenZipExe, sevenZipExe);

        var tempDir = Path.Combine(Path.GetDirectoryName(archivePath)!, Path.GetFileNameWithoutExtension(archivePath) + "-src");
        Directory.CreateDirectory(tempDir);
        try
        {
            foreach (var (entryName, text) in entries)
            {
                var fullPath = Path.Combine(tempDir, entryName);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                File.WriteAllText(fullPath, text);
            }

            var psi = new ProcessStartInfo(sevenZipExe)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = tempDir
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
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
