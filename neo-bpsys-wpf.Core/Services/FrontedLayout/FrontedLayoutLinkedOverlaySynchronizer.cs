using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Keeps design-time linked overlay configs aligned with the control they follow.
/// </summary>
public static class FrontedLayoutLinkedOverlaySynchronizer
{
    /// <summary>
    /// Copies the changed target geometry to PickingBorderOverlay controls that reference it.
    /// </summary>
    public static IReadOnlyList<FrontedControlDesignItem> SyncLinkedOverlays(
        FrontedCanvasDesignDocument document,
        FrontedControlDesignItem changedTarget)
    {
        var targetBounds = FrontedDesignerBoundsResolver.Resolve(changedTarget.Config);
        return SyncLinkedOverlays(document, changedTarget, targetBounds);
    }

    /// <summary>
    /// Copies the changed target geometry to PickingBorderOverlay controls that reference it.
    /// </summary>
    public static IReadOnlyList<FrontedControlDesignItem> SyncLinkedOverlays(
        FrontedCanvasDesignDocument document,
        FrontedControlDesignItem changedTarget,
        FrontedDesignerResolvedBounds targetBounds)
    {
        if (changedTarget.IsLinkedOverlay)
        {
            return [];
        }

        var changedOverlays = new List<FrontedControlDesignItem>();
        foreach (var item in document.Controls)
        {
            if (item.Config is not PickingBorderOverlayControlConfig overlayConfig
                || overlayConfig.TargetControlName != changedTarget.Name)
            {
                continue;
            }

            overlayConfig.Left = targetBounds.Left;
            overlayConfig.Top = targetBounds.Top;
            overlayConfig.Width = targetBounds.Width;
            overlayConfig.Height = targetBounds.Height;
            item.IsSelectableInEditor = false;
            item.IsEditableInEditor = false;
            item.IsLinkedOverlay = true;
            item.LinkedTargetControlName = overlayConfig.TargetControlName;
            changedOverlays.Add(item);
        }

        if (changedOverlays.Count > 0)
        {
            document.IsDirty = true;
        }

        return changedOverlays;
    }
}
