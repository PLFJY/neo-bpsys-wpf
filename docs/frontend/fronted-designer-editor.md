# Fronted Designer v3 独立编辑器设计规格

本文记录 Fronted Designer v3 独立编辑器的设计规格。独立编辑器面向 v3 JSON layout 文件。它是后台侧的独立编辑窗口，不直接在真实前台窗口上编辑；真实前台窗口仍用于 OBS 捕获和运行时输出。当前 v3 layout 已改为 Window-centric：编辑器只选择 Window，不再选择或管理 Canvas；每个 v3 layout window 内部固定使用 `BaseCanvas`，并保持与现有 v3 renderer、行为引擎、业务控件和 JSON 格式兼容。

当前编辑器已实现：设计期基础模型、配置转换、校验器、引用扫描和运行时关键名称目录；独立 `FrontedDesignerWindow` shell、窗口选择器、只读预览渲染、缩放控制和校验面板；内存交互层、透明 hitbox、选择框、拖拽、缩放控制点和键盘微调；基础 Property Grid、Add Control 菜单、Binding Browser 和 Resource Browser；保存用户布局、重置为内置、脏状态提示、吸附网格、Undo/Redo。Designer v3 显示层 i18n 已完成，属性名、控件类型、ComboBox 选项等可本地化，但 layout schema 与保存值不变。

## 1. 硬规则：JSON Key = Control Name

v3 layout JSON 中，root object 的控件属性名就是控件名：

```json
{
  "SurTeamName": {
    "ControlType": "Text",
    "Left": 580,
    "Top": 720
  }
}
```

这表示：

| 位置 | 值 |
| --- | --- |
| JSON key | `SurTeamName` |
| `FrontedCanvasConfig.Controls` key | `SurTeamName` |
| `FrameworkElement.Name` | `SurTeamName` |
| WPF namescope 注册名 | `SurTeamName` |
| 编辑器设计项 `Name` | `SurTeamName` |

控件名不存储在单个 config object 内部，而是存储在 `FrontedCanvasConfig.Controls` 的 dictionary key 中。`FrontedControlConfigBase` 和各派生 config 类不应新增重复的 `Name` 属性，否则会形成两个名称来源，导致保存、重命名、namescope 注册和动画查找出现分歧。

运行时渲染流程：

1. `FrontedCanvasConfigJsonConverter` 读取 root-level 控件属性，把 `property.Name` 作为 `Controls` key。
2. `FrontedRenderer` 遍历 `config.Controls`，通过 `IFrontedV3ControlRegistry` 查找 `FrontedV3ControlRegistration`，由 `FrontedV3ControlHost` 创建并包装控件实例。
3. 控件实例的 root `FrameworkElement.Name` 设置为该 `name`，宿主通过 `FrontedV3ControlHost` 统一应用根布局（Canvas.Left/Top、Width/Height、ZIndex、Visibility 等）。
4. renderer 将生成控件注册到窗口或 Canvas namescope，并记录 `FrontedRendererProperties.RegisteredName`。

编辑器读取时必须把 dictionary 转成适合 UI 绑定的设计项集合；保存时再把设计项集合转回 `Dictionary<Name, Config>`。推荐设计时模型：

```csharp
public sealed partial class FrontedControlDesignItem : ObservableObject
{
    public string Name { get; set; }
    public FrontedControlConfigBase Config { get; set; }
    public bool IsSelected { get; set; }
    public bool IsRuntimeCritical { get; set; }
    public IReadOnlyList<string> ValidationErrors { get; }
}
```

保存流程：

1. 校验每个设计项的 `Name` 和 `Config`。
2. 以 `Name` 作为 dictionary key。
3. 以 `Config` 作为 dictionary value。
4. 序列化后 `Name` 必须再次成为 JSON key。

## 2. 名称校验

控件名必须：

1. 不是 `null`、空字符串或纯空白。
2. 在同一个 Canvas 内唯一。
3. 匹配安全的 WPF name 兼容模式，推荐正则：`^[A-Za-z_][A-Za-z0-9_]*$`。
4. 不包含空白字符。
5. 不包含 `.`, `[`, `]`, `/`, `\`, `:`, `#`。
6. 不与当前 Canvas 名称、窗口名称或编辑器保留名冲突，如未来实现需要保留这些命名空间。

校验严重级别：

| 级别 | 示例 |
| --- | --- |
| Error | 重复名称、非法名称、缺少 `ControlType`、未知 `ControlType`、缺少必填字段、引用目标不存在 |
| Warning | 运行时关键控件被重命名、可见内容为空、交互密集控件没有 `Width` / `Height`、编辑器不支持的插件控件 |
| Info | 控件使用 fallback placeholder、普通 `Text` 使用原样静态文本而不是 `LocalizedText` |

保存前必须阻止 Error。Warning 可以允许保存，但应明确提示并要求用户确认。Info 只用于状态栏、校验面板或属性提示，不应阻止保存。

Canvas 级字段同样必须校验：

1. `Version` 必须为 `3`。
2. `CanvasWidth` 必须为大于 0 的有效数字。
3. `CanvasHeight` 必须为大于 0 的有效数字。
4. `BackgroundImage` 可以为空；非空时如果资源 resolver 可用，应在无法解析时给出 Warning。
5. 设计文档中的 `WindowTypeName` 和 `CanvasName` 不能为空。

重复 JSON key 不能等到反序列化成 dictionary 后再处理，因为普通 dictionary 会丢失重复项。v3 converter 应在读取 raw root object 环节发现重复 root-level property 并抛出布局配置异常，编辑器后续可把该异常转成校验友好的错误提示。

## 3. 运行时关键名称

部分控件名是运行时契约，不只是设计器显示名。当前 `BpWindow` 必须保留：

| 控件名 | 原因 |
| --- | --- |
| `SurPick0` | 求生者 0 号 pick 图淡入淡出目标 |
| `SurPick1` | 求生者 1 号 pick 图淡入淡出目标 |
| `SurPick2` | 求生者 2 号 pick 图淡入淡出目标 |
| `SurPick3` | 求生者 3 号 pick 图淡入淡出目标 |
| `HunPick` | 监管者 pick 图淡入淡出目标 |
| `SurPickingBorder0` | 求生者 0 号 picking border 呼吸动画目标 |
| `SurPickingBorder1` | 求生者 1 号 picking border 呼吸动画目标 |
| `SurPickingBorder2` | 求生者 2 号 picking border 呼吸动画目标 |
| `SurPickingBorder3` | 求生者 3 号 picking border 呼吸动画目标 |
| `HunPickingBorder` | 监管者 picking border 呼吸动画目标 |

这些名称仍是布局迁移和诊断契约；动画目标使用稳定 `BehaviorGuid` 和生成 part 引用。

这些运行时关键名称集中在 `FrontedLayoutRuntimeContractCatalog` 中。编辑器、校验器和后续删除/重命名保护都应通过该 catalog 查询，不应在 UI 层或多个服务中重复硬编码同一批名称。后续其他窗口如果出现类似运行时契约，也应扩展 catalog。

编辑器行为：

1. 对运行时关键控件显示徽标，例如“系统关键”。
2. 默认禁止重命名和删除运行时关键控件。
3. 如果未来允许重命名，必须同步更新全部引用和动画元数据。
4. 编辑器应优先采用禁止重命名和删除的策略。

其他重要语义名称也需要谨慎处理：

