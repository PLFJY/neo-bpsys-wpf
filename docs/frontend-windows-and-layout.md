# 前台窗口与布局

`builtin` 与已安装方案一样是普通活动布局包，只是物理根目录指向应用运行时 `Resources/FrontedLayouts`。package manager 模式不读取旧用户布局存储。包切换会完整刷新已创建 v3 窗口；透明度变化通过单窗口静默重启生效。

## 前台窗口是什么

“前台窗口”对应代码中的 `FrontedWindow`，是独立 WPF 窗口，用于直播软件捕获。它不是 Web frontend。

内置前台窗口包括 BP、过场、求生者/监管者比分、全局比分、赛后数据、BP 概览和地图 BP v2 等。OBS 工作流通常是：后台控制数据，前台窗口显示画面，OBS 捕获前台窗口。

## 注册模型

内置前台窗口仍通过 `FrontedWindowInfo` 标注，并在宿主启动时注册：

```csharp
[FrontedWindowInfo("窗口 GUID", "窗口显示名称")]
services.AddFrontedWindow<TView, TViewModel>();
```

v3 layout 已改为 Window-centric。新的 v3 layout window 只以 Window 为管理单位，运行时固定由 `FrontedWindowBase` 创建 `ViewBox -> Canvas BaseCanvas`，不再向用户、包管理或 FrontManagePage 暴露 Canvas。传统固定 XAML window 可继续使用原有注册方式，但不强制 BaseCanvas。

新式插件前台窗口通过 `IFrontedWindowPluginContributor` 和 `FrontedPluginWindowDescriptor` 声明。descriptor 默认创建插件自己的 XAML `Window`；仅显式指定 `Kind = PluginLayout` 时才使用 v3 layout host。现有插件仍可使用 `FrontedWindowInfo` + `AddFrontedWindow<TWindow,TViewModel>()` 注册自己的 XAML 窗口；旧 canvas 元数据被忽略。窗口类型不在 `manifest.yml` 中指定：

```csharp
services.AddFrontedWindowPluginContributor<MyFrontedWindowContributor>();
```

descriptor 使用稳定 `WindowId`，并用 `FullWindowType = plugin:{PackageId}/{WindowTypeName}` 作为布局、`.bpui` manifest 和用户目录中的窗口身份。插件窗口分为两类：

| 类型 | 说明 |
| --- | --- |
| `PluginXaml` | 插件提供真实 WPF `Window` 类型，可选 ViewModel，由宿主统一显示/隐藏 |
| `PluginLayout` | 插件只声明窗口和默认 `FrontedLayouts/{WindowTypeName}.json`，宿主用 `FrontedWindowBase` 配置驱动 host 承载 v3 renderer |

## FrontedWindowService

`FrontedWindowService` 构造时只保存服务依赖和 registry，不创建前台输出窗口。窗口实例通过 `EnsureWindowCreated(windowId)` 按需创建：

1. `ShowWindow(windowId)` 首次调用时只创建对应 descriptor 的窗口。
2. `AllWindowShow()` 遍历 registry descriptor，再逐个按需创建并显示。
3. `HideWindow()`、`GetWindowName()`、动画查找和布局 dirty 标记都不会创建窗口。
4. 已创建窗口保留在 `FrontedWindows` 表中；关闭按钮触发 `OnClosing -> Hide()`，不会销毁窗口内容。

核心状态：

| 字段 | 说明 |
| --- | --- |
| `FrontedWindows` | `windowId -> Window`；v3 layout window 由 `FrontedWindowBase` host 创建 |
| `FrontedWindowStates` | 窗口是否已显示 |

> **注意**：旧 `FrontedWindowService` 的位置保存/恢复逻辑已删除。运行时不再从 `AppData` 读写 `{WindowName}Config-{CanvasName}.json`，也不再从 `Resources/FrontedDefaultPositions` 读取默认位置。前台布局状态现在完全由 v3 `FrontedLayouts` 驱动。

## 显示与隐藏

