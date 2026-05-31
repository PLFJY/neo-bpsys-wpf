# 前台窗口与布局

## 前台窗口是什么

“前台窗口”对应代码中的 `FrontedWindow`，是独立 WPF 窗口，用于直播软件捕获。它不是 Web frontend。

内置前台窗口包括 BP、过场、求生者/监管者比分、全局比分、赛后数据、小组件等。OBS 工作流通常是：后台控制数据，前台窗口显示画面，OBS 捕获前台窗口。

## 注册

前台窗口类通过 `FrontedWindowInfo` 标注：

```csharp
[FrontedWindowInfo("窗口 GUID", "窗口显示名称", new[] { "BaseCanvas", "MapBpCanvas|地图 BP" })]
```

注册使用：

```csharp
services.AddFrontedWindow<TView, TViewModel>();
```

`AddFrontedWindow` 会：

1. 读取 `FrontedWindowInfo`。
2. 检查窗口 ID 是否重复。
3. 把信息写入 `FrontedWindowRegistryService.RegisteredWindow`。
4. 以 singleton 注册 ViewModel 和 Window。
5. 构造 Window 后设置 `DataContext`。

插件也使用同一扩展注册前台窗口。

## FrontedWindowService

`FrontedWindowService` 在构造时接收内置窗口实例，然后：

1. 从 `FrontedWindowRegistryService.RegisteredWindow` 注册所有窗口和 Canvas。
2. 加载插件或宿主注入的外部控件。
3. DEBUG 下记录初始位置。
4. 从配置文件加载元素位置。

核心状态：

| 字段 | 说明 |
| --- | --- |
| `FrontedWindows` | `windowId -> Window` |
| `FrontedWindowStates` | 窗口是否已显示 |
| `FrontedCanvas` | `(windowId, canvasName)` 列表 |
| `InjectedControls` | 来自 Core 注册表的插件/宿主注入控件 |

## 显示与隐藏

窗口由 `ShowWindow` / `HideWindow` / `AllWindowShow` / `AllWindowHide` 管理。关闭前台窗口时，`FrontedWindowBase.OnClosing` 会取消关闭并改为 `Hide()`，避免窗口实例被销毁后 DI singleton 状态和 OBS 捕获关系变得不可预期。

`FrontedWindowBase` 还会：

1. 自动用 `Viewbox` 包裹内容，让内容按窗口宽高填充。
2. 默认无边框、不可调整大小、居中启动。
3. 非设计模式下支持鼠标拖动窗口。

## 设计者模式和布局文件

设计者模式通过 `DesignBehavior.IsDesignerMode` 给控件加 `CanvasAdorner`，让用户调整 Canvas 内元素位置。

