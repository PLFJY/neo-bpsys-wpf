using neo_bpsys_wpf.Models;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace neo_bpsys_wpf.CustomControls
{
    public class CharacterSelector : Control
    {
        public bool IsSimpleModeEnabled
        {
            get => (bool)GetValue(IsSimpleModeEnabledProperty);
            set => SetValue(IsSimpleModeEnabledProperty, value);
        }

        // Using a DependencyProperty as the backing store for IsSimpleModeEnabled.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsSimpleModeEnabledProperty =
            DependencyProperty.Register(nameof(IsSimpleModeEnabled), typeof(bool), typeof(CharacterSelector), new PropertyMetadata(false));

        public ICommand Command
        {
            get => (ICommand)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        // Using a DependencyProperty as the backing store for Command.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(CharacterSelector), new PropertyMetadata(null));

        public ImageSource ImageSource
        {
            get => (ImageSource)GetValue(ImageSourceProperty);
            set => SetValue(ImageSourceProperty, value);
        }

        // Using a DependencyProperty as the backing store for ImageSource.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ImageSourceProperty =
            DependencyProperty.Register(nameof(ImageSource), typeof(ImageSource), typeof(CharacterSelector), new PropertyMetadata(null));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        // Using a DependencyProperty as the backing store for Text.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(CharacterSelector), new PropertyMetadata(string.Empty));


        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        // Using a DependencyProperty as the backing store for ItemsSource.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(CharacterSelector), new PropertyMetadata(null));

        public bool IsDropDownOpen
        {
            get => (bool)GetValue(IsDropDownOpenProperty);
            set => SetValue(IsDropDownOpenProperty, value);
        }

        // Using a DependencyProperty as the backing store for IsDropDownOpen.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsDropDownOpenProperty =
            DependencyProperty.Register(nameof(IsDropDownOpen), typeof(bool), typeof(CharacterSelector), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public int SelectedIndex
        {
            get => (int)GetValue(SelectedIndexProperty);
            set => SetValue(SelectedIndexProperty, value);
        }

        // Using a DependencyProperty as the backing store for SelectedIndex.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SelectedIndexProperty =
            DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(CharacterSelector), new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public object SelectedItem
        {
            get => (object)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        // Using a DependencyProperty as the backing store for SelectedItem.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(CharacterSelector), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public object SelectedValue
        {
            get => (object)GetValue(SelectedValueProperty);
            set => SetValue(SelectedValueProperty, value);
        }

        // Using a DependencyProperty as the backing store for SelectedValue.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SelectedValueProperty =
            DependencyProperty.Register(nameof(SelectedValue), typeof(object), typeof(CharacterSelector), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public bool IsHighlighted
        {
            get => (bool)GetValue(IsHighlightedProperty);
            set => SetValue(IsHighlightedProperty, value);
        }

        // Using a DependencyProperty as the backing store for IsHighlighted.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsHighlightedProperty =
            DependencyProperty.Register(nameof(IsHighlighted), typeof(bool), typeof(CharacterSelector), new PropertyMetadata(false));

        public CharacterSelector()
        {
            // 注册TextBox的OnTextBoxTextChanged事件处理程序，借助事件冒泡实现搜索
            AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler(OnTextBoxTextChanged), true);
        }

        private void OnTextBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            // 判断事件源是否为ComboBox中的TextBox
            if (e.OriginalSource is TextBox textBox && (textBox.Parent is ComboBoxItem || textBox.TemplatedParent is ComboBox))
            {
                //press space to search
                if (Text.Contains(' '))
                {
                    var currentText = Text.Substring(0, Text.Length - 1);
                    var findedIndex = FindIndex(currentText);
                    SelectedIndex = findedIndex;
                    if (findedIndex == -1)
                        return;
                    if (ItemsSource is Dictionary<string, Character> itemSource)
                        Text = itemSource.ElementAt(findedIndex).Key;
                }
            }
        }


        /// <summary>
        /// Find the index of ths option waiting to be found
        /// </summary>
        /// <param name="inputText"></param>
        /// <returns></returns>
        public int FindIndex(string inputText)
        {
            string inputLower = inputText.ToLowerInvariant();
            if (ItemsSource is not Dictionary<string, Character> itemSource)
                return -1;

            var index = 0;

            foreach (var item in itemSource)
            {
                var fullSpell = item.Value.FullSpell.ToLowerInvariant();
                var abbrev = item.Value.Abbrev.ToLowerInvariant();
                var fullName = item.Value.Name;
                // Check whether the full prefix matches or the short prefix matches
                if (fullSpell.StartsWith(inputLower) || abbrev.StartsWith(inputLower) || fullName.StartsWith(inputText))
                {
                    return index;
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
                    Command.Execute(null);

                IsDropDownOpen = false;
                //change Focus on Tab click
                MoveFocus();
            }
        }
    }
}
