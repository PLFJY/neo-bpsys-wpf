# 前台窗口与布局

## 前台窗口是什么

“前台窗口”对应代码中的 `FrontedWindow`，是独立 WPF 窗口，用于直播软件捕获。它不是 Web frontend。

内置前台窗口包括 BP、过场、求生者/监管者比分、全局比分、赛后数据、小组件等。OBS 工作流通常是：后台控制数据，前台窗口显示画面，OBS 捕获前台窗口。

## 注册模型

内置前台窗口仍通过 `FrontedWindowInfo` 标注，并在宿主启动时注册：

```csharp
[FrontedWindowInfo("窗口 GUID", "窗口显示名称", new[] { "BaseCanvas", "MapBpCanvas|地图 BP" })]
services.AddFrontedWindow<TView, TViewModel>();
```

插件前台窗口不再通过 `FrontedWindowInfo` 反射扫描。v3 插件应实现 `IFrontedWindowPluginContributor`，通过 `FrontedPluginWindowDescriptor` 声明窗口：

```csharp
services.AddFrontedWindowPluginContributor<MyFrontedWindowContributor>();
```

descriptor 使用稳定 `WindowId`，并用 `FullWindowType = plugin:{PackageId}/{WindowTypeName}` 作为布局、`.bpui` manifest 和用户目录中的窗口身份。插件窗口分为两类：

| 类型 | 说明 |
| --- | --- |
| `PluginXaml` | 插件提供真实 WPF `Window` 类型，可选 ViewModel，由宿主统一显示/隐藏 |
| `PluginLayout` | 插件只声明 Canvas 和默认 `FrontedLayouts/{WindowTypeName}/{CanvasName}.json`，宿主用 `FrontedPluginLayoutWindow` 承载 v3 renderer |

## FrontedWindowService

`FrontedWindowService` 在构造时接收内置窗口实例，然后：

1. 从 `IFrontedWindowRegistry` 读取内置窗口和插件窗口 descriptor。
2. 注册内置窗口 singleton。
3. 注册插件 XAML 窗口或创建插件 v3 Layout 承载窗口。
4. 为所有可定制 Canvas 建立布局刷新入口。

核心状态：

| 字段 | 说明 |
| --- | --- |
| `FrontedWindows` | `windowId -> Window` |
| `FrontedWindowStates` | 窗口是否已显示 |
| `FrontedCanvas` | descriptor 声明的 `(windowId, canvasName)` 列表 |

> **注意**：旧 `FrontedWindowService` 的位置保存/恢复逻辑已删除。运行时不再从 `AppData` 读写 `{WindowName}Config-{CanvasName}.json`，也不再从 `Resources/FrontedDefaultPositions` 读取默认位置。前台布局状态现在完全由 v3 `FrontedLayouts` 驱动。

## 显示与隐藏

窗口由 `ShowWindow` / `HideWindow` / `AllWindowShow` / `AllWindowHide` 管理。关闭前台窗口时，`FrontedWindowBase.OnClosing` 会取消关闭并改为 `Hide()`，避免窗口实例被销毁后 DI singleton 状态和 OBS 捕获关系变得不可预期。

`FrontedWindowBase` 还会：

1. 自动用 `Viewbox` 包裹内容，让内容按窗口宽高填充。
2. 默认无边框、不可调整大小、居中启动。
3. 非设计模式下支持鼠标拖动窗口。

## 设计者模式和布局文件

> **注意**：旧版"真实前台窗口内编辑"设计器模式已废弃移除。Designer v3 独立编辑器（`FrontedDesignerWindow`）是当前唯一支持的设计编辑器。

