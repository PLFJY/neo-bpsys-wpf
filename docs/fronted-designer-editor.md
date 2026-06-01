# Fronted Designer v3 独立编辑器设计规格

本文记录 Designer v3 Phase 8A 的编辑器设计规格。Phase 8B 已落地设计期基础模型、配置转换、校验器、引用扫描器和运行时关键名称目录；Phase 8C 已新增独立 `FrontedDesignerWindow` shell、窗口/Canvas 选择器、只读预览渲染、缩放控制和校验面板；Phase 8D 已新增编辑器内存交互层、透明 hitbox、选择框、拖拽、缩放控制点和键盘微调，并在 owner validation 后补齐左侧控件列表、筛选和重叠控件选择语义。Phase 8D zoom/pan 修正后，编辑 surface 不再使用 `Viewbox` 控制 Fit/手动缩放，而是使用 `ScrollViewer + PreviewZoomHost + LayoutTransform`，所有缩放统一由 `ZoomScale` 驱动。Phase 8E 已新增基础 Property Grid，可编辑选中控件的内存设计项并即时重渲染预览；owner validation 后改为按编辑器类型实例化单一模板，提交事件在属性网格重建期间被抑制，验证详情移入底部状态区弹窗，颜色字段使用 ColorPicker。Phase 8F 已新增 Add Control 菜单、默认 config 工厂、唯一命名和 `FontFamily` 字体 ComboBox；owner validation 修正后补齐 Delete Control、文本属性显式提交、失败编辑红框保留输入、字体下拉打开/提交时机修复和右侧 Property Grid 可拖拽宽度。Phase 8F foundation 修复后，设计器预览使用独立 placeholder shared data service，颜色选择只同步 Hex 缓冲并由 Apply/Enter 显式提交，左侧列表右键和 Property Grid 底部都可删除控件，并新增内存 Undo/Redo 按钮和快捷键。Phase 8G 已新增 Binding Browser 和 Resource Browser；浏览器选择只写入属性行 `EditText` 缓冲，仍需 Apply 或 Enter 才提交到内存设计文档。Phase 8H 已新增保存用户布局、重置为内置、打开布局目录、运行时用户布局优先级、脏状态提示和吸附开关。Phase 9A 已定义 `.bpui v3` 布局包标准，见 [bpui-package-v3.md](bpui-package-v3.md)。Phase 9B.1 已新增 `FrontManagePage` Layout Packages 管理器骨架，可列出系统内置包、已安装包和活动包状态；同时清理设计器重复入口，Delete 保留在 Edit menu 和左侧列表右键菜单，右侧 Property Grid 底部 Delete 已移除。编辑器入口位于 `FrontManagePage`，不是 `SettingPage`。Phase 9C 已实现 v3 `.bpui` 导出和资源打包；Phase 12 已新增 Designer v3 显示层 i18n，属性名、控件类型、窗口/Canvas 选择器、Binding Browser 常用节点和 ComboBox 选项可本地化，但 layout schema 与保存值不变。

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

