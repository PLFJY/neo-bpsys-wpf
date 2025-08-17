using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

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
    private readonly Rectangle _rectangle;
    private readonly TextBlock _textBlock;
    //布局容器，如果不使用布局容器，则需要给上述8个控件布局，实现和Grid布局定位是一样的，会比较繁琐且意义不大。
    private readonly Grid _grid;
    private readonly FrameworkElement _adornedElement;
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
        _rectangle = new Rectangle
        {
            Fill = Brushes.Red,
            Opacity = 0.15,
            Cursor = Cursors.SizeAll
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
        _grid.Children.Add(_leftThumb);
        _grid.Children.Add(_topThumb);
        _grid.Children.Add(_rightThumb);
        _grid.Children.Add(_bottomThumb);
        _grid.Children.Add(_lefTopThumb);
        _grid.Children.Add(_rightTopThumb);
        _grid.Children.Add(_rightBottomThumb);
        _grid.Children.Add(_leftbottomThumb);
        AddVisualChild(_grid);
        foreach (Thumb thumb in _grid.Children)
        {
            thumb.Width = 10;
            thumb.Height = 10;
            thumb.Background = Brushes.White;
            thumb.Opacity = 0.5;
            thumb.Template = new ControlTemplate(typeof(Thumb))
            {
                VisualTree = GetFactory(new SolidColorBrush(Colors.White))
            };
            thumb.DragDelta += Thumb_DragDelta;
            Panel.SetZIndex(thumb, 2);
        }

        _grid.Children.Add(_rectangle);
        Panel.SetZIndex(_rectangle, 1);
        _rectangle.MouseLeftButtonDown += RectangleMouseLeftButtonDown;
        _rectangle.MouseLeftButtonUp += RectangleMouseLeftButtonUp;
        _rectangle.MouseMove += RectangleMouseMove;
        ShowControlName(adornedElement);
        Unloaded += (_, _) =>
        {
            HideControlName(adornedElement);
        };
    }

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

    private static void HideControlName(FrameworkElement element)
    {
        var canvas = GetParentCanvas(element);
        if (canvas == null) return;
        if (element.GetValue(TagProperty) is not TextBlock textBlock) return;
        canvas.Children.Remove(textBlock);
        element.ClearValue(TagProperty);
    }

    private static Canvas? GetParentCanvas(FrameworkElement element)
    {
        var parent = VisualTreeHelper.GetParent(element);
        while (parent != null && parent is not Canvas)
        {
            parent = VisualTreeHelper.GetParent(parent);
        }

        return parent as Canvas;
    }

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
    private void RectangleMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isMouseDown) return;
        if (sender is not Rectangle rectangle) return;
        var pos = e.GetPosition(null);
        var dp = pos - _mouseDownPosition;
        Canvas.SetLeft(_adornedElement, _mouseDownControlPosition.X + dp.X);
        Canvas.SetTop(_adornedElement, _mouseDownControlPosition.Y + dp.Y);
        MoveControlName(_adornedElement);
    }

    private void RectangleMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Rectangle rectangle) return;
        _isMouseDown = false;
        rectangle.ReleaseMouseCapture();
    }

    private void RectangleMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Rectangle rectangle) return;
        _isMouseDown = true;
        _mouseDownPosition = e.GetPosition(null);
        _mouseDownControlPosition = new Point(
            double.IsNaN(Canvas.GetLeft(_adornedElement)) ? 0 : Canvas.GetLeft(_adornedElement),
            double.IsNaN(Canvas.GetTop(_adornedElement)) ? 0 : Canvas.GetTop(_adornedElement));
        rectangle.CaptureMouse();
    }

    protected override Visual GetVisualChild(int index)
    {
        return _grid;
    }
    protected override int VisualChildrenCount => 1;

    protected override Size ArrangeOverride(Size finalSize)
    {
        //直接给grid布局，grid内部的thumb会自动布局。
        _grid.Arrange(new Rect(new Point(-_leftThumb.Width / 2, -_leftThumb.Height / 2), new Size(finalSize.Width + _leftThumb.Width, finalSize.Height + _leftThumb.Height)));
        return finalSize;
    }
    //调整大小逻辑
    private void Thumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var thumb = sender as FrameworkElement;
        double left, top, width, height;

        var actualWidth = _adornedElement.ActualWidth;
        var actualHeight = _adornedElement.ActualHeight;

        if (thumb?.HorizontalAlignment == HorizontalAlignment.Left)
        {
            left = double.IsNaN(Canvas.GetLeft(_adornedElement)) ? 0 : Canvas.GetLeft(_adornedElement) + e.HorizontalChange;
            width = actualWidth - e.HorizontalChange;
        }
        else
        {
            left = Canvas.GetLeft(_adornedElement);
            width = actualWidth + e.HorizontalChange;
        }

        if (thumb?.VerticalAlignment == VerticalAlignment.Top)
        {
            top = double.IsNaN(Canvas.GetTop(_adornedElement)) ? 0 : Canvas.GetTop(_adornedElement) + e.VerticalChange;
            height = actualHeight - e.VerticalChange;
        }
        else
        {
            top = Canvas.GetTop(_adornedElement);
            height = actualHeight + e.VerticalChange;
        }

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
    }

    //thumb的样式
    private static FrameworkElementFactory GetFactory(Brush back)
    {
        var fef = new FrameworkElementFactory(typeof(Rectangle));
        fef.SetValue(Shape.FillProperty, back);
        return fef;
    }
}