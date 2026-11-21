# ModernFrame

`ModernFrame` 是项目本地的现代内容宿主，位于 `neo-bpsys-wpf/Controls/Modern/Frame/`，命名空间为 `neo_bpsys_wpf.Controls.Modern.Frame`。它参考了 iNKORE.UI.WPF.Modern 的 Frame / NavigationTransitionInfo 设计，但不依赖 iNKORE 包，也不导入完整控件库。

## 目标

- 为 `ModernNavigationView` Left 模式承载后台主导航页面。
- 为 `ModernNavigationView` Top 模式承载类标签页内容。
- 为 `PluginPage`、`FrontManagePage` 等局部内容切换提供带动画的轻量宿主。

当前已由 `ModernNavigationView` 在 `MainWindow.RootNavigation`、`PluginPage` 和 `FrontManagePage` 的局部标签页内部使用 `ModernFrame`。

## 内容与创建

`ModernFrame` 支持：

- `Navigate(Type pageType)` / `Navigate(Type pageType, object? parameter)`
- `Navigate(FrameworkElement content)`
- `Navigate(Func<FrameworkElement> contentFactory)`
- `Navigate(object content)`
- `GoBack()`、`ClearJournal()`、`CanGoBack`、`CurrentContent`

当通过 `Type` 导航时，`ModernFrame.ServiceProvider` 非空则优先从 DI 解析；解析不到时使用 `Activator.CreateInstance`。创建结果必须是 `FrameworkElement`，因此普通 WPF `Page`、`UserControl` 和其他 `FrameworkElement` 都可以承载，不要求继承 iNKORE Page。

`ModernFrame` 不主动覆盖内容的 `DataContext`。DI 创建的页面可以保留自身注入或设置的上下文；局部内容如果没有设置 `DataContext`，会按 WPF 视觉/逻辑树继承宿主上下文。

## 模板与 Page 承载

`ModernFrame` 是标准模板化控件，默认模板包含：

- `PART_Root`
- `PART_OldContentPresenter`
- `PART_NewContentPresenter`
- `PART_DirectContentPresenter`
- `PART_ContentScrollHost`

当前活动内容会挂到模板部件中，而不是通过 `AddVisualChild` / `AddLogicalChild` 手工维护根视觉树。这样可以保持父级资源查找、`DataContext` 继承、Loaded / Unloaded 行为和在其他自定义控件内部使用时的 WPF 常规行为。

WPF `Page` 不能直接作为普通 `ContentPresenter` 的子元素。`ModernFrame` 遇到 `Page` 时会创建内部 `ModernFramePageHost`，用 WPF `Frame` 作为合法承载容器，并在 host `Loaded` 后通过正常的 `Frame.Navigate(page)` 承载页面。外层 `ModernFrame` 仍负责导航日志和转场，`CurrentContent` 仍返回原始 `Page` 实例。

## 转场

本地转场类型包括：

- `EntranceNavigationTransitionInfo`
- `FadeNavigationTransitionInfo`
- `SlideNavigationTransitionInfo`
- `SuppressNavigationTransitionInfo`

`DefaultTransitionInfo` 默认使用 Entrance；单次 `Navigate` 可以传入转场覆盖。`IsAnimationEnabled`、`TransitionDuration`、`SystemParameters.ClientAreaAnimation` 会共同决定是否播放动画。快速连续导航会先停止当前转场并释放旧内容引用，再进入下一次导航。

`ModernFrame.TransitionDuration` 的类型是 `TimeSpan`，XAML 需使用 `0:0:0.333` 这样的明确时长格式；纯数字会被解析为天数。它不同于以毫秒整数表示的 `ModernNavigationView.TransitionDuration`。

切换已有内容时，等待目标内容 Loaded 后，在 Loaded dispatcher 阶段同时启动退场和入场 Storyboard，由关键帧表达先退场再入场；不等待 ApplicationIdle。默认 Entrance 总时长为 450 ms（150 ms 退场、300 ms 入场），位移 140 DIP；Fade 使用相同分段，横向 Slide 使用 150/300 ms、退出 150 DIP/进入 200 DIP。纵向 Slide 按参考时间线使用 250 ms 等待、350 ms 入场、200 DIP 和指数缓动，其时间随入口时长同比缩放。软件渲染不主动取消导航动画。

