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
/// 行为面板的Selection逻辑。
/// </summary>
public sealed partial class BehaviorPanelViewModel
{
    private ControlBehaviorSet? GetOrCreateSelectedSet()
    {
        if (SelectedControl is null)
        {
            return null;
        }

        if (SelectedControl.Config.BehaviorGuid == Guid.Empty)
        {
            SelectedControl.Config.BehaviorGuid = FrontedBehaviorGuidHelper.NewGuid();
            _markLayoutDirty();
        }

        _currentSet = CurrentDocument.GetOrCreateSet(SelectedControl.Config.BehaviorGuid, SelectedControl.Name);
        _currentSet.DisplayName = SelectedControl.Name;
        return _currentSet;
    }

    /// <summary>
    /// 为当前选中控件重新加载行为行和命令状态。
    /// </summary>
    private void RefreshForSelectedControl()
    {
        Behaviors.Clear();
        _currentSet = null;
        SelectedBehavior = null;

        if (SelectedControl?.Config.BehaviorGuid is not { } behaviorGuid || behaviorGuid == Guid.Empty)
        {
            OnPropertyChanged(nameof(HasBehaviors));
            OnPropertyChanged(nameof(EmptyText));
            return;
        }

        var set = CurrentDocument.FindSet(behaviorGuid);
        if (set is null)
        {
            OnPropertyChanged(nameof(HasBehaviors));
            OnPropertyChanged(nameof(EmptyText));
            return;
        }

        RefreshFromSet(set, set.Behaviors.FirstOrDefault());
    }

    /// <summary>
    /// 根据行为集合重建行为行，并选中请求的行为模型。
    /// </summary>
    /// <param name="set">要显示的行为集合。</param>
    /// <param name="selectedModel">应被选中的行为模型。</param>
    private void RefreshFromSet(ControlBehaviorSet set, FrontedBehavior? selectedModel)
    {
        _currentSet = set;
        Behaviors.Clear();
        foreach (var behavior in set.Behaviors)
        {
            Behaviors.Add(CreateBehaviorEditor(behavior));
        }

        SelectedBehavior = selectedModel is null
            ? null
            : Behaviors.FirstOrDefault(item => ReferenceEquals(item.Model, selectedModel));
        OnPropertyChanged(nameof(HasBehaviors));
        OnPropertyChanged(nameof(EmptyText));
    }

    /// <summary>
    /// 为行为模型创建行视图模型。
    /// </summary>
    /// <param name="behavior">行为模型。</param>
    /// <returns>行为编辑器行视图模型。</returns>
    private BehaviorEditorViewModel CreateBehaviorEditor(FrontedBehavior behavior)
    {
        return new BehaviorEditorViewModel(
            behavior,
            EventBusEventOptions,
            TransitionEventOptions,
            OperatorOptions,
            StopModeOptions,
            ReentryPolicyOptions,
            GraphPlaceholder,
            MarkBehaviorsDirty,
            CaptureUndoSnapshot,
            Localize,
            _nodeCatalog,
            _graphValidator,
            _graphRuntime,
            _animationRuntime,
            _previewAnimationScope,
            editor => AnimationEditorRequested?.Invoke(editor),
            CreateTargetOptions,
            saveBehaviorAsync: _saveBehaviorAsync,
            eventCatalog: _eventCatalog);
    }

    /// <summary>
    /// 根据选中控件及其生成的动画部件构建动画目标选项。
    /// </summary>
    /// <returns>节点图编辑器可用的目标选项。</returns>
    private IReadOnlyList<FrontedNodeTargetOptionViewModel> CreateTargetOptions()
    {
        var targets = new List<FrontedNodeTargetOptionViewModel>
        {
            new("Self", Localize("Designer.Graph.Target.Self", "Self"))
        };

        if (_previewAnimationScope is not null)
        {
            _previewAnimationScope.RefreshTargets();
            targets.AddRange(_previewAnimationScope.Targets.Select(target =>
                new FrontedNodeTargetOptionViewModel(
                    target.TargetReference,
                    CreateTargetDisplayName(target))));
        }

        return targets;
    }

    /// <summary>
    /// 为动画目标选项创建本地化显示名称。
    /// </summary>
    /// <param name="target">动画目标描述符。</param>
    /// <returns>目标选择器使用的显示名称。</returns>
    private string CreateTargetDisplayName(FrontedDesignerAnimationTargetOption target)
    {
        if (string.IsNullOrWhiteSpace(target.PartName))
        {
            return target.DisplayName;
        }

        var partDisplayName = Localize($"Designer.Graph.Target.{target.PartName}", target.PartName);
        return string.Format(
            Localize("Designer.Graph.Target.FormatPart", "{0}.{1}"),
            target.DisplayName,
            partDisplayName);
    }

