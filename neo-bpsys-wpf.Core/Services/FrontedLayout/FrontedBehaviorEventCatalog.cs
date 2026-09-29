using System.Reflection;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 合并宿主 Attribute 事件、显式内置事件和插件注册事件的设计器元数据目录。
/// </summary>
public sealed class FrontedBehaviorEventCatalog
{
    private readonly IReadOnlyList<FrontedBehaviorEventDescriptor> _events;

    /// <summary>
    /// 初始化仅包含宿主内置事件的事件目录。
    /// </summary>
    public FrontedBehaviorEventCatalog()
        : this(null)
    {
    }

    /// <summary>
    /// 初始化事件目录。
    /// </summary>
    /// <param name="registrations">插件或宿主显式注册的事件定义。</param>
    /// <exception cref="InvalidOperationException">合并后存在重复 EventType 时抛出。</exception>
    public FrontedBehaviorEventCatalog(IEnumerable<FrontedBehaviorEventRegistration>? registrations)
    {
        _events = BuildEvents(registrations ?? []);
    }

    /// <summary>
    /// 获取按确定性顺序排列的全部事件描述符。
    /// </summary>
    public IReadOnlyList<FrontedBehaviorEventDescriptor> Events => _events;

    /// <summary>
    /// 按规范事件类型查找描述符。
    /// </summary>
    /// <param name="eventType">事件类型。</param>
    /// <returns>找到的描述符；不存在时为 <see langword="null"/>。</returns>
    public FrontedBehaviorEventDescriptor? Find(string eventType) =>
        Events.FirstOrDefault(item => string.Equals(item.EventType, eventType, StringComparison.Ordinal));

    private static IReadOnlyList<FrontedBehaviorEventDescriptor> BuildEvents(
        IEnumerable<FrontedBehaviorEventRegistration> registrations)
    {
        var sourceTypes = new[] { typeof(ISharedDataService), typeof(IGameGuidanceService), typeof(ICharacterSelectionService) };
        var events = sourceTypes
            .SelectMany(type =>
                type.GetEvents(BindingFlags.Instance | BindingFlags.Public)
                    .Select(eventInfo => (Event: eventInfo, Metadata: eventInfo.GetCustomAttribute<FrontedBehaviorEventAttribute>()))
                    .Where(item => item.Metadata?.IsEnabled == true))
            .Select(item => new FrontedBehaviorEventDescriptor
            {
                EventType = item.Metadata!.EventType,
                DisplayName = item.Metadata.EventType,
                DisplayNameKey = item.Metadata.DisplayNameKey,
                Description = item.Metadata.EventType,
                DescriptionKey = item.Metadata.DescriptionKey,
                Category = item.Metadata.Category,
                CategoryDisplayName = item.Metadata.Category,
                CategoryDisplayNameKey = item.Metadata.CategoryKey,
                Order = item.Metadata.Order,
                // Attribute-discovered host events retain their existing Designer availability.
                // Plugin events are separately constrained to EventBus usages by their registration API.
                SupportedUsages = FrontedBehaviorEventUsage.All,
                PayloadFields = item.Event.GetCustomAttributes<FrontedBehaviorEventPayloadAttribute>()
                    .Select(payload => new FrontedBehaviorEventPayloadField
                    {
                        Path = payload.Path,
                        DisplayNameKey = payload.DisplayNameKey,
                        DescriptionKey = payload.DescriptionKey,
                        TypeName = payload.TypeName ?? payload.ValueType?.Name ?? "string",
                        ValueType = payload.ValueType,
                        EnumValues = ResolveEnumValues(payload.ValueType, payload.TypeName),
                        Source = payload.Source,
                        SourcePath = payload.SourcePath,
                        IsCommonFilterTarget = payload.IsCommonFilterTarget
                    })
                    .OrderByDescending(field => field.IsCommonFilterTarget)
                    .ThenBy(field => field.Path, StringComparer.Ordinal)
                    .ToList()
            });

        events = events
            .Concat(BuildExplicitEvents())
            .Concat(registrations.Select(ToDescriptor));

        var materialized = events
            .OrderBy(descriptor => descriptor.Category, StringComparer.Ordinal)
            .ThenBy(descriptor => descriptor.Order)
            .ThenBy(descriptor => descriptor.DisplayNameKey, StringComparer.Ordinal)
            .ThenBy(descriptor => descriptor.DisplayName, StringComparer.Ordinal)
            .ThenBy(descriptor => descriptor.EventType, StringComparer.Ordinal)
            .ToArray();

        var duplicate = materialized
            .GroupBy(item => item.EventType, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Fronted behavior event type '{duplicate.Key}' is registered more than once.");
        }

        return materialized;
    }

