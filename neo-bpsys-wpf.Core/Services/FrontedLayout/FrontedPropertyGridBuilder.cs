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
/// 为选中的设计项构建设计器 v3 属性网格行。
/// </summary>
public partial class FrontedPropertyGridBuilder
{
    private readonly FrontedFontFamilyOptionProvider _fontFamilyOptionProvider;
    private readonly IFrontedDesignerLocalizationService _localizationService;
    private readonly IFrontedV3ControlRegistry? _v3ControlRegistry;

    /// <summary>
    /// 使用默认字体选项初始化属性网格构建器。
    /// </summary>
    public FrontedPropertyGridBuilder()
        : this(new FrontedFontFamilyOptionProvider(), new FrontedDesignerLocalizationService(), null)
    {
    }

    /// <summary>
    /// 使用自定义字体选项提供程序初始化属性网格构建器。
    /// </summary>
    public FrontedPropertyGridBuilder(FrontedFontFamilyOptionProvider fontFamilyOptionProvider)
        : this(fontFamilyOptionProvider, new FrontedDesignerLocalizationService(), null)
    {
    }

    /// <summary>
    /// 使用自定义字体选项和本地化初始化属性网格构建器。
    /// </summary>
    public FrontedPropertyGridBuilder(
        FrontedFontFamilyOptionProvider fontFamilyOptionProvider,
        IFrontedDesignerLocalizationService localizationService,
        IFrontedV3ControlRegistry? v3ControlRegistry = null)
    {
        _fontFamilyOptionProvider = fontFamilyOptionProvider;
        _localizationService = localizationService;
        _v3ControlRegistry = v3ControlRegistry;
    }

    private static readonly HashSet<string> CommonPropertyNames = new(StringComparer.Ordinal)
    {
        nameof(FrontedControlConfigBase.Left),
        nameof(FrontedControlConfigBase.Top),
        nameof(FrontedControlConfigBase.Width),
        nameof(FrontedControlConfigBase.Height),
        nameof(FrontedControlConfigBase.ZIndex),
        nameof(FrontedControlConfigBase.BindingPath)
    };

