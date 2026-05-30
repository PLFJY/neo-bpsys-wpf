# Fronted Designer v3 设计文档

本文是前台窗口设计者模式 v3 重构的设计文档。Phase 1 已增加主设置 `Settings.Version = 3` 和 legacy `config.json` 迁移骨架；Phase 2 已增加 v3 layout model、资源 resolver、Text/Image 控件工厂、registry、layout service 和 renderer skeleton。Phase 3 已将 `ScoreSurWindow` 作为低风险 pilot 接入 v3 renderer；其他真实前台窗口、独立编辑器和 `.bpui` 转换仍按后续阶段推进。

## 1. 背景与目标

当前设计者模式是 XAML-first：前台窗口的具体控件已经直接写在各窗口 XAML 中，运行时再由 `FrontedWindowService` 扫描 Canvas 子元素并保存/恢复每个 Canvas 的 `ElementInfo`。这些布局文件主要记录 `Left`、`Top`、`Width`、`Height`，而前台自定义图片、文本设置、颜色、字体和窗口设置仍存放在 `config.json`。旧 `.bpui` 包也和 `Config.json`、`CustomUi/`、`FrontElementsConfig/` 等历史结构耦合。

v3 目标是转向 JSON/config-driven UI：前台窗口 XAML 最终只保留外层 Canvas，控件由 JSON 配置描述，并由已注册的控件工厂创建。这样可以把布局、素材、控件类型和绑定关系放到可迁移、可导入导出的结构中，也为独立编辑窗口、插件扩展控件和新版 `.bpui` 包打基础。

这项重构必须分阶段推进，不能在一个巨大提交中同时改设置版本、迁移、渲染器、窗口 XAML、编辑器和 `.bpui`。前台窗口会被 OBS 捕获，导播现场对稳定性要求高；每个阶段都应保持旧路径可回退，并优先迁移低风险窗口验证模型。

## 2. 版本体系

| 文件类型 | v3 版本字段 | 说明 |
| --- | --- | --- |
| 主设置 `config.json` | `Version = 3` | 新创建的配置应写入 `Version = 3`。缺失或 `null` 表示 legacy 配置。 |
| Canvas 布局配置 | `"Version": 3` | 每个前台 Canvas 独立一个 v3 布局配置文件。 |
| 未来 v3 `.bpui` 包 | `"Version": 3` | 新导入导出包格式应使用 v3 包结构。 |

这些版本号刻意对齐为 3，方便维护者和用户理解当前代际，但它们仍属于不同文件类型：`config.json` 版本不等于 Canvas 布局 schema 版本，也不等于 `.bpui` 包版本。后续代码实现时不要把三者混成一个枚举或一个迁移入口。

`config.json` 中缺失或 `null` 的 `Version` 表示 legacy 配置。当前 Phase 1 会在加载配置前检测 raw JSON root，备份 legacy `Config.json` 后写回 `Version = 3`；该迁移只更新主设置版本，不迁移前台窗口布局、不生成 v3 Canvas 配置，也不移除旧前台设置。

## 3. 新版 Canvas 配置文件结构

推荐路径：

| 来源 | 路径 |
| --- | --- |
| 用户布局 | `%APPDATA%\neo-bpsys-wpf\FrontedLayouts\{WindowTypeName}\{CanvasName}.json` |
| 内置默认布局 | `Resources\FrontedLayouts\{WindowTypeName}\{CanvasName}.json` |
| 插件默认布局 | `{PluginFolder}\FrontedLayouts\{WindowTypeName}\{CanvasName}.json` |

每个前台 Canvas 使用独立布局配置文件。`{WindowTypeName}` 使用窗口类型名，例如 `BpWindow`；`{CanvasName}` 使用 `FrontedWindowInfo` 中声明的 Canvas 名称，例如 `BaseCanvas`。

以下路径属于 legacy 格式：

| legacy 路径 | 说明 |
| --- | --- |
| `%APPDATA%\neo-bpsys-wpf\{WindowTypeName}Config-{CanvasName}.json` | 当前 `FrontedWindowService` 保存的 `ElementInfo` 用户布局。 |
| `Resources\FrontedDefaultPositions` | 当前内置默认位置文件目录。 |

v3 渲染路径应优先读取新目录。legacy 文件只应进入迁移流程，不应让新运行时渲染代码长期保留旧格式分支。

## 4. v3 Canvas config JSON 示例

```json
{
  "Version": 3,
  "CanvasWidth": 1440,
  "CanvasHeight": 810,
  "BackgroundImage": "Resources/bp.png",
  "SurTeamName": {
    "ControlType": "Text",
    "Left": 580,
    "Top": 720,
    "Width": 120,
    "Height": null,
    "BindingPath": "CurrentGame.SurTeam.Name",
    "HorizontalAlignment": "Center",
    "VerticalAlignment": "Center",
    "TextAlignment": "Center",
    "TextWrapping": "WrapWithOverflow",
    "FontFamily": "pack://application:,,,/Assets/Fonts/#Noto Sans",
    "FontWeight": "Bold",
    "Color": "#FFFFFFFF",
    "FontSize": 28,
    "ZIndex": 2
  },
  "SurPick1": {
    "ControlType": "Image",
    "Left": 143,
    "Top": 620,
    "Width": 141,
    "Height": 160,
    "BindingPath": "CurrentGame.SurPlayerList[1].PictureShown",
    "ZIndex": 1,
    "PickingBorder": true,
    "PickingBorderImagePath": "Resources/pickingBorder.png",
    "BanLockAvailable": false
  }
}
```

