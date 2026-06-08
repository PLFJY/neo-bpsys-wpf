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
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
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

    [Fact]
    public async Task SameProperty_NewAnimationCancelsOldButDoesNotRemoveNewConflict()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var guid = Guid.NewGuid();
            var root = new Canvas();
            var element = Generated(new Border(), guid, "Target");
            root.Children.Add(element);
            var runtime = new FrontedAnimationRuntime();

            // Start first long animation on Opacity
            var firstTask = runtime.ExecuteAsync(new FrontedGraphActionRequest
            {
                RequestType = FrontedGraphActionRequestType.AnimateProperty,
                Target = "Self",
                PropertyName = "Opacity",
                Values = new Dictionary<string, string?> { ["To"] = "0.5" },
                DurationMs = 5000
            }, Context(root, guid));

            // Allow first animation to start
            await Task.Yield();

            // Start second animation on the same property (should cancel the first).
            // Do NOT await completion — WPF animations require a HwndSource (window)
            // to tick the animation clock; without one, BeginAnimation never fires
            // the Completed event. Instead, the third animation below will cancel
            // this one, and we verify conflict tracking via that operation.
            _ = runtime.ExecuteAsync(new FrontedGraphActionRequest
            {
                RequestType = FrontedGraphActionRequestType.AnimateProperty,
                Target = "Self",
                PropertyName = "Opacity",
                Values = new Dictionary<string, string?> { ["To"] = "0.8" },
                DurationMs = 5000
            }, Context(root, guid));

            // Allow second animation to register in Conflicts
            await Task.Yield();

            // First animation should have completed (cancelled) without throwing
            await firstTask;

            // Start a third animation to verify the conflict tracking is intact,
            // which also cancels the second animation via CancelConflict.
            await runtime.ExecuteAsync(new FrontedGraphActionRequest
            {
                RequestType = FrontedGraphActionRequestType.AnimateProperty,
                Target = "Self",
                PropertyName = "Opacity",
                Values = new Dictionary<string, string?> { ["To"] = "1.0" },
                DurationMs = 0
            }, Context(root, guid));

            // Read the final value on the Dispatcher to avoid cross-thread issues
            // when the await continuation runs on a ThreadPool thread after the
            // CancelConflict chain schedules the second animation's continuation
            // via RunContinuationsAsynchronously.
            var finalOpacity = 0.0;
            root.Dispatcher.Invoke(() => finalOpacity = element.Opacity);
            Assert.Equal(1.0, finalOpacity, 3);
        });
    }

    [Fact]
    public async Task ResetAll_CancelsCurrentAnimationAndRestoresBase()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var guid = Guid.NewGuid();
            var root = new Canvas();
            var element = Generated(new Border { Opacity = 0.25 }, guid, "Target");
            root.Children.Add(element);
            var runtime = new FrontedAnimationRuntime();

            // Set property to change base
            await runtime.ExecuteAsync(new FrontedGraphActionRequest
            {
                RequestType = FrontedGraphActionRequestType.SetProperty,
                Target = "Self",
                PropertyName = "Opacity",
                Values = new Dictionary<string, string?> { ["Value"] = "0.5" }
            }, Context(root, guid));

            // Start long animation
            var animTask = runtime.ExecuteAsync(new FrontedGraphActionRequest
            {
                RequestType = FrontedGraphActionRequestType.AnimateProperty,
                Target = "Self",
                PropertyName = "Opacity",
                Values = new Dictionary<string, string?> { ["To"] = "0.9" },
                DurationMs = 5000
            }, Context(root, guid));

            await Task.Yield();

            // Reset all
            runtime.ResetAll(Context(root, guid));
            await animTask; // should complete without throwing

            // Value should be restored to original base (0.25, not 0.5)
            var finalOpacity = 0.0;
            root.Dispatcher.Invoke(() => finalOpacity = element.Opacity);
            Assert.Equal(0.25, finalOpacity, 3);
        });
    }

    [Fact]
    public async Task Release_CancelsFireAndForgetAnimation()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var guid = Guid.NewGuid();
            var root = new Canvas();
            var element = Generated(new Border(), guid, "Target");
            root.Children.Add(element);
            var runtime = new FrontedAnimationRuntime();

            // Start long animation with fire-and-forget semantics via the executor
            var executor = new FrontedAnimationRuntimeActionExecutor(
                runtime, root, guid, "Test", "window", "canvas");

            await executor.ExecuteAsync(new FrontedGraphActionRequest
            {
                RequestType = FrontedGraphActionRequestType.AnimateProperty,
                Target = "Self",
                PropertyName = "Opacity",
                Values = new Dictionary<string, string?> { ["To"] = "0.9" },
                DurationMs = 5000,
                WaitForCompletion = false
            }, CancellationToken.None);

            // Release should cancel all in-flight animations without throwing
            runtime.Release(root);
        });
    }

    [Fact]
    public async Task WaitFalse_FireAndForget_DoesNotRaiseUnobservedException()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var guid = Guid.NewGuid();
            var root = new Canvas();
            var element = Generated(new Border(), guid, "Target");
            root.Children.Add(element);
            var runtime = new FrontedAnimationRuntime();

            var executor = new FrontedAnimationRuntimeActionExecutor(
                runtime, root, guid, "Test", "window", "canvas");

            // Fire-and-forget a long animation
            await executor.ExecuteAsync(new FrontedGraphActionRequest
            {
                RequestType = FrontedGraphActionRequestType.AnimateProperty,
                Target = "Self",
                PropertyName = "Opacity",
                Values = new Dictionary<string, string?> { ["To"] = "0.9" },
                DurationMs = 50000,
                WaitForCompletion = false
            }, CancellationToken.None);

            // Release should cleanly cancel without raising exceptions
            runtime.Release(root);
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
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var task = action();
                // Pump the Dispatcher so that async continuations (posted via
                // DispatcherSynchronizationContext) and WPF internal operations
                // can progress.  Without this, await Task.Yield() and other
                // continuations queued on the Dispatcher would never run.
                var frame = new DispatcherFrame();
                _ = task.ContinueWith(_ =>
                {
                    try { frame.Continue = false; } catch { }
                }, TaskScheduler.Default);
                Dispatcher.PushFrame(frame);
                task.GetAwaiter().GetResult();
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return tcs.Task;
    }
}
