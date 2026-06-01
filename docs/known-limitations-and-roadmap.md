# 已知边界与路线图提醒

本文面向维护者和 AI agent，用于避免把当前边界误判为已完成能力。

## SmartBP

| 能力 | 状态 |
| --- | --- |
| 赛后数据 OCR 自动回填 | 成熟且可用 |
| 全流程自动 BP / 自动 BP 画面切换 | TODO |

`SmartBpService.StartSmartBp()` 当前有 `DispatcherTimer` 框架，但 `Timer_Tick` 中还没有完整自动 BP 识别/切屏流程。不要在未明确收到需求时补全这条链路，也不要在文档或 UI 中声称它已完成。

## 插件系统

插件是全信任模型，不是沙箱。当前风险控制是插件市场审核、微步云恶意文件扫描、人工审查和较小的插件生态。

插件加载发生在启动期间、Host build 前。安装或更新插件后需要重启，不能假设支持热加载或热更新。

`Assembly.LoadFrom` 加载入口程序集，依赖解析主要依赖插件目录、宿主已有程序集和 .NET 默认上下文。插件打包时不要漏掉自身直接依赖，也不要把宿主已有依赖重复打包成冲突版本。

## 版本概念

插件 API 版本和 PluginSdk NuGet 包版本是故意分离的：

| 概念 | 用途 |
| --- | --- |
| 插件 API 版本 | `manifest.yml` 的 `apiVersion`，宿主加载兼容性 |
| PluginSdk NuGet 包版本 | 插件项目编译和打包用的 SDK 包版本 |

不要把二者不同步当作错误。

## 前台窗口术语

`FrontedWindow` / 前台窗口是 WPF 输出窗口，用于 OBS 或其他直播软件捕获，不是 Web frontend。不要引入 Web 路由、浏览器布局或前后端分离假设。

Fronted Designer v3 当前已完成基础设施阶段，并将 `ScoreSurWindow`、`ScoreHunWindow`、`ScoreGlobalWindow`、`CutSceneWindow`、`GameDataWindow`、多 Canvas 的 `WidgetsWindow` 和 `BpWindow` 接入 v3 renderer；这些窗口的内置默认布局比分文本已绑定 `CurrentGame.MatchScore`。Phase 9F 起，legacy `.bpui` 可在导入前转换为干净 v3 包，转换不会覆盖全局 `Config.json`。旧设计者模式、旧 `config.json` 前台设置和旧 XAML-first 运行时仍保留。

Phase 8H 仍有明确边界：Resource Browser 不复制/导入外部图片；旧 `config.json` 前台设置仍保留；运行时关键控件名称只读且不能删除；被其他控件引用的普通控件在 reference-aware rename/delete 实现前阻止改名和删除；`PickingBorderOverlay` 不作为普通可选/可编辑/可添加控件。

Phase 9A 已新增 `.bpui v3` 标准文档，Phase 9C 已实现导出。当前边界：

