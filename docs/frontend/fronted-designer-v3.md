# Fronted Designer v3 设计文档

本文是前台窗口设计者模式 v3 重构的设计文档。v3 已改为 Window-centric：布局管理单位只有 Window，每个 v3 layout window 运行时固定由 `FrontedWindowBase` 创建 `ViewBox -> Canvas BaseCanvas`，控件由 `FrontedWindowConfig` 描述并由已注册的控件工厂创建。Canvas/BaseCanvas 只是实现细节，不出现在用户概念、FrontManagePage、包路径或 manifest 中。

当前内置 v3 layout window 包括 `ScoreSurWindow`、`ScoreHunWindow`、`ScoreGlobalWindow`、`CutSceneWindow`、`GameDataWindow`、`BpWindow`、`BpOverviewWindow` 和 `MapV2Window`。`WidgetsWindow` 和 MapV1 已删除；旧 `BpOverViewCanvas` 只在 legacy converter 中识别并迁移为 `BpOverviewWindow`。Designer v3 独立编辑器（`FrontedDesignerWindow`）是当前唯一的设计编辑器。

Behavior Graph 相关设计见 [fronted-behavior-system.md](fronted-behavior-system.md)。该文档记录 BehaviorGuid、behaviors 文件结构、运行时触发和编辑器接入点。

行为过滤使用显式事件 payload 和稳定控件身份，不再提供临时行为标签。引导高亮变化与清除是后台 UI 内部事件，前台动画使用 `Guidance.StepChanged`。

内置布局方案是 `PackageId = builtin` 的普通活动包，其布局根目录为应用运行时 `Resources/FrontedLayouts`。package manager 存在时活动包是唯一布局来源，不回退旧用户布局存储。激活任意包会完整重载已创建的 v3 窗口；`AllowsTransparency` 变化时只静默重启受影响窗口。

## 维护者速览

Designer v3 的正常运行时只读取当前活动包：

```text
FrontedLayoutPackages/{PackageId}/
  manifest.json
  FrontedLayouts/{WindowTypeName}.json
  FrontedBehaviors/{WindowTypeName}.behaviors.json
  Resources/...
```

`PackageId = builtin` 是特殊的包身份，但不是 fallback 链。活动包为 `builtin` 时，layout service 读取运行目录 `Resources/FrontedLayouts`；活动包为用户包时，读取 `FrontedLayoutPackages/{PackageId}/FrontedLayouts`。加载优先级按窗口来源区分：内置 v3 窗口为活动包 → 内置资源 → 空模板；插件 v3 窗口为活动包 → 空模板。`FrontedLayoutSource.PluginDefault` / `MissingOrError` 枚举值已删除，接口统一返回非空 `FrontedWindowConfig`；不再读取旧 loose user layout 或插件默认布局。

布局包管理器支持右键快速激活、复制、改名、导出、打开目录和删除。改名与描述编辑分别只更新 `manifest.json` 的 `Name`、`Description`；稳定的 `PackageId`、安装目录和 `bpui://{PackageId}/...` 资源 URI 不会变化，因此不会破坏已保存布局、行为或资源引用。导出操作以所选包为源，不会切换当前活动包。内置包与本地资源包不可改名或编辑描述。

启动时如果发现 legacy v2 `Config.json`（`Version` 缺失或为 `null`），只允许 `ILegacyV2StartupMigrationService` 处理兼容逻辑。它会先备份原始 `Config.json`，再从 AppData 根目录读取实际存在的 `*Config-*.json` loose 布局和 `CustomUi/`。启动迁移与 legacy `.bpui` 导入通过同一 legacy frontend input source abstraction 进入同一转换核心；区别仅是 `.bpui` 从 `FrontElementsConfig/` 读取布局，本地迁移从 AppData 根目录读取布局。转换结果通过 package importer 安装并激活为普通 v3 包，最后保存干净的 v3 Settings。正常 v3 runtime 不读取 legacy canvas 文件。

维护普通 v3 功能时优先查看：

| 主题 | 主要文件 |
| --- | --- |
| 活动包和包路径 | `FrontedLayoutPackageManager` |
| 布局读取/保存 | `FrontedLayoutService` |
| 行为文档读取/保存 | `FrontedBehaviorService` |
| 设计器编辑 | `FrontedDesignerWindowViewModel`、`FrontedDesignerWindow.xaml` |
| 行为面板和过滤器 | `BehaviorPanelViewModel`、`BehaviorPanelView.xaml` |
| 节点图和动画 runtime | `FrontedNodeGraphRuntime`、`FrontedAnimationRuntime`、property adapters |
| 启动 legacy v2 迁移 | `ILegacyV2StartupMigrationService`、`LegacyV2StartupMigrationService` |

