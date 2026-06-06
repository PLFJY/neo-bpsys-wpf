# Modern smooth scrolling

本阶段只提供项目本地的平滑滚动基础设施，不全局替换现有 `ScrollViewer`，也不引入 iNKORE NuGet 依赖。

## 组件

- `ModernScrollViewer`：继承 WPF `ScrollViewer`，在普通鼠标滚轮输入时用本地动画平滑滚动。它不会接管 `Ctrl+Wheel`、`Shift+Wheel`、精密触控板的小粒度滚轮输入，也不改变滚动条拖拽行为。
- `SmoothScrollBehavior`：附加行为，通过 `SmoothScrollBehavior.IsEnabled="True"` 让已有 `ScrollViewer` 选择性启用平滑滚轮滚动。
- `ScrollAnimationHelper`：提供 `SmoothScrollToVerticalOffset`，用于后续代码以同一套动画逻辑执行程序化纵向滚动。
- `ScrollViewerSearchHelper`：安全查找最近且可滚动的祖先 `ScrollViewer`，只依赖 WPF 视觉树/逻辑树，不依赖 WPF-UI 或 iNKORE 内部结构。
- `WheelScrollEventGuard`：集中判断滚轮事件是否属于下拉框、弹出层或内层列表/滚动控件，避免外层页面滚动宿主抢走子控件滚轮。

## 滚轮事件归属

平滑滚动只处理冒泡阶段的 `MouseWheel`，不再处理 `PreviewMouseWheel`。这样 `ComboBox` 下拉列表、`ListBox`、`ListView`、内层 `ScrollViewer` 等子控件可以先接收滚轮事件，外层 `ModernScrollViewer` / `SmoothScrollBehavior` 不会在预览阶段提前标记 `Handled=true`。

当滚轮来源位于已打开的 `ComboBox`、`Popup` / `ContextMenu`、`PopupRoot`，或位于内层 `ScrollViewer`、`ModernScrollViewer`、`ListBox`、`ListView`、`DataGrid`、`TreeView` 时，外层平滑滚动会让出事件。对于 `ModernScrollViewer` 自身，这类事件还会阻止继续落到父级 `ScrollViewer` 默认滚动逻辑，避免打开下拉框时背后的页面或 frame 跟着滚动。

frame 级滚动只负责页面级内容区域。LocalTabs 子页如果有自己的列表或滚动宿主，应由子页内部控件滚动；没有内部滚动宿主的普通内容仍可使用 frame 级 `ModernScrollViewer` 平滑滚动。

## 为什么保持 opt-in

现有窗口里有多处滚轮语义并不只是“滚动内容”。例如 `FrontedDesignerWindow` 的预览区域有自定义缩放和平移逻辑，不能被平滑滚动行为接管。因此本阶段只添加基础设施，不修改全局样式，也不迁移 `PluginPage`、`FrontManagePage`、`ClassicBackendWindow`、`MainWindow` 或 Designer v3 相关滚动区域。

## GameGuidance 自动滚动

GameGuidance 自动滚动是纯 View 层能力，不改变 `GameGuidanceService` 的根流程：仍然由服务执行页面导航、计时器启动、延迟和 `HighlightMessage` 广播。

页面通过两类附加属性 opt-in：

- `GuidanceAutoScrollScope.IsEnabled="True"`：标记页面根或根面板。Scope 在 `Loaded` 时注册 `HighlightMessage`，在 `Unloaded` 时注销。
- `GuidanceScrollTarget.Action` / `GuidanceScrollTarget.Index`：标记页面内可滚动到的控件或区域。带 `Index` 的目标只匹配包含该索引的消息；不带 `Index` 的目标只按 `GameAction` 匹配。

收到 `HighlightMessage` 后，Scope 在当前页面内查找匹配目标，并从目标向上寻找最近的既有可滚动 `ScrollViewer`。如果该 `ScrollViewer` 开启了 `SmoothScrollBehavior.IsProgrammaticAnimationEnabled`，会复用 `ScrollAnimationHelper.SmoothScrollToVerticalOffset(...)` 执行程序化平滑滚动；否则使用普通 `ScrollToVerticalOffset`。找不到 `ScrollViewer` 时仅回退到 `BringIntoView()`。

该机制不添加页面级 `ScrollViewer` 包裹，不依赖 WPF-UI `NavigationView`、未来 `ModernFrame`、iNKORE 或固定模板部件名。当前 WPF-UI 页面宿主和未来 `ModernFrame` 只要在目标祖先链上提供可滚动容器，都可以被同一套查找逻辑使用；页面内已有手动 `ScrollViewer` 时会优先使用最近的那个。
