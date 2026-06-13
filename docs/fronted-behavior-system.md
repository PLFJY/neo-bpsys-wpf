# Fronted Behavior Graph System

本文是 Designer v3 行为系统维护说明。旧动画服务已经移除，前台动画路径统一为：

```text
事件/Transition 请求
  -> FrontedBehaviors/{Window}.behaviors.json
  -> 节点图 runtime
  -> animation runtime
  -> property adapter / animation part
```

正常运行时不直接调用旧 `AnimationService`，ViewModel / Service 也不应直接用 WPF `BeginAnimation` 复刻旧行为。WPF 动画只应在 animation runtime、property adapter 或无关通用 UI 控件内部出现。

## 行为类型

| 类型 | 用途 | 运行规则 |
| --- | --- | --- |
| `OneShot` | 单次事件动画 | 触发后执行一个 `Graph`。 |
| `Loop` | 呼吸、循环提示、持续高亮 | `StartGraph` 启动，`LoopGraph` 按策略循环，任意 `StopTriggers` 命中后执行 `StopGraph`。 |
| `Transition` | Pick / Ban / Swap 等状态切换 | 由业务服务发出 transition request，runtime 根据行为文档和稳定目标执行。 |

Loop 支持多个 StopTrigger，语义是 OR。Designer 提供停止全部 loop 动画的安全按钮，用于预览或现场状态异常时清理仍在运行的 loop。PickingBorder 呼吸与后台引导高亮相关动画都由 behavior document 定义，不再有 legacy guidance breathing 设置。

## 动画目标和部件

行为图通过 `BehaviorGuid` 定位控件，通过 `part:{BehaviorGuid}:{PartName}` 定位运行时生成的动画部件。`LockOverlay` 和 `PickingBorder` 是常用内置 part；自定义动画部件记录在 `ControlBehaviorSet.AnimationParts`，不写入 `FrontedLayouts` 主控件列表。

动画效果由 `AnimateProperty`、`SetProperty`、`ResetProperty` 等节点请求 runtime 执行。常用属性包括 `Opacity`、`Visibility`、`VisualOffsetX/Y`、`ScaleX/Y`、`Rotation`、`FillColor`、`StrokeColor`、`TextColor`、`ClipInset*`。`WaitForCompletion` 是布尔属性，编辑器使用 `true/false` ComboBox。

## 业务事件接入

`CharacterSelectionService` 使用 Transition 表达角色选择、替换等前台动画。PickPage / 手动选角产生的 picking border 事件通过明确 payload 和稳定控件身份进入行为系统；过滤器只使用事件 payload 字段，不使用临时行为标签。布尔 payload（例如 `Event.HasOldCharacter`、`Event.HasNewCharacter`）在过滤器中使用 `true/false` ComboBox，并限制为 `Equals`、`NotEquals`、`Exists`。

新增内置 behavior 时：

1. 先确认目标 layout 控件有稳定 `BehaviorGuid`。
2. 在 `Resources/FrontedBehaviors/{Window}.behaviors.json` 添加或更新行为。
3. 只引用稳定控件身份和稳定 part 名称。
4. 用节点图 runtime 和动画 runtime 测试验证 OneShot / Loop / Transition 行为。
5. 不在 ViewModel 中直接创建 WPF Storyboard，也不恢复旧 `AnimationService`。

## 历史说明

## 默认前台动画

旧 `AnimationService` 已移除。默认前台动画由 `Resources/FrontedBehaviors/{WindowType}.behaviors.json`
提供，并与 `Resources/FrontedLayouts` 中的稳定 `BehaviorGuid` 对应。

角色 Pick 与求生者交换使用 Transition 行为；PickingBorder 呼吸效果使用 Loop 行为，并由稳定的
Guidance 行为事件驱动。后台 Pick 页面不再提供待选框闪烁开关。

普通用户不需要编辑这些行为图。内置包提供默认动画，进阶用户可在自定义布局包中修改行为文档。
内置图节点在动画编辑器画布中部按执行顺序横向排列。动画节点会等待播放完成后继续执行，因此默认
Transition 图不额外插入 `flow.delay`。

## 跨控件复制与粘贴

Designer v3 的行为面板支持把单个行为复制到其他控件，也支持在目标选择窗口中一次粘贴到多个兼容控件。行为剪贴板是应用级内存剪贴板，不改变 behaviors JSON 格式，也不引入动画库或动画片段文件。

粘贴时会深拷贝行为并生成新的 `BehaviorId`。`Graph`、`StartGraph`、`LoopGraph`、`StopGraph` 中指向源控件的 `guid:{BehaviorGuid}` 和 `part:{BehaviorGuid}:{PartName}` 会改写为目标控件的 `BehaviorGuid`；`Self` 和指向其他控件的外部引用保持不变。外部引用会显示在多目标粘贴预览中。

生成部件目标会在粘贴前验证：

- `PickingBorder` 要求目标是启用了 `PickingBorderAvailable` 的图片控件。
- `LockOverlay` 要求目标是启用了 `Lockable` 的图片控件。

触发器索引可以在源控件和目标控件索引均可明确推断时自动改写。语义索引优先使用明确控件配置，其次使用绑定路径中的唯一索引，最后使用控件名尾部数字。改写只处理受支持的 `Event.Index*` / `Event.PreviousIndex*` 字段，并要求右值精确匹配源索引，不执行任意字符串子串替换。

> **状态**: 历史迭代 0 完成，待 历史迭代 1 实施
> **功能主线**: 把 Designer v3 从静态前台布局编辑器升级成"事件驱动的控件动画/行为编排系统"
> **本报告仅做源码勘察，不包含实现代码**

## 历史迭代 1 implemented

历史迭代 1 已完成行为系统的数据基础：

- `FrontedControlConfigBase` 增加 `BehaviorGuid`，作为行为系统内部控件标识；普通 PropertyGrid 不显示该字段。
- Add Control 创建内置控件和插件控件时都会生成新的 `BehaviorGuid`；复制/粘贴控件会重新生成，重命名控件不影响该值。
- 删除控件时会通过 `IFrontedBehaviorService.RemoveBehaviors(Guid)` 调用清理入口；当前实现为 no-op，占位给后续 behaviors 持久化使用。
- Core 新增 `Models/FrontedLayout/Behaviors/` 纯数据模型，覆盖行为文档、控件行为集合、触发器、过滤器、节点图、连接和循环策略。
- 已添加 BehaviorGuid JSON/PropertyGrid/复制/删除测试，以及行为模型默认值和 JSON roundtrip 测试。

## 历史迭代 2 implemented

历史迭代 2 已完成 Designer 侧行为面板和触发器编辑能力：

- `IFrontedBehaviorService` 扩展为行为文档读写服务，`FrontedBehaviorService` 会按当前激活布局包读写 `FrontedBehaviors/{WindowType}.behaviors.json`。行为数据仍独立于控件 config 和 `FrontedWindowConfig`。
- Designer v3 右侧属性区新增可折叠的“动画 / 行为”面板。选中控件后可以添加 OneShot / Loop 行为，重命名、启用/禁用、复制和删除行为。
- 当旧布局控件的 `BehaviorGuid == Guid.Empty` 且用户第一次添加行为时，编辑器会按需生成新的 `BehaviorGuid` 并标记 layout dirty；仅切换选中控件不会生成 Guid。
- OneShot 行为可编辑 `Trigger`；Loop 行为可编辑 `StartTrigger`、`StopTriggers` 和 `LoopPolicy`。触发器编辑器使用事件 payload 参数、可读运算符和文本值组成规则；内部 `Source` 与兼容字段 `RightValueKind` 不在正常 UI 中显示。`StopTriggers` 是简单 OR 列表，任意一个停止条件满足时循环动画会停止并执行 `StopGraph`。
- UI 提供动画编辑器入口；OneShot 显示单图占位，Loop 通过 Top LocalTabs 切换 `StartGraph` / `LoopGraph` / `StopGraph` 占位摘要，并明确提示节点图编辑器将在 历史迭代 3 提供。
- Designer VM 单独跟踪 `AreBehaviorsDirty`；保存操作会同时处理 layout dirty 和 behaviors dirty。删除控件时会删除该控件自身的 `ControlBehaviorSet`。
- `FrontedBehaviorEventCatalog` 从显式标注的 `ISharedDataService` 语义事件反射并缓存事件元数据与常用 payload 过滤字段。
- 已添加行为面板 ViewModel、行为文档持久化、事件目录和轻量 Designer 集成测试。

