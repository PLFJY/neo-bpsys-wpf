using System;
using System.IO;
using neo_bpsys_wpf.Helpers;
using Xunit;

namespace neo_bpsys_wpf.Tests.Helpers;

/// <summary>插件图标路径解析测试。</summary>
public sealed class PluginIconPathResolverTest
{
    /// <summary>正常的相对图标路径解析到插件目录内。</summary>
    [Fact]
    public void ResolvesRelativeIconInsidePluginDirectory()
    {
        var pluginDirectory = Path.Combine(Path.GetTempPath(), "neo-bpsys-wpf-tests", "plugin", "A");

        var path = PluginIconPathResolver.Resolve(pluginDirectory, "icon.png");

        Assert.Equal(Path.GetFullPath(Path.Combine(pluginDirectory, "icon.png")), path);
    }

    /// <summary>路径穿越、绝对路径与空声明应回退到插件目录内的默认图标。</summary>
    [Theory]
    [InlineData("..\\..\\evil.png")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\System32\\calc.exe")]
    [InlineData("")]
    [InlineData(null)]
    public void RejectsEscapingIconPaths(string? declaredIcon)
    {
        var pluginDirectory = Path.Combine(Path.GetTempPath(), "neo-bpsys-wpf-tests", "plugin", "B");

        var path = PluginIconPathResolver.Resolve(pluginDirectory, declaredIcon);

        var root = Path.GetFullPath(pluginDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Assert.StartsWith(root, path, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>插件目录为空时返回空路径而不抛出异常。</summary>
    [Fact]
    public void ReturnsEmptyPathForMissingPluginDirectory()
    {
        Assert.Equal(string.Empty, PluginIconPathResolver.Resolve("", "icon.png"));
    }
}
