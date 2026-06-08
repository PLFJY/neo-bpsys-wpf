using neo_bpsys_wpf.Core.Helpers;
using System.Windows.Media;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

public class FrontedColorParserTest
{
    [Fact]
    public void ColorParser_ParseHexRgb()
    {
        Assert.True(ColorHelper.TryParseColor("#112233", out var color));
        Assert.Equal(Color.FromArgb(0xFF, 0x11, 0x22, 0x33), color);
    }

    [Fact]
    public void ColorParser_ParseHexArgb()
    {
        Assert.True(ColorHelper.TryParseColor("#80112233", out var color));
        Assert.Equal(Color.FromArgb(0x80, 0x11, 0x22, 0x33), color);
    }

    [Fact]
    public void ColorParser_ParseNamedColorWhite()
    {
        Assert.True(ColorHelper.TryParseColor("White", out var color));
        Assert.Equal(Colors.White, color);
    }

    [Fact]
    public void ColorParser_ParseNamedColorTransparent()
    {
        Assert.True(ColorHelper.TryParseColor("transparent", out var color));
        Assert.Equal(Colors.Transparent, color);
    }

    [Fact]
    public void ColorParser_Invalid_ReturnsFalse()
    {
        Assert.False(ColorHelper.TryParseColor("not-a-color", out _));
    }
}
