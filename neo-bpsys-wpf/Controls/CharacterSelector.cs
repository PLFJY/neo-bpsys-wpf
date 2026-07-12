using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Tutorial;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace neo_bpsys_wpf.Controls;

/// <summary>
/// 角色选择器控件，提供下拉列表选择角色并支持搜索功能。
/// </summary>
public class CharacterSelector : Control
{
    /// <summary>
    /// 获取或设置一个值，指示是否启用简单模式。
    /// </summary>
    public bool IsSimpleModeEnabled
    {
        get => (bool)GetValue(IsSimpleModeEnabledProperty);
        set => SetValue(IsSimpleModeEnabledProperty, value);
    }

    /// <summary>
    /// <see cref="IsSimpleModeEnabled"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IsSimpleModeEnabledProperty =
        DependencyProperty.Register(nameof(IsSimpleModeEnabled), typeof(bool), typeof(CharacterSelector), new PropertyMetadata(false));

    /// <summary>
    /// 获取或设置在确认选择时执行的命令。
    /// </summary>
    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>
    /// <see cref="Command"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(CharacterSelector), new PropertyMetadata(null));

    /// <summary>
    /// 获取或设置角色图片源。
    /// </summary>
    public ImageSource ImageSource
    {
        get => (ImageSource)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    /// <summary>
    /// <see cref="ImageSource"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty ImageSourceProperty =
        DependencyProperty.Register(nameof(ImageSource), typeof(ImageSource), typeof(CharacterSelector), new PropertyMetadata(null));

    /// <summary>
    /// 获取或设置显示的文本。
    /// </summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// <see cref="Text"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(CharacterSelector), new PropertyMetadata(string.Empty));


    /// <summary>
    /// 获取或设置下拉列表的项源。
    /// </summary>
    public IEnumerable ItemsSource
    {
        get => (IEnumerable)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>
    /// <see cref="ItemsSource"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(CharacterSelector), new PropertyMetadata(null));

    /// <summary>
    /// 获取或设置一个值，指示下拉列表是否已打开。
    /// </summary>
    public bool IsDropDownOpen
    {
        get => (bool)GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    /// <summary>
    /// <see cref="IsDropDownOpen"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IsDropDownOpenProperty =
        DependencyProperty.Register(nameof(IsDropDownOpen), typeof(bool), typeof(CharacterSelector), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>
    /// 获取或设置当前选中项的索引。
    /// </summary>
    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>
    /// <see cref="SelectedIndex"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty SelectedIndexProperty =
        DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(CharacterSelector), new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedIndexChanged));

    /// <summary>
    /// 当 <see cref="SelectedIndex"/> 变为有效值（&gt;= 0）时清除搜索错误状态，
    /// 覆盖下拉点选、外部绑定同步等成功选中场景。
    /// </summary>
    private static void OnSelectedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CharacterSelector selector && (int)e.NewValue >= 0)
        {
            selector.IsSearchError = false;
        }
    }

