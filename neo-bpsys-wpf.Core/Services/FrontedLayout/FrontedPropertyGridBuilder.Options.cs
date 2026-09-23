using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;
using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 前台属性网格构建器的Options逻辑。
/// </summary>
public sealed partial class FrontedPropertyGridBuilder
{
    private static bool IsSupportedProperty(PropertyInfo property)
    {
        if (property.Name == nameof(FrontedControlConfigBase.BehaviorGuid))
        {
            return false;
        }

        if (property.GetIndexParameters().Length > 0 || !property.CanRead)
        {
            return false;
        }

        if (!property.CanWrite && property.Name != nameof(FrontedControlConfigBase.ControlType))
        {
            return false;
        }

        var type = GetCoreType(property.PropertyType);
        if (type == typeof(FrontedTextBindingExpression))
        {
            return true;
        }

        if (type == typeof(string)
            || type == typeof(bool)
            || type.IsEnum
            || IsNumericType(type))
        {
            return true;
        }

        return typeof(IEnumerable).IsAssignableFrom(type) && type == typeof(string);
    }

    private static FrontedPropertyEditorKind ResolveEditorKind(PropertyInfo property)
    {
        var type = GetCoreType(property.PropertyType);
        if (type == typeof(FrontedTextBindingExpression))
        {
            return FrontedPropertyEditorKind.TextBinding;
        }

        if (property.PropertyType == typeof(string) && IsColorProperty(property.Name))
        {
            return FrontedPropertyEditorKind.Color;
        }

        if (property.PropertyType == typeof(string)
            && IsFontFamilyProperty(property.Name))
        {
            return FrontedPropertyEditorKind.FontFamily;
        }

        if (property.PropertyType == typeof(string) && TryGetStringOptions(property.Name, out _))
        {
            return FrontedPropertyEditorKind.Enum;
        }

        if (property.Name is nameof(ImageFrontedControlConfig.Lockable)
            or nameof(ImageFrontedControlConfig.PickingBorderAvailable)
            or nameof(ImageFrontedControlConfig.UseIndependentLockStretch)
            or nameof(ImageFrontedControlConfig.UseIndependentPickingBorderStretch))
        {
            return FrontedPropertyEditorKind.ToggleSwitch;
        }

        if (property.Name == nameof(FrontedControlConfigBase.IsGaussianBlurEnabled))
        {
            return FrontedPropertyEditorKind.ToggleSwitch;
        }

        if (property.Name == nameof(FrontedControlConfigBase.IsShadowEnabled))
        {
            return FrontedPropertyEditorKind.ToggleSwitch;
        }

        if (property.Name == nameof(FrontedControlConfigBase.IsGlowEnabled))
        {
            return FrontedPropertyEditorKind.ToggleSwitch;
        }

        if (type == typeof(bool))
        {
            return FrontedPropertyEditorKind.Boolean;
        }

        if (type.IsEnum)
        {
            return FrontedPropertyEditorKind.Enum;
        }

        if (IsNumericType(type))
        {
            return FrontedPropertyEditorKind.Number;
        }

        return type == typeof(string)
            ? FrontedPropertyEditorKind.Text
            : FrontedPropertyEditorKind.ReadOnly;
    }

    private IReadOnlyList<object>? ResolveOptions(PropertyInfo property, FrontedPropertyEditorKind kind)
    {
        if (kind == FrontedPropertyEditorKind.FontFamily)
        {
            return GetFontFamilyOptions();
        }

        if (kind == FrontedPropertyEditorKind.Boolean)
        {
            return
            [
                CreateBooleanOption(true),
                CreateBooleanOption(false)
            ];
        }

        if (kind != FrontedPropertyEditorKind.Enum)
        {
            return null;
        }

        if (property.PropertyType == typeof(string)
            && TryGetStringOptions(property.Name, out var stringOptions))
        {
            return stringOptions
                .Select(value => CreateOption(GetOptionPropertyName(property.Name), value))
                .Cast<object>()
                .ToArray();
        }

        var values = Enum.GetValues(GetCoreType(property.PropertyType))
            .Cast<object>();

        if (property.PropertyType == typeof(GameProgressTextDisplayMode))
        {
            values = GameProgressDisplayModeOptions.Cast<object>();
        }

        // DisplayLanguage 使用共用的 LanguageKey 枚举，但排除全局的 System 值
        if (property.Name == nameof(GameProgressTextControlConfig.DisplayLanguage))
        {
            values = values.Where(v => v is not LanguageKey.System);
        }

        return values
            .Select(value => CreateOption(property.Name, value))
            .Cast<object>()
            .ToArray();
    }

