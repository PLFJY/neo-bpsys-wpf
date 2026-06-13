# ModernNavigationView

`ModernNavigationView` 是 `MainWindow.RootNavigation` 的项目本地兼容替换，位于 `neo-bpsys-wpf/Controls/Modern/Navigation/`，命名空间为 `neo_bpsys_wpf.Controls.Modern.Navigation`。

它的定位不是要求业务代码迁移到新的导航模型，而是在外部使用方式基本保持 WPF-UI `NavigationView` / `INavigationService` 兼容的前提下，替换内部视觉实现、pane 行为、选中态、动画和内容宿主。

## 兼容边界

`MainWindow.xaml` 仍可以绑定：

- `MenuItemsSource`
- `FooterMenuItemsSource`
- `IsBackButtonVisible`
- `IsPaneOpen`
- `IsPaneToggleVisible`
- `OpenPaneLength`

`MainWindowViewModel` 仍可以构造 WPF-UI `NavigationViewItem`：

```csharp
new NavigationViewItem(info.Name, info.Icon, info.PageType)
```

`NavigationService` 对外行为保持不变，现有调用方仍使用 `INavigationService`：

- `Navigate(Type)`
- `Navigate(Type, object?)`
- `Navigate(string)`
- `Navigate(string, object?)`
- `GoBack()`
- `NavigateWithHierarchy(...)`

`GameGuidanceService` 不需要感知 `ModernNavigationView`，仍通过 `INavigationService.Navigate(pageType)` 导航。

## 数据适配

`ModernNavigationView` 不把 WPF-UI `NavigationViewItem` 直接放进视觉树，而是转换为 `ModernNavigationEntry` 后再渲染。原始 WPF-UI item 被当作数据源保存，用于兼容 `SelectedItem`、`IsActive` 和目标页信息。

适配规则：

- `Content` 为字符串时先作为本地化 key 解析。
- `Icon` 保留原始图标数据，并在渲染时转换。
- `TargetPageType` 用作主要导航身份。
- `TargetPageTag`、`Tag`、`Id` 用于 `Navigate(string)` 匹配。
- `IsEnabled` 会传递到现代导航按钮。

`ObservableCollection` 作为 `MenuItemsSource` / `FooterMenuItemsSource` 时，集合变化会同步刷新现代 entry。

## 本地化

后台页面注册中的 `BackendPageInfo.Name` 通常是类似 `HomePage` 的本地化 key。`ModernNavigationView` 遇到字符串内容时会先调用 `I18nHelper.GetLocalizedString(key)`，解析不到时才显示原始字符串。

控件 Loaded 后会监听 `WPFLocalizeExtension` 的语言变化通知，并刷新已适配 entry 的显示文本。`MainWindowViewModel` 不需要提前本地化菜单名称。

## 图标

现有后台页面图标继续使用 WPF-UI `SymbolRegular`。渲染时：

- `SymbolRegular` 会转换为 WPF-UI `SymbolIcon`。
- 已经是 `SymbolIcon` 的图标会克隆，避免同一个 UIElement 被重复挂载。
- 其他不可安全复用的 `IconElement` / `FrameworkElement` 会回退到安全图标，并输出调试信息。

WPF-UI 包不会被移除，其他 WPF-UI 控件仍继续使用。

## 视觉与布局

`ModernNavigationView` 始终只拥有一个内部内容宿主：

```text
ModernNavigationView
└── PART_Frame: ModernFrame
```

Left 模式和 Top 模式共享这个 `PART_Frame`。不要为 Top 模式添加 `PART_TopFrame`，也不要在业务页面外置 `PluginTabsFrame` 或其他独立 Frame。

Left 模式用于 `MainWindow.RootNavigation`：

- 左侧 pane
- pane toggle button
- 主菜单列表
- footer 菜单列表
- 右侧共享 `PART_Frame` 内容区

Top 模式用于局部标签导航：

- 顶部水平 `ListBox` selector
- 只显示 `MenuEntries`，不显示 footer items
- 下方共享 `PART_Frame` 内容区

内容区通过 `ModernFrame` 提供滚动宿主和页面转场。pane 展开时使用 `OpenPaneLength`，折叠时使用 `CompactPaneLength`，内容列占用剩余宽度；Top 模式隐藏左侧 pane，并让共享内容区跨完整宽度。

