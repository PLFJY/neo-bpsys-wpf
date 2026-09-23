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
/// 前台属性网格构建器的Bindings逻辑。
/// </summary>
public sealed partial class FrontedPropertyGridBuilder
{
    private static FrontedBindingTargetKind ResolveBindingTargetKind(
        FrontedControlConfigBase config,
        PropertyInfo property)
    {
        if (!IsBindingPathProperty(property.Name))
        {
            return FrontedBindingTargetKind.Any;
        }

        if (property.Name == nameof(ImageFrontedControlConfig.LockVisibilityBindingPath))
        {
            return FrontedBindingTargetKind.Boolean;
        }

        if (property.Name.EndsWith("ColorBindingPath", StringComparison.Ordinal))
        {
            return FrontedBindingTargetKind.String;
        }

        if (config is ShapeFrontedControlConfigBase)
        {
            return FrontedBindingTargetKind.String;
        }

        if (config is BackgroundTintFrontedControlConfigBase)
        {
            return FrontedBindingTargetKind.String;
        }

        return config switch
        {
            TextFrontedControlConfig => FrontedBindingTargetKind.Text,
            LocalizedTextControlConfig => FrontedBindingTargetKind.Text,
            ImageFrontedControlConfig => FrontedBindingTargetKind.Image,
            GameProgressTextControlConfig => FrontedBindingTargetKind.GameProgress,
            MapNameTextControlConfig => FrontedBindingTargetKind.Map,
            _ => FrontedBindingTargetKind.Any
        };
    }

    private static string ResolveBindingTargetTypeName(FrontedBindingTargetKind kind)
    {
        return kind switch
        {
            FrontedBindingTargetKind.Text => "Text",
            FrontedBindingTargetKind.Image => "ImageSource",
            FrontedBindingTargetKind.GameProgress => "GameProgress",
            FrontedBindingTargetKind.Map => "Map",
            FrontedBindingTargetKind.Boolean => "Boolean",
            FrontedBindingTargetKind.Number => "Number",
            FrontedBindingTargetKind.String => "String",
            FrontedBindingTargetKind.Talent => "Talent",
            FrontedBindingTargetKind.Trait => "Trait",
            _ => "Any"
        };
    }

    private static IReadOnlyList<string> ResolveAllowedBindingTypeNames(FrontedBindingTargetKind kind)
    {
        return kind switch
        {
            FrontedBindingTargetKind.Text => ["string", "number", "bool", "enum", "DateTime", "TimeSpan"],
            FrontedBindingTargetKind.Image => ["ImageSource", "BitmapSource", "BitmapImage"],
            FrontedBindingTargetKind.GameProgress => ["GameProgress", "GameProgress?"],
            FrontedBindingTargetKind.Map => ["Map", "Map?"],
            FrontedBindingTargetKind.Boolean => ["bool", "bool?"],
            FrontedBindingTargetKind.Number => ["int", "double", "float", "decimal"],
            FrontedBindingTargetKind.String => ["string"],
            FrontedBindingTargetKind.Talent => ["Talent"],
            FrontedBindingTargetKind.Trait => ["Trait"],
            _ => ["Any"]
        };
    }

}
