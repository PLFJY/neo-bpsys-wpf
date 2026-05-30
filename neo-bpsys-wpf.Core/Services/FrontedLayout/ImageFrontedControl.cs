using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 内置 v3 图片控件工厂。
/// </summary>
public class ImageFrontedControl : IFrontedControl
{
    /// <inheritdoc />
    public string ControlType => "Image";

    /// <inheritdoc />
    public Type ConfigType => typeof(ImageFrontedControlConfig);

    /// <inheritdoc />
    public FrameworkElement Create(
        string name,
        FrontedControlConfigBase config,
        FrontedControlBuildContext context)
    {
        if (config is not ImageFrontedControlConfig imageConfig)
        {
            throw new FrontedLayoutConfigException($"Control '{name}' config is not an Image config.");
        }

        var border = FrontedControlFactoryHelper.CreateOuterBorder(name, imageConfig);
        var image = new Image();

        if (!string.IsNullOrWhiteSpace(imageConfig.BindingPath))
        {
            BindingOperations.SetBinding(image, Image.SourceProperty, new Binding(imageConfig.BindingPath)
            {
                Source = context.SharedDataService
            });
        }

        FrontedControlFactoryHelper.TryApplyEnum<Stretch>(
            imageConfig.Stretch,
            value => image.Stretch = value,
            context,
            nameof(imageConfig.Stretch));

        border.Child = image;
        return border;
    }
}