不要在普通运行时重新添加这些读取路径：`AppConstants.FrontedLayoutsPath` loose 布局、legacy canvas layout、插件默认布局 fallback、直接读取 built-in JSON 作为用户包默认值。重置到内置布局也应通过 package manager / `builtin` 包身份获取快照，而不是绕过 package manager。

## 1. 背景与目标

当前设计者模式历史上是 XAML-first：前台窗口的具体控件直接写在各窗口 XAML 中，运行时再由 `FrontedWindowService` 扫描 Canvas 子元素并保存/恢复每个 Canvas 的 `ElementInfo`。这些旧版文件和 `Config.json` 前台自定义字段现在只作为 legacy 转换、迁移对照存在；当前运行时和编辑器路径是 Designer v3 + `FrontedLayouts`。SettingPage 旧前台自定义入口已移除，旧版真实窗口设计器也已移除。旧 `.bpui` 包与 `Config.json`、`CustomUi/`、`FrontElementsConfig/` 等历史结构的耦合只由 legacy 转换流程处理。

v3 目标是转向 JSON/config-driven UI：v3 layout window 不需要独立 XAML，`FrontedWindowBase` 提供配置驱动 host；传统固定 XAML window 保持原行为。`WindowSettings` 应用于窗口，`CanvasSettings` 应用于内部 `BaseCanvas`，`ControlLayout` 交给 renderer 渲染。

运行时 renderer 会将每一个生成控件的语义根放入 `FrontedEffectHost`。该宿主不写入布局或行为 JSON，不是生成控件，也不会取得名称、BehaviorGuid、绑定或动画目标身份。语义根继续拥有尺寸、对齐、可见性、变换、裁剪、Effect 和 DataContext；只有 `Canvas.Left`、`Canvas.Top`、`Canvas.Right`、`Canvas.Bottom` 与 `Panel.ZIndex` 会移动到直接父面板的运行时布局载体。行为 overlay 存在时，该载体为外层 overlay Grid，`FrontedEffectHost` 仍直接包含语义根。

当前维护应保持小步、可验证的改动。前台窗口会被 OBS 捕获，导播现场对稳定性要求高；修改设置加载、包激活、渲染器、行为 runtime 或 `.bpui` 导入导出时，都应同步更新测试和文档，并避免把 legacy 兼容重新放回普通运行时。

## 2. 版本体系

| 文件类型 | v3 版本字段 | 说明 |
| --- | --- | --- |
| 主设置 `config.json` | `Version = 3` | 新创建的配置应写入 `Version = 3`。缺失或 `null` 表示 legacy 配置。 |
| Window 布局配置 | `"Version": 3` | 每个 v3 layout window 一个 `FrontedWindowConfig`。 |
| v3 `.bpui` 包 | `"FormatVersion": 3` | 包格式版本。完整 manifest schema 见 [bpui-package-v3.md](bpui-package-v3.md)。 |

这些版本号刻意对齐为 3，方便维护者和用户理解当前代际，但它们仍属于不同文件类型：`config.json` 版本不等于 Canvas 布局 schema 版本，也不等于 `.bpui` 包版本。后续代码实现时不要把三者混成一个枚举或一个迁移入口。

`.bpui v3` 包必须只携带 Designer v3 前台布局、布局资源、manifest 和可选预览/说明，不得包含或覆盖全局 `Config.json`。manifest 使用根级 `MinVersion`，不包含 `App` 对象或 `App.MinVersion`，并标记 Window-centric layout model。v3 包可以从 `FrontManagePage` 导入安装，包内路径为 `FrontedLayouts/{WindowTypeName}.json` 和 `FrontedBehaviors/{WindowTypeName}.behaviors.json`。legacy `.bpui` 会在导入前转换为干净 v3 包，运行时 renderer 仍只读取 v3 layout，不增加 legacy 兼容分支。

`Config.json` 中缺失或 `null` 的 `Version` 表示 legacy 配置。当前启动迁移会把旧前台设置转换成普通 Designer v3 layout package 并激活，不再把转换结果写到 loose `FrontedLayouts`。迁移成功后，active `Config.json` 只保留 Settings v3 字段，不再写出旧前台窗口设置。

## 3. 新版 Window 配置文件结构

推荐路径：

| 来源 | 路径 |
| --- | --- |
| 活动用户包布局 | `%APPDATA%\neo-bpsys-wpf\FrontedLayoutPackages\{PackageId}\FrontedLayouts\{WindowTypeName}.json` |
| 活动用户包行为 | `%APPDATA%\neo-bpsys-wpf\FrontedLayoutPackages\{PackageId}\FrontedBehaviors\{WindowTypeName}.behaviors.json` |
| 内置包布局 | `Resources\FrontedLayouts\{WindowTypeName}.json` |
| 内置包行为 | `Resources\FrontedBehaviors\{WindowTypeName}.behaviors.json` |