1. Score 系列窗口的布局测试或文档可能依赖已记录的控件名。
2. 图片控件自动为 picking border 生成运行时名称，不提供独立的命名字段。
3. 未来任何 linked control、binding target、animation target 都必须纳入引用扫描和重命名重构逻辑。

## 4. 引用字段与重命名重构

部分 config 字段可能引用其他控件名。当前 Designer v3 内置控件没有 layout-item 级别的控件名引用字段；pick 呼吸边框的运行时名称由图片控件名自动生成。

未来可能出现的引用字段：

1. `LeftBindingTarget`
2. `TopBindingTarget`
3. `SizeBindingTarget`
4. `LinkedControlName`
5. `AnimationTargetName`

重命名流程：

1. 校验新名称。
2. 检查同 Canvas 内是否重复。
3. 更新设计项 `Name`。
4. 扫描同 Canvas 内全部设计项。
5. 将等于旧名称的引用字段更新为新名称。
6. 后续实现 undo stack 时记录重构操作。
7. 重新渲染 preview。
8. 标记布局为 dirty。

如果引用更新尚未实现，编辑器必须阻止被引用控件重命名，或至少显示阻断级警告，避免保存出断裂引用。

## 5. Window-centric 编辑模型

编辑器必须只暴露 Window。旧多 Canvas `WidgetsWindow` 已拆分为独立窗口：

| 窗口 | 内部 Canvas |
| --- | --- |
| `BpOverviewWindow` | 固定 `BaseCanvas` |
| `MapV2Window` | 固定 `BaseCanvas` |

其他 v3 layout window 示例：

1. `ScoreSurWindow`
2. `ScoreHunWindow`
3. `ScoreGlobalWindow`
4. `CutSceneWindow`
5. `GameDataWindow`
6. `BpWindow`

编辑器 UI 应包含：

1. Window selector。
2. 当前 layout path display。
3. dirty state indicator。

路径约定：

| 来源 | 路径 |
| --- | --- |
| 内置默认布局 | `Resources/FrontedLayouts/{WindowName}.json` |
| 用户自定义布局 | `%APPDATA%/neo-bpsys-wpf/FrontedLayouts/{WindowName}.json` |

加载优先级：

1. 用户自定义布局。
2. 内置默认布局。

当前加载 UI：编辑器按 Registry 列出可定制 v3 layout window，按选择项通过 `IFrontedLayoutService` 加载布局，转换为设计文档后执行校验，并把原始 config 渲染到编辑器自己的预览 Canvas。预览上方叠加 `InteractionLayer`，为每个设计项生成 editor-only 透明 hitbox，选中后显示名称、边框和 8 个缩放控制点。主区域为左侧控件列表、中间设计 surface、右侧选中/校验面板三列；左侧列表支持按控件名或 `ControlType` 筛选，窗口切换、布局重载成功时会清空筛选文本。列表按视觉 picking 顺序优先显示高 `ZIndex` 控件，同时允许直接选中被遮挡或低 ZIndex 控件。它不创建或复用真实 `BpWindow`、`ScoreWindow`、`CutSceneWindow` 等前台输出窗口。入口在前台窗口管理页，符合“前台窗口管理能力集中在 FrontManagePage”的后台 UI 归属。

## 6. 标题栏和窗口高度偏移

编辑器不能把真实前台窗口作为设计 surface。原生标题栏、窗口 chrome、`Viewbox` 包裹和窗口外框都会让坐标计算混入内容区以外的高度，造成纵向偏移 bug。

必须采用的设计：

1. 在编辑器窗口内部使用纯 `Canvas` 作为设计 surface。
2. 设计 surface 的 Canvas 尺寸必须精确等于 `FrontedCanvasConfig.CanvasWidth` 和 `FrontedCanvasConfig.CanvasHeight`。
3. 编辑器窗口标题栏不参与坐标计算。
4. 如果未来显示假窗口边框，它只能是视觉装饰。
5. 所有位置都基于内容 Canvas 坐标系，不基于 `Window.ActualHeight` 或窗口外边界。

只读预览按此规则设置 `PreviewCanvas.Width = config.CanvasWidth`、`PreviewCanvas.Height = config.CanvasHeight`，不读取真实前台窗口尺寸，因此不会把原生标题栏高度混入控件坐标。`PreviewCanvas` 和 `InteractionLayer` 放在同一个 `DesignSurfaceGrid` 内，二者尺寸都等于 `CanvasSettings.CanvasWidth` / `CanvasHeight`；外层 `PreviewZoomHost` 使用 `LayoutTransform` 绑定 `ZoomScale`。鼠标拖拽和缩放仍通过 `e.GetPosition(InteractionLayer)` 得到逻辑 Canvas 坐标，不乘除缩放比例；Fit 模式只根据 viewport/canvas 计算 `ZoomScale`，手动缩放也只改变 `ZoomScale`，不会改变写回的 `Left` / `Top` / `Width` / `Height`。编辑器窗口本身使用 WPF-UI `FluentWindow` 和项目既有 `CustomTitleBar`，标题栏在独立 Grid 行中，主题切换按钮隐藏，最小化、最大化和关闭按钮仍由 `CustomTitleBar` 处理。编辑器是可长期打开的非模态工具窗口，启动时不设置主窗口 `Owner`，也不默认最大化，避免 owned maximized window 触发主窗口最小化或任务栏联动。真实前台窗口宽高由 Window Settings 区域编辑并保存到 `WindowSettings`。

## 7. 设计 surface 架构

推荐结构：

```text
FrontedDesignerWindow
├── Toolbar
│   ├── Window selector
│   ├── Add Control FlyoutButton
│   ├── Save
│   ├── Reset to Built-in
│   ├── Zoom
│   └── Preview toggle
├── Main Area
│   ├── optional layer/control tree
│   ├── design surface
│   │   ├── PreviewCanvas
│   │   └── InteractionLayer
│   └── PropertyGrid
└── Status bar
```

`PreviewCanvas`：

1. 由现有 `IFrontedRenderer` 渲染。
2. 显示尽量接近运行时的真实视觉结果。
3. 使用设计时 preview data service 提供样例数据。
4. 放在 `ScrollViewer` 内的 `PreviewZoomHost` 中显示，默认 `Fit` 模式按 viewport 和 Canvas 尺寸计算 `ZoomScale`；手动缩放提供 25% 到 200% 的预设、放大、缩小和适应窗口按钮，并让 `ScrollViewer` 得到真实可滚动 extent。

`InteractionLayer`：

1. 包含透明 hitbox。
2. 包含选中框和 resize handles。
3. 处理鼠标和键盘编辑。
4. 不依赖生成控件的可见像素，也不要求生成控件可被点击。

## 8. 透明或空控件的命中测试

WPF 中没有可见内容的元素可能很难点击，甚至无法点击。常见例子：

1. 空 `Text`。
2. `Source` 为 `null` 的 `Image`。
3. 透明 `Border`。
4. 当前没有业务数据的业务控件。
5. 没有可见内容的 placeholder。

编辑器不能依赖 renderer 生成的控件本身进行选择。每个设计项都应在 `InteractionLayer` 创建透明 hitbox：

| 属性 | 规则 |
| --- | --- |
| 类型 | `Rectangle` 或 `Border` |
| `Background` | `Transparent` |
| `IsHitTestVisible` | `true` |
| 位置 | 来自 `Config.Left` / `Config.Top` |
| 尺寸 | 优先来自 `Config.Width` / `Config.Height` |

