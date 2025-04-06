using hyjiacan.py4n;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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
        public static readonly DependencyProperty ImageSourceProperty =
            DependencyProperty.Register("ImageSource", typeof(ImageSource), typeof(CharacterSelector), new PropertyMetadata(null));

        public ICommand Command
        {
            get { return (ICommand)GetValue(CommandProperty); }
            set { SetValue(CommandProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Command.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register("Command", typeof(ICommand), typeof(CharacterSelector), new PropertyMetadata(null));



        public bool IsSimpleModeEnable
        {
            get { return (bool)GetValue(IsSimpleModeEnableProperty); }
            set { SetValue(IsSimpleModeEnableProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsSimpleModeEnable.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsSimpleModeEnableProperty =
            DependencyProperty.Register("IsSimpleModeEnable", typeof(bool), typeof(CharacterSelector), new PropertyMetadata(false));




        static CharacterSelector()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(CharacterSelector), new FrameworkPropertyMetadata(typeof(CharacterSelector)));
        }
        /// <summary>
        /// A methos used to move focus
        /// </summary>
        private void MoveFocus()
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
            if (currentFocusedElement == null || currentFocusedElement.GetType() != typeof(TextBox)) return;

            //press Enter to change focuse
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                if (Command != null && Command.CanExecute(Name))
                    Command.Execute(Name);

                IsDropDownOpen = false;
                //change Focus on Tab click
                MoveFocus();
                return;
            }

            IsDropDownOpen = true;

            //press Tab to search character
            if (e.Key == Key.Tab)
            {
                e.Handled = true;
                var currentText = base.Text;
                var findIndex = FindIndex(currentText);
                base.SelectedIndex = FindIndex(currentText);
            }
        }

        //logic of searching by Pinyin
        private List<string> _fullNameList = new();
        private List<string> _fullSpells = new();
        private List<string> _abbrevs = new();

        protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
        {
            base.OnItemsSourceChanged(oldValue, newValue);
            base.IsEnabled = false;
            base.Text = "正在初始化";
            ///get Pinyin data
            var format = PinyinFormat.WITHOUT_TONE | PinyinFormat.LOWERCASE | PinyinFormat.WITH_U_AND_COLON | PinyinFormat.WITH_V;
            foreach (var item in newValue)
            {
                if (item is not string hanzi) return;
                _fullNameList.Add(hanzi);//original base

                var pinyin = Pinyin4Net.GetPinyin(hanzi, format);//get pinyin with space

                var parts = pinyin.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                //full pinyin without space
                _fullSpells.Add(string.Concat(parts));

                //special case
                if (hanzi == "26号守卫")
                {
                    _abbrevs.Add("bb");
                    continue;
                }
                //abbreviations
                _abbrevs.Add(string.Concat(parts.Select(p => p[0])));
            }
            base.IsEnabled = true;
            base.Text = string.Empty;
        }

        /// <summary>
        /// Find the index of ths option waiting to be found
        /// </summary>
        /// <param name="inputText"></param>
        /// <returns></returns>
        public int FindIndex(string inputText)
        {
            string inputLower = inputText.ToLowerInvariant();

            for (int i = 0; i < _fullNameList.Count; i++)
            {
                string fullSpell = _fullSpells[i].ToLowerInvariant();
                string abbrev = _abbrevs[i].ToLowerInvariant();
                string fullName = _fullNameList[i];

                // Check whether the full prefix matches or the short prefix matches
                if (fullSpell.StartsWith(inputLower) || abbrev.StartsWith(inputLower) || fullName.StartsWith(inputText))
                {
                    return i;
                }
            }
            //if not foound
            Text = string.Empty;
            return -1;
        }
    }
}
