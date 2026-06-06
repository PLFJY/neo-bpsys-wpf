# Fronted Designer v3 设计文档

本文是前台窗口设计者模式 v3 重构的设计文档。v3 目标是转向 JSON/config-driven UI：前台窗口 XAML 最终只保留外层 Canvas，控件由 JSON 配置描述，并由已注册的控件工厂创建。这样可以把布局、素材、控件类型和绑定关系放到可迁移、可导入导出的结构中，也为独立编辑窗口、插件扩展控件和新版 `.bpui` 包提供基础。

当前所有内置前台窗口均已接入 v3 renderer：`ScoreSurWindow`、`ScoreHunWindow`、`ScoreGlobalWindow`、`CutSceneWindow`、`GameDataWindow`、`WidgetsWindow`、`BpWindow`。Designer v3 独立编辑器（`FrontedDesignerWindow`）是当前唯一的设计编辑器。

## 1. 背景与目标

当前设计者模式历史上是 XAML-first：前台窗口的具体控件直接写在各窗口 XAML 中，运行时再由 `FrontedWindowService` 扫描 Canvas 子元素并保存/恢复每个 Canvas 的 `ElementInfo`。这些旧版文件和 `Config.json` 前台自定义字段现在只作为 legacy 转换、迁移对照存在；当前运行时和编辑器路径是 Designer v3 + `FrontedLayouts`。SettingPage 旧前台自定义入口已移除，旧版真实窗口设计器也已移除。旧 `.bpui` 包与 `Config.json`、`CustomUi/`、`FrontElementsConfig/` 等历史结构的耦合只由 legacy 转换流程处理。

v3 目标是转向 JSON/config-driven UI：前台窗口 XAML 最终只保留外层 Canvas，控件由 JSON 配置描述，并由已注册的控件工厂创建。这样可以把布局、素材、控件类型和绑定关系放到可迁移、可导入导出的结构中，也为独立编辑窗口、插件扩展控件和新版 `.bpui` 包打基础。

这项重构必须分阶段推进，不能在一个巨大提交中同时改设置版本、迁移、渲染器、窗口 XAML、编辑器和 `.bpui`。前台窗口会被 OBS 捕获，导播现场对稳定性要求高；每个阶段都应保持旧路径可回退，并优先迁移低风险窗口验证模型。

## 2. 版本体系

| 文件类型 | v3 版本字段 | 说明 |
| --- | --- | --- |
| 主设置 `config.json` | `Version = 3` | 新创建的配置应写入 `Version = 3`。缺失或 `null` 表示 legacy 配置。 |
| Canvas 布局配置 | `"Version": 3` | 每个前台 Canvas 独立一个 v3 布局配置文件。 |
| v3 `.bpui` 包 | `"FormatVersion": 3` | 包格式版本。完整 manifest schema 见 [bpui-package-v3.md](bpui-package-v3.md)。 |

这些版本号刻意对齐为 3，方便维护者和用户理解当前代际，但它们仍属于不同文件类型：`config.json` 版本不等于 Canvas 布局 schema 版本，也不等于 `.bpui` 包版本。后续代码实现时不要把三者混成一个枚举或一个迁移入口。

`.bpui v3` 包必须只携带 Designer v3 前台布局、布局资源、manifest 和可选预览/说明，不得包含或覆盖全局 `Config.json`。manifest 使用根级 `MinVersion`，不包含 `App` 对象或 `App.MinVersion`。v3 包可以从 `FrontManagePage` 导入安装，激活时会把包内 `layouts/{Window}/{Canvas}.json` 和可选 `window.json` 复制到用户布局目录；激活内置布局会清空用户布局并回退到内置资源。legacy `.bpui` 会在导入前转换为干净 v3 包，运行时 renderer 仍只读取 v3 layout，不增加 legacy 兼容分支。

