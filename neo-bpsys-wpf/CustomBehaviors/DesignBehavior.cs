using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace neo_bpsys_wpf.CustomBehaviors
{
    /// <summary>
    /// 提供设计时行为扩展，支持控件拖拽和名称显示功能
    /// 通过附加属性IsDesignMode启用/禁用设计模式
    /// </summary>
    public static class DesignBehavior
    {
        /// <summary>
        /// 标识是否处于设计模式的附加属性
        /// 启用时允许控件拖拽并显示控件名称
        /// </summary>
        public static readonly DependencyProperty IsDesignModeProperty =
            DependencyProperty.RegisterAttached(
                "IsDesignMode",
                typeof(bool),
                typeof(DesignBehavior),
                new PropertyMetadata(false, OnIsDesignModeChanged)
            );

        /// <summary>
        /// 获取指定元素的设计模式状态
        /// </summary>
        /// <param name="element">目标UI元素</param>
        /// <returns>是否启用设计模式</returns>
        public static bool GetIsDesignMode(UIElement element)
        {
            return (bool)element.GetValue(IsDesignModeProperty);
        }

        /// <summary>
        /// 设置指定元素的设计模式状态
        /// </summary>
        /// <param name="element">目标UI元素</param>
        /// <param name="value">设计模式启用状态</param>
        public static void SetIsDesignMode(UIElement element, bool value)
        {
            element.SetValue(IsDesignModeProperty, value);
        }

        /// <summary>
        /// 设计模式属性变更处理
        /// 负责注册/注销鼠标事件处理程序并控制名称显示
        /// </summary>
        /// <param name="d">属性所属的UI元素</param>
        /// <param name="e">属性变更事件参数</param>
        private static void OnIsDesignModeChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element)
            {
                if ((bool)e.NewValue)
                {
                    element.MouseLeftButtonDown += Element_MouseLeftButtonDown;
                    element.MouseLeftButtonUp += Element_MouseLeftButtonUp;
                    element.MouseMove += Element_MouseMove;
                    ShowControlName(element);
                }
                else
                {
                    element.MouseLeftButtonDown -= Element_MouseLeftButtonDown;
                    element.MouseLeftButtonUp -= Element_MouseLeftButtonUp;
                    element.MouseMove -= Element_MouseMove;
                    HideControlName(element);
                }
            }
        }

        /// <summary>
        /// 在控件上方显示名称标签
        /// 创建并定位TextBlock用于展示FrameworkElement的名称
        /// </summary>
        /// <param name="element">需要显示名称的UI元素</param>
        private static void ShowControlName(UIElement element)
        {
            if (element is FrameworkElement fe && !string.IsNullOrEmpty(fe.Name))
            {
                Canvas? canvas = GetParentCanvas(element);
                if (canvas != null)
                {
                    TextBlock textBlock = new TextBlock
                    {
                        Text = fe.Name,
                        Foreground = Brushes.Black,
                        Background = Brushes.LightGray,
                        Opacity = 0.5,
                        IsHitTestVisible = false,
                        Padding = new Thickness(2),
                    };
                    Canvas.SetLeft(textBlock, Canvas.GetLeft(fe));
                    Canvas.SetTop(textBlock, Canvas.GetTop(fe) - textBlock.ActualHeight);
                    canvas.Children.Add(textBlock);
                    element.SetValue(TagProperty, textBlock);
                }
            }
        }

        /// <summary>
        /// 隐藏控件名称标签
        /// 从画布中移除对应的TextBlock
        /// </summary>
        /// <param name="element">需要隐藏名称的UI元素</param>
        private static void HideControlName(UIElement element)
        {
            Canvas? canvas = GetParentCanvas(element);
            if (canvas != null)
            {
                TextBlock? textBlock = element.GetValue(TagProperty) as TextBlock;
                if (textBlock != null)
                {
                    canvas.Children.Remove(textBlock);
                    element.ClearValue(TagProperty);
                }
            }
        }

        /// <summary>
        /// 查找最近的父级Canvas容器
        /// 用于定位名称标签的位置
        /// </summary>
        /// <param name="element">子元素</param>
        /// <returns>最近的Canvas父容器</returns>
        private static Canvas? GetParentCanvas(UIElement element)
        {
            DependencyObject parent = VisualTreeHelper.GetParent(element);
            while (parent != null && !(parent is Canvas))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as Canvas;
        }

        /// <summary>
        /// 用于存储控件名称标签的附加属性
        /// </summary>
        private static readonly DependencyProperty TagProperty =
            DependencyProperty.RegisterAttached(
                "Tag",
                typeof(object),
                typeof(DesignBehavior),
                new PropertyMetadata(null)
            );

        // 鼠标拖拽相关状态变量
        private static Point _startPoint;
        private static bool _isDragging;

        /// <summary>
        /// 鼠标左键按下事件处理
        /// 记录起始位置并开始拖拽操作
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标事件参数</param>
        private static void Element_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is UIElement element)
            {
                _startPoint = e.GetPosition(null);
                _isDragging = true;
                element.CaptureMouse();
            }
        }

        /// <summary>
        /// 鼠标左键释放事件处理
        /// 结束拖拽操作
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标事件参数</param>
        private static void Element_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is UIElement element)
            {
                _isDragging = false;
                element.ReleaseMouseCapture();
            }
        }

        /// <summary>
        /// 鼠标移动事件处理
        /// 实现控件拖拽移动功能
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标事件参数</param>
        private static void Element_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && sender is FrameworkElement element)
            {
                Point currentPoint = e.GetPosition(null);
                Vector offset = currentPoint - _startPoint;

                double left = Canvas.GetLeft(element);
                double top = Canvas.GetTop(element);

                if (double.IsNaN(left)) left = 0;
                if (double.IsNaN(top)) top = 0;

                Canvas.SetLeft(element, left + offset.X);
                Canvas.SetTop(element, top + offset.Y);

                MoveControlName(element);
                _startPoint = currentPoint;
            }
        }

        /// <summary>
        /// 同步更新控件名称标签的位置
        /// 保持标签始终位于控件上方
        /// </summary>
        /// <param name="element">被移动的控件</param>
        private static void MoveControlName(UIElement element)
        {
            Canvas? canvas = GetParentCanvas(element);
            if (canvas != null)
            {
                TextBlock? textBlock = element.GetValue(TagProperty) as TextBlock;
                if (textBlock != null)
                {
                    Canvas.SetLeft(textBlock, Canvas.GetLeft(element));
                    Canvas.SetTop(textBlock, Canvas.GetTop(element) - textBlock.ActualHeight);
                }
            }
        }
    }
}