Fronted Designer v3 的基础设施已经存在：`FrontedCanvasConfig` 可读取 root-level 控件 JSON，`IFrontedLayoutService` 会按活动布局方案读取：`builtin` 从 `Resources\FrontedLayouts` 读取且只读；普通方案从 `%APPDATA%\neo-bpsys-wpf\FrontedLayoutPackages\{PackageId}\layouts` 读取和保存；legacy `%APPDATA%\neo-bpsys-wpf\FrontedLayouts` 只作为兼容 fallback，不再是切换包时的复制目标。`IFrontedRenderer` 可用注册的控件工厂生成 Text/Image/GlobalScoreRow 等控件。`Text` 控件支持 `BindingPath` 绑定 `ISharedDataService`，也支持在 `BindingPath` 为空时用 `Text` 字段显示原样静态文本；两者同时存在时 `BindingPath` 优先。`Text.StringFormat` 只在绑定文本时生效，静态 `Text` 不会自动本地化或格式化。需要本地化静态文本时使用 `LocalizedText`，它通过 `LocalizationKey` 查 resx，并在语言变化时刷新；需要业务规则文本时，应使用 `GameProgressText` / `MapNameText` 等业务控件，而不是普通静态 `Text`。`Image` / `BorderedImage` 控件支持 `SizingMode`，并支持内部 `Lockable` 和 `PickingBorderAvailable` overlay；Ban 位推荐使用 `Image.BindingPath` 绑定 `HeaderImageSingleColor` 并用 `LockVisibilityBindingPath` 绑定 `CanCurrent*BannedList[i]` / `CanGlobal*BannedList[i]`，pick 呼吸边框推荐用 `PickingBorderName` 注册稳定动画目标。迁移默认布局时必须按旧 XAML 审计选择模式，不要全局强制所有图片填满容器。`ScoreSurWindow`、`ScoreHunWindow`、`ScoreGlobalWindow`、`CutSceneWindow`、`GameDataWindow`、`WidgetsWindow` 和 `BpWindow` 当前已接入 v3 renderer。单 Canvas 窗口的内置默认布局位于 `Resources\FrontedLayouts\{WindowTypeName}\BaseCanvas.json`；`WidgetsWindow` 是多 Canvas 前台窗口，使用 `Resources\FrontedLayouts\WidgetsWindow\MapBpCanvas.json`、`Resources\FrontedLayouts\WidgetsWindow\BpOverViewCanvas.json`、`Resources\FrontedLayouts\WidgetsWindow\MapV2Canvas.json` 三份独立布局。局内比分窗口、GameData 顶部比分文本、Widgets overview 小比分和 BpWindow 顶栏比分已绑定 `CurrentGame.MatchScore` 派生字段：大比分读取 `CurrentSurTeamMajorText` / `CurrentHunTeamMajorText`，小比分（MinorScore）预分读取 `CurrentSurTeamPreHalfMinorScoreText` / `CurrentHunTeamPreHalfMinorScoreText`。全局比分窗口总分绑定 `CurrentGame.MatchScore.HomeTotalMinorScore` / `AwayTotalMinorScore`；比分行由 `GlobalScoreRow.Cells` 显式配置每个半场格的位置、尺寸、Visibility 和样式覆盖，再从 `CurrentGame.MatchScore` 按 `TeamType`、`GameNumber`、`GameKind`、`HalfKind` 解析文本和阵营图标。BO5/root 与 BO3 Canvas state 可以携带不同的 cell 布局，运行时不再用 `MajorGameGap` / `HalfGameGap` 或 `GlobalScoreTotalMargin` 计算 v3 ScoreGlobal 行布局。`FrontedWindowService.SetGlobalScore*` / `ResetGlobalScore` 仅作为 obsolete no-op 兼容适配器保留，不再作为 UI 状态来源。`CutSceneWindow` 默认布局使用 `TalentTraitDisplay`、`GameProgressText`、`MapNameText` 封装天赋/辅助特质、BO3/BO5 进度文本和地图名本地化；`GameDataWindow` 默认布局使用 `LocalizedText` 处理表格表头，并复用 `GameProgressText`、`MapNameText`；`WidgetsWindow` 的 overview Ban 槽和 `BpWindow` 的当前/全局 Ban 槽均已迁移为通用 `Image` + `Lockable` overlay，`BpWindow` pick 图使用 `Image` + `PickingBorderAvailable` overlay 保留 `AnimationService` 查找目标。`CurrentBanDisplay`、`BanSlotDisplay` 和 `PickingBorderOverlay` 已移除，不再作为旧布局兼容控件读取，也不作为新默认布局模型。`MapV2Display` 故意复用 `MapV2Presenter`，不要把地图 BP v2 拆成普通图片和文字控件。`FrontedWindowService` 不会读取旧 `FrontedDefaultPositions` 作为 v3 输入。

