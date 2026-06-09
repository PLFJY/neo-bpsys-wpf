# Fronted Designer v3 设计文档

本文是前台窗口设计者模式 v3 重构的设计文档。v3 已改为 Window-centric：布局管理单位只有 Window，每个 v3 layout window 运行时固定由 `FrontedWindowBase` 创建 `ViewBox -> Canvas BaseCanvas`，控件由 `FrontedWindowConfig` 描述并由已注册的控件工厂创建。Canvas/BaseCanvas 只是实现细节，不出现在用户概念、FrontManagePage、包路径或 manifest 中。

当前内置 v3 layout window 包括 `ScoreSurWindow`、`ScoreHunWindow`、`ScoreGlobalWindow`、`CutSceneWindow`、`GameDataWindow`、`BpWindow`、`BpOverviewWindow` 和 `MapV2Window`。`WidgetsWindow` 和 MapV1 已删除；旧 `BpOverViewCanvas` 只在 legacy converter 中识别并迁移为 `BpOverviewWindow`。Designer v3 独立编辑器（`FrontedDesignerWindow`）是当前唯一的设计编辑器。

Behavior Graph 相关设计见 [fronted-behavior-system.md](fronted-behavior-system.md)。该文档记录 BehaviorGuid、behaviors 文件结构、运行时触发和编辑器接入点。

## 1. 背景与目标

当前设计者模式历史上是 XAML-first：前台窗口的具体控件直接写在各窗口 XAML 中，运行时再由 `FrontedWindowService` 扫描 Canvas 子元素并保存/恢复每个 Canvas 的 `ElementInfo`。这些旧版文件和 `Config.json` 前台自定义字段现在只作为 legacy 转换、迁移对照存在；当前运行时和编辑器路径是 Designer v3 + `FrontedLayouts`。SettingPage 旧前台自定义入口已移除，旧版真实窗口设计器也已移除。旧 `.bpui` 包与 `Config.json`、`CustomUi/`、`FrontElementsConfig/` 等历史结构的耦合只由 legacy 转换流程处理。

v3 目标是转向 JSON/config-driven UI：v3 layout window 不需要独立 XAML，`FrontedWindowBase` 提供配置驱动 host；传统固定 XAML window 保持原行为。`WindowSettings` 应用于窗口，`CanvasSettings` 应用于内部 `BaseCanvas`，`ControlLayout` 交给 renderer 渲染。

这项重构必须分阶段推进，不能在一个巨大提交中同时改设置版本、迁移、渲染器、窗口 XAML、编辑器和 `.bpui`。前台窗口会被 OBS 捕获，导播现场对稳定性要求高；每个阶段都应保持旧路径可回退，并优先迁移低风险窗口验证模型。

## 2. 版本体系

| 文件类型 | v3 版本字段 | 说明 |
| --- | --- | --- |
| 主设置 `config.json` | `Version = 3` | 新创建的配置应写入 `Version = 3`。缺失或 `null` 表示 legacy 配置。 |
| Window 布局配置 | `"Version": 3` | 每个 v3 layout window 一个 `FrontedWindowConfig`。 |
| v3 `.bpui` 包 | `"FormatVersion": 3` | 包格式版本。完整 manifest schema 见 [bpui-package-v3.md](bpui-package-v3.md)。 |

这些版本号刻意对齐为 3，方便维护者和用户理解当前代际，但它们仍属于不同文件类型：`config.json` 版本不等于 Canvas 布局 schema 版本，也不等于 `.bpui` 包版本。后续代码实现时不要把三者混成一个枚举或一个迁移入口。

`.bpui v3` 包必须只携带 Designer v3 前台布局、布局资源、manifest 和可选预览/说明，不得包含或覆盖全局 `Config.json`。manifest 使用根级 `MinVersion`，不包含 `App` 对象或 `App.MinVersion`，并标记 Window-centric layout model。v3 包可以从 `FrontManagePage` 导入安装，包内路径为 `FrontedLayouts/{WindowTypeName}.json` 和 `behaviors/{WindowTypeName}.behaviors.json`。legacy `.bpui` 会在导入前转换为干净 v3 包，运行时 renderer 仍只读取 v3 layout，不增加 legacy 兼容分支。

`config.json` 中缺失或 `null` 的 `Version` 表示 legacy 配置。当前加载配置前会检测 raw JSON root，备份 legacy `Config.json` 后写回 `Version = 3`；该迁移只更新主设置版本，不迁移前台窗口布局、不生成 v3 Canvas 配置，也不移除旧前台设置。

## 3. 新版 Window 配置文件结构

