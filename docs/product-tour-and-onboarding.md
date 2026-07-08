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

页面自己的 package sequence 只用于用户单独进入某页面时的 `AutoOnLoaded` 教程。总导览 flow 不得直接按页面 sequence 或 `IncludedPackageIds` 自动拼接；`Flow.FirstRun.StandardBp` 和验证 flow 的 `Items` 必须在 `TutorialFlowDefinition` 中显式声明顺序。

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
6. Welcome 应表现为页面式 onboarding surface，而不是半透明浮在 HomePage 上的弹窗卡片；底层页面只作为被强遮罩压暗的背景。

语言选择不由 ProductTour 控件硬编码。`FirstRunWelcomeOverlay` 使用页面式布局中的 `ComboBox` 渲染 `ITutorialLanguageService.GetLanguageOptionsAsync()` 提供的 `TutorialLanguageOption`，点击“开始导览”时传回 option id。主程序的 `NeoBpsysTutorialLanguageService` 负责把 option id 映射到真实 `LanguageKey`：

| Option id | 主程序语言 |
| --- | --- |
| `System` | `LanguageKey.System` |
| `zh_Hans` | `LanguageKey.zh_Hans` |
| `en_US` | `LanguageKey.en_US` |
| `ja_JP` | `LanguageKey.ja_JP` |

语言应用仍走主程序设置链路：

```csharp
_settingsHostService.Settings.Language
_settingsHostService.SaveConfigAsync()
LocalizeDictionary.Instance.Culture
Application.Current.Resources["CurrentLanguage"]
```

ProductTour 库不得引用主程序的 `LanguageKey`，也不得在控件中出现 `zh-CN / en-US` 这类独立 culture 列表。后续 dialogue 和 product tour 文案按当前语言取值。

## UI 组件

| 控件 | 用途 |
| --- | --- |
| `FirstRunWelcomeOverlay` | 首次启动欢迎、语言选择、开始导览、跳过入口 |
| `SkipTutorialConfirmDialog` | 跳过导览的 overlay 内二次确认 |
| `DialogueOverlay` | 底部 NPC 对话框，支持打字机效果 |
| `ProductTourOverlay` | 遮罩、高亮目标控件、说明卡片、箭头和步骤导航 |
| `OverlayHost` | 把 overlay 附着到当前 owner 的可视树上 |

Guide Character 通过 `ITutorialAvatarProvider` 注入。ProductTour 库只定义 `TutorialAvatarPose`、`TutorialAvatar` 和默认空实现，不引用主程序资源路径。主程序当前用 `AliceTutorialAvatarProvider` 从 `Resources/Alice/*.png` 加载 Alice Guide Character，并按当前语言返回显示名：简体中文“爱丽丝·德罗斯”、英文“Alice DeRoss”、日文“アリス・デロス”。没有 provider 或 provider 返回 `null` 时，Welcome、Dialogue 和 ProductTour 都必须隐藏头像区域并保持可用。

`ProductTourOverlay` 通过 `ProductTourStep.TargetKind` 解析目标控件：

| `TargetKind` | 解析方式 |
| --- | --- |
| `Name` | 使用 `TargetName`，优先 `owner.FindName(...)`，再递归 VisualTree |
| `NavigationItem` | 使用 `TargetKey`，在真实 `ModernNavigationView` / `NavigationViewItem` 生成的 VisualTree 中匹配 `TargetPageType.FullName`、`Tag`、`TargetPageTag`、`Id`，最后才 fallback 到显示文本 |
| `DescendantType` | 可选先用 `TargetName` 找 host，再在 host 下查找第一个 `GetType().FullName == TargetKey` 的 `FrameworkElement` |
| `ElementTag` | 使用 `TargetKey` 匹配 `FrameworkElement.Tag` 字符串，适合 DataTemplate 内无法稳定命名但有稳定业务 ID 的按钮 |

目标在 `ScrollViewer` 内时应尝试 `BringIntoView()`；找不到目标时，日志和超时信息要包含 `flowId`、`packageId`、步骤索引、`TargetKind`、`TargetName` 和 `TargetKey`。

样式 key 至少覆盖 ProductTour overlay、spotlight、card、标题、正文、箭头、按钮、welcome、dialogue 和 confirm dialog。新增视觉元素时先考虑扩展样式资源，不要在控件代码中绑定主程序具体颜色。

首次导览运行期间，所有可见教程层都必须有右上角固定“跳过”按钮，包括 Welcome、Dialogue 和 ProductTour step。点击跳过只显示 `SkipTutorialConfirmDialog`；取消确认后当前句子或当前步骤继续，确认后当前 overlay 淡出并返回 `Skipped`。Flow 收到 `Skipped` 后只标记 flow 为 `Skipped`，不得覆盖 `IncludedPackageIds`。

## UI 配置边界

Phase 3 后，Product Tour UI 按三层边界维护：

```text
TextProvider: 固定 UI 文案
ProductTourOptions: 尺寸、动画、显示行为开关
ProductTour.xaml: 视觉样式、颜色、字体、边框、阴影
```