`config.json` 中缺失或 `null` 的 `Version` 表示 legacy 配置。当前加载配置前会检测 raw JSON root，备份 legacy `Config.json` 后写回 `Version = 3`；该迁移只更新主设置版本，不迁移前台窗口布局、不生成 v3 Canvas 配置，也不移除旧前台设置。

## 3. 新版 Canvas 配置文件结构

推荐路径：

| 来源 | 路径 |
| --- | --- |
| 用户布局 | `%APPDATA%\neo-bpsys-wpf\FrontedLayouts\{WindowTypeName}\{CanvasName}.json` |
| 内置默认布局 | `Resources\FrontedLayouts\{WindowTypeName}\{CanvasName}.json` |
| 插件默认布局 | `{PluginFolder}\FrontedLayouts\{WindowTypeName}\{CanvasName}.json` |

每个前台 Canvas 使用独立布局配置文件。内置窗口的 `{WindowTypeName}` 使用窗口类型名，例如 `BpWindow`；插件窗口使用 `plugin:{PackageId}/{WindowTypeName}`，保存到用户目录时会映射为安全子路径。`{CanvasName}` 使用窗口 descriptor 声明的 Canvas 名称，例如 `BaseCanvas`。

以下路径属于 legacy 格式，仅由 legacy `.bpui` 转换流程使用，不再被运行时读取：

| legacy 路径 | 说明 |
| --- | --- |
| `%APPDATA%\neo-bpsys-wpf\{WindowTypeName}Config-{CanvasName}.json` | 旧 `FrontedWindowService` 保存的 `ElementInfo` 用户布局。仅用于 legacy 转换。 |
| `Resources\FrontedDefaultPositions` | 旧内置默认位置文件目录。仅用于 legacy 转换。 |

v3 渲染路径优先读取新目录。legacy 文件只应进入迁移流程，不应让新运行时渲染代码长期保留旧格式分支。

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
    "PickingBorderAvailable": true,
    "PickingBorderName": "SurPickingBorder1",
    "PickingBorderImagePath": "Resources/pickingBorder.png",
    "Lockable": false
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

Canvas 可启用通用 BO3/BO5 状态：root-level `BackgroundImage` / `RequiredPlugins` / 控件表示默认/BO5 state，`EnableBoModeStates = true` 且 `BoModeStates["Bo3"]` 存在时，运行时会在 `ISharedDataService.IsBo3Mode == true` 时渲染 BO3 state。BO3 state 拥有独立 `BackgroundImage`、`RequiredPlugins` 和 `Controls`，因此控件位置、大小、ZIndex、绑定、静态文本和 `Visibility` 都可以与 BO5 不同。`BackgroundImageVariants` 已移除，不保留迁移兼容分支。

layout validator 会校验 Canvas 级字段：`Version` 必须为 3，`CanvasWidth` / `CanvasHeight` 必须大于 0，`BackgroundImage` 非空且 resolver 可用时应能解析到文件。root-level 控件 JSON key 的重复检测必须发生在 raw JSON / converter 阶段；如果先反序列化成 `Dictionary<string, FrontedControlConfigBase>`，重复 key 可能已经丢失。

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
| `MapV2Display` | `WidgetsWindow` 地图 BP v2 控件，复用 `MapV2Presenter`；地图卡片正常/禁用外框颜色由布局配置控制。 |

### Text

`Text` 控件使用外层 `Border` 和内层 `TextBlock`：

| 层级 | 接收属性 |
| --- | --- |
| 外层 `Border` | `Canvas.Left`、`Canvas.Top`、`Width`、`Height`、`Panel.ZIndex`。 |
| 内层 `TextBlock` | 文本绑定、字体、字号、字重、颜色、水平/垂直对齐、`TextAlignment`、`TextWrapping`。 |

`BindingPath` 应以 `ISharedDataService` 为 binding `Source`：

```csharp
new Binding(config.BindingPath)
{
    Source = sharedDataService
};
```

