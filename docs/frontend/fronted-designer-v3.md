# V3 控件、渲染与绑定

本文负责控件运行时、内置控件语义和绑定模型。窗口配置/路径见[布局契约](fronted-layout-v3-window-centric.md)，窗口生命周期见[窗口运行时](frontend-windows-and-layout.md)，交互与持久化编辑见[独立编辑器](fronted-designer-editor.md)。插件公开 V3 Property、Part、PartCollection、Options 与 StyleTransfer API 以 [SDK 文档](../plugin/plugin-development.md)为准。

## 源码入口

| 主题 | 入口 |
| --- | --- |
| 渲染、生成标记和 namescope | [FrontedRenderer](../../neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedRenderer.cs) |
| 注册元数据与 Host | [V3 运行时](../../neo-bpsys-wpf.Core/Services/FrontedLayout/V3)、[注册扩展](../../neo-bpsys-wpf.Core/Extensions/Registry) |
| 内置基础控件 / 业务控件 | [Core 控件](../../neo-bpsys-wpf.Core/Services/FrontedLayout)、[宿主控件](../../neo-bpsys-wpf/Controls/FrontedLayout) |
| 绑定根与订阅 | [FrontedBindingFactory](../../neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedBindingFactory.cs)、[绑定模型](../../neo-bpsys-wpf.Core/Models/FrontedLayout/Binding) |

## 渲染所有权与控件身份

FrontedRenderer 从 registry 按 CanonicalControlType 找到 registration，由 FrontedV3ControlHost 创建和初始化 FrontedV3ControlBase。Host 负责根几何、层级、Visibility、模糊与 BehaviorGuid；控件只管理内部视觉内容。Options 是 schema 到 config 的动态视图，不保存第二份值、不进入 JSON。详细扩展契约及可用示例统一见 SDK。

注册成功时 Canvas 的直接子项是 FrontedV3ControlHost，由它应用根几何并包装控件与效果链；FrontedEffectHost 是内部效果承载，不取得独立名称/GUID/绑定身份。缺插件 placeholder 则由 renderer 以效果 host 包装。移动、排序和动画必须解析当前布局载体，不按固定内部层级猜测。

ControlLayout.Controls 的 JSON key 就是控件名，也是设计项 Name、内部语义元素命名与 namescope 注册使用的逻辑名称；Config 内不再存重复 Name。renderer 把该 key 传入 Context.ControlName，并注册 Host 的 RegisteredName；不要假定所有包装元素的 FrameworkElement.Name 都等于它。名称非空、同状态内唯一，匹配 `^[A-Za-z_][A-Za-z0-9_]*$`，长度上限 64。原始 JSON 必须避免重复 key：当前 window 控件 converter 以字典赋值读取，重复 key 会被后项覆盖，后续 validator 无法找回丢失项，见[已知边界](../known-limitations-and-roadmap.md)。不能把 legacy converter 的重复键检测当成所有 v3 输入都已有保护。

动画身份使用稳定 BehaviorGuid；重命名不更换 GUID，复制生成新 GUID，删除清理关联行为。生成 part 用稳定引用，不能依赖显示名称。内置 BpWindow 的 Pick GUID 与配套 behaviors 文件须同步维护。SurPick0–3、HunPick、PickingBorder 等名称用于迁移和诊断；当前名称保护边界见[已知边界](../known-limitations-and-roadmap.md)，编辑器的引用检查不能替代 GUID 契约。

## 内置控件模型

v3 内置控件类型如下：

| `ControlType` | 用途 |
| --- | --- |
| `Text` | 文本、队名、比分、倒计时等。 |
| `LocalizedText` | 根据本地化资源 key 显示静态文本，主要用于表头、标签等不应写死在 JSON 中的用户可见文本。 |
| `Image` | 通用图片控件，根元素是承载图片和内部 overlay 的 `Grid`，用于队伍 LOGO、地图、角色图、Ban 位、pick 图等。 |
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
| FrontedV3ControlHost | 根坐标、宽高、ZIndex、Visibility；内部 Border 只承载文本。 |
| 内层 `TextBlock` | 文本绑定、字体、字号、字重、颜色、水平/垂直对齐、`TextAlignment`、`TextWrapping`。 |