`ITutorialTextProvider` 只负责控件固定文案，例如“上一步”“下一步”“等待操作...”和 Welcome 的固定说明。它不负责卡片宽度、动画时长，也不负责 package 的业务说明文案。

`ProductTourOptions` 用于控制运行时结构参数和行为开关，例如 `CardWidth`、`CardMaxHeight`、`CardMargin`、`Gap`、`SpotlightPadding`、进入/退出动画时长、打字机间隔、是否显示步骤进度、是否显示跳过按钮、是否显示箭头。`AddProductTour()` 会注册默认 options；主程序可以在注册后覆盖 singleton，以统一调整导览 UI 的尺寸和动效节奏。

遮罩强度也由 `ProductTourOptions` 控制：`WelcomeMaskOpacity` 默认强于普通 product tour，`DialogueMaskOpacity` 和 `ProductTourMaskOpacity` 保证背景界面不会和教程内容抢阅读焦点。Dialogue 还通过 `DialogueBoxMaxWidth`、`DialogueBoxMinOpacity`、`DialogueBoxMargin` 控制可读性。

`ProductTour.xaml` 负责视觉样式。等待、错误、进度、确认框、Dialogue continue 等视觉元素都应通过 style key 配置；控件代码只保留结构、状态和运行逻辑。控件代码不应手动覆盖卡片背景、边框、阴影或错误文本颜色，否则主程序无法通过资源字典统一替换视觉表现。

当前已接入真实主程序中的部分目标和 signal，用于验证导航项、TeamInfo、对局管理按钮、角色选择器宿主和比分控件的最小导览闭环。完整教学沙盒、示例队伍导入、完整 BO1 BP 教学、SmartBP 和 DesignerV3 正式教程仍未接入。

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

## 注册与 Builder

主程序内置教程注册拆分在 `neo-bpsys-wpf/Tutorial` 下：

| 文件 | 职责 |
| --- | --- |
| `NeoBpsysTutorialIds.cs` | 集中维护 flow、page、package、signal、target 的稳定字符串常量 |
| `NeoBpsysTutorialRegistration.cs` | 总入口，只调度 sequence、package、flow 注册 |
| `NeoBpsysTutorialSequences.cs` | 注册 `PageKey -> package sequence` |
| `NeoBpsysTutorialPackages.cs` | 注册 package definitions |
| `NeoBpsysTutorialFlows.cs` | 注册 `Flow.FirstRun.StandardBp`、导航验证 flow 和真实目标验证 flow，只引用 package id |
| `NeoBpsysTutorialTexts.cs` | 临时集中 package 标题、说明和 fallback 文案 |

新增教程包时优先使用常量，不要在注册代码里继续散落裸字符串。已有 ID 的字符串值用于持久化状态兼容，不得随意改名。

`neo-bpsys-wpf.ProductTour` 提供轻量 Builder API，生成的仍是普通 definition 对象：

```csharp
TutorialPackageBuilder.Create(TutorialPackageIds.TeamInfoTeamNameBasic)
    .ForPage(TutorialPageKeys.TeamInfo)
    .Version(1)
    .Sequence(100)
    .Step(TutorialTargetNames.TeamNameInput)
        .Title("填写队伍名称")
        .Description("这里可以设置队伍名称。先试着输入一个队伍名。")
        .Placement(ProductTourPlacement.Auto)
        .Interaction(ProductTourInteractionMode.AllowTargetOnly)
        .WaitForSignal(TutorialSignalIds.TeamNameConfirmed)
        .AllowMissingTarget()
        .EndStep()
    .Build();
```

动态导航项应使用 `StepNavigationItem(...)`，不要依赖菜单显示文本或给动态生成的 `NavigationViewItem` 写死 `x:Name`：

```csharp
TutorialPackageBuilder.Create(TutorialPackageIds.MainNavigationTeamInfo)
    .ForPage(TutorialPageKeys.Main)
    .StepNavigationItem(typeof(TeamInfoPage).FullName!)
        .Title("进入队伍管理")
        .Description("先进入队伍管理页面，我们会设置本次教学使用的队伍。")
        .Interaction(ProductTourInteractionMode.AllowTargetOnly)
        .WaitForSignal(TutorialSignalIds.NavigationTeamInfoOpened)
        .EndStep()
    .Build();
```

DataTemplate 内的目标应使用 `StepDescendantType(...)` 指向稳定 host 下的第一个指定类型控件：

```csharp
TutorialPackageBuilder.Create(TutorialPackageIds.BpCharacterSelectorBasic)
    .ForPage(TutorialPageKeys.BpShared)
    .StepDescendantType(
        TutorialTargetNames.FirstBanSurvivorSelectorHost,
        typeof(CharacterSelector).FullName!)
        .Title("角色选择器")
        .Description("输入后按空格可以搜索，按 Enter / Tab 或点击确认完成选择。")
        .Interaction(ProductTourInteractionMode.AllowTargetOnly)
        .WaitForSignal(TutorialSignalIds.CharacterSelectorSelectionConfirmed)
        .EndStep()
    .Build();
```