    private static string ResolveGroupName(string propertyName, FrontedControlConfigBase config)
    {
        if (config is TextFrontedControlConfig
            && propertyName is nameof(TextFrontedControlConfig.Text)
                or nameof(TextFrontedControlConfig.TextBinding)
            || config is LocalizedTextControlConfig
            && propertyName is nameof(LocalizedTextControlConfig.LocalizationKey)
                or nameof(LocalizedTextControlConfig.FallbackText)
                or nameof(LocalizedTextControlConfig.TextBinding))
        {
            return "Content";
        }

        if (config is BorderedImageFrontedControlConfig)
        {
            if (propertyName is nameof(ImageFrontedControlConfig.Lockable)
                or nameof(ImageFrontedControlConfig.LockImagePath)
                or nameof(ImageFrontedControlConfig.UseIndependentLockStretch)
                or nameof(ImageFrontedControlConfig.LockStretch)
                or nameof(ImageFrontedControlConfig.LockVisibilityBindingPath)
                or nameof(ImageFrontedControlConfig.LockVisibleWhen)
                or nameof(ImageFrontedControlConfig.LockZIndexOffset)
                or nameof(ImageFrontedControlConfig.PickingBorderAvailable)
                or nameof(ImageFrontedControlConfig.PickingBorderImagePath)
                or nameof(ImageFrontedControlConfig.UseIndependentPickingBorderStretch)
                or nameof(ImageFrontedControlConfig.PickingBorderStretch)
                or nameof(ImageFrontedControlConfig.PickingBorderFillColor)
                or nameof(ImageFrontedControlConfig.PickingBorderZIndexOffset)
                or nameof(ImageFrontedControlConfig.PickingBorder)
                or nameof(ImageFrontedControlConfig.PickingBorderImagePath)
                or nameof(ImageFrontedControlConfig.BanLockAvailable)
                or nameof(ImageFrontedControlConfig.BanLockImagePath))
            {
                return "Overlay";
            }

            if (propertyName is nameof(FrontedControlConfigBase.Left)
                or nameof(FrontedControlConfigBase.Top)
                or nameof(FrontedControlConfigBase.Width)
                or nameof(FrontedControlConfigBase.Height)
                or nameof(FrontedControlConfigBase.ZIndex)
                or nameof(ImageFrontedControlConfig.CornerRadius)
                or nameof(ImageFrontedControlConfig.ClipToBounds))
            {
                return "Border";
            }

            if (propertyName is nameof(FrontedControlConfigBase.BindingPath)
                or nameof(ImageFrontedControlConfig.ImagePath)
                or nameof(BorderedImageFrontedControlConfig.ImageWidth)
                or nameof(BorderedImageFrontedControlConfig.ImageHeight)
                or nameof(ImageFrontedControlConfig.SizingMode)
                or nameof(ImageFrontedControlConfig.Stretch)
                or nameof(ImageFrontedControlConfig.HorizontalAlignment)
                or nameof(ImageFrontedControlConfig.VerticalAlignment))
            {
                return "Image";
            }
        }

        if (propertyName is nameof(FrontedControlConfigBase.Left)
            or nameof(FrontedControlConfigBase.Top)
            or nameof(FrontedControlConfigBase.Width)
            or nameof(FrontedControlConfigBase.Height)
            or nameof(FrontedControlConfigBase.ZIndex))
        {
            return "Layout";
        }

        if (propertyName == nameof(FrontedControlConfigBase.BindingPath)
            || propertyName == nameof(TextFrontedControlConfig.TextBinding))
        {
            return "Binding";
        }

        if (propertyName.EndsWith("ColorBindingPath", StringComparison.Ordinal))
        {
            return "Binding";
        }

        if (config is ShapeFrontedControlConfigBase)
        {
            // UseGradient is a toggle in the Appearance group, not a binding switch
            if (propertyName == nameof(ShapeFrontedControlConfigBase.UseGradient))
            {
                return "Appearance";
            }

            if (IsBindingPathProperty(propertyName)
                || propertyName.StartsWith("Use", StringComparison.Ordinal))
            {
                return "Binding";
            }

            if (propertyName is nameof(ShapeFrontedControlConfigBase.StrokeColor)
                or nameof(ShapeFrontedControlConfigBase.StrokeThickness))
            {
                return "Border";
            }

            if (propertyName is nameof(ShapeFrontedControlConfigBase.FillColor)
                or nameof(ShapeFrontedControlConfigBase.GradientEndColor)
                or nameof(ShapeFrontedControlConfigBase.GradientAngle))
            {
                return "Appearance";
            }
        }

        if (config is BackgroundTintFrontedControlConfigBase)
        {
            return propertyName == nameof(BackgroundTintFrontedControlConfigBase.TintBindingPath)
                ? "Binding"
                : "Appearance";
        }

        if (config is MapV2DisplayControlConfig
            && propertyName is nameof(MapV2DisplayControlConfig.MapBorderNormalColor)
                or nameof(MapV2DisplayControlConfig.MapBorderBannedColor))
        {
            return "Border";
        }

        if (config is ImageFrontedControlConfig
            && (propertyName is nameof(ImageFrontedControlConfig.Lockable)
                or nameof(ImageFrontedControlConfig.LockImagePath)
                or nameof(ImageFrontedControlConfig.UseIndependentLockStretch)
                or nameof(ImageFrontedControlConfig.LockStretch)
                or nameof(ImageFrontedControlConfig.LockVisibilityBindingPath)
                or nameof(ImageFrontedControlConfig.LockVisibleWhen)
                or nameof(ImageFrontedControlConfig.LockZIndexOffset)
                or nameof(ImageFrontedControlConfig.PickingBorderAvailable)
                or nameof(ImageFrontedControlConfig.PickingBorderImagePath)
                or nameof(ImageFrontedControlConfig.UseIndependentPickingBorderStretch)
                or nameof(ImageFrontedControlConfig.PickingBorderStretch)
                or nameof(ImageFrontedControlConfig.PickingBorderFillColor)
                or nameof(ImageFrontedControlConfig.PickingBorderZIndexOffset)))
        {
            return "Overlay";
        }

        if (IsResourcePathProperty(propertyName))
        {
            return "Resource";
        }

        if (EffectPropertyNames.Contains(propertyName))
        {
            return "Effects";
        }

        return IsAppearanceProperty(propertyName)
            ? "Appearance"
            : "ControlSpecific";
    }