行为动画的 Control 层按 V3 Host 解析；Content 层解析文本内容
`TextBlock`；`TargetLayer=OverlayAbove/OverlayBelow` 会在该文本控件上方或下方生成运行时
`Rectangle` 承接层，用于描边、填充等不属于文本本体的视觉效果。

`TextBinding.Sources` 按顺序以 `ISharedDataService` 为 binding `Source`，运行时始终创建 `MultiBinding`：

```json
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
| Image 内部 Grid | 主内容容器与 lock/picking overlay；根坐标、宽高、层级由外部 V3 Host 应用。 |
| BorderedImage 内部 Border | 提供外框与裁剪；设计器根 resize 修改 Host 配置，内层 resize 则改图片属性。 |
| `BorderedImage` 内层主内容容器 | 作为主图视口承接 `TargetLayer=Content` 的通用视觉动画；容器内的 `Image` 接收 `Source` 绑定、`ImageWidth`、`ImageHeight`、`Stretch`、`HorizontalAlignment`、`VerticalAlignment` 等图片展示属性。 |

行为动画的 Control 层按 V3 Host 解析；`TargetLayer=Content` 指向只包含主图的主内容容器，`Opacity`、`ClipInset*`、位移、
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

## Image overlay 与旧字段兼容策略

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

控件名称、BehaviorGuid 与配套行为的维护边界见本文“渲染所有权与控件身份”。

## Binding catalog

Binding Browser 的数据源由 `IFrontedBindingCatalogProvider` 构建，默认实现是 `FrontedBindingReflectionCatalogProvider`。它只扫描显式注册根，不全局扫描服务或应用对象：

1. `IFrontedBindingRootProvider` 注册根，例如 `CurrentGame : Game`、`HomeTeam : Team`、`AwayTeam : Team`、`RemainingSeconds : string` 和 Ban 可用状态列表。
2. `[FrontedBindingObject]` 标注的 DTO 自动包含 public readable instance properties。
3. `[FrontedBindingIgnore]` 与 `JsonIgnore` 类似，只影响 Binding Browser；公开但不适合前台布局绑定的属性应显式排除。
4. `[FrontedBindable]` 可为单个属性补充显示/描述或在非自动包含类型上强制包含。
5. `[FrontedBindingCollection(FixedCount = ...)]` 用于固定列表索引；dictionary 只在声明 `KnownKeys` 时展开。

catalog 构建是 lazy + cache，只做类型反射，不调用属性 getter，不读取 `ISharedDataService` 的当前值，不调用 `NewGame`，不枚举运行时集合。新增普通 DTO 字段时优先通过模型属性和 attribute 暴露；`IFrontedBindingCatalogContributor` 只用于虚拟节点、legacy alias、插件语义 key 等动态节点，不应重建旧的手写整树。

当前 Player 图像绑定包括 `PictureShown`、`PictureShownWithFullCharacter` 和 `PictureShownHeader`。三者在没有 `Character` 时回退 `Member.Image`；有 `Character` 时分别使用 `HalfImage`、`BigImage` 和 `HeaderImage`。

### 通用动画部件

每个控件的行为集合都可以配置 `AnimationParts`。Designer 在选中控件的行为区域提供动画部件列表和
新增、删除、重命名、类型、层级、尺寸、颜色、图片、初始可见性与透明度编辑。动画部件始终是
父控件内部的生成视觉部件，不会成为主 Canvas 中的独立设计项，也不会出现在左侧控件列表或布局 JSON 中。

动画部件可通过 `part:{BehaviorGuid}:{AnimationPartName}` 选为动画目标。宽高支持像素文本和百分比；
动画部件的 `VisualOffsetX/Y` 百分比相对父控件宽高计算，适合构建扫描线、闪光、边缘高亮等通用效果。
动画部件可配置通用视觉效果 `Effect`，支持 `None`、`Glow` 和 `DropShadow`。`Glow`/`DropShadow` 使用 WPF `DropShadowEffect`，可配置颜色、不透明度、模糊半径、投影距离和方向；默认 `None` 不创建 effect，避免不必要的渲染成本。

## 插件与缺失控件

统一注册、属性及内部部件契约见 [SDK](../plugin/plugin-development.md)。未知 plugin:* 配置保留 PluginFrontedControlConfig.ExtensionData；Designer 显示占位且保留原 JSON，runtime 跳过并记录 warning。未知非插件 ControlType 仍是无效控件，不能靠默认值掩盖。包级依赖预检及缺失窗口数据保留见 [BPUI](bpui-package-v3.md)。

## 动态索引绑定

`BindingPath` 不是 Binding Browser 的白名单。除浏览器生成的普通路径外，也可以直接输入动态索引：

```text
Players[0].Name
Players[CurrentGame.SelectedPlayerIndex].Name
```

方括号中的完整动态 path 始终从 `ISharedDataService` root 重新解析，不相对于前面的 `Players` 或其他中间对象解析。一个路径可包含多个动态索引，例如：

```text
Foo[Bar.Index].Items[Baz.SelectedIndex].Name
```

普通 property path 和静态索引（如 `[0]`、`['key']`）继续使用原生 WPF Binding。只有含动态索引的路径才会启用 v3 的动态订阅运行时；它会随索引值、路径中间属性、目标 item 属性以及 `INotifyCollectionChanged` 集合变更重新解析。

动态索引不是表达式语言。`[]` 内仅支持 literal index/key 或完整 Binding Path，不支持：

```text
[Index + 1]
[Condition ? 0 : 1]
[Method()]
```

Score System 的单半场小比分可使用：

```text
CurrentGame.MatchScore.HalfByProgress[CurrentGame.GameProgress].SurMinorScore
CurrentGame.MatchScore.HalfByProgress[CurrentGame.GameProgress].HunMinorScore
```

`HalfByProgress` 复用比分状态的 BO3/BO5 进度解析，因此 numeric value 为 `6`/`7` 时会随当前 BO mode 正确区分 BO5 第四局与 BO3 第三局加赛；不要把 `GameProgress` numeric value 当作半场数组下标。


## Shape 控件

Designer v3 内置 `Rectangle` 和 `Polygon`。两者共享 Shape 填充配置，支持静态或绑定的纯色填充，以及起始色、结束色可分别静态配置或绑定的双颜色线性渐变。颜色绑定值是 `#RRGGBB` 或 `#AARRGGBB` 字符串，可使用 `HomeTeam.ColorHex` / `AwayTeam.ColorHex`。渐变角度约定为 `0°` 从左到右、`90°` 从上到下，并规范化到 `0..360`。

