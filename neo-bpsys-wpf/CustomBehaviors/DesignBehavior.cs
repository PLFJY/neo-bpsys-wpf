using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace neo_bpsys_wpf.CustomBehaviors
{
    public static class DesignBehavior
    {
        public static readonly DependencyProperty IsDesignModeProperty =
            DependencyProperty.RegisterAttached("IsDesignMode", typeof(bool), typeof(DesignBehavior), new PropertyMetadata(false, OnIsDesignModeChanged));

        public static bool GetIsDesignMode(UIElement element)
        {
            return (bool)element.GetValue(IsDesignModeProperty);
        }

        public static void SetIsDesignMode(UIElement element, bool value)
        {
            element.SetValue(IsDesignModeProperty, value);
        }

        private static void OnIsDesignModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element)
            {
                if ((bool)e.NewValue)
                {
                    element.MouseLeftButtonDown += Element_MouseLeftButtonDown;
                    element.MouseLeftButtonUp += Element_MouseLeftButtonUp;
                    element.MouseMove += Element_MouseMove;

                    // 显示控件名称
                    ShowControlName(element);
                    // 添加边框
                    AddBorder(element);
                }
                else
                {
                    element.MouseLeftButtonDown -= Element_MouseLeftButtonDown;
                    element.MouseLeftButtonUp -= Element_MouseLeftButtonUp;
                    element.MouseMove -= Element_MouseMove;

                    // 隐藏控件名称
                    HideControlName(element);
                    // 移除边框
                    RemoveBorder(element);
                }
            }
        }

        private static void ShowControlName(UIElement element)
        {
            if (element is FrameworkElement fe && !string.IsNullOrEmpty(fe.Name))
            {
                Canvas canvas = GetParentCanvas(element);
                if (canvas != null)
                {
                    TextBlock textBlock = new TextBlock
                    {
                        Text = fe.Name,
                        Foreground = Brushes.Black,
                        Background = Brushes.LightGray,
                        Padding = new Thickness(2)
                    };
                    Canvas.SetLeft(textBlock, Canvas.GetLeft(fe));
                    Canvas.SetTop(textBlock, Canvas.GetTop(fe) - textBlock.ActualHeight);
                    canvas.Children.Add(textBlock);
                    element.SetValue(TagProperty, textBlock);
                }
            }
        }

        private static void HideControlName(UIElement element)
        {
            Canvas canvas = GetParentCanvas(element);
            if (canvas != null)
            {
                TextBlock textBlock = element.GetValue(TagProperty) as TextBlock;
                if (textBlock != null)
                {
                    canvas.Children.Remove(textBlock);
                    element.ClearValue(TagProperty);
                }
            }
        }

        private static void AddBorder(UIElement element)
        {
            if (element is Control control)
            {
                control.BorderBrush = Brushes.Red;
                control.BorderThickness = new Thickness(1);
            }
        }

        private static void RemoveBorder(UIElement element)
        {
            if (element is Control control)
            {
                control.BorderBrush = null;
                control.BorderThickness = new Thickness(0);
            }
        }

        private static Canvas GetParentCanvas(UIElement element)
        {
            DependencyObject parent = VisualTreeHelper.GetParent(element);
            while (parent != null && !(parent is Canvas))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as Canvas;
        }

        private static readonly DependencyProperty TagProperty =
            DependencyProperty.RegisterAttached("Tag", typeof(object), typeof(DesignBehavior), new PropertyMetadata(null));

        private static Point _startPoint;
        private static bool _isDragging;

        private static void Element_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is UIElement element)
            {
                _startPoint = e.GetPosition(null);
                _isDragging = true;
                element.CaptureMouse();
            }
        }

        private static void Element_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is UIElement element)
            {
                _isDragging = false;
                element.ReleaseMouseCapture();
            }
        }

        private static void Element_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && sender is FrameworkElement element)
            {
                Point currentPoint = e.GetPosition(null);
                Vector offset = currentPoint - _startPoint;

                double left = Canvas.GetLeft(element);
                double top = Canvas.GetTop(element);

                if (double.IsNaN(left))
                    left = 0;
                if (double.IsNaN(top))
                    top = 0;

                Canvas.SetLeft(element, left + offset.X);
                Canvas.SetTop(element, top + offset.Y);

                // 移动控件名称
                MoveControlName(element);

                _startPoint = currentPoint;
            }
        }

        private static void MoveControlName(UIElement element)
        {
            Canvas canvas = GetParentCanvas(element);
            if (canvas != null)
            {
                TextBlock textBlock = element.GetValue(TagProperty) as TextBlock;
                if (textBlock != null)
                {
                    Canvas.SetLeft(textBlock, Canvas.GetLeft(element));
                    Canvas.SetTop(textBlock, Canvas.GetTop(element) - textBlock.ActualHeight);
                }
            }
        }
    }
}