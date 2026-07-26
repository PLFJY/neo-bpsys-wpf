using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.Core.Services.Registry;

namespace neo_bpsys_wpf.Core.Extensions.Registry;

/// <summary>
/// v3 前台控件注册扩展方法。
/// </summary>
/// <remarks>
/// <para>
/// 插件在 <c>PluginBase.Initialize</c> 中通过 <see cref="AddFrontedV3Control{TControl}(IServiceCollection)"/>
/// 注册自定义控件。插件作者只需传入控件类型，<c>PackageId</c> 从插件注册上下文自动获得，
/// 不得传入 <c>CanonicalControlType</c>、<c>Config factory</c>、<c>CreateControl delegate</c> 或 <c>Property descriptor list</c>。
/// </para>
/// <para>
/// 控件类型必须标注 <see cref="FrontedV3ControlAttribute"/> 并继承 <see cref="FrontedV3ControlBase"/>。
/// 属性通过控件类上的 <c>public static readonly FrontedV3Property&lt;T&gt;</c> 字段声明，
/// 框架在注册时通过反射发现并转换为 <see cref="FrontedV3PropertyDefinition"/>。
/// </para>
/// <para>
/// PluginSdk 项目本身只是构建期空壳（提供 .targets 与打包工具），不携带运行时程序集；
/// 所有插件 API 类型均定义在 Core 程序集中。
/// </para>
/// </remarks>
public static class FrontedV3ControlRegistryExtensions
{
    /// <summary>
    /// 注册一个 v3 前台控件到 DI 容器中。
    /// </summary>
    /// <typeparam name="TControl">控件类型，必须继承 <see cref="FrontedV3ControlBase"/> 并标注 <see cref="FrontedV3ControlAttribute"/>。</typeparam>
    /// <param name="services">服务容器。</param>
    /// <returns>服务容器，支持链式调用。</returns>
    /// <exception cref="FrontedLayoutConfigException">当控件类型缺少 <see cref="FrontedV3ControlAttribute"/>、
    /// ControlId 不合法、插件试图注册为内置控件、属性 OptionsPath 重复、属性 Storage 指向保留字段、
    /// OptionsPath 使用禁止路径、Part/PartCollection Id 非法或重复、Part Capabilities 与 Storage 配对不一致、
    /// 或 PartCollection 策略/Templates 与回调配对不一致时抛出。</exception>
    public static IServiceCollection AddFrontedV3Control<TControl>(this IServiceCollection services)
        where TControl : FrontedV3ControlBase
    {
        return services.AddFrontedV3ControlCore(typeof(TControl));
    }

    private static IServiceCollection AddFrontedV3ControlCore(
        this IServiceCollection services,
        Type controlType)
    {
        var attribute = controlType.GetCustomAttribute<FrontedV3ControlAttribute>()
            ?? throw new FrontedLayoutConfigException(
                $"Control type '{controlType.FullName}' must be annotated with [FrontedV3Control].");

        FrontedV3ControlIdValidator.EnsureValidControlId(attribute.ControlId);

        var packageId = FrontedPluginRegistrationContext.CurrentPackageId;
        var isBuiltIn = attribute.IsBuiltIn;

        if (isBuiltIn && packageId is not null)
        {
            throw new FrontedLayoutConfigException(
                $"Plugin '{packageId}' cannot register control '{attribute.ControlId}' as built-in. " +
                "Only the host can register built-in controls.");
        }

        var canonicalControlType = BuildCanonicalControlType(attribute.ControlId, packageId, isBuiltIn);
        var properties = DiscoverProperties(controlType);
        ValidateProperties(properties, controlType);

        // 扫描控件类上的 public static readonly FrontedV3Part 与 FrontedV3Parts 字段，
        // 把声明式 Part/PartCollection 接入统一 Registration，使插件作者可以与内置控件走同一链路。
        var fixedParts = FrontedV3Part.Discover(controlType);
        var partCollections = FrontedV3Parts.Discover(controlType);
        FrontedV3PartDefinitionValidator.Validate(fixedParts, partCollections, controlType);

        var configType = typeof(PluginFrontedControlConfig);
        Func<FrontedControlConfigBase> createDefaultConfig = () =>
        {
            var config = new PluginFrontedControlConfig { ControlType = canonicalControlType };
            ApplyDefaultValues(config, properties);
            return config;
        };

        var metadata = BuildMetadata(attribute);

        var registration = new FrontedV3ControlRegistration
        {
            CanonicalControlType = canonicalControlType,
            LocalControlId = attribute.ControlId,
            PackageId = packageId,
            IsBuiltIn = isBuiltIn,
            SupportsPeerStyleTransfer = attribute.SupportsPeerStyleTransfer,
            ControlType = controlType,
            ConfigType = configType,
            Properties = properties,
            CreateDefaultConfig = createDefaultConfig,
            FixedParts = fixedParts,
            PartCollections = partCollections,
            Metadata = metadata
        };

        return services.AddSingleton(registration);
    }