约定：

| 字段 | 要求 |
| --- | --- |
| JSON 属性名 | 使用 PascalCase，便于 C# 模型直接映射。 |
| `Left` / `Top` / `Width` / `Height` | 使用真实 JSON number 或 `null`，不是字符串。 |
| `ZIndex` | 使用数字字段名 `ZIndex`，不要使用 `Panel.ZIndex`。 |
| 数字兼容 | v3 不支持 legacy string-number 格式，也不需要为 v3 新文件保留字符串数字兼容。 |
| 前台 UI 图片相对路径 | 默认把 `Resources/xxx.png` 解析到 `Resources/bpui` 下，除非后续代码显式提供其他 resolver。 |
| 绝对路径 | 直接按文件系统路径读取。 |

`BackgroundImage` 与控件图片路径由 `IFrontedResourceResolver` 解析。默认语义是绝对路径直接读取，`Resources/xxx.png` 映射到运行目录 `Resources/bpui/xxx.png`，其他相对路径保守地按 `Resources/bpui` 下资源处理。

## 5. 内置控件模型

v3 初始内置控件类型建议只包含：

| `ControlType` | 用途 |
| --- | --- |
| `Text` | 文本、队名、比分、倒计时等。 |
| `Image` | 角色图、队标、地图、背景元素等。 |

### Text

`Text` 控件建议使用外层 `Border` 和内层 `TextBlock`：

| 层级 | 接收属性 |
| --- | --- |
| 外层 `Border` | `Canvas.Left`、`Canvas.Top`、`Width`、`Height`、`Panel.ZIndex`。 |
| 内层 `TextBlock` | 文本绑定、字体、字号、字重、颜色、水平/垂直对齐、`TextAlignment`、`TextWrapping`。 |

`BindingPath` 应以 `ISharedDataService` 为 binding `Source`，例如：

```csharp
new Binding(config.BindingPath)
{
    Source = sharedDataService
};
```

### Image

`Image` 控件同样建议使用外层 `Border` 和内层 `Image`：

| 层级 | 接收属性 |
| --- | --- |
| 外层 `Border` | `Canvas.Left`、`Canvas.Top`、`Width`、`Height`、`Panel.ZIndex`。 |
| 内层 `Image` | `Source` 绑定、样式、`Stretch` 等图片展示属性。 |

`BindingPath` 同样应以 `ISharedDataService` 为 binding `Source`。

如果 `PickingBorder` 为 `true`，应创建独立覆盖控件；该覆盖控件必须与原始 `Border` 对齐。不要把 picking border 放进图片 `Border` 内部。

`BanLockAvailable` 是布尔值，用于允许 Ban 位显示锁定覆盖层。它应驱动独立 overlay 或视觉状态，不应混入主 `Image` 控件。

## 6. PickingBorder / BanLockAvailable 兼容策略

当前 `BpWindow` 的 picking border 是独立 `Rectangle` 控件，`AnimationService` 与 BP pick 页面逻辑很可能依赖稳定元素名。早期迁移必须优先兼容现有动画和页面控制逻辑。

| 兼容点 | 策略 |
| --- | --- |
| 元素名 | 生成稳定覆盖控件名，例如 `SurPickingBorder0`、`SurPickingBorder1`、`SurPickingBorder2`、`SurPickingBorder3` 和 `HunPickingBorder`。 |
| 对齐 | overlay 按目标 pick `Border` 的 `Left`、`Top`、`Width`、`Height` 和必要偏移对齐。 |
| 层级 | overlay 使用独立 `ZIndex` 或默认位于目标上方。 |
| Ban 锁 | `BanLockAvailable` 只声明该槽位可显示 Ban lock overlay，不把锁图塞进主 Image。 |
| 动画 | 第一实现阶段不要重设计 `AnimationService`，除非收到明确需求。 |

这意味着迁移 `BpWindow` 时，配置驱动渲染器不仅要创建主 pick 图片，还要创建能被现有逻辑找到的 overlay 控件。

## 7. 插件扩展方向

Phase 2 已加入控件工厂抽象：

```csharp
public interface IFrontedControl
{
    string ControlType { get; }
    Type ConfigType { get; }
    FrameworkElement Create(string name, FrontedControlConfigBase config, FrontedControlBuildContext context);
}
```

`FrontedControlBuildContext` 承载 `ISharedDataService`、资源 resolver、窗口/Canvas 元信息、服务提供器和可选日志。`FrontedControlRegistry` 从 DI 收集所有 `IFrontedControl`，因此后续插件可以通过 DI 注册自定义控件工厂。

