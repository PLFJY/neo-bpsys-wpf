#nullable enable

using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3;
using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// 验证 <see cref="FrontedLayoutPackageImporter"/> 在导入 v3 `.bpui` 包时不会重写 Layout JSON，
/// 以确保宿主暂不认识的根字段、未来版本扩展字段、原始 JSON 格式与属性顺序都被保留。
/// </summary>
public sealed class FrontedLayoutPackageImporterTest
{
    /// <summary>
    /// 验证导入包含 <c>VendorExtension.KeepMe</c> 嵌套未知字段的 `.bpui` 包后，
    /// 磁盘上的 Layout JSON 仍然保留该字段。
    /// </summary>
    [Fact]
    public async Task Import_DoesNotRewriteLayoutJson()
    {
        var root = CreateTempDirectory();
        try
        {
            var archivePath = Path.Combine(root, "preserve.bpui");
            var layoutJson = """
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
                  },
                  "VendorExtension": {
                    "KeepMe": true
                  }
                }
                """;
            CreateBpuiArchiveWithLayout(archivePath, layoutJson);

            var packageRoot = Path.Combine(root, "packages");
            var importer = CreateImporter(packageRoot, Path.Combine(root, "temp"));

            var result = await importer.ImportAsync(new FrontedLayoutPackageImportRequest
            {
                PackagePath = archivePath
            }, TestContext.Current.CancellationToken);

            Assert.True(result.Success, result.ErrorMessage);
            var importedLayoutPath = Path.Combine(packageRoot, "basic-package", "FrontedLayouts", "BpWindow.json");
            Assert.True(File.Exists(importedLayoutPath), "Imported layout JSON should exist on disk.");
            var importedJson = await File.ReadAllTextAsync(importedLayoutPath);
            Assert.Contains("\"VendorExtension\"", importedJson);
            Assert.Contains("\"KeepMe\"", importedJson);
            Assert.Contains("true", importedJson);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    /// <summary>
    /// 验证导入包含顶层未知标量字段的 `.bpui` 包后，
    /// 磁盘上的 Layout JSON 仍然保留该未知字段。
    /// </summary>
    [Fact]
    public async Task UnknownLayoutJsonField_IsPreserved()
    {
        var root = CreateTempDirectory();
        try
        {
            var archivePath = Path.Combine(root, "unknown-field.bpui");
            var layoutJson = """
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
                  },
                  "UnknownFutureField": 42,
                  "AnotherUnknown": "future-value"
                }
                """;
            CreateBpuiArchiveWithLayout(archivePath, layoutJson);

            var packageRoot = Path.Combine(root, "packages");
            var importer = CreateImporter(packageRoot, Path.Combine(root, "temp"));

            var result = await importer.ImportAsync(new FrontedLayoutPackageImportRequest
            {
                PackagePath = archivePath
            }, TestContext.Current.CancellationToken);

            Assert.True(result.Success, result.ErrorMessage);
            var importedLayoutPath = Path.Combine(packageRoot, "basic-package", "FrontedLayouts", "BpWindow.json");
            Assert.True(File.Exists(importedLayoutPath), "Imported layout JSON should exist on disk.");
            var importedJson = await File.ReadAllTextAsync(importedLayoutPath);
            Assert.Contains("\"UnknownFutureField\"", importedJson);
            Assert.Contains("42", importedJson);
            Assert.Contains("\"AnotherUnknown\"", importedJson);
            Assert.Contains("\"future-value\"", importedJson);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    /// <summary>
    /// 验证当 manifest 中 Layout 的 <c>Window</c> 字段与 <c>Path</c> 派生出的 Canonical ID 不一致时，
    /// 导入被拒绝。这防止包通过 manifest 声称某个 Window ID 但实际文件路径指向另一个窗口，
    /// 避免 round-trip 时 Window 字符串与路径语义脱钩。
    /// </summary>
    [Fact]
    public async Task ManifestWindowAndPathMismatch_IsRejected()
    {
        var root = CreateTempDirectory();
        try
        {
            var archivePath = Path.Combine(root, "mismatch.bpui");
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                WriteZipEntry(archive, "manifest.json", JsonSerializer.Serialize(new FrontedLayoutPackageManifest
                {
                    PackageId = "mismatch-package",
                    Name = "Mismatch Package",
                    Content = new FrontedLayoutPackageManifestContent
                    {
                        Layouts =
                        [
                            new FrontedLayoutPackageLayoutEntry
                            {
                                Window = "BpWindow",
                                Path = "FrontedLayouts/DifferentWindow.json"
                            }
                        ]
                    }
                }));
                WriteZipEntry(
                    archive,
                    "FrontedLayouts/DifferentWindow.json",
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
            }

            var packageRoot = Path.Combine(root, "packages");
            var importer = CreateImporter(packageRoot, Path.Combine(root, "temp"));

            var result = await importer.ImportAsync(new FrontedLayoutPackageImportRequest
            {
                PackagePath = archivePath
            }, TestContext.Current.CancellationToken);

            Assert.False(result.Success);
            Assert.Contains("does not match path", result.ErrorMessage);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    private static FrontedLayoutPackageImporter CreateImporter(string packageRoot, string tempRoot)
    {
        return new FrontedLayoutPackageImporter(
            packageRoot,
            tempRoot,
            controlRegistry: new FrontedV3ControlRegistry([CreateTextRegistration()]));
    }

    private static FrontedV3ControlRegistration CreateTextRegistration()
    {
        return new FrontedV3ControlRegistration
        {
            CanonicalControlType = "Text",
            LocalControlId = "Text",
            PackageId = "builtin",
            IsBuiltIn = true,
            ControlType = typeof(TextFrontedControl),
            ConfigType = typeof(TextFrontedControlConfig),
            Properties = Array.Empty<FrontedV3PropertyDefinition>(),
            CreateDefaultConfig = () => new TextFrontedControlConfig()
        };
    }

    private static void CreateBpuiArchiveWithLayout(string archivePath, string layoutJson)
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
        WriteZipEntry(archive, "FrontedLayouts/BpWindow.json", layoutJson);
    }

    private static void WriteZipEntry(ZipArchive archive, string entryName, string text)
    {
        var entry = archive.CreateEntry(entryName);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write(text);
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
}