每个 v3 layout window 使用独立布局配置文件。内置窗口的 `{WindowTypeName}` 使用窗口类型名，例如 `BpWindow`；插件窗口使用 `plugin:{PackageId}/{WindowTypeName}`，保存到用户目录时会映射为安全子路径。内部如必须保留 CanvasName，唯一值为常量 `BaseCanvas`。

以下路径属于 legacy 格式，仅由 legacy `.bpui` 转换流程使用，不再被运行时读取：

| legacy 路径 | 说明 |
| --- | --- |
| `%APPDATA%\neo-bpsys-wpf\{WindowTypeName}Config-{CanvasName}.json` | 旧 `FrontedWindowService` 保存的 `ElementInfo` 用户布局。启动迁移从 AppData 根目录读取这些 loose 文件。 |
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

窗口宽高由 `WindowSettings.WindowWidth` / `WindowHeight` 独立保存，Canvas 设计尺寸由 `CanvasSettings.CanvasWidth` / `CanvasHeight` 保存。Designer 的 Window Settings 区域直接写入 `FrontedLayouts/{WindowTypeName}.json -> WindowSettings`；普通读取、保存、包导入和包导出不得用 Canvas 尺寸覆盖窗口尺寸。`ViewBox` 负责把固定设计坐标缩放到窗口内容区域，控件坐标不会随窗口 resize 被重写。

Canvas 可启用通用 BO3/BO5 状态：`CanvasSettings` root 表示默认/BO5 state，`EnableBoModeStates = true` 且 `BoModeStates["Bo3"]` 存在时，运行时会在 `ISharedDataService.IsBo3Mode == true` 时渲染 BO3 state。BO3 state 拥有独立 `BackgroundImage`、`RequiredPlugins` 和 `Controls`，因此控件位置、大小、ZIndex、绑定、静态文本和 `Visibility` 都可以与 BO5 不同。`BackgroundImageVariants` 已移除，不保留迁移兼容分支。

layout validator 会校验 Window-centric 字段：`Version` 必须为 3，`WindowSettings.WindowWidth` / `WindowHeight`、`CanvasSettings.CanvasWidth` / `CanvasHeight` 必须大于 0，`CanvasSettings.BackgroundImage` 非空且 resolver 可用时应能解析到文件。控件 JSON key 的重复检测必须发生在 raw JSON / converter 环节；如果先反序列化成 `Dictionary<string, FrontedControlConfigBase>`，重复 key 可能已经丢失。

## 5. 内置控件模型

v3 内置控件类型如下：

| `ControlType` | 用途 |
| --- | --- |
| `Text` | 文本、队名、比分、倒计时等。 |
| `LocalizedText` | 根据本地化资源 key 显示静态文本，主要用于表头、标签等不应写死在 JSON 中的用户可见文本。 |
| `Image` | 通用图片控件，根元素是承载图片和内部 overlay 的 `Grid`，用于队标、地图、角色图、Ban 位、pick 图等。 |
| `BorderedImage` | 外层 `Border` + 内层 `Image` 的图片控件，用于需要独立外框、容器裁剪或由外框承接 resize 的图片区域。 |
| `GlobalScoreRow` | `ScoreGlobalWindow` 的全局比分行，根据 `CurrentGame.MatchScore` 生成每半场比分格和阵营图标。 |
| `TalentTraitDisplay` | `CutSceneWindow` 默认布局控件，封装求生者/监管者固定天赋图标和监管者辅助特质图标；可选颜色覆盖。 |
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
| `Image` 根 `Grid` | `Canvas.Left`、`Canvas.Top`、`Width`、`Height`、`Panel.ZIndex` 和控件 `Name`。内部第一层是主内容容器，容器内只有主 `Image`，后续子元素是 lock / picking border overlay。 |
| `BorderedImage` 外层 `Border` | `Canvas.Left`、`Canvas.Top`、`Width`、`Height`、`Panel.ZIndex`，设计器默认 resize handles 作用于这一层。 |
| `BorderedImage` 内层主内容容器 | 作为主图视口承接 `TargetLayer=Content` 的通用视觉动画；容器内的 `Image` 接收 `Source` 绑定、`ImageWidth`、`ImageHeight`、`Stretch`、`HorizontalAlignment`、`VerticalAlignment` 等图片展示属性。 |

