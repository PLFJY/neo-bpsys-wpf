# Fronted Designer v3 独立编辑器设计规格

本文记录 Designer v3 Phase 8A 的编辑器设计规格。Phase 8B 已落地设计期基础模型、配置转换、校验器、引用扫描器和运行时关键名称目录；Phase 8C 已新增独立 `FrontedDesignerWindow` shell、窗口/Canvas 选择器、只读预览渲染、缩放控制和校验面板；Phase 8D 已新增编辑器内存交互层、透明 hitbox、选择框、拖拽、缩放控制点和键盘微调，并在 owner validation 后补齐左侧控件列表、筛选和重叠控件选择语义。编辑器入口位于 `FrontManagePage`，不是 `SettingPage`。当前仍不实现完整 Property Grid、Add Control、Binding Browser、Resource Browser、用户布局保存、`.bpui` 迁移，也不移除旧 `config.json` 前台设置。

独立编辑器面向 v3 JSON layout 文件。它是后台侧的独立编辑窗口，不直接在真实前台窗口上编辑；真实前台窗口仍用于 OBS 捕获和运行时输出。编辑器必须同时支持单 Canvas 窗口和多 Canvas 窗口，并保持与现有 v3 renderer、生成控件名、`AnimationService`、业务控件和 JSON 格式兼容。

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
2. `FrontedRenderer` 遍历 `config.Controls`，把 `(name, controlConfig)` 传给 `IFrontedControl.Create(...)`。
3. 内置控件把 root `FrameworkElement.Name` 设置为该 `name`，例如 `FrontedControlFactoryHelper.CreateOuterBorder(name, config)`。
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

重复 JSON key 不能等到反序列化成 dictionary 后再处理，因为普通 dictionary 会丢失重复项。v3 converter 应在读取 raw root object 阶段发现重复 root-level property 并抛出布局配置异常，编辑器后续可把该异常转成校验友好的错误提示。

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

这些名称被 `AnimationService` 通过 `window.FindName(...)` 查找，用于 pick 图淡入淡出和 picking border 呼吸动画。除非未来改为 metadata-based 动画目标查找，否则这些名称必须保持稳定。

Phase 8B 起，这些运行时关键名称集中在 `FrontedLayoutRuntimeContractCatalog` 中。编辑器、校验器和后续删除/重命名保护都应通过该 catalog 查询，不应在 UI 层或多个服务中重复硬编码同一批名称。后续其他窗口如果出现类似运行时契约，也应扩展 catalog。

编辑器行为：

1. 对运行时关键控件显示徽标，例如“系统关键”。
2. 默认禁止重命名和删除运行时关键控件。
3. 如果未来允许重命名，必须同步更新全部引用和动画元数据。
4. Phase 8 编辑器初始实现应优先采用禁止重命名和删除的策略。

其他重要语义名称也需要谨慎处理：

1. Score 系列窗口的布局测试或文档可能依赖已记录的控件名。
2. `PickingBorderOverlay.TargetControlName` 引用 pick 图片控件名。
3. 未来任何 linked control、binding target、animation target 都必须纳入引用扫描和重命名重构逻辑。

## 4. 引用字段与重命名重构

部分 config 字段引用其他控件名。当前已知引用字段：

| Config | 字段 |
| --- | --- |
| `PickingBorderOverlayControlConfig` | `TargetControlName` |

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

## 5. 多 Canvas 编辑模型

编辑器必须支持多 Canvas 前台窗口。当前示例：

| 窗口 | Canvas |
| --- | --- |
| `WidgetsWindow` | `MapBpCanvas` |
| `WidgetsWindow` | `BpOverViewCanvas` |
| `WidgetsWindow` | `MapV2Canvas` |

单 Canvas 窗口示例：

1. `ScoreSurWindow` / `BaseCanvas`
2. `ScoreHunWindow` / `BaseCanvas`
3. `ScoreGlobalWindow` / `BaseCanvas`
4. `CutSceneWindow` / `BaseCanvas`
5. `GameDataWindow` / `BaseCanvas`
6. `BpWindow` / `BaseCanvas`

编辑器 UI 应包含：

1. Window selector。
2. Canvas selector。
3. 当前 layout path display。
4. dirty state indicator。

路径约定：