历史迭代 2 仍不实现：可视化节点图编辑器、真实事件总线、动画 runtime、WPF 动画执行、插件节点执行、Timeline 编辑器或前台窗口行为播放。这些仍属于 历史迭代 3+。

## 历史迭代 3 implemented

历史迭代 3 已完成 Designer 预览侧的可视化节点图编辑 MVP 和图执行核心：

- 动画编辑器不再显示占位文本。OneShot 行为打开 `behavior.Graph` 的节点图编辑器；Loop 行为继续使用 Top `LocalTabs`，分别编辑 `StartGraph`、`LoopGraph` 和 `StopGraph`。
- 节点图编辑器提供左侧节点目录、中间 Canvas、右侧属性编辑器、底部验证与执行日志。支持添加、选择、拖动、复制、删除节点，点击输出端口后点击兼容输入端口创建连接，并可删除连接。
- 当前内置节点目录包含 Flow、Action、Value 三类节点。Flow 节点包括 Start、End、Delay、Parallel、If；Action 节点包括 Log、SetProperty、ResetProperty、AnimateProperty；Value 节点包括 Number、String、Boolean、Color、EventValue、ControlReference。
- 图数据仍使用 历史迭代 1/2 的 `FrontedNodeGraph` / `FrontedNode` / `FrontedNodeConnection` JSON 模型，节点位置、属性和连接可以 roundtrip 保存。模型新增的查询/删除 helper 只提供纯模型逻辑，不改变持久化结构。
- 图验证会报告缺失 Start、多个 Start、连接引用缺失节点、端口不存在、端口类型不兼容、重复或超过端口基数的连接、必填属性缺失，以及 Delay / AnimateProperty 的非法时长。验证消息会显示在编辑器中，但本阶段不阻止保存，以便用户保留并修复异常图。
- Designer preview 通过 `FrontedNodeGraphRuntime` 执行当前选中图。支持 Start、End、Delay、Parallel、If、Log，以及 SetProperty / ResetProperty / AnimateProperty 的 action request 生成。
- 普通连线就是顺序执行。Flow 输入/输出端口各最多一条连接；需要分支时应使用 Parallel 或 If。Parallel 节点提供 In、Branch1/2/3 和 Out 端口：所有分支并发执行，Out 在所有分支完成后执行，分支末端无需连接 End 节点。多个 Start 会被视为错误并阻止 preview。
- If 节点复用 `FrontedTriggerFilterTextComparer` 文本比较语义；`Event.*` 从预览上下文 payload 取值，其他左值按文本处理。
- SetProperty、ResetProperty 和 AnimateProperty 在 历史迭代 3 只写入执行日志并生成 `FrontedGraphActionRequest`，不会修改 WPF 控件，也不会创建 Storyboard。真实 WPF 动画 runtime 属于 历史迭代 4。

历史迭代 3 仍不实现：真实前台窗口 runtime 播放、真实 `IFrontedEventBus`、插件节点、Timeline 编辑器、断点调试器、Canvas/window 行为列表、Web/Blazor runtime。

## 历史迭代 4 implemented

历史迭代 4 已完成 Designer 预览侧的 WPF 动画与属性应用层：

- `FrontedRenderer` 现在会把 `FrontedControlConfigBase.BehaviorGuid` 写入生成控件的 `FrontedRendererProperties.BehaviorGuid` 附加属性。缺失插件占位控件在 Designer 预览中也会携带该 Guid；`Guid.Empty` 会原样保留，渲染时不会自动生成新 Guid。
- 新增 WPF 动画目标解析器。当前 action `Target` 字符串兼容 `Self`、原始 Guid、`guid:{...}`，并保留 registered name 作为显式 fallback。解析器只在当前预览 root/scope 内查找由 renderer 生成且 `BehaviorGuid` 匹配的控件；找不到目标时记录 warning 并跳过 action。
- 新增 `IFrontedAnimationRuntime`、动画执行上下文、属性 adapter registry 与内置 adapters。GraphRuntime 仍返回 历史迭代 3 的 `FrontedGraphActionRequest` 列表，同时 `FrontedGraphExecutionContext.ActionExecutor` 可在 action 节点执行点立即消费请求，因此 Delay 预览顺序不再被推迟到图执行结束后。
- Designer 动画编辑器的 Run Preview 会在可用时使用当前预览 Canvas、选中控件 `BehaviorGuid` 和选中控件名称创建 WPF 执行上下文。没有预览 scope 时会回退为 历史迭代 3 日志预览，并提示 “No preview target scope available.”。
- Reset 当前目标 / Reset all preview 会把本次预览 session 捕获到的基础视觉值恢复到 WPF 控件上。基础值只存在于 runtime 内存中，不写回 layout JSON 或 behaviors JSON。

动画动作现在包含 `Target`、`TargetLayer` 和 `PropertyName`。`Target` 先定位控件，`TargetLayer`
再决定属性施加到控件本体、内部内容，还是运行时生成的矩形承接层：

| TargetLayer | 含义 |
| --- | --- |
| `Auto` | 根据属性和控件结构选择默认层；编辑器会提示在效果不清晰时改为明确层。 |
| `Control` | 控件外层/root，适合整体显隐、透明度、尺寸和 transform。 |
| `Content` | 控件内部主内容；Text 类控件指向内部 `TextBlock`，Image / BorderedImage 指向主 `Image`，Shape 指向自身。 |
| `OverlayAbove` | 在控件上方懒创建运行时 `Rectangle` 承接层，不写入 layout 或 behaviors JSON。 |
| `OverlayBelow` | 在控件下方懒创建运行时 `Rectangle` 承接层，不写入 layout 或 behaviors JSON。 |

当前内置支持的属性：

| Adapter | 属性 |
| --- | --- |
| FrameworkElement | `Opacity`、`Visibility`、`Width`、`Height`、`VisualOffsetX`、`VisualOffsetY`、`ScaleX`、`ScaleY`、`Rotation` |
| Shape | `FillColor`、`StrokeColor`、`StrokeThickness` |
| TextBlock / Control | `TextColor`、`Foreground`、`FontSize` |
| BackgroundTintControlHost | `TintColor` |

`Image` / `BorderedImage` 运行时生成的锁图层和 PickingBorder 也可作为稳定动画目标。动画编辑器根据控件配置生成内置 part 目标：仅当 `Lockable = true` 时提供 `ControlName.LockOverlay`，仅当 `PickingBorderAvailable = true` 时提供 `ControlName.PickingBorder`，因此目标列表不依赖预览视觉树加载时机；视觉树辅助元素扫描只补充插件或自定义 part，并按目标引用去重。renderer 会为实际存在的内部覆盖层写入父控件 `BehaviorGuid`、父控件名称和稳定 part 名，持久化引用分别使用 `part:{BehaviorGuid}:LockOverlay` 和 `part:{BehaviorGuid}:PickingBorder`。plain `guid:{BehaviorGuid}` 仍只解析到顶层生成控件，不会误命中内部覆盖层；该解析规则同时用于 Designer 预览和真实前台运行时。

`AnimateProperty` 节点的 `WaitForCompletion` 属性默认为 `true`，执行将等待动画完成后才继续下一节点；设为 `false` 时动画启动后立即继续执行下一节点，不等待动画完成。

`VisualOffsetX/Y`、`ScaleX/Y`、`Rotation` 使用 `RenderTransform`，不会修改 `Canvas.Left` / `Canvas.Top`，也不会污染布局配置。`FillColor`、`StrokeColor`、`TextColor` 使用 `SolidColorBrush`，颜色值支持 `#RRGGBB` / `#AARRGGBB`。不支持的 `TargetLayer + PropertyName` 组合会记录 warning 并跳过，不抛出异常。

### 通用动画部件与 ClipInset

行为文档的 `ControlBehaviorSet.AnimationParts` 声明内部生成视觉部件。动画部件不是独立前台控件，
不进入主 Canvas 控件列表，也不写入 `FrontedLayouts/{Window}.json`；行为动画部件 renderer 会把它们放在父控件视觉树的内容上方或下方，并通过
`part:{BehaviorGuid}:{AnimationPartName}` 作为稳定动画目标。

当效果需要附着在控件内部的辅助视觉时使用动画部件。例如在 `SurPick0` 添加名为 `scanLine`
的矩形动画部件后，可以同时执行：

