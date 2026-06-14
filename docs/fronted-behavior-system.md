# Fronted Behavior System

本文描述 Designer v3 前台行为动画的当前架构。行为文档属于活动布局包，正常运行时只读取活动包中的 v3 behavior 文件。

## Behavior animation runtime

每个窗口的行为文件位于：

```text
FrontedBehaviors/{WindowTypeName}.behaviors.json
```

控件通过稳定的 `BehaviorGuid` 关联行为。控件重命名不会改变该身份；复制控件时生成新的 `BehaviorGuid`；删除控件时清理对应行为。

行为分为 `OneShot`、`Loop` 和 `Transition`。节点图由 `FrontedNodeGraphRuntime` 执行，属性写入和动画由 `IFrontedAnimationRuntime` 及 `IAnimatablePropertyAdapterRegistry` 完成。动画目标通过稳定控件身份和显式 `TargetLayer` 解析，不使用临时行为标签。

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