| 来源 | 路径 |
| --- | --- |
| 内置默认布局 | `Resources/FrontedLayouts/{WindowName}/{CanvasName}.json` |
| 用户自定义布局 | `%APPDATA%/neo-bpsys-wpf/FrontedLayouts/{WindowName}/{CanvasName}.json` |

加载优先级：

1. 用户自定义布局。
2. 内置默认布局。

Phase 8C 已实现基础加载 UI：编辑器使用固定目录列出已迁移的内置 v3 窗口和 Canvas，按选择项通过 `IFrontedLayoutService` 加载布局，转换为 `FrontedCanvasDesignDocument` 后执行校验，并把原始 config 渲染到编辑器自己的预览 Canvas。Phase 8D 在预览上方叠加 `InteractionLayer`，为每个设计项生成 editor-only 透明 hitbox，选中后显示名称、边框和 8 个缩放控制点。owner validation 后，主区域改为左侧控件列表、中间设计 surface、右侧选中/校验面板三列；左侧列表支持按控件名或 `ControlType` 筛选，窗口或 Canvas 切换、布局重载成功时会清空筛选文本。列表按视觉 picking 顺序优先显示高 `ZIndex` 控件，同时允许直接选中被遮挡或低 ZIndex 控件。它不创建或复用真实 `BpWindow`、`ScoreWindow`、`CutSceneWindow` 等前台输出窗口。入口在前台窗口管理页，符合“前台窗口/Canvas 管理能力集中在 FrontManagePage”的后台 UI 归属。

## 6. 标题栏和窗口高度偏移

编辑器不能把真实前台窗口作为设计 surface。原生标题栏、窗口 chrome、`Viewbox` 包裹和窗口外框都会让坐标计算混入内容区以外的高度，造成纵向偏移 bug。

必须采用的设计：

1. 在编辑器窗口内部使用纯 `Canvas` 作为设计 surface。
2. 设计 surface 的 Canvas 尺寸必须精确等于 `FrontedCanvasConfig.CanvasWidth` 和 `FrontedCanvasConfig.CanvasHeight`。
3. 编辑器窗口标题栏不参与坐标计算。
4. 如果未来显示假窗口边框，它只能是视觉装饰。
5. 所有位置都基于内容 Canvas 坐标系，不基于 `Window.ActualHeight` 或窗口外边界。

Phase 8C 的只读预览已经按此规则设置 `PreviewCanvas.Width = config.CanvasWidth`、`PreviewCanvas.Height = config.CanvasHeight`，不读取真实前台窗口尺寸，因此不会把原生标题栏高度混入控件坐标。Phase 8D 的 `PreviewCanvas` 和 `InteractionLayer` 放在同一个 `Viewbox` 内，二者尺寸都等于 `FrontedCanvasConfig.CanvasWidth` / `CanvasHeight`，所以鼠标拖拽和缩放得到的是逻辑 Canvas 坐标；ViewBox fit 或手动缩放只影响显示比例，不改变写回的 `Left` / `Top` / `Width` / `Height`。编辑器窗口本身使用 WPF-UI `FluentWindow` 和项目既有 `CustomTitleBar`，标题栏在独立 Grid 行中，主题切换按钮隐藏，最小化、最大化和关闭按钮仍由 `CustomTitleBar` 处理。

## 7. 设计 surface 架构

推荐结构：

