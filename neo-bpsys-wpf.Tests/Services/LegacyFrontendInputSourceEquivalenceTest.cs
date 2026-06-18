using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class LegacyFrontendInputSourceEquivalenceTest
{
    [Fact]
    public async Task LocalAppDataAndBpuiDirectoryUseEquivalentConversionCore()
    {
        var root = Path.Combine(Path.GetTempPath(), "legacy-input-equivalence-" + Guid.NewGuid().ToString("N"));
        var localRoot = Path.Combine(root, "local");
        var packageRoot = Path.Combine(root, "packages");
        Directory.CreateDirectory(Path.Combine(localRoot, "CustomUi"));
        try
        {
            const string config =
                """
                {
                  "BpWindowSettings": {
                    "BgImageUri": "legacy.png",
                    "TextSettings": {
                      "TeamName": {
                        "IsActive": false,
                        "Color": "#FF123456",
                        "FontFamilySite": "Arial",
                        "FontWeight": "Normal",
                        "FontSize": 37
                      }
                    }
                  }
                }
                """;
            const string layout =
                """
                {
                  "SurTeamName": { "Left": 123, "Top": 456, "Width": 222, "Height": 44 }
                }
                """;
            await File.WriteAllTextAsync(Path.Combine(localRoot, "Config.json"), config);
            await File.WriteAllTextAsync(Path.Combine(localRoot, "BpWindowConfig-BaseCanvas.json"), layout);
            await File.WriteAllTextAsync(Path.Combine(localRoot, "WidgetsWindowConfig-MapBpCanvas.json"), "{}");
            await File.WriteAllBytesAsync(Path.Combine(localRoot, "CustomUi", "legacy.png"), TinyPngBytes);

            var archivePath = Path.Combine(root, "legacy.bpui");
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                WriteEntry(archive, "Config.json", config);
                WriteEntry(archive, "FrontElementsConfig/BpWindowConfig-BaseCanvas.json", layout);
                WriteEntry(archive, "FrontElementsConfig/WidgetsWindowConfig-MapBpCanvas.json", "{}");
                var resource = archive.CreateEntry("CustomUi/legacy.png");
                await using var stream = resource.Open();
                await stream.WriteAsync(TinyPngBytes);
            }

            var manager = new FrontedLayoutPackageManager(
                packageRoot,
                Path.Combine(root, "built-in"),
                Path.Combine(root, "loose"),
                NullLogger<FrontedLayoutPackageManager>.Instance);
            var importer = new FrontedLayoutPackageImporter(
                packageRoot,
                Path.Combine(root, "import-temp"),
                manager);
            var converter = new FrontedLayoutPackageLegacyConverter(
                Path.Combine(root, "built-in"),
                Path.Combine(root, "convert-temp"),
                importer);

            var localResult = await converter.ConvertLocalAppDataAsync(
                localRoot,
                Request("equivalent.local"),
                TestContext.Current.CancellationToken);
            var bpuiResult = await converter.ConvertAsync(
                Request("equivalent.bpui", archivePath),
                TestContext.Current.CancellationToken);

            Assert.True(localResult.Success, localResult.ErrorMessage);
            Assert.True(bpuiResult.Success, bpuiResult.ErrorMessage);
            Assert.Contains(localResult.Messages, message => message.Code == LegacyConvertMessageHelper.CodeMapBpV1Skipped);
            Assert.Contains(bpuiResult.Messages, message => message.Code == LegacyConvertMessageHelper.CodeMapBpV1Skipped);

            var local = ReadBpWindow(packageRoot, "equivalent.local");
            var bpui = ReadBpWindow(packageRoot, "equivalent.bpui");
            var localName = Assert.IsType<TextFrontedControlConfig>(local.ControlLayout.Controls["SurTeamName"]);
            var bpuiName = Assert.IsType<TextFrontedControlConfig>(bpui.ControlLayout.Controls["SurTeamName"]);
            Assert.Equal(
                Path.GetFileName(Assert.Single(Directory.EnumerateFiles(
                    Path.Combine(packageRoot, "equivalent.local", "resources", "images")))),
                Path.GetFileName(Assert.Single(Directory.EnumerateFiles(
                    Path.Combine(packageRoot, "equivalent.bpui", "resources", "images")))));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static FrontedLayoutPackageLegacyConvertRequest Request(string packageId, string path = "") =>
        new()
        {
            LegacyPackagePath = path,
            PackageId = packageId,
            Name = packageId,
            InstallAfterConvert = true
        };

    private static FrontedWindowConfig ReadBpWindow(string packageRoot, string packageId) =>
        JsonSerializer.Deserialize<FrontedWindowConfig>(
            File.ReadAllText(Path.Combine(packageRoot, packageId, "FrontedLayouts", "BpWindow.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    private static void WriteEntry(ZipArchive archive, string path, string contents)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(contents);
    }

    private static byte[] TinyPngBytes =>
        Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
}
