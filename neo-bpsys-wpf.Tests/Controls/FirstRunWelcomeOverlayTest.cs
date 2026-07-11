#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.ProductTour.Controls;
using neo_bpsys_wpf.Tests.Infrastructure;
using Xunit;

namespace neo_bpsys_wpf.Tests.Controls;

/// <summary>
/// Tests first-run welcome overlay styling boundaries.
/// </summary>
public sealed class FirstRunWelcomeOverlayTest
{
    [Fact]
    public void WelcomeCardDoesNotLocallyOverrideStyleAppearance()
    {
        WpfTestThread.Run(() =>
        {
            var overlay = new FirstRunWelcomeOverlay();

            var card = FindByName<Border>(overlay, "WelcomeCard");

            Assert.NotNull(card);
            Assert.Equal(DependencyProperty.UnsetValue, card.ReadLocalValue(Border.BackgroundProperty));
            Assert.Equal(DependencyProperty.UnsetValue, card.ReadLocalValue(Border.BorderThicknessProperty));
            Assert.Equal(DependencyProperty.UnsetValue, card.ReadLocalValue(UIElement.EffectProperty));
        });
    }

    [Fact]
    public void WelcomeUsesLanguageOptionsInsteadOfCultureStrings()
    {
        WpfTestThread.Run(() =>
        {
            var overlay = new FirstRunWelcomeOverlay(
                new DefaultTutorialTextProvider(),
                new ProductTourOptions(),
                CreateLanguageOptions());

            var comboBox = FindChild<ComboBox>(overlay);
            Assert.NotNull(comboBox);
            var itemText = string.Join(
                "\n",
                comboBox.Items
                    .OfType<ComboBoxItem>()
                    .Select(item => item.Content?.ToString() ?? string.Empty));
            var text = FlattenText(overlay) + "\n" + itemText;

            Assert.Contains("跟随系统", text, StringComparison.Ordinal);
            Assert.Contains("简体中文", text, StringComparison.Ordinal);
            Assert.Contains("English", text, StringComparison.Ordinal);
            Assert.Contains("日本語", text, StringComparison.Ordinal);
            Assert.DoesNotContain("zh-CN", text, StringComparison.Ordinal);
            Assert.DoesNotContain("en-US", text, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task WelcomeStartPassesLanguageOptionId()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var options = new ProductTourOptions
            {
                WelcomeFadeOutDuration = TimeSpan.FromMilliseconds(1)
            };
            var textProvider = new DefaultTutorialTextProvider();
            var overlay = new FirstRunWelcomeOverlay(textProvider, options, CreateLanguageOptions());
            var window = new Window
            {
                Width = 800,
                Height = 600,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                Left = -10000,
                Top = -10000,
                Content = overlay
            };
            var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                window.Show();
                window.UpdateLayout();
                overlay.StartRequested += (_, id) => completion.TrySetResult(id);

                var comboBox = FindChild<ComboBox>(overlay);
                Assert.NotNull(comboBox);
                var english = comboBox.Items
                    .OfType<ComboBoxItem>()
                    .FirstOrDefault(item => Equals(item.Tag, "en_US"));
                Assert.NotNull(english);
                comboBox.SelectedItem = english;
                var start = FindButtonByContent(overlay, textProvider.StartTour);
                Assert.NotNull(start);
                start.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

                Assert.Equal("en_US", await completion.Task.WaitAsync(TimeSpan.FromSeconds(2)));
            }
            finally
            {
                window.Close();
            }
        });
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

    private static T? FindChild<T>(DependencyObject root)
        where T : DependencyObject
    {
        if (root is T element)
        {
            return element;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            var result = FindChild<T>(VisualTreeHelper.GetChild(root, i));
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static string FlattenText(DependencyObject root)
    {
        var text = root switch
        {
            TextBlock textBlock => textBlock.Text,
            ContentControl { Content: string content } => content,
            _ => string.Empty
        };
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            text += "\n" + FlattenText(VisualTreeHelper.GetChild(root, i));
        }

        return text;
    }

    private static IReadOnlyList<TutorialLanguageOption> CreateLanguageOptions() =>
    [
        new TutorialLanguageOption { Id = "System", DisplayName = "跟随系统", NativeName = "Follow system", IsSystemDefault = true },
        new TutorialLanguageOption { Id = "zh_Hans", DisplayName = "简体中文", NativeName = "简体中文" },
        new TutorialLanguageOption { Id = "en_US", DisplayName = "English", NativeName = "English", IsSelected = true },
        new TutorialLanguageOption { Id = "ja_JP", DisplayName = "日本語", NativeName = "日本語" }
    ];
}