`Polygon.Points` 保存为控件局部 `0..1` 归一化坐标，因此调整控件宽高时会保持形状比例。选中 Polygon 后，Interaction Layer 显示独立的橙色顶点手柄；拖动只修改顶点，不移动整个控件。属性区域提供添加和删除顶点按钮，且始终保留至少三个顶点。

## 业务状态边界

比分控件以 CurrentGame.MatchScore 为权威源；不回写 Team.Score，不在 WindowService 动态维护另一份全局比分。普通/自由模式与 GameProgress 映射见[比分规范](../business/score-system-v2.md)。共享状态替换、通知和 OCR 写入边界见[共享数据](../architecture/shared-data-and-state.md)。

## 业务文本本地化

`GameProgressText` 使用集中 helper 和资源 key 生成 `FREE GAME`、`GAME {n} FIRST HALF`、`GAME {n} OVERTIME SECOND HALF` 等文本，避免 BO3/BO5 进度文案散落在窗口 XAML 或 JSON 中。默认是单行文本（`DisplayMode = Inline`）；正式预设包括单行、双行、横排局数、横排半场、竖排、竖排双行、竖排局数、竖排半场。`DisplayLanguage = FollowApp` 时按 `WPFLocalizeExtension` 的当前应用语言生成文本，中文应用语言下应显示中文局数和半场文案。`MapNameText` 默认把 `CurrentGame.PickedMap` 枚举名作为本地化 key 查询地图名（地图 key 归属 `Locales.Game`），也可以通过 `BindingPath` 指向其他地图字段，例如当前对局的 picked / banned map 数据；新增地图时要同步补齐地图资源 key。`LocalizedText` 用 `LocalizationKey` 经 `I18nHelper.GetLocalizedStringFromAnyHostDictionary` 查询普通 resx 文案，适合 GameData 表头等静态标签；如果 key 缺失会显示 `FallbackText` 或 key 本身。普通 `Text.Text` 仍是原样静态文本，不会自动本地化。
