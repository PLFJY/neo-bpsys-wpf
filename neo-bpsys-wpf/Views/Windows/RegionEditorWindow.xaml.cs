using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Core.Models;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// 通用识别区域编辑窗口。
/// 通过传入任意层级的 <see cref="RegionLayoutDefinition"/> 生成可编辑框，
/// 用户调整后再将结果作为布局结构返回给调用方。
/// </summary>
public partial class RegionEditorWindow : FluentWindow
{
    // 防止拖拽/缩放把框变成 0 尺寸，保证始终可见可操作。
    private const double MinSelectionSize = 1;
    // 控制点（8 个缩放点）半径的一半，用于居中摆放。
    private const double HandleHalf = 5;

    // 当前冻结帧（只预览单帧，不做实时刷新）。
    private readonly BitmapSource _frame;
    // 当前编辑中的布局副本。保存前不会修改调用方传入对象。
    private readonly RegionLayoutDefinition _layout;
    // 展平后的节点索引（树 -> 列表），用于下拉选择和绘制遍历。
    private readonly List<NodeRef> _nodeRefs = [];
    // 树形节点（用于左侧可折叠 UI）。
    private readonly List<TreeNodeRef> _treeNodeRefs = [];
    // 动态创建的覆盖层图形，刷新时统一移除后重建。
    private readonly List<Shape> _overlayShapes = [];

    private bool _isDragging;
    private bool _isResizing;
    private Point _moveStart;
    private Rect _moveStartSelection;
    private Rect _selection;

    private NodeRef? _selected;
    private double _canvasScale = 1.0;

    /// <summary>
    /// 用户点击“保存”后返回的布局结构；取消则保持 null。
    /// </summary>
    public RegionLayoutDefinition? ResultLayout { get; private set; }

    public RegionEditorWindow(BitmapSource frame, RegionLayoutDefinition layout)
    {
        InitializeComponent();
        _frame = frame;
        _layout = DeepClone(layout);

        Title = string.Format(ResolveLocalizedOrRaw("SmartBpRegionEditorSceneTitleFormat"), _layout.SceneDisplayName);
        HeaderText.Text = string.Format(ResolveLocalizedOrRaw("SmartBpRegionEditorSceneTitleFormat"), _layout.SceneDisplayName);

        SourceImage.Source = frame;
        SourceImage.Width = frame.PixelWidth;
        SourceImage.Height = frame.PixelHeight;
        EditorCanvas.Width = frame.PixelWidth;
        EditorCanvas.Height = frame.PixelHeight;
        ConfigureWindowScaleToScreen();

        BuildNodeRefIndex();
        NodeSelector.ItemsSource = _treeNodeRefs;
        if (_treeNodeRefs.Count == 0)
        {
            UpdateTemplateHint();
        }
        else
        {
            _selected = _treeNodeRefs[0].Ref;
            _selection = GetNodeGlobalRect(_selected);
            UpdateTemplateHint();
        }

        var count = _nodeRefs.Count;
        RuleText.Text = string.Format(ResolveLocalizedOrRaw("SmartBpRegionEditorRuleRuntimeFormat"), count);
        RenderAllOverlays();
    }

    /// <summary>
    /// 根据当前屏幕可用区域自动计算编辑画布缩放。
    /// 规则：只缩小不放大，尽量让编辑区域完整显示在窗口内，避免超出屏幕。
    /// </summary>
    private void ConfigureWindowScaleToScreen()
    {
        var workArea = SystemParameters.WorkArea;
        MaxWidth = workArea.Width;
        MaxHeight = workArea.Height;

        // 左侧信息栏 + 内外边距与窗口边框预估。
        const double leftPanelWidth = 360;
        const double horizontalChrome = 64;
        const double verticalChrome = 96;

        var availableEditorWidth = Math.Max(320, workArea.Width - leftPanelWidth - horizontalChrome);
        var availableEditorHeight = Math.Max(240, workArea.Height - verticalChrome);

        var scaleX = availableEditorWidth / _frame.PixelWidth;
        var scaleY = availableEditorHeight / _frame.PixelHeight;
        _canvasScale = Math.Min(1.0, Math.Min(scaleX, scaleY));
        EditorScaleTransform.ScaleX = _canvasScale;
        EditorScaleTransform.ScaleY = _canvasScale;

        var targetWidth = leftPanelWidth + (_frame.PixelWidth * _canvasScale) + horizontalChrome;
        var targetHeight = (_frame.PixelHeight * _canvasScale) + verticalChrome;
        Width = Math.Min(workArea.Width, Math.Max(980, targetWidth));
        Height = Math.Min(workArea.Height, Math.Max(720, targetHeight));
    }

