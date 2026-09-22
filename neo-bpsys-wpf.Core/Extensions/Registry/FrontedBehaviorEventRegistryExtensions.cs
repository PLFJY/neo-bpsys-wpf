using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Services.Registry;

namespace neo_bpsys_wpf.Core.Extensions.Registry;

/// <summary>
/// 插件前台行为事件注册扩展。
/// </summary>
public static class FrontedBehaviorEventRegistryExtensions
{
    /// <summary>
    /// 在当前插件初始化作用域中注册语义事件以及类型安全的插件发布器。
    /// </summary>
    /// <typeparam name="TPlugin">插件入口类型，用于绑定发布器身份。</typeparam>
    /// <param name="services">DI 服务集合。</param>
    /// <param name="configure">事件注册配置。</param>
    /// <returns>DI 服务集合。</returns>
    /// <exception cref="FrontedLayoutConfigException">当前不在插件初始化作用域、事件定义无效或 canonical EventType 重复时抛出。</exception>
    public static IServiceCollection AddFrontedBehaviorEvents<TPlugin>(
        this IServiceCollection services,
        Action<FrontedBehaviorEventRegistryBuilder> configure)
        where TPlugin : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        var packageId = FrontedPluginRegistrationContext.CurrentPackageId;
        if (string.IsNullOrWhiteSpace(packageId))
        {
            throw new FrontedLayoutConfigException(
                $"AddFrontedBehaviorEvents<{typeof(TPlugin).Name}> must be called during PluginBase.Initialize inside a plugin registration scope.");
        }

        var builder = new FrontedBehaviorEventRegistryBuilder(packageId);
        configure(builder);
        foreach (var registration in builder.Build())
        {
            var duplicate = services.Any(descriptor =>
                descriptor.ServiceType == typeof(FrontedBehaviorEventRegistration)
                && descriptor.ImplementationInstance is FrontedBehaviorEventRegistration existing
                && string.Equals(existing.EventType, registration.EventType, StringComparison.Ordinal));
            if (duplicate)
            {
                throw new FrontedLayoutConfigException(
                    $"Behavior event '{registration.EventType}' is registered more than once.");
            }

            services.AddSingleton(registration);
        }

        services.TryAddSingleton<IFrontedBehaviorEventPublisher<TPlugin>>(serviceProvider =>
            new FrontedBehaviorEventPublisher<TPlugin>(
                packageId,
                serviceProvider.GetRequiredService<IFrontedEventBus>(),
                serviceProvider.GetServices<FrontedBehaviorEventRegistration>()));
        return services;
    }
}

/// <summary>
/// 收集当前插件的行为事件定义。
/// </summary>
public sealed class FrontedBehaviorEventRegistryBuilder
{
    private readonly string _packageId;
    private readonly List<FrontedBehaviorEventRegistration> _registrations = [];

    internal FrontedBehaviorEventRegistryBuilder(string packageId)
    {
        _packageId = packageId;
    }

    /// <summary>
    /// 添加一个插件局部事件定义。
    /// </summary>
    /// <param name="localEventId">插件内唯一的局部事件标识。</param>
    /// <param name="configure">事件元数据配置。</param>
    /// <returns>当前 builder。</returns>
    /// <exception cref="FrontedLayoutConfigException">标识、负载字段或类型无效，或当前插件重复注册同一事件时抛出。</exception>
    public FrontedBehaviorEventRegistryBuilder Add(
        string localEventId,
        Action<FrontedBehaviorEventDefinitionBuilder>? configure = null)
    {
        FrontedBehaviorEventIdValidator.EnsureValidLocalEventId(localEventId);
        var eventType = FrontedBehaviorEventIdValidator.BuildCanonicalEventType(_packageId, localEventId);
        if (_registrations.Any(item => string.Equals(item.EventType, eventType, StringComparison.Ordinal)))
        {
            throw new FrontedLayoutConfigException($"Behavior event '{eventType}' is registered more than once.");
        }

        var definition = new FrontedBehaviorEventDefinitionBuilder(localEventId);
        configure?.Invoke(definition);
        _registrations.Add(definition.Build(_packageId));
        return this;
    }

    internal IReadOnlyList<FrontedBehaviorEventRegistration> Build() => _registrations;
}

/// <summary>
/// 配置一个插件行为事件的 Designer 元数据与负载 schema。
/// </summary>
public sealed class FrontedBehaviorEventDefinitionBuilder
{
    private readonly string _localEventId;
    private readonly List<FrontedBehaviorEventPayloadField> _payloadFields = [];
    private string _displayName = string.Empty;
    private string _displayNameKey = string.Empty;
    private string _description = string.Empty;
    private string _descriptionKey = string.Empty;
    private string _category = string.Empty;
    private string _categoryDisplayName = string.Empty;
    private string _categoryDisplayNameKey = string.Empty;
    private int _order;

    internal FrontedBehaviorEventDefinitionBuilder(string localEventId)
    {
        _localEventId = localEventId;
    }

    /// <summary>
    /// 设置直接显示名称及可选宿主本地化键。
    /// </summary>
    /// <param name="displayName">直接显示名称。</param>
    /// <param name="localizationKey">可选本地化键。</param>
    /// <returns>当前 builder。</returns>
    public FrontedBehaviorEventDefinitionBuilder WithDisplayName(string displayName, string? localizationKey = null)
    {
        _displayName = displayName ?? string.Empty;
        _displayNameKey = localizationKey ?? string.Empty;
        return this;
    }

