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

public sealed record FrontedBehaviorAnimationStageViewModel(
    string DisplayName,
    FrontedNodeGraph Graph,
    FrontedNodeGraphEditorViewModel GraphEditor);

/// <summary>
/// 请求行为面板视图显示多目标行为粘贴选择器。
/// </summary>
/// <param name="Panel">拥有本次粘贴操作的行为面板。</param>
/// <param name="Previews">可用目标的兼容性与重写预览。</param>
public sealed record FrontedBehaviorCopyToRequest(
    BehaviorPanelViewModel Panel,
    IReadOnlyList<FrontedBehaviorPastePreview> Previews);