推荐路径：

| 来源 | 路径 |
| --- | --- |
| 用户布局 | `%APPDATA%\neo-bpsys-wpf\FrontedLayouts\{WindowTypeName}.json` |
| 内置默认布局 | `Resources\FrontedLayouts\{WindowTypeName}.json` |
| 插件默认布局 | `{PluginFolder}\FrontedLayouts\{WindowTypeName}.json` |
| behavior | `behaviors\{WindowTypeName}.behaviors.json` |

每个 v3 layout window 使用独立布局配置文件。内置窗口的 `{WindowTypeName}` 使用窗口类型名，例如 `BpWindow`；插件窗口使用 `plugin:{PackageId}/{WindowTypeName}`，保存到用户目录时会映射为安全子路径。内部如必须保留 CanvasName，唯一值为常量 `BaseCanvas`。

以下路径属于 legacy 格式，仅由 legacy `.bpui` 转换流程使用，不再被运行时读取：

| legacy 路径 | 说明 |
| --- | --- |
| `%APPDATA%\neo-bpsys-wpf\{WindowTypeName}Config-{CanvasName}.json` | 旧 `FrontedWindowService` 保存的 `ElementInfo` 用户布局。仅用于 legacy 转换。 |
| `Resources\FrontedDefaultPositions` | 旧内置默认位置文件目录。仅用于 legacy 转换。 |

v3 渲染路径优先读取新目录。legacy 文件只应进入迁移流程，不应让新运行时渲染代码长期保留旧格式分支。

## 4. v3 Window config JSON 示例

```json
{
  "Version": 3,
  "WindowSettings": {
    "WindowWidth": 1440,
    "WindowHeight": 810,
    "AllowsTransparency": true,
    "BackgroundColor": "#00000000",
    "Topmost": false,
    "ViewboxStretch": "Fill"
  },
  "CanvasSettings": {
    "CanvasWidth": 1440,
    "CanvasHeight": 810,
    "BackgroundImage": "Resources/bp.png",
    "EnableBoModeStates": false,
    "BoModeStates": {}
  },
  "ControlLayout": {
    "RequiredPlugins": [],
    "Controls": {
      "SurTeamName": {
        "ControlType": "Text",
        "Left": 580,
        "Top": 720,
        "Width": 120,
        "Height": null,
        "TextBinding": {
          "Sources": [
            { "Path": "CurrentGame.SurTeam.Name" }
          ]
        },
        "HorizontalAlignment": "Center",
        "VerticalAlignment": "Center",
        "TextAlignment": "Center",
        "TextWrapping": "WrapWithOverflow",
        "FontFamily": "pack://application:,,,/Assets/Fonts/#Noto Sans",
        "FontWeight": "Bold",
        "Color": "#FFFFFFFF",
        "FontSize": 28,
        "ZIndex": 2
      }
    }
  }
}
```

约定：

| 字段 | 要求 |
| --- | --- |
| `WindowSettings` | 窗口级设置。`BackgroundColor` 属于这里，`ViewboxStretch` 保存字符串 enum 名称。 |
| `CanvasSettings` | 内部 `BaseCanvas` 设置。没有 `BackgroundColor`；Canvas 纯色背景用 Rectangle / Shape 控件实现。 |
| `ControlLayout.Controls` JSON key | 就是控件名。该名称同时作为控件 dictionary key、生成控件 `FrameworkElement.Name`、namescope 注册名和编辑器设计项 `Name`。config object 内不应再加重复 `Name` 字段。 |
| JSON 属性名 | 使用 PascalCase，便于 C# 模型直接映射。 |
| `Left` / `Top` / `Width` / `Height` | 使用真实 JSON number 或 `null`，不是字符串。 |
| `ZIndex` | 使用数字字段名 `ZIndex`，不要使用 `Panel.ZIndex`。 |
| 数字兼容 | v3 不支持 legacy string-number 格式，也不需要为 v3 新文件保留字符串数字兼容。 |
| 前台 UI 图片相对路径 | 默认把 `Resources/xxx.png` 解析到 `Resources/bpui` 下，除非后续代码显式提供其他 resolver。 |
| 绝对路径 | 直接按文件系统路径读取。 |

`CanvasSettings.BackgroundImage` 与控件图片路径由 `IFrontedResourceResolver` 解析。默认语义是绝对路径直接读取，`Resources/xxx.png` 映射到运行目录 `Resources/bpui/xxx.png`，其他相对路径保守地按 `Resources/bpui` 下资源处理。

