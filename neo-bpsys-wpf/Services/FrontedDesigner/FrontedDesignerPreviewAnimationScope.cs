using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
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
    private IReadOnlyList<FrontedControlDesignItem> _controls = [];
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
        _controls = controls.ToArray();
        RefreshTargets();
    }

    /// <summary>
    /// Rebuilds animation target options from the current control configurations and preview visual tree.
    /// </summary>
    public void RefreshTargets()
    {
        var targets = new List<FrontedDesignerAnimationTargetOption>();
        foreach (var control in _controls)
        {
            if (control.Config.BehaviorGuid == Guid.Empty)
            {
                continue;
            }

            targets.Add(new FrontedDesignerAnimationTargetOption(
                control.Name,
                $"guid:{control.Config.BehaviorGuid}"));
            AddConfigDrivenPartTargets(targets, control);
        }

        var targetReferences = targets
            .Select(target => target.TargetReference)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var auxiliaryTargets = _root is null
            ? Enumerable.Empty<FrontedDesignerAnimationTargetOption>()
            : EnumerateFrameworkElements(_root)
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
        foreach (var target in auxiliaryTargets)
        {
            if (targetReferences.Add(target.TargetReference))
            {
                targets.Add(target);
            }
        }

        _targets = targets.ToArray();
    }

    public void Clear()
    {
        _root = null;
        _selfBehaviorGuid = Guid.Empty;
        _selfDisplayName = null;
        _windowId = string.Empty;
        _canvasName = string.Empty;
        _controls = [];
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

    private static void AddConfigDrivenPartTargets(
        ICollection<FrontedDesignerAnimationTargetOption> targets,
        FrontedControlDesignItem control)
    {
        if (control.Config is not ImageFrontedControlConfig image)
        {
            return;
        }

        var guid = image.BehaviorGuid;
        if (image.Lockable)
        {
            AddPartTarget(targets, control.Name, guid, FrontedAnimationPartNames.LockOverlay);
        }

        if (image.PickingBorderAvailable)
        {
            AddPartTarget(targets, control.Name, guid, FrontedAnimationPartNames.PickingBorder);
        }
    }

    private static void AddPartTarget(
        ICollection<FrontedDesignerAnimationTargetOption> targets,
        string controlName,
        Guid behaviorGuid,
        string partName)
    {
        targets.Add(new FrontedDesignerAnimationTargetOption(
            controlName,
            $"part:{behaviorGuid}:{partName}",
            partName));
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