行为动画中的 `TargetLayer=Control` 指向 `Image` 的根 `Grid` 或 `BorderedImage` 的外层
`Border`；`TargetLayer=Content` 指向只包含主图的主内容容器，`Opacity`、`ClipInset*`、位移、
缩放、旋转等通用视觉属性只作用于主图视口，不会影响 lock / picking border overlay；
`TargetLayer=OverlayAbove/OverlayBelow` 会在图片控件上方或下方生成运行时 `Rectangle` 承接层。

`BindingPath` 同样应以 `ISharedDataService` 为 binding `Source`。图片控件还支持 `ImagePath` 作为静态图片资源路径。`BindingPath` 与 `ImagePath` 同时存在时，`BindingPath` 优先。`ImagePath` 应使用 `Resources/foo.png`、`bpui://...` 等 v3 资源路径，不应长期保存任意绝对本地路径。

`Image` 和 `BorderedImage` 共享图片 overlay 行为：

| 字段 | 用途 |
| --- | --- |
| `Lockable` / `LockImagePath` | 在图片上叠加锁定图。 |
| `UseIndependentLockStretch` / `LockStretch` | 控制锁定图是否使用独立的 `Stretch`；关闭独立设置时跟随主图当前的 `Stretch`。 |
| `LockVisibilityBindingPath` / `LockVisibleWhen` | 绑定 bool 可见性；Ban 位常用 `CanCurrent*BannedList[i]` 或 `CanGlobal*BannedList[i]`，并设置 `VisibleWhenFalse`。 |
| `PickingBorderAvailable` / `PickingBorderImagePath` | 在 pick 图上叠加呼吸边框图。 |
| `UseIndependentPickingBorderStretch` / `PickingBorderStretch` | 控制呼吸边框是否使用独立的 `Stretch`；关闭独立设置时跟随主图当前的 `Stretch`。 |

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
| `TalentTraitDisplay` | 求生者 4 个固定天赋、监管者 4 个固定天赋、监管者辅助特质、辅助特质显隐状态，以及 `Color` 单色覆盖；`Color` 默认为白色且为必填项。 |
| `GameProgressText` | `CurrentGame.GameProgress` + `IsBo3Mode` 的显示文本，显式区分 BO3 第三局加赛与 BO5 第四局；正式预设包括单行、双行、横排局数、横排半场、竖排、竖排双行、竖排局数、竖排半场。`DisplayLanguage = FollowApp` 时按应用语言生成文本。 |
| `MapNameText` | 地图 key 到本地化显示名的转换；未配置 `BindingPath` 时默认读取 `CurrentGame.PickedMap`。 |
| `MapV2Display` | 通过 `MapKey` 读取 `CurrentGame.MapV2Dictionary`，复用 `MapV2Presenter`。 |

## 6. Image overlay 与旧字段兼容策略

当前 `BpWindow` 的 pick 图和 Ban 位优先由通用 `Image` 配置表达。内置行为文档通过稳定 `BehaviorGuid` 与 PickingBorder part 引用定位动画目标。

| 兼容点 | 策略 |
| --- | --- |
| 元素名 | 由图片控件名自动生成稳定覆盖控件名，例如 `SurPick0PickingBorder`、`HunPickPickingBorder`。 |
| 对齐 | overlay 是 `Image` / `BorderedImage` 内部视觉层，跟随主图片控件位置和尺寸。 |
| 拉伸 | 锁定图和选择边框分别由 `UseIndependentLockStretch` / `UseIndependentPickingBorderStretch` 决定使用自身的 Stretch 枚举或跟随主图。未声明新字段时两个开关默认关闭，独立 Stretch 值默认 `UniformToFill`。 |
| 层级 | overlay 使用 `LockZIndexOffset` / `PickingBorderZIndexOffset` 位于主图上方。 |
| Ban 锁 | 新字段为 `Lockable`、`LockImagePath`、`UseIndependentLockStretch`、`LockStretch`、`LockVisibilityBindingPath`、`LockVisibleWhen`。 |
| 动画 | 默认动画由内置行为文档提供，不在运行时硬编码。 |

旧 JSON 字段继续兼容读取：`BanLockAvailable` 映射到 `Lockable`，`BanLockImagePath` 映射到 `LockImagePath`，`PickingBorder` 映射到 `PickingBorderAvailable`，`PickingBorderImagePath` 保持原名。Property Grid 和新默认布局优先显示新字段名。

`BpWindow` 已迁移到 `Resources/FrontedLayouts/BpWindow.json`，默认动画位于 `Resources/FrontedBehaviors/BpWindow.behaviors.json`。v3 layout window 由 `FrontedWindowBase` host 创建 `ViewBox -> BaseCanvas`；Pick 与 PickingBorder 动画由行为图运行时执行。

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

