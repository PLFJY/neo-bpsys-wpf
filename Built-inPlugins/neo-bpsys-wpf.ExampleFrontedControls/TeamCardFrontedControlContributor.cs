using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace neo_bpsys_wpf.ExampleFrontedControls;

public sealed class TeamCardFrontedControlContributor : IFrontedControlPluginContributor
{
    public const string PackageId = "top.plfjy.example.fronted";
    public const string ControlTypeName = "TeamCard";
    public const string FullControlType = "plugin:top.plfjy.example.fronted/TeamCard";

    public void RegisterFrontedControls(IFrontedControlPluginRegistry registry)
    {
        registry.Register(new FrontedPluginControlDescriptor<TeamCardFrontedControlConfig>
        {
            PackageId = PackageId,
            ControlTypeName = ControlTypeName,
            ConfigType = typeof(TeamCardFrontedControlConfig),
            CreateControl = CreateControl,
            CreateDefaultConfig = () => new TeamCardFrontedControlConfig(),
            DisplayNameKey = "ExamplePlugin.TeamCard.DisplayName",
            DescriptionKey = "ExamplePlugin.TeamCard.Description",
            Properties =
            [
                BindingProperty(nameof(TeamCardFrontedControlConfig.TeamNameBindingPath), FrontedBindingTargetKind.Text),
                BindingProperty(nameof(TeamCardFrontedControlConfig.LogoBindingPath), FrontedBindingTargetKind.Image),
                Property(nameof(TeamCardFrontedControlConfig.BackgroundColor), "Appearance", FrontedPropertyEditorKind.Color),
                Property(nameof(TeamCardFrontedControlConfig.ForegroundColor), "Appearance", FrontedPropertyEditorKind.Color),
                Property(nameof(TeamCardFrontedControlConfig.CornerRadius), "Appearance", FrontedPropertyEditorKind.Number),
                Property(nameof(TeamCardFrontedControlConfig.LogoSize), "Appearance", FrontedPropertyEditorKind.Number),
                Property(nameof(TeamCardFrontedControlConfig.FontSize), "Appearance", FrontedPropertyEditorKind.Number),
                new FrontedPluginPropertyDescriptor
                {
                    PropertyName = nameof(TeamCardFrontedControlConfig.FontWeight),
                    DisplayNameKey = "ExamplePlugin.TeamCard.FontWeight",
                    GroupName = "Appearance",
                    EditorKind = FrontedPropertyEditorKind.Enum,
                    Options =
                    [
                        new FrontedPropertyEditorOption { Value = "Normal", DisplayName = "Normal" },
                        new FrontedPropertyEditorOption { Value = "Bold", DisplayName = "Bold" },
                        new FrontedPropertyEditorOption { Value = "SemiBold", DisplayName = "SemiBold" },
                        new FrontedPropertyEditorOption { Value = "Light", DisplayName = "Light" },
                        new FrontedPropertyEditorOption { Value = "Medium", DisplayName = "Medium" },
                        new FrontedPropertyEditorOption { Value = "ExtraBold", DisplayName = "ExtraBold" }
                    ]
                }
            ]
        });
    }

    private static FrameworkElement CreateControl(
        string name,
        TeamCardFrontedControlConfig config,
        FrontedControlBuildContext context)
    {
        var border = new Border { Name = name };
        ApplyCanvasLayout(border, config);
        ApplyBrush(config.BackgroundColor, value => border.Background = value);
        border.CornerRadius = new CornerRadius(Math.Max(0, config.CornerRadius));
        border.Padding = new Thickness(12);

        var grid = new Grid { ClipToBounds = true };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var logo = new Image
        {
            Width = Math.Max(0, config.LogoSize),
            Height = Math.Max(0, config.LogoSize),
            Stretch = Stretch.UniformToFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (!string.IsNullOrWhiteSpace(config.LogoBindingPath))
        {
            BindingOperations.SetBinding(logo, Image.SourceProperty, new Binding(config.LogoBindingPath)
            {
                Source = context.SharedDataService
            });
        }

        var text = new TextBlock
        {
            Margin = new Thickness(14, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontSize = Math.Max(1, config.FontSize)
        };
        if (!string.IsNullOrWhiteSpace(config.TeamNameBindingPath))
        {
            BindingOperations.SetBinding(text, TextBlock.TextProperty, new Binding(config.TeamNameBindingPath)
            {
                Source = context.SharedDataService
            });
        }
        else
        {
            text.Text = "Team";
        }

        ApplyBrush(config.ForegroundColor, value => text.Foreground = value);
        ApplyFontWeight(config.FontWeight, value => text.FontWeight = value);

        Grid.SetColumn(logo, 0);
        Grid.SetColumn(text, 1);
        grid.Children.Add(logo);
        grid.Children.Add(text);
        border.Child = grid;
        return border;
    }

    private static FrontedPluginPropertyDescriptor BindingProperty(
        string propertyName,
        FrontedBindingTargetKind bindingTargetKind)
    {
        return new FrontedPluginPropertyDescriptor
        {
            PropertyName = propertyName,
            DisplayNameKey = $"ExamplePlugin.TeamCard.{propertyName}",
            GroupName = "Binding",
            EditorKind = FrontedPropertyEditorKind.Text,
            BindingTargetKind = bindingTargetKind
        };
    }

    private static FrontedPluginPropertyDescriptor Property(
        string propertyName,
        string groupName,
        FrontedPropertyEditorKind editorKind)
    {
        return new FrontedPluginPropertyDescriptor
        {
            PropertyName = propertyName,
            DisplayNameKey = $"ExamplePlugin.TeamCard.{propertyName}",
            GroupName = groupName,
            EditorKind = editorKind
        };
    }

    private static void ApplyCanvasLayout(FrameworkElement element, FrontedControlConfigBase config)
    {
        Canvas.SetLeft(element, config.Left);
        Canvas.SetTop(element, config.Top);
        Panel.SetZIndex(element, config.ZIndex);

        if (config.Width is > 0)
        {
            element.Width = config.Width.Value;
        }

        if (config.Height is > 0)
        {
            element.Height = config.Height.Value;
        }
    }

    private static void ApplyBrush(string value, Action<Brush> apply)
    {
        try
        {
            if (TypeDescriptor.GetConverter(typeof(Brush)).ConvertFromString(value) is Brush brush)
            {
                apply(brush);
            }
        }
        catch
        {
            // Keep the sample plugin forgiving during manual testing.
        }
    }

    private static void ApplyFontWeight(string value, Action<FontWeight> apply)
    {
        try
        {
            if (TypeDescriptor.GetConverter(typeof(FontWeight)).ConvertFromString(value) is FontWeight fontWeight)
            {
                apply(fontWeight);
            }
        }
        catch
        {
            // Keep the sample plugin forgiving during manual testing.
        }
    }
}
