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
| `CompletionKind` | `Completed` 或 `CoveredByFlow` |
| `SourceFlowId` | 被 flow 覆盖时记录来源 flow |
| `CompletedAt` | 完成时间 |

判断 pending 时必须比较版本。已完成或已被 flow 覆盖的 package，如果当前定义版本更高，仍应重新视为 pending。`Suppressed` 不写入状态，也不排队。

状态文件不保存“跳过”第三态。用户点击跳过只表示当前运行返回 `TutorialRunResult.Skipped` 并停止后续自动衔接；写入持久状态时按“已处理完成”归一。首次 Welcome 被跳过时，`Flow.FirstRun.StandardBp` 和 `IncludedPackageIds` 内的 package 都标记为 `Completed`，避免首次总导览包含的页面 package 后续再次弹出。普通 flow 完整完成时，会把 `IncludedPackageIds` 内的 package 标记为 `CoveredByFlow`；flow 运行中被用户跳过时，flow 和 included package 都标记为 `Completed`。

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
| `OverlayHost` | 通过窗口根 Grid 的独立 overlay root 或 Adorner 非侵入地附着 overlay，不替换实时 `Window.Content` |

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

首次导览运行期间，所有可见教程层都必须有右上角固定“跳过”按钮，包括 Welcome、Dialogue 和 ProductTour step。点击跳过只显示 `SkipTutorialConfirmDialog`；取消确认后当前句子或当前步骤继续，确认后当前 overlay 淡出并返回 `Skipped`。`Skipped` 只是运行结果，不写入 `TutorialCompletionRecord.CompletionKind`。

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

当前已接入真实主程序中的目标和 signal，用于验证导航项、TeamInfo、对局管理按钮、角色选择器宿主、比分控件、前台管理、Designer v3 和 SmartBP 页面教程。教程系统不实现教学沙盒，复杂模块教程只介绍真实界面入口和风险，不要求用户完成文件选择、导入包、打开全部窗口、启动完整 SmartBP 流程或拖拽图层等重型操作。

## Flow 与页面 package

页面、Tab、窗口或子区域确认自己仍是当前用户正在看的 active owner 后调用：

```csharp
await tutorialRunner.RunSequenceAsync(owner, pageKey, ownerLifetimeToken);
```

运行规则：

1. 调用方先判断 owner 是否仍是当前 active owner；ProductTour runtime 不判断主导航页、FrontManage tab、SmartBP 模块状态或其他业务 UI 状态。
2. 按 `pageKey` 找到 package sequence。
3. 按 `Sequence` 排序。
4. 过滤已完成或已被 flow 覆盖且版本已满足的 package。
5. sequence 在同一个全局播放作业内逐个运行 package；每次完成后重新读取状态并解析下一个 pending package。
6. 若当前 package `CanRun=false`，返回 `NotReady`（非终态：不写完成状态），由调用方稍后重新触发。
7. 其他 sequence、直接 package 和 flow 共用 `ITutorialPlaybackCoordinator` 全局队列；忙时等待，不丢弃请求。
8. 同一 owner 实例与 tutorial key 的重复请求共享已有 queued/running task；owner 卸载或窗口关闭时由调用方 lifetime token 取消陈旧请求。

`Loaded` 和 `IsVisibleChanged` 只能作为辅助信号，不能单独代表用户正在看该 owner。主导航页应优先由 `NavigationService.PageChanged` 触发；FrontManage 子 view 由自身的 Loaded/可见信号触发，但必须等到 `ContextIdle`、`Render` 后确认自身与宿主 Window 都仍可见；SmartBP 的 `ModuleLoaded` 和 module content changed 只表示内容 ready，触发前仍必须通过 active owner 校验。

ProductTour 项目不提供 owner activation service。主程序页面、窗口或子 view 在各自 code-behind 内解释 neo-bpsys 的主导航页、FrontManage 当前 tab、窗口可见性和 SmartBP 内容状态；通过校验后才调用 `ITutorialRunner`。

`ITutorialRunner` 是页面、窗口和设置入口应使用的运行边界。owner sequence 只有 `RunSequenceAsync` 一种语义：连续运行全部未完成 package，直到完成、用户跳过、取消，或遇到 `NotReady`、`TargetMissing`、`Failed`。直接 package 与 flow 也通过同一协调器排队。

Flow 内部引用 package 时使用 `TutorialTriggerMode.EmbeddedInFlow`，并保留 flow 对全局播放的所有权。Flow 运行期间产生的 owner sequence 请求会排在其后；flow 写入覆盖状态后，sequence 启动时重新解析 pending package，因此不会重复播放已覆盖内容。