### Naming rule: do not use generic IsActive

`IsActive` 只保留给内部框架/运行时激活语义，尤其是 CommunityToolkit.Mvvm `ObservableRecipient.IsActive`。不要把 `IsActive` 用作 layout、package、settings、业务数据、绑定目录或 payload 字段。

应使用明确语义名称，例如 `IsActivePackage`、`IsCurrentPackage`、`IsVisible`、`IsBadgeVisible`、`IsEnabled`、`IsSelected`、`IsExpanded`、`IsVisibleInFrontManage`。

旧 `.bpui` 包可能在 `TextSettings` 中包含 `IsActive`，这是旧设置类继承 `ObservableRecipient` 造成的序列化泄漏。该字段不是文本样式启用标记，LegacyConverter 必须忽略它。`Visibility` 绑定必须使用 `IsVisible` 或具体的可见性语义属性，不得绑定泛名 `IsActive`。

### 通用动画部件

每个控件的行为集合都可以配置 `AnimationParts`。Designer 在选中控件的行为区域提供动画部件列表和
新增、删除、重命名、类型、层级、尺寸、颜色、图片、初始可见性与透明度编辑。动画部件始终是
父控件内部的生成视觉部件，不会成为主 Canvas 中的独立设计项，也不会出现在左侧控件列表或布局 JSON 中。

动画部件可通过 `part:{BehaviorGuid}:{AnimationPartName}` 选为动画目标。宽高支持像素文本和百分比；
动画部件的 `VisualOffsetX/Y` 百分比相对父控件宽高计算，适合构建扫描线、闪光、边缘高亮等通用效果。
动画部件可配置通用视觉效果 `Effect`，支持 `None`、`Glow` 和 `DropShadow`。`Glow`/`DropShadow` 使用 WPF `DropShadowEffect`，可配置颜色、不透明度、模糊半径、投影距离和方向；默认 `None` 不创建 effect，避免不必要的渲染成本。

## 8. 插件扩展方向

v3 之后 PluginSdk 不再作为 NuGet 包发布。插件作者应 clone 本仓库，并在插件项目中通过 `ProjectReference` 引用 `neo-bpsys-wpf.PluginSdk\neo-bpsys-wpf.PluginSdk.csproj`，再手动 `Import` 同目录下的 `neo-bpsys-wpf.PluginSdk.targets` 获取 `CreateZip` 打包 target。插件 API 兼容性仍看 `manifest.yml` 的 `apiVersion`；SDK 源码所在提交只表示编译期 API 和打包目标来源，不等同于插件 API 版本。

### 统一 V3 Control API

旧的前台控件架构（`IFrontedControl`、`IFrontedControlPluginContributor`、`FrontedPluginControlDescriptor`、`AddFrontedPluginControlContributor<T>()` 等）已整体移除，由统一 V3 Control API 替代。内置控件与插件控件使用同一套注册与属性模型，`FrontedV3ControlRegistry` 按 `CanonicalControlType` 索引所有注册。

核心类型：

| 类型 | 职责 |
| --- | --- |
| `FrontedV3ControlBase` | 所有 v3 控件的抽象基类（继承 `UserControl`），定义在 Core 程序集，命名空间 `neo_bpsys_wpf.Core.Abstractions.Services`。控件不管理根布局。 |
| `[FrontedV3Control("ControlId", IsBuiltIn = bool)]` | 标注控件类型，携带局部标识与内置标记。 |
| `FrontedV3ControlRegistration` | 注册记录，包含 `CanonicalControlType`、`ControlType`、`ConfigType`、`Properties`、`CreateDefaultConfig`、`StyleTransfer` 能力声明。 |
| `FrontedV3ControlRegistry` / `IFrontedV3ControlRegistry` | 统一注册表，从 DI 收集所有 `FrontedV3ControlRegistration` 并按 `CanonicalControlType` 索引。 |
| `FrontedV3Property<T>` | 强类型属性声明，作为控件类上的 `public static readonly` 字段。 |
| `FrontedV3Storage` | 存储访问器工厂，提供 `ExtensionData`、`ClrProperty`、`CollectionItemProperty` 三种实现。 |
| `FrontedV3OptionsView` | Options 动态代理视图，按属性 Schema 将逻辑路径投影到 Config 存储位置。 |
| `FrontedV3ControlHost` | 根布局宿主，唯一负责根级属性（Canvas.Left/Top、Width/Height、ZIndex、Visibility、GaussianBlur、BehaviorGuid）。 |
| `FrontedV3Part` / `FrontedV3PartDefinition` | 固定 Part 系统，管理控件内部固定区域。 |
| `FrontedV3Parts` / `FrontedV3PartCollectionDefinition` | PartCollection 系统，管理模板或动态集合。 |
| `FrontedV3StyleTransferService` | 统一样式继承与 peer 传播服务。 |