- `SurPick0.ClipInsetRight`：从 `100%` 动画到 `0%`，从左向右显示父控件内容。
- `SurPick0 / scanLine.VisualOffsetX`：从 `0%` 动画到 `100%`，让扫描线穿过父控件。

`ClipInsetLeft`、`ClipInsetTop`、`ClipInsetRight`、`ClipInsetBottom` 只改变裁剪区域，不改变
控件布局尺寸。动画部件的 `VisualOffsetX/Y` 百分比相对父控件宽高计算；普通控件的百分比相对
自身宽高计算。动画部件名称完全由用户定义，`shine`、`edge`、`wipeBar` 等名称使用同一管线。
动画部件支持可选通用 `Effect` 配置：`Kind=None` 不应用 WPF effect；`Glow` 和 `DropShadow` 通过 `DropShadowEffect` 实现，可配置 `Color`、`Opacity`、`BlurRadius`、`ShadowDepth` 和 `Direction`。辉光使用 `ShadowDepth=0`。效果有性能成本，默认不启用。

Loop 行为编辑器现在提供 Designer-only 生命周期预览：Preview Start、Preview Loop Once、Start Loop Preview、Stop Loop Preview、Preview Stop、Reset。Start Loop Preview 会先执行 `StartGraph`，再按 `LoopPolicy.RepeatCount` 与 `IntervalMs` 重复执行 `LoopGraph`；重复启动默认按 `ReentryPolicy.IgnoreIfRunning` 忽略，`InterruptPrevious` 会取消旧循环。Stop Loop Preview 会根据 `StopMode` 停止当前循环：`StopImmediately` 立即取消，`RunStopGraph` 取消后执行 `StopGraph`，`CompleteCurrentIteration` 请求当前轮完成后退出，并按 `ResetOnStop` 调用 reset。`AutoReverse` 当前只是配置占位符，尚未实现任意图的反向执行，该功能将在后续版本中提供。

历史迭代 4 仍不实现：真实 `IFrontedEventBus`、从 `SharedDataService` 事件自动触发、真实前台窗口赛事事件播放、插件自定义 animatable property、Timeline 编辑器、断点调试器、Canvas/window 级行为列表。

## 历史迭代 5 implemented

历史迭代 5 已完成真实事件总线 + 前台运行时接入，把行为系统从 Designer 预览扩展到真实前台窗口运行时：

### 事件总线

- 新增 `IFrontedEventBus` 接口与 `FrontedEventBus` 线程安全实现，支持 typed 和通配符（null）订阅。
- `FrontedBehaviorEvent` 模型包含 EventType、WindowId、WindowType、CanvasName、Timestamp、Payload、Source、IsPreview。
- Publish 异常不会打崩其他 handler，异常会记录日志。
- Subscribe 返回 `IDisposable`，支持 host/window 释放订阅。

### SharedDataService 事件桥接

- `FrontedSharedDataBehaviorEventBridge` 在应用启动时反射 `ISharedDataService` 上带 `FrontedBehaviorEventAttribute` 的事件。
- 对每个标注事件订阅真实 `ISharedDataService` 实例，事件触发时构造 `FrontedBehaviorEvent` 并 Publish 到 `IFrontedEventBus`。
- Payload 支持 `ServiceProperty`（从服务属性读取，支持嵌套路径）、`EventArgsProperty`（从 EventArgs 读取）、`SenderProperty`。
- 注册为 Singleton，Dispose 时取消所有订阅。

### Trigger 过滤器真实执行

- `FrontedBehaviorTriggerEvaluator` 判断 `TriggerDescriptor` 是否匹配 `FrontedBehaviorEvent`。
- EventType 必须一致，所有 Filter 全部通过才匹配。
- `Event.X` 从显式 Payload 取值，支持数值比较和文本比较。
- 复用 历史迭代 2 的 `FrontedTriggerFilterTextComparer`。

### Behavior Runtime

- `IFrontedBehaviorRuntime` 接口 + `FrontedBehaviorRuntime` Facade 实现。
- `FrontedBehaviorRuntimeHost` 持有单个 Canvas 的行为文档、事件总线订阅、运行中行为实例。
- `FrontedBehaviorRuntimeHostManager` Singleton 管理所有 Canvas 的 host 集合，以 `(windowId, canvasName)` 为 key。
- `FrontedBehavior` 新增 `ReentryPolicy` 属性（OneShot 重入策略），当前真实 runtime 支持 `InterruptPrevious` / `IgnoreIfRunning`；`Queue` / `AllowParallel` 暂按“运行中忽略”降级并记录 warning。

### OneShot 生命周期

- 事件到来时遍历所有 `ControlBehaviorSet` 中 Enabled 且 Kind == OneShot 的行为。
- Trigger 匹配后执行 behavior.Graph。
- 并发策略按 `ReentryPolicy` 处理：InterruptPrevious 取消旧执行，IgnoreIfRunning 跳过。Queue / AllowParallel 的完整实现将在后续版本中提供，当前真实 runtime 按跳过处理并记录 warning。

### Transition 生命周期

Transition 行为用于需要在业务数据变化前后分别执行动画的场景，例如角色 Pick 从旧角色视觉过渡到新角色视觉。它不是 Loop，也不是事件发生后的 OneShot。

执行顺序固定为：

```text
TransitionTrigger 匹配
  -> ExitGraph
  -> commitAsync（业务状态更新）
  -> EnterGraph
```

Transition 行为使用独立字段：

| 字段 | 含义 |
| --- | --- |
| `TransitionTrigger` | 过渡触发条件 |
| `ExitGraph` | 数据变化前运行，此时绑定仍能看到旧状态 |
| `EnterGraph` | 数据变化后运行，此时绑定能看到新状态 |
| `ReentryPolicy` | 过渡重入策略，真实 runtime 支持 `InterruptPrevious` / `IgnoreIfRunning` |

运行时通过 `IFrontedTransitionOrchestrator` 承接业务层提交：

```csharp
Task RunTransitionAsync(
    FrontedTransitionRequest request,
    Func<Task> commitAsync,
    CancellationToken cancellationToken = default);

Task RunMultiTargetTransitionAsync(
    IReadOnlyList<FrontedTransitionRequest> requests,
    Func<Task> commitAsync,
    CancellationToken cancellationToken = default);
```

如果没有匹配的 Transition 行为，`commitAsync` 会立即执行，不弹出提示，也不视为错误。`ExitGraph` 或 `EnterGraph` 执行失败只记录 warning；`ExitGraph` 失败后仍继续提交并尝试 `EnterGraph`。如果 `commitAsync` 失败，则不运行 `EnterGraph`，错误向调用方暴露。

当前接入的过渡类型：

| 过渡类型 | 用途 | 主要 payload |
| --- | --- | --- |
| `Selection.CharacterPick` | 求生者/监管者角色选择 | `Event.Camp`、`Event.PlayerIndex`、`Event.TargetBehaviorGuid`、`Event.OldCharacterId`、`Event.NewCharacterId`、`Event.HasOldCharacter`、`Event.HasNewCharacter` |
| `Selection.CharacterSwap` | 求生者角色交换 | `Event.SourceIndex`、`Event.TargetIndex`、`Event.SourceBehaviorGuid`、`Event.TargetBehaviorGuid` |

payload 使用稳定机器值，不使用本地化显示文本。图执行上下文会携带 transition payload，因此 `flow.if`、`value.eventValue` 等节点可以读取 `Event.Camp`、`Event.PlayerIndex`、`Event.HasOldCharacter` 等字段。

默认动画是行为包数据，不是编辑器模板。内置布局包可以在 `FrontedBehaviors/{WindowType}.behaviors.json` 中直接提供默认 Transition 行为；编辑器不提供内置动画模板、预设按钮或一键生成 fade/wipe 图。普通用户不需要打开动画编辑器，进阶用户可手动编辑图。

### Loop 生命周期