Phase 8C 的只读预览已经按此规则设置 `PreviewCanvas.Width = config.CanvasWidth`、`PreviewCanvas.Height = config.CanvasHeight`，不读取真实前台窗口尺寸，因此不会把原生标题栏高度混入控件坐标。Phase 8D 的 `PreviewCanvas` 和 `InteractionLayer` 放在同一个 `DesignSurfaceGrid` 内，二者尺寸都等于 `FrontedCanvasConfig.CanvasWidth` / `CanvasHeight`；外层 `PreviewZoomHost` 使用 `LayoutTransform` 绑定 `ZoomScale`。鼠标拖拽和缩放仍通过 `e.GetPosition(InteractionLayer)` 得到逻辑 Canvas 坐标，不乘除缩放比例；Fit 模式只根据 viewport/canvas 计算 `ZoomScale`，手动缩放也只改变 `ZoomScale`，不会改变写回的 `Left` / `Top` / `Width` / `Height`。编辑器窗口本身使用 WPF-UI `FluentWindow` 和项目既有 `CustomTitleBar`，标题栏在独立 Grid 行中，主题切换按钮隐藏，最小化、最大化和关闭按钮仍由 `CustomTitleBar` 处理。

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
6. `PickingBorderOverlay` 是跟随 `TargetControlName` 的 linked runtime overlay，不在普通控件列表显示，不生成普通 hitbox，也不能直接选中、拖拽或缩放；它仍保留在设计文档和 JSON 中，并继续作为 `AnimationService` 的运行时命名目标渲染和校验。
7. `PickingBorderOverlay` 的 `Left` / `Top` / `Width` / `Height` 由目标控件驱动。目标控件移动或缩放时，编辑器会同步更新 overlay config，拖拽/缩放过程中预览也会实时跟随；overlay 不反向驱动目标控件。
8. `BanSlotDisplay` 仍是可选中、可编辑的单个设计项；ban lock overlay 是其内部视觉层，不会拆成独立设计项，也不会单独出现在列表或 hitbox 中。
9. 交互优先级为：resize handle、视口平移、拖动已选控件、单击选择、空白点击清除。按住 Space 时左键拖拽用于平移，不会选择或移动控件；右键拖拽同样只平移视口。

图片控件选择/缩放语义：

1. `Image` 是 direct image 控件，设计器透明 hitbox、选中框和 resize handles 对齐并修改根 `Image` 的 `Left` / `Top` / `Width` / `Height`。
2. `BorderedImage` 是外层 `Border` + 内层 `Image`，Property Grid 把外框和内部图片属性分组显示；`Width` / `Height` 是外框尺寸，`ImageWidth` / `ImageHeight` 是内层图片尺寸。
3. 选中 `BorderedImage` 时，Property Grid 顶部提供互斥的 resize target 切换。`Border` 模式下 thumbs 调整外层 `Border`，`Image` 模式下 thumbs 调整内层 `ImageWidth` / `ImageHeight`；`Stretch`、`HorizontalAlignment`、`VerticalAlignment` 仍作用于内层 `Image`。
4. `BorderedImage` 的运行时树保持旧 XAML 的 `Border -> Image` 语义，不插入中间 layout host；未配置 `ImageWidth` / `ImageHeight` 时，内层图片继续由 WPF 的 `Image` 测量和 `Stretch` 规则决定尺寸。
5. MapV1 的 `PickedMap` / `BannedMap` 使用 direct `Image`，保持旧 XAML `ui:Image` 的填充与裁剪行为；CutScene 的 Map、SurPick0-3、HunPick 来自 `v2.1.1+af0a4be` 旧 XAML 的 `Border -> Image`，因此使用 `BorderedImage`。

Phase 8D 视口导航修正后：

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

Phase 8F foundation 修复后，`FrontedDesignerWindow` 渲染 preview 时通过 `FrontedRenderContext.SharedDataServiceOverride` 使用 `DesignerPreviewSharedDataService`，不会调用真实 `ISharedDataService.NewGame()`，也不会修改真实运行时 `CurrentGame`。真实前台窗口仍使用 DI 中的全局 `ISharedDataService`。当前 placeholder 值包括：`HomeTeam` / `AwayTeam`、应用 `Assets/icon.png` 队标、求生者 `幸运儿`、监管者 `厂长`、比分 0、选手 `Player 1` 到 `Player 5`、赛后数据 0、`GameProgress.Game1FirstHalf`、倒计时 `30`、禁用地图 `TheRedChurch`、选择地图 `EversleepingTown`、求生者天赋 `BorrowedTime` / `FlywheelEffect`、监管者天赋 `Detention` / `TrumpCard`、辅助特质 `Blink`，以及默认可见的当前/全局 Ban 位。

`InteractionLayer` 可以显示 fallback overlay 标签，帮助用户定位空控件：

