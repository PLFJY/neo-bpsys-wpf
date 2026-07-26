using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Properties;
using neo_bpsys_wpf.Core.Services.Registry;
using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.Core.Extensions.Registry;

/// <summary>
/// 内置 v3 前台控件注册扩展方法，仅供宿主注册内置控件使用。
/// </summary>
/// <remarks>
/// <para>
/// 与插件使用的 <c>AddFrontedV3Control&lt;T&gt;</c> 不同，内置控件使用强类型 Config
/// （如 <see cref="TextFrontedControlConfig"/>），属性 Schema 通过
/// <see cref="BuiltInPropertyDefinitionResolver"/> 从 Config CLR 属性自动发现。
/// </para>
/// <para>
/// 内置控件的 Canonical Control Type 为裸 <c>ControlId</c>（无 <c>plugin:</c> 前缀），
/// 与 Config 的 <c>ControlType</c> 字段完全一致，JSON 契约不变。
/// </para>
/// </remarks>
public static class FrontedV3BuiltInControlRegistryExtensions
{
    /// <summary>
    /// 注册一个内置 v3 前台控件到 DI 容器中。
    /// </summary>
    /// <typeparam name="TControl">控件类型，必须继承 <see cref="FrontedV3ControlBase"/> 并标注 <see cref="FrontedV3ControlAttribute"/>（<c>IsBuiltIn = true</c>）。</typeparam>
    /// <typeparam name="TConfig">控件配置类型，必须继承 <see cref="FrontedControlConfigBase"/> 并具有无参构造函数。</typeparam>
    /// <param name="services">服务容器。</param>
    /// <param name="createDefaultConfig">创建默认配置实例的工厂。</param>
    /// <returns>服务容器，支持链式调用。</returns>
    /// <exception cref="FrontedLayoutConfigException">当控件类型缺少 <see cref="FrontedV3ControlAttribute"/>、
    /// 未设置 <c>IsBuiltIn</c>、在插件作用域内调用、ControlId 不合法、Part/PartCollection Id 非法或重复、
    /// Part Capabilities 与 Storage 配对不一致、或 PartCollection 策略/Templates 与回调配对不一致时抛出。</exception>
    public static IServiceCollection AddBuiltInFrontedV3Control<TControl, TConfig>(
        this IServiceCollection services,
        Func<TConfig> createDefaultConfig)
        where TControl : FrontedV3ControlBase
        where TConfig : FrontedControlConfigBase, new()
    {
        ArgumentNullException.ThrowIfNull(createDefaultConfig);

        var controlType = typeof(TControl);
        var configType = typeof(TConfig);

        var attribute = controlType.GetCustomAttribute<FrontedV3ControlAttribute>()
            ?? throw new FrontedLayoutConfigException(
                $"Control type '{controlType.FullName}' must be annotated with [FrontedV3Control].");

        if (!attribute.IsBuiltIn)
        {
            throw new FrontedLayoutConfigException(
                $"Built-in control '{controlType.FullName}' must set IsBuiltIn = true on [FrontedV3Control].");
        }

        var packageId = FrontedPluginRegistrationContext.CurrentPackageId;
        if (packageId is not null)
        {
            throw new FrontedLayoutConfigException(
                $"Plugin '{packageId}' cannot register built-in control '{attribute.ControlId}'. " +
                "Use AddFrontedV3Control<T>() for plugin controls.");
        }

        FrontedV3ControlIdValidator.EnsureValidControlId(attribute.ControlId);

        var canonicalControlType = attribute.ControlId;
        var sampleConfig = new TConfig { ControlType = canonicalControlType };
        var properties = BuiltInPropertyDefinitionResolver.GetProperties(sampleConfig);

        // 内置控件通过 BuiltInPartDefinitionResolver / BuiltInPartCollectionDefinitionResolver 提供固定 Part 与集合定义，
        // 让内置控件与插件控件在 Registration 上的 FixedParts/PartCollections 字段保持同一形态，
        // Designer 选择 Part/集合项时即可统一从 Registration 查找，无需按控件类型分支。
        var fixedParts = BuiltInPartDefinitionResolver.GetParts(sampleConfig);
        var partCollections = BuiltInPartCollectionDefinitionResolver.GetCollections(sampleConfig);
        FrontedV3PartDefinitionValidator.Validate(fixedParts, partCollections, controlType);

        Func<FrontedControlConfigBase> defaultConfigFactory = () =>
        {
            var config = createDefaultConfig();
            config.ControlType = canonicalControlType;
            return config;
        };

        var metadata = new FrontedV3ControlMetadata
        {
            DisplayNameKey = attribute.DisplayNameKey,
            DescriptionKey = attribute.DescriptionKey,
            Icon = attribute.Icon,
            // Attribute 使用 double.NaN 作为"未设置"哨兵，转换为 Metadata 的 double? 时规范化为 null。
            DefaultWidth = double.IsNaN(attribute.DefaultWidth) ? null : attribute.DefaultWidth,
            DefaultHeight = double.IsNaN(attribute.DefaultHeight) ? null : attribute.DefaultHeight,
            DisplayOrder = attribute.DisplayOrder
        };

        var registration = new FrontedV3ControlRegistration
        {
            CanonicalControlType = canonicalControlType,
            LocalControlId = attribute.ControlId,
            PackageId = null,
            IsBuiltIn = true,
            SupportsPeerStyleTransfer = attribute.SupportsPeerStyleTransfer,
            ControlType = controlType,
            ConfigType = configType,
            Properties = properties,
            CreateDefaultConfig = defaultConfigFactory,
            FixedParts = fixedParts,
            PartCollections = partCollections,
            Metadata = metadata
        };

        return services.AddSingleton(registration);
    }
}
