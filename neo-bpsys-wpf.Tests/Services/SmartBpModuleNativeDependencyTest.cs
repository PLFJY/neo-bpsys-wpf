using System;
using System.IO;
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
    public void FindModuleUnmanagedLibraryPath_RejectsPathTraversal()
    {
        Directory.CreateDirectory(_root);

        var actual = SmartBpModuleManager.FindModuleUnmanagedLibraryPath(_root, @"..\OpenCvSharpExtern");

        Assert.Null(actual);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
