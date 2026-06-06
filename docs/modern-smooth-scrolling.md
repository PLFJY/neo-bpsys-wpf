# Modern smooth scrolling

本阶段只提供项目本地的平滑滚动基础设施，不全局替换现有 `ScrollViewer`，也不引入 iNKORE NuGet 依赖。

## 组件

- `ModernScrollViewer`：继承 WPF `ScrollViewer`，在普通鼠标滚轮输入时用本地动画平滑滚动。它不会接管 `Ctrl+Wheel`、`Shift+Wheel`、精密触控板的小粒度滚轮输入，也不改变滚动条拖拽行为。
- `SmoothScrollBehavior`：附加行为，通过 `SmoothScrollBehavior.IsEnabled="True"` 让已有 `ScrollViewer` 选择性启用平滑滚轮滚动。
- `NestedSmoothScrollBehavior`：附加到显式 self-scroll 的 `ListBox`、`ListView`、`DataGrid`、`TreeView`、`ScrollViewer` 或包含内部 `ScrollViewer` 的控件，使该控件自己的滚动宿主保持平滑滚动。
- `ComboBoxDropdownSmoothScrollBehavior`：附加到 `ComboBox`，在下拉 `Popup` 创建后本地接管下拉内部滚轮，使下拉列表平滑滚动，并阻止页面背景跟随滚动。
- `ModernScroll.Ownership`：显式声明滚动归属，`Auto`/`Frame` 表示页面内容默认由 frame/page 滚动，`Self` 表示该区域拥有自己的滚动语义。
- `ScrollAnimationHelper`：提供 `SmoothScrollToVerticalOffset`，用于后续代码以同一套动画逻辑执行程序化纵向滚动。
- `ScrollViewerSearchHelper`：安全查找最近且可滚动的祖先 `ScrollViewer`，只依赖 WPF 视觉树/逻辑树，不依赖 WPF-UI 或 iNKORE 内部结构。
- `WheelScrollEventGuard`：集中判断滚轮事件是否属于下拉框、弹出层或显式 self-scroll 区域，避免外层页面滚动宿主抢走明确归属给子控件的滚轮。

## 滚轮事件归属

平滑滚动以鼠标悬停位置为准，不要求页面或 frame 先获得键盘焦点。`ModernScrollViewer` 和 `SmoothScrollBehavior` 只在自身控件上注册本地 `PreviewMouseWheel` / `MouseWheel` 处理，不再向 `Window` 注册全局路由器，也不会按“最近的 `ListView` / `ListBox` / `DataGrid`”猜测滚动目标。

默认归属是 frame/page。也就是说，非约束高度的 `ListView`、`ListBox` 等普通页面内容，鼠标悬停滚轮会滚动外层 `ModernScrollViewer`。控件类型本身不构成 self-scroll 证据。

显式 self-scroll 区域需要同时声明归属和行为，例如：

```xml
<ListBox
    scrolling:ModernScroll.Ownership="Self"
    scrolling:NestedSmoothScrollBehavior.IsEnabled="True" />
```

`NestedSmoothScrollBehavior` 在控件本地查找内部 `ScrollViewer` 并平滑滚动；内部已经到达顶部或底部时不会强行吞掉事件。不要把该行为全局套到所有列表上。

`ComboBox` 下拉滚动由 `ComboBoxDropdownSmoothScrollBehavior` 专门处理。它在 `DropDownOpened` 后查找 `PART_Popup` 和下拉内部 `ScrollViewer`，只在 popup 本地处理滚轮；下拉不能继续滚动时也会标记事件已处理，避免页面背景跟随滚动。外层 `ModernScrollViewer` / `SmoothScrollBehavior` 遇到 `Popup`、`ContextMenu`、`PopupRoot` 或已打开的 `ComboBox` 时只做保护性让出，不负责程序化滚动下拉框。

不要恢复全局 `Window.PreviewMouseWheel` 路由器，也不要用控件类型或类型名把滚轮重定向到最近的 `ListView`、`ListBox`、`DataGrid`、`ComboBox` 或 `DynamicScrollViewer`。WPF 原生滚动语义仍是基础，项目代码只在明确声明归属的位置补充平滑动画。

## 为什么保持 opt-in

现有窗口里有多处滚轮语义并不只是“滚动内容”。例如 `FrontedDesignerWindow` 的预览区域有自定义缩放和平移逻辑，不能被平滑滚动行为接管。因此平滑滚动仍以局部附加行为和显式归属为边界。`ComboBox` 下拉行为可以通过基于现有默认样式的共享 setter 启用，但不能替换原控件模板或选择行为。

## GameGuidance 自动滚动

GameGuidance 自动滚动是纯 View 层能力，不改变 `GameGuidanceService` 的根流程：仍然由服务执行页面导航、计时器启动、延迟和 `HighlightMessage` 广播。

页面通过两类附加属性 opt-in：

- `GuidanceAutoScrollScope.IsEnabled="True"`：标记页面根或根面板。Scope 在 `Loaded` 时注册 `HighlightMessage`，在 `Unloaded` 时注销。
- `GuidanceScrollTarget.Action` / `GuidanceScrollTarget.Index`：标记页面内可滚动到的控件或区域。带 `Index` 的目标只匹配包含该索引的消息；不带 `Index` 的目标只按 `GameAction` 匹配。

收到 `HighlightMessage` 后，Scope 在当前页面内查找匹配目标，并从目标向上寻找最近的既有可滚动 `ScrollViewer`。如果该 `ScrollViewer` 开启了 `SmoothScrollBehavior.IsProgrammaticAnimationEnabled`，会复用 `ScrollAnimationHelper.SmoothScrollToVerticalOffset(...)` 执行程序化平滑滚动；否则使用普通 `ScrollToVerticalOffset`。找不到 `ScrollViewer` 时仅回退到 `BringIntoView()`。

该机制不添加页面级 `ScrollViewer` 包裹，不依赖 WPF-UI `NavigationView`、未来 `ModernFrame`、iNKORE 或固定模板部件名。当前 WPF-UI 页面宿主和未来 `ModernFrame` 只要在目标祖先链上提供可滚动容器，都可以被同一套查找逻辑使用；页面内已有手动 `ScrollViewer` 时会优先使用最近的那个。