    private static bool IsVisibleProperty(FrontedControlConfigBase config, string propertyName)
    {
        if (config is TextFrontedControlConfig or LocalizedTextControlConfig
            && propertyName == nameof(FrontedControlConfigBase.BindingPath))
        {
            return false;
        }

        if (config is ShapeFrontedControlConfigBase shapeConfig)
        {
            // Hide the base class BindingPath - shapes use dedicated FillBindingPath etc.
            if (propertyName == nameof(FrontedControlConfigBase.BindingPath))
            {
                return false;
            }

            // Hide FillMode enum - replaced by UseGradient toggle
            if (propertyName == nameof(ShapeFrontedControlConfigBase.FillMode))
            {
                return false;
            }

            // Hide deprecated gradient-start properties (FillColor serves dual purpose)
            if (propertyName is nameof(ShapeFrontedControlConfigBase.GradientStartColor)
                or nameof(ShapeFrontedControlConfigBase.UseGradientStartBinding)
                or nameof(ShapeFrontedControlConfigBase.GradientStartBindingPath))
            {
                return false;
            }

            // Hide binding toggles - binding is automatic when path has a value
            if (propertyName is nameof(ShapeFrontedControlConfigBase.UseFillBinding)
                or nameof(ShapeFrontedControlConfigBase.UseGradientEndBinding))
            {
                return false;
            }

            // When gradient is off, hide gradient-end and angle properties
            var useGradient = shapeConfig.UseGradient || shapeConfig.FillMode == ShapeFillMode.LinearGradient;
            if (!useGradient)
            {
                if (propertyName is nameof(ShapeFrontedControlConfigBase.GradientEndColor)
                    or nameof(ShapeFrontedControlConfigBase.GradientEndBindingPath)
                    or nameof(ShapeFrontedControlConfigBase.GradientAngle))
                {
                    return false;
                }
            }
        }

        if (config is BackgroundTintFrontedControlConfigBase
            && propertyName == nameof(FrontedControlConfigBase.BindingPath))
        {
            return false;
        }

        if (config is ImageFrontedControlConfig and not BorderedImageFrontedControlConfig)
        {
            return propertyName is not nameof(ImageFrontedControlConfig.SizingMode)
                and not nameof(ImageFrontedControlConfig.PickingBorder)
                and not nameof(ImageFrontedControlConfig.BanLockAvailable)
                and not nameof(ImageFrontedControlConfig.BanLockImagePath);
        }

        if (config is BorderedImageFrontedControlConfig)
        {
            return propertyName is not nameof(ImageFrontedControlConfig.PickingBorder)
                and not nameof(ImageFrontedControlConfig.BanLockAvailable)
                and not nameof(ImageFrontedControlConfig.BanLockImagePath);
        }

        return true;
    }