1. `.bpui v3` 导出、导入/安装、激活复制和删除已实现；导出固定为全部前台布局。
2. `FrontManagePage` 的 Layout Packages 管理页已完成紧凑 UI 打磨、包枚举/活动状态、导入、导出、激活、删除和 legacy 转换入口。
3. Phase 9B.0 已实现 Canvas Properties GUI，可编辑 `CanvasWidth`、`CanvasHeight` 和 `BackgroundImage`，并支持本地背景图片复制为 `bpui://local/...`。
4. Phase 9B.0 已实现 `bpui://local` 和 `bpui://{PackageId}` 文件资源解析；Phase 9C 导出会把 `bpui://local/...`、其他包资源和绝对路径资源复制进导出包并重写为当前 `PackageId`，同时保持 `Resources/...` 和 `pack://application:,,,/...` 原样。
5. Phase 9B.0 已新增窗口级 `AllowTransparency` 选项基础，保存到 `FrontedLayouts/{WindowTypeName}/window.json`；它不是 Canvas 属性，不会写入 `FrontedCanvasConfig`。当前运行时前台窗口仍保留旧 `config.json` 透明设置绑定，读取 `window.json` 并在 `Show()` 前应用透明窗口选项留到后续清理阶段。
6. Phase 9D 后，`FrontManagePage` 的 Layout Packages 页能列出 `builtin`、普通已安装包和活动包状态，并能导入/导出 All Frontend Layouts `.bpui`；Current Canvas/Current Window 导出范围已从 UI 移除。
7. legacy `.bpui` 检测已接入转换：转换器安全解压旧包，使用内置 v3 layout 作为基底，只应用可明确映射的旧几何和资源，生成干净 v3 包后交给现有 importer。未知旧布局文件和无法映射字段会 warning，不会让运行时读取 legacy 格式。
8. ~~SettingPage 中现有 `.bpui` 导入/导出仍是 legacy/deprecated 流程，会处理 `Config.json`、`CustomUi/` 和 `FrontElementsConfig`，暂时保留；FrontManagePage 的 legacy 转换不会调用该旧导入流程，也不会覆盖全局 `Config.json`。~~ **Phase 10+ 已完成**：SettingPage 的旧 `.bpui` import/export UI 入口已删除。`SettingPageViewModel.UiPackage.cs` 已删除。旧 `.bpui` 现在通过 `FrontManagePage` 的 Layout Packages 管理，会触发 v3 转换，不再覆盖全局 Config.json。旧 Config 字段（如 `BpWindowSettings`、`ScoreWindowSettings` 等中的自定义字段）仍保留在模型中用于运行时控件，但不再作为用户可编辑入口。
9. Phase 9B.1 清理了 `FrontedDesignerWindow` 的重复入口：Delete 保留在 Edit menu 和左侧控件列表右键菜单，右侧 Property Grid 底部 Delete 已移除；`AllowTransparency` 保留在右侧 Window Options，不再出现在顶部 Window menu。
10. v3 导出器不会写入全局 `Config.json`、`CustomUi/` 或 `FrontElementsConfig/`；导入器校验这些禁止内容仍待 Phase 9D 实现。
11. Phase 10+ 已完成 Designer v3 边界收口：编辑器手写输入按上限截断，外部导入超限 JSON/manifest/layout/package 会拒绝；图片按用途限制大小和像素，resolver 对坏图安全返回 `null`；Canvas 控件数 160 warning、256 hard limit；编辑器支持内部 `Ctrl+C` / `Ctrl+V` 控件复制粘贴且不抢文本输入控件的普通复制粘贴。
12. **Phase 10+ 已移除旧版真实窗口设计器模式**：`DesignBehavior.cs`、`CanvasAdorner.cs`、`DesignerModeChangedMessage.cs` 已删除。所有窗口 ViewModel 的 `IsDesignerMode` 属性和 `IRecipient<DesignerModeChangedMessage>` 接口已移除。`FrontManagePage` 的 `ChangeDesignerMode` 命令和相关 Reset 按钮已移除。`FrontedWindowBase` 不再注册 `DesignerModeChangedMessage`，但保留正常拖窗功能。Designer v3 独立编辑器（`FrontedDesignerWindow`）是当前唯一支持的设计编辑器。
13. **Phase 10+ 已移除 SettingPage 旧前台自定义设置**：SettingPage 的 `CustomizeFrontendUI` 整块 UI 已删除，包括所有 `CardExpander`（BP Window、CutScene Window、Score Window、GameData Window、Widgets Window）。`SettingPageViewModel.FrontedUiCustom.cs` 和 `SettingPageViewModel.UiPackage.cs` 已删除。`AppConstants.CustomUiPath` 已删除。旧前台自定义图片、背景色、透明度、窗口尺寸、文字设置不再在 SettingPage 暴露。v3 布局包通过 `FrontManagePage` Layout Packages 管理。Designer v3 独立编辑器管理前台背景图、控件属性、文字样式、Window Options。旧 Config 字段（如 `BpWindowSettings`、`ScoreWindowSettings` 等中的自定义字段）仍保留在模型中用于运行时控件，但不再作为用户可编辑入口。
14. **Phase 10+ 已移除旧位置保存/恢复 API**：`IFrontedWindowService` 的 `RestoreInitialPositions`、`SaveWindowElementsPosition`、`SaveWindowCanvasElementsPosition`、`SaveAllWindowElementsPosition` 已删除。`FrontedWindowService` 的 `#region 设计者模式` 已删除。运行时不再从 `%APPDATA%` 读写 `{WindowName}Config-{CanvasName}.json`，也不再从 `Resources/FrontedDefaultPositions` 读取默认位置。`FrontedWindowService` 启动时不再调用旧位置加载逻辑，DEBUG 下也不再记录初始位置。前台布局状态完全由 v3 `FrontedLayouts` 驱动。重置布局通过 Layout Packages 激活内置布局或删除用户布局实现。旧 `ElementInfo` 仍保留用于插件注入控件的默认位置，`ElementInfo` 也仍由 legacy `.bpui` 转换流程使用。
15. **Phase 12/12B 已完成 Designer v3 显示层 i18n 收尾**：独立编辑器的 Property Grid 属性名/组名、ComboBox 选项显示、控件类型显示、窗口/Canvas 选择器、只读布尔值、颜色校验提示、Binding Browser / Resource Browser 文案和 Binding Browser 常用节点可以本地化。该能力只作用于 UI 显示层，不改变 v3 layout schema、JSON property name、`ControlType` 存储值、`BindingPath`、资源 URI、`FontFamily`、控件 `Name` 或 `.bpui` 导入/导出格式。Binding Browser 必须始终让原始路径可见，Resource Browser 必须始终让原始 URI/path 可见，ComboBox 保存值仍是原始英文契约值。

