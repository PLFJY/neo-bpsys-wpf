#nullable enable

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using neo_bpsys_wpf.Controls.Modern.Scrolling;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Tests.Infrastructure;
using Xunit;

namespace neo_bpsys_wpf.Tests.Controls;

public class GuidanceScrollHelperTest
{
    [Fact]
    public void ActionOnlyTargetMatchesActionOnlyMessage()
    {
        RunSta(() =>
        {
            var target = CreateTarget(GameAction.PickHun);
            var message = new HighlightMessage(GameAction.PickHun, null);

            Assert.True(GuidanceScrollHelper.IsTargetMatch(target, message));
        });
    }

    [Fact]
    public void IndexedTargetMatchesWhenMessageContainsTargetIndex()
    {
        RunSta(() =>
        {
            var target = CreateTarget(GameAction.PickSur, 2);
            var message = new HighlightMessage(GameAction.PickSur, new List<int> { 0, 2 });

            Assert.True(GuidanceScrollHelper.IsTargetMatch(target, message));
        });
    }

    [Fact]
    public void IndexedTargetDoesNotMatchWhenMessageDoesNotContainTargetIndex()
    {
        RunSta(() =>
        {
            var target = CreateTarget(GameAction.PickSur, 2);
            var message = new HighlightMessage(GameAction.PickSur, new List<int> { 0, 1 });

            Assert.False(GuidanceScrollHelper.IsTargetMatch(target, message));
        });
    }

    [Fact]
    public void MultipleMessageIndexesPreferEarliestRequestedIndex()
    {
        RunSta(() =>
        {
            var scope = new StackPanel();
            var laterTarget = CreateTarget(GameAction.BanSur, 1);
            var earlierTarget = CreateTarget(GameAction.BanSur, 3);
            scope.Children.Add(laterTarget);
            scope.Children.Add(earlierTarget);

            var message = new HighlightMessage(GameAction.BanSur, new List<int> { 3, 1 });

            Assert.Same(earlierTarget, GuidanceScrollHelper.FindBestTarget(scope, message));
        });
    }

    [Fact]
    public void NullGameActionDoesNotMatch()
    {
        RunSta(() =>
        {
            var target = CreateTarget(GameAction.BanMap);
            var message = new HighlightMessage(null, null);

            Assert.False(GuidanceScrollHelper.IsTargetMatch(target, message));
            Assert.Null(GuidanceScrollHelper.FindBestTarget(new StackPanel { Children = { target } }, message));
        });
    }

    [Fact]
    public void MissingTargetReturnsNull()
    {
        RunSta(() =>
        {
            var scope = new StackPanel
            {
                Children = { CreateTarget(GameAction.PickMap) }
            };
            var message = new HighlightMessage(GameAction.BanMap, null);

            Assert.Null(GuidanceScrollHelper.FindBestTarget(scope, message));
        });
    }

    [Fact]
    public void HiddenTargetIsNotPreferredOverVisibleTarget()
    {
        RunSta(() =>
        {
            var scope = new StackPanel();
            var hiddenTarget = CreateTarget(GameAction.PickHun);
            hiddenTarget.Visibility = Visibility.Collapsed;
            var visibleTarget = CreateTarget(GameAction.PickHun);
            scope.Children.Add(hiddenTarget);
            scope.Children.Add(visibleTarget);

            var message = new HighlightMessage(GameAction.PickHun, null);

            Assert.Same(visibleTarget, GuidanceScrollHelper.FindBestTarget(scope, message));
        });
    }

    private static FrameworkElement CreateTarget(GameAction action, int? index = null)
    {
        var target = new Border();
        GuidanceScrollTarget.SetAction(target, action);
        GuidanceScrollTarget.SetIndex(target, index);
        return target;
    }

    private static void RunSta(Action action)
    {
        WpfTestThread.Run(action);
    }
}
