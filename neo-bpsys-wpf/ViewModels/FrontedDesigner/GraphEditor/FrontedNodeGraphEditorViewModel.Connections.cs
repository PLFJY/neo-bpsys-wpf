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
/// 节点图编辑器的Connections逻辑。
/// </summary>
public sealed partial class FrontedNodeGraphEditorViewModel
{
    /// <summary>
    /// 从输出或输入端口开始一个待完成连接。
    /// </summary>
    /// <param name="port">连接手势开始的端口。</param>
    [RelayCommand]
    public void StartConnection(FrontedNodePortViewModel? port)
    {
        if (port is null)
        {
            return;
        }

        _pendingPort = port;
        IsConnecting = true;
        ApplyPortHighlights(port);
    }

    /// <summary>
    /// 在兼容目标端口上完成待完成连接。
    /// </summary>
    /// <param name="port">连接手势结束的端口。</param>
    [RelayCommand]
    public void CompleteConnection(FrontedNodePortViewModel? port)
    {
        if (_pendingPort is null || port is null)
        {
            return;
        }

        if (TryNormalizeConnection(_pendingPort, port, out var source, out var target)
            && IsTargetAvailable(source, target))
        {
            AddConnection(source, target);
        }
        else
        {
            var message = _localize("Designer.Graph.Connection.IncompatibleTypes", "Incompatible port types, cannot connect.");
            ExecutionLog.Add(new FrontedGraphExecutionLogItem
            {
                Level = FrontedGraphExecutionLogLevel.Warning,
                Message = $"[{port.Node.DisplayName}.{port.Name}] {message}"
            });
        }

        ClearPortHighlights();
        _pendingPort = null;
        IsConnecting = false;
    }

    /// <summary>
    /// 在两个兼容端口之间添加或替换图连接。
    /// </summary>
    /// <param name="source">候选源端口。</param>
    /// <param name="target">候选目标端口。</param>
    /// <returns><see langword="true"/> when the connection was added.</returns>
    public bool AddConnection(FrontedNodePortViewModel source, FrontedNodePortViewModel target)
    {
        if (!TryNormalizeConnection(source, target, out source, out target)
            || !IsTargetAvailable(source, target))
        {
            return false;
        }

        CreateSnapshot();
        var replaced = Graph.GetOutgoing(source.Node.Model.NodeId, source.Descriptor.Name).ToArray();
        foreach (var connection in replaced)
        {
            Graph.Connections.Remove(connection);
        }
        var model = new FrontedNodeConnection
        {
            SourceNodeId = source.Node.Model.NodeId,
            SourcePort = source.Descriptor.Name,
            TargetNodeId = target.Node.Model.NodeId,
            TargetPort = target.Descriptor.Name
        };
        Graph.Connections.Add(model);
        ReloadConnections();
        RefreshPortConnectionStates();
        Changed();
        return true;
    }

    /// <summary>
    /// 从图中删除连接。
    /// </summary>
    /// <param name="connection">要删除的连接。</param>
    [RelayCommand]
    public void DeleteConnection(FrontedNodeConnectionViewModel? connection)
    {
        if (connection is null || !Graph.Connections.Remove(connection.Model))
        {
            return;
        }
        CreateSnapshot();
        Connections.Remove(connection);
        RefreshPortConnectionStates();
        Changed();
    }

    /// <summary>
    /// 根据图连接列表刷新每个端口的连接状态。
    /// </summary>
    public void RefreshPortConnectionStates()
    {
        foreach (var node in Nodes)
        {
            foreach (var port in node.InputPorts)
            {
                port.IsConnected = Graph.Connections.Any(c =>
                    c.TargetNodeId == node.Model.NodeId && c.TargetPort == port.Name);
            }

            foreach (var port in node.OutputPorts)
            {
                port.IsConnected = Graph.Connections.Any(c =>
                    c.SourceNodeId == node.Model.NodeId && c.SourcePort == port.Name);
            }
        }

        foreach (var node in Nodes)
        {
            node.RefreshExternalInputStates();
        }
    }

    /// <summary>
    /// 用户拖拽连接时应用兼容/不兼容的可视状态。
    /// </summary>
    /// <param name="pendingPort">当前正在连接的端口。</param>
    public void ApplyPortHighlights(FrontedNodePortViewModel? pendingPort)
    {
        foreach (var node in Nodes)
        {
            foreach (var port in node.InputPorts)
            {
                var compatible = pendingPort is not null
                    && TryNormalizeConnection(pendingPort, port, out var source, out var target)
                    && IsTargetAvailable(source, target);
                port.IsHighlighted = compatible;
                port.IsDimmed = pendingPort is not null && !compatible;
            }

            foreach (var port in node.OutputPorts)
            {
                var compatible = pendingPort is not null
                    && TryNormalizeConnection(pendingPort, port, out var source, out var target)
                    && IsTargetAvailable(source, target);
                port.IsHighlighted = compatible;
                port.IsDimmed = pendingPort is not null && !compatible;
            }
        }
    }

    /// <summary>
    /// 清除所有端口上的连接高亮和弱化状态。
    /// </summary>
    public void ClearPortHighlights()
    {
        foreach (var node in Nodes)
        {
            foreach (var port in node.InputPorts)
            {
                port.IsHighlighted = false;
                port.IsDimmed = false;
            }

            foreach (var port in node.OutputPorts)
            {
                port.IsHighlighted = false;
                port.IsDimmed = false;
            }
        }
    }

    /// <summary>
    /// 取消当前连接手势，并重置连接模式 UI 状态。
    /// </summary>
    public void CancelConnection()
    {
        ClearPortHighlights();
        _pendingPort = null;
        IsConnecting = false;
    }

}