    /// <summary>
    /// 设置直接描述及可选宿主本地化键。
    /// </summary>
    /// <param name="description">直接描述。</param>
    /// <param name="localizationKey">可选本地化键。</param>
    /// <returns>当前 builder。</returns>
    public FrontedBehaviorEventDefinitionBuilder WithDescription(string description, string? localizationKey = null)
    {
        _description = description ?? string.Empty;
        _descriptionKey = localizationKey ?? string.Empty;
        return this;
    }

    /// <summary>
    /// 设置分类标识、可选直接显示名称和本地化键。
    /// </summary>
    /// <param name="category">分类标识。</param>
    /// <param name="displayName">可选直接分类显示名称。</param>
    /// <param name="localizationKey">可选本地化键。</param>
    /// <returns>当前 builder。</returns>
    public FrontedBehaviorEventDefinitionBuilder WithCategory(
        string category,
        string? displayName = null,
        string? localizationKey = null)
    {
        _category = category ?? string.Empty;
        _categoryDisplayName = displayName ?? category ?? string.Empty;
        _categoryDisplayNameKey = localizationKey ?? string.Empty;
        return this;
    }

    /// <summary>
    /// 设置分类内排序值。
    /// </summary>
    /// <param name="order">排序值。</param>
    /// <returns>当前 builder。</returns>
    public FrontedBehaviorEventDefinitionBuilder WithOrder(int order)
    {
        _order = order;
        return this;
    }

    /// <summary>
    /// 添加一个强类型负载字段。
    /// </summary>
    /// <typeparam name="TValue">稳定负载类型。</typeparam>
    /// <param name="path">不带 <c>Event.</c> 前缀的字段名。</param>
    /// <param name="displayName">直接显示名称。</param>
    /// <param name="description">可选直接描述。</param>
    /// <param name="displayNameKey">可选显示名称本地化键。</param>
    /// <param name="descriptionKey">可选描述本地化键。</param>
    /// <returns>当前 builder。</returns>
    /// <exception cref="FrontedLayoutConfigException">字段路径重复/非法或类型不受支持时抛出。</exception>
    public FrontedBehaviorEventDefinitionBuilder AddPayload<TValue>(
        string path,
        string displayName,
        string? description = null,
        string? displayNameKey = null,
        string? descriptionKey = null)
    {
        ValidatePayloadPath(path);
        if (_payloadFields.Any(field => string.Equals(field.Path, path, StringComparison.OrdinalIgnoreCase)))
        {
            throw new FrontedLayoutConfigException(
                $"Behavior event '{_localEventId}' contains duplicate payload path '{path}' (case-insensitive).");
        }

        var valueType = typeof(TValue);
        FrontedBehaviorEventPayloadTypeValidator.EnsureSupported(valueType);
        var effectiveType = Nullable.GetUnderlyingType(valueType) ?? valueType;
        _payloadFields.Add(new FrontedBehaviorEventPayloadField
        {
            Path = path,
            DisplayName = displayName ?? string.Empty,
            DisplayNameKey = displayNameKey ?? string.Empty,
            Description = description ?? string.Empty,
            DescriptionKey = descriptionKey ?? string.Empty,
            TypeName = FrontedBehaviorEventPayloadTypeValidator.GetTypeName(valueType),
            ValueType = valueType,
            EnumValues = effectiveType.IsEnum ? Enum.GetNames(effectiveType).ToList() : [],
            Source = FrontedBehaviorPayloadSource.EventArgsProperty,
            IsCommonFilterTarget = true
        });
        return this;
    }

    internal FrontedBehaviorEventRegistration Build(string packageId)
    {
        return new FrontedBehaviorEventRegistration
        {
            EventType = FrontedBehaviorEventIdValidator.BuildCanonicalEventType(packageId, _localEventId),
            LocalEventId = _localEventId,
            PackageId = packageId,
            IsBuiltIn = false,
            DisplayName = _displayName,
            DisplayNameKey = _displayNameKey,
            Description = _description,
            DescriptionKey = _descriptionKey,
            Category = _category,
            CategoryDisplayName = _categoryDisplayName,
            CategoryDisplayNameKey = _categoryDisplayNameKey,
            Order = _order,
            SupportedUsages = FrontedBehaviorEventUsage.EventBus,
            PayloadFields = _payloadFields.ToArray()
        };
    }

    private static void ValidatePayloadPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || path.StartsWith("Event.", StringComparison.OrdinalIgnoreCase)
            || path.Contains('.')
            || path.Contains('/')
            || path.Contains('\\')
            || path.Contains(':')
            || !IsIdentifier(path))
        {
            throw new FrontedLayoutConfigException(
                $"Behavior event payload path '{path}' is invalid. Use one identifier without the 'Event.' prefix.");
        }
    }

    private static bool IsIdentifier(string value)
    {
        if (value.Length == 0 || (!char.IsLetter(value[0]) && value[0] != '_'))
        {
            return false;
        }

        return value.Skip(1).All(character => char.IsLetterOrDigit(character) || character == '_');
    }
}