## 文档边界

公开 VuePress 文档面向用户，可能落后于 UI 或内部实现。仓库内 `/docs` 面向维护者和 AI agent，应跟随代码架构变化更新。

## 代码中观察到的边界

1. 运行时前台窗口的 Width/Height 目前**不自动同步** v3 layout 的 CanvasWidth/CanvasHeight。Designer v3 编辑器可以编辑 Canvas size，但窗口尺寸需要后续接入。用户入口已从 SettingPage 删除，旧 `Settings.*WindowSettings.WindowSize` 不再是用户可编辑入口，`FrontedWindowService` 旧位置保存/恢复路径和旧窗口大小注释块已清理，后续需实现窗口尺寸从 v3 layout/window.json 自动同步。
2. `neo-bpsys-wpf.Tests` 中 SmartBP 测试大多是注释中的手工调试样例，不能当作完整自动测试覆盖。
3. `App.xaml.cs` 更新检查条件写作 `#if !DEBUG && !Preview`，而项目配置定义 `PREVIEW`。这是代码观察到的命名 caveat；本文档不声称其运行时效果已经通过编译验证，本任务也不修改代码。
4. `GameRule.json` 是项目内规则配置，不是外部权威赛事规则源。
5. 前台默认布局依赖文件命名约定，插件窗口默认布局缺失时恢复默认会失败。

## Score System v2

比分系统正在迁移到现有 `Core.Models.Game` 持有权威状态，详见 [score-system-v2.md](score-system-v2.md)。Score Phase 5 已实现 `Game.MatchScore`、Score System v2 模型、`IMatchScoreService` / `MatchScoreService` 基础，把后台 `ScorePageViewModel` 的比分写入和普通 UI 清理迁移到 service 驱动，并把 `ScorePage` 导播预览表、`ScoreSurWindow` / `ScoreHunWindow` / `ScoreGlobalWindow` 默认布局的比分显示改为读取 `Game` 持有的 `MatchScoreState`。后台预览表是只读派生 UI，不恢复旧手动 Game/half 选择或“同步至前台”流程。当前代码仍存在这些边界：

| 边界 | 说明 |
| --- | --- |
| `Team.Score` 语义混杂 | 当前仅作为迁移期兼容镜像保留；不要重新让它成为权威状态。新服务中的同步只是 transitional compatibility mirror，不是权威状态，Score 系列默认 v3 布局、CutScene v3 默认布局、GameData v3 默认布局、WidgetsWindow v3 默认布局、BpWindow v3 默认布局和后台 ScorePage 已不再依赖它。 |
| `ScoreGlobalWindow` BO3 条件布局有限 | v3 `GlobalScoreRow` 会按 BO3/BO5 隐藏比分格，但总分位置和背景仍采用固定 layout；尚未实现完整条件布局引擎。 |
| `GameProgress.Free` 未定义比分语义 | Score System v2 暂把它记录为设计缺口。 |
| `Game3Overtime*` 与 `Game4*` enum 数值重叠 | `MatchScoreService` 结合 BO3/BO5 状态解析；缺少上下文的 `MatchScoreState.GetGame(progress)` 保守按 BO5 第四局解析。 |
| 旧记录 `Team.Score` 无法还原完整历史 | 旧 JSON 没有 `MatchScore` 时会创建默认 `MatchScoreState`，不会从 `Team.Score` 反推出 per-Game/per-Half 结果。 |