当前已迁移的内置前台窗口全部使用 v3 layout 作为默认渲染来源。多 Canvas 窗口必须按 Canvas 维度读取、编辑、保存和恢复布局；例如 `WidgetsWindow` 的 `MapBpCanvas`、`BpOverViewCanvas`、`MapV2Canvas` 不能合并成一个布局文件，也不能在编辑器中只暴露窗口级单一画布。

v3 renderer 会为生成控件注册 namescope 名称，并在清理生成控件前注销这些名称。这样 `BpWindow` 迁移后，`AnimationService` 仍可通过 `window.FindName("SurPick0")`、`window.FindName("HunPick")`、`window.FindName("SurPickingBorder0")`、`window.FindName("HunPickingBorder")` 找到动画目标。

v3 layout 中 root-level 控件 JSON key 就是控件名。该名称同时作为 `FrontedCanvasConfig.Controls` key、生成控件 `FrameworkElement.Name` 和 namescope 注册名。独立编辑器必须通过设计项 `Name` 编辑 dictionary key，不能给 config 类新增重复 `Name` 字段。详细编辑器规格见 [fronted-designer-editor.md](fronted-designer-editor.md)。

v3 layout 支持通用 BO3/BO5 Canvas states。root-level state 是默认/BO5；`EnableBoModeStates = true` 时，`BoModeStates["Bo3"]` 可保存独立 BO3 背景、插件依赖和控件集合。所有内置 v3 前台窗口和插件 Layout 承载窗口都会在 `ISharedDataService.IsBo3ModeChanged` 后重载布局，renderer 根据当前 BO 模式选择 root/BO5 或 BO3 state；如果启用但缺少 BO3 state，则回退 root/BO5 并记录 warning。`ScoreGlobalWindow` 只是该通用机制的一个使用者，不再使用窗口专用背景切换逻辑。

后台侧独立 `FrontedDesignerWindow` shell 已实现。它通过 `FrontedDesignerLayoutCatalog` 只列出已迁移的内置 v3 窗口和 Canvas：`ScoreSurWindow/BaseCanvas`、`ScoreHunWindow/BaseCanvas`、`ScoreGlobalWindow/BaseCanvas`、`CutSceneWindow/BaseCanvas`、`GameDataWindow/BaseCanvas`、`WidgetsWindow/MapBpCanvas`、`WidgetsWindow/BpOverViewCanvas`、`WidgetsWindow/MapV2Canvas` 和 `BpWindow/BaseCanvas`。选择窗口和 Canvas 后，编辑器按 `IFrontedLayoutService` 的活动布局方案规则加载 JSON，转换成 `FrontedCanvasDesignDocument`，运行 `FrontedLayoutValidator`，再用现有 `IFrontedRenderer` 渲染到编辑器自己的只读 `PreviewCanvas`。如果当前活动方案是 `builtin`，保存时会自动复制出可编辑用户布局方案并激活，避免覆盖内置资源。

该预览 Canvas 的 `Width` 和 `Height` 直接来自 `FrontedCanvasConfig.CanvasWidth` / `CanvasHeight`，不使用真实前台窗口的 `ActualHeight`、外框或标题栏尺寸，因此不会引入标题栏高度偏移。独立编辑器已支持内存交互层、基础 Property Grid 和 Add Control：可选中普通设计项，编辑名称、布局、绑定文本和简单控件属性，把新控件添加到当前内存文档并即时重渲染预览。它仍不创建真实 `BpWindow`、`ScoreWindow`、`CutSceneWindow` 等前台输出窗口作为设计 surface，也不实现 Binding/Resource Browser、保存或重置用户布局。

注意：`ScoreGlobalWindow` 的 BO3/BO5 背景、总分位置和比分行配置现在由通用 Canvas state 控制；BO5 使用 root state，BO3 使用 `BoModeStates["Bo3"]`。

