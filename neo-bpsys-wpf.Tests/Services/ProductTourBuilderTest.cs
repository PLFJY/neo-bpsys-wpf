using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using neo_bpsys_wpf.ProductTour;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// 测试产品导览构建器 API。
/// </summary>
public sealed class ProductTourBuilderTest
{
    [Fact]
    public void StepBuilder_ShouldConfigureNameTarget()
    {
        var fixture = new AuthoringFixture();

        fixture.Builder.ForRegion<TestTutorialOwner>()
            .Package(new TutorialPackageRef("Package.Test"))
                .Step("Title")
                    .Text("Description")
                    .TargetName("Target")
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .Placement(ProductTourPlacement.Bottom)
                    .AllowMissingTarget()
                    .WaitFor("Signal.Test")
                .Build();

        var package = fixture.PackageRegistry.GetPackage("Package.Test");
        Assert.NotNull(package);
        Assert.Equal("Package.Test", package!.PackageId);
        Assert.Equal("Page.TestOwner", package.PageKey);
        Assert.Equal(1, package.Sequence);
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
    public void StepBuilder_ShouldConfigureTagTarget()
    {
        var fixture = new AuthoringFixture();

        fixture.Builder.ForRegion<TestTutorialOwner>()
            .Package(new TutorialPackageRef("Package.ElementTag"))
                .Step("Title")
                    .Text("Description")
                    .TargetTag("BpWindowId")
                .Build();

        var package = fixture.PackageRegistry.GetPackage("Package.ElementTag");
        Assert.NotNull(package);
        var step = Assert.Single(package!.Steps);
        Assert.Equal(TutorialTargetKind.ElementTag, step.TargetKind);
        Assert.Equal("BpWindowId", step.TargetKey);
        Assert.Null(step.TargetName);
    }

    [Fact]
    public void StepBuilder_ShouldConfigureNavigationTarget()
    {
        var fixture = new AuthoringFixture();

        fixture.Builder.ForRegion<TestTutorialOwner>()
            .Package(new TutorialPackageRef("Package.Navigation"))
                .Step("Title")
                    .Text("Description")
                    .TargetNavigation<TestPage>()
                    .WaitFor("Signal.Navigation")
                .Build();

        var package = fixture.PackageRegistry.GetPackage("Package.Navigation");
        Assert.NotNull(package);
        var step = Assert.Single(package!.Steps);
        Assert.Equal(TutorialTargetKind.NavigationItem, step.TargetKind);
        Assert.Equal(typeof(TestPage).FullName, step.TargetKey);
        Assert.Null(step.TargetName);
    }

    [Fact]
    public void StepBuilder_ShouldConfigureDescendantTypeTarget()
    {
        var fixture = new AuthoringFixture();

        fixture.Builder.ForRegion<TestTutorialOwner>()
            .Package(new TutorialPackageRef("Package.Descendant"))
                .Step("Title")
                    .Text("Description")
                    .TargetDescendantType("SurvivorPickPanel", typeof(TestTargetControl))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Build();

        var package = fixture.PackageRegistry.GetPackage("Package.Descendant");
        Assert.NotNull(package);
        var step = Assert.Single(package!.Steps);
        Assert.Equal(TutorialTargetKind.DescendantType, step.TargetKind);
        Assert.Equal("SurvivorPickPanel", step.TargetName);
        Assert.Equal(typeof(TestTargetControl).FullName, step.TargetKey);
        Assert.True(step.AllowMissingTarget);
    }

    [Fact]
    public void StepBuilder_ShouldConfigureNoTarget()
    {
        var fixture = new AuthoringFixture();

        fixture.Builder.ForRegion<TestTutorialOwner>()
            .Package(new TutorialPackageRef("Package.NoTarget"))
                .Step("Title")
                    .Text("Description")
                    .NoTarget()
                .Build();

        var package = fixture.PackageRegistry.GetPackage("Package.NoTarget");
        Assert.NotNull(package);
        var step = Assert.Single(package!.Steps);
        Assert.Equal(TutorialTargetKind.None, step.TargetKind);
        Assert.Null(step.TargetKey);
        Assert.Null(step.TargetName);
    }

    [Fact]
    public void StepBuilder_ShouldAppendMultiplePreStepActions()
    {
        var fixture = new AuthoringFixture();
        var first = new TutorialStepAction("First", (_, _) => Task.CompletedTask);
        var second = new TutorialStepAction("Second", (_, _) => Task.CompletedTask);

        fixture.Builder.ForRegion<TestTutorialOwner>()
            .Package(new TutorialPackageRef("Package.PreActions"))
                .Step("Title")
                    .PreStepAction(first)
                    .PreStepAction(second)
                .Build();

        var step = Assert.Single(fixture.PackageRegistry.GetPackage("Package.PreActions")!.Steps);
        Assert.Equal(["First", "Second"], step.PreStepActions.Select(action => action.Name).ToArray());
    }

    [Fact]
    public void StepBuilder_ShouldAppendMultiplePostStepActions()
    {
        var fixture = new AuthoringFixture();
        var first = new TutorialStepAction("First", (_, _) => Task.CompletedTask);
        var second = new TutorialStepAction("Second", (_, _) => Task.CompletedTask);

        fixture.Builder.ForRegion<TestTutorialOwner>()
            .Package(new TutorialPackageRef("Package.PostActions"))
                .Step("Title")
                    .PostStepAction(first)
                    .PostStepAction(second)
                .Build();

        var step = Assert.Single(fixture.PackageRegistry.GetPackage("Package.PostActions")!.Steps);
        Assert.Equal(["First", "Second"], step.PostStepActions.Select(action => action.Name).ToArray());
    }

    [Fact]
    public void StepBuilder_ShouldAcceptLambdaPreStepActionOverloads()
    {
        var fixture = new AuthoringFixture();

        fixture.Builder.ForRegion<TestTutorialOwner>()
            .Package(new TutorialPackageRef("Package.LambdaPre"))
                .Step("Title")
                    .PreStepAction("NamedAsync", (_, _) => Task.CompletedTask)
                    .PreStepAction((_, _) => Task.CompletedTask)
                    .PreStepAction("NamedSync", _ => { })
                    .PreStepAction(_ => { })
            .Build();

        var step = Assert.Single(fixture.PackageRegistry.GetPackage("Package.LambdaPre")!.Steps);
        Assert.Equal(4, step.PreStepActions.Count);
        Assert.Equal("NamedAsync", step.PreStepActions[0].Name);
        Assert.Equal("(_, _) => Task.CompletedTask", step.PreStepActions[1].Name);
        Assert.Equal("NamedSync", step.PreStepActions[2].Name);
        Assert.Equal("_ => { }", step.PreStepActions[3].Name);
    }

    [Fact]
    public void StepBuilder_ShouldAcceptLambdaPostStepActionOverloads()
    {
        var fixture = new AuthoringFixture();

        fixture.Builder.ForRegion<TestTutorialOwner>()
            .Package(new TutorialPackageRef("Package.LambdaPost"))
                .Step("Title")
                    .PostStepAction("NamedAsync", (_, _) => Task.CompletedTask)
                    .PostStepAction((_, _) => Task.CompletedTask)
                    .PostStepAction("NamedSync", _ => { })
                    .PostStepAction(_ => { })
            .Build();

        var step = Assert.Single(fixture.PackageRegistry.GetPackage("Package.LambdaPost")!.Steps);
        Assert.Equal(4, step.PostStepActions.Count);
        Assert.Equal("NamedAsync", step.PostStepActions[0].Name);
        Assert.Equal("(_, _) => Task.CompletedTask", step.PostStepActions[1].Name);
        Assert.Equal("NamedSync", step.PostStepActions[2].Name);
        Assert.Equal("_ => { }", step.PostStepActions[3].Name);
    }

    [Fact]
    public void StepBuilder_ShouldNormalizeMultiLineLambdaActionName()
    {
        var fixture = new AuthoringFixture();

        fixture.Builder.ForRegion<TestTutorialOwner>()
            .Package(new TutorialPackageRef("Package.LambdaMultiline"))
                .Step("Title")
                    .PreStepAction((ctx, ct) =>
                    {
                        return Task.Delay(0, ct);
                    })
            .Build();

        var step = Assert.Single(fixture.PackageRegistry.GetPackage("Package.LambdaMultiline")!.Steps);
        var name = Assert.Single(step.PreStepActions).Name;
        Assert.DoesNotContain('\n', name);
        Assert.DoesNotContain('\r', name);
        Assert.Contains("=>", name);
        Assert.Contains("Task.Delay", name);
    }

    [Fact]
    public void FlowBuilderCreatesExpectedDefinition()
    {
        var fixture = new AuthoringFixture();
        fixture.PackageRegistry.Register(CreatePackage("Package.One"));
        fixture.PackageRegistry.Register(CreatePackage("Package.Two"));

        var flow = fixture.Builder.Flow("Flow.Test")
            .Version(3)
            .Step(new DialogueFlowItem { Speaker = "Speaker", Lines = ["Line 1", "Line 2"] })
            .Step(new TutorialPackageRef("Package.One"))
            .Step(new TutorialPackageRef("Package.Two"))
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

    [Fact]
    public void Flow_ShouldAutoCoverPackageSteps()
    {
        var fixture = new AuthoringFixture();
        fixture.PackageRegistry.Register(CreatePackage("Package.One"));
        fixture.PackageRegistry.Register(CreatePackage("Package.Two"));

        var flow = fixture.Builder.Flow("Flow.AutoCover")
            .Step(new TutorialPackageRef("Package.One"))
            .Step(new TutorialPackageRef("Package.Two"))
            .Step(new TutorialPackageRef("Package.One"))
            .Build();

        Assert.Equal(["Package.One", "Package.Two"], flow.IncludedPackageIds);
        Assert.Equal(
            ["Package.One", "Package.Two", "Package.One"],
            flow.Items.OfType<PackageFlowItem>().Select(item => item.PackageId).ToArray());
    }

    [Fact]
    public void Flow_ShouldNotRequireManualCovers()
    {
        var fixture = new AuthoringFixture();
        fixture.PackageRegistry.Register(CreatePackage("Package.One"));

        var flow = fixture.Builder.Flow("Flow.NoManualCovers")
            .Step(new TutorialPackageRef("Package.One"))
            .Build();

        Assert.Equal(["Package.One"], flow.IncludedPackageIds);
    }

    [Fact]
    public void Flow_ShouldRejectMissingPackageRef()
    {
        var fixture = new AuthoringFixture();

        Assert.Throws<InvalidOperationException>(() =>
            fixture.Builder.Flow("Flow.Missing")
                .Step(new TutorialPackageRef("Package.Missing"))
                .Build());
    }

    [Fact]
    public void Flow_ShouldRejectFallbackPackage()
    {
        var fixture = new AuthoringFixture();
        fixture.PackageRegistry.Register(new TutorialPackageDefinition
        {
            PackageId = "Package.Fallback",
            PageKey = "Page.Test",
            Steps =
            [
                new ProductTourStep
                {
                    Title = "功能教学",
                    Description = "这个功能的详细教学将在你首次进入对应页面时提供。"
                }
            ]
        });

        Assert.Throws<InvalidOperationException>(() =>
            fixture.Builder.Flow("Flow.Fallback")
                .Step(new TutorialPackageRef("Package.Fallback"))
                .Build());
    }

    [Fact]
    public void Owner_ShouldRejectDuplicatePackageContent()
    {
        var fixture = new AuthoringFixture();
        var owner = fixture.Builder.ForRegion<TestTutorialOwner>();
        owner.Package(new TutorialPackageRef("Package.One"))
            .Step("Title")
                .Text("Description")
                .TargetName("Target")
            .Build();

        Assert.Throws<InvalidOperationException>(() =>
            owner.Package(new TutorialPackageRef("Package.Two"))
                .Step("Title")
                    .Text("Different description")
                    .TargetName("Target")
                .Build());
    }

    [Fact]
    public void PublicAuthoringApi_ShouldNotExposeOldStepCreationMethods()
    {
        var forbiddenNames = new[]
        {
            "Action",
            "Group",
            "Navigation",
            "Tag",
            "TagAction",
            "Descendant",
            "DescendantAction",
            "StepNavigationItem",
            "StepDescendantType",
            "StepElementTag",
            "Item",
            "Include",
            "Covers",
            "CanRun"
        };
        var packageMethodNames = typeof(ITutorialPackageBuilder<TestTutorialOwner>)
            .GetMethods()
            .Select(method => method.Name)
            .ToArray();
        var flowMethodNames = typeof(ITutorialFlowBuilder)
            .GetMethods()
            .Select(method => method.Name)
            .ToArray();

        foreach (var forbiddenName in forbiddenNames)
        {
            Assert.DoesNotContain(forbiddenName, packageMethodNames);
            Assert.DoesNotContain(forbiddenName, flowMethodNames);
        }

        Assert.Equal(["Step"], packageMethodNames.Where(name => name == "Step").Distinct().ToArray());
        Assert.Contains("Dialogue", packageMethodNames);
    }

    [Fact]
    public void Package_ShouldAllowDialogueAndStepItemsInOrder()
    {
        var fixture = new AuthoringFixture();
        fixture.Builder.ForRegion<TestTutorialOwner>()
            .Package(new TutorialPackageRef("Package.Mixed"))
                .Step("First")
                    .NoTarget()
                .Dialogue(new DialogueFlowItem { Speaker = "Alice", Lines = ["Line"] })
                .Step("Last")
                    .NoTarget()
                .Build();

        var package = fixture.PackageRegistry.GetPackage("Package.Mixed")!;
        Assert.Collection(
            package.Items,
            item => Assert.Equal("First", Assert.IsType<TutorialPackageStepItem>(item).Step.Title),
            item => Assert.Equal("Alice", Assert.IsType<TutorialPackageDialogueItem>(item).Dialogue.Speaker),
            item => Assert.Equal("Last", Assert.IsType<TutorialPackageStepItem>(item).Step.Title));
    }

    private sealed class TestTutorialOwner : FrameworkElement, ITutorialOwner<TestTutorialOwner>
    {
        /// <inheritdoc />
        public static string TutorialKey => "Page.TestOwner";

        /// <inheritdoc />
        public static void RegisterTutorials(ITutorialBuilder builder)
        {
        }
    }

    private sealed class TestPage : Page
    {
    }

    private sealed class TestTargetControl : FrameworkElement
    {
    }

    private sealed class AuthoringFixture
    {
        public TutorialPackageRegistry PackageRegistry { get; } = new();

        public TutorialSequenceRegistry SequenceRegistry { get; } = new();

        public TutorialFlowRegistry FlowRegistry { get; } = new();

        public TutorialBuilder Builder => new(PackageRegistry, SequenceRegistry, FlowRegistry);
    }

    private static TutorialPackageDefinition CreatePackage(
        string packageId,
        string targetName = "Target",
        string title = "Title") =>
        new()
        {
            PackageId = packageId,
            PageKey = "Page.Test",
            Steps =
            [
                new ProductTourStep
                {
                    TargetName = targetName,
                    Title = title,
                    Description = "Description"
                }
            ]
        };
}
