using System.IO;
using neo_bpsys_wpf.Core.Models.SmartBpModule;

namespace neo_bpsys_wpf.Services.SmartBpModule;

/// <summary>SmartBP 模块发布文件的完整性校验和旧包关键文件探针。</summary>
public sealed partial class SmartBpModuleManager
{
    private bool ValidateModuleFileIntegrity(
        string moduleRoot,
        SmartBpModuleIntegrity? integrity,
        out string error,
        bool verifyHashes)
    {
        error = string.Empty;
        if (integrity == null)
            return ValidateLegacyModuleProbes(moduleRoot, out error);

        if (integrity.Version != 1 || integrity.Files is not { Count: > 0 })
        {
            error = "Module integrity manifest is empty or uses an unsupported version.";
            return false;
        }

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in integrity.Files)
        {
            var relativePath = file?.Path;
            if (string.IsNullOrWhiteSpace(relativePath) || relativePath.Contains('\\') ||
                relativePath.Contains(':') || relativePath.StartsWith('/') ||
                relativePath.Split('/').Any(segment => segment is "" or "." or "..") ||
                relativePath.Equals("component.json", StringComparison.OrdinalIgnoreCase) ||
                !paths.Add(relativePath) || file!.Size < 0 || file.Sha256 is not { Length: 64 } ||
                !file.Sha256.All(Uri.IsHexDigit) || IsMutableModuleAsset(relativePath))
            {
                error = $"Module integrity manifest contains an invalid file record: {relativePath}";
                return false;
            }

            var path = Path.Combine(moduleRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path) || new FileInfo(path).Length != file.Size)
            {
                error = $"Module file is missing or incomplete: {relativePath}";
                return false;
            }

            if (verifyHashes && !string.Equals(ComputeSha256(path), file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                error = $"Module file SHA-256 verification failed: {relativePath}";
                return false;
            }
        }

        if (!paths.Contains(SmartBpModuleConstants.EntryAssemblyName))
        {
            error = "Module integrity manifest does not contain the entry assembly.";
            return false;
        }

        return true;
    }

    private static bool IsMutableModuleAsset(string relativePath) => ManagedAssetRootNames.Any(root =>
        relativePath.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase));

    private static bool ValidateLegacyModuleProbes(string moduleRoot, out string error)
    {
        // 旧包没有逐文件摘要；只检查既有 CPU OCR 必需组件，不给旧清单补写新契约。
        foreach (var name in new[] { "Sdcb.PaddleOCR.dll", "Sdcb.PaddleInference.dll", "OpenCvSharp.dll" })
        {
            var moduleFile = Path.Combine(moduleRoot, name);
            var hostFile = Path.Combine(AppContext.BaseDirectory, name);
            if ((!File.Exists(moduleFile) || new FileInfo(moduleFile).Length == 0) &&
                (!File.Exists(hostFile) || new FileInfo(hostFile).Length == 0))
            {
                error = $"Required module dependency is missing or empty: {name}";
                return false;
            }
        }

        var paddle = FindModuleUnmanagedLibraryPath(moduleRoot, "paddle_inference_c");
        var openCv = FindModuleUnmanagedLibraryPath(moduleRoot, "OpenCvSharpExtern");
        foreach (var (name, path) in new[] { ("paddle_inference_c.dll", paddle), ("OpenCvSharpExtern.dll", openCv) })
        {
            if (path == null || new FileInfo(path).Length == 0)
            {
                error = $"Required module native dependency is missing or empty: {name}";
                return false;
            }
        }

        // 既有发布包使用 MKL CPU runtime；入口库存在并不代表其依赖已复制完成。
        var cpuNativeRoot = Path.GetDirectoryName(paddle!)!;
        foreach (var name in new[] { "common.dll", "phi.dll", "libiomp5md.dll", "mkldnn.dll", "mklml.dll", "paddle2onnx.dll", "onnxruntime.dll", "onnxruntime_providers_shared.dll" })
        {
            var dependency = Path.Combine(cpuNativeRoot, name);
            if (!File.Exists(dependency) || new FileInfo(dependency).Length == 0)
            {
                error = $"Required module CPU runtime dependency is missing or empty: {name}";
                return false;
            }
        }

        var profile = Path.Combine(moduleRoot, "Resources", "SmartBp", "BpRecognitionLayoutProfile.json");
        if (!File.Exists(profile) || new FileInfo(profile).Length == 0)
        {
            error = "Required module resource is missing or empty: Resources/SmartBp/BpRecognitionLayoutProfile.json";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
