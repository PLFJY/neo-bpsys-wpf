#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.ProductTour.Controls;
using neo_bpsys_wpf.Tests.Infrastructure;
using Xunit;

namespace neo_bpsys_wpf.Tests.Controls;

/// <summary>
/// Tests Product Tour overlay hit-test behavior.
/// </summary>
public sealed class ProductTourOverlayHitTest
{
    [Fact]
    public void ProductTourOptionsHaveExpectedDefaults()
    {
        var options = new ProductTourOptions();

        Assert.Equal(380, options.CardWidth);
        Assert.Equal(280, options.CardMaxHeight);
        Assert.Equal(12, options.CardMargin);
        Assert.Equal(16, options.Gap);
        Assert.Equal(8, options.SpotlightPadding);
        Assert.Equal(8, options.SpotlightCornerRadius);
        Assert.Equal(TimeSpan.FromMilliseconds(240), options.OverlayFadeInDuration);
        Assert.Equal(TimeSpan.FromMilliseconds(220), options.OverlayFadeOutDuration);
        Assert.Equal(TimeSpan.FromMilliseconds(240), options.WelcomeFadeInDuration);
        Assert.Equal(TimeSpan.FromMilliseconds(280), options.WelcomeFadeOutDuration);
        Assert.Equal(TimeSpan.FromMilliseconds(300), options.WelcomeCardEnterDuration);
        Assert.Equal(16, options.WelcomeCardInitialTranslateY);
        Assert.Equal(TimeSpan.FromMilliseconds(240), options.DialogueFadeInDuration);
        Assert.Equal(TimeSpan.FromMilliseconds(200), options.DialogueFadeOutDuration);
        Assert.Equal(TimeSpan.FromMilliseconds(280), options.DialogueBoxEnterDuration);
        Assert.Equal(24, options.DialogueInitialTranslateY);
        Assert.Equal(TimeSpan.FromMilliseconds(28), options.TypewriterInterval);
        Assert.True(options.ShowStepProgress);
        Assert.Equal(0.86, options.MaskOpacity);
        Assert.Equal(0.90, options.WelcomeMaskOpacity);
        Assert.Equal(0.82, options.DialogueMaskOpacity);
        Assert.Equal(0.84, options.ProductTourMaskOpacity);
        Assert.Equal(760, options.DialogueBoxMaxWidth);
        Assert.Equal(0.94, options.DialogueBoxMinOpacity);
        Assert.Equal(new Thickness(48), options.DialogueBoxMargin);
        Assert.True(options.ShowAvatar);
        Assert.Equal(220, options.WelcomeAvatarWidth);
        Assert.Equal(260, options.DialogueAvatarWidth);
        Assert.Equal(96, options.ProductTourAvatarWidth);
        Assert.Equal(new Thickness(16), options.AvatarMargin);
    }

    [Fact]
    public async Task AllowTargetOnlyLeavesTargetHitTestReachable()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var host = CreateOwnerWithTarget();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                var overlay = await ShowOverlayAsync(host.Owner, host.Target, ProductTourInteractionMode.AllowTargetOnly, cts.Token);

                var hit = HitTestTargetCenter(host.Owner, host.Target);