- 状态机：Stopped → Starting（StartGraph 执行） → Looping（LoopGraph 重复） → Stopping（StopGraph 执行） → Stopped。
- `StartTrigger` 匹配启动，`StopTriggers` 中任意一个 trigger 匹配即停止。单个 trigger 内 filters 仍为 AND；多个 `StopTriggers` 之间为 OR。
- 支持 `StopMode`（StopImmediately / RunStopGraph / CompleteCurrentIteration / HoldCurrentState）、`RepeatCount`、`IntervalMs`、`ResetOnStop`。`CompleteCurrentIteration` 不取消当前 `LoopGraph`，当前轮执行完成后执行 `StopGraph`，然后 reset/cleanup。
- `LoopPhase` 状态机追踪生命周期阶段：`Starting`（StartGraph 执行中）→ `Looping`（LoopGraph 循环中）→ `Stopping`（StopGraph 执行中）→ `Stopped`（已清理）。
- 同一 key（WindowId + CanvasName + BehaviorGuid + BehaviorId）不会启动多个 loop 实例。

### GraphRuntime + AnimationRuntime 集成

- `FrontedAnimationRuntimeActionExecutor` 包装 `IFrontedAnimationRuntime` 作为 `IFrontedGraphActionExecutor`。
- GraphRuntime 的 Action 节点在真实前台 Canvas 上调用 AnimationRuntime 执行动画。
- `IFrontedAnimationRuntime.Release(FrameworkElement root)` 释放 root 的 runtime session，避免内存泄漏。

### 前台窗口接入

所有 7 个前台窗口已集成 `IFrontedBehaviorRuntime`：
| 窗口 | 特征 | 状态 |
| --- | --- | --- |
| BpWindow | 单 Canvas | 已集成 |
| ScoreSurWindow | 单 Canvas | 已集成 |
| ScoreHunWindow | 单 Canvas | 已集成 |
| ScoreGlobalWindow | 单 Canvas，有 reload guard | 已集成 |
| GameDataWindow | 单 Canvas | 已集成 |
| CutSceneWindow | 单 Canvas | 已集成 |
| BpOverviewWindow / MapV2Window | Window-centric v3 layout host | 已集成 |

集成模式：v3 host 首次 Show 后异步 `LoadOrReloadContentAsync(force:false)`，需要渲染时先 detach 旧 host，再 `RenderToCanvas`，渲染后 attach 新 host；如果内容已渲染且 layout 未标脏，下一次 Show 只重新 attach behavior runtime，不重新渲染控件。窗口 Hide/Unloaded/Closed 时可 detach behavior runtime，但不清空已渲染 content，也不把 layout 标脏。

### 窗口生命周期事件

- `FrontedWindowService.ShowWindow()` → 发布 `WindowShown`
- `FrontedWindowService.HideWindow()` → 发布 `WindowHidden`
- Host attach 后 → 发布 `CanvasLoaded`
- `IFrontedBehaviorRuntime.PublishManualTrigger()` → 发布 `ManualTrigger`

### bpui 包导出

- `FrontedLayoutPackageExporter.ExportAsync()` 现在包含 behaviors 文件导出。
- behavior 文件路径：`FrontedBehaviors/{WindowType}.behaviors.json`。

### 已注册的 DI 服务

| 服务 | 生命周期 | 文件 |
| --- | --- | --- |
| `IFrontedEventBus` / `FrontedEventBus` | Singleton | Core |
| `FrontedBehaviorTriggerEvaluator` | Singleton | Core |
| `FrontedBehaviorRuntimeHostManager` | Singleton | Core |
| `IFrontedBehaviorRuntime` / `FrontedBehaviorRuntime` | Singleton | Core |
| `FrontedSharedDataBehaviorEventBridge` | Singleton（应用启动时显式 Start，Start 幂等） | Core |

### 历史迭代 5 仍不实现

- Timeline 编辑器
- 插件自定义节点
- 插件自定义 animatable property
- Messenger adapter
- 断点 debugger
- Web/Blazor runtime
- Queue / AllowParallel 重入策略的完整实现
- Loop AutoReverse 图反向执行
- 内置包 behaviors 支持

## 历史迭代 5.5 implemented

历史迭代 5.5 完成行为系统可用性打磨、事件覆盖和节点属性编辑器增强：

- 行为事件目录现在来自 `ISharedDataService`、`ICharacterSelectionService`、`IGameGuidanceService` 三类显式标注接口。`Selection.CharacterSelected` / `Selection.CharacterBanned` 可作为 Trigger，并提供 `Event.Camp`、`Event.PlayerIndex` 过滤字段。
- 事件 bridge 仍复用应用启动时的 Singleton bridge，但内部按服务源发布事件，`Source` 分别为 `SharedDataService`、`CharacterSelectionService`、`GameGuidanceService`；`Start()` 幂等，`Dispose()` 会解除订阅。
- `GameGuidanceService` 暴露语义事件，行为编辑器不直接消费 Messenger。高亮变化和清除事件只服务后台页面滚动和高亮；前台行为系统使用 `Guidance.Started`、`Guidance.Stopped`、`Guidance.Cancelled` 和 `Guidance.StepChanged`。
- `Guidance.StepChanged` 同时提供当前步骤和上一步骤 payload；当前步骤可用于启动动画，`Event.PreviousAction`、`Event.PreviousIndexesText` 等上一步骤 payload 可用于停止由切换前步骤启动的动画。首次进入步骤时 `Previous*` 值为 `null`，`Event.PreviousIndexesText` 为 `[]`。
- `Guidance.StepChanged` 提供稳定的 `Event.IndexesText`。列表字符串过滤应优先使用 `Event.IndexesText` / `Event.PreviousIndexesText`，格式为 `[1, 2]`，不要依赖集合默认 `ToString()`。
- 推荐用 Behavior Loop 实现引导高亮/呼吸灯：`StartTrigger` 使用 `Guidance.StepChanged` 并按显式事件 payload 过滤；`StopTriggers` 可同时包含 `Guidance.StepChanged` 上一步 payload 过滤、`Guidance.Cancelled` 和 `Guidance.Stopped`。这样引导切步、取消和停止都能结束循环动画。

示例：

```text
Start survivor breathing light:
Event.Action == PickSur
Event.IndexesText contains "1"

Stop survivor breathing light:
Event.PreviousAction == PickSur
Event.PreviousIndexesText contains "1"
```
- 临时行为标签已移除。行为过滤使用显式事件 payload 和稳定的 `BehaviorGuid` 控件身份。
- 节点属性编辑器增加类型化 editor：`Visibility` 使用枚举下拉，数值属性使用 numeric 输入和 validator，颜色属性使用 ColorPicker + 文本输入，`PropertyName` 使用可编辑属性选择器，`Target` 使用 `Self` / Canvas 控件目标选择器并保存 `guid:{BehaviorGuid}`。
- 颜色输入统一支持 `#RRGGBB`、`#AARRGGBB` 和 WPF named colors（如 `White`、`Black`、`Transparent`、`DodgerBlue`），提交后统一存储为 `#AARRGGBB`。非法颜色只显示验证错误，不覆盖旧有效值。
- Graph Validator 会按 `PropertyName` 验证 `AnimateProperty` / `SetProperty` / `ResetProperty`：数值必须有限，`Opacity` / `TintStrength` / `TextureStrength` 为 `0..1`，颜色可解析，`Visibility` 必须是 `Visible` / `Hidden` / `Collapsed`。验证消息显示在编辑器中，不阻止保存。
- 行为卡片提供轻量“测试触发”入口，当前发布 `ManualTrigger` 用于验证行为连通性，不修改真实比赛状态。

## 历史迭代 2 UX / event catalog update

### Attribute-driven event catalog

行为编辑器不再维护硬编码事件列表。`ISharedDataService` 中只有显式标记
`FrontedBehaviorEventAttribute` 的事件会进入 `FrontedBehaviorEventCatalog`；未标记事件不会暴露。
`FrontedBehaviorEventPayloadAttribute` 描述可用于过滤器的 payload 路径、显示名、类型和未来 runtime
取值来源（服务属性、事件参数属性等）。目录通过反射构建一次并缓存，按分类、顺序和显示名稳定排序。

事件名、分类名和 payload 参数名都通过本地化 key 展示；行为 JSON 仍保存稳定的原始
`EventType` 与 `Event.*` 路径。新增共享数据事件时，应先判断它是否具有前台动画语义，再决定是否标注，
不要盲目把所有服务事件加入目录。

