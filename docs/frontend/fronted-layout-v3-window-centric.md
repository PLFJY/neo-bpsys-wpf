# Window-centric 布局数据与读取契约

本文负责 FrontedWindowConfig、磁盘路径与布局读写。窗口身份/实例生命周期见[窗口运行时](frontend-windows-and-layout.md)，包内 manifest、资源 URI 和导入导出限制见 [BPUI](bpui-package-v3.md)。

源码入口：[配置模型](../../neo-bpsys-wpf.Core/Models/FrontedLayout/FrontedWindowConfig.cs)、[布局服务](../../neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedLayoutService.cs)、[包管理器](../../neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedLayoutPackageManager.cs)、[路径映射](../../neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedV3LayoutWindowPathHelper.cs)。

## 运行时结构

每个 v3 layout window 由 `FrontedWindowBase` 作为配置驱动 host 创建：

```text
FrontedWindowBase
  -> ViewBox
     -> Canvas BaseCanvas
```

`BaseCanvas` 是内部实现细节。它只用于 renderer、behavior target resolver 和临时转换 helper 的 root canvas，不出现在布局路径、包 manifest、FrontManagePage 卡片或 Designer 选择器中。传统固定 XAML 前台窗口继续按原窗口逻辑创建，不强制使用 BaseCanvas。

`AllowsTransparency` 必须在 WPF source 初始化前应用；变化时由窗口服务静默重建受影响实例，具体流程见窗口运行时。`ViewBox` 负责缩放，控件坐标不因窗口大小变化而被重写。

## 配置模型

主路径模型是 `FrontedWindowConfig`：

```json
{
  "Version": 3,
  "WindowSettings": {
    "WindowWidth": 1440,
    "WindowHeight": 810,
    "WindowLeft": null,
    "WindowTop": null,
    "AllowsTransparency": true,
    "BackgroundColor": "#00000000",
    "Topmost": false,
    "ViewboxStretch": "Fill"
  },
  "CanvasSettings": {
    "CanvasWidth": 1440,
    "CanvasHeight": 810,
    "BackgroundImage": null,
    "EnableBoModeStates": false,
    "BoModeStates": {}
  },
  "ControlLayout": {
    "RequiredPlugins": [],
    "Controls": {}
  }
}
```

`BackgroundColor` 属于 `WindowSettings`，不属于 `CanvasSettings`。Canvas 内纯色背景应通过 Rectangle/Shape 控件实现。`ViewboxStretch` 用字符串 enum 名称保存，不保存数字。窗口位置能力迁入 `WindowSettings.WindowLeft` / `WindowSettings.WindowTop`；旧窗口状态服务只作为适配层或 legacy 入口，不再另建 v3 Canvas options 文件。

窗口宽高由 `WindowSettings.WindowWidth` / `WindowHeight` 独立保存，内部设计画布尺寸由 `CanvasSettings.CanvasWidth` / `CanvasHeight` 保存。普通读取、保存、包导入和包导出必须保留 WindowSettings；只有 legacy canvas-centric 转换缺少窗口尺寸时，才以 Canvas 设计尺寸初始化窗口尺寸。ViewBox 负责把固定设计坐标缩放到窗口内容区域，控件坐标不会随窗口 resize 被重写。

持久化主路径读写 `FrontedWindowConfig`；编辑器和 renderer 的内部 Canvas 适配不构成另一个磁盘格式。

## 状态与显示名称

CanvasSettings root/default 表示 BO5；启用 EnableBoModeStates 且存在 BoModeStates["Bo3"] 时，BO3 使用独立 BackgroundImage、RequiredPlugins 和 Controls。启用但缺失 BO3 state 时运行时回退 root 并记录 warning。不要在普通保存中用当前状态覆盖另一状态。

JSON 根级 DisplayNames 保存 zh_Hans、en_US、ja_JP 等译名，解析顺序为当前语言、简体中文、美国英语、日语，最后 Window ID；历史 JSON 缺字段继续使用既有资源回退。保存保持文档自身字典，不从选择器补齐空值。业务布尔命名见[命名规范](../naming-rules.md)。

## 路径

用户、内置和包内布局都使用一级窗口路径，路径键为 Canonical ID：

```text
FrontedLayouts/{CanonicalId}.json
FrontedBehaviors/{CanonicalId}.behaviors.json
```

Canonical ID 到文件路径的映射：

```text
BpWindow                          → FrontedLayouts/BpWindow.json
plugin:{PackageId}/{LocalId}      → FrontedLayouts/plugin/{PackageId}/{LocalId}.json
```

宿主不从插件安装目录加载默认 v3 Layout；插件 v3 窗口无默认 JSON 时使用空模板。

`.bpui` 包内结构为：

```text
FrontedLayouts/{CanonicalId}.json
FrontedBehaviors/{CanonicalId}.behaviors.json
resources/...
manifest.json
```

manifest 使用 window-centric layout model 标记，layout entry 只记录 Window 和 Path，不记录 Canvas。缺插件窗口或缺插件控件的数据文件必须保留；Registry 没有 registration 时不显示该窗口，但 importer/exporter/package manager 不删除未知 window layout 或 behavior 文件。

## 管理范围

管理对象只有 Window；注册、canonical identity 和自定义窗口作用域统一见[窗口运行时](frontend-windows-and-layout.md)。custom:{PackageId}/{WindowId} 映射为 FrontedLayouts/custom/{PackageId}/{WindowId}.json。BaseCanvas 不作为外部注册、选择、路径或 manifest 维度。

## Behavior

Behavior 文件是 window-level：

```text
FrontedBehaviors/{WindowTypeName}.behaviors.json
```

runtime host key 使用 Window scope。FrontedBehaviorDocument.CanvasName 是内部兼容字段，固定写 `BaseCanvas`；UI、路径、runtime key 和 manifest 不使用 CanvasName。TargetResolver 仍从内部 BaseCanvas root 搜索 `BehaviorGuid`。

## 活动包读取规则

`IFrontedLayoutService.LoadWindowConfigAsync` 只通过 `IFrontedLayoutPackageManager` 解析当前活动包，并统一返回非空 `FrontedWindowConfig`。加载优先级按窗口来源区分：

- 内置 v3 窗口：活动包 → 内置资源（`Resources/FrontedLayouts`）→ 空模板
- 插件 v3 窗口：活动包 → 空模板
- custom 窗口：活动包内声明与布局；通常无同身份内置资源，缺失时空模板

活动包缺布局、JSON 读取/反序列化失败时，按上述优先级继续回退；JSON `null` 视为损坏。不再读取旧用户布局存储或插件默认布局（`FrontedLayoutSource.PluginDefault` / `MissingOrError` 枚举值已删除）。`builtin` 是 package id，不是兜底路径。

布局服务本身检查大小、深度及反序列化结果，不执行完整 Designer 语义校验；不要把上述读取回退描述为所有 schema 错误都已被拦截。保存通过 EnsureWritableActivePackageAsync：普通包直接写入，builtin 首次写入先复制成可编辑包并激活；不写 loose FrontedLayouts。

## 历史输入与内部适配

启动 legacy 配置和旧 BPUI 由[迁移规则](legacy-fronted-layout-migration.md)描述，普通 runtime 不读 legacy loose 文件。FrontedCanvasConfig 虽不再是持久化主模型，仍用于 FrontedLayoutDesignConverter、设计文档、renderer 的内部适配及 legacy helper；不能误称这些当前调用也已删除。WindowConfig 不公开 FromCanvasConfig/ToCanvasConfig/SyncWindowSizeToCanvas 等旧主路径 helper。