如果 `Width` / `Height` 为空，使用最小可编辑尺寸：

| 常量 | 推荐值 |
| --- | --- |
| `MinHitWidth` | `40` |
| `MinHitHeight` | `24` |

选中边框、控制点和标签应独立于实际控件内容显示。

交互只修改当前 `FrontedCanvasDesignDocument` 中的内存配置：鼠标拖拽修改 `Left` / `Top`，缩放控制点修改 `Width` / `Height`，左/上方向缩放会同步调整 `Left` / `Top`；方向键移动选中控件，默认步长为 `0.5`，并支持 `Shift=10`、`Ctrl=1`、`Alt=0.1`。所有坐标写回前按 `0.5` 吸附，缩放最小尺寸为 `1x1`。如果控件原本没有 `Width` / `Height`，开始缩放时优先使用渲染后 root element 的 `ActualWidth` / `ActualHeight` 初始化，仍不可用时回退到 `40x24`。每次编辑会标记 `CurrentDocument.IsDirty = true`、刷新校验消息并更新右侧选中信息；鼠标拖拽/缩放中直接移动或调整生成的 preview element 和选中 hitbox/adorner，不等到 mouse-up 才更新真实预览。mouse-up 后再执行校验并从当前内存文档重渲染一次，保证最终一致。

选择规则：

1. 单击透明 hitbox 或左侧列表项才改变选中控件。
2. 在未选中控件上按下后拖拽，不会切换焦点，也不会拖动该候选控件。
3. 拖拽只作用于当前已选中控件；选中控件的 editor hitbox、outline 和 handles 会提升到其他 hitbox 之上，方便拖动被高 ZIndex 控件覆盖的低 ZIndex 控件。
4. 该提升仅存在于 `InteractionLayer`，不会写回 JSON，也不会改变运行时 `ZIndex` 或 preview 渲染顺序。
5. 空白区域单击清除选择；空白区域拖拽不改变选择。
6. `Image` / `BorderedImage` 的 lock 和 picking border 是内部视觉层，不在普通控件列表显示，不生成普通 hitbox，也不能直接选中、拖拽或缩放；行为图通过稳定 part 引用定位这些内部视觉层。
7. `CurrentBanDisplay`、`BanSlotDisplay` 和 `PickingBorderOverlay` 已从 Designer v3 控件模型中移除；Ban 位必须使用 `Image` + `Lockable` overlay，pick 呼吸边框必须使用 `Image` / `BorderedImage` + `PickingBorderAvailable`。
8. overlay 不反向驱动目标图片。移动或缩放图片控件时，内部 overlay 自动跟随图片根元素位置和尺寸。
9. lock 与 picking border 各自提供“独立缩放”开关和 Stretch 枚举 ComboBox。开关开启时使用对应 overlay 的独立枚举值；关闭时禁用该 ComboBox，并跟随主图片的实际 Stretch。
10. Overlay 属性面板在“覆盖层”下分为“Ban 位锁”和“选择框”小节；小节内属性使用简短名称并缩进显示，两个小节的“启用”均使用 ToggleSwitch。
11. 交互优先级为：resize handle、视口平移、拖动已选控件、单击选择、空白点击清除。按住 Space 时左键拖拽用于平移，不会选择或移动控件；右键拖拽同样只平移视口。

图片控件选择/缩放语义：

1. `Image` 是通用图片控件，设计器透明 hitbox、选中框和 resize handles 对齐并修改根 `Grid` 的 `Left` / `Top` / `Width` / `Height`；主图和 overlay 都是该根元素的内部视觉层。
2. `BorderedImage` 是外层 `Border` + 内层 `Image`，Property Grid 把外框和内部图片属性分组显示；`Width` / `Height` 是外框尺寸，`ImageWidth` / `ImageHeight` 是内层图片尺寸。
3. 选中 `BorderedImage` 时，Property Grid 顶部提供互斥的 resize target 切换。`Border` 模式下 thumbs 调整外层 `Border`，`Image` 模式下 thumbs 调整内层 `ImageWidth` / `ImageHeight`；`Stretch`、`HorizontalAlignment`、`VerticalAlignment` 仍作用于内层 `Image`。
4. `BorderedImage` 的外层 `Border` 继续作为 resize target；内部图片层可以包含主图和 overlay。未配置 `ImageWidth` / `ImageHeight` 时，主图继续由 WPF 的 `Image` 测量和 `Stretch` 规则决定尺寸。
5. MapV1 已删除，不再为其保留 direct image 迁移规则。CutScene 的 Map、SurPick0-3、HunPick 来自旧 XAML 的 `Border -> Image`，因此使用 `BorderedImage`。SurPick0-3 显式保留 `ImageWidth=556.5`、`ImageHeight=null`，不要把该内层宽度按无效属性清理。

视口导航规则：

1. 可编辑预览结构是 `ScrollViewer -> PreviewWorkspace -> PreviewZoomHost -> DesignSurfaceGrid`，不再在编辑 surface 上使用 `Viewbox`。
2. `Fit` 模式根据 `ScrollViewer` viewport 和当前 Canvas 尺寸计算 `ZoomScale`，小 viewport 可低于 25%。
3. `Ctrl + mouse wheel` 缩放预览，进入手动缩放模式，范围保持在 25% 到 200%，并按鼠标位置近似保持缩放锚点。
4. 右键拖拽或 `Space + left mouse drag` 平移预览 viewport。
5. 平移只改变 `ScrollViewer.HorizontalOffset` / `VerticalOffset`，不改变 layout JSON 中的坐标，也不改变当前选中控件。

## 9. Placeholder 策略

placeholder 只属于编辑器预览，不写入 layout JSON。推荐实现设计时数据服务：

```csharp
public sealed class DesignerPreviewSharedDataService : ISharedDataService
{
    // 提供编辑器预览专用样例数据。
}
```

样例数据应覆盖：

1. 求生者队名和监管者队名。
2. 选手名和监管者名。
3. 角色图片、监管者图片、地图图片。
4. 当前 Ban 和全局 Ban 示例。
5. 比分文本。
6. 倒计时。
7. `GameProgress`、`MapName`、天赋、辅助特质等业务控件所需状态。

示例文本：

| 场景 | 示例 |
| --- | --- |
| 求生者队名 | `Survivor Team` |
| 监管者队名 | `Hunter Team` |
| 求生者选手 | `SurPlayer1`, `SurPlayer2`, `SurPlayer3`, `SurPlayer4` |
| 监管者选手 | `Hunter` |
| 无绑定和无静态文本的 `Text` | overlay 标签 `[Text]` |
| 无图片源的 `Image` | overlay 标签 `[Image]` |

`FrontedDesignerWindow` 渲染 preview 时通过 `FrontedRenderContext.SharedDataServiceOverride` 使用 `DesignerPreviewSharedDataService`，不会调用真实 `ISharedDataService.NewGame()`，也不会修改真实运行时 `CurrentGame`。真实前台窗口仍使用 DI 中的全局 `ISharedDataService`。当前 placeholder 值包括：`HomeTeam` / `AwayTeam`、应用 `Assets/icon.png` 队标、求生者 `幸运儿`、监管者 `厂长`、比分 0、选手 `Player 1` 到 `Player 5`、赛后数据 0、`GameProgress.Game1FirstHalf`、倒计时 `30`、禁用地图 `TheRedChurch`、选择地图 `EversleepingTown`、求生者天赋 `BorrowedTime` / `FlywheelEffect`、监管者天赋 `Detention` / `TrumpCard`、辅助特质 `Blink`，以及默认可见的当前/全局 Ban 位。

