using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;

namespace neo_bpsys_wpf.Behaviors
{
    public static class DragBehavior
    {
        public static readonly DependencyProperty IsDraggableProperty =
            DependencyProperty.RegisterAttached("IsDraggable", typeof(bool), typeof(DragBehavior), new PropertyMetadata(false, OnIsDraggableChanged));

        public static bool GetIsDraggable(UIElement element)
        {
            return (bool)element.GetValue(IsDraggableProperty);
        }

        public static void SetIsDraggable(UIElement element, bool value)
        {
            element.SetValue(IsDraggableProperty, value);
        }

        private static void OnIsDraggableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element)
            {
                if ((bool)e.NewValue)
                {
                    element.MouseLeftButtonDown += Element_MouseLeftButtonDown;
                    element.MouseLeftButtonUp += Element_MouseLeftButtonUp;
                    element.MouseMove += Element_MouseMove;
                }
                else
                {
                    element.MouseLeftButtonDown -= Element_MouseLeftButtonDown;
                    element.MouseLeftButtonUp -= Element_MouseLeftButtonUp;
                    element.MouseMove -= Element_MouseMove;
                }
            }
        }

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
                Canvas.SetLeft(element, left + offset.X);
                Canvas.SetTop(element, top + offset.Y);

                _startPoint = currentPoint;
            }
        }
    }
}