当前实现暂时让窗口宽高跟随 Canvas 设计尺寸。`WindowSettings.WindowWidth` / `WindowHeight` 会在读取、保存、包导入导出和 legacy 转换时同步为 `CanvasSettings.CanvasWidth` / `CanvasHeight`；Designer 只需要编辑 Canvas 设计尺寸，不单独暴露窗口宽高控件。`ViewBox` 负责把固定设计坐标缩放到窗口内容区域，控件坐标不会随窗口 resize 被重写。

Canvas 可启用通用 BO3/BO5 状态：`CanvasSettings` root 表示默认/BO5 state，`EnableBoModeStates = true` 且 `BoModeStates["Bo3"]` 存在时，运行时会在 `ISharedDataService.IsBo3Mode == true` 时渲染 BO3 state。BO3 state 拥有独立 `BackgroundImage`、`RequiredPlugins` 和 `Controls`，因此控件位置、大小、ZIndex、绑定、静态文本和 `Visibility` 都可以与 BO5 不同。`BackgroundImageVariants` 已移除，不保留迁移兼容分支。

layout validator 会校验 Window-centric 字段：`Version` 必须为 3，`WindowSettings.WindowWidth` / `WindowHeight`、`CanvasSettings.CanvasWidth` / `CanvasHeight` 必须大于 0，`CanvasSettings.BackgroundImage` 非空且 resolver 可用时应能解析到文件。控件 JSON key 的重复检测必须发生在 raw JSON / converter 阶段；如果先反序列化成 `Dictionary<string, FrontedControlConfigBase>`，重复 key 可能已经丢失。

## 5. 内置控件模型

v3 内置控件类型如下：

| `ControlType` | 用途 |
| --- | --- |
| `Text` | 文本、队名、比分、倒计时等。 |
| `LocalizedText` | 根据本地化资源 key 显示静态文本，主要用于表头、标签等不应写死在 JSON 中的用户可见文本。 |
| `Image` | 通用图片控件，根元素是承载图片和内部 overlay 的 `Grid`，用于队标、地图、角色图、Ban 位、pick 图等。 |
| `BorderedImage` | 外层 `Border` + 内层 `Image` 的图片控件，用于需要独立外框、容器裁剪或由外框承接 resize 的图片区域。 |
| `GlobalScoreRow` | `ScoreGlobalWindow` 的全局比分行，根据 `CurrentGame.MatchScore` 生成每半场比分格和阵营图标。 |
| `TalentTraitDisplay` | `CutSceneWindow` 默认布局控件，封装求生者/监管者固定天赋图标和监管者辅助特质图标。 |
| `GameProgressText` | `CutSceneWindow` 默认布局控件，集中生成 BO3/BO5 相关的对局进度文本。 |
| `MapNameText` | `CutSceneWindow` 默认布局控件，按地图 key 生成本地化地图名。 |
| `MapV2Display` | `MapV2Window` 地图 BP v2 控件，复用 `MapV2Presenter`；地图卡片正常/禁用外框颜色由布局配置控制。 |
| `BackgroundTintRectangle` / `BackgroundTintPolygon` | 自动使用当前有效 Canvas 背景图，生成保留纹理的静态染色副本，并按矩形或多边形区域对齐裁剪。 |

背景局部染色控件不保存独立图片路径，也不要求用户重复选择 Canvas 背景。它们使用运行时解析后的 root/BO3 有效背景，支持静态 `TintColor` 或通过 `TintBindingPath` 绑定 `HomeTeam.ColorHex` / `AwayTeam.ColorHex`。`BaseColorWithTexture` 模式按可见矩形或多边形遮罩区域的局部平均亮度归一化，把 `TintColor` 作为该区域的目标基色，以源背景相对局部平均亮度提供纹理细节，`TextureStrength` 控制细节对比度；`TintStrength` 仍控制原图与处理结果的混合比例。队伍颜色或遮罩尺寸变化时会重新生成染色图；该实现是偶发 CPU 图像处理，不是实时 GPU shader。

### Text

`Text` 控件使用外层 `Border` 和内层 `TextBlock`：

| 层级 | 接收属性 |
| --- | --- |
| 外层 `Border` | `Canvas.Left`、`Canvas.Top`、`Width`、`Height`、`Panel.ZIndex`。 |
| 内层 `TextBlock` | 文本绑定、字体、字号、字重、颜色、水平/垂直对齐、`TextAlignment`、`TextWrapping`。 |

行为动画中的 `TargetLayer=Control` 指向外层 `Border`；`TargetLayer=Content` 指向内层
`TextBlock`；`TargetLayer=OverlayAbove/OverlayBelow` 会在该文本控件上方或下方生成运行时
`Rectangle` 承接层，用于描边、填充等不属于文本本体的视觉效果。

