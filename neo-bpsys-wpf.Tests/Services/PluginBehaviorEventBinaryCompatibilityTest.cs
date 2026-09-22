using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.StyleTransfer;
using neo_bpsys_wpf.Services.FrontedDesigner;
using neo_bpsys_wpf.ViewModels.FrontedDesigner;
using neo_bpsys_wpf.ViewModels.FrontedDesigner.GraphEditor;
using neo_bpsys_wpf.ViewModels.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// 验证插件行为事件功能没有移除 3.0/3.1 已发布的 CLR 构造函数签名。
/// </summary>
public sealed class PluginBehaviorEventBinaryCompatibilityTest
{
    /// <summary>
    /// 验证事件目录同时保留无参构造函数和支持注册集合的新构造函数。
    /// </summary>
    [Fact]
    public void FrontedBehaviorEventCatalogRetainsOldAndNewConstructorSignatures()
    {
        Assert.NotNull(typeof(FrontedBehaviorEventCatalog).GetConstructor(Type.EmptyTypes));
        Assert.NotNull(typeof(FrontedBehaviorEventCatalog).GetConstructor(
            [typeof(IEnumerable<FrontedBehaviorEventRegistration>)]));
    }

    /// <summary>
    /// 验证布局包导入器保留两个旧构造函数，并继续公开带事件目录的新构造函数。
    /// </summary>
    [Fact]
    public void FrontedLayoutPackageImporterRetainsOldAndNewConstructorSignatures()
    {
        var oldDefaultPathTypes = new[]
        {
            typeof(IFrontedLayoutPackageManager),
            typeof(ILogger<FrontedLayoutPackageImporter>),
            typeof(IFrontedV3ControlRegistry),
            typeof(IFrontedPluginMetadataProvider)
        };
        var oldCustomPathTypes = new[]
        {
            typeof(string),
            typeof(string),
            typeof(IFrontedLayoutPackageManager),
            typeof(ILogger<FrontedLayoutPackageImporter>),
            typeof(IFrontedV3ControlRegistry),
            typeof(IFrontedPluginMetadataProvider)
        };

        Assert.NotNull(typeof(FrontedLayoutPackageImporter).GetConstructor(oldDefaultPathTypes));
        Assert.NotNull(typeof(FrontedLayoutPackageImporter).GetConstructor(oldCustomPathTypes));
        Assert.NotNull(typeof(FrontedLayoutPackageImporter).GetConstructor(
            [.. oldDefaultPathTypes, typeof(FrontedBehaviorEventCatalog)]));
        Assert.NotNull(typeof(FrontedLayoutPackageImporter).GetConstructor(
            [.. oldCustomPathTypes, typeof(FrontedBehaviorEventCatalog)]));
    }

    /// <summary>
    /// 验证设计器视图模型同时保留旧生产构造函数和注入事件目录的新构造函数。
    /// </summary>
    [Fact]
    public void FrontedDesignerWindowViewModelRetainsOldAndNewConstructorSignatures()
    {
        var oldTypes = new[]
        {
            typeof(FrontedDesignerLayoutCatalog),
            typeof(IFrontedLayoutService),
            typeof(FrontedLayoutDesignConverter),
            typeof(FrontedLayoutValidator),
            typeof(FrontedLayoutReferenceScanner),
            typeof(FrontedPropertyGridBuilder),
            typeof(FrontedControlDefaultConfigFactory),
            typeof(FrontedControlNameGenerator),
            typeof(IFrontedDesignerLocalizationService),
            typeof(DesignerPreviewSharedDataService),
            typeof(IFrontedLocalResourceStore),
            typeof(IFrontedWindowLayoutOptionsService),
            typeof(IFrontedLayoutPackageManager),
            typeof(IFrontedWindowService),
            typeof(IFrontedBehaviorService),
            typeof(IFrontedBehaviorClipboard),
            typeof(FrontedBehaviorCopyPasteService),
            typeof(IFrontedAnimationRuntime),
            typeof(FrontedDesignerPreviewAnimationScope),
            typeof(ILogger<FrontedDesignerWindowViewModel>),
            typeof(ISettingsHostService),
            typeof(IFrontedV3ControlRegistry),
            typeof(FrontedV3StyleTransferService),
            typeof(IFrontedImageSafetyService)
        };
        var newTypes = oldTypes.ToList();
        newTypes.Insert(15, typeof(FrontedBehaviorEventCatalog));

        var oldConstructor = typeof(FrontedDesignerWindowViewModel).GetConstructor(oldTypes);
        var newConstructor = typeof(FrontedDesignerWindowViewModel).GetConstructor([.. newTypes]);

        Assert.NotNull(oldConstructor);
        Assert.DoesNotContain(
            oldConstructor.GetParameters(),
            parameter => parameter.ParameterType == typeof(FrontedBehaviorEventCatalog));
        Assert.NotNull(newConstructor);
    }