`InteractionLayer` 可以显示 fallback overlay 标签，帮助用户定位空控件：

```text
[SurPick0: Image]
[GameProgress: GameProgressText]
```

这些标签属于编辑器辅助视觉，不进入运行时 layout。

## 10. Add Control FlyoutButton

工具栏提供 Add Control 按钮和菜单添加控件，并按类别展示内置控件类型。

| 分组 | 控件 |
| --- | --- |
| Basic | `Text`, `LocalizedText`, `Image`, `BorderedImage` |
| Business | `MapNameText`, `GameProgressText`, `TalentTraitDisplay`, `GlobalScoreRow`, `MapV2Display` |
| Score/BP | `GlobalScoreRow`, `Image` |

`CurrentBanDisplay`、`BanSlotDisplay` 和 `PickingBorderOverlay` 已移除，不应出现在 Add Control 列表中。Ban 位和 pick 图应添加 `Image`，再通过 `BindingPath`、`Lockable`、`LockVisibilityBindingPath` 和 `PickingBorderAvailable` 配置。

`GlobalScoreRow` 是一个目的明确的复合控件，不应拆成一组无父级的顶层比分控件。编辑器中点击行主体会选中父级比分行，可移动或缩放整行；点击行内比分格 overlay 会选中该子格，同时父级仍作为当前顶层设计项。父级移动只修改 `GlobalScoreRow.Left/Top`，子格相对 `X/Y` 不变；子格移动或缩放只修改对应 `GlobalScoreCellConfig.X/Y/Width/Height`，并在合理范围内夹到父框内。子格属性面板显示 `Id`、`GameNumber`、`GameKind`、`HalfKind`、相对几何、`Visibility` 和样式覆盖项；字体、颜色、字号和 `ShowCampIcon` 留空表示继承父级。图层面板只显示顶层设计控件，`GlobalScoreRow.Cells` 不作为全局图层项。选中父行后，右侧属性面板显示专用 Score Cells 列表；点击列表项会选择对应内部比分格，但 `SelectedDesignItem` 仍保持父行。子格不能删除、复制、粘贴或拖入全局图层面板，也不能参与全局 ZIndex 拖拽、跨层投放或顶/底投放区。子格拖动、缩放和属性编辑都会进入 Designer undo/redo 栈，但这不是通用多选模型。

用户选择控件后：

1. 创建默认 config。
2. 生成唯一名称。
3. 放置在当前 viewport center 或 Canvas center。
4. 加入设计项集合。
5. 选中新控件。
6. 打开 property grid。
7. 标记布局 dirty。
8. 重新渲染 preview。

Add Control 只修改 `CurrentDocument` 的内存设计项集合；保存成功后才写入当前用户布局或活动布局包，不会覆盖内置 `Resources/FrontedLayouts`。`Image` 生成通用图片默认配置；`BorderedImage` 生成带外层容器、默认裁剪的图片框。需要 Ban 锁或 pick 呼吸边框时，在同一个图片控件上启用 overlay 字段。

工具栏提供 Delete Control。删除只影响当前内存设计文档；保存成功后才持久化。删除规则保持保守：

1. 未选中控件时不执行。
2. 运行时关键控件、不可选中控件或不可编辑控件拒绝删除。
3. 如果未来出现 incoming reference 字段，被引用控件删除时必须拒绝或同步清理引用。
4. 删除成功后从 `CurrentDocument.Controls` 移除设计项、清空选择、标记 dirty、刷新左侧列表和 Property Grid、重新校验并重渲染 preview。
5. 设计 surface 获得焦点时按 Delete 可删除选中控件；焦点在 `TextBox`、`ComboBox`、ColorPicker 等属性编辑器内时不会触发删除。

唯一名称示例：

1. `Text1`, `Text2`
2. `Image1`, `Image2`
3. `MapNameText1`

名称必须满足校验规则。默认 `Text` config 示例：

```json
{
  "ControlType": "Text",
  "Text": "Text",
  "Left": 100,
  "Top": 100,
  "Width": 160,
  "Height": 40,
  "FontSize": 24,
  "Color": "#FFFFFFFF"
}
```

## 11. 插件控件编辑器策略

插件控件 registry、descriptor API、运行时创建管线、Designer Add Control 插件 UI、插件属性元数据渲染、MissingPlugin 占位符和安装引导均按插件 descriptor 接入。插件控件的 `ControlType` 必须是：

```text
plugin:<PackageId>/<ControlTypeName>
```

Designer 和运行时读取布局时应把它识别为插件控件，而不是未知内置控件。layout JSON 的 `plugin:*` 控件会反序列化为 `PluginFrontedControlConfig` 并保留插件专属 JSON 属性；已安装插件注册 descriptor 后，runtime adapter 再转换为插件 typed config。内置控件仍使用 `Text`、`Image`、`BorderedImage` 等简单值；插件控件不能 shadow 内置 `ControlType`，也不能用本地化显示名作为保存值。

Add Control UI 应从插件 control descriptor 生成菜单项，显示插件提供的本地化名称、图标和描述，但保存到 JSON 的仍是完整 `plugin:<PackageId>/<ControlTypeName>`。如果插件未安装或版本不满足，Add Control 不应展示可添加入口，已有布局则走 MissingPlugin 占位符。

已有用户布局中的缺失插件控件：

1. 可以显示 `MissingPlugin` 占位符。
2. 占位符显示 `PackageId`、`ControlTypeName` 和完整 `ControlType`。
3. 左侧列表和校验面板应把它标为缺失插件控件。
4. 允许删除该控件。
5. 允许打开插件安装引导。
6. 在没有插件 descriptor 和 config 元数据时，不允许编辑插件专属属性。

`.bpui` 导入和普通编辑器占位符使用同一保留策略：缺失插件控件的原始配置保留在活动布局中，Designer preview 显示 MissingPlugin 占位符，runtime 前台跳过该控件并记录 warning。占位符本身只是编辑器视图，不会作为新的控件类型写入 JSON；安装插件并重启后，原始 `plugin:*` 配置可以重新 materialize 为插件 typed config。

插件属性第一版应由插件提供声明式 metadata，而不是任意 WPF PropertyGrid 控件：

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

这种方式能复用现有 Property Grid、Designer i18n、Binding Browser 类型过滤、校验和显式提交模型。没有 metadata 时可以反射公开 config 属性作为 fallback，但插件作者应优先提供明确元数据。

## 12. Property Grid

Property Grid 手写 WPF 实现，基于 `ItemsControl`，不使用 WinForms `PropertyGrid`。每行通过 `ContentControl` 和编辑器模板只创建当前需要的编辑器，避免切换选中控件时同时创建多套原生控件造成闪烁：

```text
PropertyGrid
└── ItemsControl<PropertyEditorItem>
```

`PropertyEditorItem` 应包含：

1. `DisplayName`
2. `PropertyName`
3. `PropertyType`
4. `Value`
5. `EditorKind`
6. `IsRequired`
7. `ValidationErrors`
8. 可选 description/help text

编辑器映射：