行为事件 payload 只允许携带稳定、机器可读的值，例如 enum 值、稳定 ID、数字、bool、不可变协议字符串和技术格式字符串。payload 不得携带本地化 UI 显示文本，因为语言包和 UI culture 变化会让过滤逻辑失效。`Guidance.StepChanged` 使用 `Event.Action` / `Event.PreviousAction` 表示 `GameAction` enum，过滤器应写 `Event.Action Equals PickSur` 或 `Event.PreviousAction Equals PickSur`。索引列表的字符串过滤应使用 `Event.IndexesText` / `Event.PreviousIndexesText`，例如 `Event.IndexesText Contains 0`。不要使用 `ActionName` / `PreviousActionName` 作为行为过滤字段；需要显示本地化操作名称时，由后台 UI 或调试器根据 `GameAction` 在显示时计算，不写入 `FrontedBehaviorEvent.Payload`。

`FrontManagePage` 提供独立的“行为事件调试器”窗口。该窗口直接订阅全局 `IFrontedEventBus`，不依赖动画编辑器、特定 behavior、`FrontedBehaviorRuntimeHost` 或已打开的前台窗口。调试器用于确认事件是否到达、收到的 `EventType`、payload key、原始值、显示值和可复制到过滤器中的稳定文本；它支持启用/暂停监听、清空记录、最大记录数限制、复制单个事件 JSON、导出 JSON，并提供复制路径、Equals 过滤器、Contains 过滤器和值的辅助操作。

`FrontManagePage` 还提供“停止所有循环动画”安全按钮。它调用行为运行时的 stop-all API，按 `ManualClear` 原因停止当前活动 loop，优先执行每个行为配置的 `StopGraph` 做清理，并在超时后强制清除活动状态。普通用户通常不需要编辑行为数据；内置 behavior package 应包含正确的 `StopTriggers`，该按钮只作为漏配或事件丢失时的兜底。

### Filter rule builder

过滤器 UI 使用面向用户的规则行：`当 [参数] [运算符] [文本值]`。左侧参数来自当前事件的 payload
下拉框，运算符显示为 `=`、`>`、`<`、`≥`、`≤`、`包含`、`不包含` 等可读符号/文本，右侧始终是普通文本。
`Source` 和兼容旧 历史迭代 2 JSON 的 `RightValueKind` 不在正常 UI 中显示。

未来 runtime 会将左值通过 `ToString()` 转为文本。等于和包含比较忽略大小写；大小比较在两侧都能按
Invariant Culture 解析为 decimal 时使用数值比较，否则回退为 ordinal 文本比较。一个 Trigger 的所有
过滤条件必须全部通过；任意条件失败都跳过动画。切换事件时不会静默删除旧过滤条件，找不到的路径会作为
“未知参数”保留并显示。

### Messenger policy

行为编辑器目录不直接暴露 Messenger message。行为系统消费具有前台语义的 `FrontedBehaviorEvent`；
未来可以通过 adapter 将 Messenger message 包装为语义事件。这样可以把 UI/MVVM 基础设施消息与前台行为
语义分开。未来 adapter 可以在 message 类型或 adapter 方法上复用相同的事件元数据属性，但本阶段只处理
`ISharedDataService` 的反射目录，不实现 Messenger adapter。

### Animation editor placeholder and scrolling

行为卡片提供“打开动画编辑器”入口。OneShot 显示单个图占位；Loop 使用 Top NavigationView /
`LocalTabs` 在开始动画、循环动画、结束动画之间切换，并显示当前节点/连线数量。真实节点图编辑和动画
runtime 仍属于 历史迭代 3。

BehaviorPanel 必须让 Designer 右侧的外层 ScrollViewer 负责滚动。行为列表和过滤器列表使用
`ItemsControl` + `Expander`/卡片，不在面板内部使用 `ListBox`、`ListView` 或额外 ScrollViewer，避免嵌套滚动。

---

## 目录索引

