using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace neo_bpsys_wpf.CustomControls
{
    [ContentProperty("Content")]
    public class DeferredContentHost : FrameworkElement
    {
        #region Fields
        private ContentControl _container = new ContentControl();
        #endregion
        #region Methods
        public DeferredContentHost()
        {
            this.AddVisualChild(_container);
            this.Loaded += DeferredContentHost_Loaded;
        }
        private void DeferredContentHost_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsInDesignMode)
            {
                this._container.Content = this.Content;
            }
            else
            {
                this._container.Content = this.Skeleton;
                this.Dispatcher.BeginInvoke((Action)(() => this._container.Content = this.Content), System.Windows.Threading.DispatcherPriority.ContextIdle);
            }
        }
        protected override Visual GetVisualChild(int index)
        {
            return _container;
        }
        protected override Size MeasureOverride(Size availableSize)
        {
            _container.Measure(availableSize);
            if (availableSize.Width == double.PositiveInfinity || availableSize.Height == double.PositiveInfinity)
            {
                return _container.DesiredSize;
            }
            return availableSize;
        }
        protected override Size ArrangeOverride(Size finalSize)
        {
            _container.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
            return base.ArrangeOverride(finalSize);
        }
        #endregion
        #region Properties
        protected override int VisualChildrenCount => 1;
        protected bool IsInDesignMode => DesignerProperties.GetIsInDesignMode(this);

        public object Content
        {
            get => (object)GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }
        // Using a DependencyProperty as the backing store for UIElement.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ContentProperty =
            DependencyProperty.Register(nameof(Content), typeof(object), typeof(DeferredContentHost));
        public object Skeleton
        {
            get => (object)GetValue(SkeletonProperty);
            set => SetValue(SkeletonProperty, value);
        }
        // Using a DependencyProperty as the backing store for Skeleton.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SkeletonProperty =
            DependencyProperty.Register(nameof(Skeleton), typeof(object), typeof(DeferredContentHost));
        #endregion
    }
}