| 值类型或属性 | 编辑器 |
| --- | --- |
| `string` | WPF-UI `TextBox` |
| `int` / `double` / `float` | WPF-UI `NumberBox` |
| nullable number | `NumberBox` + clear button |
| `bool` | `ToggleSwitch` 或 `CheckBox` |
| enum | `ComboBox` |
| color string | `PortableColorPicker` + 文本 fallback，保存为 `#AARRGGBB` |
| `BindingPath` | 非 Text/LocalizedText 控件使用 `TextBox` + Binding Browser |
| `TextBinding` | Text/LocalizedText 专用模态编辑器，可增删、排序来源并编辑格式与连接分隔符 |
| image/resource path | `TextBox` + Resource Browser |
| `ControlType` | read-only |
| `Name` | 带校验的 `TextBox` |
| `ZIndex` | `NumberBox` |
| `FontFamily` | 可编辑字体 ComboBox |

字符串选项处理：

1. `HorizontalAlignment` 使用 `Left` / `Center` / `Right` / `Stretch`。
2. `VerticalAlignment` 使用 `Top` / `Center` / `Bottom` / `Stretch`。
3. `TextAlignment` 使用 `Left` / `Center` / `Right` / `Justify`。
4. `TextWrapping` 使用 `NoWrap` / `Wrap` / `WrapWithOverflow`。
5. `Stretch` 使用 `None` / `Fill` / `Uniform` / `UniformToFill`。
6. `FontWeight` 使用 `Normal` / `Bold` / `SemiBold` / `Light` / `Medium` / `ExtraBold`。

字符串选项使用 `FrontedPropertyEditorOption` 分离显示名和保存值。ComboBox 显示 `Designer.Option.{Property}.{Value}` 的本地化文本，例如 `HorizontalAlignment.Center` 在中文界面显示“居中”，但提交到 config 的值仍是原始 `"Center"`。这条规则同样适用于 `VerticalAlignment`、`TextAlignment`、`TextWrapping`、`Stretch` 和 `FontWeight`。不要把本地化显示文本写入 v3 JSON。

颜色处理：

1. 优先使用项目已有 ColorPicker。
2. 接受 `#RRGGBB` 和 `#AARRGGBB` 输入；RGB 按完全不透明处理，保存时统一规范化为 `#AARRGGBB`。
3. 识别属性名：`Color`, `Foreground`, `Background`, `FillColor`, `BorderColor`。
4. 无效颜色字符串不会让编辑器崩溃；ColorPicker 显示白色 fallback，并由属性行验证错误提示用户修正。

`FontFamily` 编辑器：

1. 使用与旧 `TextSettingsEditControl` 类似的可编辑 `ComboBox`，支持文本搜索。
2. 每个选项使用自身字体预览显示名。
3. 系统字体保存普通字体名字符串。
4. 内置字体保存现有 pack URI 约定，例如 `pack://application:,,,/Assets/Fonts/#Noto Sans`。
5. 预览内置字体时按运行时同样的 split 逻辑构造 `FontFamily(new Uri(pathBeforeHash), "./" + hashAndName)`，不要把 pack URI 原样传给 `new FontFamily(string)`。
6. 如果当前值不在选项中，ComboBox 允许手写并按原始字符串提交；无效字体字符串不能让属性网格崩溃。

属性行使用混合提交模型。普通 `Text` 字符串、数字和颜色会在输入变更后短延迟自动提交，ColorPicker 选色会立即写回 config；这些行不显示右侧确认按钮。`Name`、`BindingPath`、资源路径字符串、手写 `FontFamily`、`TextBinding`、以及 `Id` / `Key` / `Filter` / `Guid` / 内部目标名称这类容易影响引用或解析的字段仍使用显式提交：先写入 `FrontedPropertyEditorItem.EditText`，按 Enter、右侧 Check/Apply 按钮或浏览器选择后才提交。`Name` 和 `BindingPath` 不在 LostFocus 时自动提交，避免焦点移动和 Property Grid 重建时把未确认输入写回布局。提交失败时保留用户输入，设置 `HasEditError` / `EditError`，文本框显示红色边框，并在属性行下方显示验证消息；用户继续编辑或提交成功后错误状态清除。`Name` 仍遵守运行时关键名称只读、合法 WPF 名称、同 Canvas 唯一和被引用控件阻止重命名规则；成功重命名后刷新左侧列表、选中摘要、preview、hitbox/selection label 和属性行。

Text 和 LocalizedText 不再把基类 `BindingPath` 作为内容来源。它们的 Property Grid 显示专用 `TextBinding` 编辑按钮：弹窗内可添加、删除、上移、下移 source，手写 Path 或通过 Binding Browser 选择，并编辑 `StringFormat`、`JoinSeparator`、`NullText`、`FallbackText`。source 顺序对应复合格式的 `{0}`、`{1}`、`{2}`；格式为空时按分隔符连接。确认弹窗作为一个 undo/redo 步骤提交；空 Path、格式语法错误或超出 source 数量的占位符会阻止确认且保留输入。

其他 `BindingPath` 仍是可手写文本框，但旁边提供 Browse button。Binding Browser 由显式 root + 反射 catalog 驱动：`IFrontedBindingRootProvider` 注册 `CurrentGame`、`HomeTeam`、`AwayTeam`、`RemainingSeconds` 和 Ban 可用状态列表等根；标注 `[FrontedBindingObject]` 的 DTO 自动扫描 public readable instance properties；`[FrontedBindingIgnore]` 像 `JsonIgnore` 一样隐藏不适合布局绑定的公开属性；`[FrontedBindingCollection(FixedCount = ...)]` 为固定列表生成 `[0]`、`[1]` 等索引路径。catalog 扫描只看类型和属性元数据，不读取 `ISharedDataService` 当前值，不调用 getter，不创建新对局，也不枚举运行时集合。Binding Browser 按当前属性行的目标类型过滤候选路径：TextBinding source 显示字符串、数字、bool、enum、`DateTime` 和 `TimeSpan`，`Image` / `BorderedImage` 只显示 `ImageSource` / `BitmapSource` / `BitmapImage` 兼容值，`LockVisibilityBindingPath` 只显示 bool，`GameProgressText.BindingPath` 只显示 `GameProgress`，`MapNameText.BindingPath` 只显示 `Map` / `Map?`。不匹配的叶子节点会从树和搜索结果中隐藏，父节点只在仍有可用子节点时保留。选择结果只更新该行 `EditText`，不会立即写入 config，不会推入 Undo；用户后续按 Apply 或 Enter 后才走 `ApplyPropertyEdit`、校验、预览刷新和 Undo snapshot。

Binding Browser 的标题、搜索、按钮、空状态、期望类型和节点显示名可以本地化，但完整 `BindingPath` 始终作为原始路径在树、搜索结果或选中路径区域可见。选择后写回的仍是 `CurrentGame.SurTeam.Name` 这类原始路径，绝不写入“主队名称”等显示文本。

