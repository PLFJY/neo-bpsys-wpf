using Microsoft.Extensions.Logging;
using System.Windows;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

public enum FrontedAnimationTargetReferenceKind
{
    Self,
    BehaviorGuid,
    RegisteredName
}

public sealed class FrontedAnimationTargetReference
{
    public FrontedAnimationTargetReferenceKind Kind { get; init; } = FrontedAnimationTargetReferenceKind.Self;
    public Guid? BehaviorGuid { get; init; }
    public string? Name { get; init; }
    public string? DisplayName { get; init; }

    public static FrontedAnimationTargetReference Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || string.Equals(value.Trim(), "Self", StringComparison.OrdinalIgnoreCase))
        {
            return new FrontedAnimationTargetReference { Kind = FrontedAnimationTargetReferenceKind.Self };
        }

        var text = value.Trim();
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

public sealed class FrontedAnimationTarget
{
    public required FrameworkElement Element { get; init; }
    public Guid BehaviorGuid { get; init; }
    public string? Name { get; init; }
    public string? DisplayName { get; init; }
}

public sealed class FrontedAnimationExecutionContext
{
    public required FrameworkElement Root { get; init; }
    public Guid SelfBehaviorGuid { get; init; }
    public string? SelfDisplayName { get; init; }
    public string WindowId { get; init; } = string.Empty;
    public string CanvasName { get; init; } = string.Empty;
    public bool IsDesignerPreview { get; init; }
    public ILogger? Logger { get; init; }
    public CancellationToken CancellationToken { get; init; }

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