    /// <summary>
    /// 将树状布局展平成列表，保留父子关系和层级信息。
    /// 这样可以支持“传结构 -> 自动生成可编辑框”的通用流程。
    /// </summary>
    private void BuildNodeRefIndex()
    {
        _nodeRefs.Clear();
        _treeNodeRefs.Clear();
        foreach (var root in _layout.Roots)
        {
            _treeNodeRefs.Add(BuildTree(root, null, 0));
        }
    }

    /// <summary>
    /// 深度优先遍历节点，同时构建“展平索引”和“树形展示项”。
    /// </summary>
    private TreeNodeRef BuildTree(RegionLayoutNode node, NodeRef? parent, int depth)
    {
        var templateMark = node.NodeType == RegionLayoutNodeType.TemplateItem && !string.IsNullOrWhiteSpace(node.TemplateGroupId)
            ? string.Format(ResolveLocalizedOrRaw("SmartBpRegionEditorTemplateMarkFormat"), node.TemplateGroupId)
            : string.Empty;
        var display = $"{node.Label} ({node.Id}){templateMark}";
        var current = new NodeRef(node, parent, depth, display);
        _nodeRefs.Add(current);
        var treeNode = new TreeNodeRef(current);
        foreach (var child in node.Children)
        {
            treeNode.Children.Add(BuildTree(child, current, depth + 1));
        }

        return treeNode;
    }