Fronted Designer v3 的基础设施已经存在：`FrontedCanvasConfig` 可读取 root-level 控件 JSON，`IFrontedLayoutService` 按用户布局优先、内置默认布局兜底读取 `%APPDATA%\neo-bpsys-wpf\FrontedLayouts\{WindowTypeName}\{CanvasName}.json` 或 `Resources\FrontedLayouts\{WindowTypeName}\{CanvasName}.json`，`IFrontedRenderer` 可用注册的控件工厂生成 Text/Image/GlobalScoreRow 等控件。`Text` 控件支持 `BindingPath` 绑定 `ISharedDataService`，也支持在 `BindingPath` 为空时用 `Text` 字段显示原样静态文本；两者同时存在时 `BindingPath` 优先。`Text.StringFormat` 只在绑定文本时生效，静态 `Text` 不会自动本地化或格式化。需要本地化静态文本时使用 `LocalizedText`，它通过 `LocalizationKey` 查 resx，并在语言变化时刷新；需要业务规则文本时，应使用 `GameProgressText` / `MapNameText` 等业务控件，而不是普通静态 `Text`。`Image` 控件支持 `SizingMode`：`Auto` 保留 WPF/default 行为，`FillContainer` 让内层 `Image` 跟随外层 `Border` 尺寸，`OverflowCrop` 保留旧 `Border + Image + ClipToBounds` 裁剪语义。迁移默认布局时必须按旧 XAML 审计选择模式，不要全局强制所有图片填满容器。`ScoreSurWindow`、`ScoreHunWindow`、`ScoreGlobalWindow`、`CutSceneWindow`、`GameDataWindow`、`WidgetsWindow` 和 `BpWindow` 当前已接入 v3 renderer。单 Canvas 窗口的内置默认布局位于 `Resources\FrontedLayouts\{WindowTypeName}\BaseCanvas.json`；`WidgetsWindow` 是多 Canvas 前台窗口，使用 `Resources\FrontedLayouts\WidgetsWindow\MapBpCanvas.json`、`Resources\FrontedLayouts\WidgetsWindow\BpOverViewCanvas.json`、`Resources\FrontedLayouts\WidgetsWindow\MapV2Canvas.json` 三份独立布局。局内比分窗口、GameData 顶部比分文本、Widgets overview 小比分和 BpWindow 顶栏比分已绑定 `CurrentGame.MatchScore` 派生字段：大比分读取 `CurrentSurTeamMajorText` / `CurrentHunTeamMajorText`，小比分（MinorScore）预分读取 `CurrentSurTeamPreHalfMinorScoreText` / `CurrentHunTeamPreHalfMinorScoreText`。全局比分窗口总分绑定 `CurrentGame.MatchScore.HomeTotalMinorScore` / `AwayTotalMinorScore`，比分行由 `GlobalScoreRow` 从 `CurrentGame.MatchScore` 生成；`FrontedWindowService.SetGlobalScore*` / `ResetGlobalScore` 仅作为 obsolete no-op 兼容适配器保留，不再作为 UI 状态来源。`CutSceneWindow` 默认布局使用 `TalentTraitDisplay`、`GameProgressText`、`MapNameText` 封装天赋/辅助特质、BO3/BO5 进度文本和地图名本地化；`GameDataWindow` 默认布局使用 `LocalizedText` 处理表格表头，并复用 `GameProgressText`、`MapNameText`；`WidgetsWindow` 使用 `CurrentBanDisplay` 封装当前 Ban 槽和锁定覆盖层，使用 `MapV2Display` 封装地图 BP v2 展示；`BpWindow` 使用 `BanSlotDisplay` 封装当前局/全局 Ban 槽和锁定覆盖层，使用独立 `PickingBorderOverlay` 保留 `AnimationService` 的呼吸边框目标。`MapV2Display` 故意复用 `MapV2Presenter`，不要把地图 BP v2 拆成普通图片和文字控件。`FrontedWindowService` 不会读取旧 `FrontedDefaultPositions` 作为 v3 输入。

当前已迁移的内置前台窗口全部使用 v3 layout 作为默认渲染来源。多 Canvas 窗口必须按 Canvas 维度读取、编辑、保存和恢复布局；例如 `WidgetsWindow` 的 `MapBpCanvas`、`BpOverViewCanvas`、`MapV2Canvas` 不能合并成一个布局文件，也不能在编辑器中只暴露窗口级单一画布。

v3 renderer 会为生成控件注册 namescope 名称，并在清理生成控件前注销这些名称。这样 `BpWindow` 迁移后，`AnimationService` 仍可通过 `window.FindName("SurPick0")`、`window.FindName("HunPick")`、`window.FindName("SurPickingBorder0")`、`window.FindName("HunPickingBorder")` 找到动画目标。

v3 layout 中 root-level 控件 JSON key 就是控件名。该名称同时作为 `FrontedCanvasConfig.Controls` key、生成控件 `FrameworkElement.Name` 和 namescope 注册名。独立编辑器必须通过设计项 `Name` 编辑 dictionary key，不能给 config 类新增重复 `Name` 字段。详细编辑器规格见 [fronted-designer-editor.md](fronted-designer-editor.md)。

Designer v3 Phase 8C 已新增后台侧独立 `FrontedDesignerWindow` shell。它通过 `FrontedDesignerLayoutCatalog` 只列出已迁移的内置 v3 窗口和 Canvas：`ScoreSurWindow/BaseCanvas`、`ScoreHunWindow/BaseCanvas`、`ScoreGlobalWindow/BaseCanvas`、`CutSceneWindow/BaseCanvas`、`GameDataWindow/BaseCanvas`、`WidgetsWindow/MapBpCanvas`、`WidgetsWindow/BpOverViewCanvas`、`WidgetsWindow/MapV2Canvas` 和 `BpWindow/BaseCanvas`。选择窗口和 Canvas 后，编辑器按 `IFrontedLayoutService` 的用户布局优先、内置布局兜底规则加载 JSON，转换成 `FrontedCanvasDesignDocument`，运行 `FrontedLayoutValidator`，再用现有 `IFrontedRenderer` 渲染到编辑器自己的只读 `PreviewCanvas`。

