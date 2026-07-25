using System.Collections;
using System.Reflection;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.StyleTransfer;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Properties;

/// <summary>
/// 内置控件根属性定义解析器，为内置控件提供属性 Schema。
/// </summary>
/// <remarks>
/// <para>
/// Text、Rectangle、Image、BorderedImage、MapV2Display、GlobalScoreRow 等内置控件
/// 通过 V3 注册表注册，其 Designer 属性编辑走通用 Schema 驱动路径。
/// 该解析器为这些控件提供 <see cref="FrontedV3PropertyDefinition"/> 列表。
/// </para>
/// <para>
/// 该解析器的核心契约：
/// <list type="bullet">
/// <item>不通过 <c>if (config is BorderedImage...)</c> 等类型分支选择属性构造路径。</item>
/// <item>分组、编辑器类型、可见性由属性名模式与属性类型推断，不依赖 Config 具体类型。</item>
/// <item>属性编辑通过 <see cref="FrontedV3PropertyDefinition.SetValue"/> 调用
/// <see cref="IFrontedV3StorageAccessor.SetValue"/>，不通过 propertyName 字符串反射写入。</item>
/// </list>
/// </para>
/// <para>
/// Storage 映射到 Config 的现有 CLR 属性（<see cref="FrontedV3Storage.ClrProperty"/>），
/// JSON 不变。
/// </para>
/// </remarks>
internal static class BuiltInPropertyDefinitionResolver
{
    private static readonly HashSet<string> ReservedPropertyNames = new(StringComparer.Ordinal)
    {
        nameof(FrontedControlConfigBase.BehaviorGuid),
        nameof(FrontedControlConfigBase.ControlType)
    };

    private static readonly HashSet<string> LayoutPropertyNames = new(StringComparer.Ordinal)
    {
        nameof(FrontedControlConfigBase.Left),
        nameof(FrontedControlConfigBase.Top),
        nameof(FrontedControlConfigBase.Width),
        nameof(FrontedControlConfigBase.Height),
        nameof(FrontedControlConfigBase.ZIndex)
    };

    private static readonly HashSet<string> ColorPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Color",
        "Foreground",
        "Background",
        "FillColor",
        "BorderColor"
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

    /// <summary>
    /// 返回给定 Config 可用的根属性定义列表。
    /// </summary>
    /// <param name="config">控件配置实例。</param>
    /// <returns>属性定义列表；无可用属性时返回空列表。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="config"/> 为 <see langword="null"/> 时抛出。</exception>
    public static IReadOnlyList<FrontedV3PropertyDefinition> GetProperties(FrontedControlConfigBase config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var properties = config.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(IsSupportedProperty)
            .Select(CreateDefinition)
            .Where(definition => definition is not null)
            .Cast<FrontedV3PropertyDefinition>()
            .ToList();

        return properties;
    }

    /// <summary>
    /// 返回给定 Config 是否有可用的 Schema 属性。
    /// </summary>
    /// <param name="config">控件配置实例。</param>
    /// <returns>有可用属性时为 <see langword="true"/>。</returns>
    public static bool HasProperties(FrontedControlConfigBase config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return GetProperties(config).Count > 0;
    }

    private static bool IsSupportedProperty(PropertyInfo property)
    {
        if (ReservedPropertyNames.Contains(property.Name))
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

        return type == typeof(string)
               || type == typeof(bool)
               || type.IsEnum
               || IsNumericType(type);
    }

    private static FrontedV3PropertyDefinition? CreateDefinition(PropertyInfo property)
    {
        var storage = FrontedV3Storage.ClrProperty(property.Name);
        var metadata = CreateMetadata(property);
        return new FrontedV3PropertyDefinition(
            optionsPath: property.Name,
            storage: storage,
            propertyType: property.PropertyType,
            metadata: metadata);
    }

