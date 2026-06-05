# ModernFrame

`ModernFrame` 是项目本地的现代内容宿主，位于 `neo-bpsys-wpf/Controls/Modern/Frame/`，命名空间为 `neo_bpsys_wpf.Controls.Modern.Frame`。它参考了 iNKORE.UI.WPF.Modern 的 Frame / NavigationTransitionInfo 设计，但不依赖 iNKORE 包，也不导入完整控件库。

## 目标

- 为 `ModernNavigationView` Left 模式承载后台主导航页面。
- 为后续 `ModernNavigationView` Top 模式承载类标签页内容。
- 为 `PluginPage` 等局部内容切换提供带动画的轻量宿主。

当前阶段已经由 `ModernNavigationView` 在 `MainWindow.RootNavigation` 内部使用 `ModernFrame`。仍不迁移 `PluginPage`、`FrontManagePage` 标签页，也不实现 Top 模式标签页。

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
- `SlideNavigationTransitionInfo`
- `SuppressNavigationTransitionInfo`

`DefaultTransitionInfo` 默认使用 Entrance；单次 `Navigate` 可以传入转场覆盖。`IsAnimationEnabled`、`TransitionDuration`、`SystemParameters.ClientAreaAnimation` 会共同决定是否播放动画。快速连续导航会先停止当前转场并释放旧内容引用，再进入下一次导航。

切换已有内容时，`ModernFrame` 按 iNKORE/WinUI 风格先播放旧内容退出动画，退出完成后清理旧 presenter，再播放新内容进入动画。这样避免旧页面和新页面在内容区同时可见；`SuppressNavigationTransitionInfo` 仍立即交换内容。进入动画完成后，活动宿主会恢复到可见、可命中、透明度 1 的状态，并保留 identity `RenderTransform`，避免清理 transform 时产生结束抖动。

## 默认滚动宿主

`ModernFrame` 默认用 `ModernScrollViewer` 包裹当前活动内容，避免每个后台页面都重复定义外层滚动容器。活动内容在视觉树中位于该 `ModernScrollViewer` 下，因此 `ScrollViewerSearchHelper.FindNearestScrollableAncestor(target)` 可以从页面内目标找到 frame 拥有的滚动宿主。

`Page` 经过内部 `Frame` 承载时，页面加载会经过 WPF dispatcher；需要在页面 Loaded 后或使用现有引导滚动重试机制查找目标。加载完成后，`GuidanceScrollHelper` 可以从页面内容发现 `ModernFrame` 的 `ModernScrollViewer` 滚动宿主。

如果页面内部已经有手写 `ScrollViewer`，现有查找逻辑会优先命中更近的内部滚动容器。`IsContentScrollHostEnabled` 可关闭默认滚动宿主，为后续特殊页面保留逃生口。
