using neo_bpsys_wpf.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace neo_bpsys_wpf.CustomControls
{
    public class GlobalScorePresenter : Control
    {
        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Text.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(GlobalScorePresenter), new FrameworkPropertyMetadata("-", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public bool IsHunIcon
        {
            get { return (bool)GetValue(IsHunIconProperty); }
            set { SetValue(IsHunIconProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsHunIcon.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsHunIconProperty =
            DependencyProperty.Register("IsHunIcon", typeof(bool), typeof(GlobalScorePresenter), new PropertyMetadata(false));



        public bool IsCampVisible
        {
            get { return (bool)GetValue(IsCampVisibleProperty); }
            set { SetValue(IsCampVisibleProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsCampVisible.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsCampVisibleProperty =
            DependencyProperty.Register("IsCampVisible", typeof(bool), typeof(GlobalScorePresenter), new PropertyMetadata(false));


    }
}