DataTemplate 内有稳定业务 ID 的控件可使用 `StepElementTag(...)`。例如前台管理页的卡片“打开”按钮把 `Tag` 绑定到前台窗口 `WindowId`；BP 前台窗口教程使用 `FrontedWindowHelper.GetFrontedWindowGuid(FrontedWindowType.BpWindow)` 定位单个 BP Window 按钮，不指向“打开全部”按钮。

Flow Builder 用于串联 dialogue 和 package 引用，不复制 package 内部步骤：

```csharp
TutorialFlowBuilder.Create(TutorialFlowIds.FirstRunStandardBp)
    .Version(1)
    .Include(TutorialPackageIds.MainNavigationFrontManage)
    .Include(TutorialPackageIds.FrontManageBpWindowLaunchBasic)
    .Dialogue("neo-bpsys-wpf", "欢迎来到 neo-bpsys-wpf。")
    .Package(TutorialPackageIds.MainNavigationFrontManage)
    .Package(TutorialPackageIds.FrontManageBpWindowLaunchBasic)
    .Item(MainWindowActivate)
    .Package(TutorialPackageIds.MainNavigationTeamInfo)
    .Package(TutorialPackageIds.TeamInfoTeamNameBasic)
    .Build();
```

`IncludedPackageIds` 可以集中维护覆盖状态，但不能用 `foreach IncludedPackageIds` 自动生成 flow items。正式标准 BP 总导览当前顺序是：前台管理打开 BP Window、进入队伍管理、队名与 MainWindow 顶部队伍摘要、预设队伍导入与选手管理、BO1 上半与开启对局引导、Ban 求生角色选择器、Pick 与全局禁选、比分、新建对局与全局禁选继承，最后只简单说明 v3 编辑器和智慧 BP 的独立教程。

## 固定 UI 文案

Product Tour 控件固定 UI 文案通过 `ITutorialTextProvider` 提供。`AddProductTour()` 注册 `DefaultTutorialTextProvider`，主程序在其后注册 `NeoBpsysTutorialTextProvider` 覆盖默认实现。当前 provider 先保留中文默认文本，后续接正式 resx 或 `WPFLocalizeExtension` 时应优先在 provider 内集中处理。

package 的业务标题和说明暂时仍在 `NeoBpsysTutorialTexts.cs` 中维护；后续接入本地化 key 时，保持 `PackageId`、`FlowId`、`PageKey` 不变。

## 页面接入

旧的 code-behind 入口 `TutorialPageLoader.RunPendingOnLoaded(owner, pageKey)` 继续保留。新页面可以优先使用 attached property 声明：

```xaml
<Page
    xmlns:tour="clr-namespace:neo_bpsys_wpf.Tutorial"
    xmlns:tutorial="clr-namespace:neo_bpsys_wpf.Tutorial"
    tour:Tutorial.PageKey="{x:Static tutorial:TutorialPageKeys.TeamInfo}"
    tour:Tutorial.AutoRunOnLoaded="True">
</Page>
```

attached property 内部仍通过 `IAppHost.Host` 获取 `ITutorialService`，避免每个页面 code-behind 重复写静态服务查找。不要为了接入教程把 WPF `Page` 嵌入普通 `ContentControl` 或 `Grid`；页面承载规则仍遵循仓库 WPF 页面约束。

## Signal 边界

教程系统不应到处读取业务对象内部状态。业务关键动作发生时由 ViewModel command、控件 code-behind、service event 或 messenger 发布 signal：

```csharp
_tutorialSignalService.Publish("GameGuidanceStarted");
```

交互式步骤用 `WaitForSignal(...)` 等待信号。等待超时的错误信息应包含 flow、package、步骤索引、target 和 signal，方便定位是业务 signal 未发布还是目标控件未就绪。

当前约定的基础 signal 包括：

```text
Navigation.Home.Opened
Navigation.TeamInfo.Opened
Navigation.Score.Opened
Navigation.FrontManage.Opened
Navigation.SmartBp.Opened
Navigation.MapBp.Opened
Navigation.BanSurvivor.Opened
Navigation.BanHunter.Opened
Navigation.PickCharacter.Opened
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

页面打开类 signal 不应根据导航项显示文本判断。优先在统一导航完成事件按 PageType 发布；没有统一事件时，可以在对应 Page 的 `Loaded` 中发布。

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
| 运行真实目标验证 | 强制运行 `Flow.Phase4.RealTargetProbe`，按正式总导览顺序裁剪验证前台管理、BP Window、队伍管理、队名、顶部队伍摘要、BO1 上半选择和开始对局引导按钮 |

危险操作不得使用系统默认 `MessageBox`。如果需要更强一致性，应优先复用 Product Tour 的确认 overlay 或 WPF-UI 风格确认控件。

`Flow.Phase4.RealTargetProbe` 是手动验证 flow，不是默认首次启动导览。它不导入示例队伍、不创建新对局、不执行完整 BO1 BP 流程，也不实现教学沙盒。验证顺序必须是正式总导览的裁剪版，不能按页面 sequence 或 package 注册顺序自动生成。

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