```text
[SurPick0: Image]
[GameProgress: GameProgressText]
```

这些标签属于编辑器辅助视觉，不进入运行时 layout。

## 10. Add Control FlyoutButton

Phase 8F 起，工具栏提供 Add Control 按钮和菜单添加控件，并按类别展示内置控件类型。

| 分组 | 控件 |
| --- | --- |
| Basic | `Text`, `LocalizedText`, `Image`, `BorderedImage` |
| Business | `MapNameText`, `GameProgressText`, `TalentTraitDisplay`, `GlobalScoreRow`, `CurrentBanDisplay`, `BanSlotDisplay`, `MapV2Display` |
| Score/BP | `GlobalScoreRow`, `BanSlotDisplay` |

`PickingBorderOverlay` 不应出现在普通 Add Control 列表中。它是跟随 pick 目标控件的 linked runtime overlay，承担 `AnimationService` 的独立命名目标职责，应由宿主控件或未来高级动作创建和维护，而不是作为普通控件直接添加、选中或编辑。

用户选择控件后：

1. 创建默认 config。
2. 生成唯一名称。
3. 放置在当前 viewport center 或 Canvas center。
4. 加入设计项集合。
5. 选中新控件。
6. 打开 property grid。
7. 标记布局 dirty。
8. 重新渲染 preview。

当前 Add Control 只修改 `CurrentDocument` 的内存设计项集合，不写入 AppData 或内置 `Resources/FrontedLayouts`。重新打开或 reload 布局仍按现有加载优先级恢复用户/内置布局，直到后续保存阶段实现。`Image` 生成 direct image 默认配置；`BorderedImage` 生成带外层容器、默认裁剪的图片框。`PickingBorderOverlay` 不出现在普通 Add Control 菜单中；它仍是跟随 pick 目标控件的 linked runtime overlay，由宿主布局或未来高级动作维护。

Phase 8F owner validation 后，工具栏新增 Delete Control。删除只影响当前内存设计文档；重新加载布局仍会恢复内置或用户布局，直到保存阶段实现。删除规则保持保守：

1. 未选中控件时不执行。
2. 运行时关键控件、不可选中控件或不可编辑控件拒绝删除。
3. 存在 incoming reference 的控件拒绝删除，例如 `PickingBorderOverlay.TargetControlName` 指向该控件时不能删除。
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

## 11. Property Grid

Phase 8E 已实现基础 Property Grid。Property Grid 手写 WPF 实现，基于 `ItemsControl`，不使用 WinForms `PropertyGrid`。owner validation 后每行通过 `ContentControl` 和编辑器模板只创建当前需要的编辑器，避免切换选中控件时同时创建多套原生控件造成闪烁：

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
| `BindingPath` | Phase 8E 为普通 `TextBox`；Binding Browser 留到 Phase 8G |
| image/resource path | Phase 8E 为普通 `TextBox`；Resource Browser 留到 Phase 8G |
| `ControlType` | read-only |
| `Name` | 带校验的 `TextBox` |
| `ZIndex` | `NumberBox` |
| `FontFamily` | Phase 8F 起使用可编辑字体 ComboBox |

字符串选项处理：

1. `HorizontalAlignment` 使用 `Left` / `Center` / `Right` / `Stretch`。
2. `VerticalAlignment` 使用 `Top` / `Center` / `Bottom` / `Stretch`。
3. `TextAlignment` 使用 `Left` / `Center` / `Right` / `Justify`。
4. `TextWrapping` 使用 `NoWrap` / `Wrap` / `WrapWithOverflow`。
5. `Stretch` 使用 `None` / `Fill` / `Uniform` / `UniformToFill`。
6. `FontWeight` 使用 `Normal` / `Bold` / `SemiBold` / `Light` / `Medium` / `ExtraBold`。

