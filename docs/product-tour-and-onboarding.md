# 教程与导览系统

`neo-bpsys-wpf.ProductTour` 是 solution 内的 WPF 控件库，用于承载首次启动导览、页面教程包、NPC 对话框式引导和聚焦控件式 Product Tour。它不是 `GameGuidanceService` 的替代品；对局内 BP 步骤、计时器、页面切换和高亮规则仍由 `GameGuidanceService` 负责。

## 项目边界

| 项目 | 职责 |
| --- | --- |
| `neo-bpsys-wpf.ProductTour` | 教程模型、注册表、状态存储、Signal、Overlay 控件和默认样式 |
| `neo-bpsys-wpf` | 注册具体页面 package、首次总导览 flow、业务 signal、设置页入口和语言应用 |
| `neo-bpsys-wpf.Core` | 不承载 Product Tour 类型，避免把 WPF UI 教程状态放入 Core |

主项目通过 `services.AddProductTour()` 注册基础服务，并在 `App.xaml` 合并：

```xaml
<ResourceDictionary Source="/neo-bpsys-wpf.ProductTour;component/Themes/ProductTour.xaml" />
```

默认样式放在 `neo-bpsys-wpf.ProductTour/Themes/ProductTour.xaml`。控件库不得写死主程序具体颜色、字体、图片资源；视觉资源应优先使用 `DynamicResource`，并暴露样式 key 供主程序覆盖。

## 核心概念

教程系统使用四层结构：

| 概念 | 说明 |
| --- | --- |
| `TutorialPackageDefinition` | 单个页面、窗口或功能的教程包，包含 `PackageId`、`Version`、`PageKey`、`Sequence` 和步骤 |
| `TutorialFlowDefinition` | 总导览流程，按 `TutorialFlowItem` 串联 dialogue、package、action 和自定义步骤 |
| `TutorialState` | 用户教程状态，分为 `CompletedFlows` 和 `CompletedPackages` |
| `ITutorialSignalService` | 业务动作到教程等待步骤之间的解耦信号通道 |

页面教程包是单一来源。首次总导览通过 `PackageFlowItem` 引用已有 package，不复制同一份步骤。新增功能教学时应新增 package，并挂到对应 page sequence 后面；不要修改旧 package 来承载新功能。

## 状态规则

状态文件保存到：

```text
%APPDATA%\neo-bpsys-wpf\TutorialState.json
```

`TutorialCompletionRecord` 记录：

| 字段 | 说明 |
| --- | --- |
| `Version` | 完成时的 package 或 flow 版本 |
| `CompletionKind` | `Completed`、`Skipped` 或 `CoveredByFlow` |
| `SourceFlowId` | 被 flow 覆盖时记录来源 flow |
| `CompletedAt` | 完成时间 |

判断 pending 时必须比较版本。已完成或已被 flow 覆盖的 package，如果当前定义版本更高，仍应重新视为 pending。`Suppressed` 不写入状态，也不排队。

首次总导览 `Flow.FirstRun.StandardBp` 完整完成后，会把 `IncludedPackageIds` 内的 package 标记为 `CoveredByFlow`。用户跳过 flow 时只标记 flow 为 `Skipped`，不得把 included package 标记为 `CoveredByFlow`。

## 启动链路

首次 Welcome 不是普通弹窗，也不在 `MainWindow.Loaded` 立即显示。它由 `MainWindow` 在 `StartupLoading` storyboard 完成后调用 `IOnboardingCoordinator.ShowFirstRunWelcomeAsync(owner)`，视觉上必须续接启动加载状态。

维护 Welcome UI 时遵循以下约束：

1. 背景应延续启动加载动画的 Fluent 深色面，不做网页落地页式渐变、夸张插画或营销卡片。
2. 内容布局参考 SmartBP 模块未加载页：居中窄内容、标题、说明、语言选择和主操作垂直排布。
3. 使用 WPF-UI / Fluent 动态资源，例如 `TextFillColorPrimaryBrush`、`TextFillColorSecondaryBrush`、`CardBackgroundFillColorDefaultBrush`、`ControlStrokeColorDefaultBrush`、`AccentFillColorDefaultBrush`。
4. 显示和关闭都要播放淡入淡出动画；点击“开始导览”后必须等淡出完成再移除 overlay。
5. “跳过”必须进入 `SkipTutorialConfirmDialog` 二次确认，不使用系统 `MessageBox`。

语言选择在点击“开始导览”时应用到：

```csharp
LocalizeDictionary.Instance.Culture
Application.Current.Resources["CurrentLanguage"]
```

并同步保存设置。后续 dialogue 和 product tour 文案按当前语言取值。

## UI 组件