如果 `BindingPath` 为空，`Text` 控件也可以使用 `"Text"` 字段显示原样静态文本。`BindingPath` 与 `Text` 同时存在时，`BindingPath` 优先，静态 `Text` 会被忽略。`Text` 支持可选 `StringFormat`，但只在 `BindingPath` 非空时应用；静态 `Text` 不会套用格式化。静态 `Text` 不会自动走 `WPFLocalizeExtension`、`I18nHelper` 或 resx，需要业务规则或本地化文本时，应优先使用 `GameProgressText`、`MapNameText`、`LocalizedText` 等控件。

### LocalizedText

`LocalizedText` 使用外层 `Border` 和内层 `TextBlock`，布局和字体字段与 `Text` 基本一致，但文本来源是 `LocalizationKey`。如果资源 key 缺失，则显示 `FallbackText`；`FallbackText` 为空时显示 key 本身。`LocalizedText` 会在语言设置变化时刷新文本。

### Image / BorderedImage

内置图片控件拆为 `Image` 和 `BorderedImage`：

| 层级 | 接收属性 |
| --- | --- |
| `Image` 根 `Grid` | `Canvas.Left`、`Canvas.Top`、`Width`、`Height`、`Panel.ZIndex` 和控件 `Name`。内部第一层是主 `Image`，后续子元素是 lock / picking border overlay。 |
| `BorderedImage` 外层 `Border` | `Canvas.Left`、`Canvas.Top`、`Width`、`Height`、`Panel.ZIndex`，设计器默认 resize handles 作用于这一层。 |
| `BorderedImage` 内层 `Image` | `Source` 绑定、`ImageWidth`、`ImageHeight`、`Stretch`、`HorizontalAlignment`、`VerticalAlignment` 等图片展示属性。 |

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
| `GameProgressText` | `CurrentGame.GameProgress` + `IsBo3Mode` 的显示文本，显式区分 BO3 第三局加赛与 BO5 第四局；`UseLineBreak = true` 时把 Game / Overtime 和 half 分为两行。 |
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

`BpWindow` 已迁移到 `Resources/FrontedLayouts/BpWindow/BaseCanvas.json`。`BpWindow.xaml` 不再持有 BP 控件，只保留外层 `BaseCanvas`；默认布局中的 `SurPick0..3`、`HunPick`、`SurPickingBorder0..3` 和 `HunPickingBorder` 由 v3 renderer 生成或注册。renderer 会把控件名注册到窗口 namescope，因此 `AnimationService` 继续可以通过 `window.FindName(...)` 找到 pick 图和呼吸边框。

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
| 多 Canvas | `WidgetsWindow` 等多 Canvas 窗口必须逐 Canvas 编辑和保存。 |
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

> 以下记录各阶段的完成范围，供追溯参考。