    private static FrontedBehaviorEventDescriptor ToDescriptor(FrontedBehaviorEventRegistration registration)
    {
        if (!registration.IsBuiltIn)
        {
            if (string.IsNullOrWhiteSpace(registration.PackageId)
                || !FrontedBehaviorEventIdValidator.TryParseCanonicalEventType(
                    registration.EventType, out var packageId, out var localEventId)
                || !string.Equals(packageId, registration.PackageId, StringComparison.Ordinal)
                || !string.Equals(localEventId, registration.LocalEventId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Plugin behavior event registration '{registration.EventType}' does not match its package/local identity.");
            }
        }

        return new FrontedBehaviorEventDescriptor
        {
            EventType = registration.EventType,
            DisplayName = string.IsNullOrWhiteSpace(registration.DisplayName)
                ? registration.LocalEventId
                : registration.DisplayName,
            DisplayNameKey = registration.DisplayNameKey,
            Description = string.IsNullOrWhiteSpace(registration.Description)
                ? string.IsNullOrWhiteSpace(registration.DisplayName)
                    ? registration.LocalEventId
                    : registration.DisplayName
                : registration.Description,
            DescriptionKey = registration.DescriptionKey,
            Category = string.IsNullOrWhiteSpace(registration.Category)
                ? registration.PackageId ?? "Plugin"
                : registration.Category,
            CategoryDisplayName = string.IsNullOrWhiteSpace(registration.CategoryDisplayName)
                ? string.IsNullOrWhiteSpace(registration.Category)
                    ? registration.PackageId ?? "Plugin"
                    : registration.Category
                : registration.CategoryDisplayName,
            CategoryDisplayNameKey = registration.CategoryDisplayNameKey,
            Order = registration.Order,
            SupportedUsages = registration.SupportedUsages,
            PayloadFields = registration.PayloadFields.Select(field => new FrontedBehaviorEventPayloadField
            {
                Path = field.Path.StartsWith("Event.", StringComparison.Ordinal)
                    ? field.Path
                    : $"Event.{field.Path}",
                DisplayName = string.IsNullOrWhiteSpace(field.DisplayName) ? field.Path : field.DisplayName,
                DisplayNameKey = field.DisplayNameKey,
                Description = string.IsNullOrWhiteSpace(field.Description)
                    ? string.IsNullOrWhiteSpace(field.DisplayName) ? field.Path : field.DisplayName
                    : field.Description,
                DescriptionKey = field.DescriptionKey,
                TypeName = field.TypeName,
                ValueType = field.ValueType,
                EnumValues = [.. field.EnumValues],
                Source = field.Source,
                SourcePath = field.SourcePath,
                IsCommonFilterTarget = field.IsCommonFilterTarget
            }).ToList()
        };
    }

    private static IReadOnlyList<FrontedBehaviorEventDescriptor> BuildExplicitEvents() =>
    [
        new()
        {
            EventType = "Selection.CharacterPick",
            DisplayName = "Selection.CharacterPick",
            DisplayNameKey = "Designer.Behaviors.Event.CharacterPick",
            DescriptionKey = "Designer.Behaviors.Event.CharacterPick.Description",
            Category = "Game",
            CategoryDisplayNameKey = "Designer.Behaviors.Category.Game",
            Order = 1000,
            SupportedUsages = FrontedBehaviorEventUsage.All,
            PayloadFields =
            [
                Payload("Event.Camp", "Designer.Behaviors.Payload.Camp", "Camp", typeof(Core.Enums.Camp)),
                Payload("Event.PlayerIndex", "Designer.Behaviors.Payload.PlayerIndex", "int"),
                Payload("Event.TargetBehaviorGuid", "Designer.Behaviors.Payload.TargetBehaviorGuid", "Guid"),
                Payload("Event.OldCharacterName", "Designer.Behaviors.Payload.OldCharacterName", "string"),
                Payload("Event.NewCharacterName", "Designer.Behaviors.Payload.NewCharacterName", "string"),
                Payload("Event.OldCharacterId", "Designer.Behaviors.Payload.OldCharacterId", "string"),
                Payload("Event.NewCharacterId", "Designer.Behaviors.Payload.NewCharacterId", "string"),
                Payload("Event.HasOldCharacter", "Designer.Behaviors.Payload.HasOldCharacter", "bool"),
                Payload("Event.HasNewCharacter", "Designer.Behaviors.Payload.HasNewCharacter", "bool")
            ]
        },
        new()
        {
            EventType = "Selection.CharacterSwap",
            DisplayName = "Selection.CharacterSwap",
            DisplayNameKey = "Designer.Behaviors.Event.CharacterSwap",
            DescriptionKey = "Designer.Behaviors.Event.CharacterSwap.Description",
            Category = "Game",
            CategoryDisplayNameKey = "Designer.Behaviors.Category.Game",
            Order = 1001,
            SupportedUsages = FrontedBehaviorEventUsage.All,
            PayloadFields =
            [
                Payload("Event.SourceIndex", "Designer.Behaviors.Payload.SourceIndex", "int"),
                Payload("Event.TargetIndex", "Designer.Behaviors.Payload.TargetIndex", "int"),
                Payload("Event.SourceBehaviorGuid", "Designer.Behaviors.Payload.SourceBehaviorGuid", "Guid"),
                Payload("Event.TargetBehaviorGuid", "Designer.Behaviors.Payload.TargetBehaviorGuid", "Guid")
            ]
        }
    ];

    private static FrontedBehaviorEventPayloadField Payload(
        string path,
        string displayNameKey,
        string typeName,
        Type? valueType = null) =>
        new()
        {
            Path = path,
            DisplayNameKey = displayNameKey,
            DescriptionKey = $"{displayNameKey}.Description",
            TypeName = typeName,
            ValueType = valueType,
            EnumValues = ResolveEnumValues(valueType, typeName),
            Source = FrontedBehaviorPayloadSource.EventArgsProperty,
            IsCommonFilterTarget = true
        };

    private static List<string> ResolveEnumValues(Type? valueType, string? typeName)
    {
        var enumType = Nullable.GetUnderlyingType(valueType ?? typeof(object)) ?? valueType;
        if (enumType?.IsEnum == true)
        {
            return Enum.GetNames(enumType).ToList();
        }

        var normalizedTypeName = typeName?.TrimEnd('?');
        if (string.IsNullOrWhiteSpace(normalizedTypeName))
        {
            return [];
        }

        enumType = typeof(FrontedBehaviorEventCatalog).Assembly
            .GetTypes()
            .FirstOrDefault(type => type.IsEnum && string.Equals(type.Name, normalizedTypeName, StringComparison.Ordinal));
        return enumType is null ? [] : Enum.GetNames(enumType).ToList();
    }
}
