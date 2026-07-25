using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Services.Registry;

namespace neo_bpsys_wpf.PluginSdk;

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
    /// 或 OptionsPath 使用禁止路径时抛出。</exception>
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

        var configType = typeof(PluginFrontedControlConfig);
        Func<FrontedControlConfigBase> createDefaultConfig = () =>
            new PluginFrontedControlConfig { ControlType = canonicalControlType };

        var registration = new FrontedV3ControlRegistration
        {
            CanonicalControlType = canonicalControlType,
            LocalControlId = attribute.ControlId,
            PackageId = packageId,
            IsBuiltIn = isBuiltIn,
            ControlType = controlType,
            ConfigType = configType,
            Properties = properties,
            CreateDefaultConfig = createDefaultConfig
        };

        return services.AddSingleton(registration);
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
        var seenPaths = new HashSet<string>(StringComparer.Ordinal);

        foreach (var prop in properties)
        {
            if (!seenPaths.Add(prop.OptionsPath))
            {
                throw new FrontedLayoutConfigException(
                    $"Control '{controlType.FullName}' has duplicate OptionsPath '{prop.OptionsPath}'. " +
                    "Each OptionsPath must be unique within a control.");
            }

            if (IsForbiddenOptionsPath(prop.OptionsPath))
            {
                throw new FrontedLayoutConfigException(
                    $"Control '{controlType.FullName}' property OptionsPath '{prop.OptionsPath}' is forbidden. " +
                    "Options.Layout, Options.Geometry, Options.Position are reserved and cannot be used.");
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

    private static bool IsForbiddenOptionsPath(string optionsPath)
    {
        if (optionsPath.StartsWith("Options.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