| 阶段 | 范围 | 当前状态 |
| --- | --- | --- |
| Phase 0 | 设计文档 only | 已完成 |
| Phase 1 | `Settings.Version = 3`，legacy config 迁移 skeleton | 已完成 |
| Phase 2 | v3 layout models、资源 resolver、Text/Image factory、renderer skeleton | 已完成 |
| Phase 3 | `ScoreSurWindow` / `ScoreHunWindow` 迁移到 v3 renderer，绑定 `MatchScore` | 已完成 |
| Phase 4 | `CutSceneWindow` 迁移，`TalentTraitDisplay` / `GameProgressText` / `MapNameText` 控件 | 已完成 |
| Phase 5 | `GameDataWindow` 迁移，`LocalizedText` 控件 | 已完成 |
| Phase 6 | `WidgetsWindow` 多 Canvas 迁移，当前 Ban 位改为 `Image` + `Lockable` / `MapV2Display` | 已完成 |
| Phase 7 | `BpWindow` 迁移，Ban 位和 pick 呼吸边框改为 `Image` overlay | 已完成 |
| Phase 8A | 独立编辑器设计规格 | 已完成 |
| Phase 8B | 设计期基础：设计项模型、validator、引用扫描、关键名称 catalog | 已完成 |
| Phase 8C | 独立编辑器 shell、窗口/Canvas 选择器、只读预览 | 已完成 |
| Phase 8D | interaction layer、透明 hitbox、拖拽/缩放、键盘微调 | 已完成 |
| Phase 8E | 基础 Property Grid、Name 编辑、运行时关键名称保护 | 已完成 |
| Phase 8F | Add Control、默认 config factory、FontFamily 字体 ComboBox、Undo/Redo | 已完成 |
| Phase 8G | Binding Browser、Resource Browser | 已完成 |
| Phase 8H | 用户 layout save/reset、脏状态提示、吸附网格 | 已完成 |
| Phase 9A | `.bpui v3` 文档规格 | 已完成 |
| Phase 9B.0 | Canvas Properties GUI、`bpui://local` 资源规范化、Window Options | 已完成 |
| Phase 9B.1 | `FrontManagePage` Layout Package Manager UI skeleton | 已完成 |
| Phase 9C | v3 `.bpui` 导出、manifest 对话框、资源重写 | 已完成 |
| Phase 9D | v3 `.bpui` 导入/安装/激活/删除 | 已完成 |
| Phase 9F | legacy `.bpui` 转换器 | 已完成 |
| Phase 12 | Designer v3 显示层 i18n | 已完成 |
| Phase 13A | 插件前台控件文档和 schema | 已完成 |
| Phase 13B | 插件控件 registry、descriptor API、runtime 缺失跳过 | 已完成 |
| Phase 13C | Designer 插件控件支持、Add Control、缺失占位符 | 已完成 |
| Phase 13C.5 | 示例插件清理 | 已完成 |
| Phase 13D/15 | `.bpui` 依赖扫描、导出/导入、缺失保留 | 已完成 |
| Phase 13E | 插件市场交互式安装/更新引导 | 已完成 |
| Phase 13F | 安全、版本兼容、i18n 和测试收口 | 已完成 |
| Phase 14B | 左侧图层面板：ZIndex 分组、同层排序、跨层移动 | 已完成 |
| Phase 14D | 画布/对象智能对齐、临时辅助线 | 已完成 |

## 11. 明确非目标

当前明确不做以下事情：

| 非目标 | 说明 |
| --- | --- |
| 修改无关运行时行为 | 不改 ViewModel、插件加载逻辑或未迁移窗口的运行逻辑。 |
| 继续批量迁移 XAML | 当前已迁移 `ScoreSurWindow`、`ScoreHunWindow`、`ScoreGlobalWindow`、`CutSceneWindow`、`GameDataWindow`、`WidgetsWindow` 和 `BpWindow`；后续不应顺手改无关窗口。 |
| 实现完整编辑器 UI | 当前编辑器已有交互层、Property Grid、Add Control、Binding/Resource Browser、保存/重置。仍不实现 Save As。 |
| 修改 `AnimationService` 查找逻辑 | 当前 `AnimationService` 仍通过 `FindName` 查找动画目标。 |
| 迁移 `.bpui` | 旧 `.bpui` 已有转换器。 |
| 改变现有 v3 layout JSON schema | v3 schema 已稳定。 |
| 把 `AllowTransparency` 当成控件属性 | 它是窗口级选项。 |

## 12. 与 Score System v2 的关系

`ScoreSurWindow` 和 `ScoreHunWindow` 已作为 v3 renderer pilot 接入 JSON 布局，默认布局不再绑定旧的 `CurrentGame.*Team.Score.*` 字段。Score System v2 的权威比分状态在现有 `Core.Models.Game.MatchScoreState`，局内比分窗口绑定 `CurrentGame.MatchScore` 的派生字段。`Team.Score` 只作为剩余旧窗口的过渡兼容镜像。

`ScoreGlobalWindow` 已接入 v3 renderer，默认布局绑定 `MatchScoreState`，全局比分行由 `GlobalScoreRow` 控件生成。`FrontedWindowService` 不再动态创建并直接修改全局比分控件。详细设计见 [score-system-v2.md](score-system-v2.md)。
