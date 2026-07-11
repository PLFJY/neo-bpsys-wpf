using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using System.Windows;
using System.Windows.Threading;

namespace neo_bpsys_wpf.Views.Windows;

public partial class FrontedDesignerWindow : ITutorialOwner<FrontedDesignerWindow>
{
    /// <summary>Designer v3 window tutorial key.</summary>
    public const string TutorialPageKey = TutorialPageKeys.DesignerV3;

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>Designer v3 tutorial package references.</summary>
    public static class Tours
    {
        /// <summary>Designer v3 overview package reference.</summary>
        public static readonly TutorialPackageRef Overview = new(TutorialPackageIds.DesignerV3Overview);

        /// <summary>Designer v3 layout edit package reference.</summary>
        public static readonly TutorialPackageRef LayoutEditBasic = new(TutorialPackageIds.DesignerV3LayoutEditBasic);

        /// <summary>Designer v3 property panel package reference.</summary>
        public static readonly TutorialPackageRef PropertyPanelBasic = new(TutorialPackageIds.DesignerV3PropertyPanelBasic);

        /// <summary>Designer v3 package import/export package reference.</summary>
        public static readonly TutorialPackageRef PackageImportExport = new(TutorialPackageIds.DesignerV3PackageImportExport);

        /// <summary>Designer v3 help package reference.</summary>
        public static readonly TutorialPackageRef HelpBasic = new(TutorialPackageIds.DesignerV3HelpBasic);
    }

    /// <summary>Designer v3 tutorial target names not owned by direct window fields.</summary>
    public static class TutorialTargets
    {
        /// <summary>Property search box target name.</summary>
        public const string PropertySearchBox = "PropertySearchBox";

        /// <summary>First editable property editor target name.</summary>
        public const string FirstEditablePropertyEditor = "FirstEditablePropertyEditor";

        /// <summary>Property apply button target name.</summary>
        public const string PropertyApplyButton = "PropertyApplyButton";

        /// <summary>Behavior add button target name.</summary>
        public const string AddBehaviorButton = "AddBehaviorButton";

        /// <summary>Behavior trigger editor target name.</summary>
        public const string BehaviorTriggerEditor = "BehaviorTriggerEditor";

        /// <summary>Behavior action editor target name.</summary>
        public const string BehaviorActionEditor = "BehaviorActionEditor";

        /// <summary>Layout package export button target name.</summary>
        public const string ExportLayoutPackageButton = "ExportLayoutPackageButton";
    }

