# Fronted Behavior System

本文描述 Designer v3 前台行为动画的当前架构。行为文档属于活动布局包，正常运行时只读取活动包中的 v3 behavior 文件。

## Behavior animation runtime

每个窗口的行为文件位于：

```text
FrontedBehaviors/{WindowTypeName}.behaviors.json
```

控件通过稳定的 `BehaviorGuid` 关联行为。控件重命名不会改变该身份；复制控件时生成新的 `BehaviorGuid`；删除控件时清理对应行为。

行为分为 `OneShot`、`Loop` 和 `Transition`。节点图由 `FrontedNodeGraphRuntime` 执行，属性写入和动画由 `IFrontedAnimationRuntime` 及 `IAnimatablePropertyAdapterRegistry` 完成。动画目标通过稳定控件身份和显式 `TargetLayer` 解析，不使用临时行为标签。

`flow.if` 的事件字段选择器显示本地化字段名和稳定路径，例如“是否存在上一个角色
(`Event.HasOldCharacter`)”，但节点 JSON 只保存稳定路径。运行时解析 `Event.*`、
`StartEvent.*` 和 `StopEvent.*` 时兼容 payload 中的无前缀键及对应带前缀键；
Transition payload 会同时提供两种形式。条件无法解析时，图执行日志会记录路径和
可用 payload 键；设计器预览缺少事件上下文时也会明确提示。

| TargetLayer | 目标 |
| --- | --- |
| `Control` | 控件根元素 |
| `Content` | 控件主要内容元素 |
| `OverlayAbove` | 控件上方运行时动画层 |
| `OverlayBelow` | 控件下方运行时动画层 |

## Business event integration

`IFrontedEventBus` 是行为触发的统一入口。`FrontedSharedDataBehaviorEventBridge` 把共享数据和赛事业务事件转换为显式事件 payload；`FrontedBehaviorTriggerEvaluator` 根据事件类型和过滤规则决定是否触发行为。

后台引导高亮变化与清除不暴露为前台行为触发器。前台引导动画只使用 `Guidance.StepChanged`。过滤器比较显式 payload 字段，不依赖 UI 文本或临时标签。

## Loop lifecycle

```text
StartGraph
  -> LoopGraph repeated execution
  -> StopGraph
  -> cleanup
```

同一行为重新触发时，runtime 会停止旧实例并启动新实例。窗口关闭、布局重载、活动包切换或行为被删除时必须取消循环并清理动画状态。

## Transition lifecycle

`IFrontedTransitionOrchestrator` 串联来源状态退出和目标状态进入动画，并在重入、取消、窗口关闭和包切换时终止旧 transition。Transition 不拥有第二份比赛状态，状态变化仍以 `ISharedDataService` 和显式事件 payload 为权威源。

Transition 的顺序固定为 `ExitGraph -> commit -> EnterGraph`。其中 `commit` 是实际比赛状态变更点，必须在 WPF UI Dispatcher 上执行；SmartBP 等后台识别线程只能通过 `ICharacterSelectionService` / `IFrontedTransitionOrchestrator` 调用服务层，由服务层负责切回 UI 线程，不能直接修改 `CurrentGame`。

## Behavior package files

```text
package/
├── manifest.json
├── FrontedLayouts/
│   └── BpWindow.json
└── FrontedBehaviors/
    └── BpWindow.behaviors.json
```

导出器复制活动包中的 behavior 文件；导入器、package manager 和删除流程将 behavior 文件与布局包一起处理。legacy `.bpui` 转换不在普通 v3 runtime 中增加 legacy behavior fallback。

## Designer integration

Designer 的行为面板编辑当前选中控件的行为列表、触发器、过滤器和节点图。编辑结果进入同一 dirty tracking、Undo/Redo、保存、导入和导出流程。