    private static readonly HashSet<string> ColorPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Color",
        "Foreground",
        "Background",
        "FillColor",
        "BorderColor",
        "ShadowColor",
        "GlowColor"
    };

    private static readonly HashSet<string> AppearancePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Color",
        "Foreground",
        "Background",
        "FillColor",
        "BorderColor",
        "FontFamily",
        "FontWeight",
        "FontSize",
        "HorizontalAlignment",
        "VerticalAlignment",
        "TextAlignment",
        "TextWrapping",
        "Stretch",
        "SizingMode",
        "CornerRadius",
        "ClipToBounds"
    };

    /// <summary>
    /// 视觉效果（高斯模糊、阴影、发光）属性名集合，统一归入 "Effects" 分组。
    /// </summary>
    /// <remarks>
    /// 这些属性原属外观分组，现独立为单独的"效果"分组，便于和外观属性区分编辑。
    /// </remarks>
    private static readonly HashSet<string> EffectPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(FrontedControlConfigBase.IsGaussianBlurEnabled),
        nameof(FrontedControlConfigBase.GaussianBlurRadius),
        nameof(FrontedControlConfigBase.IsShadowEnabled),
        nameof(FrontedControlConfigBase.ShadowColor),
        nameof(FrontedControlConfigBase.ShadowRadius),
        nameof(FrontedControlConfigBase.ShadowDepth),
        nameof(FrontedControlConfigBase.ShadowDirection),
        nameof(FrontedControlConfigBase.ShadowOpacity),
        nameof(FrontedControlConfigBase.IsGlowEnabled),
        nameof(FrontedControlConfigBase.GlowColor),
        nameof(FrontedControlConfigBase.GlowRadius),
        nameof(FrontedControlConfigBase.GlowOpacity)
    };

    private static readonly HashSet<string> ResourcePathPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ImagePath",
        "ImageSource",
        "SourcePath",
        "ResourcePath",
        "BackgroundImage",
        "LockImageSource",
        "LockImagePath",
        "BorderImagePath",
        "PickingBorderImagePath",
        "BanLockImagePath"
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<object>> StringOptionProperties =
        new Dictionary<string, IReadOnlyList<object>>(StringComparer.OrdinalIgnoreCase)
        {
            ["HorizontalAlignment"] = ["Left", "Center", "Right", "Stretch"],
            ["VerticalAlignment"] = ["Top", "Center", "Bottom", "Stretch"],
            ["TextAlignment"] = ["Left", "Center", "Right", "Justify"],
            ["TextWrapping"] = ["NoWrap", "Wrap", "WrapWithOverflow"],
            ["Stretch"] = ["None", "Fill", "Uniform", "UniformToFill"],
            ["FontWeight"] = ["Normal", "Bold", "SemiBold", "Light", "Medium", "ExtraBold"]
        };

    private static readonly IReadOnlyList<GameProgressTextDisplayMode> GameProgressDisplayModeOptions =
    [
        GameProgressTextDisplayMode.Inline,
        GameProgressTextDisplayMode.TwoLine,
        GameProgressTextDisplayMode.HorizontalGameOnly,
        GameProgressTextDisplayMode.HorizontalHalfOnly,
        GameProgressTextDisplayMode.Vertical,
        GameProgressTextDisplayMode.VerticalTwoLine,
        GameProgressTextDisplayMode.VerticalGameOnly,
        GameProgressTextDisplayMode.VerticalHalfOnly
    ];

    /// <summary>
    /// 为选中的设计项构建属性编辑器行。
    /// </summary>
    public ObservableCollection<FrontedPropertyEditorItem> Build(
        FrontedCanvasDesignDocument document,
        FrontedControlDesignItem selectedItem,
        FrontedLayoutValidator validator,
        FrontedLayoutReferenceScanner referenceScanner)
    {
        var messages = validator.Validate(document);
        referenceScanner.SetControls(document.Controls);

        var rows = new List<FrontedPropertyEditorItem>();
        AddIdentityRows(rows, selectedItem, messages);
        var firstConfigRowIndex = rows.Count;
        AddConfigRows(rows, selectedItem, messages);
        OrderConfigRows(rows, firstConfigRowIndex);
        MarkGroupHeaders(rows);
        return new ObservableCollection<FrontedPropertyEditorItem>(rows);
    }

    /// <summary>
    /// 基于 v3 Schema 属性定义构建属性编辑器行，不通过反射扫描 Config 类型。
    /// </summary>
    /// <param name="document">当前设计文档，用于校验。</param>
    /// <param name="selectedItem">选中的设计项。</param>
    /// <param name="validator">布局校验器。</param>
    /// <param name="referenceScanner">引用扫描器。</param>
    /// <param name="properties">选中目标的 Schema 属性定义列表。</param>
    /// <param name="schemaByPath">输出字典，填充 OptionsPath → 属性定义的映射，供 ApplyPropertyEdit 查找。</param>
    /// <returns>属性编辑器行集合。</returns>
    /// <remarks>
    /// <para>
    /// 该方法是 Root 控件属性网格的 Schema 驱动路径：属性行由
    /// <see cref="FrontedV3PropertyDefinition"/> 列表构造，属性值通过
    /// <see cref="FrontedV3PropertyDefinition.GetValue"/> 读取，编辑时通过
    /// <see cref="FrontedV3PropertyDefinition.SetValue"/> 写入，不通过 propertyName 字符串反射。
    /// </para>
    /// <para>
    /// 身份行（Name、ControlType）和缺失插件诊断行仍由本方法构造，因为它们不属于 Schema 属性。
    /// 其余 Config 级属性全部来自 Schema。
    /// </para>
    /// </remarks>
    public ObservableCollection<FrontedPropertyEditorItem> BuildFromSchema(
        FrontedCanvasDesignDocument document,
        FrontedControlDesignItem selectedItem,
        FrontedLayoutValidator validator,
        FrontedLayoutReferenceScanner referenceScanner,
        IReadOnlyList<FrontedV3PropertyDefinition> properties,
        IDictionary<string, FrontedV3PropertyDefinition> schemaByPath)
    {
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(schemaByPath);

        var messages = validator.Validate(document);
        referenceScanner.SetControls(document.Controls);

        var rows = new List<FrontedPropertyEditorItem>();
        AddIdentityRows(rows, selectedItem, messages);
        var firstConfigRowIndex = rows.Count;

        // 缺失插件：Registration 不存在时显示诊断行，不走 Schema。
        if (selectedItem.Config is PluginFrontedControlConfig missingPlugin
            && _v3ControlRegistry?.GetRegistration(selectedItem.Config.ControlType) is null)
        {
            AddMissingPluginRows(rows, selectedItem, missingPlugin, messages);
        }
        else
        {
            AddSchemaConfigRows(rows, selectedItem, messages, properties, schemaByPath);
        }

        OrderConfigRows(rows, firstConfigRowIndex);
        MarkGroupHeaders(rows);
        return new ObservableCollection<FrontedPropertyEditorItem>(rows);
    }

}
