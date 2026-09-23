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
/// 节点图编辑器的Nodes逻辑。
/// </summary>
public sealed partial class FrontedNodeGraphEditorViewModel
{
    /// <summary>
    /// 在默认插入位置附近添加目录中的节点。
    /// </summary>
    /// <param name="nodeType">节点类型标识。</param>
    [RelayCommand]
    public void AddNode(string? nodeType)
    {
        var nodeCount = Nodes.Count;
        AddNodeAt(nodeType, 40 + Nodes.Count * 20, 40 + Nodes.Count * 20);
        if (Nodes.Count == nodeCount)
        {
            return;
        }

        SelectedNodes.Clear();
        if (Nodes.Count > 0)
        {
            SelectedNodes.Add(Nodes[^1]);
        }
    }

    /// <summary>
    /// 在指定图画布位置添加目录中的节点。
    /// </summary>
    /// <param name="nodeType">节点类型标识。</param>
    /// <param name="x">画布 X 坐标。</param>
    /// <param name="y">画布 Y 坐标。</param>
    public void AddNodeAt(string? nodeType, double x, double y)
    {
        if (string.IsNullOrWhiteSpace(nodeType)
            || _catalog.Find(nodeType) is null
            || !CanAddNodeType(nodeType))
        {
            return;
        }

        CreateSnapshot();
        var model = _catalog.CreateNode(nodeType, Math.Max(0, x), Math.Max(0, y));
        Graph.Nodes.Add(model);
        var viewModel = CreateNode(model);
        Nodes.Add(viewModel);
        ClearIsSelected();
        SelectedNodes.Clear();
        SelectedNodes.Add(viewModel);
        viewModel.IsSelected = true;
        SelectedNode = viewModel;
        UpdateCanvasSize();
        Changed();
    }

    /// <summary>
    /// 在图为空时创建最小的开始到结束图。
    /// </summary>
    [RelayCommand]
    public void AutoCreateStartEnd()
    {
        if (Graph.Nodes.Count > 0)
        {
            return;
        }

        CreateSnapshot();
        var start = _catalog.CreateNode("flow.start", 60, 100);
        var end = _catalog.CreateNode("flow.end", 360, 100);
        Graph.Nodes.AddRange([start, end]);
        Graph.Connections.Add(new FrontedNodeConnection { SourceNodeId = start.NodeId, SourcePort = "Out", TargetNodeId = end.NodeId, TargetPort = "In" });
        Reload();
        UpdateCanvasSize();
        Changed();
    }

    /// <summary>
    /// 删除选中节点或多选节点。
    /// </summary>
    [RelayCommand]
    public void DeleteSelectedNode()
    {
        if (SelectedNodes.Count > 0)
        {
            DeleteNodes([.. SelectedNodes]);
        }
        else
        {
            DeleteNode(SelectedNode);
        }
    }

    /// <summary>
    /// 删除多个节点及其附加连接。
    /// </summary>
    /// <param name="nodes">要删除的节点视图模型。</param>
    public void DeleteNodes(IReadOnlyList<FrontedNodeEditorViewModel> nodes)
    {
        if (nodes.Count == 0)
        {
            return;
        }

        CreateSnapshot();
        foreach (var node in nodes)
        {
            if (Graph.RemoveNode(node.Model.NodeId))
            {
                Nodes.Remove(node);
            }
        }
        ClearIsSelected();
        SelectedNodes.Clear();
        SelectedNode = null;
        ReloadConnections();
        UpdateCanvasSize();
        Changed();
    }

    /// <summary>
    /// 删除单个节点及其附加连接。
    /// </summary>
    /// <param name="node">要删除的节点视图模型。</param>
    public void DeleteNode(FrontedNodeEditorViewModel? node)
    {
        if (node is null || !Graph.RemoveNode(node.Model.NodeId))
        {
            return;
        }

        Nodes.Remove(node);
        SelectedNodes.Remove(node);
        if (SelectedNode == node)
        {
            SelectedNode = null;
        }
        ReloadConnections();
        UpdateCanvasSize();
        Changed();
    }

