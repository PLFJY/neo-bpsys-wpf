using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using neo_bpsys_wpf.Models;

namespace neo_bpsys_wpf.CustomControls
{
    public class CharacterSelector : ComboBox
    {
        public ImageSource ImageSource
        {
            get { return (ImageSource)GetValue(ImageSourceProperty); }
            set { SetValue(ImageSourceProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ImageSource.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ImageSourceProperty = DependencyProperty.Register(
            "ImageSource",
            typeof(ImageSource),
            typeof(CharacterSelector),
            new PropertyMetadata(null)
        );

        public ICommand Command
        {
            get { return (ICommand)GetValue(CommandProperty); }
            set { SetValue(CommandProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Command.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
            "Command",
            typeof(ICommand),
            typeof(CharacterSelector),
            new PropertyMetadata(null)
        );

        public bool IsSimpleModeEnable
        {
            get { return (bool)GetValue(IsSimpleModeEnableProperty); }
            set { SetValue(IsSimpleModeEnableProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsSimpleModeEnable.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsSimpleModeEnableProperty =
            DependencyProperty.Register(
                "IsSimpleModeEnable",
                typeof(bool),
                typeof(CharacterSelector),
                new PropertyMetadata(false)
            );

        static CharacterSelector()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(CharacterSelector),
                new FrameworkPropertyMetadata(typeof(CharacterSelector))
            );
        }

        /// <summary>
        /// A methos used to move focus
        /// </summary>
        private static void MoveFocus()
        {
            var focusedElement = Keyboard.FocusedElement as UIElement;
            if (focusedElement != null)
            {
                var request = new TraversalRequest(FocusNavigationDirection.Next);
                focusedElement.MoveFocus(request);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            //ensure foucuce on Combobox edit area
            var currentFocusedElement = Keyboard.FocusedElement as UIElement;

            if (currentFocusedElement == null || currentFocusedElement.GetType() != typeof(TextBox))
                return;

            //press Enter to change focuse
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                if (Command != null && Command.CanExecute(Name))
                    Command.Execute(Name);

                IsDropDownOpen = false;
                //change Focus on Tab click
                MoveFocus();
                MoveFocus();
                return;
            }

            IsDropDownOpen = true;

            //press Tab to search character
            if (e.Key == Key.Tab)
            {
                e.Handled = true;
                var currentText = base.Text;
                var findedIndex = FindIndex(currentText);
                base.SelectedIndex = findedIndex;
                if (findedIndex == -1)
                    return;
                if (ItemsSource is Dictionary<string, Character> itemSource)
                    Text = itemSource.ElementAt(findedIndex).Key;
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
            if (base.ItemsSource is not Dictionary<string, Character> itemSource)
                return -1;

            var index = 0;

            foreach (var item in itemSource)
            {
                var fullSpell = item.Value.FullSpell.ToLowerInvariant();
                var abbrev = item.Value.Abbrev.ToLowerInvariant();
                var fullName = item.Value.Name;
                // Check whether the full prefix matches or the short prefix matches
                if (
                    fullSpell.StartsWith(inputLower)
                    || abbrev.StartsWith(inputLower)
                    || fullName.StartsWith(inputText)
                )
                {
                    return index;
                }
                index++;
            }

            Text = string.Empty;
            return -1;
        }
    }
}
