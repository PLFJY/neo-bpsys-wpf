using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using System.Windows;

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
        _targets = controls
            .Where(control => control.Config.BehaviorGuid != Guid.Empty)
            .Select(control => new FrontedDesignerAnimationTargetOption(control.Name, control.Config.BehaviorGuid))
            .ToArray();
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
}

public sealed record FrontedDesignerAnimationTargetOption(string DisplayName, Guid BehaviorGuid);