    private static bool IsBindingPathProperty(string propertyName) =>
        propertyName.Equals(nameof(FrontedControlConfigBase.BindingPath), StringComparison.Ordinal)
        || propertyName.EndsWith(nameof(FrontedControlConfigBase.BindingPath), StringComparison.Ordinal);

    private static bool IsResourcePathProperty(string propertyName)
    {
        if (IsBindingPathProperty(propertyName))
        {
            return false;
        }

        return ResourcePathPropertyNames.Contains(propertyName)
               || ResourcePathPropertyNames.Any(propertyName.EndsWith);
    }

    private static bool IsColorProperty(string propertyName) =>
        ColorPropertyNames.Contains(propertyName) || propertyName.EndsWith("Color", StringComparison.OrdinalIgnoreCase);

    private static bool IsColorOverriddenByBinding(FrontedControlConfigBase config, string propertyName) =>
        propertyName == nameof(TextFrontedControlConfig.Color)
        && config switch
        {
            TextFrontedControlConfig text => !string.IsNullOrWhiteSpace(text.ColorBindingPath),
            LocalizedTextControlConfig localizedText => !string.IsNullOrWhiteSpace(localizedText.ColorBindingPath),
            GameProgressTextControlConfig gameProgressText => !string.IsNullOrWhiteSpace(gameProgressText.ColorBindingPath),
            MapNameTextControlConfig mapNameText => !string.IsNullOrWhiteSpace(mapNameText.ColorBindingPath),
            _ => false
        };

    private static bool IsFontFamilyProperty(string propertyName) =>
        propertyName.EndsWith("FontFamily", StringComparison.OrdinalIgnoreCase);

