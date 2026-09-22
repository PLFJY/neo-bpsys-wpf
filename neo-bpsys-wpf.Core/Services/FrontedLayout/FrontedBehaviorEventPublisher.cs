using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 将类型化插件发布调用验证并转换到统一 <see cref="IFrontedEventBus"/>。
/// </summary>
/// <typeparam name="TPlugin">插件入口类型。</typeparam>
internal sealed class FrontedBehaviorEventPublisher<TPlugin> : IFrontedBehaviorEventPublisher<TPlugin>
{
    private readonly string _packageId;
    private readonly IFrontedEventBus _eventBus;
    private readonly IReadOnlyDictionary<string, FrontedBehaviorEventRegistration> _registrations;

    /// <summary>
    /// 初始化发布器。
    /// </summary>
    /// <param name="packageId">由插件注册上下文捕获的包 ID。</param>
    /// <param name="eventBus">统一前台事件总线。</param>
    /// <param name="registrations">进程内事件注册。</param>
    public FrontedBehaviorEventPublisher(
        string packageId,
        IFrontedEventBus eventBus,
        IEnumerable<FrontedBehaviorEventRegistration> registrations)
    {
        _packageId = packageId;
        _eventBus = eventBus;
        _registrations = registrations
            .Where(item => string.Equals(item.PackageId, packageId, StringComparison.Ordinal))
            .ToDictionary(item => item.LocalEventId, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public void Publish(
        string eventId,
        IReadOnlyDictionary<string, object?>? payload = null,
        string? windowId = null,
        string? windowType = null)
    {
        FrontedBehaviorEventIdValidator.EnsureValidLocalEventId(eventId);
        if (!_registrations.TryGetValue(eventId, out var registration))
        {
            throw new InvalidOperationException(
                $"Plugin '{_packageId}' cannot publish unregistered behavior event '{eventId}'.");
        }

        var input = payload ?? new Dictionary<string, object?>();
        var schema = registration.PayloadFields.ToDictionary(field => field.Path, StringComparer.Ordinal);
        foreach (var key in input.Keys)
        {
            if (key.StartsWith("Event.", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Behavior event payload key '{key}' must not include the 'Event.' prefix.");
            }

            if (!schema.ContainsKey(key))
            {
                throw new InvalidOperationException(
                    $"Behavior event '{registration.EventType}' payload contains unknown field '{key}'.");
            }
        }

        var normalized = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var field in registration.PayloadFields)
        {
            if (!input.TryGetValue(field.Path, out var value))
            {
                throw new InvalidOperationException(
                    $"Behavior event '{registration.EventType}' payload is missing required field '{field.Path}'.");
            }

            var valueType = field.ValueType
                ?? throw new InvalidOperationException(
                    $"Behavior event '{registration.EventType}' field '{field.Path}' has no runtime type metadata.");
            normalized[field.Path] = FrontedBehaviorEventPayloadTypeValidator.Normalize(value, valueType, field.Path);
        }

        _eventBus.Publish(new FrontedBehaviorEvent
        {
            EventType = registration.EventType,
            WindowId = windowId,
            WindowType = windowType,
            Source = $"Plugin:{_packageId}",
            Timestamp = DateTimeOffset.UtcNow,
            Payload = normalized
        });
    }
}
