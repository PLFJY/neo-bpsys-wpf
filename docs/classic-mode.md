# Classic Mode 维护说明

Classic Mode 是操作密集型的导播台后台 Shell。它只重排现有后台 View 和控件，不复制业务状态，不新增 Pick / Ban / Score 专用业务 ViewModel，也不重写 Team、GameData、Fronted Designer v3 或前台窗口逻辑。

## 定位

Classic Mode 复用现有页面 ViewModel、共享服务和 `CurrentGame` 状态。`ClassicBackWindow` 可以直接注入现有 VM 给不同区域设置 `DataContext`，但不要创建 `ClassicPickViewModel`、`ClassicBanViewModel`、`ClassicScoreViewModel` 或类似第二套业务状态。

新增 Classic UI 时，应优先复制现有页面中的控件绑定和命令，不要把业务规则写进 `ClassicBackWindow.xaml.cs`。code-behind 只允许做窗口生命周期、弹窗入口、InfoBar 绑定和轻量 UI 事件。

## 导航边界

Modern 后台使用 `MainWindow` + WPF-UI `NavigationView`。Classic Mode 启动时不显示 `MainWindow`，也不执行 Modern 页面预加载。

Classic Mode 下项目内 `NavigationService.Navigate(...)`、`GoBack()` 和层级导航应对页面切换 no-op，避免访问未设置的 `NavigationView`。这只阻断页面切换，不应阻断 `GameGuidanceService` 的引导流程：计时器启动、Ban 位数量设置和 `HighlightMessage` 广播仍然必须继续执行。

## 管理入口

Classic 主窗口只保留前台管理、设置、插件等单按钮入口。不要把完整 `FrontManagePage` 内联到 `ClassicBackWindow`，也不要复制设置页、插件页或前台管理页的业务逻辑。

这些入口应通过弹窗承载现有 singleton Page：

- 前台管理打开 `FrontManagePage`。
- 设置打开 `SettingPage`。
- 插件打开 `PluginPage`。

弹窗必须 single-instance。重复点击同一入口时只 `Activate()` / `Focus()` 已打开窗口，不要创建多个相同窗口。关闭弹窗时必须 detach Page content，避免 singleton Page 再次承载时出现 re-parent 异常。

## UI 间距

Classic UI 使用共享 margin resources 维护基本节奏：

- `ClassicInlineControlMargin`: 操作控件默认右下间距，建议 `0,0,8,8`。
- `ClassicCardMargin`: 分区卡片间距，建议 `0,0,10,10`。
- `ClassicSectionSpacing`: 分区内部标题到内容的垂直间距，建议 `0,10,0,0`。

所有 `WrapPanel` 的直接子控件都应有右边距和下边距，尤其是 `Button`、`ui:Button`、`ToggleButton`、`ComboBox`、`ui:ToggleSwitch` 和直接放入的 `TextBlock`。不要使用巨大固定 `Margin` 模拟布局；优先使用 Grid、StackPanel、WrapPanel 和共享 spacing 资源。新增 Classic UI 后必须检查按钮间距和 WrapPanel 换行后的垂直间距。

## 重启规范

项目已有 `App.Restart()` 封装，当前实现会释放单实例 mutex 并重启应用。任何需要重启应用的入口都必须调用 `AppBase.Current.Restart()`、`App.Current.Restart()` 的等价封装路径，不能手动 `Process.Start(exe)` 再 `Application.Current.Shutdown()`。

原因是应用使用 mutex 单实例。手动重启容易让新实例被旧实例的 mutex 拦截，也容易重复实现释放流程。

设置页切换 Classic Mode 后可以保存配置并弹出重启确认框；用户确认后必须走上述 Restart 封装。