教程步骤打开拥有独立教程的子窗口时，调用方必须在设置 `Owner` 后、`Show()` 或 `ShowDialog()` 前调用 `ITutorialPlaybackCoordinator.BeginChildWindowSessionAsync(...)`。如果当前播放 owner 是该子窗口的祖先，协调器会把当前父步骤以 `ChildWindowHandoff` 结束，将已成功打开子窗口的父 package 记为完成并释放全局播放权；子窗口随后正常排队并独占 overlay。调用方在子窗口 `Closed` 或模态调用的 `finally` 中完成 session，父 sequence 才会重新解析下一个 pending package 并继续。session 是逐窗口、恰好完成一次的作用域对象，非模态、模态、取消和嵌套子窗口都不得共享全局 completion source。

普通 package 的 `Items` 可以按作者顺序混合 `TutorialPackageStepItem` 与 `TutorialPackageDialogueItem`。Dialogue 直接复用 `DialogueOverlay`；package 内不允许嵌套 `PackageFlowItem`。

## 注册与 Builder

主程序内置教程注册以 owner 为边界。页面、窗口或区域实现 `ITutorialOwner<TSelf>`，在静态 `RegisterTutorials(ITutorialBuilder builder)` 中注册自己的 package；应用级首次导览实现 `IAppTutorial<App>`，当前入口是 `App.Tour.xaml.cs`。`NeoBpsysTutorialRegistration.Register(...)` 只负责创建 `TutorialBuilder` 并调度 owner/app/flow 注册，不再保留旧 registrar、definition helper 或低层 builder 兼容入口。

主程序内置教程注册拆分在 `neo-bpsys-wpf/Tutorial` 下：

| 文件 | 职责 |
| --- | --- |
| `NeoBpsysTutorialIds.cs` | 集中维护 flow、page、package、signal、target 的稳定字符串常量 |
| `NeoBpsysTutorialRegistration.cs` | 总入口，只调度 owner、app、flow 注册 |
| `*.Tutorials.cs` | 各 owner 自己声明 package refs、sequence 和步骤 |
| `NeoBpsysTutorialFlows.cs` | 注册导航验证 flow 和真实目标验证 flow，只引用 `TutorialPackageRef`；标准首次导览 flow 由 `App.Tour.xaml.cs` 注册 |
| `NeoBpsysTutorialTexts.cs` | 临时集中 package 标题、说明和 fallback 文案 |

新增教程包时，owner 应公开 `Tours` 静态类，成员类型为 `TutorialPackageRef`。已有 ID 的字符串值用于持久化状态兼容，不得随意改名。

Owner package 示例：

```csharp
public static class Tours
{
    public static readonly TutorialPackageRef TeamNameBasic =
        new(TutorialPackageIds.TeamInfoTeamNameBasic);
}

public static void RegisterTutorials(ITutorialBuilder builder)
{
    builder.ForPage<TeamInfoPage>()
        .Package(Tours.TeamNameBasic)
            .Step(nameof(HomeTeamNameInput), "填写队伍名称", "这里可以设置队伍名称。")
            .Action(nameof(HomeTeamNameConfirmButton), "确认队伍名称", "点击确认后写入当前比赛数据。")
                .WaitFor(TutorialSignalIds.TeamNameConfirmed)
            .Build();
}
```

动态导航项应使用 `.Navigation<TPage>(...)`，不要依赖菜单显示文本或给动态生成的 `NavigationViewItem` 写死 `x:Name`：

```csharp
builder.ForWindow<MainWindow>()
    .Package(MainWindow.Tours.NavigationTeamInfo)
        .Navigation<TeamInfoPage>("进入队伍管理", "先进入队伍管理页面，我们会设置本次教学使用的队伍。")
            .WaitFor(TutorialSignalIds.NavigationTeamInfoOpened)
        .Build();
```

DataTemplate 内的目标应使用 `.Descendant<TControl>(...)` 或 `.DescendantAction<TControl>(...)` 指向稳定 host 下的第一个指定类型控件：

```csharp
builder.ForPage<BanSurPage>()
    .Package(BanSurPage.Tours.CharacterSelectorBasic)
        .DescendantAction<CharacterSelector>(
            nameof(FirstBanSurvivorSelectorHost),
            "角色选择器",
            "输入后按空格可以搜索，按 Enter / Tab 或点击确认完成选择。")
            .WaitFor(TutorialSignalIds.CharacterSelectorSelectionConfirmed)
        .Build();
```

DataTemplate 内有稳定业务 ID 的控件可使用 `.Tag(...)` 或 `.TagAction(...)`。例如前台管理页的卡片“打开”按钮把 `Tag` 绑定到前台窗口 `WindowId`；BP 前台窗口教程使用 `FrontedWindowHelper.GetFrontedWindowGuid(FrontedWindowType.BpWindow)` 定位单个 BP Window 按钮，不指向“打开全部”按钮。

Authoring API 使用 `TutorialPackageRef` 引用 package。`builder.Flow(...).Step(MainWindow.Tours.NavigationFrontManage)` 会自动维护 `IncludedPackageIds`，并在 build 时校验引用的 package 已注册、不是 fallback package。Owner builder 只在同一个 owner authoring 链条内拒绝重复主内容，低层 registry 不做全局重复判定，以免误伤合法的分段教学。