    private static FrontedV3PropertyMetadata CreateMetadata(PropertyInfo property)
    {
        var groupName = ResolveGroupName(property.Name);
        var editorKind = ResolveEditorKind(property);
        var isVisible = IsVisibleByDefault(property.Name);
        var semantic = ResolveSemantic(property);

        return new FrontedV3PropertyMetadata
        {
            DisplayNameKey = property.Name,
            GroupName = groupName,
            EditorKind = editorKind,
            IsVisible = isVisible,
            Semantic = semantic
        };
    }

    /// <summary>
    /// 根据属性名推断 <see cref="FrontedV3PropertySemantic"/>，驱动 StyleTransfer 传播范围。
    /// </summary>
    /// <param name="property">控件 CLR 属性。</param>
    /// <returns>推断出的语义分类；未分类时为 <see cref="FrontedV3PropertySemantic.Other"/>。</returns>
    /// <remarks>
    /// <para>
    /// 推断规则（按优先级，先命中先返回）：
    /// <list type="number">
    /// <item><see cref="FrontedV3PropertySemantic.Effects"/>：<c>IsGaussianBlurEnabled</c>、<c>GaussianBlurRadius</c>。</item>
    /// <item><see cref="FrontedV3PropertySemantic.DataIdentity"/>：<c>MapKey</c>、<c>TeamType</c>、<c>BindingPath</c>、以 <c>BindingPath</c> 结尾、<c>ControlName</c>、<c>Name</c>。</item>
    /// <item><see cref="FrontedV3PropertySemantic.RootSize"/>：根级 <c>Width</c>、<c>Height</c>。</item>
    /// <item><see cref="FrontedV3PropertySemantic.Appearance"/>：颜色属性（<see cref="ColorPropertyNames"/> 或以 <c>Color</c> 结尾）、字体属性（<c>FontFamily</c>/<c>FontWeight</c>/<c>FontSize</c>）、<c>CornerRadius</c>、<c>ClipToBounds</c>。</item>
    /// <item><see cref="FrontedV3PropertySemantic.Other"/>：其余属性。</item>
    /// </list>
    /// </para>
    /// <para>
    /// 注意：<c>IsGaussianBlurEnabled</c> 与 <c>GaussianBlurRadius</c> 虽然在 <see cref="IsAppearanceProperty"/> 中返回 <see langword="true"/>，
    /// 但语义上属于视觉效果，因此特判为 <see cref="FrontedV3PropertySemantic.Effects"/>，不归入 <see cref="FrontedV3PropertySemantic.Appearance"/>。
    /// </para>
    /// <para>
    /// <c>BehaviorGuid</c> 在 <see cref="ReservedPropertyNames"/> 中已被排除，不会进入本方法；
    /// <c>Left</c>/<c>Top</c>/<c>ZIndex</c> 等根级保留字段无对应语义分类，落入 <see cref="FrontedV3PropertySemantic.Other"/>。
    /// </para>
    /// </remarks>
    private static FrontedV3PropertySemantic ResolveSemantic(PropertyInfo property)
    {
        var name = property.Name;

        // Effects: 高斯模糊视觉效果（特判，优先于 Appearance，因为 IsAppearanceProperty 也包含这两个名称）
        if (name is nameof(FrontedControlConfigBase.IsGaussianBlurEnabled)
            or nameof(FrontedControlConfigBase.GaussianBlurRadius))
        {
            return FrontedV3PropertySemantic.Effects;
        }

        // DataIdentity: 数据身份字段（永不传播，防止控件指向错误数据源）
        if (name is "MapKey" or "TeamType" or "ControlName" or "Name"
            || name == nameof(FrontedControlConfigBase.BindingPath)
            || name.EndsWith(nameof(FrontedControlConfigBase.BindingPath), StringComparison.Ordinal))
        {
            return FrontedV3PropertySemantic.DataIdentity;
        }

        // RootSize: 根级 Width/Height
        if (name is nameof(FrontedControlConfigBase.Width) or nameof(FrontedControlConfigBase.Height))
        {
            return FrontedV3PropertySemantic.RootSize;
        }

        // Appearance: 颜色、字体、CornerRadius、ClipToBounds
        if (IsAppearanceProperty(name))
        {
            return FrontedV3PropertySemantic.Appearance;
        }

        return FrontedV3PropertySemantic.Other;
    }