    private void NodeSelector_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not TreeNodeRef treeNode)
            return;

        _selected = treeNode.Ref;
        _selection = GetNodeGlobalRect(_selected);
        UpdateTemplateHint();
        RenderAllOverlays();
    }

    private void ApplyTemplateButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_selected == null)
            return;

        var sourceRoot = GetTemplateRoot(_selected);
        if (sourceRoot == null || string.IsNullOrWhiteSpace(sourceRoot.Node.TemplateGroupId))
            return;

        var groupId = sourceRoot.Node.TemplateGroupId;
        var targets = _nodeRefs
            .Where(n => n.Parent == null
                        && !ReferenceEquals(n, sourceRoot)
                        && n.Node.NodeType == RegionLayoutNodeType.TemplateItem
                        && string.Equals(n.Node.TemplateGroupId, groupId, StringComparison.Ordinal))
            .ToList();

        foreach (var target in targets)
        {
            // 大元素保留位置，仅应用尺寸（W/H）。
            target.Node.Rect = target.Node.Rect with { W = sourceRoot.Node.Rect.W, H = sourceRoot.Node.Rect.H };

            // 子元素按 Id 优先映射，缺失时按索引兜底，应用位置和尺寸（X/Y/W/H）。
            var sourceChildrenById = sourceRoot.Node.Children.ToDictionary(c => c.Id, c => c.Rect, StringComparer.Ordinal);
            for (var i = 0; i < target.Node.Children.Count; i++)
            {
                var targetChild = target.Node.Children[i];
                if (sourceChildrenById.TryGetValue(targetChild.Id, out var rect))
                {
                    targetChild.Rect = rect;
                    continue;
                }

                if (i < sourceRoot.Node.Children.Count)
                {
                    targetChild.Rect = sourceRoot.Node.Children[i].Rect;
                }
            }
        }

        if (_selected != null)
            _selection = GetNodeGlobalRect(_selected);
        RenderAllOverlays();
        UpdateTemplateHint(string.Format(ResolveLocalizedOrRaw("SmartBpRegionEditorTemplateAppliedFormat"), targets.Count));
    }

    /// <summary>
    /// 鼠标按下且命中选中框内部时，进入“整体拖动”模式。
    /// </summary>
    private void EditorCanvas_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isResizing || _selected == null)
            return;

        var pos = ClampToImageBounds(e.GetPosition(EditorCanvas));
        if (!_selection.Contains(pos))
            return;

        _isDragging = true;
        _moveStart = pos;
        _moveStartSelection = _selection;
        EditorCanvas.CaptureMouse();
    }

    /// <summary>
    /// 拖动过程中持续更新全局像素矩形，并在约束区域内夹紧。
    /// </summary>
    private void EditorCanvas_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_isResizing || !_isDragging || _selected == null)
            return;

        var movePoint = ClampToImageBounds(e.GetPosition(EditorCanvas));
        var dx = movePoint.X - _moveStart.X;
        var dy = movePoint.Y - _moveStart.Y;

        var moved = new Rect(
            _moveStartSelection.X + dx,
            _moveStartSelection.Y + dy,
            _moveStartSelection.Width,
            _moveStartSelection.Height);

        _selection = ClampSelection(moved, GetConstraintRect(_selected));
        CommitSelectionToNode(_selected, _selection);
        RenderAllOverlays();
    }

    private void EditorCanvas_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            EditorCanvas.ReleaseMouseCapture();
        }
    }

    private void ResizeHandle_OnDragStarted(object sender, DragStartedEventArgs e)
    {
        _isResizing = true;
        _isDragging = false;
        EditorCanvas.ReleaseMouseCapture();
    }

    private void ResizeHandle_OnDragCompleted(object sender, DragCompletedEventArgs e)
    {
        _isResizing = false;
    }

    private void ResizeHandle_OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (_selected == null || sender is not Thumb thumb)
            return;

        // 统一以 left/top/right/bottom 表示，便于处理 8 个方向缩放。
        var left = _selection.Left;
        var top = _selection.Top;
        var right = _selection.Right;
        var bottom = _selection.Bottom;
        var tag = thumb.Tag as string;

        switch (tag)
        {
            case "TopLeft":
                left += e.HorizontalChange;
                top += e.VerticalChange;
                break;
            case "Top":
                top += e.VerticalChange;
                break;
            case "TopRight":
                right += e.HorizontalChange;
                top += e.VerticalChange;
                break;
            case "Left":
                left += e.HorizontalChange;
                break;
            case "Right":
                right += e.HorizontalChange;
                break;
            case "BottomLeft":
                left += e.HorizontalChange;
                bottom += e.VerticalChange;
                break;
            case "Bottom":
                bottom += e.VerticalChange;
                break;
            case "BottomRight":
                right += e.HorizontalChange;
                bottom += e.VerticalChange;
                break;
            default:
                return;
        }

        // 每次缩放都在约束边界中做 clamp，并保证最小尺寸。
        var constraint = GetConstraintRect(_selected);
        left = Math.Clamp(left, constraint.Left, right - MinSelectionSize);
        top = Math.Clamp(top, constraint.Top, bottom - MinSelectionSize);
        right = Math.Clamp(right, left + MinSelectionSize, constraint.Right);
        bottom = Math.Clamp(bottom, top + MinSelectionSize, constraint.Bottom);

        _selection = new Rect(left, top, right - left, bottom - top);
        CommitSelectionToNode(_selected, _selection);
        RenderAllOverlays();
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        ResultLayout = _layout;
        DialogResult = true;
        Close();
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    /// <summary>
    /// 刷新叠加层：
    /// 1) 先重绘所有节点的半透明框（层级着色）；
    /// 2) 再绘制当前选中框和 8 个缩放点；
    /// 3) 同步右侧坐标文本。
    /// </summary>
    private void RenderAllOverlays()
    {
        foreach (var shape in _overlayShapes)
        {
            EditorCanvas.Children.Remove(shape);
        }

        _overlayShapes.Clear();

        foreach (var n in _nodeRefs)
        {
            var rect = GetNodeGlobalRect(n);
            var isSelected = ReferenceEquals(_selected, n);
            AddOverlayRect(rect, n.Depth, isSelected);
        }

        if (_selected == null)
        {
            SelectionRect.Visibility = Visibility.Collapsed;
            SetHandlesVisible(false);
            RectText.Text = ResolveLocalizedOrRaw("SmartBpRegionEditorSelectNodeFirst");
            UpdateTemplateHint();
            return;
        }

        SelectionRect.Visibility = Visibility.Visible;
        SelectionRect.Width = _selection.Width;
        SelectionRect.Height = _selection.Height;
        Canvas.SetLeft(SelectionRect, _selection.X);
        Canvas.SetTop(SelectionRect, _selection.Y);
        UpdateHandleLayout();
        RectText.Text = BuildRectText(_selected);
    }

    private NodeRef? GetTemplateRoot(NodeRef node)
    {
        var root = node;
        while (root.Parent != null)
        {
            root = root.Parent;
        }

        if (root.Node.NodeType != RegionLayoutNodeType.TemplateItem || string.IsNullOrWhiteSpace(root.Node.TemplateGroupId))
            return null;
        return root;
    }

    private void UpdateTemplateHint(string? overrideText = null)
    {
        if (!string.IsNullOrWhiteSpace(overrideText))
        {
            TemplateHintText.Text = overrideText;
            ApplyTemplateButton.IsEnabled = true;
            return;
        }

        if (_selected == null)
        {
            TemplateHintText.Text = ResolveLocalizedOrRaw("SmartBpRegionEditorTemplateNotInGroup");
            ApplyTemplateButton.IsEnabled = false;
            return;
        }

        var root = GetTemplateRoot(_selected);
        if (root == null)
        {
            TemplateHintText.Text = ResolveLocalizedOrRaw("SmartBpRegionEditorTemplateNotInGroup");
            ApplyTemplateButton.IsEnabled = false;
            return;
        }

        var sameGroupCount = _nodeRefs.Count(n =>
            n.Parent == null &&
            n.Node.NodeType == RegionLayoutNodeType.TemplateItem &&
            string.Equals(n.Node.TemplateGroupId, root.Node.TemplateGroupId, StringComparison.Ordinal));
        TemplateHintText.Text = string.Format(
            ResolveLocalizedOrRaw("SmartBpRegionEditorTemplateGroupSummaryFormat"),
            root.Node.TemplateGroupId,
            sameGroupCount);
        ApplyTemplateButton.IsEnabled = sameGroupCount > 1;
    }

    /// <summary>
    /// 输出当前选中节点坐标信息：
    /// 根节点显示“相对整帧”，子节点显示“相对父节点”。
    /// </summary>
    private string BuildRectText(NodeRef selected)
    {
        if (selected.Parent == null)
        {
            var rel = RelativeRect.FromPixelRect(
                _selection.X, _selection.Y, _selection.Width, _selection.Height, _frame.PixelWidth, _frame.PixelHeight);
            return string.Format(
                ResolveLocalizedOrRaw("SmartBpRegionEditorNodeRectRootFormat"),
                selected.Node.Label,
                rel.X,
                rel.Y,
                rel.W,
                rel.H);
        }

        var parentRect = GetNodeGlobalRect(selected.Parent);
        var relChild = RelativeRect.FromPixelRect(
            _selection.X - parentRect.X,
            _selection.Y - parentRect.Y,
            _selection.Width,
            _selection.Height,
            parentRect.Width,
            parentRect.Height);
        return string.Format(
            ResolveLocalizedOrRaw("SmartBpRegionEditorNodeRectChildFormat"),
            selected.Node.Label,
            relChild.X,
            relChild.Y,
            relChild.W,
            relChild.H);
    }

    /// <summary>
    /// 绘制一个只读叠加矩形，颜色随层级变化，选中态加亮。
    /// </summary>
    private void AddOverlayRect(Rect rect, int depth, bool isSelected)
    {
        var fill = depth % 2 == 0 ? Color.FromArgb(66, 59, 130, 246) : Color.FromArgb(66, 16, 185, 129);
        if (isSelected)
            fill = Color.FromArgb(130, 125, 211, 252);

        var stroke = isSelected ? "#F8FAFC" : (depth % 2 == 0 ? "#3B82F6" : "#10B981");
        var rectangle = new Rectangle
        {
            Width = rect.Width,
            Height = rect.Height,
            Fill = new SolidColorBrush(fill),
            Stroke = (Brush)new BrushConverter().ConvertFromString(stroke)!,
            StrokeThickness = isSelected ? 2 : 1,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(rectangle, rect.X);
        Canvas.SetTop(rectangle, rect.Y);
        Panel.SetZIndex(rectangle, isSelected ? 10 : 3 + depth);
        EditorCanvas.Children.Add(rectangle);
        _overlayShapes.Add(rectangle);
    }

    /// <summary>
    /// 获取节点可移动/可缩放约束区域。
    /// 如果节点声明 ClampToParent 且存在父节点，则限制在父节点内；
    /// 否则允许在整帧范围内编辑。
    /// </summary>
    private Rect GetConstraintRect(NodeRef node)
    {
        if (!node.Node.ClampToParent || node.Parent == null)
            return new Rect(0, 0, _frame.PixelWidth, _frame.PixelHeight);

        return GetNodeGlobalRect(node.Parent);
    }

    /// <summary>
    /// 递归换算节点全局像素矩形。
    /// 根节点坐标相对整帧；子节点坐标相对父节点矩形。
    /// </summary>
    private Rect GetNodeGlobalRect(NodeRef node)
    {
        if (node.Parent == null)
        {
            return new Rect(
                (node.Node.Rect.X / 100d) * _frame.PixelWidth,
                (node.Node.Rect.Y / 100d) * _frame.PixelHeight,
                (node.Node.Rect.W / 100d) * _frame.PixelWidth,
                (node.Node.Rect.H / 100d) * _frame.PixelHeight);
        }

        var parentRect = GetNodeGlobalRect(node.Parent);
        return new Rect(
            parentRect.X + parentRect.Width * (node.Node.Rect.X / 100d),
            parentRect.Y + parentRect.Height * (node.Node.Rect.Y / 100d),
            parentRect.Width * (node.Node.Rect.W / 100d),
            parentRect.Height * (node.Node.Rect.H / 100d));
    }

    /// <summary>
    /// 将编辑后的“全局像素矩形”写回节点模型。
    /// 根节点写回为相对整帧坐标，子节点写回为相对父节点坐标。
    /// </summary>
    private void CommitSelectionToNode(NodeRef node, Rect newRectGlobal)
    {
        if (node.Parent == null)
        {
            node.Node.Rect = RelativeRect.FromPixelRect(
                newRectGlobal.X,
                newRectGlobal.Y,
                newRectGlobal.Width,
                newRectGlobal.Height,
                _frame.PixelWidth,
                _frame.PixelHeight);
            return;
        }

        var parentGlobal = GetNodeGlobalRect(node.Parent);
        node.Node.Rect = RelativeRect.FromPixelRect(
            newRectGlobal.X - parentGlobal.X,
            newRectGlobal.Y - parentGlobal.Y,
            newRectGlobal.Width,
            newRectGlobal.Height,
            parentGlobal.Width,
            parentGlobal.Height);
    }

    /// <summary>
    /// 深拷贝布局，避免编辑过程中直接污染调用方原对象。
    /// </summary>
    private static RegionLayoutDefinition DeepClone(RegionLayoutDefinition layout)
    {
        return new RegionLayoutDefinition
        {
            SceneDisplayName = layout.SceneDisplayName,
            Roots = [.. layout.Roots.Select(CloneNode)]
        };
    }

    private static RegionLayoutNode CloneNode(RegionLayoutNode node)
    {
        return new RegionLayoutNode
        {
            Id = node.Id,
            Label = node.Label,
            NodeType = node.NodeType,
            TemplateGroupId = node.TemplateGroupId,
            Rect = node.Rect,
            ClampToParent = node.ClampToParent,
            Children = [.. node.Children.Select(CloneNode)]
        };
    }

    /// <summary>
    /// 缩放/移动后的矩形统一做边界与尺寸修正。
    /// </summary>
    private static Rect ClampSelection(Rect rect, Rect constraint)
    {
        // 浮点误差下可能出现 right-width 比 left 略小，先做安全归一化，避免 Math.Clamp 抛异常。
        var maxWidth = Math.Max(MinSelectionSize, constraint.Width);
        var maxHeight = Math.Max(MinSelectionSize, constraint.Height);
        var width = Math.Clamp(rect.Width, MinSelectionSize, maxWidth);
        var height = Math.Clamp(rect.Height, MinSelectionSize, maxHeight);

        var minX = constraint.X;
        var maxX = Math.Max(minX, constraint.Right - width);
        var minY = constraint.Y;
        var maxY = Math.Max(minY, constraint.Bottom - height);

        var x = Math.Clamp(rect.X, minX, maxX);
        var y = Math.Clamp(rect.Y, minY, maxY);
        return new Rect(x, y, width, height);
    }

    /// <summary>
    /// 鼠标点位限制在图像边界内，防止越界拖拽导致异常状态。
    /// </summary>
    private Point ClampToImageBounds(Point p)
    {
        return new Point(
            Math.Clamp(p.X, 0, _frame.PixelWidth),
            Math.Clamp(p.Y, 0, _frame.PixelHeight));
    }

    /// <summary>
    /// 根据当前选中矩形刷新 8 个缩放点位置。
    /// </summary>
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

    private static void SetHandlePosition(Thumb thumb, double x, double y)
    {
        Canvas.SetLeft(thumb, x - HandleHalf);
        Canvas.SetTop(thumb, y - HandleHalf);
    }

    private static string ResolveLocalizedOrRaw(string keyOrRawText)
    {
        var localized = I18nHelper.GetLocalizedString(keyOrRawText);
        var value = string.IsNullOrWhiteSpace(localized) ? keyOrRawText : localized;
        return value.Replace("`n", "\n");
    }

    /// <summary>
    /// 展平索引项：保存节点、父节点、层级和展示字符串。
    /// </summary>
    private sealed record NodeRef(RegionLayoutNode Node, NodeRef? Parent, int Depth, string Display);

    /// <summary>
    /// 左侧树形 UI 节点。
    /// </summary>
    private sealed class TreeNodeRef(NodeRef reference)
    {
        public string Display => reference.Display;

        public NodeRef Ref => reference;

        public List<TreeNodeRef> Children { get; } = [];
    }
}
