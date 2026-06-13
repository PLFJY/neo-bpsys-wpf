using System.Reflection;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Cached Designer metadata built from explicitly attributed shared-data events.
/// </summary>
public sealed class FrontedBehaviorEventCatalog
{
    private static readonly Lazy<IReadOnlyList<FrontedBehaviorEventDescriptor>> CachedEvents =
        new(BuildEvents, LazyThreadSafetyMode.ExecutionAndPublication);

    public IReadOnlyList<FrontedBehaviorEventDescriptor> Events => CachedEvents.Value;

    public FrontedBehaviorEventDescriptor? Find(string eventType) =>
        Events.FirstOrDefault(item => string.Equals(item.EventType, eventType, StringComparison.Ordinal));

    private static IReadOnlyList<FrontedBehaviorEventDescriptor> BuildEvents()
    {
        var sourceTypes = new[] { typeof(ISharedDataService), typeof(IGameGuidanceService), typeof(ICharacterSelectionService) };
        return sourceTypes
            .SelectMany(type =>
                type.GetEvents(BindingFlags.Instance | BindingFlags.Public)
                    .Select(eventInfo => (Event: eventInfo, Metadata: eventInfo.GetCustomAttribute<FrontedBehaviorEventAttribute>()))
                    .Where(item => item.Metadata?.IsEnabled == true))
            .Select(item => new FrontedBehaviorEventDescriptor
            {
                EventType = item.Metadata!.EventType,
                DisplayNameKey = item.Metadata.DisplayNameKey,
                DescriptionKey = item.Metadata.DescriptionKey,
                Category = item.Metadata.Category,
                CategoryDisplayNameKey = item.Metadata.CategoryKey,
                Order = item.Metadata.Order,
                PayloadFields = item.Event.GetCustomAttributes<FrontedBehaviorEventPayloadAttribute>()
                    .Select(payload => new FrontedBehaviorEventPayloadField
                    {
                        Path = payload.Path,
                        DisplayNameKey = payload.DisplayNameKey,
                        DescriptionKey = payload.DescriptionKey,
                        TypeName = payload.TypeName ?? payload.ValueType?.Name ?? "string",
                        EnumValues = ResolveEnumValues(payload.ValueType, payload.TypeName),
                        Source = payload.Source,
                        SourcePath = payload.SourcePath,
                        IsCommonFilterTarget = payload.IsCommonFilterTarget
                    })
                    .OrderByDescending(field => field.IsCommonFilterTarget)
                    .ThenBy(field => field.Path, StringComparer.Ordinal)
                    .ToList()
            })
            .OrderBy(descriptor => descriptor.Category, StringComparer.Ordinal)
            .ThenBy(descriptor => descriptor.Order)
            .ThenBy(descriptor => descriptor.DisplayNameKey, StringComparer.Ordinal)
            .ThenBy(descriptor => descriptor.EventType, StringComparer.Ordinal)
            .ToArray();
    }

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
