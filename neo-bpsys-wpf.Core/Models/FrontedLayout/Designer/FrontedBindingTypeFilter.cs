using neo_bpsys_wpf.Core.Models;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// 设计器 v3 绑定浏览器使用的类型兼容性过滤器。
/// </summary>
public sealed class FrontedBindingTypeFilter
{
    /// <summary>
    /// 接受任何可选值的过滤器。
    /// </summary>
    public static FrontedBindingTypeFilter Any { get; } = new(FrontedBindingTargetKind.Any);

    /// <summary>
    /// 接受文本兼容值的过滤器。
    /// </summary>
    public static FrontedBindingTypeFilter Text { get; } = new(FrontedBindingTargetKind.Text);

    /// <summary>
    /// 接受图像兼容值的过滤器。
    /// </summary>
    public static FrontedBindingTypeFilter Image { get; } = new(FrontedBindingTargetKind.Image);

    /// <summary>
    /// 接受对局进度值的过滤器。
    /// </summary>
    public static FrontedBindingTypeFilter GameProgress { get; } = new(FrontedBindingTargetKind.GameProgress);

    /// <summary>
    /// 接受地图值的过滤器。
    /// </summary>
    public static FrontedBindingTypeFilter Map { get; } = new(FrontedBindingTargetKind.Map);

    /// <summary>
    /// 为指定目标类别初始化类型过滤器。
    /// </summary>
    public FrontedBindingTypeFilter(FrontedBindingTargetKind kind)
    {
        Kind = kind;
    }

    /// <summary>
    /// 预期的绑定目标类别。
    /// </summary>
    public FrontedBindingTargetKind Kind { get; }

    /// <summary>
    /// 目标类别显示名称的本地化键。
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
    /// 返回源值类型是否与此目标兼容。
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
            FrontedBindingTargetKind.Text => coreType == typeof(string)
                                             || coreType == typeof(bool)
                                             || coreType == typeof(DateTime)
                                             || coreType == typeof(TimeSpan)
                                             || coreType.IsEnum
                                             || IsNumericType(coreType),
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
               || type == typeof(byte)
               || type == typeof(short)
               || type == typeof(long)
               || type == typeof(double)
               || type == typeof(float)
               || type == typeof(decimal);
    }
}
