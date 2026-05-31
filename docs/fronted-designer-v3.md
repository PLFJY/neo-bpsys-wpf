# Fronted Designer v3 设计文档

本文是前台窗口设计者模式 v3 重构的设计文档。Phase 1 已增加主设置 `Settings.Version = 3` 和 legacy `config.json` 迁移骨架；Phase 2 已增加 v3 layout model、资源 resolver、Text/Image 控件工厂、registry、layout service 和 renderer skeleton。Phase 3 已将 `ScoreSurWindow` 和 `ScoreHunWindow` 作为低风险 pilot 接入 v3 renderer；Score System v2 Phase 4 已额外将 `ScoreGlobalWindow` 接入 v3 renderer 并新增 `GlobalScoreRow` 控件；Designer v3 Phase 4 已迁移 `CutSceneWindow`；Designer v3 Phase 5 已迁移 `GameDataWindow` 并新增 `LocalizedText`；Designer v3 Phase 6 已迁移多 Canvas 的 `WidgetsWindow`，并新增 `CurrentBanDisplay` / `MapV2Display`；Designer v3 Phase 7 已迁移 `BpWindow`，并新增 `BanSlotDisplay` / `PickingBorderOverlay`。Phase 8A 已补充独立编辑器设计规格，Phase 8B 已新增设计期模型、layout validator、runtime contract catalog、引用扫描和设计项转换，Phase 8C 已新增独立编辑器 shell、窗口/Canvas 选择器、ViewBox 只读预览、缩放控制和校验面板，Phase 8D 已新增编辑器内存交互层、透明 hitbox、selection adorner、拖拽、缩放和键盘微调，Phase 8E 已新增基础 Property Grid，Phase 8F 已新增 Add Control 菜单、默认 config 工厂和 FontFamily 字体 ComboBox，Phase 8G 已新增 Binding Browser 与 Resource Browser，Phase 8H 已新增用户布局保存/重置/运行时加载优先级、脏状态提示和默认关闭的吸附网格，见 [fronted-designer-editor.md](fronted-designer-editor.md)。Phase 9A 已新增 `.bpui v3` 布局包标准，见 [bpui-package-v3.md](bpui-package-v3.md)；Phase 9C/9D 已实现 v3 package 导出、导入、安装、激活复制和删除，Phase 9F 已实现 legacy 转换。

## 1. 背景与目标

当前设计者模式历史上是 XAML-first：前台窗口的具体控件直接写在各窗口 XAML 中，运行时再由 `FrontedWindowService` 扫描 Canvas 子元素并保存/恢复每个 Canvas 的 `ElementInfo`。这些布局文件主要记录 `Left`、`Top`、`Width`、`Height`，而前台自定义图片、文本设置、颜色、字体和窗口设置仍存放在 `config.json`。旧 `.bpui` 包也和 `Config.json`、`CustomUi/`、`FrontElementsConfig/` 等历史结构耦合。当前 `ScoreSurWindow`、`ScoreHunWindow`、`ScoreGlobalWindow`、`CutSceneWindow`、`GameDataWindow`、`WidgetsWindow` 和 `BpWindow` 已接入 v3 renderer。

v3 目标是转向 JSON/config-driven UI：前台窗口 XAML 最终只保留外层 Canvas，控件由 JSON 配置描述，并由已注册的控件工厂创建。这样可以把布局、素材、控件类型和绑定关系放到可迁移、可导入导出的结构中，也为独立编辑窗口、插件扩展控件和新版 `.bpui` 包打基础。

这项重构必须分阶段推进，不能在一个巨大提交中同时改设置版本、迁移、渲染器、窗口 XAML、编辑器和 `.bpui`。前台窗口会被 OBS 捕获，导播现场对稳定性要求高；每个阶段都应保持旧路径可回退，并优先迁移低风险窗口验证模型。

## 2. 版本体系

