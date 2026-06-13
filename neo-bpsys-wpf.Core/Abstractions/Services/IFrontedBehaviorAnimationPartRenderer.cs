using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using System.Windows;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Renders behavior-owned generated animation parts into already-rendered fronted controls.
/// </summary>
public interface IFrontedBehaviorAnimationPartRenderer
{
    /// <summary>
    /// Applies animation parts from a behavior document to the rendered visual tree.
    /// </summary>
    /// <param name="root">The rendered fronted root element.</param>
    /// <param name="behaviorDocument">The behavior document containing per-control animation parts.</param>
    void ApplyAnimationParts(FrameworkElement root, FrontedBehaviorDocument behaviorDocument);
}
