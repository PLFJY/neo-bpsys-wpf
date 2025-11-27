using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace neo_bpsys_wpf.Views.Windows;

public partial class RegionSelectorWindow : Window
{
    private Point _startPoint;
    private bool _isSelecting;
    private readonly List<Int32Rect> _regions = new();
    private int _stepIndex;

    public RegionSelectorWindow()
    {
        InitializeComponent();
        MouseLeftButtonDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseUp;
        StepText.Text = "框选求生者1";
    }

    public IReadOnlyList<Int32Rect> Regions => _regions;

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _isSelecting = true;
        _startPoint = e.GetPosition(this);
        SelectionRect.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionRect, _startPoint.X);
        Canvas.SetTop(SelectionRect, _startPoint.Y);
        SelectionRect.Width = 0;
        SelectionRect.Height = 0;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isSelecting) return;
        var p = e.GetPosition(this);
        var x = Math.Min(p.X, _startPoint.X);
        var y = Math.Min(p.Y, _startPoint.Y);
        var w = Math.Abs(p.X - _startPoint.X);
        var h = Math.Abs(p.Y - _startPoint.Y);
        Canvas.SetLeft(SelectionRect, x);
        Canvas.SetTop(SelectionRect, y);
        SelectionRect.Width = w;
        SelectionRect.Height = h;
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSelecting) return;
        _isSelecting = false;
        var left = Canvas.GetLeft(SelectionRect);
        var top = Canvas.GetTop(SelectionRect);
        var w = SelectionRect.Width;
        var h = SelectionRect.Height;
        SelectionRect.Visibility = Visibility.Collapsed;
        var dpi = VisualTreeHelper.GetDpi(this);
        var scaleX = dpi.DpiScaleX;
        var scaleY = dpi.DpiScaleY;
        var rect = new Int32Rect((int)(left * scaleX), (int)(top * scaleY), (int)(w * scaleX), (int)(h * scaleY));
        _regions.Add(rect);
        _stepIndex++;
        if (_stepIndex == 1) StepText.Text = "框选求生者2";
        else if (_stepIndex == 2) StepText.Text = "框选求生者3";
        else if (_stepIndex == 3) StepText.Text = "框选求生者4";
        else if (_stepIndex == 4) StepText.Text = "框选监管者";
        else Close();
    }
}