`ModernNavigationView` 会按 `NavigationBehavior` 集中配置内部 `PART_Frame.ContentScrollHostMode`：

- `PageNavigation` 使用 `Enabled`，保持 `MainWindow.RootNavigation` 的后台页面外层滚动宿主，`GameGuidanceService` 仍能通过 frame 的 `ModernScrollViewer` 做引导滚动。
- `LocalTabs` 使用 `Auto`，默认保留 frame/page 级滚动；只有 tab 内容根显式声明 `ModernScroll.Ownership="Self"` 时才走直接 presenter。

共享 `PART_Frame` 默认会在新导航后立即把 frame 级滚动宿主置顶。因此 `MainWindow.RootNavigation` 页面切换、`PluginPage` 标签切换和 `FrontManagePage` 标签切换都会回到页面顶部。该行为不递归重置子视图内部的 self-scroll 区域；使用直接 presenter 的局部页面继续拥有自己的滚动状态。置顶发生在 frame `Navigated` 之前，`GameGuidance` 后续的目标自动滚动仍可以覆盖它。

`LocalTabs` 的 `Auto` 模式不按 `ListView` / `ListBox` / `DynamicScrollViewer` 类型推断滚动归属。`PluginInstalledView`、`PluginMarketView` 这类非约束列表页默认仍由 frame 级 `ModernScrollViewer` 滚动；`FrontedLayoutPackagesView` 这类独立分栏页面可以在根上显式声明 self ownership，并在具体列表/详情滚动区域启用 `NestedSmoothScrollBehavior`。

菜单项前景色跟随 WPF-UI 动态主题资源。按钮默认、悬停、按下和选中状态使用 NavigationView item 前景色资源；禁用状态使用 WPF-UI 文本禁用色资源。文本和图标都从按钮 `Foreground` 继承，不硬编码黑白颜色，因此主题切换时可以随资源更新。

pane toggle 使用项目本地 `ModernPaneToggleButtonStyle`，参考 iNKORE/WinUI 的 TogglePaneButton：点击区域按 `CompactPaneLength` 占满折叠 pane 宽度，图标居中，默认、悬停、按下和禁用状态使用 WPF-UI 动态主题资源。

主菜单滚动宿主在 pane 折叠时仍使用 `Auto` 垂直滚动条，但会在该 ScrollViewer 作用域内切换为 4px 的本地窄滚动条模板，避免普通 WPF 滚动条挤压或覆盖 compact 图标，同时保留可见滚动指示和鼠标滚轮滚动。pane 展开时恢复默认滚动条样式。后续如需要更接近 iNKORE `ScrollViewerEx`，可以再做 opt-in 的自动隐藏滚动条行为。

当前 Top 模式只实现本项目需要的本地标签形态，参考 iNKORE `nvSample7` 的选择和推荐转场模型，但适配到本项目已有的内部 frame 导航模型。未复制 iNKORE 完整 `NavigationView` 的 overflow、层级、测量和 flyout 实现。

本阶段不实现：

- Top overflow
- 层级 flyout
- settings item
- autosuggest
- breadcrumb
- `FrontManagePage` 标签页迁移，留到后续 历史迭代 6
- `MessageBox` / `ContentDialog` 迁移

## 导航行为

点击菜单或 footer entry 会导航到 `TargetPageType`。外部 `Navigate(Type)` 会优先按 `TargetPageType` 选中匹配 entry；没有匹配 entry 时仍会导航到目标页。`Navigate(string)` 优先匹配 tag / id，不用本地化后的显示文本做身份判断。

用户点击当前已选中的 entry 会被视为无操作，不会重复触发 `ItemInvoked` / `Navigating` / `Navigated`，也不会重复启动 `ModernFrame` 转场或压入返回栈。外部 `INavigationService.Navigate(...)` 调用仍保持原兼容行为。

`GoBack()` 和 `ClearJournal()` 委托给内部 `ModernFrame`。`NavigateWithHierarchy(...)` 当前按普通 `Navigate(...)` 处理，保留兼容入口，后续如确实引入层级菜单再扩展。

## 导航行为模式

控件通过 `NavigationBehavior` 区分全局页面导航和局部标签导航：