动画期间新旧内容不可命中，导航本身仍可打断。每次导航以代次隔离待执行调度和完成回调；取消、卸载及模板重建解绑 Loaded/Completed，释放旧 PageHost，并移除其待执行导航。完成后恢复活动宿主的可见性、命中及 identity transform。`SuppressNavigationTransitionInfo` 和关闭动画仍立即交换内容。

`Navigated` 保持原有同步时序；新增 `NavigationCompleted` 仅在内容 Loaded 且转场完成后引发，首次及无动画导航同样等待内容就绪。被后续导航或卸载取消的导航不发布完成事件。非关键首次加载可以通过 `BackendPresentation` 等待嵌套宿主均完成；它核对实际视觉树和当前内容归属，避免 WPF 延后的 Unloaded 通知使已离页视图启动查询。导航调试 Trace 输出包含内容就绪至转场开始的阶段耗时。

## 默认滚动宿主

`ModernFrame` 默认用 `ModernScrollViewer` 包裹当前活动内容，避免每个后台页面都重复定义外层滚动容器。活动内容在视觉树中位于该 `ModernScrollViewer` 下，因此 `ScrollViewerSearchHelper.FindNearestScrollableAncestor(target)` 可以从页面内目标找到 frame 拥有的滚动宿主。

`Page` 经过内部 `Frame` 承载时，页面加载会经过 WPF dispatcher；需要在页面 Loaded 后或使用现有引导滚动重试机制查找目标。加载完成后，`GuidanceScrollHelper` 可以从页面内容发现 `ModernFrame` 的 `ModernScrollViewer` 滚动宿主。

`ResetScrollOnNavigation` 默认为 `true`。新页面导航会在新内容挂到活动宿主后立即把 frame 级滚动宿主重置到顶部，并在重置前取消该 `ModernScrollViewer` 上已有的纵向滚动动画。该重置只作用于 `PART_ContentScrollHost`；如果当前内容使用直接 presenter，`ModernFrame` 不会递归查找或重置页面内部的 `ListView`、`ListBox`、`ComboBox`、`ScrollViewer` 或显式 self-scroll 区域。

重置发生在 `Navigated` 事件之前，不使用 `Dispatcher.BeginInvoke`、timer 或转场完成回调。这样 `GameGuidanceService` 导航后再发送 `HighlightMessage` 时，`GuidanceAutoScrollScope` 后续触发的目标滚动仍然可以覆盖 frame 的初始置顶。`GoBack()` 当前不执行置顶，避免返回上一页时强制丢失用户所在位置。

`IsContentScrollHostEnabled` 是兼容性开关，设为 `false` 时会完全跳过外层 `ModernScrollViewer`，改用直接 presenter 承载内容。

`ContentScrollHostMode` 用于控制默认滚动宿主策略：

```csharp
public enum ModernFrameContentScrollHostMode
{
    Enabled,
    Disabled,
    Auto
}
```

- `Enabled` 是默认值，保持既有后台页面行为，使用外层 `ModernScrollViewer`。
- `Disabled` 使用直接 presenter，不创建外层滚动测量。
- `Auto` 保留外层 `ModernScrollViewer` 作为默认页面滚动宿主。只有内容根或承载根显式声明 `ModernScroll.Ownership="Self"` 时，才使用直接 presenter。

滚动归属是显式契约，不再按控件类型推断。普通 `ListView`、`ListBox`、`DataGrid`、`TreeView` 或 WPF-UI `DynamicScrollViewer` 出现在页面中，并不表示该列表一定拥有滚动；很多列表是非约束高度的页面内容，应继续由外层 frame/page 滚动。

可用 `ModernScroll.Ownership` 标记归属：

```xml
scrolling:ModernScroll.Ownership="Frame"
scrolling:ModernScroll.Ownership="Self"
```

`Frame` 强制保持外层 frame/page 滚动；`Self` 表示该区域拥有自己的滚动语义。显式 self-scroll 的控件仍应通过 `NestedSmoothScrollBehavior.IsEnabled="True"` 获得平滑滚动。frame 级 `ModernScrollViewer` 只处理页面级滚轮；滚轮来源位于已打开的 `ComboBox` 下拉框、`Popup` / `ContextMenu`、`PopupRoot` 或显式 self-scroll 区域时，frame 不会在预览环节抢占该事件，也不会滚动背后的页面。

滚轮策略和嵌套交接以[平滑滚动](modern-smooth-scrolling.md)为准；本文只规定内容宿主选择与导航置顶时序。源码入口：[ModernFrame.cs](../../neo-bpsys-wpf/Controls/Modern/Frame/ModernFrame.cs)。