图片/资源路径字段旁提供 Resource Browser。当前资源来源包括内置运行时文件 `Resources/bpui`，返回值使用 resolver 约定的 `Resources/<fileName>`；也支持通过 “Browse file...” 选择 png/jpg/jpeg/webp/bmp 绝对路径。`Image` / `BorderedImage` 的静态图片选择写入 `ImagePath`，动态数据仍通过 `BindingPath` 和 Binding Browser 选择；两者同时存在时运行时以 `BindingPath` 为准。控件级 Resource Browser 选择外部文件仍只写入编辑缓冲。Canvas Settings 中提供 `CanvasWidth`、`CanvasHeight`、`BackgroundImage`、清除背景、浏览资源和选择本地图片；选择本地图片会复制到 editor-local resource store，layout JSON 写为 `bpui://local/...`。导出包时再复制进包资源并重写为 `bpui://{PackageId}/...`。Window Settings 中提供 `WindowSettings.WindowWidth` / `WindowHeight`、透明和背景色编辑，保存到同一个 window-centric layout JSON。

Resource Browser 的标题、搜索、按钮、空状态和来源/类型显示可本地化，但选中区域必须保留原始资源 URI 或文件路径。写回配置的仍是 `Resources/foo.png`、`bpui://...` 或绝对路径原值，不写入本地化显示文本。

`FontFamily` 行仍使用可编辑 ComboBox，但不再依赖 `SelectedValue` 双向绑定。下拉打开期间不会触发 LostFocus 提交或重建 Property Grid；用户从下拉中选择时写入对应 `FrontedFontFamilyOption.Value`，因此内置字体继续保存 `pack://application:,,,/Assets/Fonts/#...` 原值；用户手写自定义字体时按 Enter 或真正失焦提交 `ComboBox.Text`。下拉项继续使用各自的 `PreviewFontFamily` 显示，保持旧 `TextSettingsEditControl` 的字体预览语义。

属性编辑直接修改当前设计项的 `Config`。每次成功编辑后：

1. 校验设计项和 Canvas。
2. 重新渲染 preview。
3. 更新 hitbox 和 adorner。
4. 标记布局 dirty。

选中 `MapV2Display` 后，属性摘要区提供“编辑内部样式”入口。`MapV2Display` 作为父级复合控件保留整体位置和大小，内部编辑器按队伍名称、地图卡片、地图名称、阵营名称和选图边框五个固定部件筛选属性；从列表选中部件后，设计画布显示该部件的选择框和缩放手柄，可以直接拖动、缩放，也可以在属性面板编辑相对于父控件的 `X`、`Y`、`Width` 和 `Height`。这些相对布局保存在 `MapV2Display.InternalParts` 中；内部部件不是全局图层，不能脱离父级删除。属性摘要区同时提供“将样式应用到所有地图卡片”按钮。该操作把控件大小、内部部件布局、地图名/队名/阵营文字样式、地图卡片边框、选图边框样式和行为集复制到当前 Canvas 中其他 `MapV2Display`，并作为单个 Undo 步骤立即刷新预览；每张卡片自己的位置、`MapKey`、`ZIndex`、`Visibility` 和 `BindingPath` 保持不变，不会复制文字内容或绑定源。复制行为时会替换目标地图卡片原行为集，并把动画目标 GUID 与 `Event.MapKey` / `StartEvent.MapKey` / `StopEvent.MapKey` 过滤改写为目标卡片。

`MapV2Display` 的选图边框不是独立可编辑控件。内置 `MapV2Window.behaviors.json` 通过 `MapV2.PickingBorderStateChanged` 事件和 `part:{BehaviorGuid}:PickingBorder` 目标实现 MapBpV2 的旧呼吸灯语义；后台开关只发布业务状态，淡入、呼吸、淡出和隐藏由 v3 行为定义。

属性编辑提交必须只由用户交互触发。普通 ComboBox 在 `DropDownClosed` 后提交，CheckBox 在 Click 后提交，非显式提交文本/数字行在用户输入后短延迟提交，非显式提交颜色行在 ColorPicker 或 Hex 输入变化后提交，显式提交文本行仍在 Enter、Apply 或浏览器选择后提交，FontFamily ComboBox 按上述下拉/手写规则提交。属性网格重建、切换选中控件、绑定初始化和 layout pass 期间应抑制提交事件，避免 BpWindow / CutSceneWindow 中大量枚举或字符串选项行触发递归重建。失败的属性提交不应请求 preview render，也不应重建到丢失用户输入。

拖拽和缩放过程中的 live geometry edit 只更新内存 config、linked overlay、preview element、hitbox/adorner、选中控件几何摘要和 dirty 状态，不运行完整校验、不重建 Property Grid、不强制重渲染。鼠标释放或键盘微调等 commit 操作再执行一次校验、属性行刷新和最终 preview render。

的名称编辑采用保守策略：

1. `Name` 属于设计项和 JSON key，不属于 config object；不要给 `FrontedControlConfigBase` 或派生 config 添加重复 `Name`。
2. 运行时关键控件的 `Name` 只读。
3. 普通控件改名必须非空、匹配 `^[A-Za-z_][A-Za-z0-9_]*$`，并在当前 Canvas 内唯一。
4. 如果未来出现 layout-item 级别的控件名引用字段，改名前必须先实现引用感知重命名，避免静默断开引用。
5. 新布局优先使用 `Image` / `BorderedImage` 的 `Lockable`、`LockImagePath`、`UseIndependentLockStretch`、`LockStretch`、`LockVisibilityBindingPath`、`LockVisibleWhen`、`PickingBorderAvailable`、`PickingBorderImagePath`、`UseIndependentPickingBorderStretch` 和 `PickingBorderStretch` 配置内部覆盖层。覆盖层不是独立设计项，不生成普通 hitbox。
6. `CurrentBanDisplay`、`BanSlotDisplay` 和 `PickingBorderOverlay` 已不再支持，普通 Add Control 不应提供这些入口。

## 13. Binding Browser

已实现。任何可浏览的 `BindingPath` 属性都会显示：

1. `TextBox`
2. Browse button

Browse button 打开 `BindingBrowserDialog`。Dialog 使用 binding catalog 的 `TreeView`，根节点来自 `IFrontedBindingRootProvider`，子节点来自标注 DTO 的反射扫描：

```text
BindingRoot
├── CurrentGame : Game
│   ├── SurTeam
│   │   ├── Name
│   │   └── Logo
│   ├── HunTeam
│   ├── SurPlayerList
│   │   ├── [0]
│   │   ├── [1]
│   │   ├── [2]
│   │   └── [3]
│   ├── HunPlayer
│   ├── MatchScore
│   ├── PickedMap
│   └── PickedMapImage
├── RemainingSeconds
├── CanCurrentSurBannedList
├── CanCurrentHunBannedList
├── CanGlobalSurBannedList
└── CanGlobalHunBannedList
```

集合应为前台常用列表提供固定索引，避免用户手写 `[0]`：

| 集合 | 推荐索引 |
| --- | --- |
| `SurPlayerList` | `0..3` |
| `CurrentHunBannedList` | `0..1` |
| `CurrentSurBannedList` | `0..3` |
| `GlobalBannedHunList` | `0..2` 或按当前模型支持数量 |
| `GlobalBannedSurList` | `0..11` |

选择节点后写入路径，例如：

```text
CurrentGame.SurTeam.Name
CurrentGame.SurPlayerList[0].Member.Name
CurrentGame.SurPlayerList[0].PictureShownWithFullCharacter
CurrentGame.SurPlayerList[0].PictureShownHeader
CurrentGame.MatchScore.CurrentSurTeamMajorText
CanCurrentSurBannedList[0]
```