`TextBinding.Sources` 按顺序以 `ISharedDataService` 为 binding `Source`，运行时始终创建 `MultiBinding`：

```csharp
{
  "Sources": [
    { "Path": "CurrentGame.HomeTeam.Name" },
    { "Path": "CurrentGame.AwayTeam.Name" }
  ],
  "StringFormat": "{0} vs {1}",
  "JoinSeparator": " - "
}
```

source 顺序对应 `{0}`、`{1}`、`{2}`。`StringFormat` 非空时使用当前 culture 的复合格式；为空时按 `JoinSeparator` 连接各值。没有有效 source 时，`Text` 使用原样静态 `Text`。两者同时存在时 `TextBinding` 优先。普通 `Text` 的静态文本和格式字符串都不会自动本地化。

### LocalizedText

`LocalizedText` 使用外层 `Border` 和内层 `TextBlock`，布局和字体字段与 `Text` 基本一致。没有有效 `TextBinding` source 时，文本来源仍是 `LocalizationKey`；资源 key 缺失时显示 `FallbackText` 或 key 本身。存在 source 时先按 Text MultiBinding 规则得到原始字符串，再尝试把该字符串作为本地化 key；找不到 key 时显示原始字符串。语言设置变化时两种模式都会刷新。

### Image / BorderedImage

内置图片控件拆为 `Image` 和 `BorderedImage`：

| 层级 | 接收属性 |
| --- | --- |
| `Image` 根 `Grid` | `Canvas.Left`、`Canvas.Top`、`Width`、`Height`、`Panel.ZIndex` 和控件 `Name`。内部第一层是主 `Image`，后续子元素是 lock / picking border overlay。 |
| `BorderedImage` 外层 `Border` | `Canvas.Left`、`Canvas.Top`、`Width`、`Height`、`Panel.ZIndex`，设计器默认 resize handles 作用于这一层。 |
| `BorderedImage` 内层 `Image` | `Source` 绑定、`ImageWidth`、`ImageHeight`、`Stretch`、`HorizontalAlignment`、`VerticalAlignment` 等图片展示属性。 |

行为动画中的 `TargetLayer=Control` 指向 `Image` 的根 `Grid` 或 `BorderedImage` 的外层
`Border`；`TargetLayer=Content` 指向主 `Image`，不会影响 lock / picking border overlay；
`TargetLayer=OverlayAbove/OverlayBelow` 会在图片控件上方或下方生成运行时 `Rectangle` 承接层。

`BindingPath` 同样应以 `ISharedDataService` 为 binding `Source`。图片控件还支持 `ImagePath` 作为静态图片资源路径。`BindingPath` 与 `ImagePath` 同时存在时，`BindingPath` 优先。`ImagePath` 应使用 `Resources/foo.png`、`bpui://...` 等 v3 资源路径，不应长期保存任意绝对本地路径。

`Image` 和 `BorderedImage` 共享图片 overlay 行为：

| 字段 | 用途 |
| --- | --- |
| `Lockable` / `LockImagePath` | 在图片上叠加锁定图。 |
| `LockVisibilityBindingPath` / `LockVisibleWhen` | 绑定 bool 可见性；Ban 位常用 `CanCurrent*BannedList[i]` 或 `CanGlobal*BannedList[i]`，并设置 `VisibleWhenFalse`。 |
| `PickingBorderAvailable` / `PickingBorderImagePath` | 在 pick 图上叠加呼吸边框图。 |
| `PickingBorderName` | 注册到 namescope 的稳定动画目标名，例如 `SurPickingBorder0`、`HunPickingBorder`。 |

Ban 位不再需要专用业务控件即可表达：当前局 Ban 绑定 `CurrentGame.CurrentSurBannedList[i].HeaderImageSingleColor` / `CurrentGame.CurrentHunBannedList[i].HeaderImageSingleColor`，全局 Ban 绑定 `CurrentGame.SurTeam.GlobalBannedSurList[i].HeaderImageSingleColor` / `CurrentGame.HunTeam.GlobalBannedHunList[i].HeaderImageSingleColor`。选手图片可绑定 `PictureShown`、`PictureShownWithFullCharacter` 或 `PictureShownHeader`。

`BorderedImage` 支持 `SizingMode`：

