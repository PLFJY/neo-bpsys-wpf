using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using neo_bpsys_wpf.Core.Models.SmartBpModule;
using neo_bpsys_wpf.Services.SmartBpModule;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class SmartBpModuleNativeDependencyTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"SmartBpNative-{Guid.NewGuid():N}");

    [Fact]
    public void FindModuleUnmanagedLibraryPath_FindsRidNativeAssetWithoutExtension()
    {
        var nativeRoot = Path.Combine(_root, "runtimes", SmartBpModuleConstants.Rid, "native");
        Directory.CreateDirectory(nativeRoot);
        var expected = Path.Combine(nativeRoot, "OpenCvSharpExtern.dll");
        File.WriteAllBytes(expected, []);

        var actual = SmartBpModuleManager.FindModuleUnmanagedLibraryPath(_root, "OpenCvSharpExtern");

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FindModuleUnmanagedLibraryPath_FindsOnnxRuntimeProviderAssetFromRidNativeDirectory()
    {
        var nativeRoot = Path.Combine(_root, "runtimes", "win-x64", "native");
        Directory.CreateDirectory(nativeRoot);
        var expected = Path.Combine(nativeRoot, "onnxruntime_providers_shared.dll");
        File.WriteAllBytes(expected, []);

        var actual = SmartBpModuleManager.FindModuleUnmanagedLibraryPath(_root, "onnxruntime_providers_shared");

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FindModuleUnmanagedLibraryPath_RejectsPathTraversal()
    {
        Directory.CreateDirectory(_root);

        var actual = SmartBpModuleManager.FindModuleUnmanagedLibraryPath(_root, @"..\OpenCvSharpExtern");

        Assert.Null(actual);
    }

    [Fact]
    public void FindModuleUnmanagedLibraryPath_FindsModuleOwnedPaddleCpuRuntime()
    {
        var nativeRoot = Path.Combine(_root, "Runtime", "Paddle", "cpu", "3.3.1.70", "native");
        Directory.CreateDirectory(nativeRoot);
        var expected = Path.Combine(nativeRoot, "paddle_inference_c.dll");
        File.WriteAllBytes(expected, []);

        var actual = SmartBpModuleManager.FindModuleUnmanagedLibraryPath(_root, "paddle_inference_c");

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task RapidOcrNet_InitializesAndDetectsThroughSmartBpModuleLoadContext()
    {
        var repositoryRoot = FindRepositoryRoot();
        if (repositoryRoot == null) return;

        var moduleRoot = Path.Combine(repositoryRoot, "neo-bpsys-wpf.SmartBp.Module", "bin", "Debug", "net10.0-windows10.0.20348");
        var hostAssemblyPath = Path.Combine(AppContext.BaseDirectory, "neo-bpsys-wpf.dll");
        var modelRoot = Path.Combine(moduleRoot, "OCRModels", "RapidOCR", "Models", "ppocr-v5-zh-mobile");
        var samplePath = Path.Combine(moduleRoot, "Resources", "SmartBp", "TestFrames", "ban-sur-16x9.png");
        var requiredFiles = new[]
        {
            Path.Combine(moduleRoot, SmartBpModuleConstants.EntryAssemblyName),
            Path.Combine(moduleRoot, "RapidOcrNet.dll"),
            Path.Combine(moduleRoot, "runtimes", SmartBpModuleConstants.Rid, "native", "onnxruntime.dll"),
            Path.Combine(modelRoot, "ch_PP-OCRv5_det_mobile.onnx"),
            Path.Combine(modelRoot, "ch_ppocr_mobile_v2.0_cls_mobile.onnx"),
            Path.Combine(modelRoot, "ch_PP-OCRv5_rec_mobile.onnx"),
            Path.Combine(modelRoot, "ppocrv5_dict.txt"),
            samplePath,
            hostAssemblyPath
        };
        if (requiredFiles.Any(path => !File.Exists(path))) return;

        var probeRoot = Path.Combine(_root, "probe");
        Directory.CreateDirectory(probeRoot);
        await File.WriteAllTextAsync(Path.Combine(probeRoot, "Probe.csproj"), ProbeProject);
        await File.WriteAllTextAsync(Path.Combine(probeRoot, "Program.cs"), ProbeProgram);

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run -- \"{moduleRoot}\" \"{modelRoot}\" \"{samplePath}\" \"{hostAssemblyPath}\"",
            WorkingDirectory = probeRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Failed to start RapidOCR module probe.");

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        var completed = await Task.Run(() => process.WaitForExit(120_000));
        if (!completed)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("RapidOCR module probe timed out.");
        }

        var output = await outputTask;
        var error = await errorTask;
        Assert.True(process.ExitCode == 0,
            $"RapidOCR module probe failed. ExitCode={process.ExitCode}{Environment.NewLine}STDOUT:{Environment.NewLine}{output}{Environment.NewLine}STDERR:{Environment.NewLine}{error}");
        Assert.Contains("init ok", output, StringComparison.Ordinal);
        Assert.Contains("detect ok", output, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "neo-bpsys-wpf.slnx")))
                return directory.FullName;
        }

        return null;
    }

    private const string ProbeProject = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net10.0-windows10.0.20348</TargetFramework>
            <Nullable>enable</Nullable>
          </PropertyGroup>
        </Project>
        """;

    private const string ProbeProgram = """
        using System;
        using System.IO;
        using System.Reflection;
        using System.Runtime.Loader;

        var moduleRoot = args[0];
        var modelRoot = args[1];
        var samplePath = args[2];
        var hostAssemblyPath = args[3];
        var hostRoot = Path.GetDirectoryName(hostAssemblyPath)!;

        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            var path = Path.Combine(hostRoot, name.Name + ".dll");
            return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };

        var hostAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(hostAssemblyPath);
        hostAssembly
            .GetType("neo_bpsys_wpf.Services.SmartBpModule.SmartBpModuleManager")!
            .GetMethod("RegisterModuleNativeSearchDirectories", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!
            .Invoke(null, new object?[] { moduleRoot, null });

        var loadContext = new AssemblyLoadContext("SmartBpProbe", isCollectible: false);
        loadContext.Resolving += (_, name) =>
        {
            var path = Path.Combine(moduleRoot, name.Name + ".dll");
            return File.Exists(path) ? loadContext.LoadFromAssemblyPath(path) : null;
        };

        loadContext.LoadFromAssemblyPath(Path.Combine(moduleRoot, "neo-bpsys-wpf.SmartBp.Module.dll"));
        var rapidAssembly = loadContext.LoadFromAssemblyPath(Path.Combine(moduleRoot, "RapidOcrNet.dll"));
        var rapidType = rapidAssembly.GetType("RapidOcrNet.RapidOcr", throwOnError: true)!;
        using var rapidOcr = (IDisposable)Activator.CreateInstance(rapidType)!;
        rapidType.GetMethod("InitModels", new[] { typeof(string), typeof(string), typeof(string), typeof(string), typeof(int) })!
            .Invoke(rapidOcr, new object[]
            {
                Path.Combine(modelRoot, "ch_PP-OCRv5_det_mobile.onnx"),
                Path.Combine(modelRoot, "ch_ppocr_mobile_v2.0_cls_mobile.onnx"),
                Path.Combine(modelRoot, "ch_PP-OCRv5_rec_mobile.onnx"),
                Path.Combine(modelRoot, "ppocrv5_dict.txt"),
                1
            });
        Console.WriteLine("init ok");

        var optionsType = rapidAssembly.GetType("RapidOcrNet.RapidOcrOptions", throwOnError: true)!;
        var options = optionsType.GetProperty("Default", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
            ?? optionsType.GetField("Default", BindingFlags.Public | BindingFlags.Static)!.GetValue(null);
        var detect = rapidType.GetMethod("Detect", new[] { typeof(string), optionsType })!;
        var result = detect.Invoke(rapidOcr, new[] { samplePath, options });
        Console.WriteLine("detect ok " + result!.GetType().FullName);
        """;
}