    /// <summary>
    /// 获取或设置当前选中的项。
    /// </summary>
    public object SelectedItem
    {
        get => (object)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>
    /// <see cref="SelectedItem"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(CharacterSelector), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>
    /// 获取或设置当前选中的值。
    /// </summary>
    public object SelectedValue
    {
        get => (object)GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    /// <summary>
    /// <see cref="SelectedValue"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty SelectedValueProperty =
        DependencyProperty.Register(nameof(SelectedValue), typeof(object), typeof(CharacterSelector), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>
    /// 获取或设置一个值，指示控件是否应高亮显示。
    /// </summary>
    public bool IsHighlighted
    {
        get => (bool)GetValue(IsHighlightedProperty);
        set => SetValue(IsHighlightedProperty, value);
    }

    /// <summary>
    /// <see cref="IsHighlighted"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IsHighlightedProperty =
        DependencyProperty.Register(nameof(IsHighlighted), typeof(bool), typeof(CharacterSelector), new PropertyMetadata(false));

    /// <summary>
    /// 在下拉列表中应被禁用（灰显不可选）的角色名称集合
    /// </summary>
    public ISet<string> DisabledKeys
    {
        get => (ISet<string>)GetValue(DisabledKeysProperty);
        set => SetValue(DisabledKeysProperty, value);
    }

    /// <summary>
    /// <see cref="DisabledKeys"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty DisabledKeysProperty =
        DependencyProperty.Register(nameof(DisabledKeys), typeof(ISet<string>), typeof(CharacterSelector),
            new PropertyMetadata(null));

    /// <summary>
    /// 获取或设置一个值，指示当前搜索是否匹配失败（无匹配角色或匹配角色已被禁选）。
    /// 为 true 时控件会显示黄色错误边框与提示文字；成功选中角色后自动清除。
    /// </summary>
    public bool IsSearchError
    {
        get => (bool)GetValue(IsSearchErrorProperty);
        set => SetValue(IsSearchErrorProperty, value);
    }

    /// <summary>
    /// <see cref="IsSearchError"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IsSearchErrorProperty =
        DependencyProperty.Register(nameof(IsSearchError), typeof(bool), typeof(CharacterSelector), new PropertyMetadata(false));

    /// <summary>
    /// 初始化角色选择器控件。
    /// </summary>
    public CharacterSelector()
    {
        // 注册TextBox的OnTextBoxTextChanged事件处理程序，借助事件冒泡实现搜索
        AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnTextBoxTextChanged), true);
        AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnTemplateButtonClick), true);
    }

    private void OnTextBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        // 判断事件源是否为ComboBox中的TextBox
        if (e.OriginalSource is TextBox textBox && (textBox.Parent is ComboBoxItem || textBox.TemplatedParent is ComboBox))
        {
            //press space to search
            if (Text.Length > 0 && Text.Last() == ' ')
            {
                var currentText = Text[..^1];
                var foundIndex = FindIndex(currentText);

                SelectedIndex = foundIndex;
                if (foundIndex == -1)
                {
                    IsSearchError = true;
                    return;
                }
                IsSearchError = false;
                if (ItemsSource is SortedDictionary<string, Character> itemSource)
                    Text = itemSource.ElementAt(foundIndex).Key;
                TutorialSignalPublisher.Publish(TutorialSignalIds.CharacterSelectorSearchCommitted, CreateSignalPayload());
            }
        }
    }

    private void OnTemplateButtonClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is Button)
        {
            TutorialSignalPublisher.Publish(TutorialSignalIds.CharacterSelectorSelectionConfirmed, CreateSignalPayload());
        }
    }


    /// <summary>
    /// 查找待匹配选项的索引。
    /// 跳过 <see cref="DisabledKeys"/> 中已禁用的角色，确保搜索（简称/全称）不能绕过排斥机制。
    /// </summary>
    /// <param name="inputText"></param>
    /// <returns></returns>
    public int FindIndex(string inputText)
    {
        string inputLower = inputText.ToLowerInvariant();
        if (ItemsSource is not SortedDictionary<string, Character> itemSource)
            return -1;

        var disabled = DisabledKeys;
        var index = 0;

        foreach (var item in itemSource)
        {
            var fullSpell = item.Value.FullSpell.ToLowerInvariant();
            var abbrev = item.Value.Abbrev.ToLowerInvariant();
            var fullName = item.Value.Name;
            // Check whether the full prefix matches or the short prefix matches
            if (fullSpell.StartsWith(inputLower) || abbrev.StartsWith(inputLower) || fullName.StartsWith(inputText))
            {
                // 跳过已禁用（被排斥）的角色，继续查找下一个未禁用的匹配项
                if (disabled is null || !disabled.Contains(item.Key))
                {
                    return index;
                }
            }
            index++;
        }

        Text = string.Empty;
        return -1;
    }
    private static void MoveFocus()
    {
        if (Keyboard.FocusedElement is UIElement focusedElement)
        {
            var request = new TraversalRequest(FocusNavigationDirection.Next);
            focusedElement.MoveFocus(request);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is not UIElement currentFocusedElement || currentFocusedElement.GetType() != typeof(TextBox))
            return;

        IsDropDownOpen = true;

        //press enter or tab to confirm
        if (e.Key == Key.Tab || e.Key == Key.Enter)
        {
            e.Handled = true;
            if (Command != null && Command.CanExecute(null))
            {
                Command.Execute(null);
                TutorialSignalPublisher.Publish(TutorialSignalIds.CharacterSelectorSelectionConfirmed, CreateSignalPayload());
            }

            IsDropDownOpen = false;
            //change Focus on Tab click
            MoveFocus();
        }
    }

    private object CreateSignalPayload() => new
    {
        Name,
        Text,
        SelectedIndex,
        SelectedValue,
        SelectedItem
    };
}
