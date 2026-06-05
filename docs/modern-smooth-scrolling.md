# Modern smooth scrolling

本阶段只提供项目本地的平滑滚动基础设施，不全局替换现有 `ScrollViewer`，也不引入 iNKORE NuGet 依赖。

## 组件

- `ModernScrollViewer`：继承 WPF `ScrollViewer`，在普通鼠标滚轮输入时用本地动画平滑滚动。它不会接管 `Ctrl+Wheel`、`Shift+Wheel`、精密触控板的小粒度滚轮输入，也不改变滚动条拖拽行为。
- `SmoothScrollBehavior`：附加行为，通过 `SmoothScrollBehavior.IsEnabled="True"` 让已有 `ScrollViewer` 选择性启用平滑滚轮滚动。
- `ScrollAnimationHelper`：提供 `SmoothScrollToVerticalOffset`，用于后续代码以同一套动画逻辑执行程序化纵向滚动。
- `ScrollViewerSearchHelper`：安全查找最近且可滚动的祖先 `ScrollViewer`，只依赖 WPF 视觉树/逻辑树，不依赖 WPF-UI 或 iNKORE 内部结构。

## 为什么保持 opt-in

现有窗口里有多处滚轮语义并不只是“滚动内容”。例如 `FrontedDesignerWindow` 的预览区域有自定义缩放和平移逻辑，不能被平滑滚动行为接管。因此本阶段只添加基础设施，不修改全局样式，也不迁移 `PluginPage`、`FrontManagePage`、`ClassicBackendWindow`、`MainWindow` 或 Designer v3 相关滚动区域。

## 后续 GameGuidance 用法

后续引导式 BP 自动滚动可以先用 `ScrollViewerSearchHelper.FindNearestScrollableAncestor(target)` 找到目标控件所在的可滚动容器，再用 `ScrollAnimationHelper.SmoothScrollToVerticalOffset(...)` 执行动画滚动。这样可以复用同一套动画、取消和 reduced motion 入口，而不需要依赖具体模板部件名。
