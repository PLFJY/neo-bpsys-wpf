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
    private FrontedBehaviorDocument? _behaviorDocument;
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
        IEnumerable<FrontedControlDesignItem> controls,
        FrontedBehaviorDocument? behaviorDocument = null)
    {
        _root = root;
        _selfBehaviorGuid = selectedControl?.Config.BehaviorGuid ?? Guid.Empty;
        _selfDisplayName = selectedControl?.Name;
        _windowId = windowId ?? string.Empty;
        _canvasName = canvasName ?? string.Empty;
        _controls = controls.ToArray();
        _behaviorDocument = behaviorDocument;
        RefreshTargets();
    }

    /// <summary>
    /// 根据当前控件配置和预览可视化树重新生成动画目标选项。
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
        _behaviorDocument = null;
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

    private void AddConfigDrivenPartTargets(
        ICollection<FrontedDesignerAnimationTargetOption> targets,
        FrontedControlDesignItem control)
    {
        var guid = control.Config.BehaviorGuid;
        var set = _behaviorDocument?.FindSet(guid);
        foreach (var AnimationPart in set?.AnimationParts.Where(item => !string.IsNullOrWhiteSpace(item.Name)) ?? [])
        {
            AddPartTarget(targets, control.Name, guid, AnimationPart.Name);
        }

        if (control.Config is not ImageFrontedControlConfig image)
        {
            return;
        }

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
/// 描述设计器预览编辑器显示的动画目标。
/// </summary>
public sealed record FrontedDesignerAnimationTargetOption
{
    /// <summary>
    /// 初始化设计器预览动画目标选项。
    /// </summary>
    /// <param name="displayName">所属控件的显示名称。</param>
    /// <param name="targetReference">稳定的持久化目标引用。</param>
    /// <param name="partName">可选的稳定生成部件名称。</param>
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
    /// 获取所属控件的显示名称。
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// 获取稳定的持久化目标引用。
    /// </summary>
    public string TargetReference { get; }

    /// <summary>
    /// 获取可选的稳定生成部件名称。
    /// </summary>
    public string? PartName { get; }
}
