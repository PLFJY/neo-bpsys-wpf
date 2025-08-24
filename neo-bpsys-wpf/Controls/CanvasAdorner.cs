using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using neo_bpsys_wpf.AttachedBehaviors;

namespace neo_bpsys_wpf.Controls;

/// <summary>
/// 设计者模式下的控件Adorner，用于调整控件大小和显示控件名称
/// </summary>
public class CanvasAdorner : Adorner
{
    //4条边
    private readonly Thumb _leftThumb, _topThumb, _rightThumb, _bottomThumb;

    //4个角
    private readonly Thumb _lefTopThumb, _rightTopThumb, _rightBottomThumb, _leftbottomThumb;

    //中间移动区域
    private readonly Border _outLine;

    private readonly TextBlock _textBlock;

    //布局容器，如果不使用布局容器，则需要给上述8个控件布局，实现和Grid布局定位是一样的，会比较繁琐且意义不大。
    private readonly Grid _grid;
    private readonly FrameworkElement _adornedElement;

    /// <summary>
    /// 创建一个CanvasAdorner
    /// </summary>
    /// <param name="adornedElement">要添加Adorner的控件</param>
    public CanvasAdorner(FrameworkElement adornedElement) : base(adornedElement)
    {
        _adornedElement = adornedElement;
        //初始化thumb
        _leftThumb = new Thumb
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = Cursors.SizeWE
        };
        _topThumb = new Thumb
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Cursor = Cursors.SizeNS
        };
        _rightThumb = new Thumb
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = Cursors.SizeWE
        };
        _bottomThumb = new Thumb
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Cursor = Cursors.SizeNS
        };
        _lefTopThumb = new Thumb
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Cursor = Cursors.SizeNWSE
        };
        _rightTopThumb = new Thumb
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Cursor = Cursors.SizeNESW
        };
        _rightBottomThumb = new Thumb
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Cursor = Cursors.SizeNWSE
        };
        _leftbottomThumb = new Thumb
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            Cursor = Cursors.SizeNESW
        };
        _outLine = new Border
        {
            Background = Brushes.Transparent,
            Cursor = Cursors.SizeAll,
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Red
        };
        _textBlock = new TextBlock
        {
            Text = adornedElement.Name,
            Foreground = Brushes.Black,
            Background = Brushes.LightGray,
            Opacity = 0.5,
            IsHitTestVisible = false,
            Padding = new Thickness(2),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _grid = new Grid();
        //将thumbs添加到一个grid里面给grid布局
        _grid.Children.Add(_leftThumb);
        _grid.Children.Add(_topThumb);
        _grid.Children.Add(_rightThumb);
        _grid.Children.Add(_bottomThumb);
        _grid.Children.Add(_lefTopThumb);
        _grid.Children.Add(_rightTopThumb);
        _grid.Children.Add(_rightBottomThumb);
        _grid.Children.Add(_leftbottomThumb);
        AddVisualChild(_grid);
        //修改thumb的样式
        foreach (Thumb thumb in _grid.Children)
        {
            thumb.Width = 5;
            thumb.Height = 5;
            thumb.Background = Brushes.White;
            thumb.Template = new ControlTemplate(typeof(Thumb))
            {
                VisualTree = GetFactory(new SolidColorBrush(Colors.White))
            };
            thumb.DragDelta += Thumb_DragDelta;
            Panel.SetZIndex(thumb, 2);
        }

        _grid.Children.Add(_outLine);
        Panel.SetZIndex(_outLine, 1);
        _outLine.MouseLeftButtonDown += OutLineMouseLeftButtonDown;
        _outLine.MouseLeftButtonUp += OutLineMouseLeftButtonUp;
        _outLine.MouseMove += OutLineMouseMove;
        ShowControlName(adornedElement);
        Unloaded += (_, _) => { HideControlName(adornedElement); };
    }

    /// <summary>
    /// 显示控件名称
    /// </summary>
    /// <param name="element">要显示控件名称的控件</param>
    private void ShowControlName(FrameworkElement element)
    {
        if (string.IsNullOrEmpty(element.Name)) return;
        var canvas = GetParentCanvas(element);
        if (canvas == null) return;
        Canvas.SetLeft(_textBlock, (Canvas.GetLeft(element) + element.ActualWidth / 2) - _textBlock.ActualWidth / 2);
        Canvas.SetTop(_textBlock, Canvas.GetTop(element) - _textBlock.ActualHeight * 1.5);
        canvas.Children.Add(_textBlock);
        element.SetValue(TagProperty, _textBlock);
    }

    /// <summary>
    /// 隐藏控件名称
    /// </summary>
    /// <param name="element">要隐藏控件名称的控件</param>
    private static void HideControlName(FrameworkElement element)
    {
        var canvas = GetParentCanvas(element);
        if (canvas == null) return;
        if (element.GetValue(TagProperty) is not TextBlock textBlock) return;
        canvas.Children.Remove(textBlock);
        element.ClearValue(TagProperty);
    }

    /// <summary>
    /// 获取父Canvas
    /// </summary>
    /// <param name="element">要获取的元素</param>
    /// <returns>父Canvas</returns>
    private static Canvas? GetParentCanvas(FrameworkElement element)
    {
        var parent = VisualTreeHelper.GetParent(element);
        while (parent != null && parent is not Canvas)
        {
            parent = VisualTreeHelper.GetParent(parent);
        }

        return parent as Canvas;
    }

    /// <summary>
    /// 移动控件名称
    /// </summary>
    /// <param name="element">要移动的控件</param>
    private static void MoveControlName(FrameworkElement element)
    {
        var canvas = GetParentCanvas(element);
        if (canvas == null) return;
        if (element.GetValue(TagProperty) is not TextBlock textBlock) return;
        Canvas.SetLeft(textBlock, (Canvas.GetLeft(element) + element.ActualWidth / 2) - textBlock.ActualWidth / 2);
        Canvas.SetTop(textBlock, Canvas.GetTop(element) - textBlock.ActualHeight * 1.5);
    }

    //鼠标是否按下
    private bool _isMouseDown;

    //鼠标按下的位置
    private Point _mouseDownPosition;

    //鼠标按下控件的位置
    private Point _mouseDownControlPosition;

    /// <summary>
    /// 鼠标移动事件
    /// </summary>
    /// <param name="sender">事件发送者（控件）</param>
    /// <param name="e">鼠标事件参数</param>
    private void OutLineMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isMouseDown) return;
        if (sender is not Border) return;

        var pos = e.GetPosition(null);
        var dp = pos - _mouseDownPosition;
        var newLeft = _mouseDownControlPosition.X + dp.X;
        var newTop = _mouseDownControlPosition.Y + dp.Y;

        // 检查Shift键状态
        var isShiftPressed = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

        // 执行对齐检查
        if (!isShiftPressed) // 仅当未按住Shift时执行吸附
        {
            SnapToNearestControl(ref newLeft, ref newTop);
        }

        Canvas.SetLeft(_adornedElement, newLeft);
        Canvas.SetTop(_adornedElement, newTop);
        MoveControlName(_adornedElement);
    }

    // 吸附距离阈值
    private const double SnapDistance = 10.0;

    /// <summary>
    /// 移动时吸附
    /// </summary>
    /// <param name="newLeft">新的左边位置</param>
    /// <param name="newTop">新的顶部位置</param>
    private void SnapToNearestControl(ref double newLeft, ref double newTop)
    {
        if (_adornedElement.Parent is not Canvas parentCanvas) return;

        foreach (var child in parentCanvas.Children)
        {
            if (child is not FrameworkElement element || element == _adornedElement) continue;

            if (!DesignBehavior.GetIsDesignMode(element)) continue;

            var otherLeft = Canvas.GetLeft(element);
            var otherTop = Canvas.GetTop(element);

            // 水平方向对齐
            if (Math.Abs(newLeft - otherLeft) <= SnapDistance) // 左边对齐
            {
                newLeft = otherLeft;
            }
            else if (Math.Abs(newLeft + _adornedElement.ActualWidth -
                              (otherLeft + element.ActualWidth)) <= SnapDistance) // 右边对齐
            {
                newLeft = otherLeft + element.ActualWidth - _adornedElement.ActualWidth;
            }

            // 垂直方向对齐
            if (Math.Abs(newTop - otherTop) <= SnapDistance) // 顶部对齐
            {
                newTop = otherTop;
            }
            else if (Math.Abs(newTop + _adornedElement.ActualHeight -
                              (otherTop + element.ActualHeight)) <= SnapDistance) // 底部对齐
            {
                newTop = otherTop + element.ActualHeight - _adornedElement.ActualHeight;
            }
        }
    }

    /// <summary>
    /// 鼠标抬起事件
    /// </summary>
    /// <param name="sender">事件发送者（控件）</param>
    /// <param name="e">鼠标事件参数</param>
    private void OutLineMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border) return;
        _isMouseDown = false;
        border.ReleaseMouseCapture();
    }

    /// <summary>
    /// 鼠标按下事件
    /// </summary>
    /// <param name="sender">事件发送者（控件）</param>
    /// <param name="e">鼠标事件参数</param>
    private void OutLineMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border) return;
        _isMouseDown = true;
        _mouseDownPosition = e.GetPosition(null);
        _mouseDownControlPosition = new Point(
            double.IsNaN(Canvas.GetLeft(_adornedElement)) ? 0 : Canvas.GetLeft(_adornedElement),
            double.IsNaN(Canvas.GetTop(_adornedElement)) ? 0 : Canvas.GetTop(_adornedElement));
        border.CaptureMouse();
    }

    /// <summary>
    /// 获取子控件
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    protected override Visual GetVisualChild(int index)
    {
        return _grid;
    }

    /// <summary>
    /// 获取子控件数量
    /// </summary>
    protected override int VisualChildrenCount => 1;

    /// <summary>
    /// 调整大小
    /// </summary>
    /// <param name="finalSize">最终大小</param>
    /// <returns>最终大小</returns>
    protected override Size ArrangeOverride(Size finalSize)
    {
        //直接给grid布局，grid内部的thumb会自动布局。
        _grid.Arrange(new Rect(new Point(-_leftThumb.Width / 2, -_leftThumb.Height / 2),
            new Size(finalSize.Width + _leftThumb.Width, finalSize.Height + _leftThumb.Height)));
        return finalSize;
    }

    /// <summary>
    /// 拖拽调整大小
    /// </summary>
    /// <param name="sender">拖拽的控件</param>
    /// <param name="e">拖拽事件参数</param>
    private void Thumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var thumb = sender as FrameworkElement;
        double left, top, width, height;

        var actualWidth = _adornedElement.ActualWidth;
        var actualHeight = _adornedElement.ActualHeight;

        if (thumb?.HorizontalAlignment == HorizontalAlignment.Left)
        {
            left = double.IsNaN(Canvas.GetLeft(_adornedElement))
                ? 0
                : Canvas.GetLeft(_adornedElement) + e.HorizontalChange;
            width = actualWidth - e.HorizontalChange;
        }
        else
        {
            left = Canvas.GetLeft(_adornedElement);
            width = actualWidth + e.HorizontalChange;
        }

        if (thumb?.VerticalAlignment == VerticalAlignment.Top)
        {
            top = double.IsNaN(Canvas.GetTop(_adornedElement))
                ? 0
                : Canvas.GetTop(_adornedElement) + e.VerticalChange;
            height = actualHeight - e.VerticalChange;
        }
        else
        {
            top = Canvas.GetTop(_adornedElement);
            height = actualHeight + e.VerticalChange;
        }

        var isShiftPressed = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

        // if (!isShiftPressed)
        // {
        //     AdjustSizeToNearestControl(ref width, ref height, ref top, ref left, thumb, e);
        // }


        if (thumb?.HorizontalAlignment != HorizontalAlignment.Center)
        {
            if (width >= 0)
            {
                Canvas.SetLeft(_adornedElement, left);
                _adornedElement.Width = width;
            }
        }

        if (thumb?.VerticalAlignment != VerticalAlignment.Center)
        {
            if (height >= 0)
            {
                Canvas.SetTop(_adornedElement, top);
                _adornedElement.Height = height;
            }
        }

        MoveControlName(_adornedElement);
    }

    private const double SnapSizeDistance = 200;

    private void AdjustSizeToNearestControl(
        ref double height, ref double width,
        ref double top, ref double left,
        FrameworkElement thumb, DragDeltaEventArgs e)
    {
        if (_adornedElement.Parent is not Canvas parentCanvas) return;

        var minDistance = double.MaxValue;
        FrameworkElement? closestElement = null;

        // 先找到最近的控件
        foreach (var child in parentCanvas.Children)
        {
            if (child is not FrameworkElement otherElement || otherElement == _adornedElement) continue;
            if (!DesignBehavior.GetIsDesignMode(otherElement)) continue;

            var otherLeft = Canvas.GetLeft(otherElement);
            var otherTop = Canvas.GetTop(otherElement);

            var distance = Distance(new Point(left, top), new Point(otherLeft, otherTop));

            if (distance <= SnapSizeDistance && distance < minDistance)
            {
                minDistance = distance;
                closestElement = otherElement;
            }
            
        }

        // 如果找到最近的控件，执行对齐逻辑
        if (closestElement != null)
        {
            var originalLeft = Canvas.GetLeft(_adornedElement);
            var originalTop = Canvas.GetTop(_adornedElement);
            var originalWidth = width;
            var originalHeight = height;

            var otherWidth = closestElement.ActualWidth;
            var otherHeight = closestElement.ActualHeight;

            if (thumb.HorizontalAlignment == HorizontalAlignment.Left)
            {
                if (Math.Abs(_adornedElement.ActualWidth - otherWidth) <= SnapDistance)
                {
                    width = otherWidth;
                    var change = width - originalWidth;
                }
            }
        }
    }

    private static double Distance(Point p1, Point p2) =>
        Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));

    /// <summary>
    /// Thumbs样式工厂方法
    /// </summary>
    /// <param name="back">背景颜色</param>
    /// <returns>Thumb样式工厂</returns>
    private static FrameworkElementFactory GetFactory(Brush back)
    {
        var fef = new FrameworkElementFactory(typeof(Rectangle));
        fef.SetValue(Shape.FillProperty, back);
        return fef;
    }
}