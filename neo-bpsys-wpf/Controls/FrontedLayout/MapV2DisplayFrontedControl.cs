using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace neo_bpsys_wpf.Controls.FrontedLayout;

/// <summary>
/// 内置 v3 地图 BP v2 展示控件工厂。
/// </summary>
public class MapV2DisplayFrontedControl(ILogger<MapV2DisplayFrontedControl>? logger = null) : IFrontedControl
{
    private readonly ILogger<MapV2DisplayFrontedControl>? _logger = logger;

    /// <inheritdoc />
    public string ControlType => "MapV2Display";

    /// <inheritdoc />
    public Type ConfigType => typeof(MapV2DisplayControlConfig);

    /// <inheritdoc />
    public FrameworkElement Create(
        string name,
        FrontedControlConfigBase config,
        FrontedControlBuildContext context)
    {
        if (config is not MapV2DisplayControlConfig mapConfig)
        {
            throw new FrontedLayoutConfigException($"Control '{name}' config is not a MapV2Display config.");
        }

        var settingsHostService = context.Services.GetRequiredService<ISettingsHostService>();
        return new MapV2DisplayElement(
            name,
            mapConfig,
            context.SharedDataService,
            settingsHostService,
            _logger ?? context.Logger);
    }

    private sealed class MapV2DisplayElement : Border
    {
        private readonly MapV2Presenter _presenter = new();

        public MapV2DisplayElement(
            string name,
            MapV2DisplayControlConfig config,
            ISharedDataService sharedDataService,
            ISettingsHostService settingsHostService,
            ILogger? logger)
        {
            var outer = CutSceneFrontedControlHelper.CreateOuterBorder(name, config);
            Name = outer.Name;
            Width = outer.Width;
            Height = outer.Height;
            Canvas.SetLeft(this, Canvas.GetLeft(outer));
            Canvas.SetTop(this, Canvas.GetTop(outer));
            Panel.SetZIndex(this, Panel.GetZIndex(outer));

            if (string.IsNullOrWhiteSpace(config.MapKey)
                || !sharedDataService.CurrentGame.MapV2Dictionary.ContainsKey(config.MapKey))
            {
                logger?.LogWarning(
                    "Invalid MapV2Display MapKey. Control: {ControlName}, MapKey: {MapKey}",
                    name,
                    config.MapKey);
                return;
            }

            BindingOperations.SetBinding(_presenter, MapV2Presenter.MapProperty, new Binding($"CurrentGame.MapV2Dictionary[{config.MapKey}]")
            {
                Source = sharedDataService
            });

            _presenter.HorizontalAlignment = HorizontalAlignment.Stretch;
            _presenter.VerticalAlignment = VerticalAlignment.Stretch;
            BindingOperations.SetBinding(_presenter, WidthProperty, new Binding(nameof(ActualWidth))
            {
                Source = this
            });
            BindingOperations.SetBinding(_presenter, HeightProperty, new Binding(nameof(ActualHeight))
            {
                Source = this
            });

            ApplyDefaultPresenterStyle();
            Child = _presenter;
        }

        private void ApplyDefaultPresenterStyle()
        {
            var defaultFont = new FontFamily("Arial");
            _presenter.MapNameForeground = Brushes.White;
            _presenter.MapNameFontSize = 14;
            _presenter.MapNameFontFamily = defaultFont;
            _presenter.MapNameFontWeight = FontWeights.Normal;
            _presenter.TeamNameForeground = Brushes.White;
            _presenter.TeamNameFontSize = 18;
            _presenter.TeamNameFontFamily = defaultFont;
            _presenter.TeamNameFontWeight = FontWeights.Normal;
            _presenter.CampNameForeground = Brushes.White;
            _presenter.CampNameFontSize = 20;
            _presenter.CampNameFontFamily = defaultFont;
            _presenter.CampNameFontWeight = FontWeights.Normal;
            _presenter.PickingBorderBrush = Brushes.White;
            _presenter.PickingBorderImage = ImageHelper.GetUiImageSource("pickingBorder");
        }
    }
}
