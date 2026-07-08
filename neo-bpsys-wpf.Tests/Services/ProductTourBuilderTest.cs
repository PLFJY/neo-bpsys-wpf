using neo_bpsys_wpf.ProductTour;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// Tests Product Tour builder APIs.
/// </summary>
public sealed class ProductTourBuilderTest
{
    [Fact]
    public void PackageBuilderCreatesExpectedDefinition()
    {
        var package = TutorialPackageBuilder.Create("Package.Test")
            .ForPage("Page.Test")
            .Version(2)
            .Sequence(100)
            .Kind("ProductTour")
            .Step("Target")
                .Title("Title")
                .Description("Description")
                .Placement(ProductTourPlacement.Bottom)
                .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                .WaitForSignal("Signal.Test")
                .AllowMissingTarget()
                .EndStep()
            .Build();

        Assert.Equal("Package.Test", package.PackageId);
        Assert.Equal("Page.Test", package.PageKey);
        Assert.Equal(2, package.Version);
        Assert.Equal(100, package.Sequence);
        Assert.Equal("ProductTour", package.Kind);
        var step = Assert.Single(package.Steps);
        Assert.Equal("Target", step.TargetName);
        Assert.Equal("Title", step.Title);
        Assert.Equal("Description", step.Description);
        Assert.Equal(ProductTourPlacement.Bottom, step.Placement);
        Assert.Equal(ProductTourInteractionMode.AllowTargetOnly, step.InteractionMode);
        Assert.Equal("Signal.Test", step.WaitForSignalId);
        Assert.Equal(TutorialExpectedAction.SignalReceived, step.ExpectedAction);
        Assert.True(step.AllowMissingTarget);
    }

    [Fact]
    public void StepNavigationItemCreatesNavigationTargetStep()
    {
        var package = TutorialPackageBuilder.Create("Package.Navigation")
            .ForPage("Page.Main")
            .StepNavigationItem("Example.TeamInfoPage")
                .Title("Title")
                .Description("Description")
                .EndStep()
            .Build();

        var step = Assert.Single(package.Steps);
        Assert.Equal(TutorialTargetKind.NavigationItem, step.TargetKind);
        Assert.Equal("Example.TeamInfoPage", step.TargetKey);
        Assert.Null(step.TargetName);
    }

    [Fact]
    public void StepDescendantTypeCreatesDescendantTypeTargetStep()
    {
        var package = TutorialPackageBuilder.Create("Package.Descendant")
            .ForPage("Page.Pick")
            .StepDescendantType("SurvivorPickPanel", "Example.CharacterSelector")
                .Title("Title")
                .Description("Description")
                .EndStep()
            .Build();

        var step = Assert.Single(package.Steps);
        Assert.Equal(TutorialTargetKind.DescendantType, step.TargetKind);
        Assert.Equal("SurvivorPickPanel", step.TargetName);
        Assert.Equal("Example.CharacterSelector", step.TargetKey);
    }

    [Fact]
    public void FlowBuilderCreatesExpectedDefinition()
    {
        var flow = TutorialFlowBuilder.Create("Flow.Test")
            .Version(3)
            .Include("Package.One")
            .Include("Package.Two")
            .Dialogue("Speaker", "Line 1", "Line 2")
            .Package("Package.One")
            .Package("Package.Two")
            .Build();

        Assert.Equal("Flow.Test", flow.FlowId);
        Assert.Equal(3, flow.Version);
        Assert.Equal(["Package.One", "Package.Two"], flow.IncludedPackageIds);
        Assert.Collection(
            flow.Items,
            item =>
            {
                var dialogue = Assert.IsType<DialogueFlowItem>(item);
                Assert.Equal("Speaker", dialogue.Speaker);
                Assert.Equal(["Line 1", "Line 2"], dialogue.Lines);
            },
            item => Assert.Equal("Package.One", Assert.IsType<PackageFlowItem>(item).PackageId),
            item => Assert.Equal("Package.Two", Assert.IsType<PackageFlowItem>(item).PackageId));
    }
}