- [1. 关键源码地图](#1-关键源码地图)
- [2. 当前 Designer v3 数据流](#2-当前-designer-v3-数据流)
- [3. BehaviorGuid 接入点](#3-behaviorguid-接入点)
- [4. behaviors 文件接入点](#4-behaviors-文件接入点)
- [5. 行为列表 UI 接入点](#5-行为列表-ui-接入点)
- [6. 事件总线候选来源](#6-事件总线候选来源)
- [7. 历史迭代 1 建议实施步骤](#7-phase-1-建议实施步骤)
- [8. 历史迭代 1 建议测试清单](#8-phase-1-建议测试清单)
- [9. 不建议 历史迭代 1 做的事情](#9-不建议-phase-1-做的事情)
- [10. 开放问题](#10-开放问题)

---

## 1. 关键源码地图

| 文件路径 | 作用 | 和 Behavior 系统的关系 | 建议改动阶段 |
| --- | --- | --- | --- |
| `Core/Models/FrontedLayout/FrontedControlConfigBase.cs` | 所有控件配置基类 | **加 `BehaviorGuid` 的目标基类** | 历史迭代 1 |
| `Core/Models/FrontedLayout/Designer/FrontedControlDesignItem.cs` | 设计时控件项包装 | 持有 Config 引用；编辑器选中状态在此 | 历史迭代 1 |
| `Core/Models/FrontedLayout/Designer/FrontedCanvasDesignDocument.cs` | 单 Canvas 设计文档 | IsDirty、Controls 集合 | 历史迭代 1 |
| `ViewModels/Windows/FrontedDesignerWindowViewModel.cs` | Designer 主 VM（4945 行） | **AddControl/Paste/Delete 入口都在这里** | 历史迭代 1 |
| `Views/Windows/FrontedDesignerWindow.xaml` | Designer 窗口布局 | 右侧属性面板结构（Row 2 = PropertyGrid） | 历史迭代 2 |
| `Views/Windows/FrontedDesignerWindow.xaml.cs` | Designer code-behind（3081 行） | 预览元素注册、对话框、交互辅助 | 历史迭代 1-2 |
| `Core/Services/FrontedLayout/FrontedControlDefaultConfigFactory.cs` | 默认控件工厂 | 新建控件时确定 BehaviorGuid | 历史迭代 1 |
| `Core/Services/FrontedLayout/FrontedControlNameGenerator.cs` | 控件名称生成器 | 不相关（Guid ≠ Name） | 无关 |
| `Core/Services/FrontedLayout/FrontedLayoutDesignConverter.cs` | Config ↔ DesignDocument 转换 | 转换时需保留 BehaviorGuid | 历史迭代 1 |
| `Core/Models/FrontedLayout/Json/FrontedCanvasConfigJsonConverter.cs` | JSON 反/序列化转换器 | 读/写时需透传 BehaviorGuid | 历史迭代 1 |
| `Core/Services/FrontedLayout/FrontedRenderer.cs` | 前台运行时渲染 | **AnimationTargetResolver 应接在这里** | 历史迭代 3+ |
| `Core/Services/FrontedLayout/FrontedRendererProperties.cs` | 附加属性（IsGeneratedControl/RegisteredName） | RegisteredName → FrameworkElement 映射 | 历史迭代 3+ |
| `Core/Abstractions/Services/IFrontedControl.cs` | 控件工厂接口 | Create() 返回 FrameworkElement | 历史迭代 3+ |
| `Core/Services/FrontedLayout/FrontedControlRegistry.cs` | 控件注册表 | 运行时按 ControlType 查找工厂 | 历史迭代 3+ |
| `Core/Services/FrontedLayout/FrontedPropertyGridBuilder.cs` | PropertyGrid 构造器 | **需跳过 BehaviorGuid**（不显示给用户） | 历史迭代 1 |
| `Services/SharedDataService.cs` | 共享数据服务（事件核心） | 事件总线候选来源 | 历史迭代 3+ |
| `Core/Abstractions/Services/ISharedDataService.cs` | 共享数据接口 | 事件声明（12 个事件） | 历史迭代 3+ |
| `Services/CharacterSelectionService.cs` | 角色选择服务 | CharacterSelected/CharacterBanned 事件 | 历史迭代 3+ |
| `Core/Services/FrontedLayout/FrontedLayoutPackageExporter.cs` | bpui 导出器 | behaviors 文件导出点 | 历史迭代 3 |
| `Core/Services/FrontedLayout/FrontedLayoutPackageImporter.cs` | bpui 导入器 | behaviors 文件导入点 | 历史迭代 3 |
| `Core/Services/FrontedLayout/FrontedLayoutPackageManager.cs` | 包管理器 | 删除/复制包的 behaviors 联动 | 历史迭代 3 |
| `Core/Models/FrontedLayout/PackageModels/FrontedLayoutPackageManifest.cs` | manifest 模型 | 可能需要 HasBehaviors/RequiredNodePlugins 字段 | 历史迭代 3 |
| `Tests/Models/FrontedLayoutDesignerFoundationTest.cs` | Designer 核心测试（5103 行） | 新增测试加在此处 | 历史迭代 1 |
| `Tests/Models/FrontedCanvasConfigTest.cs` | Canvas JSON 往返测试（2728 行） | 新增 BehaviorGuid JSON 透传测试 | 历史迭代 1 |
| `Tests/Services/FrontedLayoutPackageManagerTest.cs` | bpui 导入导出测试（1660 行） | 新增 behaviors 文件导入导出测试 | 历史迭代 3 |

---

## 2. 当前 Designer v3 数据流

### 2.1 布局如何加载

1. `FrontedDesignerWindowViewModel.ReloadLayoutCoreAsync()` → `_layoutService.LoadCanvasConfigWithMetadataAsync(windowType, canvasName)`
2. `FrontedLayoutService` 从用户布局路径 → 内置默认路径读取 JSON
3. `FrontedCanvasConfigJsonConverter.Read()` 反序列化为 [`FrontedCanvasConfig`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf.Core/Models/FrontedLayout/FrontedCanvasConfig.cs)（`Controls` 是 `Dictionary<string, FrontedControlConfigBase>`）
4. [`FrontedLayoutDesignConverter.FromConfig()`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedLayoutDesignConverter.cs) 转换为 [`FrontedCanvasDesignDocument`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf.Core/Models/FrontedLayout/Designer/FrontedCanvasDesignDocument.cs)（`Controls` 是 `ObservableCollection<FrontedControlDesignItem>`）
5. 设置 `CurrentDocument.IsDirty = false`

### 2.2 控件如何进入文档

- **新建**: `AddControl` 命令 → [`FrontedControlDefaultConfigFactory.Create()`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedControlDefaultConfigFactory.cs) → 创建 config + 生成 Name → 包装为 `FrontedControlDesignItem` → 加入 `CurrentDocument.Controls` → `IsDirty = true`
- **粘贴**: `PasteControl` 命令 → `FrontedDesignerClipboardPayload.CreateConfig()` 深拷贝 config → Left/Top +10, ZIndex max+1 → 生成新 Name → 包装为 `FrontedControlDesignItem` → 加入集合
- **从 JSON 加载**: 由 `FrontedLayoutDesignConverter.FromConfig()` 批量创建

### 2.3 选中控件如何维护

- [`SelectedDesignItem`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf/ViewModels/Windows/FrontedDesignerWindowViewModel.cs#L329) 属性（`[ObservableProperty]`）
- `OnSelectedDesignItemChanged` 分部方法同步 `IsSelected` 状态、刷新 PropertyGrid、更新预览选中框
- 左侧 Layer Panel 的 `IsSelected` 绑定到 `DesignerLayerNode.IsSelected` → 关联 `ControlItem.IsSelected`

### 2.4 控件如何保存

1. `SaveCurrentLayoutAsync()` → `_designConverter.ToConfig(CurrentDocument)` → [`FrontedCanvasConfig`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf.Core/Models/FrontedLayout/FrontedCanvasConfig.cs)（`Controls` 变回 `Dictionary<string, FrontedControlConfigBase>`，key = Name）
2. 设置 `config.Version = 3` → `_layoutService.SaveCanvasConfigAsync()`
3. 通过 `FrontedCanvasConfigJsonConverter.Write()` 序列化为 JSON → 写入磁盘
4. `IsDirty = false`

### 2.5 控件如何导出到 bpui

1. `FrontedLayoutPackageExporter.ExportLayoutsAsync()` → 加载每个窗口/Canvas 配置
2. 写入 staging 目录 `layouts/{WindowType}/{CanvasName}.json`
3. 资源文件（图片）复制到包内 `resources/` 并重写 URI
4. 打包为 `.bpui` (zip)

---

## 3. BehaviorGuid 接入点

### 3.1 应该改哪个基类

**[`FrontedControlConfigBase`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf.Core/Models/FrontedLayout/FrontedControlConfigBase.cs)** 是唯一正确的位置。原因：

- 所有 13 个内置控件 + 插件控件都继承自此基类
- JSON 序列化器 `Write()` 使用 `JsonSerializer.Serialize(writer, control, control.GetType())`，基类属性自动写入所有子类
- JSON 反序列化器 `ReadControl()` 按 ControlType 分派后，基类属性由 `JsonSerializer.Deserialize(ref reader, ...)` 自动反填充
- PropertyGridBuilder 需要主动跳过该属性

```csharp
// 建议新增字段
/// <summary>
/// 行为系统内部使用的控件标识符。用户不可编辑。
/// PropertyGrid 不应显示此字段。重命名控件时不改变此值。
/// 复制控件时直接重新生成新 Guid。
/// </summary>
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
public Guid BehaviorGuid { get; set; }
```

**`JsonIgnoreCondition.WhenWritingDefault`** 保证默认值 `Guid.Empty` 不会被序列化到旧 JSON 中，实现向前兼容。

### 3.2 新建控件在哪里生成

在 `AddControl` 方法中创建 config 之后，应在 [`FrontedControlDefaultConfigFactory.Create()`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedControlDefaultConfigFactory.cs) 内部直接设置 `config.BehaviorGuid = Guid.NewGuid()`。这样所有通过工厂创建的控件默认就有 BehaviorGuid。

反序列化得到的 config 中 `BehaviorGuid == Guid.Empty` 是合法的（旧布局没有此字段），不应影响运行时渲染。

### 3.3 复制控件在哪里重新生成

在 [`PasteControl`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf/ViewModels/Windows/FrontedDesignerWindowViewModel.cs#L1193) 中，`copiedControl.CreateConfig()` 之后立即覆盖：

```csharp
var config = copiedControl.CreateConfig();
config.BehaviorGuid = Guid.NewGuid();  // 重新生成
```

**注意**: `CopyBo5ToBo3` 使用的 `CloneControls` 不应重置 Guid — 同一控件的不同 BO 状态应共享同一 BehaviorGuid。

### 3.4 删除控件在哪里清理 behavior

在 [`DeleteSelectedControl`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf/ViewModels/Windows/FrontedDesignerWindowViewModel.cs#L1251) 中，从 `CurrentDocument.Controls.Remove` 之后追加：

```csharp
if (selectedItem.Config.BehaviorGuid != Guid.Empty)
    _behaviorService.RemoveBehaviors(selectedItem.Config.BehaviorGuid);
```

历史迭代 1 中 `_behaviorService` 可以是空实现（NoopBehaviorService），清理入口只做预留调用。

### 3.5 重命名为什么不影响 BehaviorGuid

重命名只更改 `FrontedControlDesignItem.Name` 属性，本质上是 `FrontedCanvasConfig.Controls` 字典的 key 变更。BehaviorGuid 在 Config 内部，与 Name 完全独立。保存时 `ToConfig` 将 Name 写回字典 key，Config 序列化时 BehaviorGuid 自然跟随着控件属性 JSON 输出。

---

## 4. behaviors 文件接入点

### 4.1 建议文件路径

```
FrontedBehaviors/{WindowType}.behaviors.json
```

镜像 `FrontedLayouts/` 的窗口级文件结构，放在独立的 `FrontedBehaviors/` 根目录下。在 bpui 包内的路径为 `FrontedBehaviors/BpWindow.behaviors.json`。

### 4.2 保存位置

```
%APPDATA%/neo-bpsys-wpf/FrontedLayoutPackages/{PackageId}/FrontedBehaviors/{WindowType}.behaviors.json
```

### 4.3 导入位置

在 [`FrontedLayoutPackageImporter.ImportAsync()`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedLayoutPackageImporter.cs) 中，解压 staging 目录后检查 `FrontedBehaviors/` 目录是否存在，若存在则将整个目录复制到安装路径。

### 4.4 导出位置

在 [`FrontedLayoutPackageExporter.ExportLayoutsAsync()`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedLayoutPackageExporter.cs) 之后追加：读取当前包下的 behaviors 文件，若存在则复制到 staging 的 `FrontedBehaviors/` 目录。

### 4.5 和 manifest 的关系

[`FrontedLayoutPackageManifest`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf.Core/Models/FrontedLayout/PackageModels/FrontedLayoutPackageManifest.cs) 建议增加两个可选字段：

```json
{
  "hasBehaviors": true,
  "requiredNodePlugins": ["pluginId1", "pluginId2"]
}
```

- `hasBehaviors`: 标记包是否包含行为数据
- `requiredNodePlugins`: 行为节点图依赖的插件节点包

---

## 5. 行为列表 UI 接入点

### 5.1 当前右侧面板结构

[`FrontedDesignerWindow.xaml`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf/Views/Windows/FrontedDesignerWindow.xaml) 右侧面板（Grid.Column="3"）当前结构：

| 行 | 内容 |
| --- | --- |
| Row 0 | 选中控件摘要信息（Name, Type, Geometry, RuntimeCritical, Validation） |
| Row 1 | Polygon 顶点编辑器（条件显示） |
| Row 2 | **Property Grid**（ItemsControl\<FrontedPropertyEditorItem\>） |
| Row 3 | Canvas Properties（Expander） |
| Row 4 | Window Options（Expander） |

没有 tab 机制，所有内容垂直排列在 ScrollViewer 中。

### 5.2 最适合新增的 View / ViewModel

在 Property Grid（Row 2）下方新增一个 **Expander**，名为"Behaviors / 动画/行为"：

```
Row 2:   Property Grid（现有）
Row 2.5: Behaviors Panel（新增 Expander，可折叠）
           ├── 行为列表（ListView/ItemsControl）
           │     ├── 行为类型图标（OneShot/Loop）
           │     ├── 行为名称/摘要
           │     ├── 触发事件名
           │     └── 删除按钮
           ├── "添加单次行为" 按钮
           ├── "添加循环行为" 按钮
           └── （选中行为时展开详细编辑区）
Row 3:   Canvas Properties（现有）
Row 4:   Window Options（现有）
```

**ViewModel**: 建议新建独立的 `BehaviorPanelViewModel`，在 [`FrontedDesignerWindowViewModel`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf/ViewModels/Windows/FrontedDesignerWindowViewModel.cs) 中持有其实例，避免 VM 进一步膨胀（已 4945 行）。

### 5.3 需要哪些 commands

| 阶段 | Command | 说明 |
| --- | --- | --- |
| 历史迭代 1 | `AddOneShotCommand` | 为选中控件添加空 OneShot 行为 |
| 历史迭代 1 | `AddLoopCommand` | 为选中控件添加空 Loop 行为 |
| 历史迭代 1 | `DeleteBehaviorCommand` | 删除选中的行为 |
| 历史迭代 1 | `SelectBehaviorCommand` | 选中某项行为以显示编辑区域 |
| 历史迭代 2 | `EditTriggerCommand` | 打开事件选择器 |
| 历史迭代 2 | `EditFilterCommand` | 打开事件过滤器编辑 |
| 历史迭代 3+ | `OpenNodeGraphCommand` | 打开节点图编辑器 |

### 5.4 Dirty tracking 如何接

建议在 `FrontedDesignerWindowViewModel` 中新增独立属性 `AreBehaviorsDirty: bool`，与 `CurrentDocument.IsDirty` 分开跟踪。保存按钮同时检查两者。这样可以独立保存 layout vs behaviors，避免把 behavior 数据和 layout 数据耦合。

---

## 6. 事件总线候选来源

### 6.1 SharedDataService 当前事件（[ISharedDataService](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf.Core/Abstractions/Services/ISharedDataService.cs)）

| 事件名 | Payload 应包含 | 已定参数 |
| --- | --- | --- |
| `CurrentGameChanged` | Game 实例 | `EventHandler` |
| `PickedMapChanged` | 地图名 | `EventHandler` |
| `MapV2BannedChanged` | 地图名 + 禁用状态 | `EventHandler` |
| `IsBo3ModeChanged` | bool | `EventHandler` |
| `TeamSwapped` | 无额外数据 | `EventHandler` |
| `GlobalScoreTotalMarginChanged` | double | `EventHandler` |
| `IsTraitVisibleChanged` | bool | `EventHandler` |
| `CountDownValueChanged` | 剩余秒数字符串 | `EventHandler` |
| `BanCountChanged` | BanListName + Index | `BanCountChangedEventArgs` |

### 6.2 CharacterSelectionService 事件（[CharacterSelectionService](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf/Services/CharacterSelectionService.cs)）

| 事件名 | Payload | 已定参数 |
| --- | --- | --- |
| `CharacterSelected` | Camp + PlayerIndex | `CharacterSelectedEventArgs` |
| `CharacterBanned` | Camp + PlayerIndex | `CharacterBannedEventArgs` |

### 6.3 需要新增语义事件的事件

| 缺失事件 | 来源 | 说明 |
| --- | --- | --- |
| `FrontedWindowShown` / `FrontedWindowHidden` | `FrontedWindowService` | 前台窗口生命周期，当前没有事件 |
| `MatchScoreChanged` | `Game.MatchScore` | 比分变化，当前通过 `INotifyPropertyChanged` 级联传播 |
| 布局重新渲染完成 | `FrontedWindowService.ReloadFrontedLayoutsAsync()` | 渲染完成通知 |

**关于 IFrontedEventBus**: 历史迭代 1 不需要真正的 EventBus 实现。历史迭代 3+ 时建议用适配器模式包装现有 `ISharedDataService` 和 `CharacterSelectionService` 的事件，统一转换为强类型 `FrontedEvent`。

---

## 7. 历史迭代 1 建议实施步骤

### 步骤 1: `FrontedControlConfigBase` 加 `BehaviorGuid`

在基类中新增 `Guid BehaviorGuid { get; set; }`，注明 `JsonIgnoreCondition.WhenWritingDefault`。验证新建布局 JSON 序列化时 `Guid.Empty` 不出现。

### 步骤 2: 默认工厂中自动生成 BehaviorGuid

在 [`FrontedControlDefaultConfigFactory.Create()`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedControlDefaultConfigFactory.cs) 末尾设置 `config.BehaviorGuid = Guid.NewGuid()`。验证 `AddControl` 创建的控件 Guid ≠ Empty。

### 步骤 3: 复制控件时重新生成 BehaviorGuid

在 [`PasteControl()`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf/ViewModels/Windows/FrontedDesignerWindowViewModel.cs#L1193) 中 `CreateConfig()` 后追加 `config.BehaviorGuid = Guid.NewGuid()`。验证粘贴的控件与源控件 Guid 不同。

### 步骤 4: PropertyGrid 排除 BehaviorGuid

在 [`FrontedPropertyGridBuilder.AddConfigRows()`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedPropertyGridBuilder.cs) 中跳过名为 `BehaviorGuid` 的属性。验证选中任何控件，右侧面板不出现 BehaviorGuid 行。

### 步骤 5: 删除控件时清理入口

在 [`DeleteSelectedControl`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf/ViewModels/Windows/FrontedDesignerWindowViewModel.cs#L1251) 中添加 `_behaviorService.RemoveBehaviors(guid)` 的预留调用。历史迭代 1 中 `_behaviorService` 可以是 NoopBehaviorService。

### 步骤 6: BehaviorGuid JSON 往返验证

在 [`FrontedCanvasConfigTest.cs`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf.Tests/Models/FrontedCanvasConfigTest.cs) 中新增测试：序列化含 BehaviorGuid 的 config → 反序列化 → Guid 值一致；旧 JSON（无 Guid 字段）反序列化后 `BehaviorGuid == Guid.Empty`。

### 步骤 7: 核心模型类定义（纯数据类，不含序列化/持久化）

在 `Core/Models/FrontedLayout/Behaviors/` 目录下定义：

- `FrontedBehaviorDocument` — 行为文档根
- `ControlBehaviorSet` — 单控件的行为集合，以 BehaviorGuid 为 key
- `FrontedBehavior` — 单个行为（OneShot 或 Loop）
- `FrontedBehaviorKind` — 枚举：OneShot, Loop
- `TriggerDescriptor` — 触发事件描述（事件名 + 来源）
- `TriggerFilter` — 事件过滤器（条件表达式等占位）
- `FrontedNodeGraph` — 节点图容器
- `FrontedNode` — 单个节点
- `FrontedNodeConnection` — 节点连线
- `LoopPolicy` — 循环策略（次数/持续时间等）
- `ReentryPolicy` — 重入策略
- `FillBehavior` — 填充行为

**注意**: 只定义模型，不实现节点图执行、不实现持久化。

### 步骤 8: 模型层单元测试

- 模型默认值、属性设置、简单 JSON 序列化/反序列化
- `ControlBehaviorSet` 与 `BehaviorGuid` 的关联查找

### 步骤 9: 更新 Designer 设计文档

- 更新 [`docs/fronted-designer-v3.md`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/docs/fronted-designer-v3.md) 增加 BehaviorGuid 和行为系统章节
- 本报告即 历史迭代 0 勘察记录，作为交接参考

---

## 8. 历史迭代 1 建议测试清单

| # | 测试名称 | 目的 | 测试文件 |
| --- | --- | --- | --- |
| 1 | `BehaviorGuid_NewControlViaFactory_HasNonEmptyGuid` | 工厂创建一定有 Guid | `FrontedLayoutDesignerFoundationTest.cs` |
| 2 | `BehaviorGuid_CloneConfigViaClipboard_GuidIsNew` | 粘贴时 Guid 重新生成 | `FrontedLayoutDesignerFoundationTest.cs` |
| 3 | `BehaviorGuid_PropertyGridDoesNotShowGuidRow` | PropertyGrid 跳过 Guid | `FrontedLayoutDesignerFoundationTest.cs` |
| 4 | `BehaviorGuid_JsonRoundTrip_PreservesGuid` | JSON 往返保持 Guid | `FrontedCanvasConfigTest.cs` |
| 5 | `BehaviorGuid_OldJsonMissingGuid_DeserializesAsEmpty` | 旧 JSON 兼容 | `FrontedCanvasConfigTest.cs` |
| 6 | `BehaviorGuid_NewJsonWithEmptyGuid_NotSerialized` | 空 Guid 不写入 JSON | `FrontedCanvasConfigTest.cs` |
| 7 | `ControlBehaviorSet_LookupByGuid_ReturnsCorrectSet` | 查 BehaviorGuid 对应集合 | 新增 Behaviors/ 测试 |
| 8 | `FrontedBehavior_OneShotDefaults_AreSane` | OneShot 模型默认值 | 新增 Behaviors/ 测试 |
| 9 | `FrontedBehavior_LoopDefaults_AreSane` | Loop 模型默认值 | 新增 Behaviors/ 测试 |
| 10 | `FrontedNodeGraph_EmptyGraph_IsValid` | 节点图模型基础验证 | 新增 Behaviors/ 测试 |
| 11 | `DeleteControl_TriggersBehaviorCleanupCall` | 删除时清理入口被调用 | `FrontedLayoutDesignerFoundationTest.cs` |

---

## 9. 不建议 历史迭代 1 做的事情

| 事项 | 原因 |
| --- | --- |
| **节点图 UI 编辑器** | 需要完整 Graph Canvas + 节点拖放 + 连线，至少 历史迭代 2 |
| **真实动画 runtime** | 需要集成 WPF 动画引擎，涉及性能调试 |  |
| **真实事件总线** | 强行抽象 EventBus 可能过度设计；用现有事件机制即可 |  |
| **插件节点系统** | 节点图引擎未成型时抽象插件接口会反复修改 |  |
| **Timeline 编辑器** | Timeline 是 Loop 行为的可视化增强，非 MVP 必需品 |  |
| **复杂 debugger/可视化** | 运行时的调试工具代价高，初期用日志 + 诊断即可 |  |
| **behaviors 文件导入导出** | 依赖 历史迭代 1 的模型定义和 历史迭代 2 的 UI 编辑能力 |  |
| **behaviors 文件实际持久化** | 历史迭代 1 只定义模型和 BehaviorGuid 基础设施，不写入磁盘 |  |
| **manifest 扩展** | 等 behaviors 文件导入导出再一起改 manifest 格式 |  |
| **旧系统兼容迁移** | Designer v3 没有旧兼容需求，不要设计迁移方案 |  |
| **把行为数据塞进控件 config** | 已定版，行为数据独立为 behaviors 文件 |  |
| **重构 VM 或已有服务** | 不要借行为系统之名重构现有代码 |  |

---

## 10. 开放问题

| # | 问题 | 建议 |
| --- | --- | --- |
| Q1 | `BehaviorGuid` 用 `Guid.NewGuid()` 还是 `Guid.CreateVersion7()`？ | .NET 9 支持 `Guid.CreateVersion7()`，推荐使用。有序 Guid 在调试和日志中更方便。 |
| Q2 | 序列化策略用 `[JsonInclude]` 还是 `[JsonIgnore(Condition)]`？ | 推荐 `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]`。 |
| Q3 | 内置默认布局 JSON 是否需要补充 BehaviorGuid？ | **不需要**。默认 JSON 反序列化后 Guid 为空，不影响运行时渲染。用户通过 Designer 添加时才生成。 |
| Q4 | BehaviorGuid 是否要暴露给插件？ | 建议插件开发者能读取但不能修改。在 `IFrontedControl.Create()` 的参数中传递 Guid。 |
| Q5 | `NoopBehaviorService` 是否在 历史迭代 1 实现？ | 建议实现 `IFrontedBehaviorService` + `NoopFrontedBehaviorService`，DI 注册为 Singleton。 |
| Q6 | BO3/BO5 状态的 behavior 数据是否需要独立？ | 建议和 layout 保持一致：如果 BO3 状态有独立控件副本，behavior 也应独立。 |
| Q7 | 是否需要 behaviors 文件脏追踪单独保存？ | 建议新增 `AreBehaviorsDirty` 属性，与 `IsDirty` 分开跟踪。 |
| Q8 | 测试是否应依赖具体文件系统？ | 历史迭代 1 所有测试应是纯内存模型测试，不涉及文件 I/O。 |
| Q9 | `DesignerPreviewSharedDataService` 是否需要触发行为事件？ | 当前是隔离的 preview 数据源，不触发真实事件。历史迭代 1 不需要改。 |
| Q10 | 建议行为文件命名？ | `FrontedBehaviors/{WindowType}.behaviors.json` |

---

## 当前编辑器与停止动画约定

行为过滤编辑器读取事件 payload descriptor 的 `TypeName` 与 `EnumValues`。枚举字段使用
ComboBox 显示可选值，保存时仍写稳定 enum 名称；字符串字段继续使用文本输入。

动画节点图的复制剪贴板是应用级内存剪贴板，复制节点及其内部连线，粘贴时重建全部
NodeId / ConnectionId，因此可以在 StartGraph、LoopGraph、StopGraph 之间复制。连线交互允许
从 Out 或 In 开始，最终始终规范化保存为 Out -> In；单输出端口创建新连线时会替换旧连线。

`FrontedBehaviorPropertyMetadata` 是 setProperty 与 animateProperty 共用的动画属性输入元数据源，
负责数值范围、占位提示、枚举值与颜色类型。StopGraph 仍严格按图连接顺序执行；
`WaitForCompletion=true` 的动画完成后才会进入后续节点。LoopGraph 被 StopTriggers 取消后，运行时
先收口旧图和同属性动画，再启动 StopGraph；StopGraph 成功完成后跳过 ResetTarget，并记录取消、
节点顺序、动画状态和 reset 决策日志。

## 附录：建议核心模型一览

```
FrontedBehaviorDocument
  └─ ControlBehaviorSet[]
       ├─ BehaviorGuid (→ FrontedControlConfigBase.BehaviorGuid)
       └─ FrontedBehavior[]
            ├─ BehaviorId: Guid
            ├─ Kind: FrontedBehaviorKind (OneShot | Loop)
            ├─ OneShot:
            │    ├─ Trigger: TriggerDescriptor
            │    └─ Graph: FrontedNodeGraph
            └─ Loop:
                 ├─ StartTrigger: TriggerDescriptor
                 ├─ StartFilter: TriggerFilter
                 ├─ StartGraph: FrontedNodeGraph
                 ├─ LoopGraph: FrontedNodeGraph
                 ├─ StopTriggers: List<TriggerDescriptor>
                 ├─ StopGraph: FrontedNodeGraph
                 └─ LoopPolicy: LoopPolicy

TriggerDescriptor
  ├─ EventName: string
  ├─ Source: string (如 "SharedDataService", "CharacterSelectionService")
  └─ Filter: TriggerFilter?

FrontedNodeGraph
  ├─ Nodes: FrontedNode[]
  └─ Connections: FrontedNodeConnection[]

LoopPolicy
  ├─ LoopCount: int? (null = infinite)
  ├─ Duration: TimeSpan?
  ├─ ReentryPolicy: ReentryPolicy
  └─ FillBehavior: FillBehavior
```