Flow Builder 用于串联 dialogue 和 package 引用，不复制 package 内部步骤：

```csharp
builder.Flow(TutorialFlowIds.FirstRunStandardBp)
    .Version(1)
    .Step(new DialogueFlowItem { Speaker = "neo-bpsys-wpf", Lines = ["欢迎来到 neo-bpsys-wpf。"] })
    .Step(MainWindow.Tours.NavigationFrontManage)
    .Step(FrontedWindowsView.Tours.BpWindowLaunchBasic)
    .Step(MainWindowActivate)
    .Step(MainWindow.Tours.NavigationTeamInfo)
    .Step(TeamInfoPage.Tours.TeamNameBasic)
    .Build();
```

`IncludedPackageIds` 由 flow builder 根据 `TutorialPackageRef` 自动生成，用于覆盖状态；仍不能反过来用 `foreach IncludedPackageIds` 自动生成 flow items。正式标准 BP 总导览当前顺序是：前台管理打开 BP Window、进入队伍管理、队名与 MainWindow 顶部队伍摘要、预设队伍导入与选手管理、BO1 上半与开启对局引导、Ban 求生角色选择器、Pick 与全局禁选、比分、新建对局与全局禁选继承，最后只简单说明 v3 编辑器和智慧 BP 的独立教程。

前台管理、Designer v3 和 SmartBP 的复杂模块教程独立于首次标准 BP 主线：

1. `Page.FrontManage` 只拥有父级 overview、Tab 导航和导航 signal；`FrontedWindowsView` 与 `FrontedLayoutPackagesView` 分别在自身 Loaded/可见且宿主 Window 可见后提交自己的 sequence。父页不得扫描视觉树来发现或触发子教程 owner。
2. `Page.FrontManage.Windows` 和 `Page.FrontManage.LayoutPackages` 只有在 FrontManage 仍是当前主导航页且对应 tab 可见时运行。
3. `Window.DesignerV3` 在 ViewModel 挂接、初始布局加载、首个成功 preview render、`ContextIdle` 与 `Render` 完成后运行 overview、布局包和 Help sequence。属性教程保持 OnDemand，只由初始加载完成后的首次用户控件选择触发；行为教程只在已有选中控件且用户展开外层 Behavior Expander 后运行。
4. 动画编辑器在首个 tab、tab 内容和图编辑器渲染后运行自身完整 sequence。
5. `Page.SmartBp` 按 `SmartBpPageViewModel.IsModuleLoaded` 判断 ready；首次有效进入依次运行模块内容、OCR 模型管理、捕获、区域编辑、全流程 BP 和赛后回填 package。
6. 这些高级包不得加入 `Flow.FirstRun.StandardBp` 的 `IncludedPackageIds`，否则首次主线会错误地把未完整教学的功能标记为 `CoveredByFlow`。
7. 复杂模块步骤默认使用 `ProductTourInteractionMode.AllowTargetOnly`，允许用户点击被高亮目标；文件选择、捕获、导入、保存、打开全部窗口和启动识别等动作不作为必须完成条件。

## 固定 UI 文案

Product Tour 控件固定 UI 文案通过 `ITutorialTextProvider` 提供。`AddProductTour()` 注册 `DefaultTutorialTextProvider`，主程序在其后注册 `NeoBpsysTutorialTextProvider` 覆盖默认实现。当前 provider 先保留中文默认文本，后续接正式 resx 或 `WPFLocalizeExtension` 时应优先在 provider 内集中处理。

package 的业务标题和说明暂时仍在 `NeoBpsysTutorialTexts.cs` 中维护；后续接入本地化 key 时，保持 `PackageId`、`FlowId`、`PageKey` 不变。

## 页面接入

已迁移的主线页面、FrontManage 子 view、Designer v3、BehaviorPanel、AnimationEditor 和 SmartBP 都通过 DI 获取 `ITutorialRunner`，并在确认当前 owner 仍 active 后调用 runner。旧 `TutorialPageLoader` 和 attached property 入口已删除，不再保留兼容层。

不要为了接入教程把 WPF `Page` 嵌入普通 `ContentControl` 或 `Grid`；页面承载规则仍遵循仓库 WPF 页面约束。

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
3. 页面、窗口或区域在 active trigger 中调用 `ITutorialRunner.RunSequenceAsync(...)`，并传入随 owner 卸载/关闭取消的 lifetime token。
4. 交互步骤所需的业务动作发布 signal。
5. 如果首次总导览需要覆盖该教程，只把 package id 加入 `IncludedPackageIds` 并用 `PackageFlowItem` 引用。
6. 不复制已有教程步骤到 flow。

修改 Welcome 或 overlay 视觉时：

1. 先对齐 Fluent / WPF-UI 资源。
2. 保持启动加载动画后的视觉连续性。
3. 避免网页落地页式装饰、强渐变和不属于应用设计语言的插画。
4. 不新增样式坐标类测试；需要验证时使用 XAML smoke 或行为测试。
