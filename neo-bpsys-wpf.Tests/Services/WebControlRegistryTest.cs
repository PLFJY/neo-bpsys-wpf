using System;
using neo_bpsys_wpf.WebRenderer.Services;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>Web Control 注册表与适配器描述的契约测试。</summary>
public sealed class WebControlRegistryTest
{
    /// <summary>合法 <c>plugin:*</c> 控件类型可以构造描述符。</summary>
    [Fact]
    public void DescriptorAcceptsPluginControlType()
    {
        var descriptor = new WebControlAdapterDescriptor("plugin:top.plfjy.example/TeamCard", "teamcard-v1");

        Assert.Equal("plugin:top.plfjy.example/TeamCard", descriptor.ControlType);
        Assert.Equal("teamcard-v1", descriptor.AdapterId);
    }

    /// <summary>非 <c>plugin:</c> 前缀或非法路径字符必须被拒绝。</summary>
    [Theory]
    [InlineData("Text", "adapter")]
    [InlineData("plugin:abc", "adapter")]
    [InlineData("plugin:abc/../x", "adapter")]
    [InlineData("plugin:abc/x:y", "adapter")]
    [InlineData("plugin:abc/x", "")]
    [InlineData("plugin:abc/x", "bad id")]
    public void DescriptorRejectsInvalidIdentity(string controlType, string adapterId)
    {
        Assert.ThrowsAny<ArgumentException>(() => new WebControlAdapterDescriptor(controlType, adapterId));
    }

    /// <summary>默认注册表不加载适配器，并拒绝空控件类型。</summary>
    [Fact]
    public void DefaultRegistryReturnsNoAdapter()
    {
        var registry = new WebControlRegistry();

        Assert.False(registry.TryGet("plugin:top.plfjy.example/TeamCard", out var descriptor));
        Assert.Null(descriptor);
        Assert.ThrowsAny<ArgumentException>(() => registry.TryGet(" ", out _));
    }
}
