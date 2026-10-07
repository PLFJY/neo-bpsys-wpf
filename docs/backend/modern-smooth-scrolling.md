# Modern smooth scrolling

本环节只提供项目本地的平滑滚动基础设施，不全局替换现有 `ScrollViewer`，也不引入 iNKORE NuGet 依赖。

## 组件

- `ModernScrollViewer`：继承 WPF `ScrollViewer`，为普通滚轮和精密触控板的小粒度滚轮提供有界平滑滚动，并默认启用原生纵向触摸平移。它不会接管 `Ctrl+Wheel`、`Shift+Wheel`，也不改变滚动条拖拽行为。
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

`NestedSmoothScrollBehavior` 在控件本地查找并缓存内部 `ScrollViewer`，模板变更后重新解析；内部实际偏移已经到达顶部或底部时，外层可处理同一个事件，不重放或复制滚轮事件。继承到内部宿主的 `Self` 不会阻止该宿主处理输入。启用此行为的 `ListBox`、`ListView`、`DataGrid` 在未显式配置滚动单位时使用虚拟化面板的像素滚动，保留虚拟化，不全局设置 `CanContentScroll=False`。不要把该行为全局套到所有列表上。

## 三类输入

触摸屏使用 WPF 原生 manipulation/panning，不转换成滚轮事件。`ModernScrollViewer` 通过依赖属性元数据将 `PanningMode` 默认设为 `VerticalOnly`，允许 XAML 覆盖。现有宿主启用上述附加滚动行为后，在未配置 panning 的位置补上纵向平移；显式样式或局部设置优先。沿用 WPF 默认惯性与减速，不自定义 `TouchMove`，不标记触摸 manipulation 已处理。开始触摸平移会取消旧滚轮动画，避免两种输入争夺偏移。

精密/高分辨率滚轮保留 `Delta / 120.0` 的小数距离。非整轮增量或间隔不超过 40ms 的事件进入连续模式，连续模式在超过 120ms 的停顿后退出；不识别设备厂商。`WheelScrollPolicy` 把动画目标相对实际可见偏移的领先距离限制为连续模式一档、离散模式最多三档，并考虑视口和内容边界。反向输入直接从可见偏移计算，丢弃原方向待完成距离。连续动画最长 60ms，同一目标、时长和缓动不会重复启动动画；动画计时使用单调时钟。停止输入后只需完成有限剩余距离，而不会追赶累积到远处的目标。

传统鼠标滚轮保留默认 220ms 平滑动画以及有限的同向多档累积。距离遵循 `SystemParameters.WheelScrollLines`：正数按每行 16 DIP 的既有换算，0 不滚动，-1 按一屏滚动；倍率仍参与计算。关闭平滑滚动、关闭系统客户端动画或低渲染级别时保留立即滚动回退。卸载宿主会清理输入序列及活动动画。

左侧纵向导航已经使用 `ModernScrollViewer` 包裹 `ItemsControl`，直接复用同一策略和触摸默认值，无需替换导航模板。顶部横向导航和设计器自定义缩放区域不套用纵向触摸行为。

`ComboBox` 下拉滚动由 `ComboBoxDropdownSmoothScrollBehavior` 专门处理。它在 `DropDownOpened` 后查找 `PART_Popup` 和下拉内部 `ScrollViewer`，只在 popup 本地处理滚轮；下拉不能继续滚动时也会标记事件已处理，避免页面背景跟随滚动。外层 `ModernScrollViewer` / `SmoothScrollBehavior` 遇到 `Popup`、`ContextMenu`、`PopupRoot` 或已打开的 `ComboBox` 时只做保护性让出，不负责程序化滚动下拉框。

不要恢复全局 `Window.PreviewMouseWheel` 路由器，也不要用控件类型或类型名把滚轮重定向到最近的 `ListView`、`ListBox`、`DataGrid`、`ComboBox` 或 `DynamicScrollViewer`。WPF 原生滚动语义仍是基础，项目代码只在明确声明归属的位置补充平滑动画。

## 验证

自动化覆盖分数增量、长输入流的目标上界、反向、边界、倍率、系统行数/整屏设置、时间戳回绕、Self/Frame 归属、嵌套交接、修饰键、弹出层保护和卸载清理。WPF 行为测试使用统一 `WpfTestThread`，不依赖真实触控板。