                cts.Cancel();
                await overlay.Task;
                Assert.NotNull(hit);
                Assert.True(
                    IsDescendantOf(hit, host.Target),
                    $"Expected target hit, got {DescribeHit(hit, host.Target, overlay.Overlay)}.");
            }
            finally
            {
                host.Window.Close();
            }
        });
    }

    [Fact]
    public async Task OverlayUsesProvidedOptionsForCardLayout()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var host = CreateOwnerWithTarget(new Thickness(700, 40, 0, 0), 40, 40);
            var options = new ProductTourOptions
            {
                CardWidth = 300,
                CardMaxHeight = 180,
                CardMargin = 37,
                Gap = 40,
                OverlayFadeInDuration = TimeSpan.FromMilliseconds(1),
                OverlayFadeOutDuration = TimeSpan.FromMilliseconds(1)
            };
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                var overlay = await ShowOverlayAsync(
                    host.Owner,
                    host.Target,
                    ProductTourInteractionMode.BlockAll,
                    cts.Token,
                    options);

                var card = FindByName<Border>(overlay.Overlay, "Card");

                cts.Cancel();
                await overlay.Task;
                Assert.NotNull(card);
                Assert.Equal(options.CardWidth, card.Width);
                Assert.Equal(options.CardMaxHeight, card.MaxHeight);
            }
            finally
            {
                host.Window.Close();
            }
        });
    }

    [Fact]
    public async Task OverlayUsesTextProviderForButtonText()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var host = CreateOwnerWithTarget();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                var overlay = await ShowOverlayAsync(
                    host.Owner,
                    host.Target,
                    ProductTourInteractionMode.BlockAll,
                    cts.Token,
                    new ProductTourOptions
                    {
                        OverlayFadeInDuration = TimeSpan.FromMilliseconds(1),
                        OverlayFadeOutDuration = TimeSpan.FromMilliseconds(1)
                    },
                    new FakeTutorialTextProvider());

                cts.Cancel();
                await overlay.Task;

                Assert.NotNull(FindButtonByContent(overlay.Overlay, "FAKE_PREVIOUS"));
                Assert.NotNull(FindButtonByContent(overlay.Overlay, "FAKE_FINISH"));
                Assert.NotNull(FindButtonByContent(overlay.Overlay, "FAKE_SKIP"));
            }
            finally
            {
                host.Window.Close();
            }
        });
    }

    [Fact]
    public async Task WaitingForSignalKeepsNextButtonVisibleButDisabled()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var host = CreateOwnerWithTarget();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                var overlay = await ShowOverlayAsync(
                    host.Owner,
                    host.Target,
                    ProductTourInteractionMode.BlockAll,
                    cts.Token,
                    textProvider: new FakeTutorialTextProvider(),
                    waitForSignalId: "Signal.Test");
                var nextButton = FindButtonByContent(overlay.Overlay, "FAKE_NEXT");

                Assert.NotNull(nextButton);
                Assert.Equal(Visibility.Visible, nextButton.Visibility);
                Assert.False(nextButton.IsEnabled);

                cts.Cancel();
                await overlay.Task;
            }
            finally
            {
                host.Window.Close();
            }
        });
    }

    [Fact]
    public async Task BlockAllInterceptsTargetHitTest()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var host = CreateOwnerWithTarget();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                var overlay = await ShowOverlayAsync(host.Owner, host.Target, ProductTourInteractionMode.BlockAll, cts.Token);

                var hit = HitTestTargetCenter(host.Owner, host.Target);

                cts.Cancel();
                await overlay.Task;
                Assert.NotNull(hit);
                Assert.False(
                    IsDescendantOf(hit, host.Target),
                    $"Expected overlay mask hit, got target descendant {DescribeHit(hit, host.Target, overlay.Overlay)}.");
                Assert.True(
                    IsDescendantOf(hit, overlay.Overlay),
                    $"Expected overlay mask hit, got {DescribeHit(hit, host.Target, overlay.Overlay)}.");
            }
            finally
            {
                host.Window.Close();
            }
        });
    }

    private static TestHost CreateOwnerWithTarget()
        => CreateOwnerWithTarget(new Thickness(40), 120, 40);

    private static TestHost CreateOwnerWithTarget(Thickness targetMargin, double targetWidth, double targetHeight)
    {
        var owner = new Grid
        {
            Width = 800,
            Height = 600,
            Background = Brushes.White
        };
        var target = new Button
        {
            Name = "TargetButton",
            Content = "Target",
            Width = targetWidth,
            Height = targetHeight,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = targetMargin
        };
        owner.Children.Add(target);

        var window = new Window
        {
            Width = 800,
            Height = 600,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            Left = -10000,
            Top = -10000,
            Content = owner
        };
        window.Show();
        window.UpdateLayout();
        return new TestHost(window, owner, target);
    }

    private static async Task<ShownOverlay> ShowOverlayAsync(
        Grid owner,
        Button target,
        ProductTourInteractionMode interactionMode,
        CancellationToken cancellationToken,
        ProductTourOptions? options = null,
        ITutorialTextProvider? textProvider = null,
        string? waitForSignalId = null)
    {
        var overlay = new ProductTourOverlay(textProvider ?? new DefaultTutorialTextProvider(), options ?? new ProductTourOptions());
        owner.Children.Add(overlay);
        owner.UpdateLayout();
        var runTask = overlay.ShowStepAsync(
            new ProductTourStep
            {
                Title = "Title",
                Description = "Description",
                Placement = ProductTourPlacement.Right,
                InteractionMode = interactionMode,
                WaitForSignalId = waitForSignalId
            },
            target,
            new ProductTourStepContext
            {
                Owner = owner,
                StepIndex = 0,
                StepCount = 1
            },
            cancellationToken);
        await Task.Delay(350, cancellationToken);
        owner.UpdateLayout();
        return new ShownOverlay(overlay, runTask);
    }

    private static T? FindByName<T>(DependencyObject root, string name)
        where T : FrameworkElement
    {
        if (root is T element && element.Name == name)
        {
            return element;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            var result = FindByName<T>(VisualTreeHelper.GetChild(root, i), name);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static Button? FindButtonByContent(DependencyObject root, string content)
    {
        if (root is Button button && Equals(button.Content, content))
        {
            return button;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            var result = FindButtonByContent(VisualTreeHelper.GetChild(root, i), content);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static DependencyObject? HitTestTargetCenter(Grid owner, Button target)
    {
        var center = target.TranslatePoint(new Point(target.ActualWidth / 2, target.ActualHeight / 2), owner);
        return owner.InputHitTest(center) as DependencyObject;
    }

    private static bool IsDescendantOf(DependencyObject current, DependencyObject ancestor)
    {
        if (ReferenceEquals(current, ancestor))
        {
            return true;
        }

        var parent = VisualTreeHelper.GetParent(current);
        while (parent != null)
        {
            if (ReferenceEquals(parent, ancestor))
            {
                return true;
            }

            if (parent is FrameworkElement { TemplatedParent: not null } parentElement &&
                ReferenceEquals(parentElement.TemplatedParent, ancestor))
            {
                return true;
            }

            parent = VisualTreeHelper.GetParent(parent);
        }

        if (current is FrameworkElement { TemplatedParent: not null } currentElement &&
            ReferenceEquals(currentElement.TemplatedParent, ancestor))
        {
            return true;
        }

        return false;
    }

    private static string DescribeHit(DependencyObject hit, Button target, ProductTourOverlay overlay)
    {
        var parts = new System.Collections.Generic.List<string>();
        var current = hit;
        while (current != null)
        {
            var name = current is FrameworkElement element && !string.IsNullOrWhiteSpace(element.Name)
                ? $"#{element.Name}"
                : string.Empty;
            var targetMarker = ReferenceEquals(current, target) ? " target" : string.Empty;
            var overlayMarker = ReferenceEquals(current, overlay) ? " overlay" : string.Empty;
            parts.Add($"{current.GetType().FullName}{name}{targetMarker}{overlayMarker}");
            current = VisualTreeHelper.GetParent(current);
        }

        return string.Join(" <- ", parts);
    }

    private sealed record ShownOverlay(ProductTourOverlay Overlay, Task<ProductTourStepAction> Task);

    private sealed record TestHost(Window Window, Grid Owner, Button Target);

    private sealed class FakeTutorialTextProvider : ITutorialTextProvider
    {
        public string Previous => "FAKE_PREVIOUS";

        public string Next => "FAKE_NEXT";

        public string Finish => "FAKE_FINISH";

        public string Skip => "FAKE_SKIP";

        public string WaitingForAction => "FAKE_WAITING";

        public string Continue => "FAKE_CONTINUE";

        public string ClickToContinue => "FAKE_CLICK";

        public string WelcomeTitle => "FAKE_WELCOME";

        public string WelcomeDescription => "FAKE_DESCRIPTION";

        public string LanguageLabel => "FAKE_LANGUAGE";

        public string StartTour => "FAKE_START";

        public string RestartAvailableHint => "FAKE_RESTART";

        public string SkipConfirmTitle => "FAKE_SKIP_TITLE";

        public string SkipConfirmDescription => "FAKE_SKIP_DESCRIPTION";

        public string SkipConfirmContinue => "FAKE_SKIP_CONTINUE";

        public string SkipConfirmConfirm => "FAKE_SKIP_CONFIRM";

        public string SkipForCurrentSession => "FAKE_SKIP_CURRENT";

        public string SkipPermanently => "FAKE_SKIP_PERMANENT";

        public string SuppressUntilNextStartup => "FAKE_SUPPRESS";

        public string FirstRunSkipConfirmDescription => "FAKE_FIRST_RUN_SKIP";

        public string SequenceSkipConfirmDescription => "FAKE_SEQUENCE_SKIP";
    }
}
