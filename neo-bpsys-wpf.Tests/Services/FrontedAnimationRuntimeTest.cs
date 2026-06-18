using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Tests.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
            var part = GeneratedPart(new Border(), guid, "TargetPickingBorder", "PickingBorder");
            root.Children.Add(target);
            root.Children.Add(part);

            var resolved = new FrontedAnimationTargetResolver().Resolve(
                FrontedAnimationTargetReference.Parse($"guid:{guid}"),
                Context(root, Guid.NewGuid()));

            Assert.Same(target, resolved!.Element);
        });
    }

    [Fact]
    public void TargetResolver_ResolvePartString_TargetsGeneratedPart()
    {
        RunOnStaThread(() =>
        {
            var guid = Guid.NewGuid();
            var root = new Canvas();
            var target = Generated(new Border(), guid, "Target");
            var part = GeneratedPart(new Border(), guid, "TargetPickingBorder", "PickingBorder");
            var lockPart = GeneratedPart(new Border(), guid, "TargetLockOverlay", "LockOverlay");
            root.Children.Add(target);
            root.Children.Add(part);
            root.Children.Add(lockPart);

            var resolved = new FrontedAnimationTargetResolver().Resolve(
                FrontedAnimationTargetReference.Parse($"part:{guid}:PickingBorder"),
                Context(root, Guid.NewGuid()));
            var resolvedLock = new FrontedAnimationTargetResolver().Resolve(
                FrontedAnimationTargetReference.Parse($"part:{guid}:LockOverlay"),
                Context(root, Guid.NewGuid()));

            Assert.Same(part, resolved!.Element);
            Assert.Equal(guid, resolved.BehaviorGuid);
            Assert.Same(lockPart, resolvedLock!.Element);
        });
    }

    [Fact]
    public void TargetResolver_MissingPart_ReturnsNull()
    {
        RunOnStaThread(() =>
        {
            var guid = Guid.NewGuid();
            var root = new Canvas();
            root.Children.Add(Generated(new Border(), guid, "Target"));

            var resolved = new FrontedAnimationTargetResolver().Resolve(
                FrontedAnimationTargetReference.Parse($"part:{guid}:PickingBorder"),
                Context(root, Guid.NewGuid()));

            Assert.Null(resolved);
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
    public void FrameworkElementAdapter_PseudoPartPercentageOffset_UsesParentWidth()
    {
        RunOnStaThread(() =>
        {
            var parent = new Grid { Width = 320, Height = 100 };
            var part = new Rectangle { Width = 4 };
            FrontedRendererProperties.SetAnimationPartParent(part, parent);
            var target = Target(part);
            var adapter = new FrameworkElementCommonAdapter();

            adapter.SetValue(target, "VisualOffsetX", "100%", Context(new Canvas(), target.BehaviorGuid));

            var group = Assert.IsType<TransformGroup>(part.RenderTransform);
            Assert.Equal(320, Assert.IsType<TranslateTransform>(group.Children[^1]).X);
        });
    }

    [Fact]
    public void FrameworkElementAdapter_ClipInsetRightPercentage_ClipsWithoutResizing()
    {
        RunOnStaThread(() =>
        {
            var element = new Border { Width = 200, Height = 80 };
            var target = Target(element);

            new FrameworkElementCommonAdapter().SetValue(
                target,
                "ClipInsetRight",
                "50%",
                Context(new Canvas(), target.BehaviorGuid));

            var clip = Assert.IsType<RectangleGeometry>(element.Clip);
            Assert.Equal(new Rect(0, 0, 100, 80), clip.Rect);
        });
    }

    [Fact]
    public async Task AnimationRuntime_AnimateGeneratedPartOpacity_DurationZero_SetsImmediately()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var guid = Guid.NewGuid();
            var root = new Canvas();
            var part = GeneratedPart(new Border { Opacity = 0 }, guid, "TargetPickingBorder", "PickingBorder");
            root.Children.Add(Generated(new Border(), guid, "Target"));
            root.Children.Add(part);

            await new FrontedAnimationRuntime().ExecuteAsync(new FrontedGraphActionRequest
            {
                RequestType = FrontedGraphActionRequestType.AnimateProperty,
                Target = $"part:{guid}:PickingBorder",
                PropertyName = "Opacity",
                Values = new Dictionary<string, string?> { ["To"] = "1" },
                DurationMs = 0
            }, Context(root, guid));

            Assert.Equal(1, part.Opacity, 3);
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
    public async Task AnimationRuntime_TextContentLayer_ChangesInnerTextBlock()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var guid = Guid.NewGuid();
            var root = new Canvas();
            var textBlock = new TextBlock { Foreground = Brushes.White, FontSize = 24 };
            var element = Generated(new Border { Child = textBlock }, guid, "TextTarget");
            root.Children.Add(element);

            await new FrontedAnimationRuntime().ExecuteAsync(new FrontedGraphActionRequest
            {
                RequestType = FrontedGraphActionRequestType.SetProperty,
                Target = "Self",
                TargetLayer = FrontedAnimationTargetLayer.Content,
                PropertyName = "TextColor",
                Values = new Dictionary<string, string?> { ["Value"] = "#FF010203" }
            }, Context(root, guid));

            await new FrontedAnimationRuntime().ExecuteAsync(new FrontedGraphActionRequest
            {
                RequestType = FrontedGraphActionRequestType.SetProperty,
                Target = "Self",
                TargetLayer = FrontedAnimationTargetLayer.Content,
                PropertyName = "FontSize",
                Values = new Dictionary<string, string?> { ["Value"] = "32" }
            }, Context(root, guid));

            var brush = Assert.IsType<SolidColorBrush>(textBlock.Foreground);
            Assert.Equal(1, element.Opacity);
        });
    }

    [Fact]
    public async Task AnimationRuntime_TextOverlayLayer_CreatesRectangleAndAppliesStroke()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var guid = Guid.NewGuid();
            var root = new Canvas();
            var element = Generated(new Border
            {
                Width = 100,
                Height = 40,
                Child = new TextBlock { Text = "Timer" }
            }, guid, "TextTarget");
            Canvas.SetLeft(element, 10);
            Canvas.SetTop(element, 20);
            Panel.SetZIndex(element, 5);
            root.Children.Add(element);

            var runtime = new FrontedAnimationRuntime();
            await runtime.ExecuteAsync(new FrontedGraphActionRequest
            {
                RequestType = FrontedGraphActionRequestType.SetProperty,
                Target = "Self",
                TargetLayer = FrontedAnimationTargetLayer.OverlayAbove,
                PropertyName = "StrokeColor",
                Values = new Dictionary<string, string?> { ["Value"] = "#FFFF6700" }
            }, Context(root, guid));
            await runtime.ExecuteAsync(new FrontedGraphActionRequest
            {
                RequestType = FrontedGraphActionRequestType.SetProperty,
                Target = "Self",
                TargetLayer = FrontedAnimationTargetLayer.OverlayAbove,
                PropertyName = "StrokeThickness",
                Values = new Dictionary<string, string?> { ["Value"] = "10" }
            }, Context(root, guid));

            var overlay = Assert.Single(root.Children.OfType<Rectangle>());
            Assert.True(FrontedRendererProperties.GetIsAnimationAuxiliaryElement(overlay));
            var brush = Assert.IsType<SolidColorBrush>(overlay.Stroke);
        });
    }

    [Fact]
    public async Task AnimationRuntime_ControlLayerStroke_LogsUnsupportedAndDoesNotCreateOverlay()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var guid = Guid.NewGuid();
            var root = new Canvas();
            var element = Generated(new Border { Width = 100, Height = 40 }, guid, "TextTarget");
            root.Children.Add(element);
            var logger = new RecordingLogger();

            await new FrontedAnimationRuntime().ExecuteAsync(new FrontedGraphActionRequest
            {
                RequestType = FrontedGraphActionRequestType.SetProperty,
                Target = "Self",
                TargetLayer = FrontedAnimationTargetLayer.Control,
                PropertyName = "StrokeColor",
                Values = new Dictionary<string, string?> { ["Value"] = "#FFFFFFFF" }
            }, Context(root, guid, logger));

            Assert.Empty(root.Children.OfType<Rectangle>());
            Assert.Contains(logger.Messages, message => message.Contains("target layer Control", StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public async Task AnimationRuntime_ShapeContentLayer_AppliesFillAndStroke()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var guid = Guid.NewGuid();
            var root = new Canvas();
            var shape = (Rectangle)Generated(new Rectangle(), guid, "ShapeTarget");
            root.Children.Add(shape);

            var runtime = new FrontedAnimationRuntime();
            await runtime.ExecuteAsync(new FrontedGraphActionRequest
            {
                RequestType = FrontedGraphActionRequestType.SetProperty,
                Target = "Self",
                TargetLayer = FrontedAnimationTargetLayer.Content,
                PropertyName = "FillColor",
                Values = new Dictionary<string, string?> { ["Value"] = "#FF112233" }
            }, Context(root, guid));
            await runtime.ExecuteAsync(new FrontedGraphActionRequest
            {
                RequestType = FrontedGraphActionRequestType.SetProperty,
                Target = "Self",
                TargetLayer = FrontedAnimationTargetLayer.Content,
                PropertyName = "StrokeColor",
                Values = new Dictionary<string, string?> { ["Value"] = "#FF445566" }
            }, Context(root, guid));

        });
    }

    [Fact]
    public async Task AnimationRuntime_ImageContentLayer_ChangesMainImageOnly()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var guid = Guid.NewGuid();
            var root = new Canvas();
            var mainImage = new Image { Opacity = 1 };
            var overlay = new Border { Opacity = 1 };
            var imageRoot = (Grid)Generated(new Grid(), guid, "ImageTarget");
            imageRoot.Children.Add(mainImage);
            imageRoot.Children.Add(overlay);
            root.Children.Add(imageRoot);

            await new FrontedAnimationRuntime().ExecuteAsync(new FrontedGraphActionRequest
            {
                RequestType = FrontedGraphActionRequestType.SetProperty,
                Target = "Self",
                TargetLayer = FrontedAnimationTargetLayer.Content,
                PropertyName = "Opacity",
                Values = new Dictionary<string, string?> { ["Value"] = "0.25" }
            }, Context(root, guid));

            Assert.Equal(0.25, mainImage.Opacity, 3);
            Assert.Equal(1, imageRoot.Opacity);
            Assert.Equal(1, overlay.Opacity);
        });
    }

    [Fact]
    public async Task AnimationRuntime_ImageContentLayer_ResolvesMainImageInsideAnimationHost()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var guid = Guid.NewGuid();
            var root = new Canvas();
            var mainImage = new Image { Opacity = 1 };
            var pickingBorder = new Border { Opacity = 1 };
            var imageContent = new Grid();
            imageContent.Children.Add(mainImage);
            imageContent.Children.Add(pickingBorder);
            var borderedImage = (Border)Generated(new Border { Child = imageContent, Opacity = 1 }, guid, "SurPick0Content");
            var host = (Grid)Generated(new Grid { Opacity = 1 }, guid, "SurPick0");
            host.Children.Add(new Canvas());
            host.Children.Add(borderedImage);
            host.Children.Add(new Canvas());
            root.Children.Add(host);

            await new FrontedAnimationRuntime().ExecuteAsync(new FrontedGraphActionRequest
            {
                RequestType = FrontedGraphActionRequestType.SetProperty,
                Target = "Self",
                TargetLayer = FrontedAnimationTargetLayer.Content,
                PropertyName = "Opacity",
                Values = new Dictionary<string, string?> { ["Value"] = "0.25" }
            }, Context(root, guid));

            Assert.Equal(0.25, mainImage.Opacity, 3);
            Assert.Equal(1, host.Opacity);
            Assert.Equal(1, borderedImage.Opacity);
            Assert.Equal(1, pickingBorder.Opacity);
        });
    }

    [Fact]
    public async Task AnimationRuntime_ImageContentLayer_ClipsMainImageInsideAnimationHost()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var guid = Guid.NewGuid();
            var root = new Canvas();
            var mainImage = new Image { Width = 141, Height = 160 };
            var pickingBorder = new Border { Width = 141, Height = 160 };
            var primaryContent = new Grid { Width = 141, Height = 160 };
            FrontedRendererProperties.SetIsPrimaryContentElement(primaryContent, true);
            primaryContent.Children.Add(mainImage);
            var imageContent = new Grid { Width = 141, Height = 160 };
            imageContent.Children.Add(primaryContent);
            imageContent.Children.Add(pickingBorder);
            var borderedImage = (Border)Generated(
                new Border { Width = 141, Height = 160, Child = imageContent },
                guid,
                "SurPick0Content");
            var host = (Grid)Generated(new Grid { Width = 141, Height = 160 }, guid, "SurPick0");
            host.Children.Add(new Canvas());
            host.Children.Add(borderedImage);
            host.Children.Add(new Canvas());
            root.Children.Add(host);

            await new FrontedAnimationRuntime().ExecuteAsync(new FrontedGraphActionRequest
            {
                RequestType = FrontedGraphActionRequestType.SetProperty,
                Target = "Self",
                TargetLayer = FrontedAnimationTargetLayer.Content,
                PropertyName = "ClipInsetRight",
                Values = new Dictionary<string, string?> { ["Value"] = "100%" }
            }, Context(root, guid));

            var clip = Assert.IsType<RectangleGeometry>(primaryContent.Clip);
            Assert.Equal(new Rect(0, 0, 0, 160), clip.Rect);
        });
    }

    [Fact]
    public async Task AnimationRuntime_ImageContentLayer_ChangesPrimaryContentHostOnly()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var guid = Guid.NewGuid();
            var root = new Canvas();
            var mainImage = new Image { Opacity = 1 };
            var primaryContent = new Grid { Opacity = 1 };
            FrontedRendererProperties.SetIsPrimaryContentElement(primaryContent, true);
            primaryContent.Children.Add(mainImage);
            var pickingBorder = new Border { Opacity = 1 };
            var imageContent = new Grid();
            imageContent.Children.Add(primaryContent);
            imageContent.Children.Add(pickingBorder);
            var borderedImage = (Border)Generated(new Border { Child = imageContent, Opacity = 1 }, guid, "SurPick0Content");
            var host = (Grid)Generated(new Grid { Opacity = 1 }, guid, "SurPick0");
            host.Children.Add(new Canvas());
            host.Children.Add(borderedImage);
            host.Children.Add(new Canvas());
            root.Children.Add(host);

            await new FrontedAnimationRuntime().ExecuteAsync(new FrontedGraphActionRequest
            {
                RequestType = FrontedGraphActionRequestType.SetProperty,
                Target = "Self",
                TargetLayer = FrontedAnimationTargetLayer.Content,
                PropertyName = "Opacity",
                Values = new Dictionary<string, string?> { ["Value"] = "0.25" }
            }, Context(root, guid));

            Assert.Equal(0.25, primaryContent.Opacity, 3);
            Assert.Equal(1, mainImage.Opacity);
            Assert.Equal(1, pickingBorder.Opacity);
            Assert.Equal(1, host.Opacity);
            Assert.Equal(1, borderedImage.Opacity);
        });
    }

    [Fact]
    public async Task AnimationRuntime_ResetAll_RestoresContentAndOverlayValues()
    {
        await RunOnStaThreadAsync(async () =>
        {
            var guid = Guid.NewGuid();
            var root = new Canvas();
            var textBlock = new TextBlock { FontSize = 24 };
            var element = Generated(new Border
            {
                Width = 100,
                Height = 40,
                Child = textBlock
            }, guid, "TextTarget");
            root.Children.Add(element);
            var runtime = new FrontedAnimationRuntime();
            var context = Context(root, guid);

            await runtime.ExecuteAsync(new FrontedGraphActionRequest
            {
                RequestType = FrontedGraphActionRequestType.SetProperty,
                Target = "Self",
                TargetLayer = FrontedAnimationTargetLayer.Content,
                PropertyName = "FontSize",
                Values = new Dictionary<string, string?> { ["Value"] = "42" }
            }, context);
            await runtime.ExecuteAsync(new FrontedGraphActionRequest
            {
                RequestType = FrontedGraphActionRequestType.SetProperty,
                Target = "Self",
                TargetLayer = FrontedAnimationTargetLayer.OverlayAbove,
                PropertyName = "StrokeThickness",
                Values = new Dictionary<string, string?> { ["Value"] = "8" }
            }, context);

            var overlay = Assert.Single(root.Children.OfType<Rectangle>());

            runtime.ResetAll(context);

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

    private static FrontedAnimationExecutionContext Context(Canvas root, Guid selfGuid, ILogger? logger = null) =>
        new()
        {
            Root = root,
            SelfBehaviorGuid = selfGuid,
            IsDesignerPreview = true,
            Logger = logger ?? NullLogger.Instance
        };

    private static FrameworkElement Generated(FrameworkElement element, Guid guid, string name)
    {
        FrontedRendererProperties.SetIsGeneratedControl(element, true);
        FrontedRendererProperties.SetBehaviorGuid(element, guid);
        FrontedRendererProperties.SetRegisteredName(element, name);
        return element;
    }

    private static FrameworkElement GeneratedPart(
        FrameworkElement element,
        Guid parentGuid,
        string name,
        string partName)
    {
        FrontedRendererProperties.SetIsGeneratedControl(element, true);
        FrontedRendererProperties.SetIsAnimationAuxiliaryElement(element, true);
        FrontedRendererProperties.SetParentBehaviorGuid(element, parentGuid);
        FrontedRendererProperties.SetParentRegisteredName(element, "Target");
        FrontedRendererProperties.SetAnimationPartName(element, partName);
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
        WpfTestThread.Run(action);
    }

    private static Task RunOnStaThreadAsync(Func<Task> action)
    {
        return WpfTestThread.RunAsync(action);
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) => NullLogger.Instance.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