    /// <summary>
    /// 根据行为事件目录创建触发事件选项。
    /// </summary>
    /// <param name="descriptor">事件描述符。</param>
    /// <returns>事件选项视图模型。</returns>
    private BehaviorEventOptionViewModel CreateEventOption(FrontedBehaviorEventDescriptor descriptor)
    {
        return new BehaviorEventOptionViewModel(
            descriptor.EventType,
            descriptor.DisplayNameKey,
            descriptor.CategoryDisplayNameKey,
            descriptor.DescriptionKey,
            string.IsNullOrWhiteSpace(descriptor.DisplayName) ? descriptor.EventType : descriptor.DisplayName,
            string.IsNullOrWhiteSpace(descriptor.CategoryDisplayName) ? descriptor.Category : descriptor.CategoryDisplayName,
            string.IsNullOrWhiteSpace(descriptor.Description) ? descriptor.EventType : descriptor.Description,
            descriptor.SupportedUsages,
            descriptor.PayloadFields.Select(field => new BehaviorPayloadFieldOptionViewModel(
                field.Path,
                field.DisplayNameKey,
                field.DescriptionKey,
                field.TypeName,
                string.IsNullOrWhiteSpace(field.DisplayName) ? field.Path : field.DisplayName,
                string.IsNullOrWhiteSpace(field.Description) ? field.Path : field.Description,
                field.EnumValues,
                false,
                field.IsCommonFilterTarget,
                Localize)).ToArray(),
            Localize);
    }

    private IReadOnlyList<BehaviorEventOptionViewModel> FilterEventOptions(FrontedBehaviorEventUsage usage) =>
        EventOptions.Where(option => (option.SupportedUsages & usage) != 0).ToArray();

    /// <summary>
    /// 创建本地化触发过滤运算符选项。
    /// </summary>
    /// <returns>运算符选项。</returns>
    private IReadOnlyList<BehaviorOptionViewModel> CreateOperatorOptions() =>
    [
        new(TriggerFilterOperator.Equals, "="),
        new(TriggerFilterOperator.NotEquals, "≠"),
        new(TriggerFilterOperator.GreaterThan, ">"),
        new(TriggerFilterOperator.LessThan, "<"),
        new(TriggerFilterOperator.GreaterThanOrEqual, "≥"),
        new(TriggerFilterOperator.LessThanOrEqual, "≤"),
        new(TriggerFilterOperator.Contains, "Designer.Behaviors.Operator.Contains", "contains", Localize),
        new(TriggerFilterOperator.NotContains, "Designer.Behaviors.Operator.NotContains", "does not contain", Localize),
        new(TriggerFilterOperator.Exists, "Designer.Behaviors.Operator.Exists", "exists", Localize)
    ];

    private IReadOnlyList<BehaviorOptionViewModel> CreateEnumOptions<TEnum>(string prefix)
        where TEnum : struct, Enum
    {
        return Enum.GetValues<TEnum>()
            .Select(value =>
            {
                var raw = value.ToString();
                return new BehaviorOptionViewModel(value, $"{prefix}.{raw}", raw, Localize);
            })
            .ToArray();
    }

    /// <summary>
    /// 将行为文档标记为已修改，并刷新粘贴/保存命令状态。
    /// </summary>
    private void MarkBehaviorsDirty()
    {
        _markBehaviorsDirty();
        OnPropertyChanged(nameof(HasBehaviors));
    }

    /// <summary>
    /// 在修改行为数据前捕获外层设计器撤销快照。
    /// </summary>
    private void CaptureUndoSnapshot()
    {
        _captureUndoSnapshot();
    }

    /// <summary>
    /// 解析设计器本地化字符串，并为仅加载 Core 的测试上下文提供兜底。
    /// </summary>
    /// <param name="key">本地化键。</param>
    /// <param name="fallback">兜底文本。</param>
    /// <returns>本地化文本或兜底文本。</returns>
    private string Localize(string key, string fallback) =>
        _localizationService.GetDesignerText(key, fallback);

    /// <summary>
    /// 刷新行为面板中的所有本地化显示字符串，以支持无需重启的热切换语言。
    /// </summary>
    /// <summary>
    /// 应用语言变化后刷新行为面板文本。
    /// </summary>
}