Phase 12 起，字符串选项使用 `FrontedPropertyEditorOption` 分离显示名和保存值。ComboBox 显示 `Designer.Option.{Property}.{Value}` 的本地化文本，例如 `HorizontalAlignment.Center` 在中文界面显示“居中”，但提交到 config 的值仍是原始 `"Center"`。这条规则同样适用于 `VerticalAlignment`、`TextAlignment`、`TextWrapping`、`Stretch` 和 `FontWeight`。不要把本地化显示文本写入 v3 JSON。

颜色处理：

1. 优先使用项目已有 ColorPicker。
2. 保存为 `#AARRGGBB`。
3. 识别属性名：`Color`, `Foreground`, `Background`, `FillColor`, `BorderColor`。
4. 无效颜色字符串不会让编辑器崩溃；ColorPicker 显示白色 fallback，并由属性行验证错误提示用户修正。

`FontFamily` 编辑器：

1. 使用与旧 `TextSettingsEditControl` 类似的可编辑 `ComboBox`，支持文本搜索。
2. 每个选项使用自身字体预览显示名。
3. 系统字体保存普通字体名字符串。
4. 内置字体保存现有 pack URI 约定，例如 `pack://application:,,,/Assets/Fonts/#Noto Sans`。
5. 预览内置字体时按运行时同样的 split 逻辑构造 `FontFamily(new Uri(pathBeforeHash), "./" + hashAndName)`，不要把 pack URI 原样传给 `new FontFamily(string)`。
6. 如果当前值不在选项中，ComboBox 允许手写并按原始字符串提交；无效字体字符串不能让属性网格崩溃。

Phase 8F owner validation 后，文本类属性使用显式提交模型。`Name`、`BindingPath`、普通 `Text` 字符串、资源路径字符串和手写 `FontFamily` 都先写入 `FrontedPropertyEditorItem.EditText`，按 Enter 或右侧 Check/Apply 按钮才提交。颜色行同样遵守显式提交：ColorPicker 选择颜色只把 `EditText` 和可见 Hex 文本更新为 `#AARRGGBB`，Apply 或 Hex 文本框 Enter 才写回 config；手写 Hex 有效时同步 ColorPicker，提交失败时保留输入并显示红色错误。`Name` 和 `BindingPath` 不再在 LostFocus 时自动提交，避免焦点移动和 Property Grid 重建时把未确认输入写回布局。提交失败时保留用户输入，设置 `HasEditError` / `EditError`，文本框显示红色边框，并在属性行下方显示验证消息；用户继续编辑或提交成功后错误状态清除。`Name` 仍遵守运行时关键名称只读、合法 WPF 名称、同 Canvas 唯一和被引用控件阻止重命名规则；成功重命名后刷新左侧列表、选中摘要、preview、hitbox/selection label 和属性行。

Phase 8G 起，`BindingPath` 仍是可手写文本框，但旁边新增 Browse button。Binding Browser 使用 curated `ISharedDataService` 树，包含 `CurrentGame`、队伍、固定索引的 `SurPlayerList[0..3]`、`HunPlayer`、`MatchScore`、当前/全局 Ban 列表和倒计时等常用路径；搜索可按显示名或完整绑定路径过滤。Binding Browser 现在按当前属性行的目标类型过滤候选路径：`Text` / `LocalizedText` 只显示字符串和数字，`Image` 只显示 `ImageSource` / `BitmapSource` / `BitmapImage` 兼容值，`GameProgressText.BindingPath` 只显示 `GameProgress`，`MapNameText.BindingPath` 只显示 `Map` / `Map?`。不匹配的叶子节点会从树和搜索结果中隐藏，父节点只在仍有可用子节点时保留。选择结果只更新该行 `EditText`，不会立即写入 config，不会调用真实 `ISharedDataService`，也不会推入 Undo；用户后续按 Apply 或 Enter 后才走 `ApplyPropertyEdit`、校验、预览刷新和 Undo snapshot。

