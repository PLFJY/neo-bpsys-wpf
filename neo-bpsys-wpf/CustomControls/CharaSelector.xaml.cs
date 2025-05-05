using neo_bpsys_wpf.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace neo_bpsys_wpf.CustomControls
{
    /// <summary>
    /// CaraSelector.xaml 的交互逻辑
    /// </summary>
    public partial class CharaSelector : UserControl
    {
        public int Index
        {
            get { return (int)GetValue(IndexProperty); }
            set { SetValue(IndexProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Index.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IndexProperty =
            DependencyProperty.Register("Index", typeof(int), typeof(CharaSelector), new PropertyMetadata(0));

        public bool IsSimpleModeEnabled
        {
            get { return (bool)GetValue(IsSimpleModeEnabledProperty); }
            set { SetValue(IsSimpleModeEnabledProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsSimpleModeEnabled.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsSimpleModeEnabledProperty =
            DependencyProperty.Register("IsSimpleModeEnabled", typeof(bool), typeof(CharaSelector), new PropertyMetadata(false));

        public ICommand Command
        {
            get { return (ICommand)GetValue(CommandProperty); }
            set { SetValue(CommandProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Command.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register("Command", typeof(ICommand), typeof(CharaSelector), new PropertyMetadata(null));

        public bool IsDropDownOpen
        {
            get { return (bool)GetValue(IsDropDownOpenProperty); }
            set { SetValue(IsDropDownOpenProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsDropDownOpen.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsDropDownOpenProperty =
            DependencyProperty.Register("IsDropDownOpen", typeof(bool), typeof(CharaSelector), new PropertyMetadata(false));

        public IEnumerable ItemsSource
        {
            get { return (IEnumerable)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ItemsSource.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register("ItemsSource", typeof(IEnumerable), typeof(CharaSelector), new PropertyMetadata(null));

        public int SelectedIndex
        {
            get { return (int)GetValue(SelectedIndexProperty); }
            set { SetValue(SelectedIndexProperty, value); }
        }

        // Using a DependencyProperty as the backing store for SelectedIndex.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SelectedIndexProperty =
            DependencyProperty.Register("SelectedIndex", typeof(int), typeof(CharaSelector), new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public object SelectedItem
        {
            get { return (object)GetValue(SelectedItemProperty); }
            set { SetValue(SelectedItemProperty, value); }
        }

        // Using a DependencyProperty as the backing store for SelectedItem.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register("SelectedItem", typeof(object), typeof(CharaSelector), new PropertyMetadata(null));

        public object SelectedValue
        {
            get { return (object)GetValue(SelectedValueProperty); }
            set { SetValue(SelectedValueProperty, value); }
        }

        // Using a DependencyProperty as the backing store for SelectedValue.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SelectedValueProperty =
            DependencyProperty.Register("SelectedValue", typeof(object), typeof(CharaSelector), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));


        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Text.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(CharaSelector), new PropertyMetadata(string.Empty));


        public ImageSource ImageSource
        {
            get { return (ImageSource)GetValue(ImageSourceProperty); }
            set { SetValue(ImageSourceProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ImageSource.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ImageSourceProperty =
            DependencyProperty.Register("ImageSource", typeof(ImageSource), typeof(CharaSelector), new PropertyMetadata(null));

        public CharaSelector()
        {
            InitializeComponent();
        }

        private static void MoveFocus()
        {
            var focusedElement = Keyboard.FocusedElement as UIElement;
            if (focusedElement != null)
            {
                var request = new TraversalRequest(FocusNavigationDirection.Next);
                focusedElement.MoveFocus(request);
            }
        }

        private void ComboBox_KeyDown(object sender, KeyEventArgs e)
        {
            var currentFocusedElement = Keyboard.FocusedElement as UIElement;

            if (currentFocusedElement == null || currentFocusedElement.GetType() != typeof(TextBox))
                return;

            IsDropDownOpen = true;

            //press enter or tab to confirm
            if (e.Key == Key.Tab || e.Key == Key.Enter)
            {
                e.Handled = true;
                if (Command != null && Command.CanExecute(Index))
                    Command.Execute(Index);

                IsDropDownOpen = false;
                //change Focus on Tab click
                MoveFocus();
                if (!IsSimpleModeEnabled)
                    MoveFocus();
                return;
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
            if (combobox.ItemsSource is not Dictionary<string, Character> itemSource)
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

        private void combobox_TextChanged(object sender, TextChangedEventArgs e)
        {
            //press space to search
            if (combobox.Text.Contains(' '))
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
}