    /// <summary>
    /// 复制选中节点或多选节点。
    /// </summary>
    [RelayCommand]
    public void DuplicateSelectedNode()
    {
        var nodesToClone = SelectedNodes.Count > 0
            ? SelectedNodes.ToList()
            : SelectedNode is not null ? [SelectedNode] : [];
        if (nodesToClone.Count == 0)
        {
            return;
        }

        var models = new List<FrontedNode>();
        foreach (var node in nodesToClone)
        {
            if (!CanAddNodeType(node.Model.NodeType))
            {
                continue;
            }

            var source = node.Model;
            models.Add(new FrontedNode
            {
                NodeType = source.NodeType,
                DisplayName = source.DisplayName,
                X = source.X + 30,
                Y = source.Y + 30,
                Properties = source.Properties.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal)
            });
        }

        if (models.Count == 0)
        {
            return;
        }

        CreateSnapshot();
        var clones = new List<FrontedNodeEditorViewModel>();
        foreach (var clone in models)
        {
            Graph.Nodes.Add(clone);
            var viewModel = CreateNode(clone);
            Nodes.Add(viewModel);
            clones.Add(viewModel);
        }

        ClearIsSelected();
        SelectedNodes.Clear();
        foreach (var clone in clones)
        {
            SelectedNodes.Add(clone);
            clone.IsSelected = true;
        }
        SelectedNode = clones.FirstOrDefault();
        UpdateCanvasSize();
        Changed();
    }

    /// <summary>
    /// 将选中节点及其内部连接复制到应用级图剪贴板。
    /// </summary>
    [RelayCommand]
    public void CopySelectedNodes()
    {
        var selected = SelectedNodes.Count > 0
            ? SelectedNodes.Select(item => item.Model).ToArray()
            : SelectedNode is null ? [] : [SelectedNode.Model];
        if (selected.Length == 0)
        {
            return;
        }

        var selectedIds = selected.Select(node => node.NodeId).ToHashSet();
        FrontedNodeGraphClipboard.Payload = new FrontedNodeGraphClipboardPayload
        {
            Nodes = DeepClone(selected),
            Connections = DeepClone(Graph.Connections
                .Where(connection => selectedIds.Contains(connection.SourceNodeId)
                                     && selectedIds.Contains(connection.TargetNodeId))
                .ToArray())
        };
    }

    /// <summary>
    /// 从应用级图剪贴板粘贴节点，并重新映射所有复制出的标识。
    /// </summary>
    [RelayCommand]
    public void PasteNodes()
    {
        var payload = FrontedNodeGraphClipboard.Payload;
        if (payload is null || payload.Version != 1 || payload.Nodes.Count == 0)
        {
            return;
        }

        var nodes = DeepClone(payload.Nodes);
        RemovePastedDuplicateSingletonFlowNodes(nodes);

        if (nodes.Count == 0)
        {
            return;
        }

        CreateSnapshot();
        var copiedIds = nodes.Select(node => node.NodeId).ToHashSet();
        var connections = DeepClone(payload.Connections)
            .Where(connection => copiedIds.Contains(connection.SourceNodeId)
                                 && copiedIds.Contains(connection.TargetNodeId))
            .ToList();
        var idMap = nodes.ToDictionary(node => node.NodeId, _ => FrontedBehaviorGuidHelper.NewGuid());
        foreach (var node in nodes)
        {
            node.NodeId = idMap[node.NodeId];
            node.X += 32;
            node.Y += 32;
        }

        foreach (var connection in connections)
        {
            connection.ConnectionId = FrontedBehaviorGuidHelper.NewGuid();
            connection.SourceNodeId = idMap[connection.SourceNodeId];
            connection.TargetNodeId = idMap[connection.TargetNodeId];
        }

        Graph.Nodes.AddRange(nodes);
        Graph.Connections.AddRange(connections);
        Reload();
        ClearIsSelected();
        SelectedNodes.Clear();
        foreach (var node in Nodes.Where(item => nodes.Any(pasted => pasted.NodeId == item.Model.NodeId)))
        {
            node.IsSelected = true;
            SelectedNodes.Add(node);
        }
        SelectedNode = SelectedNodes.FirstOrDefault();
        Changed();
    }

}
