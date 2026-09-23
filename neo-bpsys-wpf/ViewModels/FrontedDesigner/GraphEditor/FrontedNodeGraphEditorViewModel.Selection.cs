using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Tutorial;

namespace neo_bpsys_wpf.ViewModels.FrontedDesigner.GraphEditor;

/// <summary>
/// 节点图编辑器的Selection逻辑。
/// </summary>
public sealed partial class FrontedNodeGraphEditorViewModel
{
    /// <summary>
    /// 选择单个节点，并在拖拽场景中保留多选状态。
    /// </summary>
    /// <param name="node">要选中的节点；传入 <see langword="null"/> 时清除选择。</param>
    [RelayCommand]
    public void SelectNode(FrontedNodeEditorViewModel? node)
    {
        SelectedNode = node;
        if (node is not null && !SelectedNodes.Contains(node))
        {
            // 点击不在多选中的节点：清空多选，单选该节点
            ClearIsSelected();
            SelectedNodes.Clear();
            SelectedNodes.Add(node);
            node.IsSelected = true;
        }
        else if (node is null)
        {
            ClearIsSelected();
            SelectedNodes.Clear();
        }
        // 如果 node 已在 SelectedNodes 中，保持多选（用于拖拽场景）
    }

    /// <summary>
    /// 通过更新可视选中标记预览框选结果，但不提交到 <see cref="SelectedNodes"/>。
    /// </summary>
    /// <param name="selectionRect">图画布坐标系中的选择矩形。</param>
    public void UpdateSelectionPreview(Rect selectionRect)
    {
        ClearIsSelected();
        foreach (var node in Nodes)
        {
            var nodeRect = new Rect(node.X, node.Y, node.CardWidth, node.CardHeight);
            if (selectionRect.IntersectsWith(nodeRect))
            {
                node.IsSelected = true;
            }
        }
    }

    /// <summary>
    /// 选中与选择矩形相交的所有节点。
    /// </summary>
    /// <param name="selectionRect">图画布坐标系中的选择矩形。</param>
    public void SelectNodes(Rect selectionRect)
    {
        ClearIsSelected();
        SelectedNodes.Clear();
        foreach (var node in Nodes)
        {
            var nodeRect = new Rect(node.X, node.Y, node.CardWidth, node.CardHeight);
            if (selectionRect.IntersectsWith(nodeRect))
            {
                SelectedNodes.Add(node);
                node.IsSelected = true;
            }
        }
        SelectedNode = SelectedNodes.FirstOrDefault();
    }

    /// <summary>
    /// 清除当前节点选择。
    /// </summary>
    [RelayCommand]
    public void DeselectAll()
    {
        ClearIsSelected();
        SelectedNode = null;
        SelectedNodes.Clear();
    }

    /// <summary>
    /// 清除每个节点的可视选中标记，但不改变已提交的选择集合。
    /// </summary>
    private void ClearIsSelected()
    {
        foreach (var node in Nodes)
        {
            node.IsSelected = false;
        }
    }

    /// <summary>
    /// 开始拖拽事务，并为整次拖拽创建一个撤销快照。
    /// </summary>
    public void BeginMoveNodes()
    {
        CreateSnapshot();
        _isDragging = true;
    }

    /// <summary>
    /// 移动节点；当被移动节点处于选中状态时移动整个多选集合。
    /// </summary>
    /// <param name="node">正在拖拽的节点。</param>
    /// <param name="x">拖拽节点的新 X 坐标。</param>
    /// <param name="y">拖拽节点的新 Y 坐标。</param>
    public void MoveNode(FrontedNodeEditorViewModel node, double x, double y)
    {
        var dx = x - node.X;
        var dy = y - node.Y;

        var nodesToMove = SelectedNodes.Contains(node) && SelectedNodes.Count > 1
            ? (IReadOnlyList<FrontedNodeEditorViewModel>)[.. SelectedNodes]
            : [node];

        foreach (var n in nodesToMove)
        {
            n.X = Math.Max(0, n.X + dx);
            n.Y = Math.Max(0, n.Y + dy);
            foreach (var connection in Connections.Where(connection => connection.Source == n || connection.Target == n))
            {
                connection.Refresh();
            }
        }
        UpdateCanvasSize();
        Changed();
    }

    /// <summary>
    /// 结束当前拖拽事务。
    /// </summary>
    public void EndMoveNodes()
    {
        _isDragging = false;
    }

}