窗口由 `ShowWindow` / `HideWindow` / `AllWindowShow` / `AllWindowHide` 管理。关闭前台窗口时，`FrontedWindowBase.OnClosing` 会取消关闭并改为 `Hide()`，避免窗口实例被销毁后 OBS 捕获关系变得不可预期。`FrontedWindowService` 需要真正销毁实例时会走显式 service close 通道，绕过 close-to-hide。

v3 layout 窗口的显示流程分两段执行：Show 前调用 `EnsureInitialWindowSettingsAppliedAsync()`，只读取并应用 `WindowSettings` 中必须在 HWND/source 创建前确定的设置（尺寸、位置、Topmost、AllowsTransparency、BackgroundColor、ViewboxStretch）；随后立刻 `Show()`。完整 `CanvasSettings`、控件渲染、资源解析和 behavior runtime attach 由 `LoadOrReloadContentAsync(force: false)` 在 Show 后异步完成。

已经创建且 `IsContentRendered == true`、`IsLayoutDirty == false` 的 v3 窗口再次显示时，不重新加载完整 layout，也不重新 `RenderToCanvas`。如果 Hide/Unloaded 时 behavior runtime 已 detach，下一次 Show 只重新 attach behavior runtime，不重建控件。Designer 保存布局、包激活/删除或 BO3/BO5 切换会标记或触发 reload；普通 Hide/Show 不会把内容标脏。

`FrontedWindowBase` 还会：

1. 自动用 `Viewbox` 包裹内容，让内容按窗口宽高填充。
2. 默认无边框、不可调整大小、居中启动。
3. 非设计模式下支持鼠标拖动窗口。

## 设计者模式和布局文件

> **注意**：旧版"真实前台窗口内编辑"设计器模式已废弃移除。Designer v3 独立编辑器（`FrontedDesignerWindow`）是当前唯一支持的设计编辑器。

Fronted Designer v3 的基础设施已经存在：`FrontedWindowConfig` 是新主路径模型，包含 `WindowSettings`、`CanvasSettings` 和 `ControlLayout`。`IFrontedLayoutService` 会按活动布局方案读取 `FrontedLayouts/{WindowTypeName}.json`；内置默认布局位于 `Resources\FrontedLayouts\{WindowTypeName}.json`，包内布局位于 `FrontedLayouts/{WindowTypeName}.json`，behavior 位于 `FrontedBehaviors/{WindowTypeName}.behaviors.json`。`IFrontedRenderer` 可用注册的控件工厂生成 Text/Image/GlobalScoreRow 等控件。`Text` 和 `LocalizedText` 的动态内容使用 `TextBinding`：有序 `Sources` 绑定 `ISharedDataService`，`StringFormat` 非空时使用复合格式，否则按 `JoinSeparator` 连接；没有有效 source 时分别回退到静态 `Text` 或 `LocalizationKey`。需要业务规则文本时，应使用 `GameProgressText` / `MapNameText` 等业务控件，而不是普通静态 `Text`。`WidgetsWindow` 和 MapV1 已删除；旧 `BpOverViewCanvas` 迁移为 `BpOverviewWindow`，旧 `MapV2Canvas` 迁移为 `MapV2Window`。`MapV2Display` 继续复用 `MapV2Presenter`，不要把地图 BP v2 拆成普通图片和文字控件。`FrontedWindowService` 不会读取旧 `FrontedDefaultPositions` 作为 v3 输入。

当前已迁移的内置前台窗口全部使用 v3 layout 作为默认渲染来源。v3 layout 管理单位只剩 Window；Canvas/BaseCanvas 只是运行时实现细节，不再按 Canvas 维度读取、编辑、保存或恢复布局。

v3 renderer 会为生成控件注册 namescope 名称，并在清理生成控件前注销这些名称。行为动画通过稳定 `BehaviorGuid` 和 `part:{BehaviorGuid}:PickingBorder` 定位目标；注册名称继续用于布局契约、迁移和诊断。

