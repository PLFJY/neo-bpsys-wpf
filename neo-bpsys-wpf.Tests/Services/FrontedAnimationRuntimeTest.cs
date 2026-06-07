using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public class FrontedAnimationRuntimeTest
{
    [Fact]
    public void TargetResolver_ResolveSelf_ByBehaviorGuid()
    {
        RunOnStaThread(() =>
        {
            var guid = Guid.NewGuid();
            var root = new Canvas();
            var target = Generated(new Border(), guid, "Target");
            root.Children.Add(target);

            var resolved = new FrontedAnimationTargetResolver().Resolve(
                new FrontedAnimationTargetReference { Kind = FrontedAnimationTargetReferenceKind.Self },
                Context(root, guid));

            Assert.Same(target, resolved!.Element);
        });
    }

    [Fact]
    public void TargetResolver_ResolveGuidString_Target()
    {
        RunOnStaThread(() =>
        {
            var guid = Guid.NewGuid();
            var root = new Canvas();
            var target = Generated(new Border(), guid, "Target");
            root.Children.Add(target);

            var resolved = new FrontedAnimationTargetResolver().Resolve(
                FrontedAnimationTargetReference.Parse($"guid:{guid}"),
                Context(root, Guid.NewGuid()));

            Assert.Same(target, resolved!.Element);
        });
    }

    [Fact]
    public void TargetResolver_MissingGuid_ReturnsNullAndDoesNotThrow()
    {
        RunOnStaThread(() =>
        {
            var root = new Canvas();

            var resolved = new FrontedAnimationTargetResolver().Resolve(
                FrontedAnimationTargetReference.Parse(Guid.NewGuid().ToString()),
                Context(root, Guid.NewGuid()));

            Assert.Null(resolved);
        });
    }

    [Fact]
    public void FrameworkElementAdapter_SetVisualOffset_UsesRenderTransform()
    {
        RunOnStaThread(() =>
        {
            var target = Target(new Border());
            var adapter = new FrameworkElementCommonAdapter();

            adapter.SetValue(target, "VisualOffsetX", "42", Context(new Canvas(), target.BehaviorGuid));

            var group = Assert.IsType<TransformGroup>(target.Element.RenderTransform);
            Assert.Equal(42, Assert.IsType<TranslateTransform>(group.Children[^1]).X);
            Assert.True(double.IsNaN(Canvas.GetLeft(target.Element)));
        });
    }

    [Fact]
    public void FrameworkElementAdapter_SetScaleAndRotation_UseTransformGroup()
    {
        RunOnStaThread(() =>
        {
            var target = Target(new Border());
            var adapter = new FrameworkElementCommonAdapter();

            adapter.SetValue(target, "ScaleX", "1.5", Context(new Canvas(), target.BehaviorGuid));
            adapter.SetValue(target, "Rotation", "30", Context(new Canvas(), target.BehaviorGuid));

            var group = Assert.IsType<TransformGroup>(target.Element.RenderTransform);
            Assert.Equal(1.5, group.Children.OfType<ScaleTransform>().Single().ScaleX);
            Assert.Equal(30, group.Children.OfType<RotateTransform>().Single().Angle);
        });
    }

    [Fact]
    public void ShapeAdapter_SetFillColor()
    {
        RunOnStaThread(() =>
        {
            var target = Target(new Rectangle());
            new ShapeAnimatablePropertyAdapter().SetValue(target, "FillColor", "#112233", Context(new Canvas(), target.BehaviorGuid));

            var brush = Assert.IsType<SolidColorBrush>(((Shape)target.Element).Fill);
            Assert.Equal(Color.FromRgb(0x11, 0x22, 0x33), brush.Color);
        });
    }

    [Fact]
    public void TextAdapter_SetTextColor()
    {
        RunOnStaThread(() =>
        {
            var target = Target(new TextBlock());
            new TextAnimatablePropertyAdapter().SetValue(target, "TextColor", "#FF010203", Context(new Canvas(), target.BehaviorGuid));

            var brush = Assert.IsType<SolidColorBrush>(((TextBlock)target.Element).Foreground);
            Assert.Equal(Color.FromArgb(0xFF, 1, 2, 3), brush.Color);
        });
    }

    [Fact]
    public async Task AnimationRuntime_SetAndResetProperty_ChangesAndRestoresTarget()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var guid = Guid.NewGuid();
            var root = new Canvas();
            var element = Generated(new Border { Opacity = 0.25 }, guid, "Target");
            root.Children.Add(element);
            var runtime = new FrontedAnimationRuntime();

            await runtime.ExecuteAsync(new FrontedGraphActionRequest
            {
                RequestType = FrontedGraphActionRequestType.SetProperty,
                Target = "Self",
                PropertyName = "Opacity",
                Values = new Dictionary<string, string?> { ["Value"] = "0.8" }
            }, Context(root, guid));

            Assert.Equal(0.8, element.Opacity, 3);

            await runtime.ExecuteAsync(new FrontedGraphActionRequest
            {
                RequestType = FrontedGraphActionRequestType.ResetProperty,
                Target = "Self",
                PropertyName = "Opacity"
            }, Context(root, guid));

            Assert.Equal(0.25, element.Opacity, 3);
        });
    }

    [Fact]
    public async Task AnimationRuntime_AnimateProperty_DurationZero_SetsImmediately()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var guid = Guid.NewGuid();
            var root = new Canvas();
            var element = Generated(new Border(), guid, "Target");
            root.Children.Add(element);

            await new FrontedAnimationRuntime().ExecuteAsync(new FrontedGraphActionRequest
            {
                RequestType = FrontedGraphActionRequestType.AnimateProperty,
                Target = guid.ToString(),
                PropertyName = "Opacity",
                Values = new Dictionary<string, string?> { ["To"] = "0.35" },
                DurationMs = 0
            }, Context(root, guid));

            Assert.Equal(0.35, element.Opacity, 3);
        });
    }

    [Fact]
    public async Task AnimationRuntime_UnsupportedProperty_LogsAndSkips()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var guid = Guid.NewGuid();
            var root = new Canvas();
            var element = Generated(new Border(), guid, "Target");
            root.Children.Add(element);

            await new FrontedAnimationRuntime().ExecuteAsync(new FrontedGraphActionRequest
            {
                RequestType = FrontedGraphActionRequestType.SetProperty,
                Target = "Self",
                PropertyName = "Missing",
                Values = new Dictionary<string, string?> { ["Value"] = "1" }
            }, Context(root, guid));

            Assert.Equal(1, element.Opacity);
        });
    }

    private static FrontedAnimationExecutionContext Context(Canvas root, Guid selfGuid) =>
        new()
        {
            Root = root,
            SelfBehaviorGuid = selfGuid,
            IsDesignerPreview = true,
            Logger = NullLogger.Instance
        };

    private static FrameworkElement Generated(FrameworkElement element, Guid guid, string name)
    {
        FrontedRendererProperties.SetIsGeneratedControl(element, true);
        FrontedRendererProperties.SetBehaviorGuid(element, guid);
        FrontedRendererProperties.SetRegisteredName(element, name);
        return element;
    }

    private static FrontedAnimationTarget Target(FrameworkElement element)
    {
        var guid = Guid.NewGuid();
        Generated(element, guid, "Target");
        return new FrontedAnimationTarget { Element = element, BehaviorGuid = guid, Name = "Target" };
    }

    private static void RunOnStaThread(Action action)
    {
        ExceptionDispatchInfo? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ExceptionDispatchInfo.Capture(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        exception?.Throw();
    }

    private static Task RunOnStaThreadAsync(Func<Task> action)
    {
        ExceptionDispatchInfo? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                exception = ExceptionDispatchInfo.Capture(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        exception?.Throw();
        return Task.CompletedTask;
    }
}