Phase 12 起，Binding Browser 的节点显示名和期望类型名可以本地化，但完整 `BindingPath` 始终作为原始路径在树、搜索结果或选中路径区域可见。选择后写回的仍是 `CurrentGame.SurTeam.Name` 这类原始路径，绝不写入“主队名称”等显示文本。

Phase 8G 起，图片/资源路径字段旁新增 Resource Browser。当前资源来源包括内置运行时文件 `Resources/bpui`，返回值使用 resolver 约定的 `Resources/<fileName>`；也支持通过 “Browse file...” 选择 png/jpg/jpeg/webp/bmp 绝对路径。控件级 Resource Browser 选择外部文件仍只写入编辑缓冲。Phase 9B.0 已在 Canvas Properties 中提供 `CanvasWidth`、`CanvasHeight`、`BackgroundImage`、清除背景、浏览资源和选择本地图片；选择本地图片会复制到 editor-local resource store，layout JSON 写为 `bpui://local/...`。导出包时再复制进包资源并重写为 `bpui://{PackageId}/...`。

`FontFamily` 行仍使用可编辑 ComboBox，但不再依赖 `SelectedValue` 双向绑定。下拉打开期间不会触发 LostFocus 提交或重建 Property Grid；用户从下拉中选择时写入对应 `FrontedFontFamilyOption.Value`，因此内置字体继续保存 `pack://application:,,,/Assets/Fonts/#...` 原值；用户手写自定义字体时按 Enter 或真正失焦提交 `ComboBox.Text`。下拉项继续使用各自的 `PreviewFontFamily` 显示，保持旧 `TextSettingsEditControl` 的字体预览语义。

属性编辑直接修改当前设计项的 `Config`。每次成功编辑后：

1. 校验设计项和 Canvas。
2. 重新渲染 preview。
3. 更新 hitbox 和 adorner。
4. 标记布局 dirty。

属性编辑提交必须只由用户交互触发。普通 ComboBox 在 `DropDownClosed` 后提交，文本类属性在 Enter 或 Apply 按钮后提交，CheckBox 在 Click 后提交，ColorPicker 只同步 Hex 编辑缓冲，颜色写回也由 Apply 或 Enter 提交，FontFamily ComboBox 按上述下拉/手写规则提交。属性网格重建、切换选中控件、绑定初始化和 layout pass 期间应抑制提交事件，避免 BpWindow / CutSceneWindow 中大量枚举或字符串选项行触发递归重建。失败的属性提交不应请求 preview render，也不应重建到丢失用户输入。

拖拽和缩放过程中的 live geometry edit 只更新内存 config、linked overlay、preview element、hitbox/adorner、选中控件几何摘要和 dirty 状态，不运行完整校验、不重建 Property Grid、不强制重渲染。鼠标释放或键盘微调等 commit 操作再执行一次校验、属性行刷新和最终 preview render。

Phase 8E 的名称编辑采用保守策略：

1. `Name` 属于设计项和 JSON key，不属于 config object；不要给 `FrontedControlConfigBase` 或派生 config 添加重复 `Name`。
2. 运行时关键控件的 `Name` 只读。
3. 普通控件改名必须非空、匹配 `^[A-Za-z_][A-Za-z0-9_]*$`，并在当前 Canvas 内唯一。
4. 如果旧名称被其他控件引用，Phase 8E 阻止改名并提示“Reference-aware rename will be implemented later.”，避免静默断开引用。
5. `PickingBorderOverlay` 不是普通 Property Grid 目标；如果被程序化选中，属性行应按只读处理或清空选择。
6. `BanSlotDisplay` 仍作为单个控件编辑；内部 ban lock overlay 不是单独属性面板目标。

## 12. Binding Browser

Phase 8G 已实现。任何可浏览的 `BindingPath` 属性都会显示：

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

选择节点后写入路径，例如：

```text
CurrentGame.SurTeam.Name
CurrentGame.SurPlayerList[0].Member.Name
CurrentGame.MatchScore.CurrentSurTeamMajorText
```