| 控件 | 用途 |
| --- | --- |
| `FirstRunWelcomeOverlay` | 首次启动欢迎、语言选择、开始导览、跳过入口 |
| `SkipTutorialConfirmDialog` | 跳过导览的 overlay 内二次确认 |
| `DialogueOverlay` | 底部 NPC 对话框，支持打字机效果 |
| `ProductTourOverlay` | 遮罩、高亮目标控件、说明卡片、箭头和步骤导航 |
| `OverlayHost` | 把 overlay 附着到当前 owner 的可视树上 |

`ProductTourOverlay` 通过 `TargetName` 查找目标控件，优先 `owner.FindName(...)`，再递归 VisualTree。目标在 `ScrollViewer` 内时应尝试 `BringIntoView()`；找不到目标时，错误信息要包含 `flowId`、`packageId`、步骤索引和 `targetName`。

样式 key 至少覆盖 ProductTour overlay、spotlight、card、标题、正文、箭头、按钮、welcome、dialogue 和 confirm dialog。新增视觉元素时先考虑扩展样式资源，不要在控件代码中绑定主程序具体颜色。

## Flow 与页面 package

页面 `Loaded` 时调用：

```csharp
RunPendingPagePackagesAsync(owner, pageKey, TutorialTriggerMode.AutoOnLoaded)
```

运行规则：

1. 按 `pageKey` 找到 package sequence。
2. 按 `Sequence` 排序。
3. 过滤已完成、已跳过或已被 flow 覆盖且版本已满足的 package。
4. 找到第一个 pending package 后运行。
5. 如果当前已有 flow、dialogue 或 product tour 正在运行，返回 `Suppressed`。

Flow 内部引用 package 时使用 `TutorialTriggerMode.EmbeddedInFlow`，不受页面 auto loaded suppression 影响。Flow 运行中，页面切换产生的 auto loaded package 必须被 suppress，避免总导览过程中误弹页面教程。

## Signal 边界

教程系统不应到处读取业务对象内部状态。业务关键动作发生时由 ViewModel command、控件 code-behind、service event 或 messenger 发布 signal：

```csharp
_tutorialSignalService.Publish("GameGuidanceStarted");
```

交互式步骤用 `WaitForSignal(...)` 等待信号。等待超时的错误信息应包含 flow、package、步骤索引、target 和 signal，方便定位是业务 signal 未发布还是目标控件未就绪。

当前约定的基础 signal 包括：

```text
BpWindowOpened
GameProgressSelected.Bo1FirstHalf
TeamNameConfirmed
TeamJsonImported.Home
TeamJsonImported.Away
MemberStateChanged
MemberPositionSwapped
GameGuidanceStarted
MapBpCompleted
GuidanceNextClicked
GuidanceStepChanged
CharacterSelector.SearchCommitted
CharacterSelector.SelectionConfirmed
GlobalBanRecordUpdated
ScoreChanged
NewGameCreated
```

新增 package 时优先复用稳定 signal；如果确实需要新增 signal，应使用明确的业务事件名，不用临时 UI 标签。

## 与 GameGuidanceService 的关系

Product Tour 只负责“告诉用户做什么”和“等待用户完成动作”。当教程需要开启对局引导时，应调用现有 `GameGuidanceService` 的入口或触发现有 ViewModel command，然后等待 `GameGuidanceStarted`、`GuidanceStepChanged`、`GuidanceNextClicked` 等 signal。

不得在 Product Tour 中重写：

1. `GameRule.json` 的解析。
2. BP 步骤推进规则。
3. 计时器启动/停止规则。
4. 对局页面切换和高亮业务逻辑。

这能保证真实导播流程和教程流程使用同一套业务实现。

## 设置页入口

设置页提供“教程与导览”区域：

| 操作 | 行为 |
| --- | --- |
| 重新启动首次导览 | 二次确认后清除 `Flow.FirstRun.StandardBp` 状态，并重新显示首次导览 |
| 重置全部教程状态 | 二次确认后清空 `CompletedFlows` 和 `CompletedPackages` |

危险操作不得使用系统默认 `MessageBox`。如果需要更强一致性，应优先复用 Product Tour 的确认 overlay 或 WPF-UI 风格确认控件。

## 维护 checklist

新增页面教程时：

1. 添加新的 `TutorialPackageDefinition`，使用稳定 `PackageId` 和递增 `Version`。
2. 注册到对应 `PageKey` 的 sequence。
3. 页面 `Loaded` 调用 pending package 运行入口。
4. 交互步骤所需的业务动作发布 signal。
5. 如果首次总导览需要覆盖该教程，只把 package id 加入 `IncludedPackageIds` 并用 `PackageFlowItem` 引用。
6. 不复制已有教程步骤到 flow。

修改 Welcome 或 overlay 视觉时：

1. 先对齐 Fluent / WPF-UI 资源。
2. 保持启动加载动画后的视觉连续性。
3. 避免网页落地页式装饰、强渐变和不属于应用设计语言的插画。
4. 不新增样式坐标类测试；需要验证时使用 XAML smoke 或行为测试。