注册入口：

```csharp
services.AddFrontedV3Control<TeamCardControl>();
```

`AddFrontedV3Control<TControl>()` 只接受控件类型一个参数。`PackageId` 由宿主在插件初始化作用域内自动注入。控件类型必须标注 `[FrontedV3Control]` 并继承 `FrontedV3ControlBase`。属性通过控件类上的 `public static readonly FrontedV3Property<T>` 字段声明，框架在注册时反射发现并校验：`OptionsPath` 必须唯一、不得使用保留路径、`Storage` 不得指向根级保留字段。Designer 会为每个已注册控件额外提供通用根布局字段 `Left`、`Top`、`Width`、`Height` 和 `ZIndex`，插件无需也不得重复声明这些字段。

Canonical Control Type 命名规则：

```text
内置控件:   直接使用 ControlId（例如 Text、BorderedImage）
插件控件:   plugin:<PackageId>/<ControlId>（例如 plugin:plfjy.ExamplePlugin/TeamCard）
```

`PackageId` 必须匹配插件 `manifest.yml` 的 `id`，`ControlId` 在插件内唯一。完整字符串是稳定 layout schema，不本地化，不使用显示名，也不能 shadow 内置控件类型。Canvas 通过 `RequiredPlugins` 声明本 Canvas 依赖，`.bpui` manifest 通过 `PluginDependencies` 汇总包级依赖。

### 根布局由 Host 管理

运行时结构统一为 `Canvas → FrontedV3ControlHost → FrontedV3ControlBase`。`FrontedV3ControlHost` 是 v3 控件路径中唯一被直接加入 Canvas 的元素，所有根级属性都由 Host 拥有：

- `Canvas.Left` / `Canvas.Top`
- `Panel.ZIndex`
- `Width` / `Height`
- `Visibility`
- 高斯模糊 `Effect`
- `BehaviorGuid` 标记
- 运行时生成标记
- Designer 选中与根 Move/Resize
- 错误占位（控件初始化失败时显示安全占位，原 Config 保留，不写默认值）

包装的 Control 只负责矩形区域内的视觉内容，不得设置自己的 Canvas 坐标。

Host 的统一所有权不意味着这些字段只读。Designer 在根控件属性面板中以通用 Schema 提供 X/Y（`Left`/`Top`）、宽度、高度和层级（`ZIndex`）编辑；提交只写入 Config，再由预览和运行时 Host 应用。它们不是控件 Options，也不由插件重复注册。

### Options 不进入 JSON

`Options` 是由属性 Schema 构建的动态代理视图，**不进入 JSON**，**不缓存独立值**。XAML 中将 `DataContext` 设置为 `Options`，绑定路径 `{Binding Appearance.TextColor}` 通过 `ICustomTypeDescriptor` 发现动态属性，最终委托到对应 `IFrontedV3StorageAccessor`，直接读写当前 Config 的根级字段。

`OptionsPath`（如 `Appearance.TextColor`）只是 Designer 属性网格与 StyleTransfer 的逻辑路径，不进入 JSON；实际读写位置由 `Storage` 访问器决定。插件控件使用 `FrontedV3Storage.ExtensionData("key")` 时，属性值存储到 `PluginFrontedControlConfig.ExtensionData` 字典，序列化后平铺到 JSON 根级字段（如 `TextColor`、`TeamName`）。

### Part 管理固定内部区域

固定 Part 系统管理控件内部固定区域（如 BorderedImage 的内层 Image、MapV2Display 的 TeamName/MapCard/MapName/CampName/PickingBorder 部件）。通过控件类上的 `public static readonly FrontedV3Part` 字段声明：

```csharp
public static readonly FrontedV3Part LogoPart =
    FrontedV3Part.Register<TeamCardControl>("Logo")
        .WithSize(
            FrontedV3Storage.ClrProperty("LogoWidth"),
            FrontedV3Storage.ClrProperty("LogoHeight"))
        .WithCapabilities(FrontedV3PartCapabilities.Resize);
```

XAML 中通过 `fronted:FrontedV3.PartId="Logo"` 附加属性标记 Part Visual，与 C# 特性 `[FrontedV3PartVisual("Logo")]` 等价。Visual 发现器同时扫描两种声明方式，缺失或重复的 Visual 输出诊断日志，不崩溃 Designer。Part 的几何值通过 `Storage` 访问器读写到 Config 的现有字段，存储访问器为 `null` 时表示该维度不可持久化。