    /// <summary>
    /// 从 <see cref="FrontedV3ControlAttribute"/> 推导 <see cref="FrontedV3ControlMetadata"/>。
    /// </summary>
    /// <param name="attribute">控件 Attribute。</param>
    /// <returns>用于 Registration 的元数据实例。</returns>
    private static FrontedV3ControlMetadata BuildMetadata(FrontedV3ControlAttribute attribute)
    {
        return new FrontedV3ControlMetadata
        {
            DisplayNameKey = attribute.DisplayNameKey,
            DescriptionKey = attribute.DescriptionKey,
            Icon = attribute.Icon,
            // Attribute 使用 double.NaN 作为"未设置"哨兵（double? 不是合法的特性属性类型），
            // 转换为 Metadata 的 double? 时将 NaN 规范化为 null。
            DefaultWidth = double.IsNaN(attribute.DefaultWidth) ? null : attribute.DefaultWidth,
            DefaultHeight = double.IsNaN(attribute.DefaultHeight) ? null : attribute.DefaultHeight,
            // Attribute 使用 int.MaxValue 作为"未设置"哨兵（int? 不是合法的特性属性类型），
            // 转换为 Metadata 的 int? 时将 MaxValue 规范化为 null。
            DisplayOrder = attribute.DisplayOrder == int.MaxValue ? null : attribute.DisplayOrder
        };
    }

    /// <summary>
    /// 将属性元数据中声明的默认值写入新创建的 Config，仅写入声明了默认值的属性。
    /// </summary>
    /// <param name="config">新创建的控件配置实例。</param>
    /// <param name="properties">控件注册的属性定义列表。</param>
    private static void ApplyDefaultValues(FrontedControlConfigBase config, IReadOnlyList<FrontedV3PropertyDefinition> properties)
    {
        foreach (var property in properties)
        {
            var metadata = property.Metadata;
            object? defaultValue = null;

            if (metadata.DefaultValueFactory is not null)
            {
                defaultValue = metadata.DefaultValueFactory();
            }
            else if (metadata.DefaultValue is not null)
            {
                defaultValue = metadata.DefaultValue;
            }

            if (defaultValue is null)
            {
                continue;
            }

            property.SetValue(config, defaultValue);
        }
    }

    private static string BuildCanonicalControlType(string localControlId, string? packageId, bool isBuiltIn)
    {
        if (isBuiltIn || packageId is null)
        {
            return localControlId;
        }

        return $"{FrontedPluginControlType.Prefix}{packageId}/{localControlId}";
    }

    private static List<FrontedV3PropertyDefinition> DiscoverProperties(Type controlType)
    {
        var fields = controlType.GetFields(BindingFlags.Public | BindingFlags.Static);
        var definitions = new List<FrontedV3PropertyDefinition>();

        foreach (var field in fields)
        {
            if (!typeof(FrontedV3Property).IsAssignableFrom(field.FieldType))
            {
                continue;
            }

            if (field.GetValue(null) is not FrontedV3Property property)
            {
                continue;
            }

            definitions.Add(property.ToDefinition());
        }

        return definitions;
    }