浏览器按属性行或专用编辑器携带的 `BindingTargetKind` 初始化过滤器。内置控件的推断规则为：`TextBinding.Sources` 使用文本过滤；`ImageFrontedControlConfig.BindingPath` 使用图片过滤；`GameProgressTextControlConfig.BindingPath` 使用 `GameProgress` 过滤；`MapNameTextControlConfig.BindingPath` 使用 `Map` 过滤；未知插件或未来控件默认使用 `Any`，避免宿主过早拒绝插件自定义路径。浏览器标题区会显示当前期望绑定类型，搜索结果遵守同一过滤器，例如文本模式搜索 `Logo` 不会返回队标图片，图片模式搜索 `Name` 不会返回字符串名称。

浏览器只更新属性行编辑缓冲；Apply/Enter 前，选中控件 config 仍保持旧值。取消浏览器不会修改 `EditText`。

### 13.1 注册绑定源

生产代码不再逐个手写普通 DTO 属性节点。新增绑定路径遵循以下契约：

1. 根对象只能通过 `IFrontedBindingRootProvider` 显式注册，例如 `CurrentGame : Game`、`RemainingSeconds : string`、`CanCurrentSurBannedList : ObservableCollection<bool>`。
2. 稳定 DTO 标注 `[FrontedBindingObject]` 后，其 public readable instance properties 会自动进入 catalog；需要排除时给属性加 `[FrontedBindingIgnore]`。
3. 单个属性需要自定义显示、描述或强制包含时使用 `[FrontedBindable]`。
4. 固定长度集合用 `[FrontedBindingCollection(FixedCount = ...)]` 声明索引数量；dictionary 只在声明 `KnownKeys` 时展开 key。
5. `IFrontedBindingCatalogContributor` 只用于虚拟节点、legacy alias、插件语义 key 等动态扩展，不应拿来重建整棵手写 DTO 树。

例如为 `MatchScoreState` 新增普通 public readable 属性并进入浏览器，应先确认 `MatchScoreState` 已标注 `[FrontedBindingObject]`，然后只添加模型属性；若该属性不适合前台布局绑定，则加 `[FrontedBindingIgnore]`。不要在 provider 中补一行 `Leaf(...)`。

## 14. Resource Browser

已实现控件级资源路径浏览。Resource Browser 面向图片和资源路径字段：

1. `BackgroundImage`
2. `ImagePath`
3. `LockImagePath`
4. `PickingBorderImagePath`
5. `BackgroundImage` 和未来其他 image fields

浏览器应支持：

1. 内置 `Resources/bpui` 路径。
2. 通过文件选择器选择绝对路径图片。
3. 缩略图预览；缩略图加载失败时显示为空，不阻断浏览器。

路径规则：

1. `Resources/foo.png` 解析到运行目录 `Resources/bpui/foo.png`。
2. 绝对路径在 resolver 支持时直接加载。
3. 不应让用户直接编辑 raw pack URI，除非目标字段明确需要 pack URI，如字体。
4. 内置 `Assets` 资源不是当前 resolver 的文件路径资源，暂不列入 Resource Browser。

## 15. 编辑操作

### Select

1. 点击透明 hitbox。
2. 或在 layer/control tree 中选择。
3. 更新 property grid 和状态栏。

### Drag

1. 根据鼠标移动更新 `Left` / `Top`。
2. 鼠标位置使用 `e.GetPosition(InteractionLayer)` 的逻辑 Canvas 坐标，通常不需要乘除 `ZoomScale`。
3. 如果启用吸附或微调，推荐 snap 到 `0.5`。

### Resize

1. 使用 8 个 handles。
2. 更新 `Width` / `Height`。
3. 左上、左、上等方向 resize 需要同时更新 `Left` / `Top`。
4. 遵守最小尺寸。

### Keyboard

1. 方向键移动选中控件 `0.5`。
2. 可选扩展：`Shift + Arrow = 10`。
3. 可选扩展：`Ctrl + Arrow = 1`。
4. 可选扩展：`Alt + Arrow = 0.1`。
5. 初始实现可以只支持方向键 `0.5`。

### Delete

1. 删除选中控件。
2. 默认阻止删除运行时关键控件。
3. 如果未来出现控件间引用字段，被其他控件引用的控件不能静默删除，必须阻止或确认并同步清理引用。

### Copy/Paste

已实现内部控件复制/粘贴。`Ctrl+C` 复制当前选中的普通可编辑控件，`Ctrl+V` 粘贴单个控件。该剪贴板只存在于编辑器 ViewModel 内，不使用系统剪贴板。运行时关键控件和不可选/不可编辑控件不能复制。粘贴时深拷贝 config，名称按尾部数字递增并避开冲突，`Left` / `Top` 偏移 `+10`。起，已安装插件控件的 typed config 和缺失插件控件的 `PluginFrontedControlConfig.ExtensionData` 都按同一 JSON 深拷贝路径保留；插件控件新增或粘贴后的默认名称使用 `ControlTypeName`，例如 `TeamCard1`，而不是完整 `plugin:...` 字符串。焦点位于 `TextBox`、可编辑 `ComboBox`、ColorPicker 文本区域等文本输入时，窗口不会拦截 `Ctrl+C` / `Ctrl+V`，保留普通文本复制粘贴。

行为面板另有独立的应用级行为剪贴板。用户可复制一个行为、快速粘贴到当前选中控件，或通过“复制行为到...”选择多个目标控件。粘贴会生成新的 `BehaviorId`，把指向源控件的动画目标改写为目标控件，并在源/目标语义索引都可推断时改写引导索引过滤器。`PickingBorder` 和 `LockOverlay` 等生成部件只有在目标控件启用了对应能力时才兼容；指向其他控件的外部引用不会被静默改写。

### Undo/Redo

修复后已提供基础内存 Undo/Redo。工具栏有 Undo / Redo 按钮，快捷键为 `Ctrl+Z`、`Ctrl+Y` 和 `Ctrl+Shift+Z`；焦点位于 `TextBox`、`ComboBox`、ColorPicker 等属性编辑器内时不抢编辑控件自身的撤销/重做。Undo/Redo 以完整 Canvas config JSON 快照实现，包含 root/BO5 与 `BoModeStates["Bo3"]`。新增控件、删除控件、成功属性提交、重命名、颜色/字体提交、键盘移动、鼠标拖拽/缩放提交、粘贴和“复制 BO5 布局到 BO3”都会进入 undo；切换窗口/Canvas 或 reload 会清空栈。历史上限仍是 50 步。

### BO3/BO5 Canvas States

Canvas 属性区可启用 BO3/BO5 状态。禁用时编辑 root/default state；启用后 BO5 仍编辑 root-level `BackgroundImage` / `RequiredPlugins` / `Controls`，BO3 编辑 `BoModeStates["Bo3"]`。状态切换不会创建 undo；“复制 BO5 布局到 BO3”会深拷贝背景、插件依赖、控件、ZIndex、Visibility 和插件 ExtensionData，并作为一个 undo 步骤记录。运行时根据 `ISharedDataService.IsBo3Mode` 选择 state。

### Visibility

所有 v3 控件都有通用 `Visibility`：`Visible` 正常显示，`Hidden` 不显示但保留 WPF 布局占位，`Collapsed` 不显示且折叠。Designer 不会因为 Hidden/Collapsed 删除控件，图层面板仍保留这些控件并允许重新选中恢复。

## 16. 保存和布局路径

内置布局是 source-controlled default layouts：

