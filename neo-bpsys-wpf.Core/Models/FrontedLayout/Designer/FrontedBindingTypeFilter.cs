using neo_bpsys_wpf.Core.Models;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// Type compatibility filter used by Designer v3 Binding Browser.
/// </summary>
public sealed class FrontedBindingTypeFilter
{
    /// <summary>
    /// Filter accepting any selectable value.
    /// </summary>
    public static FrontedBindingTypeFilter Any { get; } = new(FrontedBindingTargetKind.Any);

    /// <summary>
    /// Filter accepting text-compatible values.
    /// </summary>
    public static FrontedBindingTypeFilter Text { get; } = new(FrontedBindingTargetKind.Text);

    /// <summary>
    /// Filter accepting image-compatible values.
    /// </summary>
    public static FrontedBindingTypeFilter Image { get; } = new(FrontedBindingTargetKind.Image);

    /// <summary>
    /// Filter accepting game progress values.
    /// </summary>
    public static FrontedBindingTypeFilter GameProgress { get; } = new(FrontedBindingTargetKind.GameProgress);

    /// <summary>
    /// Filter accepting map values.
    /// </summary>
    public static FrontedBindingTypeFilter Map { get; } = new(FrontedBindingTargetKind.Map);

    /// <summary>
    /// Initializes a type filter for the specified target kind.
    /// </summary>
    public FrontedBindingTypeFilter(FrontedBindingTargetKind kind)
    {
        Kind = kind;
    }

    /// <summary>
    /// Expected binding target kind.
    /// </summary>
    public FrontedBindingTargetKind Kind { get; }

    /// <summary>
    /// Localization key for the target kind display name.
    /// </summary>
    public string DisplayNameKey => Kind switch
    {
        FrontedBindingTargetKind.Text => "TextBinding",
        FrontedBindingTargetKind.Image => "ImageBinding",
        FrontedBindingTargetKind.GameProgress => "GameProgressBinding",
        FrontedBindingTargetKind.Map => "MapBinding",
        FrontedBindingTargetKind.Boolean => "BooleanBinding",
        FrontedBindingTargetKind.Number => "NumberBinding",
        FrontedBindingTargetKind.String => "StringBinding",
        FrontedBindingTargetKind.Talent => "TalentBinding",
        FrontedBindingTargetKind.Trait => "TraitBinding",
        _ => "AnyBinding"
    };

    /// <summary>
    /// Returns whether a source value type is compatible with this target.
    /// </summary>
    public bool IsAllowed(Type? valueType)
    {
        if (valueType is null)
        {
            return Kind == FrontedBindingTargetKind.Any;
        }

        var coreType = Nullable.GetUnderlyingType(valueType) ?? valueType;
        return Kind switch
        {
            FrontedBindingTargetKind.Any => true,
            FrontedBindingTargetKind.Text => coreType == typeof(string) || IsNumericType(coreType),
            FrontedBindingTargetKind.Image => typeof(ImageSource).IsAssignableFrom(coreType),
            FrontedBindingTargetKind.GameProgress => coreType == typeof(Enums.GameProgress),
            FrontedBindingTargetKind.Map => coreType == typeof(Enums.Map),
            FrontedBindingTargetKind.Boolean => coreType == typeof(bool),
            FrontedBindingTargetKind.Number => IsNumericType(coreType),
            FrontedBindingTargetKind.String => coreType == typeof(string),
            FrontedBindingTargetKind.Talent => coreType == typeof(Talent),
            FrontedBindingTargetKind.Trait => coreType == typeof(Trait),
            _ => false
        };
    }

    private static bool IsNumericType(Type type)
    {
        return type == typeof(int)
               || type == typeof(double)
               || type == typeof(float)
               || type == typeof(decimal);
    }
}