    private static void ValidateProperties(List<FrontedV3PropertyDefinition> properties, Type controlType)
    {
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var prop in properties)
        {
            if (string.IsNullOrWhiteSpace(prop.OptionsPath))
            {
                throw new FrontedLayoutConfigException(
                    $"Control '{controlType.FullName}' has a property with an empty OptionsPath. " +
                    "OptionsPath must be a non-empty dot-separated path.");
            }

            if (!seenPaths.Add(prop.OptionsPath))
            {
                throw new FrontedLayoutConfigException(
                    $"Control '{controlType.FullName}' has duplicate OptionsPath '{prop.OptionsPath}'. " +
                    "Each OptionsPath must be unique within a control (case-insensitive).");
            }

            if (IsForbiddenOptionsPath(prop.OptionsPath))
            {
                throw new FrontedLayoutConfigException(
                    $"Control '{controlType.FullName}' property OptionsPath '{prop.OptionsPath}' is forbidden. " +
                    "The first segment must not be 'Options' or a reserved segment (Layout, Geometry, Position), " +
                    "and the path must be a valid dot-separated identifier path " +
                    "(no empty segments, no leading/trailing/consecutive dots).");
            }

            if (FrontedV3ReservedFields.IsReserved(prop.Storage.TargetField))
            {
                throw new FrontedLayoutConfigException(
                    $"Control '{controlType.FullName}' property '{prop.OptionsPath}' cannot use reserved storage field " +
                    $"'{prop.Storage.TargetField}'. Reserved fields: Left, Top, Width, Height, ZIndex, Visibility, " +
                    "BehaviorGuid, GaussianBlur, ControlType.");
            }
        }
    }

    private static readonly HashSet<string> ReservedFirstSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "Layout",
        "Geometry",
        "Position"
    };

    /// <summary>
    /// 判断 OptionsPath 是否被禁止：首段为保留段（Layout/Geometry/Position）或路径段不合法。
    /// </summary>
    /// <param name="optionsPath">待校验的 OptionsPath。</param>
    /// <returns>被禁止时为 <see langword="true"/>。</returns>
    /// <remarks>
    /// 路径合法性规则：不允许空段、不允许前后 <c>.</c>、不允许连续 <c>..</c>、
    /// 每段必须是有效的 C# 标识符（字母/下划线开头，仅含字母、数字、下划线）。
    /// </remarks>
    private static bool IsForbiddenOptionsPath(string optionsPath)
    {
        if (optionsPath.StartsWith(".", StringComparison.Ordinal)
            || optionsPath.EndsWith(".", StringComparison.Ordinal))
        {
            return true;
        }

        var segments = optionsPath.Split('.');
        foreach (var segment in segments)
        {
            if (string.IsNullOrEmpty(segment))
            {
                // 连续点或空段。
                return true;
            }

            if (!IsValidIdentifierSegment(segment))
            {
                return true;
            }
        }

        if (segments.Length == 0)
        {
            return true;
        }

        var firstSegment = segments[0];
        // OptionsPath 是相对于 Options 根的路径，不得再以 "Options" 开头（会导致 Options.Options.* 双重前缀）。
        if (firstSegment.Equals("Options", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Layout/Geometry/Position 是宿主保留命名空间，禁止作为首段。
        if (ReservedFirstSegments.Contains(firstSegment))
        {
            return true;
        }

        return false;
    }

    private static bool IsValidIdentifierSegment(string segment)
    {
        if (segment.Length == 0)
        {
            return false;
        }

        // 首字符必须是字母或下划线。
        var first = segment[0];
        if (!char.IsLetter(first) && first != '_')
        {
            return false;
        }

        // 其余字符必须是字母、数字或下划线。
        for (var i = 1; i < segment.Length; i++)
        {
            var c = segment[i];
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                return false;
            }
        }

        return true;
    }
}