| 文件类型 | v3 版本字段 | 说明 |
| --- | --- | --- |
| 主设置 `config.json` | `Version = 3` | 新创建的配置应写入 `Version = 3`。缺失或 `null` 表示 legacy 配置。 |
| Canvas 布局配置 | `"Version": 3` | 每个前台 Canvas 独立一个 v3 布局配置文件。 |
| v3 `.bpui` 包 | `"FormatVersion": 3` | 包格式版本。完整 manifest schema 见 [bpui-package-v3.md](bpui-package-v3.md)。 |

这些版本号刻意对齐为 3，方便维护者和用户理解当前代际，但它们仍属于不同文件类型：`config.json` 版本不等于 Canvas 布局 schema 版本，也不等于 `.bpui` 包版本。后续代码实现时不要把三者混成一个枚举或一个迁移入口。

`.bpui v3` 包必须只携带 Designer v3 前台布局、布局资源、manifest 和可选预览/说明，不得包含或覆盖全局 `Config.json`。manifest 使用根级 `MinVersion`，不包含 `App` 对象或 `App.MinVersion`。Phase 9D 起，v3 包可以从 `FrontManagePage` 导入安装，激活时会把包内 `layouts/{Window}/{Canvas}.json` 和可选 `window.json` 复制到用户布局目录；激活内置布局会清空用户布局并回退到内置资源。Phase 9F 起，legacy `.bpui` 会在导入前转换为干净 v3 包，运行时 renderer 仍只读取 v3 layout，不增加 legacy 兼容分支。

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
  "StaticTitle": {
    "ControlType": "Text",
    "Left": 20,
    "Top": 20,
    "Text": "示例静态文本",
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
| root-level 控件 JSON key | 就是控件名。该名称同时作为 `FrontedCanvasConfig.Controls` key、生成控件 `FrameworkElement.Name`、namescope 注册名和编辑器设计项 `Name`。config object 内不应再加重复 `Name` 字段。 |
| JSON 属性名 | 使用 PascalCase，便于 C# 模型直接映射。 |
| `Left` / `Top` / `Width` / `Height` | 使用真实 JSON number 或 `null`，不是字符串。 |
| `ZIndex` | 使用数字字段名 `ZIndex`，不要使用 `Panel.ZIndex`。 |
| 数字兼容 | v3 不支持 legacy string-number 格式，也不需要为 v3 新文件保留字符串数字兼容。 |
| 前台 UI 图片相对路径 | 默认把 `Resources/xxx.png` 解析到 `Resources/bpui` 下，除非后续代码显式提供其他 resolver。 |
| 绝对路径 | 直接按文件系统路径读取。 |

`BackgroundImage` 与控件图片路径由 `IFrontedResourceResolver` 解析。默认语义是绝对路径直接读取，`Resources/xxx.png` 映射到运行目录 `Resources/bpui/xxx.png`，其他相对路径保守地按 `Resources/bpui` 下资源处理。

Phase 8B 起，layout validator 会校验 Canvas 级字段：`Version` 必须为 3，`CanvasWidth` / `CanvasHeight` 必须大于 0，`BackgroundImage` 非空且 resolver 可用时应能解析到文件。root-level 控件 JSON key 的重复检测必须发生在 raw JSON / converter 阶段；如果先反序列化成 `Dictionary<string, FrontedControlConfigBase>`，重复 key 可能已经丢失。

## 5. 内置控件模型

v3 初始内置控件类型建议只包含：

| `ControlType` | 用途 |
| --- | --- |
| `Text` | 文本、队名、比分、倒计时等。 |
| `LocalizedText` | 根据本地化资源 key 显示静态文本，主要用于表头、标签等不应写死在 JSON 中的用户可见文本。 |
| `Image` | 角色图、队标、地图、背景元素等。 |
| `GlobalScoreRow` | `ScoreGlobalWindow` 的全局比分行，根据 `CurrentGame.MatchScore` 生成每半场比分格和阵营图标。 |
| `TalentTraitDisplay` | `CutSceneWindow` 默认布局控件，封装求生者/监管者固定天赋图标和监管者辅助特质图标。 |
| `GameProgressText` | `CutSceneWindow` 默认布局控件，集中生成 BO3/BO5 相关的对局进度文本。 |
| `MapNameText` | `CutSceneWindow` 默认布局控件，按地图 key 生成本地化地图名。 |
| `CurrentBanDisplay` | `WidgetsWindow` 当前局 Ban 位控件，封装当前 Ban 头像和锁定覆盖层。 |
| `BanSlotDisplay` | `BpWindow` 当前局/全局 Ban 位控件，封装 Ban 头像和当前/全局锁定覆盖层。 |
| `PickingBorderOverlay` | `BpWindow` pick 呼吸边框覆盖层，保留 `AnimationService` 查找的独立命名元素。 |
| `MapV2Display` | `WidgetsWindow` 地图 BP v2 控件，复用 `MapV2Presenter`。 |

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

如果 `BindingPath` 为空，`Text` 控件也可以使用 `"Text"` 字段显示原样静态文本。`BindingPath` 与 `Text` 同时存在时，`BindingPath` 优先，静态 `Text` 会被忽略。`Text` 支持可选 `StringFormat`，但只在 `BindingPath` 非空时应用；静态 `Text` 不会套用格式化。静态 `Text` 不会自动走 `WPFLocalizeExtension`、`I18nHelper` 或 resx，本阶段按 JSON 中写入的原文显示；需要业务规则或本地化文本时，应优先使用 `GameProgressText`、`MapNameText`、`LocalizedText` 等控件，而不是把业务/i18n 文案写进普通 `Text`。

### LocalizedText

`LocalizedText` 使用外层 `Border` 和内层 `TextBlock`，布局和字体字段与 `Text` 基本一致，但文本来源是 `LocalizationKey`。如果资源 key 缺失，则显示 `FallbackText`；`FallbackText` 为空时显示 key 本身。`LocalizedText` 会在语言设置变化时刷新文本。`Text.Text` 仍表示原样静态文本，不承担本地化语义。

### Image

`Image` 控件同样建议使用外层 `Border` 和内层 `Image`：

| 层级 | 接收属性 |
| --- | --- |
| 外层 `Border` | `Canvas.Left`、`Canvas.Top`、`Width`、`Height`、`Panel.ZIndex`。 |
| 内层 `Image` | `Source` 绑定、样式、`Stretch` 等图片展示属性。 |

`BindingPath` 同样应以 `ISharedDataService` 为 binding `Source`。

`Image` 支持 `SizingMode`，用于保留旧 XAML 中不同的图片布局语义：

| `SizingMode` | 旧 XAML 对应行为 | 使用场景 |
| --- | --- | --- |
| `Auto` | 不强制内层 `Image.Width/Height`，只在配置提供时应用 `Stretch` / `HorizontalAlignment` / `VerticalAlignment`，其余交给 WPF 默认测量与排列。 | `Border` 内默认 `Image`，例如 GameData 求生者表头头像；CutScene 地图这类旧 XAML 未给内层图片写固定尺寸的区域也应优先审计后使用。 |
| `FillContainer` | 内层 `Image` 跟随外层 `Border.ActualWidth/ActualHeight`，缺省对齐为 `Stretch`。 | 旧 direct fixed-size `ui:Image`，例如队标；MapBp v1 picked / banned 地图图。 |
| `OverflowCrop` | 不绑定内层 `Image.Width/Height`，外层 `Border` 通过 `ClipToBounds` 裁剪溢出内容，缺省对齐为 `Center` / `Center`。 | 旧 `Border + Image + ClipToBounds + UniformToFill` 的角色图裁剪，例如 CutScene pick 图和 BpOverview pick 图。 |

迁移布局时必须先看旧 XAML 的具体写法，不要把所有 `Image` 都设成 `FillContainer`，也不要统一改 `Stretch`。`CornerRadius > 0` 只表示外层 `Border` 需要圆角和裁剪，不等于图片必须填满容器。

如果 `PickingBorder` 为 `true`，应创建独立覆盖控件；该覆盖控件必须与原始 `Border` 对齐。不要把 picking border 放进图片 `Border` 内部。

`BanLockAvailable` 是布尔值，用于允许 Ban 位显示锁定覆盖层。它应驱动独立 overlay 或视觉状态，不应混入主 `Image` 控件。

### GlobalScoreRow

`GlobalScoreRow` 是 Score System v2 使用的内置控件类型，配置类型为 `GlobalScoreRowControlConfig`。它读取 `ISharedDataService.CurrentGame.MatchScore`，按 `ScoreGameKey` 的显式 BO3/BO5 顺序生成比分格；空半场显示 `-` 并隐藏阵营图标，已记录结果显示主队或客队的小比分和记录时阵营图标。该控件会响应 `CurrentGameChanged`、`IsBo3ModeChanged` 和 `MatchScoreState.PropertyChanged`。

### CutScene 业务控件

`TalentTraitDisplay`、`GameProgressText` 和 `MapNameText` 是 CutScene / GameData v3 默认布局使用的内置业务控件，`CutSceneWindow.xaml` 和 `GameDataWindow.xaml` 只保留外层 `BaseCanvas`，默认布局位于 `Resources/FrontedLayouts/{WindowTypeName}/BaseCanvas.json`。`WidgetsWindow` 是多 Canvas 前台窗口，XAML 保留 `MapBpCanvas`、`BpOverViewCanvas`、`MapV2Canvas` 三个外层 Canvas，默认布局分别位于 `Resources/FrontedLayouts/WidgetsWindow/MapBpCanvas.json`、`Resources/FrontedLayouts/WidgetsWindow/BpOverViewCanvas.json`、`Resources/FrontedLayouts/WidgetsWindow/MapV2Canvas.json`。这些控件用于把不适合散落在 JSON 中的业务规则收束起来：

| 控件 | 封装规则 |
| --- | --- |
| `TalentTraitDisplay` | 求生者 4 个固定天赋、监管者 4 个固定天赋、监管者辅助特质、辅助特质显隐状态，以及黑白图标设置。 |
| `GameProgressText` | `CurrentGame.GameProgress` + `IsBo3Mode` 的显示文本，显式区分 BO3 第三局加赛与 BO5 第四局；`UseLineBreak = true` 时把 Game / Overtime 和 half 分为两行，当前用于 `WidgetsWindow/BpOverViewCanvas.json`。 |
| `MapNameText` | 地图 key 到本地化显示名的转换；未配置 `BindingPath` 时默认读取 `CurrentGame.PickedMap`，`WidgetsWindow/MapBpCanvas.json` 用 `BindingPath` 分别显示 picked / banned map。 |
| `CurrentBanDisplay` | 读取 `CurrentGame.CurrentHunBannedList` / `CurrentGame.CurrentSurBannedList` 和 `CanCurrent*BannedList`，显示当前 Ban 头像及 Widgets 设置中的当前 Ban 锁图。 |
| `BanSlotDisplay` | 读取 `CurrentGame` 的当前局或全局 Ban 列表，并按 `CanCurrent*BannedList` / `CanGlobal*BannedList` 显示 `BpWindowSettings.CurrentBanLockImage` 或 `GlobalBanLockImage`。 |
| `PickingBorderOverlay` | 使用 `BpWindowSettings.PickingBorderBrush` 和 `PickingBorderImage` 生成独立覆盖层，默认隐藏，供 `AnimationService` 控制呼吸动画。 |
| `MapV2Display` | 通过 `MapKey` 读取 `CurrentGame.MapV2Dictionary`，复用 `MapV2Presenter` 并使用 WidgetsWindow 当前 Map BP v2 文本和 picking border 设置。 |

维护 CutScene 默认布局时，不应把四个天赋图标拆成四个普通 `Image` 控件，也不应在 JSON 中复制 BO3/BO5 文本判断；应继续使用这些内置业务控件。维护 GameData 默认布局时，地图名和对局进度也应继续使用 `MapNameText` / `GameProgressText`，表头应使用 `LocalizedText`。维护 Widgets 默认布局时，当前 Ban 槽位应使用 `CurrentBanDisplay`，Map BP v2 九宫位应使用 `MapV2Display`，`BpOverViewCanvas` 比分文本读取 `CurrentGame.MatchScore.CurrentSurTeamPreHalfMinorScoreText` / `CurrentGame.MatchScore.CurrentHunTeamPreHalfMinorScoreText`，不再读取 `Team.Score.GameScores`。维护 BpWindow 默认布局时，当前局和全局 Ban 槽位应使用 `BanSlotDisplay`，pick 呼吸边框应使用独立的 `PickingBorderOverlay`，比分文本读取 `CurrentGame.MatchScore` 派生字段，不再读取旧 `Team.Score`。CutScene、GameData 和 BpWindow 大比分文本读取 `CurrentGame.MatchScore.CurrentSurTeamMajorText` / `CurrentGame.MatchScore.CurrentHunTeamMajorText`，不再读取旧 `Team.Score.MajorPointsOnFront`。

`MapV2Display` 不应拆成普通 `Image` / `Text` 控件。它有自己的 `MapV2Presenter`，内部包含地图 `ImageBrush Stretch=UniformToFill`、队标、地图名、阵营文本/图标和 picking border 动画；v3 只负责让 `MapV2Presenter` 填满外层宿主尺寸。

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

Phase 7 后，`BpWindow` 已迁移到 `Resources/FrontedLayouts/BpWindow/BaseCanvas.json`。`BpWindow.xaml` 不再持有 BP 控件，只保留外层 `BaseCanvas`；默认布局中的 `SurPick0..3`、`HunPick`、`SurPickingBorder0..3` 和 `HunPickingBorder` 由 v3 renderer 生成。renderer 会在渲染生成控件时把控件名注册到窗口 namescope，并在清理生成控件前注销已注册名称，因此 `AnimationService` 继续可以通过 `window.FindName(...)` 找到 pick 图和呼吸边框。

Phase 8B 起，这批运行时关键名称集中在 `FrontedLayoutRuntimeContractCatalog` 中。校验器会检查 `BpWindow/BaseCanvas` 是否仍包含这些名称；缺失会报告错误，因为当前 `AnimationService` 仍依赖这些 namescope 名称。后续如果动画查找改为 metadata-based，再调整 catalog 和校验规则。

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

新版编辑器应是独立窗口，而不是直接编辑被 OBS 捕获的真实前台窗口。详细设计见 [fronted-designer-editor.md](fronted-designer-editor.md)。编辑器依赖 v3 的硬规则：JSON key 等于控件名；加载时把 `Dictionary<string, FrontedControlConfigBase>` 转成设计项集合，保存时再以设计项 `Name` 写回 dictionary key。

Phase 8C 的 `FrontedDesignerWindow` 是后台侧独立编辑器窗口，入口在 `FrontManagePage`，不在设置页。它通过固定的 `FrontedDesignerLayoutCatalog` 暴露已迁移的 `ScoreSurWindow`、`ScoreHunWindow`、`ScoreGlobalWindow`、`CutSceneWindow`、`GameDataWindow`、`WidgetsWindow` 三个 Canvas 和 `BpWindow`，按窗口/Canvas 选择读取 v3 layout JSON，使用 `FrontedLayoutDesignConverter` 和 `FrontedLayoutValidator` 显示设计文档与校验结果，并调用现有 `IFrontedRenderer` 把真实 v3 布局渲染到自己的 `PreviewCanvas`。编辑器窗口使用 `FluentWindow` + 项目 `CustomTitleBar`，标题栏隐藏主题切换按钮且不被内容覆盖；预览 Canvas 放在 `ViewBox` 中，默认 Fit，并提供 PowerPoint 风格的缩放预设、放大、缩小和适应窗口按钮。该阶段只读，不创建真实前台输出窗口，不保存用户布局，也不实现交互层。

| 区域/能力 | 设计要求 |
| --- | --- |
| 独立性 | 编辑窗口独立于真实前台输出窗口。不要在 OBS 捕获的真实窗口上直接编辑。 |
| 模拟准确性 | 尽量模拟目标 Canvas 的尺寸、背景、控件、绑定和资源解析。 |
| 标题栏偏移 | 必须考虑窗口标题栏导致的偏移问题。坐标计算以模拟 Canvas 内容区为准，不以窗口外边界为准。 |
| 多 Canvas | `WidgetsWindow` 等多 Canvas 窗口必须逐 Canvas 编辑和保存。 |
| 命中测试 | 透明、空文本、空图片和初始隐藏控件必须通过独立 interaction layer 的透明 hitbox 选中，不依赖生成控件自身可见像素。 |
| Placeholder | 预览占位数据只属于编辑器，不写入 v3 layout JSON。 |
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

旧 `.bpui` 必须在导入前转换，不要在新的运行时渲染代码里长期保留 legacy 分支。Phase 9F 的 `IFrontedLayoutPackageLegacyConverter` 负责处理旧包结构。新 `.bpui v3` 包格式、资源隔离和包管理规格见 [bpui-package-v3.md](bpui-package-v3.md)。

新导入流程建议为：

```text
选择旧 .bpui
  -> 解压到 temp
  -> 检测 legacy 包结构
  -> 转换为 v3 package/layout
  -> 导入转换后的 v3 结果
```

转换服务负责理解旧 `Config.json`、`CustomUi/` 和 `FrontElementsConfig/` 的历史关系，并产出 v3 Canvas 布局与包元数据。
当前转换策略是保守迁移：每个目标布局先加载当前内置 v3 layout，再只复制旧 `ElementInfo` 中同名控件的 `Left`、`Top`、`Width`、`Height`。旧 `CustomUi/` 图片复制到包内 `resources/images/` 并生成 `bpui://{PackageId}/...` URI；旧 `Config.json` 只读取明确的前台图片字段用于背景等安全映射，不覆盖全局设置。无法识别的旧文件或字段记录 warning，不进入运行时。

## 10. 分阶段计划

| 阶段 | 范围 |
| --- | --- |
| Phase 0 | 设计文档 only。新增本文档并更新文档索引/改动前必读。 |
| Phase 1 | 增加 `Settings.Version = 3`，建立 legacy config 迁移 skeleton。已实现：legacy `Config.json` 备份后写回 v3；未迁移前台窗口。 |
| Phase 2 | 增加 v3 layout models、资源 resolver、Text/Image factory、renderer skeleton。已实现：基础服务和 DI 注册；未接入真实前台窗口。 |
| Phase 3 | 先迁移低风险前台窗口，验证读取、渲染、保存和恢复路径。已实现：`ScoreSurWindow` 和 `ScoreHunWindow` 分别使用 `Resources/FrontedLayouts/{WindowTypeName}/BaseCanvas.json` 通过 v3 renderer 生成控件，且默认布局的比分文本已绑定 `CurrentGame.MatchScore`。当前只有局内求生者/监管者比分窗口已接入 v3 renderer。 |
| Phase 4 | 已迁移 `CutSceneWindow` 到 v3 renderer，并使用 `TalentTraitDisplay`、`GameProgressText`、`MapNameText` 封装天赋/辅助特质、BO3/BO5 进度文本和地图名本地化。Score System v2 已在独立 Phase 4 中先迁移 `ScoreGlobalWindow`。`BpWindow` 尚未迁移，后续迁移仍需保留 PickingBorder、BanLock、BP 动画兼容性。 |
| Phase 5 | 已迁移 `GameDataWindow` 到 v3 renderer。默认布局位于 `Resources/FrontedLayouts/GameDataWindow/BaseCanvas.json`，表头使用 `LocalizedText`，地图名/对局进度使用 `MapNameText` / `GameProgressText`，比分绑定 `CurrentGame.MatchScore`。 |
| Phase 6 | 已迁移 `WidgetsWindow` 到 v3 renderer。该窗口有 `MapBpCanvas`、`BpOverViewCanvas`、`MapV2Canvas` 三个独立 Canvas 和三份独立布局文件；新增 `CurrentBanDisplay`、`MapV2Display`，并让 `MapNameText.BindingPath` 支持 picked / banned map。 |
| Phase 7 | 已迁移 `BpWindow` 到 v3 renderer。新增 `BanSlotDisplay` 封装当前局/全局 Ban 头像和锁定覆盖层，新增 `PickingBorderOverlay` 保留 pick 呼吸边框的独立命名动画目标，v3 renderer 注册生成控件名以兼容 `AnimationService`。 |
| Phase 8A | 已完成独立编辑器设计规格文档，见 [fronted-designer-editor.md](fronted-designer-editor.md)。不实现 UI、不改 renderer、不迁移 `.bpui`。 |
| Phase 8B | 已实现设计期基础：`FrontedControlDesignItem`、`FrontedCanvasDesignDocument`、设计项与 dictionary 转换、layout validator、引用扫描、运行时关键名称 catalog，以及 converter 阶段的重复 JSON key 检测。不实现 UI、不改 renderer、不迁移 `.bpui`。 |
| Phase 8C | 已实现独立编辑器 shell、窗口/Canvas 选择器、layout source 显示、ViewBox 只读 preview surface、缩放控制和 validator 消息面板。预览 Canvas 使用 layout config 的 `CanvasWidth` / `CanvasHeight`，不使用真实窗口尺寸。 |
| Phase 8D | 已实现 interaction layer、透明 hitbox、selection adorner、drag、resize、键盘微调和内存 dirty/validation 刷新；不保存用户布局。 |
| Phase 8E | 已实现基础 Property Grid：编辑选中设计项与 config 简单属性、保守 Name 改名、运行时关键名称只读、被引用控件改名阻止；仍只改内存，不保存用户布局。 |
| Phase 8F | 已实现 Add Control 菜单、默认 config factory、唯一命名、基础 placeholder 策略和 `FontFamily` 字体 ComboBox；仍只改内存，不保存用户布局。 |
| Phase 8G | 已实现 Binding Browser 与 Resource Browser。浏览器选择只写入属性行 `EditText` 缓冲，仍需 Apply/Enter 提交；Resource Browser 支持 `Resources/bpui` 与绝对路径图片，但不复制外部资源。 |
| Phase 8H | 已实现用户 layout save/reset/load priority、validation-driven save、打开用户布局目录、切换/重载/关闭脏状态提示，以及默认无吸附、Shift 临时吸附、工具栏持久吸附开关。 |
| Phase 9A | 已完成文档规格：Designer v3 `.bpui` 布局包标准，见 [bpui-package-v3.md](bpui-package-v3.md)。不实现导入、导出、包管理 UI 或 legacy 转换。 |
| Phase 9B.0 | 已实现 Canvas Properties GUI、本地 `bpui://local` 资源规范化、`bpui://{PackageId}` resolver、顶部工具栏整理和窗口级 Window Options 基础。`AllowTransparency` 是窗口级选项，不写入 `FrontedCanvasConfig`。 |
| Phase 9B.1 | `FrontManagePage` Layout Package Manager UI skeleton。 |
| Phase 9C | 已实现 v3 `.bpui` 导出、manifest 对话框、包管理器 UI 打磨和资源重写。 |
| Phase 9D | 已实现 v3 `.bpui` 导入、安装、激活复制和删除完善。 |
| Phase 9E+ | 已实现 legacy `.bpui` 转换入口和转换器；后续仍可扩展更完整的字段映射 UI。 |

每个阶段都应有明确回退策略。涉及用户可见文本时，应同步考虑 `WPFLocalizeExtension` 和 `Locales/*.resx`。

## 11. 明确非目标

Phase 2 之后仍明确不做以下事情：

| 非目标 | 说明 |
| --- | --- |
| 修改无关运行时行为 | 不改 ViewModel、插件加载逻辑或未迁移窗口的运行逻辑。 |
| 继续批量迁移 XAML | 当前已迁移 `ScoreSurWindow`、`ScoreHunWindow`、`ScoreGlobalWindow`、`CutSceneWindow`、`GameDataWindow`、`WidgetsWindow` 和 `BpWindow`；后续不应顺手改无关窗口。 |
| 替换旧设计者/编辑器 | ~~当前 `DesignBehavior`、旧设计者入口和 `FrontedWindowService` 行为保持不变；独立编辑器尚未实现。~~ **Phase 10+ 已完成**：旧版真实窗口设计器模式已移除。`DesignBehavior`、`CanvasAdorner`、`DesignerModeChangedMessage` 和 `FrontManagePage` 的 `ChangeDesignerMode` 命令已删除。Designer v3 独立编辑器（`FrontedDesignerWindow`）是当前唯一支持的设计编辑器。 |
| 移除 `config.json` 中的前台设置 | ~~自定义图片、文本设置、窗口设置仍保留在当前结构中。~~ **Phase 10+ 已完成**：旧前台自定义 UI 已从 SettingPage 删除，`SettingPageViewModel.FrontedUiCustom.cs` 已删除。旧 Config 字段仍保留在模型中用于运行时控件，但不再作为用户编辑入口。 |
| 实现完整编辑器 UI | 当前已新增 Phase 8C shell、Phase 8D 交互层和 Phase 8E 基础 Property Grid；仍不实现 Add Control、Binding browser、Resource browser 或保存。 |
| 实现 legacy `.bpui` 转换 | ~~Phase 9D 已实现 v3 `.bpui` 导入/安装，但仍不转换旧 `.bpui`，也不修改现有 SettingPage legacy `.bpui` import/export 流程。~~ **Phase 10+ 已完成**：Phase 9D 已实现 v3 `.bpui` 导入/安装和 legacy `.bpui` 转换。`SettingPageViewModel.UiPackage.cs` 已删除。旧 `.bpui` 现在通过 `FrontManagePage` 的 Layout Packages 管理，不再通过 SettingPage 覆盖 Config.json。旧 Config 字段仍保留在模型中用于运行时控件，但不再作为用户编辑入口。 |

## 12. 与 Score System v2 的关系

`ScoreSurWindow` 和 `ScoreHunWindow` 已作为 v3 renderer pilot 接入 JSON 布局，且 Phase 3 后默认布局不再绑定旧的 `CurrentGame.*Team.Score.*` 字段。Score System v2 的权威比分状态在现有 `Core.Models.Game.MatchScoreState`，局内比分窗口绑定 `CurrentGame.MatchScore` 的派生字段：第二半显示同一个 `ScoreGame` 第一半的小比分（MinorScore）时必须按当前求生者/监管者队伍映射，而不是盲目使用第一半的求生者/监管者小比分。`Team.Score` 只作为剩余旧窗口的过渡兼容镜像。

`ScoreGlobalWindow` 已接入 v3 renderer，默认布局绑定现有 `Core.Models.Game.MatchScoreState`，全局比分行由 `GlobalScoreRow` 控件生成。`FrontedWindowService` 不再动态创建并直接修改全局比分控件；旧 `SetGlobalScore*` / `ResetGlobalScore` 方法仅作为 obsolete no-op 兼容入口保留。详细设计见 [score-system-v2.md](score-system-v2.md)。