v3 layout 中 `ControlLayout.Controls` 的 JSON key 就是控件名。该名称同时作为控件 dictionary key、生成控件 `FrameworkElement.Name` 和 namescope 注册名。独立编辑器必须通过设计项 `Name` 编辑 dictionary key，不能给 config 类新增重复 `Name` 字段。详细编辑器规格见 [fronted-designer-editor.md](fronted-designer-editor.md)。

v3 layout 支持通用 BO3/BO5 Canvas states。`CanvasSettings` root 是默认/BO5；`EnableBoModeStates = true` 时，`BoModeStates["Bo3"]` 可保存独立 BO3 背景、插件依赖和控件集合。所有已创建的内置 v3 前台窗口和插件 Layout 承载窗口都会在 `ISharedDataService.IsBo3ModeChanged` 后标记 layout dirty；窗口可见时立即异步重载内容，窗口隐藏时等下次 Show 再刷新。renderer 根据当前 BO 模式选择 root/BO5 或 BO3 state；如果启用但缺少 BO3 state，则回退 root/BO5 并记录 warning。`ScoreGlobalWindow` 只是该通用机制的一个使用者，不再使用窗口专用背景切换逻辑。

## Naming rule: do not use generic IsActive

`IsActive` 只保留给内部框架/运行时激活语义，尤其是 CommunityToolkit.Mvvm `ObservableRecipient.IsActive`。

不要把 `IsActive` 用作布局、包、设置或业务状态字段。包激活状态使用 `IsActivePackage`；窗口列表或设计器可见性使用 `IsVisibleInFrontManage`、`IsVisible`、`IsBadgeVisible` 等明确名称；启用、选中、展开状态分别使用 `IsEnabled`、`IsSelected`、`IsExpanded`。

旧 `.bpui` 包可能在 `TextSettings` 中包含 `IsActive`，这是旧设置类继承 `ObservableRecipient` 造成的序列化泄漏。该字段不是文本样式启用标记，LegacyConverter 必须忽略它。

`Visibility` 绑定必须使用 `IsVisible` 或具体的可见性语义属性，不得绑定泛名 `IsActive`。

后台侧独立 `FrontedDesignerWindow` shell 已实现。它通过 `FrontedDesignerLayoutCatalog` 只列出可定制 v3 layout window，例如 `ScoreSurWindow`、`ScoreHunWindow`、`ScoreGlobalWindow`、`CutSceneWindow`、`GameDataWindow`、`BpWindow`、`BpOverviewWindow` 和 `MapV2Window`。选择窗口后，编辑器按 `IFrontedLayoutService` 的活动布局方案规则加载 `FrontedWindowConfig`，内部转换到设计文档，运行 `FrontedLayoutValidator`，再用现有 `IFrontedRenderer` 渲染到编辑器自己的只读 `PreviewCanvas`。如果当前活动方案是 `builtin`，保存时会自动复制出可编辑用户布局方案并激活，避免覆盖内置资源。

该预览 Canvas 的 `Width` 和 `Height` 直接来自 `CanvasSettings.CanvasWidth` / `CanvasHeight`，不使用真实前台窗口的 `ActualHeight`、外框或标题栏尺寸，因此不会引入标题栏高度偏移。v3 layout window 的真实窗口宽高来自 `WindowSettings.WindowWidth` / `WindowHeight`，不会在普通读取、保存、包导入或导出时被 Canvas 尺寸覆盖。独立编辑器已支持内存交互层、基础 Property Grid 和 Add Control：可选中普通设计项，编辑名称、布局、绑定文本和简单控件属性，把新控件添加到当前内存文档并即时重渲染预览。它不创建真实前台输出窗口作为设计 surface。

注意：`ScoreGlobalWindow` 的 BO3/BO5 背景、总分位置和比分行配置现在由通用 Canvas state 控制；BO5 使用 root state，BO3 使用 `BoModeStates["Bo3"]`。

legacy 布局文件命名约定（仅用于 legacy `.bpui` 转换，不再被运行时读取）：