当前 xUnit 4 测试项目可先构建，再直接运行生成的测试程序（.NET 10 SDK 的旧 VSTest `dotnet test` 入口不适用）。构建与运行应串行，避免运行中的程序集锁住构建输出：

```powershell
dotnet build .\neo-bpsys-wpf.Tests\neo-bpsys-wpf.Tests.csproj
& .\neo-bpsys-wpf.Tests\bin\Debug\net10.0-windows10.0.20348\neo-bpsys-wpf.Tests.exe -noLogo -noColor -class '*WheelScrollPolicyTest' -class '*ModernWheelScrollTest' -class '*ModernFrameTest' -class '*GuidanceScrollHelperTest'
```

Windows 11 真机仍需分别验证精密触控板、触摸屏和普通鼠标：

- 左侧导航：缓慢/快速双指滚动、立即反向、单指纵向滑动和惯性、点击/轻触导航项。
- 共享 `ModernFrame` 页面：三类输入和导航后置顶；GameGuidance 自动滚动仍到达指定目标。
- 布局包列表、图形编辑器的嵌套列表、插件市场滚动区及 Score 页预览 DataGrid：逐个验证三类输入、上下边界交接、选择和虚拟化大列表性能。
- ToggleSwitch、按钮、TextBox：轻触与小幅移动不误触，文本光标/选择保持可用；水平拖动不触发纵向平移。
- ComboBox 下拉、ContextMenu、含滚动内容的弹出层：内部滚动及到达边界时背景页面不滚动，关闭后页面恢复响应。
- 设计器预览：既有滚轮缩放和平移继续正常；禁用平滑动画及系统整屏滚轮设置也需单独检查。

测试通过不能代替上述硬件验证；触摸嵌套惯性、点击阈值和驱动产生的真实事件流仍需在设备上确认。

## 为什么保持 opt-in

现有窗口里有多处滚轮语义并不只是“滚动内容”。例如 `FrontedDesignerWindow` 的预览区域有自定义缩放和平移逻辑，不能被平滑滚动行为接管。因此平滑滚动仍以局部附加行为和显式归属为边界。`ComboBox` 下拉行为应优先加在具体使用点或项目自有命名样式上；不要用全局隐式 `ComboBox` style 覆盖 WPF-UI 默认样式链。

## GameGuidance 自动滚动

GameGuidance 自动滚动是纯 View 层能力，不改变 `GameGuidanceService` 的根流程：仍然由服务执行页面导航、计时器启动、延迟和 `HighlightMessage` 广播。

页面通过两类附加属性 opt-in：

- `GuidanceAutoScrollScope.IsEnabled="True"`：标记页面根或根面板。Scope 在 `Loaded` 时注册 `HighlightMessage`，在 `Unloaded` 时注销。
- `GuidanceScrollTarget.Action` / `GuidanceScrollTarget.Index`：标记页面内可滚动到的控件或区域。带 `Index` 的目标只匹配包含该索引的消息；不带 `Index` 的目标只按 `GameAction` 匹配。

收到 `HighlightMessage` 后，Scope 在当前页面内查找匹配目标，并从目标向上寻找最近的既有可滚动 `ScrollViewer`。如果该 `ScrollViewer` 开启了 `SmoothScrollBehavior.IsProgrammaticAnimationEnabled`，会复用 `ScrollAnimationHelper.SmoothScrollToVerticalOffset(...)` 执行程序化平滑滚动；否则使用普通 `ScrollToVerticalOffset`。找不到 `ScrollViewer` 时仅回退到 `BringIntoView()`。

`ModernFrame` 新导航会先同步把 frame 级 `PART_ContentScrollHost` 置顶，并取消该宿主上的旧纵向动画；它不会延迟到 dispatcher 空闲、timer 或转场完成后执行。这样 GameGuidance 导航完成后再广播 `HighlightMessage` 时，`GuidanceAutoScrollScope` 的后续目标滚动不会被 frame 置顶覆盖。直接 presenter 和显式 self-scroll 子区域不参与 frame 级置顶。

该机制不添加页面级 `ScrollViewer` 包裹，不依赖 WPF-UI `NavigationView`、未来 `ModernFrame`、iNKORE 或固定模板部件名。当前 WPF-UI 页面宿主和未来 `ModernFrame` 只要在目标祖先链上提供可滚动容器，都可以被同一套查找逻辑使用；页面内已有手动 `ScrollViewer` 时会优先使用最近的那个。
