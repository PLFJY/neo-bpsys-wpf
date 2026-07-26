# 引导式 BP

首次启动导览和页面 Product Tour 见 [product-tour-and-onboarding.md](product-tour-and-onboarding.md)。Product Tour 可以引导用户点击“开启对局引导”并等待相关 signal，但不替代 `GameGuidanceService`，也不重写对局引导的规则推进、计时器、导航和高亮业务逻辑。

## 核心服务

`GameGuidanceService` 实现对局引导功能。它依赖：

| 依赖 | 用途 |
| --- | --- |
| `ISharedDataService` | 读取当前对局、设置 Ban 位数量、启动计时器 |
| `INavigationService` | 切换后台页面 |
| `IInfoBarService` | 显示非阻断提示 |
| `WeakReferenceMessenger` | 发送高亮消息 |

规则文件路径固定为：

```text
{AppDomain.CurrentDomain.BaseDirectory}\GameRule.json
```

主项目文件中设置了 `GameRule.json` 总是复制到输出目录。

## 规则结构

`GameRule.json` 是 `GameProgress -> GameProperty` 的字典。`GameProperty` 包含：

| 字段 | 说明 |
| --- | --- |
| `SurCurrentBan` | 当前局求生者 Ban 位数 |
| `HunCurrentBan` | 当前局监管者 Ban 位数 |
| `SurGlobalBan` | 全局求生者 Ban 位数 |
| `HunGlobalBan` | 全局监管者 Ban 位数 |
| `WorkFlow` | 步骤列表 |

每个步骤包含：

| 字段 | 说明 |
| --- | --- |
| `Action` | `GameAction`，如 `BanMap`、`PickSur` |
| `Index` | 需要高亮的索引列表，可为 `null` |
| `Time` | 计时器秒数，可为 `null` |

## 启动流程

`StartGuidance()` 会：

1. 读取当前 `CurrentGame.GameProgress` 对应规则。
2. 如果是 `GameProgress.Free` 或规则不支持，提示自由赛不可用。
3. 设置当前/全局 Ban 位数量。
4. 从记录更新双方全局 Ban。
5. 设置 `IsGuidanceStarted = true`。
6. 立即进入下一步。

`HandleStepChange` 会：

1. 在更新 `_currentStep` 前读取当前步骤，作为步骤变化事件的上一步信息。
2. 更新 `_currentStep`。
3. 如果步骤不是 `PickCamp`，按 `_actionToPage` 导航到对应后台页面。
4. 调用 `_sharedDataService.TimerStart(thisStep.Time)`。
5. 等待 250ms，让待选框动画/页面状态就位。
6. 构造包含当前步骤和上一步骤 payload 的 `GameGuidanceStepChangedEventArgs`。
7. 触发权威步骤事件 `GuidanceStepChanged`，前台行为运行时通过 `Guidance.StepChanged` 消费它。
8. 触发后台高亮事件 `GuidanceHighlightChanged`。
9. 发送后台 UI 便利消息 `HighlightMessage(thisStep.Action, thisStep.Index)`。
10. 返回本地化后的步骤名称。

`MoveToStepAsync` 用于 SmartBP 等自动同步路径，成功时返回 `null`，失败时返回错误文本。它和 `StartGuidance`、`NextStepAsync`、`PrevStepAsync`、`StopGuidance`、`CompleteGuidance` 都会在存在 WPF `Application.Current.Dispatcher` 时切回 UI Dispatcher 执行，避免后台识别线程直接修改 WPF 导航、计时器、消息和事件状态。

## 行为事件 payload

`Guidance.StepChanged` 同时暴露当前步骤和上一步骤 payload。当前步骤 payload 适合启动动画，上一步骤 payload 适合停止由切换前引导步骤启动的动画。首次进入步骤时，所有 `Previous*` 值为 `null`，`PreviousIndexesText` 为 `[]`。

`Guidance.Cancelled` 在用户取消当前引导时触发，`Guidance.Stopped` 在引导以停止/完成语义结束时触发。两者 payload 使用稳定机器值：`Reason`、`Time`、`PreviousStepIndex`、`PreviousAction`、`PreviousIndexes` 和 `PreviousIndexesText`，不包含本地化操作名。Loop 行为的 `StopTriggers` 应把这些事件与 `Guidance.StepChanged` 的上一步过滤一起配置，避免取消引导后循环动画残留。

列表索引的字符串过滤应优先使用 `IndexesText` / `PreviousIndexesText`，其格式稳定为 `[1, 2]`。高亮变化与清除事件只用于后台引导 UI，不暴露给前台行为触发器。

例如，启动求生者 1 号位呼吸灯：

```text
Event.Action == PickSur
Event.IndexesText contains "1"
```

停止由上一步启动的求生者 1 号位呼吸灯：

```text
Event.PreviousAction == PickSur
Event.PreviousIndexesText contains "1"
```

## Action 到页面映射

| Action | 页面 |
| --- | --- |
| `BanMap` / `PickMap` | `MapBpPage` |
| `BanSur` | `BanSurPage` |
| `BanHun` | `BanHunPage` |
| `PickSur` / `DistributeChara` / `PickHun` | `PickPage` |
| `PickSurTalent` / `PickHunTalent` | `TalentPage` |

`PickCamp` 当前不会触发页面切换。

## 维护建议

规则应跟随实际第五人格赛事规则变化维护，但本仓库中的 `GameRule.json` 不是外部权威规则源。修改规则时应：

1. 明确对应哪个 `GameProgress`。
2. 检查 Ban 位数量与页面控件容量一致。
3. 确认 `GameAction` 是否已映射到后台页面。
4. 检查 `Index` 是否能被对应页面的高亮逻辑理解。
5. 自由赛 `GameProgress.Free` 当前不支持引导式 BP，这是代码显式行为。