浏览器按属性行携带的 `BindingTargetKind` 初始化过滤器。内置控件的推断规则为：`TextFrontedControlConfig.BindingPath` 和 `LocalizedTextControlConfig.BindingPath` 使用文本过滤；`ImageFrontedControlConfig.BindingPath` 使用图片过滤；`GameProgressTextControlConfig.BindingPath` 使用 `GameProgress` 过滤；`MapNameTextControlConfig.BindingPath` 使用 `Map` 过滤；未知插件或未来控件默认使用 `Any`，避免宿主过早拒绝插件自定义路径。浏览器标题区会显示当前期望绑定类型，搜索结果遵守同一过滤器，例如文本模式搜索 `Logo` 不会返回队标图片，图片模式搜索 `Name` 不会返回字符串名称。

浏览器只更新属性行编辑缓冲；Apply/Enter 前，选中控件 config 仍保持旧值。取消浏览器不会修改 `EditText`。

## 13. Resource Browser

Phase 8G 已实现控件级资源路径浏览。Resource Browser 面向图片和资源路径字段：

1. `BackgroundImage`
2. 图片路径字段
3. `PickingBorderOverlay.BorderImagePath`
4. `BanSlotDisplay.LockImageSource`
5. 未来其他 image fields

浏览器应支持：

1. 内置 `Resources/bpui` 路径。
2. 通过文件选择器选择绝对路径图片。
3. 缩略图预览；缩略图加载失败时显示为空，不阻断浏览器。

路径规则：

1. `Resources/foo.png` 解析到运行目录 `Resources/bpui/foo.png`。
2. 绝对路径在 resolver 支持时直接加载。
3. 不应让用户直接编辑 raw pack URI，除非目标字段明确需要 pack URI，如字体。
4. 内置 `Assets` 资源不是当前 resolver 的文件路径资源，暂不列入 Resource Browser。

## 14. 编辑操作

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
3. 被其他控件引用的控件不能静默删除，必须阻止或确认并同步清理引用。

### Copy/Paste

Phase 10 已实现内部控件复制/粘贴。`Ctrl+C` 复制当前选中的普通可编辑控件，`Ctrl+V` 粘贴到当前 Canvas；该剪贴板只存在于编辑器 ViewModel 内，不使用系统剪贴板。运行时关键控件、`PickingBorderOverlay`、不可选/不可编辑控件不能复制。粘贴时深拷贝 config，名称按尾部数字递增并避开冲突，`Left` / `Top` 偏移 `+10`，`ZIndex` 设为当前最大值加 1，并正常进入 dirty、undo、validation、preview 刷新流程。焦点位于 `TextBox`、可编辑 `ComboBox`、ColorPicker 文本区域等文本输入时，窗口不会拦截 `Ctrl+C` / `Ctrl+V`，保留普通文本复制粘贴。

### Undo/Redo

Phase 8F foundation 修复后已提供基础内存 Undo/Redo。工具栏有 Undo / Redo 按钮，快捷键为 `Ctrl+Z`、`Ctrl+Y` 和 `Ctrl+Shift+Z`；焦点位于 `TextBox`、`ComboBox`、ColorPicker 等属性编辑器内时不抢编辑控件自身的撤销/重做。Undo/Redo 以当前 Canvas config 的 JSON 快照实现，覆盖新增控件、删除控件、成功属性提交、重命名、颜色/字体提交、键盘移动和鼠标拖拽/缩放提交；切换窗口/Canvas 或 reload 会清空栈。该能力仍只影响当前内存设计文档，不保存用户布局。

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
2. Reset to Built-in
3. Open Layout Folder

保存前：

1. 执行完整 layout validation。
2. 存在 Error 时保存失败。
3. 只有 Warning 时可允许用户确认后保存。