```csharp
public enum ModernNavigationBehavior
{
    PageNavigation,
    LocalTabs
}
```

`PageNavigation` 是默认值，用于 `MainWindow.RootNavigation`。它保持 历史迭代 4 行为：通过 `INavigationViewPageProvider`、`IServiceProvider` 或 `Activator` 创建后台 Page，保留 `ModernFrame` journal/back 行为，并继续兼容 `NavigationService` 和 `GameGuidanceService`。

`LocalTabs` 用于局部标签页。它直接创建本地 `FrameworkElement`，当前约定子视图使用 `UserControl`；如果子视图没有自己的 `DataContext`，会继承 `ModernNavigationView.DataContext`。每次本地标签切换成功后都会清空 `PART_Frame` journal，避免标签切换进入全局返回栈。切换方向根据旧/新 tab 在 `MenuEntries` 中的索引选择横向 slide 转场。局部标签页不要全局关闭 `ModernFrame` 滚动宿主，应依赖 `ContentScrollHostMode.Auto` 保留默认 frame 级滚动，只对明确需要独立 viewport 的区域声明 self ownership。

## 后台页迁移

`PluginPage` 是首个迁移到 Top LocalTabs 的后台页。它仍然是唯一带 `BackendPageInfo` 的全局插件管理页面，子视图不注册为后台页面：

- `PluginInstalledView`：已安装插件列表，类型为 `UserControl`。
- `PluginMarketView`：插件市场、下载队列、详情面板、设置面板，类型为 `UserControl`。

`PluginPage.xaml.cs` 使用和 `MainWindowViewModel` 相同的 WPF-UI `NavigationViewItem` 构造路径创建 tab：

```csharp
new NavigationViewItem("Installed", SymbolRegular.AppsList24, typeof(PluginInstalledView));
new NavigationViewItem("PluginMarket", SymbolRegular.AppsAddIn24, typeof(PluginMarketView));
```

这样可以继续复用 `ModernNavigationEntry` 对 WPF-UI item 的适配逻辑，包括 `Content` 本地化、`SymbolIcon`/`SymbolRegular` 图标转换、`TargetPageType` 和 `TargetPageTag` 支持。插件市场浮层外部点击、ComboBox popup 例外和 Markdown hyperlink 拦截逻辑随 `PluginMarketView` 一起迁移，避免 `PluginPage` 访问子视图内部命名元素。

`FrontManagePage` 同样迁移到 Top LocalTabs。它仍然是唯一带 `BackendPageInfo` 的前台窗口管理后台页，子视图不注册为后台页面：

- `FrontedWindowsView`：前台窗口打开/关闭、打开 Fronted Designer、插件前台窗口列表，类型为 `UserControl`。
- `FrontedLayoutPackagesView`：`.bpui` / Fronted layout package 列表、导入导出、激活、复制、删除和详情区域，类型为 `UserControl`。

`FrontManagePage.xaml.cs` 使用 WPF-UI `NavigationViewItem` 创建 tab：

```csharp
new NavigationViewItem("FrontendWindows", SymbolRegular.ShareScreenStart24, typeof(FrontedWindowsView));
new NavigationViewItem("LayoutPackages", SymbolRegular.AppsList24, typeof(FrontedLayoutPackagesView));
```

布局包列表的双击激活逻辑随 `FrontedLayoutPackagesView` 迁移，避免 `FrontManagePage` 访问子视图内部命名元素。这里的 Fronted 仍指 WPF 前台输出窗口，不是 Web frontend。

子视图迁移时如果需要覆写 WPF-UI 控件或 WPF 控件的局部 `Style`，必须使用 `BasedOn="{StaticResource {x:Type ...}}"` 继承默认样式，例如 `ui:Button`、`ui:HyperlinkButton`、`ui:TextBox`、`ListBoxItem` 等。不要为了单元测试宿主缺少资源而写没有 `BasedOn` 的裸控件 `Style`；测试或子视图资源应补齐主题/控件字典，运行时 XAML 必须保留默认控件模板、状态和主题资源链。纯 `Border` 等无默认控件模板的轻量元素不适用这条 `BasedOn` 约束，但能用普通属性/Converter 表达显隐时优先避免写裸 `Style`。
