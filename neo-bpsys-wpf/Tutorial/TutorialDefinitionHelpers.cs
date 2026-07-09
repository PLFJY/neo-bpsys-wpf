using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.ProductTour;

namespace neo_bpsys_wpf.Tutorial;

internal static class TutorialDefinitionHelpers
{
    /// <summary>
    /// Creates a package definition.
    /// </summary>
    /// <param name="packageId">Package id.</param>
    /// <param name="pageKey">Page key.</param>
    /// <param name="sequence">Package sequence.</param>
    /// <param name="steps">Package steps.</param>
    /// <param name="canRun">Optional run predicate.</param>
    /// <returns>The package definition.</returns>
    /// <summary>
    /// Creates a package definition with an owner-aware run predicate.
    /// </summary>
    /// <param name="packageId">Package id.</param>
    /// <param name="pageKey">Page key.</param>
    /// <param name="sequence">Package sequence.</param>
    /// <param name="steps">Package steps.</param>
    /// <param name="canRun">Optional owner-aware run predicate.</param>
    /// <returns>The package definition.</returns>
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

    public static TutorialPackageDefinition Package(
        string packageId,
        string pageKey,
        int sequence,
        IEnumerable<ProductTourStep> steps,
        Func<IServiceProvider, FrameworkElement?, bool>? canRun)
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
        Task.Delay(TutorialTransitionDelays.NavigationSettleDelay, cancellationToken);

    /// <summary>
    /// Determines whether a tutorial package has been completed in the current tutorial state.
    /// </summary>
    /// <param name="serviceProvider">Service provider used to resolve the tutorial state store.</param>
    /// <param name="packageId">Package id to inspect.</param>
    /// <returns><see langword="true"/> when the package is recorded as completed; otherwise, <see langword="false"/>.</returns>
    public static bool IsPackageCompleted(IServiceProvider serviceProvider, string packageId)
    {
        var stateStore = serviceProvider.GetRequiredService<ITutorialStateStore>();
        var state = stateStore.LoadAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        return state.CompletedPackages.TryGetValue(packageId, out var record)
            && record.CompletionKind == TutorialCompletionKind.Completed;
    }

    /// <summary>
    /// Determines whether a tutorial package has any completion record in the current tutorial state.
    /// </summary>
    /// <param name="serviceProvider">Service provider used to resolve the tutorial state store.</param>
    /// <param name="packageId">Package id to inspect.</param>
    /// <returns><see langword="true"/> when the package is recorded; otherwise, <see langword="false"/>.</returns>
    public static bool IsPackageRecorded(IServiceProvider serviceProvider, string packageId)
    {
        var stateStore = serviceProvider.GetRequiredService<ITutorialStateStore>();
        var state = stateStore.LoadAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        return state.CompletedPackages.ContainsKey(packageId);
    }
}