| `SizingMode` | 行为 | 使用场景 |
| --- | --- | --- |
| `Auto` | 不强制内层 `Image.Width/Height`，其余交给 WPF 默认测量与排列。 | 旧默认 `Image`，例如 GameData 求生者表头头像 |
| `FillContainer` | 内层 `Image` 获取外层容器分配的完整布局槽，缺省对齐为 `Stretch`。 | 明确需要外层框架的图片容器 |
| `OverflowCrop` | 外层 `Border` 通过 `ClipToBounds` 裁剪溢出内容，缺省对齐为 `Center` / `Center`。 | 旧 `Border + Image + ClipToBounds + UniformToFill` 的角色图裁剪 |

迁移布局时必须先看旧 XAML 的具体写法，不要把所有 `Image` 都改成 `BorderedImage`，也不要统一改 `Stretch`。

### GlobalScoreRow

`GlobalScoreRow` 是 Score System v2 使用的内置控件类型。它是"父行框 + 子比分格"的复合控件：父级仍是普通 v3 顶层控件，保存 `Left`、`Top`、`Width`、`Height`、`ZIndex`、`Visibility`、`TeamType` 和默认字体/颜色/阵营图标样式；`Cells` 保存多个 `GlobalScoreCellConfig`，每个子格用相对父行的 `X`、`Y`、`Width`、`Height` 定位，并可单独设置 `Visibility`、字体、颜色、字号和 `ShowCampIcon` 覆盖值。子格样式字段为空时继承父行。

运行时不再用 `MajorGameGap` / `HalfGameGap` 作为 v3 主布局机制。BO5 使用 root Canvas state，BO3 使用 `BoModeStates["Bo3"]`，两者可以有独立的 `GlobalScoreRow.Cells`。

### CutScene 业务控件

`TalentTraitDisplay`、`GameProgressText` 和 `MapNameText` 是 CutScene / GameData v3 默认布局使用的内置业务控件：

| 控件 | 封装规则 |
| --- | --- |
| `TalentTraitDisplay` | 求生者 4 个固定天赋、监管者 4 个固定天赋、监管者辅助特质、辅助特质显隐状态，以及黑白图标设置。 |
| `GameProgressText` | `CurrentGame.GameProgress` + `IsBo3Mode` 的显示文本，显式区分 BO3 第三局加赛与 BO5 第四局；`DisplayMode = TwoLine` 时把 Game / Overtime 和 half 分为两行。 |
| `MapNameText` | 地图 key 到本地化显示名的转换；未配置 `BindingPath` 时默认读取 `CurrentGame.PickedMap`。 |
| `MapV2Display` | 通过 `MapKey` 读取 `CurrentGame.MapV2Dictionary`，复用 `MapV2Presenter`。 |

## 6. Image overlay 与旧字段兼容策略

当前 `BpWindow` 的 pick 图和 Ban 位优先由通用 `Image` 配置表达。`AnimationService` 与 BP pick 页面逻辑仍依赖稳定元素名，因此 renderer 会把主图片控件名和 `PickingBorderName` 对应的内部 overlay 名都注册到窗口 namescope。

| 兼容点 | 策略 |
| --- | --- |
| 元素名 | 生成稳定覆盖控件名，例如 `SurPickingBorder0`、`SurPickingBorder1`、`SurPickingBorder2`、`SurPickingBorder3` 和 `HunPickingBorder`。 |
| 对齐 | overlay 是 `Image` / `BorderedImage` 内部视觉层，跟随主图片控件位置和尺寸。 |
| 层级 | overlay 使用 `LockZIndexOffset` / `PickingBorderZIndexOffset` 位于主图上方。 |
| Ban 锁 | 新字段为 `Lockable`、`LockImagePath`、`LockVisibilityBindingPath`、`LockVisibleWhen`。 |
| 动画 | 不重设计 `AnimationService`，除非收到明确需求。 |

旧 JSON 字段继续兼容读取：`BanLockAvailable` 映射到 `Lockable`，`BanLockImagePath` 映射到 `LockImagePath`，`PickingBorder` 映射到 `PickingBorderAvailable`，`PickingBorderImagePath` 保持原名。Property Grid 和新默认布局优先显示新字段名。

`BpWindow` 已迁移到 `Resources/FrontedLayouts/BpWindow.json`。v3 layout window 由 `FrontedWindowBase` host 创建 `ViewBox -> BaseCanvas`；默认布局中的 `SurPick0..3`、`HunPick`、`SurPickingBorder0..3` 和 `HunPickingBorder` 由 v3 renderer 生成或注册。renderer 会把控件名注册到窗口 namescope，因此 `AnimationService` 继续可以通过 `window.FindName(...)` 找到 pick 图和呼吸边框。

这些运行时关键名称集中在 `FrontedLayoutRuntimeContractCatalog` 中。校验器会检查 `BpWindow/BaseCanvas` 是否仍包含这些名称；缺失会报告错误。