### PartCollection 管理模板或动态子项

PartCollection 系统管理模板或动态集合（如 GlobalScoreRow 的 Cells）。通过控件类上的 `public static readonly FrontedV3Parts` 字段声明：

```csharp
public static readonly FrontedV3Parts CellsCollection =
    FrontedV3Parts.RegisterCollection<GlobalScoreRowControl>("Cells")
        .WithStrategy(FrontedV3PartCollectionStrategy.FixedTemplate)
        .WithItemCapabilities(FrontedV3PartCapabilities.MoveAndResize)
        .WithCollectionGetter(c => ((GlobalScoreRowControlConfig)c).Cells)
        .WithItemKeySelector(item => ((GlobalScoreCellConfig)item).Id)
        .WithEnsureTemplateItems(c => EnsureCells((GlobalScoreRowControlConfig)c));
```

三种预设策略：

| 策略 | 行为 | 典型场景 |
| --- | --- | --- |
| `FixedTemplate` | 根据业务模板补齐缺失项，拒绝任意增删；可移动、缩放、编辑 | GlobalScoreRow 的 Cells |
| `Dynamic` | 允许任意增删集合项 | 动态图层列表 |
| `ReadOnly` | 只读，不允许增删或几何操作 | 只读装饰集合 |

### StyleTransfer 不默认传播数据身份

`FrontedV3StyleTransferService` 提供父-子继承与同 peer 传播，替代旧链路中 MapV2/GlobalScore 的手写 StyleTransfer 特判。不可破坏的传播约束：

- Peer 传播仅匹配完全相同的 `CanonicalControlType`。`plugin:a/TeamCard` 不能传播给 `plugin:b/TeamCard`。
- 默认仅传播 `Appearance` 语义的属性（颜色、字体、边框等）。
- `RootSize`/`PartLayout`/`Behaviors`/`Effects` 语义只有 profile 显式开启时才传播。
- `DataIdentity` 语义（MapKey、TeamType、BindingPath、ControlName 等）和 `Other` 语义**永远不传播**。
- 根级保留字段（`Left`/`Top`/`ZIndex` 等）由 Designer 的通用根选择 Schema 提供，但不会注册为控件 Options，因此不在 peer StyleTransfer 传播范围内。

属性语义通过 `FrontedV3PropertyMetadata.Semantic` 声明，默认为 `Other`（不参与传播）。继承模式通过 `FrontedV3PropertyMetadata.Inheritance` 声明，支持 `None`、`ParentFallback`（动态回退到父值）、`CopyFromParentOnCreate`（创建时复制后独立）、`LockedToParent`（锁定到父值，拒绝 override）。

### 缺失插件数据不会丢失

layout JSON 中的 `plugin:*` 控件即使插件未安装也会反序列化为通用 `PluginFrontedControlConfig`，并通过 `JsonExtensionData` 保留插件专属属性，确保可读取、保存和 roundtrip。缺失插件时：

- `ExtensionData` 原样保留，不会丢失。
- Designer 显示 Missing Plugin placeholder，根控件仍可选择、移动、缩放和删除。
- 运行时默认跳过该控件并记录 warning，不让前台窗口崩溃。
- 导出会继续保留控件 JSON、`ExtensionData` 和依赖元数据。
- 不会写入任何默认值掩盖缺失。
- 安装插件并重启后，schema 恢复可用。

### Designer 去特判化

Designer v3 的选中模型由 `FrontedV3DesignSelection` 统一管理，不再为 MapV2/GlobalScore 等控件手写特判。几何操作通过 `IFrontedV3GeometryTarget` 抽象，根控件使用 `RootControlGeometryTarget`，固定 Part 使用 `FixedPartGeometryTarget`。PropertyGrid 通过 `Options` 动态代理视图统一展示属性，不再为插件控件单独构建 descriptor 驱动的属性面板。

## 8. 前台编辑窗口设计

新版编辑器是独立窗口，而不是直接编辑被 OBS 捕获的真实前台窗口。详细设计见 [fronted-designer-editor.md](fronted-designer-editor.md)。编辑器依赖 v3 的硬规则：JSON key 等于控件名。

`FrontedDesignerWindow` 是后台侧独立编辑器窗口，入口在 `FrontManagePage`。它通过 `FrontedDesignerLayoutCatalog` 暴露可编辑窗口；该 catalog 只从 `IFrontedWindowRegistry.GetV3LayoutWindows()` 获取 v3 registrations，不存在硬编码 fallback 或内置窗口清单。Designer 按窗口选择读取 v3 layout JSON，使用 `FrontedLayoutDesignConverter` 和 `FrontedLayoutValidator` 显示设计文档与校验结果，并调用现有 `IFrontedRenderer` 把真实 v3 布局渲染到自己的 `PreviewCanvas`。

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