该预览 Canvas 的 `Width` 和 `Height` 直接来自 `FrontedCanvasConfig.CanvasWidth` / `CanvasHeight`，不使用真实前台窗口的 `ActualHeight`、外框或标题栏尺寸，因此不会引入标题栏高度偏移。Phase 8F 后，独立编辑器已支持内存交互层、基础 Property Grid 和 Add Control：可选中普通设计项，编辑名称、布局、绑定文本和简单控件属性，把新控件添加到当前内存文档并即时重渲染预览。它仍不创建真实 `BpWindow`、`ScoreWindow`、`CutSceneWindow` 等前台输出窗口作为设计 surface，也不实现 Binding/Resource Browser、保存或重置用户布局。

注意：`ScoreGlobalWindow` 当前没有完整条件布局引擎。BO3 模式下 `GlobalScoreRow` 会隐藏 BO5 后续比分格，但总分位置和背景仍采用固定 v3 layout。

legacy 布局文件命名约定：

```text
%APPDATA%\neo-bpsys-wpf\{WindowTypeName}Config-{CanvasName}.json
%APPDATA%\neo-bpsys-wpf\{WindowTypeName}Config-{CanvasName}.default.json   # DEBUG 记录初始位置时可能生成
```

legacy 内置默认布局位于：

```text
neo-bpsys-wpf/Resources/FrontedDefaultPositions
```

插件前台窗口默认布局位于插件目录下：

```text
{pluginFolder}/FrontedDefaultPositions/{WindowTypeName}Config-{CanvasName}.default.json
```

恢复默认布局时，服务优先查内置资源；如果不是内置窗口，会通过 `PluginService.FrontedWindowAssemblyFolder` 找到插件目录。

v3 布局文件命名约定：

```text
%APPDATA%\neo-bpsys-wpf\FrontedLayouts\{WindowTypeName}\{CanvasName}.json
neo-bpsys-wpf/Resources/FrontedLayouts/{WindowTypeName}/{CanvasName}.json
```

v3 独立编辑器保存用户布局时应写入 AppData 的 `FrontedLayouts` 目录；“重置为内置”应删除或忽略用户布局，再回落到 `Resources/FrontedLayouts` 下的默认布局。

注意：v3 布局读取用户布局优先。如果用户目录下已有旧的 `ScoreSurWindow` / `ScoreHunWindow` / `ScoreGlobalWindow` / `CutSceneWindow` / `GameDataWindow` / `WidgetsWindow` v3 JSON，且其中比分字段仍绑定旧字段、缺少 `GlobalScoreRow`、没有业务控件、把本地化表头写成普通静态 `Text`，或 Widgets overview 仍读取 `Team.Score`，运行时会继续使用用户布局；需要恢复默认布局或后续迁移工具才能切换到当前内置布局。

## 插件注入控件

插件可调用：

```csharp
FrontedWindowHelper.InjectControlToFrontedWindow(
    "control-id",
    control,
    FrontedWindowType.BpWindow,
    "BaseCanvas",
    new ElementInfo(width, height, left, top));
```

该调用只是把 `InjectedControlInfo` 放入静态注册表。真正加入 Canvas 发生在 `FrontedWindowService.LoadInjectedControl()`。注入控件会绑定目标 Canvas DataContext 的 `IsDesignerMode`，因此能参与位置编辑。

注意：

1. 注入控件需要稳定、唯一的 ID。
2. 控件应有 `Name`，否则布局保存时不会记录。
3. 布局保存会跳过 `Tag == "nv"` 的元素。
4. 插件新增窗口或注入控件通常需要重启宿主才能进入当前 DI/注册表。

## 透明背景

部分窗口设置支持 `AllowsWindowTransparency`。代码中透明时背景 Brush 返回 `Transparent`，否则回退到默认绿幕色 `#00FF00` 或用户配置色。改前台窗口背景时要同时考虑 OBS 抠色、透明窗口和 WPF 渲染性能。