## 7. Binding catalog

Binding Browser 的数据源由 `IFrontedBindingCatalogProvider` 构建，默认实现是 `FrontedBindingReflectionCatalogProvider`。它只扫描显式注册根，不全局扫描服务或应用对象：

1. `IFrontedBindingRootProvider` 注册根，例如 `CurrentGame : Game`、`HomeTeam : Team`、`AwayTeam : Team`、`RemainingSeconds : string` 和 Ban 可用状态列表。
2. `[FrontedBindingObject]` 标注的 DTO 自动包含 public readable instance properties。
3. `[FrontedBindingIgnore]` 与 `JsonIgnore` 类似，只影响 Binding Browser；公开但不适合前台布局绑定的属性应显式排除。
4. `[FrontedBindable]` 可为单个属性补充显示/描述或在非自动包含类型上强制包含。
5. `[FrontedBindingCollection(FixedCount = ...)]` 用于固定列表索引；dictionary 只在声明 `KnownKeys` 时展开。

catalog 构建是 lazy + cache，只做类型反射，不调用属性 getter，不读取 `ISharedDataService` 的当前值，不调用 `NewGame`，不枚举运行时集合。新增普通 DTO 字段时优先通过模型属性和 attribute 暴露；`IFrontedBindingCatalogContributor` 只用于虚拟节点、legacy alias、插件语义 key 等动态节点，不应重建旧的手写整树。

当前 Player 图像绑定包括 `PictureShown`、`PictureShownWithFullCharacter` 和 `PictureShownHeader`。三者在没有 `Character` 时回退 `Member.Image`；有 `Character` 时分别使用 `HalfImage`、`BigImage` 和 `HeaderImage`。

## 8. 插件扩展方向

控件工厂抽象：

```csharp
public interface IFrontedControl
{
    string ControlType { get; }
    Type ConfigType { get; }
    FrameworkElement Create(string name, FrontedControlConfigBase config, FrontedControlBuildContext context);
}
```

`FrontedControlBuildContext` 承载 `ISharedDataService`、资源 resolver、窗口/Canvas 元信息、服务提供器和可选日志。`FrontedControlRegistry` 从 DI 收集所有 `IFrontedControl`，因此后续插件可以通过 DI 注册自定义控件工厂。

第三方插件前台控件采用明确命名空间，内置控件仍使用简单 `ControlType`；插件控件必须使用：

```text
plugin:<PackageId>/<ControlTypeName>
```

`PackageId` 必须匹配插件 `manifest.yml` 的 `id`，`ControlTypeName` 在插件内唯一。完整字符串是稳定 layout schema，不本地化，不使用显示名，也不能 shadow 内置控件类型。Canvas 通过 `RequiredPlugins` 声明本 Canvas 依赖，`.bpui` manifest 通过 `PluginDependencies` 汇总包级依赖。

插件控件 registry 和 descriptor API：

```csharp
public interface IFrontedControlPluginContributor
{
    void RegisterFrontedControls(IFrontedControlPluginRegistry registry);
}

public interface IFrontedControlPluginRegistry
{
    void Register<TConfig>(FrontedPluginControlDescriptor<TConfig> descriptor)
        where TConfig : FrontedControlConfigBase;
}

public sealed class FrontedPluginControlDescriptor<TConfig>
    where TConfig : FrontedControlConfigBase
{
    public required string PackageId { get; init; }
    public required string ControlTypeName { get; init; }
    public string FullControlType => $"plugin:{PackageId}/{ControlTypeName}";
    public required Type ConfigType { get; init; }
    public required Func<string, TConfig, FrontedControlBuildContext, FrameworkElement> CreateControl { get; init; }
    public Func<TConfig>? CreateDefaultConfig { get; init; }
    public IReadOnlyList<FrontedPluginPropertyDescriptor>? Properties { get; init; }
    public Func<TConfig, IEnumerable<FrontedLayoutValidationMessage>>? Validate { get; init; }
    public string? DisplayNameKey { get; init; }
    public string? DescriptionKey { get; init; }
    public string? Icon { get; init; }
    public Version? MinHostVersion { get; init; }
    public int ConfigSchemaVersion { get; init; } = 1;
}
```

插件属性元数据使用声明式 descriptor：

