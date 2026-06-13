using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.ViewModels.Windows;
using System;
using System.Windows.Media;
using Xunit;

namespace neo_bpsys_wpf.Tests.ViewModels;

public sealed class FrontedAnimationPartEditorViewModelTest
{
    [Fact]
    public void Editor_ValidatesInvalidTextInputsAndDuplicateName()
    {
        var editor = new FrontedAnimationPartEditorViewModel(
            new FrontedAnimationPartConfig { Name = "shine" },
            name => !string.Equals(name, "existing", StringComparison.OrdinalIgnoreCase));

        editor.Name = "existing";
        editor.WidthText = "-1";
        editor.LeftText = "not-a-number";
        editor.Fill = "#invalid";
        editor.OpacityText = "2";
        editor.ZIndexText = "1.5";

        Assert.True(editor.HasErrors);
        Assert.NotEmpty(editor.GetErrors(nameof(editor.Name)));
        Assert.NotEmpty(editor.GetErrors(nameof(editor.WidthText)));
        Assert.NotEmpty(editor.GetErrors(nameof(editor.LeftText)));
        Assert.NotEmpty(editor.GetErrors(nameof(editor.Fill)));
        Assert.NotEmpty(editor.GetErrors(nameof(editor.OpacityText)));
        Assert.NotEmpty(editor.GetErrors(nameof(editor.ZIndexText)));
    }

    [Fact]
    public void Editor_ColorPickerAndTextStaySynchronized()
    {
        var editor = new FrontedAnimationPartEditorViewModel(
            new FrontedAnimationPartConfig { Name = "shine" },
            _ => true);

        editor.FillColor = Color.FromArgb(0x80, 0x11, 0x22, 0x33);
        Assert.Equal("#80112233", editor.Fill);

        editor.Stroke = "#FF445566";
        Assert.Equal(Color.FromArgb(0xFF, 0x44, 0x55, 0x66), editor.StrokeColor);
        Assert.False(editor.HasErrors);
    }

    [Fact]
    public void Editor_AppliesValidatedTextValuesToConfig()
    {
        var editor = new FrontedAnimationPartEditorViewModel(
            new FrontedAnimationPartConfig { Name = "shine" },
            _ => true)
        {
            WidthText = "4",
            HeightText = "100%",
            LeftText = "12.5",
            TopText = "-3",
            Fill = "#FFFFFFFF",
            StrokeThicknessText = "2",
            OpacityText = "0.5",
            ZIndexText = "10"
        };
        var target = new FrontedAnimationPartConfig();

        editor.ApplyTo(target);

        Assert.Equal(4D, target.Width);
        Assert.Null(target.WidthText);
        Assert.Null(target.Height);
        Assert.Equal("100%", target.HeightText);
        Assert.Equal(12.5D, target.Left);
        Assert.Equal(-3D, target.Top);
        Assert.Equal("#FFFFFFFF", target.Fill);
        Assert.Equal(2D, target.StrokeThickness);
        Assert.Equal(0.5D, target.Opacity);
        Assert.Equal(10, target.ZIndex);
    }

    [Fact]
    public void Editor_ExposesTypeSpecificEditorState()
    {
        var editor = new FrontedAnimationPartEditorViewModel(
            new FrontedAnimationPartConfig { Name = "part", Kind = FrontedAnimationPartKind.Rectangle },
            _ => true);

        Assert.True(editor.IsRectangle);
        Assert.True(editor.IsShape);
        Assert.False(editor.IsImage);

        editor.Kind = FrontedAnimationPartKind.Border;
        Assert.True(editor.IsBorder);
        Assert.True(editor.IsShape);
        Assert.False(editor.IsRectangle);

        editor.Kind = FrontedAnimationPartKind.Image;
        Assert.True(editor.IsImage);
        Assert.False(editor.IsShape);
        Assert.False(editor.IsBorder);
    }
}