```text
FrontedDesignerWindow
├── Toolbar
│   ├── Window selector
│   ├── Canvas selector
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
4. 放在 `ViewBox` 中显示，默认 `Fit` 模式让完整 Canvas 适配预览区域；手动缩放提供 25% 到 200% 的预设、放大、缩小和适应窗口按钮。

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
5. 初始隐藏的 `PickingBorderOverlay`。
6. 没有可见内容的 placeholder。

编辑器不能依赖 renderer 生成的控件本身进行选择。Phase 8D 已为每个设计项在 `InteractionLayer` 创建透明 hitbox：

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

Phase 8D 的交互只修改当前 `FrontedCanvasDesignDocument` 中的内存配置：鼠标拖拽修改 `Left` / `Top`，缩放控制点修改 `Width` / `Height`，左/上方向缩放会同步调整 `Left` / `Top`；方向键移动选中控件，默认步长为 `0.5`，并支持 `Shift=10`、`Ctrl=1`、`Alt=0.1`。所有坐标写回前按 `0.5` 吸附，缩放最小尺寸为 `1x1`。如果控件原本没有 `Width` / `Height`，开始缩放时优先使用渲染后 root element 的 `ActualWidth` / `ActualHeight` 初始化，仍不可用时回退到 `40x24`。每次编辑会标记 `CurrentDocument.IsDirty = true`、刷新校验消息并更新右侧选中信息；鼠标拖拽/缩放中直接移动或调整生成的 preview element 和选中 hitbox/adorner，不等到 mouse-up 才更新真实预览。mouse-up 后再执行校验并从当前内存文档重渲染一次，保证最终一致。由于 Phase 8D 仍没有保存按钮，dirty 状态只作为未保存视觉提示。

Phase 8D owner validation 后的选择规则：

1. 单击透明 hitbox 或左侧列表项才改变选中控件。
2. 在未选中控件上按下后拖拽，不会切换焦点，也不会拖动该候选控件。
3. 拖拽只作用于当前已选中控件；选中控件的 editor hitbox、outline 和 handles 会提升到其他 hitbox 之上，方便拖动被高 ZIndex 控件覆盖的低 ZIndex 控件。
4. 该提升仅存在于 `InteractionLayer`，不会写回 JSON，也不会改变运行时 `ZIndex` 或 preview 渲染顺序。
5. 空白区域单击清除选择；空白区域拖拽不改变选择。

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

`InteractionLayer` 可以显示 fallback overlay 标签，帮助用户定位空控件：

```text
[SurPick0: Image]
[GameProgress: GameProgressText]
```

这些标签属于编辑器辅助视觉，不进入运行时 layout。

## 10. Add Control FlyoutButton

工具栏应提供 WPF-UI `FlyoutButton` 添加控件，并按类别展示内置控件类型。

| 分组 | 控件 |
| --- | --- |
| Basic | `Text`, `LocalizedText`, `Image` |
| Business | `MapNameText`, `GameProgressText`, `TalentTraitDisplay`, `GlobalScoreRow`, `CurrentBanDisplay`, `BanSlotDisplay`, `MapV2Display`, `PickingBorderOverlay` |
| Score/BP | `GlobalScoreRow`, `BanSlotDisplay`, `PickingBorderOverlay` |

用户选择控件后：

1. 创建默认 config。
2. 生成唯一名称。
3. 放置在当前 viewport center 或 Canvas center。
4. 加入设计项集合。
5. 选中新控件。
6. 打开 property grid。
7. 标记布局 dirty。
8. 重新渲染 preview。

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

## 11. Property Grid

Property Grid 应手写 WPF 实现，优先基于 `ItemsControl`，不使用 WinForms `PropertyGrid`：

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
| color string | `ColorPicker` |
| `BindingPath` | `TextBox` + Browse button |
| image/resource path | `TextBox` + Resource browse button |
| `ControlType` | read-only |
| `Name` | 带校验的 `TextBox` |
| `ZIndex` | `NumberBox` |
| `FontFamily` | 初期 `TextBox`，后续可做 font picker |

颜色处理：

1. 优先使用项目已有 ColorPicker。
2. 保存为 `#AARRGGBB`。
3. 识别属性名：`Color`, `Foreground`, `Background`, `FillColor`, `BorderColor`。

属性编辑应直接修改当前设计项的 `Config`。每次编辑后：

1. 校验设计项和 Canvas。
2. 重新渲染 preview。
3. 更新 hitbox 和 adorner。
4. 标记布局 dirty。

## 12. Binding Browser

任何 `BindingPath` 属性都应显示：

1. `TextBox`
2. Browse button

Browse button 打开 `BindingBrowserDialog`。Dialog 使用基于 `ISharedDataService` public properties 的 `TreeView`：

```text
ISharedDataService
├── CurrentGame
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
└── CanGlobalSurBannedList
```

集合应为前台常用列表提供固定索引，避免用户手写 `[0]`：

| 集合 | 推荐索引 |
| --- | --- |
| `SurPlayerList` | `0..3` |
| `CurrentHunBannedList` | `0..1` |
| `CurrentSurBannedList` | `0..3` |
| `GlobalBannedHunList` | `0..2` 或按当前模型支持数量 |
| `GlobalBannedSurList` | `0..11` |