插件最终应能通过 DI 注册新的前台控件工厂，使宿主在读取 v3 Canvas 配置时按 `ControlType` 分派创建控件。这是未来实现目标，不是 Phase 0 代码；Phase 0 不新增接口、不改 PluginSdk，也不调整插件加载流程。

## 8. 前台编辑窗口设计

新版编辑器应是独立窗口，而不是直接编辑被 OBS 捕获的真实前台窗口。

| 区域/能力 | 设计要求 |
| --- | --- |
| 独立性 | 编辑窗口独立于真实前台输出窗口。不要在 OBS 捕获的真实窗口上直接编辑。 |
| 模拟准确性 | 尽量模拟目标 Canvas 的尺寸、背景、控件、绑定和资源解析。 |
| 标题栏偏移 | 必须考虑窗口标题栏导致的偏移问题。坐标计算以模拟 Canvas 内容区为准，不以窗口外边界为准。 |
| 中央区域 | 显示可缩放 Canvas preview。 |
| 控件操作 | 支持鼠标拖拽、缩放、点击聚焦、键盘方向键微调。 |
| 微调步长 | 方向键调整步长为 `0.5`。 |
| 右侧属性栏 | 使用手写 WPF-UI Property Grid，优先 ItemsControl-based，不使用 WinForms `PropertyGrid`。 |

Property Grid 应根据选中控件配置类型和属性数据类型选择编辑器：

| 数据类型 | 编辑器 |
| --- | --- |
| `string` | `ui:TextBox` |
| `int` / `double` | `ui:NumberBox` |
| `bool` | `ui:ToggleSwitch` |
| `enum` | `ui:ComboBox` |
| `BindingPath` | `TextBox` + 浏览 button |

Binding browser 应使用 `TreeView`，从 `ISharedDataService` 的 public properties 展开。浏览器生成的路径示例：

```text
CurrentGame.SurTeam.Name
CurrentGame.SurPlayerList[0].PictureShown
```

Phase 0 只记录设计，不实现编辑器窗口、Property Grid 或 Binding browser。

## 9. 旧 .bpui 兼容策略

旧 `.bpui` 必须在导入前转换，不要在新的运行时渲染代码里长期保留 legacy 分支。推荐单独设计 `ILegacyBpuiMigrationService` 处理旧包结构。

新导入流程建议为：

```text
选择旧 .bpui
  -> 解压到 temp
  -> 检测 legacy 包结构
  -> 转换为 v3 package/layout
  -> 导入转换后的 v3 结果
```

转换服务负责理解旧 `Config.json`、`CustomUi/` 和 `FrontElementsConfig/` 的历史关系，并产出 v3 Canvas 布局与包元数据。Phase 0 不修改 `.bpui` 导入导出代码，也不实现转换服务。

## 10. 分阶段计划

| 阶段 | 范围 |
| --- | --- |
| Phase 0 | 设计文档 only。新增本文档并更新文档索引/改动前必读。 |
| Phase 1 | 增加 `Settings.Version = 3`，建立 legacy config 迁移 skeleton。已实现：legacy `Config.json` 备份后写回 v3；未迁移前台窗口。 |
| Phase 2 | 增加 v3 layout models、资源 resolver、Text/Image factory、renderer skeleton。已实现：基础服务和 DI 注册；未接入真实前台窗口。 |
| Phase 3 | 先迁移一个小型低风险前台窗口，验证读取、渲染、保存和恢复路径。已实现：仅 `ScoreSurWindow` 使用 `Resources/FrontedLayouts/ScoreSurWindow/BaseCanvas.json` 通过 v3 renderer 生成控件。 |
| Phase 4 | 迁移 `BpWindow`，保留 PickingBorder、BanLock、BP 动画兼容性。 |
| Phase 5 | 实现独立前台编辑窗口。 |
| Phase 6 | 实现 v3 `.bpui` 导出/导入和 legacy `.bpui` 转换。 |

每个阶段都应有明确回退策略。涉及用户可见文本时，应同步考虑 `WPFLocalizeExtension` 和 `Locales/*.resx`。

## 11. 明确非目标

Phase 2 之后仍明确不做以下事情：

| 非目标 | 说明 |
| --- | --- |
| 修改无关运行时行为 | 不改 ViewModel、插件加载逻辑或未迁移窗口的运行逻辑。 |
| 继续批量迁移 XAML | Phase 3 只修改 `ScoreSurWindow`；`ScoreHunWindow`、`ScoreGlobalWindow`、`BpWindow`、`CutSceneWindow`、`GameDataWindow`、`WidgetsWindow` 仍是 XAML-first。 |
| 替换旧设计者/编辑器 | 当前 `DesignBehavior`、旧设计者入口和 `FrontedWindowService` 行为保持不变；独立编辑器尚未实现。 |
| 移除 `config.json` 中的前台设置 | 自定义图片、文本设置、窗口设置仍保留在当前结构中。 |
| 实现编辑器 UI | 不新增独立编辑窗口、Property Grid 或 Binding browser。 |
| 实现 `.bpui` 转换 | 不修改现有 `.bpui` import/export 流程。 |
