using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// 调试用：尝试导入真实的 1.x no_author 旧版 .bpui 包，暴露转换器在该包上产生的具体错误。
/// 文件不存在时跳过，避免在无该样本的机器上失败。
/// </summary>
public sealed class LegacyNoAuthorPackageImportTest
{
    private const string SampleArchivePath = @"E:\Downloads\bp-sys-wpf-1.x-no_author.bpui";

    [Fact]
    public async Task ConvertRealNoAuthorBpuiPackageSucceeds()
    {
        if (!File.Exists(SampleArchivePath))
        {
            Assert.Skip($"Sample archive not found at {SampleArchivePath}.");
        }

        var root = Path.Combine(Path.GetTempPath(), "neo-bpsys-wpf-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var builtInRoot = Path.Combine(root, "builtIn");
            Directory.CreateDirectory(builtInRoot);
            var converter = new FrontedLayoutPackageLegacyConverter(builtInRoot, Path.Combine(root, "temp"));

            var result = await converter.ConvertAsync(new FrontedLayoutPackageLegacyConvertRequest
            {
                LegacyPackagePath = SampleArchivePath,
                PackageId = "converted.legacy.no-author-sample",
                Name = "no_author sample"
            }, TestContext.Current.CancellationToken);

            var messages = string.Join("\n", result.Messages.Select(m => $"[{m.Severity}] {m.Code}: {m.Message}"));
            var diagnosticPath = Path.Combine(Path.GetTempPath(), "no-author-conversion-diagnostic.txt");
            File.WriteAllText(diagnosticPath, $"Success={result.Success}\nErrorMessage={result.ErrorMessage}\nConvertedPackagePath={result.ConvertedPackagePath}\nMessages:\n{messages}");
            Assert.True(result.Success, $"Conversion failed. Diagnostic written to {diagnosticPath}. ErrorMessage={result.ErrorMessage}\nMessages:\n{messages}");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
