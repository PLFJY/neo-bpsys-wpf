using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Collections.Generic;
using neo_bpsys_wpf.Core.AttachedBehaviors;

namespace neo_bpsys_wpf.Core.Controls;

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

    private static readonly HashSet<FrameworkElement> SelectedElements = new();
    private Dictionary<FrameworkElement, Point>? _selectedStartPositions;
    private bool _hasDragged;
    private const double DragStartThreshold = 2.0;
    private Line? _hGuideLine;
    private Line? _vGuideLine;
    private TextBlock? _hDistanceText;
    private TextBlock? _vDistanceText;

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
        Unloaded += (_, _) => { HideControlName(adornedElement); HideDistanceGuides(); };
        UpdateSelectionVisual();
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
        var dx = dp.X;
        var dy = dp.Y;

        var isShiftPressed = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

        if (Math.Abs(dx) + Math.Abs(dy) > DragStartThreshold) _hasDragged = true;

        if (!isShiftPressed)
        {
            AdjustDeltaWithSnap(ref dx, ref dy);
        }

        if (_selectedStartPositions == null || _selectedStartPositions.Count == 0)
        {
            _selectedStartPositions = new();
            var left = double.IsNaN(Canvas.GetLeft(_adornedElement)) ? 0 : Canvas.GetLeft(_adornedElement);
            var top = double.IsNaN(Canvas.GetTop(_adornedElement)) ? 0 : Canvas.GetTop(_adornedElement);
            _selectedStartPositions[_adornedElement] = new Point(left, top);
        }

        foreach (var kv in _selectedStartPositions)
        {
            var element = kv.Key;
            var start = kv.Value;
            Canvas.SetLeft(element, start.X + dx);
            Canvas.SetTop(element, start.Y + dy);
            MoveControlName(element);
        }

        if (_selectedStartPositions.TryGetValue(_adornedElement, out var startPos))
        {
            UpdateDistanceGuides(startPos.X + dx, startPos.Y + dy);
        }
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

            if (!DesignBehavior.GetIsDesignerMode(element)) continue;

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
        _selectedStartPositions = null;

        var isCtrlPressed = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        if (isCtrlPressed && !_hasDragged)
        {
            if (!SelectedElements.Add(_adornedElement)) SelectedElements.Remove(_adornedElement);
            if (_adornedElement.Parent is Canvas c) RefreshSelectionVisuals(c);
        }
        HideDistanceGuides();
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
        _hasDragged = false;
        var isCtrlPressed = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        if (!isCtrlPressed)
        {
            SelectedElements.Clear();
            SelectedElements.Add(_adornedElement);
            UpdateSelectionVisual();
            if (_adornedElement.Parent is Canvas c) RefreshSelectionVisuals(c);
        }
        _selectedStartPositions = new();
        if (_adornedElement.Parent is Canvas parentCanvas)
        {
            foreach (var child in parentCanvas.Children)
            {
                if (child is FrameworkElement fe && SelectedElements.Contains(fe))
                {
                    var left = double.IsNaN(Canvas.GetLeft(fe)) ? 0 : Canvas.GetLeft(fe);
                    var top = double.IsNaN(Canvas.GetTop(fe)) ? 0 : Canvas.GetTop(fe);
                    _selectedStartPositions[fe] = new Point(left, top);
                }
            }
        }
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

        if (!isShiftPressed)
        {
            SnapResizeToNearest(ref left, ref top, ref width, ref height, thumb);
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

        MoveControlName(_adornedElement);
        UpdateDistanceGuides(left, top);
    }

    private void UpdateSelectionVisual()
    {
        _outLine.BorderBrush = SelectedElements.Contains(_adornedElement) ? Brushes.DeepSkyBlue : Brushes.Red;
    }

    private static void RefreshSelectionVisuals(Canvas canvas)
    {
        foreach (var child in canvas.Children)
        {
            if (child is FrameworkElement fe)
            {
                var layer = AdornerLayer.GetAdornerLayer(fe);
                var adorners = layer?.GetAdorners(fe);
                if (adorners == null) continue;
                foreach (var adorner in adorners)
                {
                    if (adorner is CanvasAdorner ca) ca.UpdateSelectionVisual();
                }
            }
        }
    }

    private void EnsureGuideElements(Canvas canvas)
    {
        if (_hGuideLine == null)
        {
            _hGuideLine = new Line
            {
                Stroke = Brushes.DeepSkyBlue,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 4 },
                IsHitTestVisible = false
            };
            Canvas.SetLeft(_hGuideLine, 0);
            Canvas.SetTop(_hGuideLine, 0);
            Panel.SetZIndex(_hGuideLine, 999);
            canvas.Children.Add(_hGuideLine);
        }
        if (_vGuideLine == null)
        {
            _vGuideLine = new Line
            {
                Stroke = Brushes.DeepSkyBlue,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 4 },
                IsHitTestVisible = false
            };
            Canvas.SetLeft(_vGuideLine, 0);
            Canvas.SetTop(_vGuideLine, 0);
            Panel.SetZIndex(_vGuideLine, 999);
            canvas.Children.Add(_vGuideLine);
        }
        if (_hDistanceText == null)
        {
            _hDistanceText = new TextBlock
            {
                Background = Brushes.LightGray,
                Foreground = Brushes.Black,
                Opacity = 0.8,
                Padding = new Thickness(2),
                IsHitTestVisible = false,
                FontSize = 12
            };
            Panel.SetZIndex(_hDistanceText, 1000);
            canvas.Children.Add(_hDistanceText);
        }
        if (_vDistanceText == null)
        {
            _vDistanceText = new TextBlock
            {
                Background = Brushes.LightGray,
                Foreground = Brushes.Black,
                Opacity = 0.8,
                Padding = new Thickness(2),
                IsHitTestVisible = false,
                FontSize = 12
            };
            Panel.SetZIndex(_vDistanceText, 1000);
            canvas.Children.Add(_vDistanceText);
        }
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    private void UpdateDistanceGuides(double left, double top)
    {
        var canvas = GetParentCanvas(_adornedElement);
        if (canvas == null) return;
        EnsureGuideElements(canvas);

        var width = _adornedElement.ActualWidth;
        var height = _adornedElement.ActualHeight;
        var r1Left = left;
        var r1Top = top;
        var r1Right = r1Left + width;
        var r1Bottom = r1Top + height;

        FrameworkElement? nearest = null;
        double bestDist = double.MaxValue;
        double bestHGap = 0;
        double bestVGap = 0;
        Point h1 = new Point();
        Point h2 = new Point();
        Point v1 = new Point();
        Point v2 = new Point();

        foreach (var child in canvas.Children)
        {
            if (child is not FrameworkElement other || other == _adornedElement) continue;
            if (!DesignBehavior.GetIsDesignerMode(other)) continue;

            var oLeft = Canvas.GetLeft(other);
            var oTop = Canvas.GetTop(other);
            var oRight = oLeft + other.ActualWidth;
            var oBottom = oTop + other.ActualHeight;

            double hGap;
            Point hx1, hx2;
            var overlapVTop = Math.Max(r1Top, oTop);
            var overlapVBottom = Math.Min(r1Bottom, oBottom);
            var midY = (r1Top + r1Bottom) / 2;

            if (r1Left >= oRight)
            {
                hGap = r1Left - oRight;
                var y = Clamp(midY, oTop, oBottom);
                hx1 = new Point(oRight, y);
                hx2 = new Point(r1Left, y);
            }
            else if (oLeft >= r1Right)
            {
                hGap = oLeft - r1Right;
                var y = Clamp(midY, oTop, oBottom);
                hx1 = new Point(r1Right, y);
                hx2 = new Point(oLeft, y);
            }
            else
            {
                hGap = 0;
                var y = overlapVTop <= overlapVBottom ? (overlapVTop + overlapVBottom) / 2 : midY;
                hx1 = new Point(r1Right, y);
                hx2 = new Point(r1Right, y);
            }

            double vGap;
            Point vx1, vx2;
            var overlapHLeft = Math.Max(r1Left, oLeft);
            var overlapHRight = Math.Min(r1Right, oRight);
            var midX = (r1Left + r1Right) / 2;

            if (r1Top >= oBottom)
            {
                vGap = r1Top - oBottom;
                var x = Clamp(midX, oLeft, oRight);
                vx1 = new Point(x, oBottom);
                vx2 = new Point(x, r1Top);
            }
            else if (oTop >= r1Bottom)
            {
                vGap = oTop - r1Bottom;
                var x = Clamp(midX, oLeft, oRight);
                vx1 = new Point(x, r1Bottom);
                vx2 = new Point(x, oTop);
            }
            else
            {
                vGap = 0;
                var x = overlapHLeft <= overlapHRight ? (overlapHLeft + overlapHRight) / 2 : midX;
                vx1 = new Point(x, r1Bottom);
                vx2 = new Point(x, r1Bottom);
            }

            var dist = Math.Sqrt(hGap * hGap + vGap * vGap);
            if (dist < bestDist)
            {
                bestDist = dist;
                nearest = other;
                bestHGap = hGap;
                bestVGap = vGap;
                h1 = hx1;
                h2 = hx2;
                v1 = vx1;
                v2 = vx2;
            }
        }

        if (nearest == null)
        {
            HideDistanceGuides();
            return;
        }

        var showH = bestHGap > 0;
        var showV = bestVGap > 0;

        if (_hGuideLine != null)
        {
            if (showH)
            {
                _hGuideLine.X1 = h1.X;
                _hGuideLine.Y1 = h1.Y;
                _hGuideLine.X2 = h2.X;
                _hGuideLine.Y2 = h2.Y;
                _hGuideLine.Visibility = Visibility.Visible;
            }
            else
            {
                _hGuideLine.Visibility = Visibility.Collapsed;
            }
        }

        if (_vGuideLine != null)
        {
            if (showV)
            {
                _vGuideLine.X1 = v1.X;
                _vGuideLine.Y1 = v1.Y;
                _vGuideLine.X2 = v2.X;
                _vGuideLine.Y2 = v2.Y;
                _vGuideLine.Visibility = Visibility.Visible;
            }
            else
            {
                _vGuideLine.Visibility = Visibility.Collapsed;
            }
        }

        if (_hDistanceText != null)
        {
            if (showH)
            {
                _hDistanceText.Text = Math.Round(bestHGap).ToString() + " px";
                var hxMidX = (h1.X + h2.X) / 2;
                var hxMidY = (h1.Y + h2.Y) / 2;
                Canvas.SetLeft(_hDistanceText, hxMidX - 20);
                Canvas.SetTop(_hDistanceText, hxMidY - 18);
                _hDistanceText.Visibility = Visibility.Visible;
            }
            else
            {
                _hDistanceText.Visibility = Visibility.Collapsed;
            }
        }

        if (_vDistanceText != null)
        {
            if (showV)
            {
                _vDistanceText.Text = Math.Round(bestVGap).ToString() + " px";
                var vxMidX = (v1.X + v2.X) / 2;
                var vxMidY = (v1.Y + v2.Y) / 2;
                Canvas.SetLeft(_vDistanceText, vxMidX + 4);
                Canvas.SetTop(_vDistanceText, vxMidY - 10);
                _vDistanceText.Visibility = Visibility.Visible;
            }
            else
            {
                _vDistanceText.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void HideDistanceGuides()
    {
        if (_hGuideLine != null) _hGuideLine.Visibility = Visibility.Collapsed;
        if (_vGuideLine != null) _vGuideLine.Visibility = Visibility.Collapsed;
        if (_hDistanceText != null) _hDistanceText.Visibility = Visibility.Collapsed;
        if (_vDistanceText != null) _vDistanceText.Visibility = Visibility.Collapsed;
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
            if (!DesignBehavior.GetIsDesignerMode(otherElement)) continue;

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

    private void SnapResizeToNearest(ref double left, ref double top, ref double width, ref double height, FrameworkElement? thumb)
    {
        if (_adornedElement.Parent is not Canvas parentCanvas) return;
        var originalLeft = double.IsNaN(Canvas.GetLeft(_adornedElement)) ? 0 : Canvas.GetLeft(_adornedElement);
        var originalTop = double.IsNaN(Canvas.GetTop(_adornedElement)) ? 0 : Canvas.GetTop(_adornedElement);
        var originalRight = originalLeft + _adornedElement.ActualWidth;
        var originalBottom = originalTop + _adornedElement.ActualHeight;

        double? snapX = null;
        double? snapY = null;

        foreach (var child in parentCanvas.Children)
        {
            if (child is not FrameworkElement element || element == _adornedElement) continue;
            if (!DesignBehavior.GetIsDesignerMode(element)) continue;

            var otherLeft = Canvas.GetLeft(element);
            var otherTop = Canvas.GetTop(element);
            var otherRight = otherLeft + element.ActualWidth;
            var otherBottom = otherTop + element.ActualHeight;

            if (thumb?.HorizontalAlignment == HorizontalAlignment.Left)
            {
                var d1 = otherLeft - left;
                var d2 = otherRight - left;
                if (Math.Abs(d1) <= SnapDistance && (snapX == null || Math.Abs(d1) < Math.Abs(snapX.Value))) snapX = d1;
                if (Math.Abs(d2) <= SnapDistance && (snapX == null || Math.Abs(d2) < Math.Abs(snapX.Value))) snapX = d2;
            }
            else if (thumb?.HorizontalAlignment == HorizontalAlignment.Right)
            {
                var right = left + width;
                var d1 = otherLeft - right;
                var d2 = otherRight - right;
                if (Math.Abs(d1) <= SnapDistance && (snapX == null || Math.Abs(d1) < Math.Abs(snapX.Value))) snapX = d1;
                if (Math.Abs(d2) <= SnapDistance && (snapX == null || Math.Abs(d2) < Math.Abs(snapX.Value))) snapX = d2;
            }

            if (thumb?.VerticalAlignment == VerticalAlignment.Top)
            {
                var d1 = otherTop - top;
                var d2 = otherBottom - top;
                if (Math.Abs(d1) <= SnapDistance && (snapY == null || Math.Abs(d1) < Math.Abs(snapY.Value))) snapY = d1;
                if (Math.Abs(d2) <= SnapDistance && (snapY == null || Math.Abs(d2) < Math.Abs(snapY.Value))) snapY = d2;
            }
            else if (thumb?.VerticalAlignment == VerticalAlignment.Bottom)
            {
                var bottom = top + height;
                var d1 = otherTop - bottom;
                var d2 = otherBottom - bottom;
                if (Math.Abs(d1) <= SnapDistance && (snapY == null || Math.Abs(d1) < Math.Abs(snapY.Value))) snapY = d1;
                if (Math.Abs(d2) <= SnapDistance && (snapY == null || Math.Abs(d2) < Math.Abs(snapY.Value))) snapY = d2;
            }
        }

        if (snapX.HasValue)
        {
            if (thumb?.HorizontalAlignment == HorizontalAlignment.Left)
            {
                left += snapX.Value;
                width = originalRight - left;
            }
            else if (thumb?.HorizontalAlignment == HorizontalAlignment.Right)
            {
                var right = left + width + snapX.Value;
                width = right - left;
            }
        }

        if (snapY.HasValue)
        {
            if (thumb?.VerticalAlignment == VerticalAlignment.Top)
            {
                top += snapY.Value;
                height = originalBottom - top;
            }
            else if (thumb?.VerticalAlignment == VerticalAlignment.Bottom)
            {
                var bottom = top + height + snapY.Value;
                height = bottom - top;
            }
        }
    }

    private void AdjustDeltaWithSnap(ref double dx, ref double dy)
    {
        if (_adornedElement.Parent is not Canvas parentCanvas) return;
        if (_selectedStartPositions == null || _selectedStartPositions.Count == 0) return;

        double groupLeft = double.MaxValue;
        double groupTop = double.MaxValue;
        double groupRight = double.MinValue;
        double groupBottom = double.MinValue;

        foreach (var kv in _selectedStartPositions)
        {
            var el = kv.Key;
            var start = kv.Value;
            var left = start.X;
            var top = start.Y;
            var right = left + el.ActualWidth;
            var bottom = top + el.ActualHeight;
            if (left < groupLeft) groupLeft = left;
            if (top < groupTop) groupTop = top;
            if (right > groupRight) groupRight = right;
            if (bottom > groupBottom) groupBottom = bottom;
        }

        double? snapDx = null;
        double? snapDy = null;

        foreach (var child in parentCanvas.Children)
        {
            if (child is not FrameworkElement element) continue;
            if (_selectedStartPositions.ContainsKey(element)) continue;
            if (!DesignBehavior.GetIsDesignerMode(element)) continue;

            var otherLeft = Canvas.GetLeft(element);
            var otherTop = Canvas.GetTop(element);
            var otherRight = otherLeft + element.ActualWidth;
            var otherBottom = otherTop + element.ActualHeight;

            var dLeft = otherLeft - (groupLeft + dx);
            var dRight = otherRight - (groupRight + dx);
            var dTop = otherTop - (groupTop + dy);
            var dBottom = otherBottom - (groupBottom + dy);

            if (Math.Abs(dLeft) <= SnapDistance && (snapDx == null || Math.Abs(dLeft) < Math.Abs(snapDx.Value))) snapDx = dLeft;
            if (Math.Abs(dRight) <= SnapDistance && (snapDx == null || Math.Abs(dRight) < Math.Abs(snapDx.Value))) snapDx = dRight;
            if (Math.Abs(dTop) <= SnapDistance && (snapDy == null || Math.Abs(dTop) < Math.Abs(snapDy.Value))) snapDy = dTop;
            if (Math.Abs(dBottom) <= SnapDistance && (snapDy == null || Math.Abs(dBottom) < Math.Abs(snapDy.Value))) snapDy = dBottom;
        }

        if (snapDx.HasValue) dx += snapDx.Value;
        if (snapDy.HasValue) dy += snapDy.Value;
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
