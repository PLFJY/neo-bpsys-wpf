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

当前只实现 Left 模式：

- 左侧 pane
- pane toggle button
- 主菜单列表
- footer 菜单列表
- 右侧内容区

内容区使用 `ModernFrame`，并通过 `ModernFrame` 的默认 `ModernScrollViewer` 提供外层滚动宿主和页面转场。pane 展开时使用 `OpenPaneLength`，折叠时使用 `CompactPaneLength`，内容列占用剩余宽度。

本阶段不实现：

- Top 模式
- overflow
- 层级 flyout
- settings item
- autosuggest
- breadcrumb
- `PluginPage` / `FrontManagePage` 标签页迁移
- `MessageBox` / `ContentDialog` 迁移

## 导航行为

点击菜单或 footer entry 会导航到 `TargetPageType`。外部 `Navigate(Type)` 会优先按 `TargetPageType` 选中匹配 entry；没有匹配 entry 时仍会导航到目标页。`Navigate(string)` 优先匹配 tag / id，不用本地化后的显示文本做身份判断。

`GoBack()` 和 `ClearJournal()` 委托给内部 `ModernFrame`。`NavigateWithHierarchy(...)` 当前按普通 `Navigate(...)` 处理，保留兼容入口，后续如确实引入层级菜单再扩展。
