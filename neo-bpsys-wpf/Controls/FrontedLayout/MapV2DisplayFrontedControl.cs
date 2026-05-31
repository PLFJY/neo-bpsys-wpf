using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

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
        private readonly ISettingsHostService _settingsHostService;
        private readonly MapV2Presenter _presenter = new();
        private bool _isSubscribed;

        public MapV2DisplayElement(
            string name,
            MapV2DisplayControlConfig config,
            ISharedDataService sharedDataService,
            ISettingsHostService settingsHostService,
            ILogger? logger)
        {
            _settingsHostService = settingsHostService;

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

            Child = _presenter;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_isSubscribed)
            {
                return;
            }

            _isSubscribed = true;
            _settingsHostService.SettingsChanged += OnSettingsChanged;
            BindSettings(_settingsHostService.Settings.WidgetsWindowSettings);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (!_isSubscribed)
            {
                return;
            }

            _isSubscribed = false;
            _settingsHostService.SettingsChanged -= OnSettingsChanged;
        }

        private void OnSettingsChanged(object? sender, Settings settings) =>
            BindSettings(settings.WidgetsWindowSettings);

        private void BindSettings(WidgetsWindowSettings settings)
        {
            BindingOperations.SetBinding(_presenter, MapV2Presenter.MapNameForegroundProperty, new Binding("TextSettings.MapBpV2_MapName.Foreground") { Source = settings });
            BindingOperations.SetBinding(_presenter, MapV2Presenter.MapNameFontSizeProperty, new Binding("TextSettings.MapBpV2_MapName.FontSize") { Source = settings });
            BindingOperations.SetBinding(_presenter, MapV2Presenter.MapNameFontFamilyProperty, new Binding("TextSettings.MapBpV2_MapName.FontFamily") { Source = settings });
            BindingOperations.SetBinding(_presenter, MapV2Presenter.MapNameFontWeightProperty, new Binding("TextSettings.MapBpV2_MapName.FontWeight") { Source = settings });
            BindingOperations.SetBinding(_presenter, MapV2Presenter.TeamNameForegroundProperty, new Binding("TextSettings.MapBpV2_TeamName.Foreground") { Source = settings });
            BindingOperations.SetBinding(_presenter, MapV2Presenter.TeamNameFontSizeProperty, new Binding("TextSettings.MapBpV2_TeamName.FontSize") { Source = settings });
            BindingOperations.SetBinding(_presenter, MapV2Presenter.TeamNameFontFamilyProperty, new Binding("TextSettings.MapBpV2_TeamName.FontFamily") { Source = settings });
            BindingOperations.SetBinding(_presenter, MapV2Presenter.TeamNameFontWeightProperty, new Binding("TextSettings.MapBpV2_TeamName.FontWeight") { Source = settings });
            BindingOperations.SetBinding(_presenter, MapV2Presenter.CampNameForegroundProperty, new Binding("TextSettings.MapBpV2_CampWords.Foreground") { Source = settings });
            BindingOperations.SetBinding(_presenter, MapV2Presenter.CampNameFontSizeProperty, new Binding("TextSettings.MapBpV2_CampWords.FontSize") { Source = settings });
            BindingOperations.SetBinding(_presenter, MapV2Presenter.CampNameFontFamilyProperty, new Binding("TextSettings.MapBpV2_CampWords.FontFamily") { Source = settings });
            BindingOperations.SetBinding(_presenter, MapV2Presenter.CampNameFontWeightProperty, new Binding("TextSettings.MapBpV2_CampWords.FontWeight") { Source = settings });
            BindingOperations.SetBinding(_presenter, MapV2Presenter.PickingBorderImageProperty, new Binding(nameof(WidgetsWindowSettings.MapBpV2PickBorderImage)) { Source = settings });
            BindingOperations.SetBinding(_presenter, MapV2Presenter.PickingBorderBrushProperty, new Binding(nameof(WidgetsWindowSettings.MapBpV2_PickingBorderBrush)) { Source = settings });
        }
    }
}