Phase 8H 起，Save 只写入 `%APPDATA%/neo-bpsys-wpf/FrontedLayouts/{WindowName}/{CanvasName}.json`，绝不覆盖源码或发布目录中的 `Resources/FrontedLayouts`。Reset to Built-in 会删除当前 Canvas 的用户布局文件并重新加载内置布局，清空 undo/redo、选择和筛选。Open Layout Folder 打开当前用户布局文件所在目录，目录不存在时会先创建。

`.bpui v3` package 导出/导入已放到 `FrontManagePage` 的 `Layout Packages` tab。导出会打开 manifest 对话框，并固定导出全部已迁移前台布局；导入会安装 v3 包并可立即激活。SettingPage 中现有 `.bpui` 导入导出是 legacy 流程，会覆盖全局 `Config.json`，不能作为 Designer v3 包管理入口。

`AllowTransparency` 是窗口级选项，不是普通控件属性。单 Canvas 窗口可以在 Canvas Properties 附近显示该开关，多 Canvas 窗口则对整个窗口生效。由于 WPF 透明窗口行为可能需要重新创建窗口或重启应用，开关变化后应提示需要重启；如果用户选择立即重启，必须先处理设计器未保存修改，提供 Save / Discard / Cancel。

窗口/Canvas 切换、Reload、Reset to Built-in 和关闭编辑器时，如果当前文档 dirty，会通过 `MessageBoxHelper` 提示 Save / Discard / Cancel。Save 会先执行完整校验，存在 Error 时阻止保存并取消切换或关闭；Warning/Info 不阻止保存。关闭窗口的 dirty prompt 必须先在 `Closing` 中设置 `e.Cancel = true`，再通过 Dispatcher 异步显示本地化的宽版 helper 对话框；用户选择 Save 且保存成功或选择 Discard 后，设置强制关闭标记并再次调用 `Close()`。这样避免 WPF 在窗口已经进入 closing 状态时执行 `ShowDialog` / `Close` 触发异常。验证详情窗口是非模态子窗口，父编辑器关闭时只做受保护关闭，已关闭或正在关闭时不能让异常冒泡。

顶部工具栏从 Phase 8H owner validation 修正后使用 `ScrollViewer + WrapPanel`，窗口选择器、Canvas 选择器、Add/Delete、Undo/Redo、保存/重置/打开目录、reload/validate、缩放、吸附和 dirty/path 状态都允许在窄窗口下自动换行。长 layout path 只显示省略文本并通过 tooltip 查看完整路径，不能把工具栏撑出窗口右侧。

吸附行为从 Phase 8H 开始改为默认关闭：`SnapEnabled` 是工具栏 ToggleSwitch 的持久开关，`IsShiftSnapActive` 只表示编辑 surface 中 Shift 当前按下，`EffectiveSnapEnabled = SnapEnabled || IsShiftSnapActive`。Shift 临时吸附只更新状态文字，例如“临时吸附”，不会修改 ToggleSwitch 的 `IsChecked`，避免 KeyDown/KeyUp 时反复刷新开关。鼠标拖拽和缩放在 `EffectiveSnapEnabled` 为 true 时按默认 10 px 网格吸附；关闭时仍按 0.5 坐标精度归一化。方向键在吸附开启时使用网格步长，普通模式保留 0.5/修饰键微调语义。

Phase 10 起，编辑器 typed/pasted input 会按集中限制截断：搜索 128 字符，控件名 64，`BindingPath` 256，资源路径和 Canvas `BackgroundImage` 1024，`FontFamily` 256，静态 `Text` 512。发生截断时显示 `InputTruncated`。这些限制只适用于编辑器输入；外部导入 `.bpui`、layout JSON 或 manifest 时，超长字段会被拒绝，不会截断。Add Control 在当前 Canvas 已有 256 个控件时拒绝新增并显示 `ControlCountLimitReached`；保存仍由 validator 阻止硬限制错误。

## 16. 未来实现阶段建议