当前转换策略是 legacy-first：目标 window 由 converter 内的旧 XAML 尺寸、背景和控件 blueprint 默认表从零创建，不读取当前 `Resources/FrontedLayouts/{Window}.json`，也不 clone 当前内置 v3 控件。实际输出控件只来自 legacy source window/canvas mapping 与旧 release XAML 对照出的 Legacy Control Blueprint；fuzzy matching 只用于诊断候选提示，不参与正式转换结果。`WidgetsWindow/BpOverViewCanvas` 会拆分到 `BpOverviewWindow`，`WidgetsWindow/MapV2Canvas` 会拆分到 `MapV2Window`，`WidgetsWindow/MapBpCanvas` 仍因 MapBpV1 未接入 Designer v3 而跳过并告警。`ScoreGlobalWindow/BaseCanvas` 兼容旧 `MainTeamName` / `MainScoreTotal` 到 v3 `HomeTeamName` / `HomeScoreTotal`，并把旧半场格子聚合到 `GlobalScoreRow.Cells`。旧 Ban 锁 overlay 会折叠进 `Image` / `BorderedImage` 的 lockable metadata；overlay 的独立几何不再覆盖主图几何。旧 `CustomUi/` 图片复制到包内 `resources/images/` 并生成 `bpui://{PackageId}/...` URI。旧 `Config.json` 只读取明确的前台图片/颜色字段用于安全映射，不覆盖全局设置。

legacy 转换会把 `ScoreWindowSettings.GlobalScoreBgImageUri` 写入 `ScoreGlobalWindow/BaseCanvas/BackgroundImage`，把 `ScoreWindowSettings.GlobalScoreBgImageUriBo3` 写入 `BoModeStates["Bo3"].BackgroundImage` 并启用 BO mode states。

## 11. 明确非目标

当前明确不做以下事情：

| 非目标 | 说明 |
| --- | --- |
| 修改无关运行时行为 | 不改 ViewModel、插件加载逻辑或未迁移窗口的运行逻辑。 |
| 继续批量迁移 XAML | 当前 v3 layout window 通过 `FrontedWindowBase` host 渲染；`WidgetsWindow` 已删除，后续不应顺手改无关窗口。 |
| 实现完整编辑器 UI | 当前编辑器已有交互层、Property Grid、Add Control、Binding/Resource Browser、保存/重置。仍不实现 Save As。 |
| 修改 Pick 控件 `BehaviorGuid` | 必须同步更新内置 `BpWindow.behaviors.json` 的目标引用。 |
| 迁移 `.bpui` | 旧 `.bpui` 已有转换器。 |
| 改变现有 v3 layout JSON schema | v3 schema 已稳定。 |
| 把 `AllowTransparency` 当成控件属性 | 它是窗口级选项。 |

## 11.1 Shape 控件

Designer v3 内置 `Rectangle` 和 `Polygon`。两者共享 Shape 填充配置，支持静态或绑定的纯色填充，以及起始色、结束色可分别静态配置或绑定的双颜色线性渐变。颜色绑定值是 `#RRGGBB` 或 `#AARRGGBB` 字符串，可使用 `HomeTeam.ColorHex` / `AwayTeam.ColorHex`。渐变角度约定为 `0°` 从左到右、`90°` 从上到下，并规范化到 `0..360`。

`Polygon.Points` 保存为控件局部 `0..1` 归一化坐标，因此调整控件宽高时会保持形状比例。选中 Polygon 后，Interaction Layer 显示独立的橙色顶点手柄；拖动只修改顶点，不移动整个控件。属性区域提供添加和删除顶点按钮，且始终保留至少三个顶点。

## 12. 与 Score System v2 的关系

`ScoreSurWindow` 和 `ScoreHunWindow` 已作为 v3 renderer pilot 接入 JSON 布局，默认布局不再绑定旧的 `CurrentGame.*Team.Score.*` 字段。Score System v2 的权威比分状态在现有 `Core.Models.Game.MatchScoreState`，局内比分窗口绑定 `CurrentGame.MatchScore` 的派生字段。运行时不再把 `MatchScoreState` 同步回 `Team.Score`。

`ScoreGlobalWindow` 已接入 v3 renderer，默认布局绑定 `MatchScoreState`，全局比分行由 `GlobalScoreRow` 控件生成。`FrontedWindowService` 不再动态创建并直接修改全局比分控件。详细设计见 [score-system-v2.md](../business/score-system-v2.md)。