```csharp
public sealed class FrontedPluginPropertyDescriptor
{
    public required string PropertyName { get; init; }
    public string? DisplayNameKey { get; init; }
    public string? DescriptionKey { get; init; }
    public string GroupName { get; init; } = "Plugin";
    public FrontedPropertyEditorKind? EditorKind { get; init; }
    public IReadOnlyList<FrontedPropertyEditorOption>? Options { get; init; }
    public FrontedBindingTargetKind BindingTargetKind { get; init; } = FrontedBindingTargetKind.Any;
    public bool IsVisible { get; init; } = true;
    public bool IsReadOnly { get; init; }
}
```

插件 config 类型应继承 `FrontedControlConfigBase`，构造函数设置完整插件 `ControlType`，插件专属属性必须能被 `System.Text.Json` 序列化。布局 JSON 不保存可执行状态，不保存本地绝对路径作为长期依赖。

layout JSON 中的 `plugin:*` 控件会先反序列化为通用 `PluginFrontedControlConfig` 并保留插件专属 JSON 属性；插件已安装且 descriptor 已注册时，adapter 会把通用 config 转换为插件声明的 typed config 并创建控件。运行时缺失插件控件时跳过并记录 warning，不让前台窗口崩溃。Designer preview 显示 MissingPlugin 占位符，允许选择、移动、缩放和删除底层插件控件配置。

## 8. 前台编辑窗口设计

新版编辑器是独立窗口，而不是直接编辑被 OBS 捕获的真实前台窗口。详细设计见 [fronted-designer-editor.md](fronted-designer-editor.md)。编辑器依赖 v3 的硬规则：JSON key 等于控件名。

`FrontedDesignerWindow` 是后台侧独立编辑器窗口，入口在 `FrontManagePage`。它通过固定的 `FrontedDesignerLayoutCatalog` 暴露已迁移窗口，按窗口/Canvas 选择读取 v3 layout JSON，使用 `FrontedLayoutDesignConverter` 和 `FrontedLayoutValidator` 显示设计文档与校验结果，并调用现有 `IFrontedRenderer` 把真实 v3 布局渲染到自己的 `PreviewCanvas`。

| 区域/能力 | 设计要求 |
| --- | --- |
| 独立性 | 编辑窗口独立于真实前台输出窗口。不要在 OBS 捕获的真实窗口上直接编辑。 |
| 模拟准确性 | 尽量模拟目标 Canvas 的尺寸、背景、控件、绑定和资源解析。 |
| 标题栏偏移 | 必须考虑窗口标题栏导致的偏移问题。坐标计算以模拟 Canvas 内容区为准，不以窗口外边界为准。 |
| Window-centric | Designer 只选择 Window；每个 v3 layout window 内部固定 `BaseCanvas`。 |
| 命中测试 | 透明、空文本、空图片和初始隐藏控件必须通过独立 interaction layer 的透明 hitbox 选中。 |
| Placeholder | 预览占位数据只属于编辑器，不写入 v3 layout JSON。 |
| 中央区域 | 显示可缩放 Canvas preview。 |
| 控件操作 | 支持鼠标拖拽、缩放、点击聚焦、键盘方向键微调。 |
| 微调步长 | 方向键调整步长为 `0.5`。 |
| 右侧属性栏 | 使用手写 WPF-UI Property Grid，不使用 WinForms `PropertyGrid`。 |

Property Grid 根据选中控件配置类型和属性数据类型选择编辑器：

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

## 9. 旧 .bpui 兼容策略

旧 `.bpui` 必须在导入前转换，不要在新的运行时渲染代码里长期保留 legacy 分支。`IFrontedLayoutPackageLegacyConverter` 负责处理旧包结构。新 `.bpui v3` 包格式、资源隔离和包管理规格见 [bpui-package-v3.md](bpui-package-v3.md)。

新导入流程：

```text
选择旧 .bpui
  -> 解压到 temp
  -> 检测 legacy 包结构
  -> 转换为 v3 package/layout
  -> 导入转换后的 v3 结果
```

转换服务负责理解旧 `Config.json`、`CustomUi/` 和 `FrontElementsConfig/` 的历史关系，并产出 v3 Canvas 布局与包元数据。

当前转换策略是保守迁移：每个目标布局先加载当前内置 v3 layout，再按"精确控件名、窗口限定别名、聚合控件、已知旧 overlay 消费、规范化匹配、技术候选诊断"顺序迁移旧 `ElementInfo` 的几何。`ScoreGlobalWindow/BaseCanvas` 额外兼容旧 `MainTeamName` / `MainScoreTotal` 到 v3 `HomeTeamName` / `HomeScoreTotal`，并把旧半场格子聚合到 `GlobalScoreRow.Cells`。旧 `CustomUi/` 图片复制到包内 `resources/images/` 并生成 `bpui://{PackageId}/...` URI。旧 `Config.json` 只读取明确的前台图片/颜色字段用于安全映射，不覆盖全局设置。

