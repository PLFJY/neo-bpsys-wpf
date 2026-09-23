using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Services.FrontedDesigner;
using neo_bpsys_wpf.ViewModels.FrontedDesigner.GraphEditor;
using System.Collections.ObjectModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;

namespace neo_bpsys_wpf.ViewModels.FrontedDesigner;

/// <summary>
/// 行为面板的Localization逻辑。
/// </summary>
public sealed partial class BehaviorPanelViewModel
{
    public void RefreshLocalization()
    {
        foreach (var option in EventOptions)
        {
            option.Refresh();
        }

        foreach (var option in OperatorOptions)
        {
            option.Refresh();
        }

        foreach (var option in StopModeOptions)
        {
            option.Refresh();
        }

        foreach (var option in ReentryPolicyOptions)
        {
            option.Refresh();
        }

        foreach (var behavior in Behaviors)
        {
            behavior.RefreshLocalization();
        }

        OnPropertyChanged(nameof(EmptyText));
        OnPropertyChanged(nameof(GraphPlaceholder));
    }

    /// <summary>
    /// 重新生成克隆图中的所有节点和连接 ID，使其可与源行为共存。
    /// </summary>
    /// <param name="graph">要重写的图。</param>
    private static void RegenerateGraphIds(FrontedNodeGraph? graph)
    {
        if (graph is null)
        {
            return;
        }

        var nodeIds = new Dictionary<Guid, Guid>();
        foreach (var node in graph.Nodes)
        {
            var oldId = node.NodeId;
            node.NodeId = FrontedBehaviorGuidHelper.NewGuid();
            nodeIds[oldId] = node.NodeId;
        }

        foreach (var connection in graph.Connections)
        {
            connection.ConnectionId = FrontedBehaviorGuidHelper.NewGuid();
            if (nodeIds.TryGetValue(connection.SourceNodeId, out var sourceNodeId))
            {
                connection.SourceNodeId = sourceNodeId;
            }

            if (nodeIds.TryGetValue(connection.TargetNodeId, out var targetNodeId))
            {
                connection.TargetNodeId = targetNodeId;
            }
        }
    }
}
