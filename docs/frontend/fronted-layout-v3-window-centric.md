# Fronted Layout v3 Window-centric

内置布局方案是 `PackageId = builtin` 的普通活动包，布局根目录是应用运行时 `Resources/FrontedLayouts`。启用 package manager 后，活动包是权威来源，活动包为 `builtin` 时也不得回退旧用户布局存储。

激活任意包都会完整重载已创建 v3 窗口的 WindowSettings、CanvasSettings、控件、资源和 behavior runtime。`AllowsTransparency` 变化时只静默重启对应已创建窗口，未创建窗口不会因此被创建。

Fronted Layout v3 现在只以前台窗口为布局管理单位，不再把 Canvas 暴露为用户、Designer、FrontManagePage 或 `.bpui` package 的管理单位。

## 运行时结构

每个 v3 layout window 由 `FrontedWindowBase` 作为配置驱动 host 创建：

```text
FrontedWindowBase
  -> ViewBox
     -> Canvas BaseCanvas
```

`BaseCanvas` 是内部实现细节。它只用于 renderer、behavior target resolver 和临时转换 helper 的 root canvas，不出现在布局路径、包 manifest、FrontManagePage 卡片或 Designer 选择器中。传统固定 XAML 前台窗口继续按原窗口逻辑创建，不强制使用 BaseCanvas。

`AllowsTransparency` 必须在 WPF window source 初始化前应用。已显示窗口 reload layout 时不会直接修改 `AllowsTransparency`；如果配置变化，需要重开窗口或等待下次创建生效。`ViewBox` 负责缩放，控件坐标不因窗口大小变化而被重写。

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

`FrontedCanvasConfig` 只允许作为 legacy converter、旧测试断言或临时转换 helper 使用。新的 LayoutService、Designer、runtime 和 package 主路径应读写 `FrontedWindowConfig`。

## Naming rule: do not use generic IsActive

`IsActive` 只保留给内部框架/运行时激活语义，尤其是 CommunityToolkit.Mvvm `ObservableRecipient.IsActive`。

不要把 `IsActive` 用作 layout、package、settings 或业务数据字段。请使用明确语义名称：

- `IsActivePackage`
- `IsCurrentPackage`
- `IsVisible`
- `IsBadgeVisible`
- `IsEnabled`
- `IsSelected`
- `IsExpanded`
- `IsVisibleInFrontManage`

旧 `.bpui` 包可能在 `TextSettings` 中包含 `IsActive`，原因是旧设置类继承了 `ObservableRecipient` 并把框架激活状态泄漏进序列化。该字段不是文本样式启用标记，LegacyConverter 必须忽略它。

`Visibility` 绑定必须使用 `IsVisible` 或具体的可见性语义属性，不得绑定泛名 `IsActive`。

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
Resources/...
manifest.json
```

manifest 使用 window-centric layout model 标记，layout entry 只记录 Window 和 Path，不记录 Canvas。缺插件窗口或缺插件控件的数据文件必须保留；Registry 没有 registration 时不显示该窗口，但 importer/exporter/package manager 不删除未知 window layout 或 behavior 文件。

## Registry 和窗口

Registry 使用强类型 registration 模型，基类 `FrontedWindowRegistration` 只暴露 `Id`（Canonical ID）、`LocalId`、`PackageId`、`IsBuiltIn`、`DisplayName`、`Kind`。`Kind` 只有 `Xaml` / `V3Layout` 两种，由派生类 `FrontedXamlWindowRegistration` / `FrontedV3LayoutWindowRegistration` 固定返回。FrontManagePage 只从 registry 获取可管理窗口，来源分组（BuiltIn / Plugin / External）由 UI 层基于 `IsBuiltIn + PackageId` 推导，顺序使用 DI 注册顺序或 UI 按 `LocalId` 排序；基类不再有 `GroupKey` / `DisplayOrder` / `I18nDisplayNames`。

`WidgetsWindow` 已删除。`MapBpCanvas` / MapV1 已删除且不注册。旧 `BpOverViewCanvas` 迁移为 `BpOverviewWindow`，旧 `MapV2Canvas` 迁移为 `MapV2Window`。这两个窗口都是 registration + `FrontedWindowBase` host 驱动，不创建独立 XAML。

## Behavior

Behavior 文件是 window-level：

```text
FrontedBehaviors/{WindowTypeName}.behaviors.json
```

runtime host key 使用 Window scope。`FrontedBehaviorDocument.CanvasName` 如果暂时保留，固定写 `BaseCanvas`；UI、路径、runtime key 和 manifest 不使用 CanvasName。TargetResolver 仍从内部 BaseCanvas root 搜索 `BehaviorGuid`。

## 活动包读取规则

`IFrontedLayoutService.LoadWindowConfigAsync` 只通过 `IFrontedLayoutPackageManager` 解析当前活动包，并统一返回非空 `FrontedWindowConfig`。加载优先级按窗口来源区分：

- 内置 v3 窗口：活动包 → 内置资源（`Resources/FrontedLayouts`）→ 空模板
- 插件 v3 窗口：活动包 → 空模板

活动包缺布局、JSON 无效或 schema 无法通过验证时，按上述优先级继续回退；JSON `null` 视为损坏。不再读取旧用户布局存储或插件默认布局（`FrontedLayoutSource.PluginDefault` / `MissingOrError` 枚举值已删除）。`builtin` 是 package id，不是兜底路径。

legacy canvas 与 `FrontedCanvasConfig` 只能在启动迁移、旧 `.bpui` 转换和测试辅助中出现。主路径模型是 `FrontedWindowConfig`，public model 不再暴露 `FromCanvasConfig`、`ToCanvasConfig` 或 `SyncWindowSizeToCanvas`。

## Legacy 转换

Legacy `.bpui` 和旧 `Config.json` 继续支持，但输出新 `FrontedWindowConfig`。旧窗口尺寸、透明、背景色和位置映射到 `WindowSettings`；旧 Canvas 宽高、背景图和 BO 状态映射到 `CanvasSettings`；控件布局和 RequiredPlugins 映射到 `ControlLayout`。

Legacy `WidgetsWindow` 映射规则：

| 旧布局 | 新输出 |
| --- | --- |
| `WidgetsWindow/MapBpCanvas` | 跳过并记录 Info，MapV1 不再支持 |
| `WidgetsWindow/BpOverViewCanvas` | `BpOverviewWindow.json` |
| `WidgetsWindow/MapV2Canvas` | `MapV2Window.json` |

MapV1 跳过不能导致导入失败。资源复制、`bpui://` 路径改写、TextSettings 迁移、GlobalScoreRow 聚合、BO5 overtime 消费和缺失插件 placeholder 规则继续保留。

## Legacy conversion messages

Legacy conversion diagnostics use stable message codes and localized text.

Map BP V1 note: Legacy Map BP V1 was removed in Designer v3 and is intentionally skipped during conversion. This is a compatibility note, not a conversion failure.

Technical messages should preserve code and args for debugging, but the UI must show localized user-facing text.