legacy 布局文件命名约定（仅用于 legacy `.bpui` 转换，不再被运行时读取）：

```text
%APPDATA%\neo-bpsys-wpf\{WindowTypeName}Config-{CanvasName}.json
%APPDATA%\neo-bpsys-wpf\{WindowTypeName}Config-{CanvasName}.default.json
```

legacy 内置默认布局位于：

```text
neo-bpsys-wpf/Resources/FrontedDefaultPositions
{pluginFolder}/FrontedDefaultPositions/{WindowTypeName}Config-{CanvasName}.default.json
```

> **注意**：这些 legacy 位置文件不再被运行时 `FrontedWindowService` 读取。它们只属于 legacy `.bpui` 转换流程。重置布局通过 Layout Packages 激活内置布局或删除用户布局实现。

v3 布局文件命名约定：

```text
%APPDATA%\neo-bpsys-wpf\FrontedLayouts\{WindowTypeName}\{CanvasName}.json
neo-bpsys-wpf/Resources/FrontedLayouts/{WindowTypeName}/{CanvasName}.json
```

v3 独立编辑器保存用户布局时应写入 AppData 的 `FrontedLayouts` 目录；“重置为内置”应删除或忽略用户布局，再回落到 `Resources/FrontedLayouts` 下的默认布局。

`FrontManagePage` 使用顶层 tabs：`Frontend Windows` 提供前台窗口打开/关闭和独立编辑器入口，`Layout Packages` 提供 v3 布局包管理器。`Frontend Windows` tab 不再包含旧版设计模式 ToggleSwitch，Reset 按钮也已移除；布局重置通过激活/删除包实现。包列表使用紧凑两栏布局，右侧详情按 Basic、Contents、Location、Validation 分组。当前可列出系统内置包、已安装包和活动包状态，并可导入、导出、激活和删除 v3 `.bpui` 包。导出固定为全部前台布局；导入 legacy `.bpui` 会触发转换流程。

激活普通包时，包内 `layouts/{WindowTypeName}/{CanvasName}.json` 会复制到 `%APPDATA%\neo-bpsys-wpf\FrontedLayouts\{WindowTypeName}\{CanvasName}.json`，可选 `layouts/{WindowTypeName}/window.json` 也会复制到同一窗口目录。激活内置布局或删除活动包会清空 `FrontedLayouts` 用户布局目录并回退到内置 `Resources/FrontedLayouts`。已打开的前台窗口会尝试重新渲染 v3 布局。

注意：v3 布局读取用户布局优先。如果用户目录下已有旧的 `ScoreSurWindow` / `ScoreHunWindow` / `ScoreGlobalWindow` / `CutSceneWindow` / `GameDataWindow` / `WidgetsWindow` v3 JSON，且其中比分字段仍绑定旧字段、缺少 `GlobalScoreRow`、没有业务控件、把本地化表头写成普通静态 `Text`，或 Widgets overview 仍读取 `Team.Score`，运行时会继续使用用户布局；需要恢复默认布局或后续迁移工具才能切换到当前内置布局。

## 插件前台窗口和控件

旧的“向现有内置前台窗口注入 WPF 控件”能力已移除。插件应使用 Designer v3 插件控件、Plugin XAML Window 或 Plugin v3 Layout Window。

`WindowId` 是运行时身份，必须稳定；`FullWindowType` 是 layout / `.bpui` 身份。内置窗口使用 `BpWindow` 等短名，插件窗口使用 `plugin:{PackageId}/{WindowTypeName}`。磁盘路径会转换为安全目录，例如 `FrontedLayouts/plugin/top.plfjy.demo/ExampleLayoutOverlay/BaseCanvas.json`。

## 透明背景

部分窗口设置支持 `AllowsWindowTransparency`。代码中透明时背景 Brush 返回 `Transparent`，否则回退到默认绿幕色 `#00FF00` 或用户配置色。改前台窗口背景时要同时考虑 OBS 抠色、透明窗口和 WPF 渲染性能。
