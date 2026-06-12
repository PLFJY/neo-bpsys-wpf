using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System.Windows;
using System.Windows.Media;

namespace neo_bpsys_wpf.Services.FrontedDesigner;

public sealed class FrontedDesignerPreviewAnimationScope
{
    private readonly ILogger<FrontedDesignerPreviewAnimationScope>? _logger;
    private FrameworkElement? _root;
    private Guid _selfBehaviorGuid;
    private string? _selfDisplayName;
    private string _windowId = string.Empty;
    private string _canvasName = string.Empty;
    private IReadOnlyList<FrontedDesignerAnimationTargetOption> _targets = [];

    public FrontedDesignerPreviewAnimationScope()
    {
    }

    public FrontedDesignerPreviewAnimationScope(ILogger<FrontedDesignerPreviewAnimationScope> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<FrontedDesignerAnimationTargetOption> Targets => _targets;

    public FrameworkElement? Root => _root;

    public FrontedAnimationExecutionContext? CreateContext()
    {
        if (_root is null)
        {
            return null;
        }

        return new FrontedAnimationExecutionContext
        {
            Root = _root,
            SelfBehaviorGuid = _selfBehaviorGuid,
            SelfDisplayName = _selfDisplayName,
            WindowId = _windowId,
            CanvasName = _canvasName,
            IsDesignerPreview = true,
            Logger = _logger
        };
    }

    public void Update(
        FrameworkElement root,
        FrontedControlDesignItem? selectedControl,
        string? windowId,
        string? canvasName,
        IEnumerable<FrontedControlDesignItem> controls)
    {
        _root = root;
        _selfBehaviorGuid = selectedControl?.Config.BehaviorGuid ?? Guid.Empty;
        _selfDisplayName = selectedControl?.Name;
        _windowId = windowId ?? string.Empty;
        _canvasName = canvasName ?? string.Empty;
        var controlTargets = controls
            .Where(control => control.Config.BehaviorGuid != Guid.Empty)
            .Select(control => new FrontedDesignerAnimationTargetOption(
                control.Name,
                $"guid:{control.Config.BehaviorGuid}"));
        var partTargets = EnumerateFrameworkElements(root)
            .Where(FrontedRendererProperties.GetIsAnimationAuxiliaryElement)
            .Select(element => new
            {
                ParentName = FrontedRendererProperties.GetParentRegisteredName(element),
                ParentGuid = FrontedRendererProperties.GetParentBehaviorGuid(element),
                PartName = FrontedRendererProperties.GetAnimationPartName(element)
            })
            .Where(part => part.ParentGuid != Guid.Empty
                           && !string.IsNullOrWhiteSpace(part.ParentName)
                           && !string.IsNullOrWhiteSpace(part.PartName))
            .Select(part => new FrontedDesignerAnimationTargetOption(
                part.ParentName,
                $"part:{part.ParentGuid}:{part.PartName}",
                part.PartName));
        _targets = controlTargets.Concat(partTargets).ToArray();
    }

    public void Clear()
    {
        _root = null;
        _selfBehaviorGuid = Guid.Empty;
        _selfDisplayName = null;
        _windowId = string.Empty;
        _canvasName = string.Empty;
        _targets = [];
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

/// <summary>
/// Describes an animation target shown by the Designer preview editor.
/// </summary>
public sealed record FrontedDesignerAnimationTargetOption
{
    /// <summary>
    /// Initializes a Designer preview animation target option.
    /// </summary>
    /// <param name="displayName">The owning control display name.</param>
    /// <param name="targetReference">The stable persisted target reference.</param>
    /// <param name="partName">The optional stable generated part name.</param>
    public FrontedDesignerAnimationTargetOption(
        string displayName,
        string targetReference,
        string? partName = null)
    {
        DisplayName = displayName;
        TargetReference = targetReference;
        PartName = partName;
    }

    /// <summary>
    /// Gets the owning control display name.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the stable persisted target reference.
    /// </summary>
    public string TargetReference { get; }

    /// <summary>
    /// Gets the optional stable generated part name.
    /// </summary>
    public string? PartName { get; }
}
