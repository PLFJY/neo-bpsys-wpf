using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using System.Windows;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

public sealed class FrontedAnimationTargetResolver : IFrontedAnimationTargetResolver
{
    public FrontedAnimationTarget? Resolve(
        FrontedAnimationTargetReference reference,
        FrontedAnimationExecutionContext context)
    {
        var effectiveReference = reference.Kind == FrontedAnimationTargetReferenceKind.Self
            ? new FrontedAnimationTargetReference
            {
                Kind = FrontedAnimationTargetReferenceKind.BehaviorGuid,
                BehaviorGuid = context.SelfBehaviorGuid,
                DisplayName = context.SelfDisplayName
            }
            : reference;

        if (effectiveReference.Kind == FrontedAnimationTargetReferenceKind.BehaviorGuid)
        {
            if (effectiveReference.BehaviorGuid is not { } guid || guid == Guid.Empty)
            {
                context.Logger?.LogWarning("Fronted animation target has no BehaviorGuid.");
                return null;
            }

            var element = EnumerateFrameworkElements(context.Root)
                .FirstOrDefault(item => FrontedRendererProperties.GetIsGeneratedControl(item)
                                        && !FrontedRendererProperties.GetIsAnimationAuxiliaryElement(item)
                                        && FrontedRendererProperties.GetBehaviorGuid(item) == guid);
            if (element is null)
            {
                context.Logger?.LogWarning("Fronted animation target {BehaviorGuid} was not found.", guid);
                return null;
            }

            return CreateTarget(element, effectiveReference.DisplayName);
        }

        if (effectiveReference.Kind == FrontedAnimationTargetReferenceKind.RegisteredName
            && !string.IsNullOrWhiteSpace(effectiveReference.Name))
        {
            var element = EnumerateFrameworkElements(context.Root)
                .FirstOrDefault(item => FrontedRendererProperties.GetIsGeneratedControl(item)
                                        && string.Equals(
                                            FrontedRendererProperties.GetRegisteredName(item),
                                            effectiveReference.Name,
                                            StringComparison.Ordinal));
            if (element is null)
            {
                context.Logger?.LogWarning("Fronted animation target named {TargetName} was not found.", effectiveReference.Name);
                return null;
            }

            return CreateTarget(element, effectiveReference.DisplayName);
        }

        if (effectiveReference.Kind == FrontedAnimationTargetReferenceKind.GeneratedPart
            && effectiveReference.BehaviorGuid is { } parentGuid
            && parentGuid != Guid.Empty
            && !string.IsNullOrWhiteSpace(effectiveReference.PartName))
        {
            var element = EnumerateFrameworkElements(context.Root)
                .FirstOrDefault(item => FrontedRendererProperties.GetIsAnimationAuxiliaryElement(item)
                                        && FrontedRendererProperties.GetParentBehaviorGuid(item) == parentGuid
                                        && string.Equals(
                                            FrontedRendererProperties.GetAnimationPartName(item),
                                            effectiveReference.PartName,
                                            StringComparison.Ordinal));
            if (element is null)
            {
                context.Logger?.LogWarning(
                    "Fronted animation part {PartName} on target {BehaviorGuid} was not found.",
                    effectiveReference.PartName,
                    parentGuid);
                return null;
            }

            return CreateTarget(element, effectiveReference.DisplayName);
        }

        return null;
    }

    private static FrontedAnimationTarget CreateTarget(FrameworkElement element, string? displayName)
    {
        var registeredName = FrontedRendererProperties.GetRegisteredName(element);
        return new FrontedAnimationTarget
        {
            Element = element,
            BehaviorGuid = FrontedRendererProperties.GetIsAnimationAuxiliaryElement(element)
                ? FrontedRendererProperties.GetParentBehaviorGuid(element)
                : FrontedRendererProperties.GetBehaviorGuid(element),
            Name = string.IsNullOrWhiteSpace(registeredName) ? element.Name : registeredName,
            DisplayName = displayName,
            TargetLayer = FrontedAnimationTargetLayer.Control,
            ControlElement = element
        };
    }

    private static IEnumerable<FrameworkElement> EnumerateFrameworkElements(DependencyObject root)
    {
        if (root is FrameworkElement frameworkElement)
        {
            yield return frameworkElement;
        }

        var children = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < children; i++)
        {
            foreach (var child in EnumerateFrameworkElements(VisualTreeHelper.GetChild(root, i)))
            {
                yield return child;
            }
        }
    }
}
