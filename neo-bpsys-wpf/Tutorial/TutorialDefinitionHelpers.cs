using System.Windows;
using neo_bpsys_wpf.ProductTour;

namespace neo_bpsys_wpf.Tutorial;

internal static class TutorialDefinitionHelpers
{
    public static TutorialPackageDefinition Package(
        string packageId,
        string pageKey,
        int sequence,
        IEnumerable<ProductTourStep> steps,
        Func<IServiceProvider, bool>? canRun = null)
    {
        var builder = TutorialPackageBuilder.Create(packageId)
            .ForPage(pageKey)
            .Version(1)
            .Sequence(sequence)
            .Kind("ProductTour");

        if (canRun != null)
        {
            builder.CanRun(canRun);
        }

        foreach (var step in steps)
        {
            builder.AddStep(step);
        }

        return builder.Build();
    }

    public static ProductTourStep NavigationStep(
        string targetPageTypeFullName,
        string title,
        string description,
        ProductTourInteractionMode mode,
        string? signalId = null,
        bool allowMissing = false)
    {
        var builder = TutorialPackageBuilder.Create("Transient.Step")
            .ForPage("Transient.Page")
            .StepNavigationItem(targetPageTypeFullName)
            .Title(title)
            .Description(description)
            .Placement(ProductTourPlacement.Right)
            .Interaction(mode)
            .Timeout(TimeSpan.FromSeconds(30));

        if (signalId != null)
        {
            builder.WaitForSignal(signalId);
        }

        if (allowMissing)
        {
            builder.AllowMissingTarget();
        }

        var step = builder.EndStep().Build().Steps[0];
        if (signalId != null)
        {
            step.AfterCompleteAsync = DelayForNavigationTransitionAsync;
        }

        return step;
    }

    public static ProductTourStep Step(
        string? targetName,
        string title,
        string description,
        ProductTourInteractionMode mode,
        string? signalId = null,
        bool allowMissing = false,
        Func<IServiceProvider, CancellationToken, Task>? beforeShowAsync = null,
        ProductTourAvatarPlacement avatarPlacement = ProductTourAvatarPlacement.Auto,
        TutorialAvatarPose? avatarPose = null,
        Point? cardOffset = null,
        string? scrollAnchorName = null)
    {
        var builder = TutorialPackageBuilder.Create("Transient.Step")
            .ForPage("Transient.Page")
            .Step(targetName)
            .Title(title)
            .Description(description)
            .Placement(ProductTourPlacement.Auto)
            .CardOffset(cardOffset ?? default)
            .AvatarPlacement(avatarPlacement)
            .Interaction(mode)
            .Timeout(TimeSpan.FromSeconds(30));

        if (!string.IsNullOrWhiteSpace(scrollAnchorName))
        {
            builder.ScrollAnchor(scrollAnchorName);
        }

        if (avatarPose != null)
        {
            builder.AvatarPose(avatarPose.Value);
        }

        if (signalId != null)
        {
            builder.WaitForSignal(signalId);
        }

        if (allowMissing)
        {
            builder.AllowMissingTarget();
        }

        var step = builder.EndStep().Build().Steps[0];
        step.BeforeShowAsync = beforeShowAsync;
        return step;
    }

    public static ProductTourStep DescendantTypeStep(
        string? hostTargetName,
        string targetTypeFullName,
        string title,
        string description,
        ProductTourInteractionMode mode,
        string? signalId = null,
        bool allowMissing = false)
    {
        var builder = TutorialPackageBuilder.Create("Transient.Step")
            .ForPage("Transient.Page")
            .StepDescendantType(hostTargetName, targetTypeFullName)
            .Title(title)
            .Description(description)
            .Placement(ProductTourPlacement.Auto)
            .Interaction(mode)
            .Timeout(TimeSpan.FromSeconds(30));

        if (signalId != null)
        {
            builder.WaitForSignal(signalId);
        }

        if (allowMissing)
        {
            builder.AllowMissingTarget();
        }

        return builder.EndStep().Build().Steps[0];
    }

    public static ProductTourStep ElementTagStep(
        string targetTag,
        string title,
        string description,
        ProductTourInteractionMode mode,
        string? signalId = null,
        bool allowMissing = false,
        Point? cardOffset = null,
        Func<IServiceProvider, CancellationToken, Task>? beforeShowAsync = null)
    {
        var builder = TutorialPackageBuilder.Create("Transient.Step")
            .ForPage("Transient.Page")
            .StepElementTag(targetTag)
            .Title(title)
            .Description(description)
            .Placement(ProductTourPlacement.Auto)
            .CardOffset(cardOffset ?? default)
            .Interaction(mode)
            .Timeout(TimeSpan.FromSeconds(30));

        if (signalId != null)
        {
            builder.WaitForSignal(signalId);
        }

        if (allowMissing)
        {
            builder.AllowMissingTarget();
        }

        var step = builder.EndStep().Build().Steps[0];
        step.BeforeShowAsync = beforeShowAsync;
        return step;
    }

    private static Task DelayForNavigationTransitionAsync(IServiceProvider _, CancellationToken cancellationToken) =>
        Task.Delay(450, cancellationToken);
}