| 阶段 | 范围 |
| --- | --- |
| Phase 8B | 已实现：`FrontedControlDesignItem` / `FrontedCanvasDesignDocument`、设计项与 dictionary 转换、`FrontedLayoutValidator`、名称校验、引用扫描、运行时关键名称 catalog、重复 JSON key 检测 |
| Phase 8C | 已实现：`FrontedDesignerWindow` shell、window/canvas selector、只读 preview surface、缩放控制、layout source 状态和 validator 消息面板 |
| Phase 8D | 已实现：interaction layer、透明 hitbox、selection adorner、drag、resize、键盘微调；owner validation 后补齐左侧控件列表/筛选、单击选择与拖拽分离、选中 hitbox editor-only 提层、拖拽/缩放时 preview live update、无显式尺寸时使用渲染实际尺寸；仍只改内存，不保存用户布局 |
| Phase 8E | 已实现：基础 Property Grid、Text/Number/Boolean/Enum/ColorPicker 编辑、对齐/换行/拉伸/字重字符串选项 ComboBox、保守 Name 编辑、运行时关键名称只读、被引用控件改名阻止；owner validation 后验证详情移至底部状态区弹窗，属性重建期间抑制提交，live 拖拽不重建属性网格；仍只改内存，不保存用户布局 |
| Phase 8F | 已实现：Add Control 菜单、默认 config factory、唯一命名、视口中心放置、独立 placeholder preview data、FontFamily 字体 ComboBox、ColorPicker Hex 缓冲显式提交、左侧右键/Property Grid 底部删除、基础内存 Undo/Redo；仍只改内存，不保存用户布局。Phase 9B.1 后右侧 Property Grid 底部删除按钮已移除。 |
| Phase 8G | Binding Browser、Resource Browser |
| Phase 8H | 已实现：用户 layout save/reset/load priority、validation-driven save、打开布局目录、dirty prompt 和 snap-to-grid |
| Phase 9B.0: Canvas Properties GUI, local bpui resource normalization, toolbar cleanup, and Window Options foundation | 已实现：Canvas Properties GUI，包含 `CanvasWidth`、`CanvasHeight`、`BackgroundImage`，并将本地图片规范化为 `bpui://local/...`；工具栏整理为主按钮和二级菜单；新增窗口级 Window Options 基础。 |
| Phase 9B.1: FrontManagePage Layout Package Manager UI skeleton | 已实现：`FrontManagePage` 的 `Frontend Windows` / `Layout Packages` 顶层 tabs、Layout Packages tab skeleton、布局包枚举服务基础、活动包状态读取/写入骨架，以及设计器工具栏/菜单重复项清理；独立编辑器入口保留在 `Frontend Windows` 页，不单独占用 tab。 |
| Phase 9C: v3 package export | 已实现：Layout Package Manager 紧凑 UI、导出 manifest 对话框、All Frontend Layouts 导出、manifest 生成、layout/window options 打包、资源复制和 URI 重写。 |
| Phase 9D: v3 package import/activation/delete | 已实现：v3 `.bpui` 导入安装、重复包替换确认、激活复制到用户布局目录、切回内置、删除普通包和删除活动包时先切回内置。legacy 转换仍是后续阶段。 |
| Phase 12: Designer v3 i18n display layer | 已实现：通过 `IFrontedDesignerLocalizationService` 本地化编辑器显示层；Property Grid 的 `PropertyName`、`GroupName`、`ControlType`、`BindingPath`、资源 URI、`FontFamily` 和控件 `Name` 保存值保持原始契约值。 |

## 17. 非目标

当前编辑器仍不做：

1. 不实现 Save As。
2. 不修改 `AnimationService` 查找逻辑。
3. 不迁移 `.bpui`。
4. 不移除旧 `config.json` 前台设置。
5. 不改变现有 v3 layout JSON schema。
6. 不把 `AllowTransparency` 当成控件属性；它是窗口级选项。