选择叶子节点后写入路径，例如：

```text
CurrentGame.SurTeam.Name
CurrentGame.SurPlayerList[0].Member.Name
CurrentGame.MatchScore.CurrentSurTeamMajorText
```

## 13. Resource Browser

Resource Browser 面向图片和资源路径字段：

1. `BackgroundImage`
2. 图片路径字段
3. `PickingBorderOverlay.BorderImagePath`
4. `BanSlotDisplay.LockImageSource`
5. 未来其他 image fields

浏览器应支持：

1. 内置 `Resources/bpui` 路径。
2. 用户导入的自定义图片路径。
3. resolver 支持时的绝对路径。
4. 后续可增加缩略图预览。

路径规则：

1. `Resources/foo.png` 解析到运行目录 `Resources/bpui/foo.png`。
2. 绝对路径在 resolver 支持时直接加载。
3. 不应让用户直接编辑 raw pack URI，除非目标字段明确需要 pack URI，如字体。

## 14. 编辑操作

### Select

1. 点击透明 hitbox。
2. 或在 layer/control tree 中选择。
3. 更新 property grid 和状态栏。

### Drag

1. 根据鼠标移动更新 `Left` / `Top`。
2. 计算时除以 zoom scale。
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
3. 被其他控件引用的控件不能静默删除，必须阻止或确认并同步清理引用。

### Copy/Paste

可作为未来能力。如果实现，粘贴时必须生成新的唯一名称。

### Undo/Redo

作为未来阶段能力记录。除非项目 owner 明确要求，初始编辑器实现不强制包含 undo/redo。

## 15. 保存和布局路径

内置布局是 source-controlled default layouts：

```text
neo-bpsys-wpf/Resources/FrontedLayouts/{WindowName}/{CanvasName}.json
```

用户自定义布局保存到 AppData：

```text
%APPDATA%/neo-bpsys-wpf/FrontedLayouts/{WindowName}/{CanvasName}.json
```

或使用项目现有 `AppConstants.FrontedLayoutsPath` 约定生成路径。

加载优先级：

1. 用户自定义布局。
2. 内置默认布局。

按钮：

1. Save
2. Save As
3. Reset to Built-in
4. Open Layout Folder

保存前：

1. 执行完整 layout validation。
2. 存在 Error 时保存失败。
3. 只有 Warning 时可允许用户确认后保存。

Phase 8A 不实现保存 UI，只记录未来阶段行为。

## 16. 未来实现阶段建议

| 阶段 | 范围 |
| --- | --- |
| Phase 8B | 已实现：`FrontedControlDesignItem` / `FrontedCanvasDesignDocument`、设计项与 dictionary 转换、`FrontedLayoutValidator`、名称校验、引用扫描、运行时关键名称 catalog、重复 JSON key 检测 |
| Phase 8C | 已实现：`FrontedDesignerWindow` shell、window/canvas selector、只读 ViewBox preview surface、缩放控制、layout source 状态和 validator 消息面板 |
| Phase 8D | 已实现：interaction layer、透明 hitbox、selection adorner、drag、resize、键盘微调；owner validation 后补齐左侧控件列表/筛选、单击选择与拖拽分离、选中 hitbox editor-only 提层、拖拽/缩放时 preview live update、无显式尺寸时使用渲染实际尺寸；仍只改内存，不保存用户布局 |
| Phase 8E | PropertyGrid、基础编辑器、ColorPicker 支持 |
| Phase 8F | Add Control FlyoutButton、默认 config factory、设计时 placeholder data |
| Phase 8G | Binding Browser、Resource Browser |
| Phase 8H | 用户 layout save/reset/load priority、validation-driven save |

## 17. 非目标

Phase 8C 不做：

1. 不实现 drag/resize、透明 hitbox、selection adorner 或键盘微调。
2. 不实现 Property Grid、Add Control、Binding Browser 或 Resource Browser。
3. 不保存用户布局，不实现 reset/save-as/open-folder。
4. 不改变 `FrontedRenderer` 行为。
5. 不修改 `AnimationService` 查找逻辑。
6. 不迁移 `.bpui`。
7. 不移除旧 `config.json` 前台设置。
8. 不改变现有 v3 layout JSON。