    private static bool IsAppearanceProperty(string propertyName) =>
        AppearancePropertyNames.Contains(propertyName)
        || propertyName.EndsWith("Color", StringComparison.OrdinalIgnoreCase)
        || propertyName.EndsWith("FontFamily", StringComparison.OrdinalIgnoreCase)
        || propertyName.EndsWith("FontWeight", StringComparison.OrdinalIgnoreCase)
        || propertyName.EndsWith("FontSize", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 判断属性是否为视觉效果（高斯模糊、阴影、发光）的细节属性。
    /// </summary>
    /// <param name="propertyName">属性名。</param>
    /// <returns>属于效果细节属性时为 <see langword="true"/>。</returns>
    /// <remarks>
    /// 效果细节属性在对应开关关闭时应以禁用状态展示（保留原编辑器类型以正确显示值），
    /// 而非切换为 <see cref="FrontedPropertyEditorKind.ReadOnly"/>。
    /// </remarks>
    private static bool IsEffectDetailProperty(string propertyName) =>
        propertyName is nameof(FrontedControlConfigBase.GaussianBlurRadius)
            or nameof(FrontedControlConfigBase.ShadowColor)
            or nameof(FrontedControlConfigBase.ShadowRadius)
            or nameof(FrontedControlConfigBase.ShadowDepth)
            or nameof(FrontedControlConfigBase.ShadowDirection)
            or nameof(FrontedControlConfigBase.ShadowOpacity)
            or nameof(FrontedControlConfigBase.GlowColor)
            or nameof(FrontedControlConfigBase.GlowRadius)
            or nameof(FrontedControlConfigBase.GlowOpacity);

    /// <summary>
    /// 判断效果细节属性是否因对应开关关闭而处于禁用状态。
    /// </summary>
    /// <param name="propertyName">效果细节属性名。</param>
    /// <param name="config">控件配置实例，用于读取开关状态。</param>
    /// <returns>属性应禁用时为 <see langword="true"/>；非效果细节属性或开关已开启时为 <see langword="false"/>。</returns>
    private static bool IsEffectDetailDisabled(string propertyName, FrontedControlConfigBase config)
    {
        if (propertyName == nameof(FrontedControlConfigBase.GaussianBlurRadius))
        {
            return !config.IsGaussianBlurEnabled;
        }

        if (propertyName is nameof(FrontedControlConfigBase.ShadowColor)
            or nameof(FrontedControlConfigBase.ShadowRadius)
            or nameof(FrontedControlConfigBase.ShadowDepth)
            or nameof(FrontedControlConfigBase.ShadowDirection)
            or nameof(FrontedControlConfigBase.ShadowOpacity))
        {
            return !config.IsShadowEnabled;
        }

        if (propertyName is nameof(FrontedControlConfigBase.GlowColor)
            or nameof(FrontedControlConfigBase.GlowRadius)
            or nameof(FrontedControlConfigBase.GlowOpacity))
        {
            return !config.IsGlowEnabled;
        }

        return false;
    }

    private static bool IsOverlayStretchEditingDisabled(
        string propertyName,
        FrontedControlConfigBase config)
    {
        if (config is not ImageFrontedControlConfig imageConfig)
        {
            return false;
        }

        return propertyName switch
        {
            nameof(ImageFrontedControlConfig.LockStretch) =>
                !imageConfig.UseIndependentLockStretch,
            nameof(ImageFrontedControlConfig.PickingBorderStretch) =>
                !imageConfig.UseIndependentPickingBorderStretch,
            _ => false
        };
    }

    private static string? ResolveOverlaySectionName(
        string propertyName,
        FrontedControlConfigBase config)
    {
        if (config is not ImageFrontedControlConfig)
        {
            return null;
        }

        return propertyName switch
        {
            nameof(ImageFrontedControlConfig.Lockable)
                or nameof(ImageFrontedControlConfig.LockImagePath)
                or nameof(ImageFrontedControlConfig.UseIndependentLockStretch)
                or nameof(ImageFrontedControlConfig.LockStretch)
                or nameof(ImageFrontedControlConfig.LockVisibilityBindingPath)
                or nameof(ImageFrontedControlConfig.LockVisibleWhen)
                or nameof(ImageFrontedControlConfig.LockZIndexOffset)
                or nameof(ImageFrontedControlConfig.BanLockAvailable)
                or nameof(ImageFrontedControlConfig.BanLockImagePath) => "BanLock",
            nameof(ImageFrontedControlConfig.PickingBorderAvailable)
                or nameof(ImageFrontedControlConfig.PickingBorderImagePath)
                or nameof(ImageFrontedControlConfig.UseIndependentPickingBorderStretch)
                or nameof(ImageFrontedControlConfig.PickingBorderStretch)
                or nameof(ImageFrontedControlConfig.PickingBorderFillColor)
                or nameof(ImageFrontedControlConfig.PickingBorderZIndexOffset)
                or nameof(ImageFrontedControlConfig.PickingBorder) => "PickingBorder",
            _ => null
        };
    }

    private static bool TryGetStringOptions(string propertyName, out IReadOnlyList<object> options)
    {
        if (StringOptionProperties.TryGetValue(propertyName, out options!))
        {
            return true;
        }

        if (propertyName.EndsWith("FontWeight", StringComparison.OrdinalIgnoreCase)
            && StringOptionProperties.TryGetValue("FontWeight", out options!))
        {
            return true;
        }

        if (propertyName.EndsWith("Stretch", StringComparison.OrdinalIgnoreCase)
            && StringOptionProperties.TryGetValue("Stretch", out options!))
        {
            return true;
        }

        options = [];
        return false;
    }

    private static string GetOptionPropertyName(string propertyName)
    {
        if (propertyName.EndsWith("FontWeight", StringComparison.OrdinalIgnoreCase))
        {
            return "FontWeight";
        }

        if (propertyName.EndsWith("Stretch", StringComparison.OrdinalIgnoreCase))
        {
            return "Stretch";
        }

        return propertyName;
    }

    private static int GetPropertyOrder(PropertyInfo property)
    {
        if (CommonPropertyNames.Contains(property.Name))
        {
            return property.Name switch
            {
                nameof(FrontedControlConfigBase.Left) => 10,
                nameof(FrontedControlConfigBase.Top) => 11,
                nameof(FrontedControlConfigBase.Width) => 12,
                nameof(FrontedControlConfigBase.Height) => 13,
                nameof(FrontedControlConfigBase.ZIndex) => 14,
                nameof(FrontedControlConfigBase.BindingPath) => 20,
                _ => 30
            };
        }

        var effectOrder = GetAppearancePropertyOrder(property.Name);
        if (effectOrder > 0)
        {
            return effectOrder;
        }

        return property.DeclaringType == typeof(FrontedControlConfigBase)
            ? 30
            : 100 + property.MetadataToken;
    }

    private static int GetGroupOrder(string groupName) => groupName switch
    {
        "Layout" => 10,
        "Binding" => 20,
        "Resource" => 30,
        "Image" => 40,
        "Border" => 50,
        "Overlay" => 60,
        "Appearance" => 70,
        "Effects" => 75,
        "Content" => 80,
        "ControlSpecific" => 90,
        _ => 100
    };

    private static void OrderConfigRows(List<FrontedPropertyEditorItem> rows, int firstConfigRowIndex)
    {
        var orderedRows = rows
            .Skip(firstConfigRowIndex)
            .Select((row, index) => new { Row = row, Index = index })
            .OrderBy(item => GetGroupOrder(item.Row.GroupName))
            .ThenBy(item => GetAppearancePropertyOrder(item.Row.PropertyName))
            .ThenBy(item => item.Index)
            .Select(item => item.Row)
            .ToArray();

        for (var index = 0; index < orderedRows.Length; index++)
        {
            rows[firstConfigRowIndex + index] = orderedRows[index];
        }
    }

    private static int GetAppearancePropertyOrder(string propertyName) => propertyName switch
    {
        nameof(FrontedControlConfigBase.IsGaussianBlurEnabled) => 9000,
        nameof(FrontedControlConfigBase.GaussianBlurRadius) => 9001,
        nameof(FrontedControlConfigBase.IsShadowEnabled) => 9100,
        nameof(FrontedControlConfigBase.ShadowColor) => 9101,
        nameof(FrontedControlConfigBase.ShadowRadius) => 9102,
        nameof(FrontedControlConfigBase.ShadowDepth) => 9103,
        nameof(FrontedControlConfigBase.ShadowDirection) => 9104,
        nameof(FrontedControlConfigBase.ShadowOpacity) => 9105,
        nameof(FrontedControlConfigBase.IsGlowEnabled) => 9200,
        nameof(FrontedControlConfigBase.GlowColor) => 9201,
        nameof(FrontedControlConfigBase.GlowRadius) => 9202,
        nameof(FrontedControlConfigBase.GlowOpacity) => 9203,
        _ => 0
    };
}