`flow.if` 保持兼容的 `Left / Operator / Right` 存储结构，但 Designer 会按当前行为和图阶段从
`FrontedBehaviorEventCatalog` 构造条件字段。布尔字段使用 `true / false` 选择器，枚举字段保存稳定枚举名，
数字字段使用数字编辑器；可选操作符也会按字段类型收窄。OneShot 使用 `Trigger.EventType`，Transition 的
ExitGraph / EnterGraph 使用 `TransitionTrigger.EventType`，Loop 的 StartGraph / LoopGraph 使用
`StartTrigger.EventType`。Loop StopGraph 合并所有 StopTrigger 的 `Event.*` 字段，并额外提供
`StartEvent.*` 以读取启动循环时捕获的事件负载。

运行时条件路径含义：

| 路径 | 含义 |
| --- | --- |
| `Event.*` | 当前图阶段的事件负载；Loop StopGraph 中是实际匹配的停止事件 |
| `StartEvent.*` | Loop 启动事件负载 |
| `StopEvent.*` | Loop 停止事件负载 |
| `Context.TriggerEventType` / `Context.CurrentControlDisplayName` | 图执行上下文元数据 |

属性动画必须通过已注册 adapter。新增可动画属性时，应实现或扩展 `IAnimatablePropertyAdapter`，并保证捕获基础值、设置值、动画和 reset 的语义一致。

`flow.parallel` 使用 `BranchCount` 保存并行任务数，默认值为 `3`，允许范围为 `1` 到 `20`。分支输出使用稳定端口名 `Branch1` 到 `Branch20`；运行时仅执行当前数量范围内已连接的分支，并在全部完成后从 `Out` 继续。旧图未保存 `BranchCount` 时继续按 3 个分支处理。设计器减小任务数时会移除超出新范围的分支连接，避免保存不可见连线。

## 数值事件值与运算

动作节点的 `Value`、`From` 和 `To` 保持原有字面量写法，同时可接收 `number` 值端口。值节点包含数值常量、事件数值，以及四则、取模、取负、`abs`、`min`、`max`、`clamp`、`round`、`floor`、`ceil`；值图按动作执行时需求值，不参与 flow。已连接值端口优先于字面量。

数值属性也可写受限表达式，例如 `=clamp(Event.PlayerIndex / 10, 0, 1)`。表达式只支持数值、括号、上述数值函数与 `Event.*` / `StartEvent.*` / `StopEvent.*` 变量，使用 invariant culture，绝不执行脚本或任意 .NET 代码。表达式或数值图无法解析、除零或得到非有限数值时，runtime 记录 warning、跳过当前动作并继续后续 flow；旧字面量行为文件不会被转换或改写。

节点图属性面板中的普通数字字段使用 WPF-UI `NumberBox`。数值范围、整数要求和动画属性元数据约束由基于 CommunityToolkit.Mvvm `ObservableValidator` 的属性编辑 ViewModel 校验，验证通过后才写回节点 JSON。允许百分比表达式的动画字段继续使用文本输入。连接数值端口后，可在行为节点的 `ValueInputUnit`、`FromInputUnit` 或 `ToInputUnit` 选择绝对值或百分比；数据节点只输出无单位数值。百分比仅适用于支持相对长度的属性（如 `ClipInsetRight`），运行时会把计算结果写成 `%` 值。此时对应手填值会禁用，避免与外部输入产生歧义。行为文档加载时会将旧节点缺少的单位字段迁移为显式 `Absolute`，运行时不提供缺失字段的兼容推断。

## Runtime services

| 服务 | 职责 |
| --- | --- |
| `IFrontedBehaviorService` | 读取和保存 behavior 文档 |
| `IFrontedEventBus` | 发布显式前台事件 |
| `FrontedBehaviorTriggerEvaluator` | 执行触发过滤 |
| `IFrontedNodeGraphRuntime` | 执行节点图 |
| `IFrontedAnimationRuntime` | 应用属性和动画 |
| `IAnimatablePropertyAdapterRegistry` | 解析属性 adapter |
| `FrontedBehaviorRuntimeHostManager` | 管理窗口 runtime host 生命周期 |
| `IFrontedTransitionOrchestrator` | 管理 transition 生命周期 |

正常运行时只从活动包读取 behavior 文件。不要在 layout service、window service 或 renderer 中添加旧 behavior 文件、旧 canvas 或旧用户布局 fallback。