```text
neo-bpsys-wpf/Resources/FrontedLayouts/{WindowName}.json
```

用户自定义布局保存到 AppData：

```text
%APPDATA%/neo-bpsys-wpf/FrontedLayouts/{WindowName}.json
```

或使用项目现有 `AppConstants.FrontedLayoutsPath` 约定生成路径。

加载优先级：

1. 用户自定义布局。
2. 内置默认布局。

按钮：

1. Save
2. Reset to Built-in

保存前：

1. 执行完整 layout validation。
2. 存在 Error 时保存失败。
3. 只有 Warning 时可允许用户确认后保存。

设计器保存写入当前活动 layout package；当前活动项为内置方案时，保存会先复制出一个可写的用户布局方案并激活它。旧 `%APPDATA%/neo-bpsys-wpf/FrontedLayouts/{WindowName}/{CanvasName}.json` 目录（包含旧多 Canvas 层级）仅作为 legacy 迁移残留存在，不再通过设计器菜单暴露。Reset to Built-in 会删除用户窗口布局文件并重新加载内置布局，清空 undo/redo、选择和筛选。打开布局包目录统一在 `FrontManagePage` 的 `Layout Packages` 管理区执行。

`.bpui v3` package 导出/导入已放到 `FrontManagePage` 的 `Layout Packages` tab。导出会打开 manifest 对话框，并固定导出全部已迁移前台布局；导入会安装 v3 包并可立即激活。SettingPage 中现有 `.bpui` 导入导出是 legacy 流程，会覆盖全局 `Config.json`，不能作为 Designer v3 包管理入口。

`AllowsTransparency` 和 `BackgroundColor` 是 `WindowSettings`，不是普通控件属性，也不属于 `CanvasSettings`。`BackgroundColor` 使用 `#AARRGGBB` 并通过 ColorPicker 编辑；为空或非法时运行时回退为 Transparent 并记录 warning。由于 WPF 透明窗口行为必须在 source 初始化前应用，设计器保存 `AllowsTransparency` 后不弹出应用重启提示；如果目标前台窗口已经创建，`FrontedWindowService` 会静默重启该窗口实例。未创建过的窗口无需操作，下一次 `ShowWindow` 会按最新设置创建。

窗口切换、Reload、Reset to Built-in 和关闭编辑器时，如果当前文档 dirty，会通过 `MessageBoxHelper` 提示 Save / Discard / Cancel。Save 会先执行完整校验，存在 Error 时阻止保存并取消切换或关闭；Warning/Info 不阻止保存。关闭窗口的 dirty prompt 必须先在 `Closing` 中设置 `e.Cancel = true`，再通过 Dispatcher 异步显示本地化的宽版 helper 对话框；用户选择 Save 且保存成功或选择 Discard 后，设置强制关闭标记并再次调用 `Close()`。这样避免 WPF 在窗口已经进入 closing 状态时执行 `ShowDialog` / `Close` 触发异常。验证详情窗口是非模态子窗口，父编辑器关闭时只做受保护关闭，已关闭或正在关闭时不能让异常冒泡。

顶部工具栏从使用 `ScrollViewer + WrapPanel`，窗口选择器、Canvas 选择器、Add/Delete、Undo/Redo、保存/重置、reload/validate、缩放、吸附和 dirty/path 状态都允许在窄窗口下自动换行。长 layout path 只显示省略文本并通过 tooltip 查看完整路径，不能把工具栏撑出窗口右侧。

吸附行为从 开始改为默认关闭：`SnapEnabled` 是工具栏 ToggleSwitch 的持久开关，`IsShiftSnapActive` 只表示编辑 surface 中 Shift 当前按下，`EffectiveSnapEnabled = SnapEnabled || IsShiftSnapActive`。Shift 临时吸附只更新状态文字，例如“临时吸附”，不会修改 ToggleSwitch 的 `IsChecked`，避免 KeyDown/KeyUp 时反复刷新开关。鼠标拖拽和缩放在 `EffectiveSnapEnabled` 为 true 时优先尝试智能对齐：活动控件的 left/center/right 或 top/center/bottom 可吸附到画布边缘、画布中心线以及其他可选择、可编辑、非 linked overlay 控件的边缘/中心；未命中智能候选的轴继续按默认 10 px 网格吸附。关闭时仍按 0.5 坐标精度归一化。方向键在吸附开启时使用网格步长，普通模式保留 0.5/修饰键微调语义。智能吸附产生的辅助线只作为 `InteractionLayer` 上的临时 `Line` 元素显示，不写入 `PreviewCanvas`，也不会序列化到 v3 layout 或 `.bpui`。

起，编辑器 typed/pasted input 会按集中限制截断：搜索 128 字符，控件名 64，`BindingPath` 256，资源路径和 Canvas `BackgroundImage` 1024，`FontFamily` 256，静态 `Text` 512。发生截断时显示 `InputTruncated`。这些限制只适用于编辑器输入；外部导入 `.bpui`、layout JSON 或 manifest 时，超长字段会被拒绝，不会截断。Add Control 在当前 Canvas 已有 256 个控件时拒绝新增并显示 `ControlCountLimitReached`；保存仍由 validator 阻止硬限制错误。

## 17. 已实现功能总览

> 本编辑器的所有环节功能已实现完成。

已实现的核心能力：
- 设计期基础：`FrontedControlDesignItem` / `FrontedCanvasDesignDocument`、设计项与 dictionary 转换、`FrontedLayoutValidator`、名称校验、引用扫描、运行时关键名称 catalog、重复 JSON key 检测
- 编辑器 shell：`FrontedDesignerWindow`、窗口/Canvas 选择器、只读预览 surface、缩放控制、layout source 状态和 validator 消息面板
- 交互层：透明 hitbox、selection adorner、drag、resize、键盘微调、左侧控件列表/筛选、单击选择与拖拽分离
- Property Grid：Text/Number/Boolean/Enum/ColorPicker 编辑、对齐/换行/拉伸/字重 ComboBox、Name 编辑保护、显式提交模型
- Add Control 菜单：默认 config 工厂、唯一命名、视口中心放置、独立 placeholder preview data、FontFamily 字体 ComboBox
- Binding Browser 与 Resource Browser
- 保存/重置：用户 layout save/reset、validation-driven save、脏状态提示、吸附网格（默认关闭 + Shift 临时吸附 + 智能对齐）
- Undo/Redo、Copy/Paste（内部剪贴板）
- Canvas Properties GUI、`bpui://local` 资源规范化、Window Options
- `.bpui v3` 包导出/导入/安装/激活/删除
- Designer v3 显示层 i18n
- 插件控件支持：Add Control、声明式 Property Grid 元数据、缺失插件占位符
- 左侧图层面板：ZIndex 分组、同层排序、跨层移动、顶/底投放区
- Shape 控件：Rectangle/Polygon 的静态或绑定纯色、双颜色线性渐变和角度编辑；Polygon 选中后支持独立顶点手柄拖动及顶点增删

## 18. 非目标

当前编辑器仍不做：

1. 不实现 Save As。
2. 不在运行时硬编码内置 Pick 或 PickingBorder 动画。
3. 不迁移 `.bpui`。
4. 不移除旧 `config.json` 前台设置。
5. 不改变现有 v3 layout JSON schema。
6. 不把 `AllowTransparency` / `BackgroundColor` 当成控件属性；它们是窗口级选项。
