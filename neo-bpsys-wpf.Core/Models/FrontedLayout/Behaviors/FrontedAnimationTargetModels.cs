using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;
using System.Windows;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// Describes the visual layer that receives an animation action.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrontedAnimationTargetLayer
{
    /// <summary>
    /// Chooses a target layer from the property name and the target control shape.
    /// </summary>
    Auto,

    /// <summary>
    /// Applies the action to the generated control root element.
    /// </summary>
    Control,

    /// <summary>
    /// Applies the action to the target control's primary content element.
    /// </summary>
    Content,

    /// <summary>
    /// Applies the action to a runtime rectangle overlay above the target control.
    /// </summary>
    OverlayAbove,

    /// <summary>
    /// Applies the action to a runtime rectangle overlay below the target control.
    /// </summary>
    OverlayBelow
}

/// <summary>
/// Identifies the kind of persisted animation target reference.
/// </summary>
public enum FrontedAnimationTargetReferenceKind
{
    /// <summary>
    /// The control that owns the behavior being executed.
    /// </summary>
    Self,

    /// <summary>
    /// A generated control identified by its behavior GUID.
    /// </summary>
    BehaviorGuid,

    /// <summary>
    /// A generated control identified by its registered name.
    /// </summary>
    RegisteredName,

    /// <summary>
    /// A generated auxiliary part identified by its owning behavior GUID and stable part name.
    /// </summary>
    GeneratedPart
}

/// <summary>
/// Persisted animation target reference parsed from behavior graph action nodes.
/// </summary>
public sealed class FrontedAnimationTargetReference
{
    /// <summary>
    /// Gets the target reference kind.
    /// </summary>
    public FrontedAnimationTargetReferenceKind Kind { get; init; } = FrontedAnimationTargetReferenceKind.Self;

    /// <summary>
    /// Gets the behavior GUID when <see cref="Kind" /> is <see cref="FrontedAnimationTargetReferenceKind.BehaviorGuid" />.
    /// </summary>
    public Guid? BehaviorGuid { get; init; }

    /// <summary>
    /// Gets the registered element name when <see cref="Kind" /> is <see cref="FrontedAnimationTargetReferenceKind.RegisteredName" />.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the stable generated part name when <see cref="Kind" /> is <see cref="FrontedAnimationTargetReferenceKind.GeneratedPart" />.
    /// </summary>
    public string? PartName { get; init; }

    /// <summary>
    /// Gets the user-facing display name, when available.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Parses a stored target reference string.
    /// </summary>
    /// <param name="value">The stored target reference.</param>
    /// <returns>The parsed target reference.</returns>
    public static FrontedAnimationTargetReference Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || string.Equals(value.Trim(), "Self", StringComparison.OrdinalIgnoreCase))
        {
            return new FrontedAnimationTargetReference { Kind = FrontedAnimationTargetReferenceKind.Self };
        }

        var text = value.Trim();
        if (text.StartsWith("part:", StringComparison.OrdinalIgnoreCase))
        {
            var separatorIndex = text.IndexOf(':', "part:".Length);
            if (separatorIndex > "part:".Length
                && Guid.TryParse(text["part:".Length..separatorIndex].Trim('{', '}', ' '), out var partGuid))
            {
                var partName = text[(separatorIndex + 1)..].Trim();
                if (!string.IsNullOrWhiteSpace(partName))
                {
                    return new FrontedAnimationTargetReference
                    {
                        Kind = FrontedAnimationTargetReferenceKind.GeneratedPart,
                        BehaviorGuid = partGuid,
                        PartName = partName,
                        DisplayName = value
                    };
                }
            }
        }

        if (text.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
        {
            text = text["name:".Length..].Trim();
            return new FrontedAnimationTargetReference
            {
                Kind = FrontedAnimationTargetReferenceKind.RegisteredName,
                Name = text,
                DisplayName = value
            };
        }

        if (text.StartsWith("guid:", StringComparison.OrdinalIgnoreCase))
        {
            text = text["guid:".Length..].Trim();
        }

        text = text.Trim('{', '}');
        if (Guid.TryParse(text, out var guid))
        {
            return new FrontedAnimationTargetReference
            {
                Kind = FrontedAnimationTargetReferenceKind.BehaviorGuid,
                BehaviorGuid = guid,
                DisplayName = value
            };
        }

        return new FrontedAnimationTargetReference
        {
            Kind = FrontedAnimationTargetReferenceKind.RegisteredName,
            Name = value,
            DisplayName = value
        };
    }
}

/// <summary>
/// Runtime animation target after resolving a persisted target reference and visual layer.
/// </summary>
public sealed class FrontedAnimationTarget
{
    /// <summary>
    /// Gets the WPF element that receives the animation action.
    /// </summary>
    public required FrameworkElement Element { get; init; }

    /// <summary>
    /// Gets the behavior GUID of the owning generated control.
    /// </summary>
    public Guid BehaviorGuid { get; init; }

    /// <summary>
    /// Gets the resolved target name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the user-facing display name, when available.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets the resolved visual target layer.
    /// </summary>
    public FrontedAnimationTargetLayer TargetLayer { get; init; } = FrontedAnimationTargetLayer.Control;

    /// <summary>
    /// Gets the generated control root used before layer resolution.
    /// </summary>
    public FrameworkElement? ControlElement { get; init; }
}

/// <summary>
/// Runtime context used while applying WPF animation actions.
/// </summary>
public sealed class FrontedAnimationExecutionContext
{
    /// <summary>
    /// Gets the root element that scopes target lookup and runtime sessions.
    /// </summary>
    public required FrameworkElement Root { get; init; }

    /// <summary>
    /// Gets the behavior GUID of the control that owns the executing behavior.
    /// </summary>
    public Guid SelfBehaviorGuid { get; init; }

    /// <summary>
    /// Gets the user-facing name of the control that owns the executing behavior.
    /// </summary>
    public string? SelfDisplayName { get; init; }

    /// <summary>
    /// Gets the current fronted window identifier.
    /// </summary>
    public string WindowId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the current canvas name.
    /// </summary>
    public string CanvasName { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the action runs in designer preview.
    /// </summary>
    public bool IsDesignerPreview { get; init; }

    /// <summary>
    /// Gets the optional logger used for runtime warnings.
    /// </summary>
    public ILogger? Logger { get; init; }

    /// <summary>
    /// Gets the cancellation token for the current animation action.
    /// </summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Creates a copy of the context with a replacement cancellation token.
    /// </summary>
    /// <param name="cancellationToken">The replacement cancellation token.</param>
    /// <returns>A copied context using the replacement token.</returns>
    public FrontedAnimationExecutionContext WithCancellationToken(CancellationToken cancellationToken) =>
        new()
        {
            Root = Root,
            SelfBehaviorGuid = SelfBehaviorGuid,
            SelfDisplayName = SelfDisplayName,
            WindowId = WindowId,
            CanvasName = CanvasName,
            IsDesignerPreview = IsDesignerPreview,
            Logger = Logger,
            CancellationToken = cancellationToken
        };
}
