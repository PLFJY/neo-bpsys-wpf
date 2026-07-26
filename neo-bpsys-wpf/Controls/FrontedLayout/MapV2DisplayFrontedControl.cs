using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Abstractions.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace neo_bpsys_wpf.Controls.FrontedLayout;

/// <summary>
/// 内置 v3 地图 BP v2 展示控件。
/// </summary>
[FrontedV3Control("MapV2Display", IsBuiltIn = true, SupportsPeerStyleTransfer = true)]
public class MapV2DisplayFrontedControl : FrontedV3ControlBase
{
    /// <inheritdoc />
    protected override void OnInitializeFrontedV3(FrontedV3ControlContext context)
    {
        if (context.Config is not MapV2DisplayControlConfig mapConfig)
        {
            throw new FrontedLayoutConfigException("Control config is not a MapV2Display config.");
        }

        var settingsHostService = context.Services.GetRequiredService<ISettingsHostService>();
        var element = new MapV2DisplayElement(
            context.ControlName ?? string.Empty,
            mapConfig,
            context.SharedDataService,
            settingsHostService,
            context.ResourceResolver,
            context.Logger);
        Content = element;
    }

    private sealed class MapV2DisplayElement : Border
    {
        private readonly MapV2Presenter _presenter = new();

        public MapV2DisplayElement(
            string name,
            MapV2DisplayControlConfig config,
            ISharedDataService sharedDataService,
            ISettingsHostService settingsHostService,
            IFrontedResourceResolver resourceResolver,
            ILogger? logger)
        {
            Name = name;

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

            ApplyPresenterStyle(config, resourceResolver, logger);
            MapV2InternalPartLayoutHelper.EnsureParts(config);
            _presenter.ApplyInternalPartLayout(config.InternalParts);
            MarkPickingBorderPart(name, config);
            Child = _presenter;
        }

        private void MarkPickingBorderPart(string controlName, MapV2DisplayControlConfig config)
        {
            if (config.BehaviorGuid == Guid.Empty)
            {
                return;
            }

            var pickingBorder = _presenter.PickingBorderAnimationTarget;
            FrontedRendererProperties.SetIsGeneratedControl(pickingBorder, true);
            FrontedRendererProperties.SetIsAnimationAuxiliaryElement(pickingBorder, true);
            FrontedRendererProperties.SetParentBehaviorGuid(pickingBorder, config.BehaviorGuid);
            FrontedRendererProperties.SetParentRegisteredName(pickingBorder, controlName);
            FrontedRendererProperties.SetAnimationPartName(pickingBorder, FrontedAnimationPartNames.PickingBorder);
            FrontedRendererProperties.SetAnimationPartParent(pickingBorder, _presenter);
            FrontedRendererProperties.SetRegisteredName(pickingBorder, $"{controlName}__{FrontedAnimationPartNames.PickingBorder}");
        }

        private void ApplyPresenterStyle(
            MapV2DisplayControlConfig config,
            IFrontedResourceResolver resourceResolver,
            ILogger? logger)
        {
            _presenter.MapNameForeground = ResolveBrush(config.MapNameColor, logger);
            _presenter.MapNameFontSize = config.MapNameFontSize > 0 ? config.MapNameFontSize : 14;
            _presenter.MapNameFontFamily = ResolveFontFamily(config.MapNameFontFamily, logger);
            _presenter.MapNameFontWeight = ResolveFontWeight(config.MapNameFontWeight, logger);

            _presenter.TeamNameForeground = ResolveBrush(config.TeamNameColor, logger);
            _presenter.TeamNameFontSize = config.TeamNameFontSize > 0 ? config.TeamNameFontSize : 18;
            _presenter.TeamNameFontFamily = ResolveFontFamily(config.TeamNameFontFamily, logger);
            _presenter.TeamNameFontWeight = ResolveFontWeight(config.TeamNameFontWeight, logger);

            _presenter.CampNameForeground = ResolveBrush(config.CampNameColor, logger);
            _presenter.CampNameFontSize = config.CampNameFontSize > 0 ? config.CampNameFontSize : 20;
            _presenter.CampNameFontFamily = ResolveFontFamily(config.CampNameFontFamily, logger);
            _presenter.CampNameFontWeight = ResolveFontWeight(config.CampNameFontWeight, logger);

            _presenter.MapBorderNormalBrush = ResolveBrush(config.MapBorderNormalColor, logger, "#2B483B");
            _presenter.MapBorderBannedBrush = ResolveBrush(config.MapBorderBannedColor, logger, "#9C3E2F");
            _presenter.PickingBorderBrush = ResolveBrush(config.PickingBorderFillColor, logger);
            _presenter.PickingBorderImage = !string.IsNullOrWhiteSpace(config.PickingBorderImagePath)
                ? resourceResolver.ResolveImage(config.PickingBorderImagePath, FrontedImagePurpose.UiElement)
                  ?? ImageHelper.GetUiImageSource("pickingBorder")
                : ImageHelper.GetUiImageSource("pickingBorder");
        }

        private static Brush ResolveBrush(string? value, ILogger? logger)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Brushes.White;
            }

            try
            {
                return (Brush)new BrushConverter().ConvertFromString(value)!;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Invalid MapV2Display color value: {Color}", value);
                return Brushes.White;
            }
        }

        private static Brush ResolveBrush(string? value, ILogger? logger, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return (Brush)new BrushConverter().ConvertFromString(fallback)!;
            }

            try
            {
                return (Brush)new BrushConverter().ConvertFromString(value)!;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Invalid MapV2Display color value: {Color}", value);
                return (Brush)new BrushConverter().ConvertFromString(fallback)!;
            }
        }

        private static FontFamily ResolveFontFamily(string? value, ILogger? logger)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new FontFamily("Arial");
            }

            try
            {
                return FrontedFontResourceHelper.CreateFontFamily(value, logger: logger);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Invalid MapV2Display FontFamily value: {FontFamily}", value);
                return new FontFamily("Arial");
            }
        }

        private static FontWeight ResolveFontWeight(string? value, ILogger? logger)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return FontWeights.Normal;
            }

            try
            {
                if (new FontWeightConverter().ConvertFromString(value) is FontWeight fontWeight)
                {
                    return fontWeight;
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Invalid MapV2Display FontWeight value: {FontWeight}", value);
            }

            return FontWeights.Normal;
        }
    }
}