    /// <summary>
    /// 验证行为编辑器相关公开视图模型保留插件行为事件功能引入前的构造函数签名。
    /// </summary>
    [Fact]
    public void BehaviorEditorViewModelsRetainOldAndNewConstructorSignatures()
    {
        var oldEventOptionTypes = new[]
        {
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(IReadOnlyList<BehaviorPayloadFieldOptionViewModel>),
            typeof(Func<string, string, string>)
        };
        var newEventOptionTypes = new[]
        {
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(FrontedBehaviorEventUsage),
            typeof(IReadOnlyList<BehaviorPayloadFieldOptionViewModel>),
            typeof(Func<string, string, string>),
            typeof(bool)
        };
        var oldPayloadFieldTypes = new[]
        {
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(IReadOnlyList<string>),
            typeof(bool),
            typeof(bool),
            typeof(Func<string, string, string>)
        };
        var newPayloadFieldTypes = new[]
        {
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(IReadOnlyList<string>),
            typeof(bool),
            typeof(bool),
            typeof(Func<string, string, string>)
        };
        var oldEditorTypes = new[]
        {
            typeof(FrontedBehavior),
            typeof(IReadOnlyList<BehaviorEventOptionViewModel>),
            typeof(IReadOnlyList<BehaviorOptionViewModel>),
            typeof(IReadOnlyList<BehaviorOptionViewModel>),
            typeof(IReadOnlyList<BehaviorOptionViewModel>),
            typeof(string),
            typeof(Action),
            typeof(Action),
            typeof(Func<string, string, string>),
            typeof(FrontedNodeCatalog),
            typeof(FrontedNodeGraphValidator),
            typeof(IFrontedNodeGraphRuntime),
            typeof(IFrontedAnimationRuntime),
            typeof(FrontedDesignerPreviewAnimationScope),
            typeof(Action<FrontedBehaviorAnimationEditorViewModel>),
            typeof(Func<IReadOnlyList<FrontedNodeTargetOptionViewModel>>),
            typeof(Func<Task<bool>>)
        };
        var newEditorTypes = oldEditorTypes.ToList();
        newEditorTypes.Insert(2, typeof(IReadOnlyList<BehaviorEventOptionViewModel>));
        newEditorTypes.Add(typeof(FrontedBehaviorEventCatalog));

        Assert.NotNull(typeof(BehaviorEventOptionViewModel).GetConstructor(oldEventOptionTypes));
        Assert.NotNull(typeof(BehaviorEventOptionViewModel).GetConstructor(newEventOptionTypes));
        Assert.NotNull(typeof(BehaviorPayloadFieldOptionViewModel).GetConstructor(oldPayloadFieldTypes));
        Assert.NotNull(typeof(BehaviorPayloadFieldOptionViewModel).GetConstructor(newPayloadFieldTypes));
        Assert.NotNull(typeof(BehaviorEditorViewModel).GetConstructor(oldEditorTypes));
        Assert.NotNull(typeof(BehaviorEditorViewModel).GetConstructor([.. newEditorTypes]));
    }

    /// <summary>
    /// 验证示例插件页面视图模型保留原有无参构造函数，并继续公开 Publisher 注入构造函数。
    /// </summary>
    [Fact]
    public void ExamplePluginMainPageViewModelRetainsOldAndNewConstructorSignatures()
    {
        var viewModelType = typeof(ExamplePlugin.ViewModels.MainPageViewModel);

        Assert.NotNull(viewModelType.GetConstructor(Type.EmptyTypes));
        Assert.NotNull(viewModelType.GetConstructor(
            [typeof(IFrontedBehaviorEventPublisher<ExamplePlugin.ExamplePlugin>)]));
    }
}