legacy 转换会把 `ScoreWindowSettings.GlobalScoreBgImageUri` 写入 `ScoreGlobalWindow/BaseCanvas/BackgroundImage`，把 `ScoreWindowSettings.GlobalScoreBgImageUriBo3` 写入 `BoModeStates["Bo3"].BackgroundImage` 并启用 BO mode states。

## 10. 分阶段实现历史

> 以下记录已经落地的能力范围，供追溯参考。

| 能力 | 当前状态 |
| --- | --- |
| `Settings.Version = 3`、legacy config 迁移骨架 | 已完成 |
| v3 layout models、资源 resolver、Text/Image factory、renderer skeleton | 已完成 |
| `ScoreSurWindow` / `ScoreHunWindow` / `ScoreGlobalWindow` 迁移到 v3 renderer 并绑定 `MatchScore` | 已完成 |
| `CutSceneWindow`、`GameDataWindow` 和 `BpWindow` 迁移到 v3 renderer | 已完成 |
| `WidgetsWindow` 删除，`BpOverviewWindow` / `MapV2Window` 改为配置驱动窗口 | 已完成 |
| 独立 Designer：Window 选择、只读预览、interaction layer、透明 hitbox、拖拽/缩放、键盘微调 | 已完成 |
| Property Grid、Add Control、字体选择、Undo/Redo、Binding Browser、Resource Browser | 已完成 |
| 用户 layout save/reset、脏状态提示、吸附网格 | 已完成 |
| `.bpui v3` 导出、导入、安装、激活、删除和 legacy `.bpui` 转换 | 已完成 |
| 插件前台控件、插件 Layout 窗口、依赖扫描、缺失插件占位符和市场引导 | 已完成 |
| 安全限制、版本兼容、i18n、左侧图层面板和智能对齐 | 已完成 |

## 11. 明确非目标

当前明确不做以下事情：

| 非目标 | 说明 |
| --- | --- |
| 修改无关运行时行为 | 不改 ViewModel、插件加载逻辑或未迁移窗口的运行逻辑。 |
| 继续批量迁移 XAML | 当前 v3 layout window 通过 `FrontedWindowBase` host 渲染；`WidgetsWindow` 已删除，后续不应顺手改无关窗口。 |
| 实现完整编辑器 UI | 当前编辑器已有交互层、Property Grid、Add Control、Binding/Resource Browser、保存/重置。仍不实现 Save As。 |
| 修改 `AnimationService` 查找逻辑 | 当前 `AnimationService` 仍通过 `FindName` 查找动画目标。 |
| 迁移 `.bpui` | 旧 `.bpui` 已有转换器。 |
| 改变现有 v3 layout JSON schema | v3 schema 已稳定。 |
| 把 `AllowTransparency` 当成控件属性 | 它是窗口级选项。 |

## 11.1 Shape 控件

Designer v3 内置 `Rectangle` 和 `Polygon`。两者共享 Shape 填充配置，支持静态或绑定的纯色填充，以及起始色、结束色可分别静态配置或绑定的双颜色线性渐变。颜色绑定值是 `#RRGGBB` 或 `#AARRGGBB` 字符串，可使用 `HomeTeam.ColorHex` / `AwayTeam.ColorHex`。渐变角度约定为 `0°` 从左到右、`90°` 从上到下，并规范化到 `0..360`。

`Polygon.Points` 保存为控件局部 `0..1` 归一化坐标，因此调整控件宽高时会保持形状比例。选中 Polygon 后，Interaction Layer 显示独立的橙色顶点手柄；拖动只修改顶点，不移动整个控件。属性区域提供添加和删除顶点按钮，且始终保留至少三个顶点。

## 12. 与 Score System v2 的关系

`ScoreSurWindow` 和 `ScoreHunWindow` 已作为 v3 renderer pilot 接入 JSON 布局，默认布局不再绑定旧的 `CurrentGame.*Team.Score.*` 字段。Score System v2 的权威比分状态在现有 `Core.Models.Game.MatchScoreState`，局内比分窗口绑定 `CurrentGame.MatchScore` 的派生字段。`Team.Score` 只作为剩余旧窗口的过渡兼容镜像。

`ScoreGlobalWindow` 已接入 v3 renderer，默认布局绑定 `MatchScoreState`，全局比分行由 `GlobalScoreRow` 控件生成。`FrontedWindowService` 不再动态创建并直接修改全局比分控件。详细设计见 [score-system-v2.md](score-system-v2.md)。
