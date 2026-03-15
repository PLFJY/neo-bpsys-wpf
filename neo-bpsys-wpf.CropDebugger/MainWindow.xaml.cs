using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Rect = System.Windows.Rect;

namespace neo_bpsys_wpf.CropDebugger;

public partial class MainWindow : Window
{
    private const double MinSelectionSize = 1;
    private const double HandleHalf = 5;

    private BitmapSource? _image;
    private bool _isDragging;
    private bool _isResizing;
    private bool _isMoving;
    private Point _dragStart;
    private Point _moveStart;
    private Rect _moveStartSelection;
    private Rect _selection;

    public MainWindow()
    {
        InitializeComponent();
        ResetSelectionView();
        AllowDrop = true;
        Drop += MainWindow_OnDrop;
    }

    private void LoadImageButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.webp|All Files|*.*",
            Title = "选择一张截图"
        };

        if (dialog.ShowDialog(this) == true)
        {
            LoadImage(dialog.FileName);
        }
    }

    private void ClearSelectionButton_OnClick(object sender, RoutedEventArgs e)
    {
        _selection = Rect.Empty;
        SelectionRect.Visibility = Visibility.Collapsed;
        ResetSelectionView();
    }

    private void CopyRelativeRectButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_image is null || _selection.IsEmpty || _selection.Width <= 0 || _selection.Height <= 0)
        {
            return;
        }

        Clipboard.SetText(RelativeRectCodeText.Text);
    }

    private void EditorCanvas_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_image is null || _isResizing || e.OriginalSource is Thumb)
        {
            return;
        }

        var pos = ClampToImageBounds(e.GetPosition(EditorCanvas));

        if (!_selection.IsEmpty && _selection.Width > 0 && _selection.Height > 0 && _selection.Contains(pos))
        {
            _isMoving = true;
            _moveStart = pos;
            _moveStartSelection = _selection;
            EditorCanvas.CaptureMouse();
            return;
        }

        _isDragging = true;
        _dragStart = pos;
        EditorCanvas.CaptureMouse();

        _selection = new Rect(_dragStart, _dragStart);
        UpdateSelectionRectVisual();
        UpdateSelectionText();
    }

    private void EditorCanvas_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_image is null || _isResizing)
        {
            return;
        }

        if (_isMoving)
        {
            var movePoint = ClampToImageBounds(e.GetPosition(EditorCanvas));
            var dx = movePoint.X - _moveStart.X;
            var dy = movePoint.Y - _moveStart.Y;

            var newX = _moveStartSelection.X + dx;
            var newY = _moveStartSelection.Y + dy;
            newX = Math.Clamp(newX, 0, _image.PixelWidth - _moveStartSelection.Width);
            newY = Math.Clamp(newY, 0, _image.PixelHeight - _moveStartSelection.Height);

            _selection = new Rect(newX, newY, _moveStartSelection.Width, _moveStartSelection.Height);
            UpdateSelectionRectVisual();
            UpdateSelectionText();
            return;
        }

        if (!_isDragging)
        {
            return;
        }

        var dragPoint = ClampToImageBounds(e.GetPosition(EditorCanvas));
        _selection = Normalize(_dragStart, dragPoint);
        UpdateSelectionRectVisual();
        UpdateSelectionText();
    }

    private void EditorCanvas_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isResizing)
        {
            return;
        }

        if (_isMoving)
        {
            _isMoving = false;
            EditorCanvas.ReleaseMouseCapture();
            UpdateSelectionRectVisual();
            UpdateSelectionText();
            return;
        }

        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        EditorCanvas.ReleaseMouseCapture();

        if (_selection.Width <= 0 || _selection.Height <= 0)
        {
            _selection = Rect.Empty;
            SelectionRect.Visibility = Visibility.Collapsed;
            ResetSelectionView();
            return;
        }

        UpdateSelectionRectVisual();
        UpdateSelectionText();
    }

    private void MainWindow_OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
        {
            return;
        }

        LoadImage(files[0]);
    }

    private void LoadImage(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path);
        bitmap.EndInit();
        bitmap.Freeze();

        _image = bitmap;
        SourceImage.Source = bitmap;
        SourceImage.Width = bitmap.PixelWidth;
        SourceImage.Height = bitmap.PixelHeight;
        EditorCanvas.Width = bitmap.PixelWidth;
        EditorCanvas.Height = bitmap.PixelHeight;

        ImageInfoText.Text = $"{System.IO.Path.GetFileName(path)} ({bitmap.PixelWidth} x {bitmap.PixelHeight})";

        _selection = Rect.Empty;
        SelectionRect.Visibility = Visibility.Collapsed;
        ResetSelectionView();
    }

    private Point ClampToImageBounds(Point p)
    {
        if (_image is null)
        {
            return p;
        }

        var x = Math.Clamp(p.X, 0, _image.PixelWidth);
        var y = Math.Clamp(p.Y, 0, _image.PixelHeight);
        return new Point(x, y);
    }

    private static Rect Normalize(Point a, Point b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        var w = Math.Abs(a.X - b.X);
        var h = Math.Abs(a.Y - b.Y);
        return new Rect(x, y, w, h);
    }

    private void UpdateSelectionRectVisual()
    {
        if (_selection.IsEmpty || _selection.Width <= 0 || _selection.Height <= 0)
        {
            SelectionRect.Visibility = Visibility.Collapsed;
            SetHandlesVisible(false);
            return;
        }

        SelectionRect.Visibility = Visibility.Visible;
        SelectionRect.Width = _selection.Width;
        SelectionRect.Height = _selection.Height;
        Canvas.SetLeft(SelectionRect, _selection.X);
        Canvas.SetTop(SelectionRect, _selection.Y);
        UpdateHandleLayout();
    }

    private void UpdateSelectionText()
    {
        if (_image is null || _selection.IsEmpty || _selection.Width <= 0 || _selection.Height <= 0)
        {
            ResetSelectionView();
            return;
        }

        var pxX = (int)Math.Round(_selection.X);
        var pxY = (int)Math.Round(_selection.Y);
        var pxW = (int)Math.Round(_selection.Width);
        var pxH = (int)Math.Round(_selection.Height);
        PixelRectText.Text = $"PixelRect: X={pxX}, Y={pxY}, W={pxW}, H={pxH}";

        var rx = _selection.X / _image.PixelWidth;
        var ry = _selection.Y / _image.PixelHeight;
        var rw = _selection.Width / _image.PixelWidth;
        var rh = _selection.Height / _image.PixelHeight;

        RelativeRectText.Text = $"Relative: X={rx:F6}, Y={ry:F6}, W={rw:F6}, H={rh:F6}";
        RelativeRectCodeText.Text =
            $"new RelativeRect({rx.ToString("F6", CultureInfo.InvariantCulture)}, {ry.ToString("F6", CultureInfo.InvariantCulture)}, {rw.ToString("F6", CultureInfo.InvariantCulture)}, {rh.ToString("F6", CultureInfo.InvariantCulture)})";
    }

    private void ResetSelectionView()
    {
        PixelRectText.Text = "PixelRect: -";
        RelativeRectText.Text = "Relative: -";
        RelativeRectCodeText.Text = "new RelativeRect(...)";
        SetHandlesVisible(false);
    }

    private void ResizeHandle_OnDragStarted(object sender, DragStartedEventArgs e)
    {
        _isResizing = true;
        _isDragging = false;
        _isMoving = false;
        EditorCanvas.ReleaseMouseCapture();
    }

    private void ResizeHandle_OnDragCompleted(object sender, DragCompletedEventArgs e)
    {
        _isResizing = false;
    }

    private void ResizeHandle_OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (_image is null || sender is not Thumb thumb || _selection.IsEmpty)
        {
            return;
        }

        var left = _selection.Left;
        var top = _selection.Top;
        var right = _selection.Right;
        var bottom = _selection.Bottom;

        var dx = e.HorizontalChange;
        var dy = e.VerticalChange;
        var tag = thumb.Tag as string;

        switch (tag)
        {
            case "TopLeft":
                left += dx;
                top += dy;
                break;
            case "Top":
                top += dy;
                break;
            case "TopRight":
                right += dx;
                top += dy;
                break;
            case "Left":
                left += dx;
                break;
            case "Right":
                right += dx;
                break;
            case "BottomLeft":
                left += dx;
                bottom += dy;
                break;
            case "Bottom":
                bottom += dy;
                break;
            case "BottomRight":
                right += dx;
                bottom += dy;
                break;
            default:
                return;
        }

        var imageWidth = _image.PixelWidth;
        var imageHeight = _image.PixelHeight;

        if (tag is "TopLeft" or "Left" or "BottomLeft")
        {
            left = Math.Clamp(left, 0, right - MinSelectionSize);
        }

        if (tag is "TopRight" or "Right" or "BottomRight")
        {
            right = Math.Clamp(right, left + MinSelectionSize, imageWidth);
        }

        if (tag is "TopLeft" or "Top" or "TopRight")
        {
            top = Math.Clamp(top, 0, bottom - MinSelectionSize);
        }

        if (tag is "BottomLeft" or "Bottom" or "BottomRight")
        {
            bottom = Math.Clamp(bottom, top + MinSelectionSize, imageHeight);
        }

        _selection = new Rect(new Point(left, top), new Point(right, bottom));
        UpdateSelectionRectVisual();
        UpdateSelectionText();
    }

    private void SetHandlesVisible(bool isVisible)
    {
        var visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        HandleTopLeft.Visibility = visibility;
        HandleTopCenter.Visibility = visibility;
        HandleTopRight.Visibility = visibility;
        HandleMiddleLeft.Visibility = visibility;
        HandleMiddleRight.Visibility = visibility;
        HandleBottomLeft.Visibility = visibility;
        HandleBottomCenter.Visibility = visibility;
        HandleBottomRight.Visibility = visibility;
    }

    private void UpdateHandleLayout()
    {
        SetHandlesVisible(true);

        var left = _selection.Left;
        var top = _selection.Top;
        var right = _selection.Right;
        var bottom = _selection.Bottom;
        var midX = (left + right) / 2;
        var midY = (top + bottom) / 2;

        SetHandlePosition(HandleTopLeft, left, top);
        SetHandlePosition(HandleTopCenter, midX, top);
        SetHandlePosition(HandleTopRight, right, top);
        SetHandlePosition(HandleMiddleLeft, left, midY);
        SetHandlePosition(HandleMiddleRight, right, midY);
        SetHandlePosition(HandleBottomLeft, left, bottom);
        SetHandlePosition(HandleBottomCenter, midX, bottom);
        SetHandlePosition(HandleBottomRight, right, bottom);
    }

    private static void SetHandlePosition(Thumb thumb, double x, double y)
    {
        Canvas.SetLeft(thumb, x - HandleHalf);
        Canvas.SetTop(thumb, y - HandleHalf);
    }
}