```text
%APPDATA%\neo-bpsys-wpf\{WindowTypeName}Config-{CanvasName}.json
%APPDATA%\neo-bpsys-wpf\{WindowTypeName}Config-{CanvasName}.default.json
```

legacy 内置默认布局位于：

```text
neo-bpsys-wpf/Resources/FrontedDefaultPositions
{pluginFolder}/FrontedDefaultPositions/{WindowTypeName}Config-{CanvasName}.default.json (legacy)
```

> **注意**：这些 legacy 位置文件不再被运行时 `FrontedWindowService` 读取。它们只属于 legacy `.bpui` 转换流程。重置布局通过 Layout Packages 激活内置布局或删除用户布局实现。

v3 布局文件命名约定：

```text
%APPDATA%\neo-bpsys-wpf\FrontedLayouts\{WindowTypeName}.json
neo-bpsys-wpf/Resources/FrontedLayouts/{WindowTypeName}.json
```

v3 独立编辑器保存用户布局时应写入 AppData 的 `FrontedLayouts` 目录；“重置为内置”应删除或忽略用户布局，再回落到 `Resources/FrontedLayouts` 下的默认布局。

`FrontManagePage` 使用顶层 tabs：`Frontend Windows` 提供前台窗口打开/关闭和独立编辑器入口，`Layout Packages` 提供 v3 布局包管理器。`Frontend Windows` tab 不再包含旧版设计模式 ToggleSwitch，Reset 按钮也已移除；布局重置通过激活/删除包实现。包列表使用紧凑两栏布局，右侧详情按 Basic、Contents、Location、Validation 分组。当前可列出系统内置包、已安装包和活动包状态，并可导入、导出、激活和删除 v3 `.bpui` 包。导出固定为全部前台布局；导入 legacy `.bpui` 会触发转换流程。

激活普通包时，包内 `FrontedLayouts/{WindowTypeName}.json` 和 `FrontedBehaviors/{WindowTypeName}.behaviors.json` 会作为活动包数据读取。激活内置布局或删除活动包会回退到内置 `Resources/FrontedLayouts`。已创建的前台窗口会尝试重新渲染 v3 布局；未创建过的窗口不会因此被创建。

注意：v3 布局读取用户布局优先。如果用户目录下已有旧开发版 v3 JSON，不保证兼容；需要重置为内置布局或通过 legacy `.bpui` 转换重新生成 Window-centric 布局。

## 插件前台窗口和控件

旧的“向现有内置前台窗口注入 WPF 控件”能力已移除。插件应使用 Designer v3 插件控件、Plugin XAML Window 或 Plugin v3 Layout Window。

`WindowId` 是运行时身份，必须稳定；`FullWindowType` 是 layout / `.bpui` 身份。内置窗口使用 `BpWindow` 等短名，插件窗口使用 `plugin:{PackageId}/{WindowTypeName}`。用户目录中的插件窗口磁盘路径会转换为安全路径，例如 `FrontedLayouts/plugin/top.plfjy.demo/ExampleLayoutOverlay.json`；插件自己的默认布局路径仍是 `{PluginFolder}/FrontedLayouts/{WindowTypeName}.json`。

## 透明背景

v3 layout window 的 `AllowsTransparency` 和 `BackgroundColor` 位于 `WindowSettings`。`AllowsTransparency` 必须在 WPF window source 初始化前应用；切换这类会影响 source 的 WPF 属性时，`FrontedWindowService` 会在窗口已经创建的情况下静默重启对应前台窗口实例，不需要重启应用。若窗口当前可见，旧实例会先发布 hidden、真正关闭并移出字典，然后创建新实例、在 Show 前应用最新 `WindowSettings`、Show 后重新加载 layout 内容并发布 shown。若窗口已创建但隐藏，只关闭并移除旧实例，下一次 `ShowWindow` 会按最新设置重新创建。若窗口从未创建，则无需操作。Canvas 纯色背景不属于 `CanvasSettings.BackgroundColor`，应使用 Rectangle / Shape 控件实现。