    /// <summary>
    /// Registers tutorials owned by the Designer v3 window.
    /// </summary>
    /// <param name="builder">Tutorial builder.</param>
    public static void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.ForWindow<FrontedDesignerWindow>()
            .Package(Tours.Overview)
                .Dialogue(new DialogueFlowItem
                {
                    Speaker = "爱丽丝·德罗斯",
                    LinesKey = "Dialogue.DesignerV3Overview.Lines"
                })
            .Package(Tours.LayoutEditBasic)
                .StepKey("Step.DesignerV3LayoutEditBasic.0.Title")
                    .TextKey("Step.DesignerV3LayoutEditBasic.0.Description")
                    .TargetName(nameof(LayerPanelHostGrid))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .StepKey("Step.DesignerV3LayoutEditBasic.1.Title")
                    .TextKey("Step.DesignerV3LayoutEditBasic.1.Description")
                    .TargetName(nameof(LayerPanelScrollViewer))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .StepKey("Step.DesignerV3LayoutEditBasic.2.Title")
                    .TextKey("Step.DesignerV3LayoutEditBasic.2.Description")
                    .TargetName(nameof(PreviewWorkspace))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .Placement(ProductTourPlacement.RightBottom)
                    .AllowMissingTarget()
                .StepKey("Step.DesignerV3LayoutEditBasic.3.Title")
                    .TextKey("Step.DesignerV3LayoutEditBasic.3.Description")
                    .TargetName(nameof(PreviewWorkspace))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .Placement(ProductTourPlacement.RightBottom)
                    .AllowMissingTarget()
                .StepKey("Step.DesignerV3LayoutEditBasic.4.Title")
                    .TextKey("Step.DesignerV3LayoutEditBasic.4.Description")
                    .TargetName(nameof(PreviewWorkspace))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .Placement(ProductTourPlacement.RightBottom)
                    .AllowMissingTarget()
                .StepKey("Step.DesignerV3LayoutEditBasic.5.Title")
                    .TextKey("Step.DesignerV3LayoutEditBasic.5.Description")
                    .TargetName(nameof(PreviewWorkspace))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .Placement(ProductTourPlacement.RightBottom)
                    .AllowMissingTarget()
                .StepKey("Step.DesignerV3LayoutEditBasic.6.Title")
                    .TextKey("Step.DesignerV3LayoutEditBasic.6.Description")
                    .TargetName(nameof(SaveLayoutButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.PropertyPanelBasic)
                .OnDemand()
                .StepKey("Step.DesignerV3PropertyPanelBasic.0.Title")
                    .TextKey("Step.DesignerV3PropertyPanelBasic.0.Description")
                    .TargetName(nameof(PropertyPanelHost))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .StepKey("Step.DesignerV3PropertyPanelBasic.1.Title")
                    .TextKey("Step.DesignerV3PropertyPanelBasic.1.Description")
                    .TargetName(TutorialTargets.FirstEditablePropertyEditor)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .StepKey("Step.DesignerV3PropertyPanelBasic.2.Title")
                    .TextKey("Step.DesignerV3PropertyPanelBasic.2.Description")
                    .TargetName(TutorialTargets.PropertyApplyButton)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .StepKey("Step.DesignerV3PropertyPanelBasic.3.Title")
                    .TextKey("Step.DesignerV3PropertyPanelBasic.3.Description")
                    .PreStepAction(ScrollDesignerPropertyPanelToBehaviorAreaAction())
                    .TargetName(nameof(BehaviorPanelHost))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.PackageImportExport)
                .StepKey("Step.DesignerV3PackageImportExport.0.Title")
                    .TextKey("Step.DesignerV3PackageImportExport.0.Description")
                    .NoTarget()
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                .StepKey("Step.DesignerV3PackageImportExport.1.Title")
                    .TextKey("Step.DesignerV3PackageImportExport.1.Description")
                    .TargetName(TutorialTargets.ExportLayoutPackageButton)
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.HelpBasic)
                .StepKey("Step.DesignerV3HelpBasic.0.Title")
                    .TextKey("Step.DesignerV3HelpBasic.0.Description")
                    .PreStepAction(ScrollDesignerHelpButtonIntoViewAction())
                    .TargetName(nameof(DesignerHelpButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                .Build();
    }

    private static TutorialStepAction ScrollDesignerPropertyPanelToBehaviorAreaAction() =>
        new("ScrollDesignerPropertyPanelToBehaviorArea", (context, cancellationToken) =>
            ScrollDesignerPropertyPanelToBehaviorAreaAsync(context.Services, cancellationToken));

    private static TutorialStepAction ScrollDesignerHelpButtonIntoViewAction() =>
        new("ScrollDesignerHelpButtonIntoView", (context, cancellationToken) =>
            ScrollDesignerHelpButtonIntoViewAsync(context.Services, cancellationToken));

    private static async Task ScrollDesignerPropertyPanelToBehaviorAreaAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        _ = serviceProvider;
        if (FindActiveDesignerWindow() is not { } window)
        {
            return;
        }

        await window.Dispatcher.InvokeAsync(
            () =>
            {
                if (window.BehaviorPanelHost.IsVisible)
                {
                    window.BehaviorPanelHost.BringIntoView();
                }
                else if (window.AnimationPartsPanelHost.IsVisible)
                {
                    window.AnimationPartsPanelHost.BringIntoView();
                }
                else
                {
                    window.PropertyPanelScrollViewer.ScrollToBottom();
                }

                window.UpdateLayout();
            },
            DispatcherPriority.ContextIdle,
            cancellationToken);
    }

    private static async Task ScrollDesignerHelpButtonIntoViewAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        _ = serviceProvider;
        if (FindActiveDesignerWindow() is not { } window)
        {
            return;
        }

        await window.Dispatcher.InvokeAsync(
            () =>
            {
                window.DesignerHelpButton.BringIntoView();
                window.UpdateLayout();
            },
            DispatcherPriority.ContextIdle,
            cancellationToken);
    }

    private static FrontedDesignerWindow? FindActiveDesignerWindow() =>
        Application.Current?.Windows
            .OfType<FrontedDesignerWindow>()
            .FirstOrDefault(window => window.IsActive)
        ?? Application.Current?.Windows
            .OfType<FrontedDesignerWindow>()
            .FirstOrDefault(window => window.IsVisible);
}