    private static string ResolveGroupName(string propertyName)
    {
        if (LayoutPropertyNames.Contains(propertyName))
        {
            return "Layout";
        }

        if (propertyName == nameof(FrontedControlConfigBase.BindingPath)
            || propertyName.EndsWith(nameof(FrontedControlConfigBase.BindingPath), StringComparison.Ordinal)
            || propertyName.EndsWith("ColorBindingPath", StringComparison.Ordinal))
        {
            return "Binding";
        }

        if (IsResourcePathProperty(propertyName))
        {
            return "Resource";
        }

        if (IsAppearanceProperty(propertyName))
        {
            return "Appearance";
        }

        return "ControlSpecific";
    }

    private static FrontedPropertyEditorKind? ResolveEditorKind(PropertyInfo property)
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

        if (property.PropertyType == typeof(string) && IsFontFamilyProperty(property.Name))
        {
            return FrontedPropertyEditorKind.FontFamily;
        }

        if (property.PropertyType == typeof(string) && TryGetStringOptions(property.Name, out _))
        {
            return FrontedPropertyEditorKind.Enum;
        }

        if (property.Name == nameof(FrontedControlConfigBase.IsGaussianBlurEnabled))
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

    private static bool IsVisibleByDefault(string propertyName)
    {
        // Hide deprecated or internal-only properties by name pattern.
        // This replaces the type-specific IsVisibleProperty checks in the legacy builder.
        if (propertyName is nameof(ShapeFrontedControlConfigBase.FillMode)
            or nameof(ShapeFrontedControlConfigBase.GradientStartColor)
            or nameof(ShapeFrontedControlConfigBase.UseGradientStartBinding)
            or nameof(ShapeFrontedControlConfigBase.GradientStartBindingPath)
            or nameof(ShapeFrontedControlConfigBase.UseFillBinding)
            or nameof(ShapeFrontedControlConfigBase.UseGradientEndBinding))
        {
            return false;
        }

        return true;
    }

    private static bool IsColorProperty(string propertyName) =>
        ColorPropertyNames.Contains(propertyName)
        || propertyName.EndsWith("Color", StringComparison.OrdinalIgnoreCase);

    private static bool IsFontFamilyProperty(string propertyName) =>
        propertyName.EndsWith("FontFamily", StringComparison.OrdinalIgnoreCase);

    private static bool IsAppearanceProperty(string propertyName) =>
        ColorPropertyNames.Contains(propertyName)
        || propertyName.EndsWith("Color", StringComparison.OrdinalIgnoreCase)
        || propertyName.EndsWith("FontFamily", StringComparison.OrdinalIgnoreCase)
        || propertyName.EndsWith("FontWeight", StringComparison.OrdinalIgnoreCase)
        || propertyName.EndsWith("FontSize", StringComparison.OrdinalIgnoreCase)
        || propertyName is "GaussianBlurRadius"
        or "IsGaussianBlurEnabled"
        or "CornerRadius"
        or "ClipToBounds";

    private static bool IsResourcePathProperty(string propertyName)
    {
        if (propertyName.EndsWith(nameof(FrontedControlConfigBase.BindingPath), StringComparison.Ordinal))
        {
            return false;
        }

        return ResourcePathPropertyNames.Contains(propertyName)
               || ResourcePathPropertyNames.Any(propertyName.EndsWith);
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

        options = [];
        return false;
    }

    private static Type GetCoreType(Type type) => Nullable.GetUnderlyingType(type) ?? type;

    private static bool IsNumericType(Type type) =>
        type == typeof(byte)
        || type == typeof(short)
        || type == typeof(int)
        || type == typeof(long)
        || type == typeof(float)
        || type == typeof(double)
        || type == typeof(decimal);
}
