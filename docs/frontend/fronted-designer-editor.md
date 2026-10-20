# Fronted Designer 独立编辑器

本文负责编辑交互、属性网格、浏览器、撤销和保存安全。持久化模型以[布局契约](fronted-layout-v3-window-centric.md)为准；控件渲染/绑定语义见[运行时](fronted-designer-v3.md)，公开属性/Part API 见 [SDK](../plugin/plugin-development.md)，动画触发与图执行见 [Behavior](fronted-behavior-system.md)。

## 入口与文档所有权

[FrontedDesignerWindow](../../neo-bpsys-wpf/Views/Windows/FrontedDesignerWindow.xaml) 是后台的独立非模态工具窗口，不创建或复用 OBS 输出窗口作为编辑 surface。窗口目录来自 IFrontedWindowRegistry.GetV3LayoutWindows，包括活动包自定义窗口；XAML registration 不进入 Designer。

[LayoutLifecycle](../../neo-bpsys-wpf/ViewModels/Windows/FrontedDesignerWindowViewModel.LayoutLifecycle.cs) 经 IFrontedLayoutService 加载 FrontedWindowConfig，暂存文档自身 WindowSettings、DisplayNames，再把内部 canvas/control 适配为 FrontedCanvasDesignDocument。这里仍使用 FrontedCanvasConfig 作为内部编辑模型，并不恢复 canvas-centric 磁盘格式。设计文档转回 WindowConfig 时必须保留窗口宽高、位置和名称字典，不能拿 Canvas 尺寸或新选择器项默认值覆盖旧文档。

切换窗口时选择器可先变化，旧文档在新布局加载成功前仍拥有原 WindowSettings。异步窗口选项保存必须固定开始时的目标窗口 ID；不能在 await 后取已变化的选择器值。对局预览使用 DesignerPreviewSharedDataService 与 SharedDataServiceOverride，不调用真实共享服务 NewGame，不改变比赛数据，也不把 placeholder 写入 JSON。

## 名称、身份与引用

JSON key / 控件名与 BehaviorGuid 的规则统一见[运行时契约](fronted-designer-v3.md)。设计项 Name 编辑字典 key；不向 Config 新增 Name。改名前检查格式、长度与当前状态内重名；FrontedLayoutReferenceScanner 发现 incoming references 时阻止改名/删除。当前内置配置没有那批历史假设中的 LinkedControlName、LeftBindingTarget 等字段，不应把它们列为已实现 API。

当前源码没有“关键控件 catalog 一律只读”的保护。不要依赖该旧设计假设；涉及内置布局改名时应检查迁移名称、生成部件和配套行为引用。行为跟随 GUID 而不是显示名；复制控件生成新身份，删除处理行为文档；复制行为生成新 BehaviorId，改写 self 目标及可推断的语义索引，不静默改写其他控件的外部引用。

校验由 [FrontedLayoutValidator](../../neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedLayoutValidator.cs) 负责：名称/引用/配置错误阻止保存；当前 SaveCurrentLayoutAsync 不因 Warning/Info 阻止或额外确认。原始重复 JSON key 的读取缺口见[已知边界](../known-limitations-and-roadmap.md)，不能声称 dictionary 建成后还能发现被覆盖项。

## 预览坐标、选择和几何

结构为 ScrollViewer → PreviewWorkspace → PreviewZoomHost → DesignSurfaceGrid，后者叠放 PreviewCanvas 与 InteractionLayer。两层尺寸来自 CanvasSettings；缩放用 LayoutTransform。鼠标 GetPosition(InteractionLayer) 得到逻辑设计坐标，无需再乘除 ZoomScale；真实窗口外框、标题栏、ActualHeight 不参与计算。工具窗口不默认设置主窗口 Owner 或最大化，避免 owned maximized window 的任务栏联动。

透明 hitbox 独立于可见像素，空文本、无图片、Hidden/Collapsed 和缺失插件控件仍可选择。空尺寸优先用预览实际尺寸，仍不可用时使用最小 hitbox。命中提升只影响 InteractionLayer，不更改 JSON ZIndex。

- 单击 hitbox 或图层列表选择；在未选中项上按下拖动不抢选中控件焦点。
- 拖动/八方向 resize 修改当前内存配置；左/上缩放同时调整 Left/Top。Shift、Ctrl、Alt 微调步长分别为 10、1、0.1，普通方向键为 0.5；最终坐标按 0.5 精度规范化。
- Ctrl+Wheel 手动缩放 25%–200%，Fit 可低于 25%；右键或 Space+左键平移只改变 viewport offset。
- 图片 lock/picking overlay 是内部视觉，不是独立图层或 hitbox。BorderedImage 可在根框与内部 ImageWidth/ImageHeight 之间切换 resize target；Part/PartCollection 通过统一几何目标适配。
- GlobalScoreRow.Cells 是父行内部固定模板：父移动不改子格相对 X/Y；子格不进入全局层级、复制删除或 ZIndex 拖放。MapV2 的动画内部部件也不作为顶层控件编辑。
- Rectangle/Polygon 编辑及归一化顶点格式见[控件运行时](fronted-designer-v3.md)。Visibility 改变不删除设计项。

Add Control 从 V3 registry 建默认 config 和唯一名称，放在视口/画布中心，仅修改内存。缺插件不提供新建入口，但已有 PluginFrontedControlConfig 保留 ExtensionData，显示可移动、缩放、删除的占位；专属属性在缺 schema 时不开放。不会恢复 descriptor/typed-config 旧插件管线。

## Property Grid 与提交

WPF ItemsControl 按属性类型按需创建编辑器：字符串 TextBox，数字 NumberBox（可空值提供清除），bool ToggleSwitch/CheckBox，enum ComboBox，颜色 PortableColorPicker，资源/绑定路径配浏览按钮，TextBinding 用专用多来源对话框。显示标签和选项本地化，JSON 使用不变量原值；颜色保存 #AARRGGBB。字体支持系统名、内置 pack URI 和包内字体；pack 字体按 URI 与 #族名拆开构造，不将整个 URI 直接当系统字体名。

属性行使用混合提交模型。普通 `Text` 字符串、数字和颜色会在输入变更后短延迟自动提交，ColorPicker 选色会立即写回 config；这些行不显示右侧确认按钮。`Name`、`BindingPath`、资源路径字符串、手写 `FontFamily`、`TextBinding`、以及 `Id` / `Key` / `Filter` / `Guid` / 内部目标名称这类容易影响引用或解析的字段仍使用显式提交：先写入 `FrontedPropertyEditorItem.EditText`，按 Enter、右侧 Check/Apply 按钮或浏览器选择后才提交。`Name` 和 `BindingPath` 不在 LostFocus 时自动提交，避免焦点移动和 Property Grid 重建时把未确认输入写回布局。提交失败时保留用户输入，设置 `HasEditError` / `EditError`，文本框显示红色边框，并在属性行下方显示验证消息；用户继续编辑或提交成功后错误状态清除。Name 提交仍遵守合法 WPF 名称、当前状态内唯一、可编辑性与 incoming reference 阻断规则；成功重命名后刷新左侧列表、选中摘要、preview、hitbox/selection label 和属性行。

Text 和 LocalizedText 不再把基类 `BindingPath` 作为内容来源。它们的 Property Grid 显示专用 `TextBinding` 编辑按钮：弹窗内可添加、删除、上移、下移 source，手写 Path 或通过 Binding Browser 选择，并编辑 `StringFormat`、`JoinSeparator`、`NullText`、`FallbackText`。source 顺序对应复合格式的 `{0}`、`{1}`、`{2}`；格式为空时按分隔符连接。确认弹窗作为一个 undo/redo 步骤提交；空 Path、格式语法错误或超出 source 数量的占位符会阻止确认且保留输入。

Binding Browser 只更新显式提交字段的编辑缓冲；资源浏览器确认和手写资源路径的提交差别见下文。

图片/资源路径字段旁提供 Resource Browser。当前资源来源包括内置运行时文件 `Resources/bpui`，返回值使用 resolver 约定的 `Resources/<fileName>`；也支持通过 “Browse file...” 选择 png/jpg/jpeg/webp/bmp 绝对路径。`Image` / `BorderedImage` 的静态图片选择写入 `ImagePath`，动态数据仍通过 `BindingPath` 和 Binding Browser 选择；两者同时存在时运行时以 `BindingPath` 为准。Resource Browser 确认选择后立即调用 ApplyPropertyResourceSelectionAsync；绝对文件先复制为 editor-local 资源再更新配置并记录撤销。手写路径仍需 Enter/Apply，Binding Browser 则只写编辑缓冲。Canvas Settings 中提供 `CanvasWidth`、`CanvasHeight`、`BackgroundImage`、清除背景、浏览资源和选择本地图片；选择本地图片会复制到 editor-local resource store，layout JSON 写为 `bpui://local/...`。导出包时再复制进包资源并重写为 `bpui://{PackageId}/...`。Window Settings 中提供 `WindowSettings.WindowWidth` / `WindowHeight`、透明和背景色编辑，保存到同一个 window-centric layout JSON。

Resource Browser 的标题、搜索、按钮、空状态和来源/类型显示可本地化，但选中区域必须保留原始资源 URI 或文件路径。写回配置的仍是 `Resources/foo.png`、`bpui://...` 或绝对路径原值，不写入本地化显示文本。

`FontFamily` 行仍使用可编辑 ComboBox，但不再依赖 `SelectedValue` 双向绑定。下拉打开期间不会触发 LostFocus 提交或重建 Property Grid；用户从下拉中选择时写入对应 `FrontedFontFamilyOption.Value`，因此内置字体继续保存 `pack://application:,,,/Assets/Fonts/#...` 原值；用户手写自定义字体时按 Enter 或真正失焦提交 `ComboBox.Text`。下拉项继续使用各自的 `PreviewFontFamily` 显示，保持旧 `TextSettingsEditControl` 的字体预览语义。

属性编辑直接修改当前设计项的 `Config`。每次成功编辑后：

1. 校验设计项和 Canvas。
2. 重新渲染 preview。
3. 更新 hitbox 和 adorner。
4. 标记布局 dirty。

复合控件通过 V3 统一选择模型编辑内部区域：固定 Part 和 PartCollection 子项的可移动、缩放及持久化维度来自 registration schema；MapV2Display 的固定部件几何仍保存到 InternalParts，不进入全局图层。根控件选中且注册声明 SupportsPeerStyleTransfer 时，可执行 ApplyAppearanceToSameType；当前 UI 使用 Default profile，只传播同 CanonicalControlType 的 Appearance，不复制根尺寸、部件布局、行为或数据身份。子项选中时禁用该命令。完整传播契约见 [SDK](../plugin/plugin-development.md)。

`MapV2Display` 的选图边框不是独立可编辑控件。内置 `MapV2Window.behaviors.json` 通过 `MapV2.PickingBorderStateChanged` 事件和 `part:{BehaviorGuid}:PickingBorder` 目标实现 MapBpV2 的旧呼吸灯语义；后台开关只发布业务状态，淡入、呼吸、淡出和隐藏由 v3 行为定义。

属性编辑提交必须只由用户交互触发。普通 ComboBox 在 `DropDownClosed` 后提交，CheckBox 在 Click 后提交，非显式提交文本/数字行在用户输入后短延迟提交，非显式提交颜色行在 ColorPicker 或 Hex 输入变化后提交，显式提交文本行仍在 Enter/Apply 时提交；资源浏览器确认会直接提交资源选择，Binding Browser 仅更新缓冲，FontFamily ComboBox 按上述下拉/手写规则提交。属性网格重建、切换选中控件、绑定初始化和 layout pass 期间应抑制提交事件，避免 BpWindow / CutSceneWindow 中大量枚举或字符串选项行触发递归重建。失败的属性提交不应请求 preview render，也不应重建到丢失用户输入。

拖拽和缩放过程中的 live geometry edit 只更新内存 config、linked overlay、preview element、hitbox/adorner、选中控件几何摘要和 dirty 状态，不运行完整校验、不重建 Property Grid、不强制重渲染。提交时执行一次校验与属性行刷新，优先修补受影响的预览元素。

## Binding Browser

[FrontedBindingBrowserWindow](../../neo-bpsys-wpf/Views/Windows/FrontedBindingBrowserWindow.xaml) 使用 TreeView 与搜索展示 catalog。节点类型过滤与搜索采用同一 FrontedBindingTypeFilter；不匹配叶子隐藏，无可用子项的父节点也隐藏：

| 属性目标 | 可选值类型 |
| --- | --- |
| TextBinding.Sources | string、数字、bool、enum、DateTime、TimeSpan |
| Image/BorderedImage.BindingPath | ImageSource 及其派生类型 |
| LockVisibilityBindingPath | bool |
| GameProgressText.BindingPath | GameProgress |
| MapNameText.BindingPath | Map / Map? |
| 未明确类别的扩展属性 | Any，避免错误限制插件路径 |

节点标签、标题、期望类型和按钮可本地化，但完整 BindingPath 保持原始值，例如 CurrentGame.SurTeam.Name、CurrentGame.SurPlayerList[0].PictureShownHeader。确认仅更新属性行 EditText 并清除该行旧错误；Enter/Apply 前不写 config、不记录撤销，取消保持原缓冲。TextBinding 对话框的确认则作为一个整体编辑提交。

根注册、DTO attribute 与固定集合索引以[控件运行时](fronted-designer-v3.md)的 Binding catalog 为准；catalog 只读类型元数据、不调用 getter。浏览器类型过滤不是手写路径的白名单，动态索引仍使用运行时绑定契约。

## Resource Browser 与导入

浏览器枚举 Resources/bpui 图片、提供缩略图与文件选择，失败缩略图不阻断操作；Assets 内嵌资源不当作文件目录枚举。浏览和显式导入是不同动作：资源 URI、local 命名空间、当前包字体隔离和导出收集规则以 [BPUI](bpui-package-v3.md) 为准。未保存的导入资源应由 Resources 分部跟踪，取消编辑或关闭时只清理本次未被引用的资源，不删除其他包或用户原文件。

## 撤销、复制与 BO 状态

普通布局和行为编辑共用内存历史；Ctrl+Z、Ctrl+Y、Ctrl+Shift+Z 在文本/ComboBox/ColorPicker 输入内不抢局部撤销。控件剪贴板在 ViewModel 内，不使用系统剪贴板；Ctrl+C/V 在文本输入内保持正常文本语义。粘贴深拷贝 config 与插件扩展数据、生成唯一安全名称，几何偏移 +10。

切换 BO 编辑状态本身不记历史；“复制 BO5 到 BO3”深拷贝背景、依赖、控件、可见性和插件字段，作为一次撤销。恢复特定状态不得写坏另一状态或切换当前编辑状态。窗口切换/reload 清空历史；几何、层级与完整编辑条目共用栈，不能丢掉非几何派生字段。

## 保存、重置与关闭

保存先验证实际内存文档，再通过 LayoutService 写当前活动可编辑包；builtin 首次写入自动复制为用户方案。布局与行为分别保存，成功项各自清除 dirty；异常保留错误状态，不能将多文件写入描述为完整事务。保存后通知包列表并重载已创建输出窗口。WindowSettings / DisplayNames 随原文档写回，不由选择器补值。

ResetToBuiltInCoreAsync 经包管理器读取 builtin 的布局与行为，替换当前内存预览、清空选择/筛选/历史和待导入资源；源码没有旧文档所说的“先删除用户布局文件”。对应内置布局缺失时报告缺失。它也不等同于激活 builtin 或删除整个包。

窗口切换、reload、reset、关闭前对 dirty 文档提供 Save/Discard/Cancel。保存失败取消后续动作。Closing 先设 e.Cancel，再经 Dispatcher 显示异步提示；确认后以强制关闭标记再 Close，避免在 Closing 中重入 ShowDialog/Close。验证详情为非模态子窗口，父窗口关闭时受保护地关闭。

BPUI 操作集中于 FrontManagePage 的 Layout Packages；SettingPage 旧导入导出入口已删除，legacy 包转换不覆盖当前 Config.json。窗口透明度变化由窗口服务静默重建对应实例，见[窗口运行时](frontend-windows-and-layout.md)。

吸附默认关闭：`SnapEnabled` 是工具栏 ToggleSwitch 的持久开关，`IsShiftSnapActive` 只表示编辑 surface 中 Shift 当前按下，`EffectiveSnapEnabled = SnapEnabled || IsShiftSnapActive`。Shift 临时吸附只更新状态文字，例如“临时吸附”，不会修改 ToggleSwitch 的 `IsChecked`，避免 KeyDown/KeyUp 时反复刷新开关。鼠标拖拽和缩放在 `EffectiveSnapEnabled` 为 true 时优先尝试智能对齐：活动控件的 left/center/right 或 top/center/bottom 可吸附到画布边缘、画布中心线以及其他可选择、可编辑、非 linked overlay 控件的边缘/中心；未命中智能候选的轴继续按默认 10 px 网格吸附。关闭时仍按 0.5 坐标精度归一化。方向键在吸附开启时使用网格步长，普通模式保留 0.5/修饰键微调语义。智能吸附产生的辅助线只作为 `InteractionLayer` 上的临时 `Line` 元素显示，不写入 `PreviewCanvas`，也不会序列化到 v3 layout 或 `.bpui`。

编辑器 typed/pasted input 会按集中限制截断：搜索 128 字符，控件名 64，`BindingPath` 256，资源路径和 Canvas `BackgroundImage` 1024，`FontFamily` 256，静态 `Text` 512。发生截断时显示 `InputTruncated`。这些限制只适用于编辑器输入；外部导入 `.bpui`、layout JSON 或 manifest 时，超长字段会被拒绝，不会截断。Add Control 在当前 Canvas 已有 256 个控件时拒绝新增并显示 `ControlCountLimitReached`；保存仍由 validator 阻止硬限制错误。

强类型编辑文档会重新构造 WindowConfig；它没有通用 root JsonExtensionData。插件 ExtensionData 有明确保留契约，但任意未知窗口根字段不能据此宣称 Designer 保存也无损；原始包快照导入/导出与编辑后保存应分别验证。

## 性能与资源生命周期

Designer 真正关闭时取消待执行的预览、选择器、属性提交恢复、选择装饰器和滚动归位 DispatcherOperation，并取消 ViewModel 的排队校验/预览，停止窗口计时器，释放预览动画 runtime session，清空单例 PreviewAnimationScope 的根视觉树、控件和行为文档引用。重新打开后由新预览渲染重新建立作用域；完整重绘先释放将被替换视觉树的动画 session，避免 BaseValues 和 overlay 持有旧控件，但不执行结束窗口会话的清理。动画编辑器关闭会取消循环及单次图预览，图任务自行在 finally 中释放自己的令牌源。节点图视图使用 WPF 弱属性事件；行为面板在 Loaded/Unloaded 时幂等订阅/解除 ViewModel 事件。

### 增量更新与历史预算

多选根控件时，属性面板仅显示所有选中项都可编辑、属性路径与类型、编辑器及可选值一致的共有属性。非共有、只读、身份、绑定和资源选择等不可批量编辑的属性整行隐藏；筛选后重新确定分组与分节标题。共有属性值不同时保留编辑器并留空，输入后通过各控件自身的 Schema Storage 批量写入，因此不同类型控件的共有属性也可安全编辑。切回单选恢复完整属性面板。

选择和校验更新复用路径、类型及编辑器匹配的属性行，避免销毁 ColorPicker、ComboBox 等完整模板；只插入、移动或删除实际变化的行。多选交集比较每种控件注册的 Schema 与每个配置的动态编辑能力，不为每个选中项构建完整属性网格；字体选项直接共享现有缓存。选择更新复用根 hitbox，仅重新建立选择装饰器，并仅更新动画 Self 上下文，不扫描整个预览树。主选中项不变但多选集合变化时，同样刷新属性交集。

根控件拖动、缩放、键盘微调和几何属性提交使用专属几何历史，仅保存涉及控件的名称、Left/Top、可空 Width/Height 与所属 BO 状态；子部件与其他编辑继续使用完整快照。完整快照使用 UTF-8 JSON 字节保存；撤销和重做每个栈同时限制为最多 50 步及约 32 MiB 的快照负载，至少保留最近一步。跨 BO 状态恢复只写回对应状态，完整快照的几何判断须按真实派生配置类型比较非当前状态，不能通过默认基类序列化忽略 Text 等字段。属性提交不再重复执行整份校验和预览；几何属性优先修补预览，其他属性只刷新涉及控件的子节点，不重建所有图层卡片。

图片安全校验缓存最多 1024 个文件的尺寸元数据，不持有解码位图；文件长度、写入时间或创建时间变化后重新读取，且每次都按调用方图片用途重新执行大小限制。行为节点拖动期间只更新位置与受影响连接，释放后统一校验；节点图历史使用 UTF-8 字节、每栈最多 50 步及约 16 MiB 负载，淘汰最旧条目。

根控件拖动、缩放提交和图层重排优先修补现有预览元素，不重新创建所有控件、绑定和图片。几何提交合并调度校验；图层重排只改变顺序和 ZIndex，不重复执行资源校验。重复选择当前条目不刷新选择集合；更新完整选择集合期间，View 在最终通知中统一重建交互层，避免根条目和派生选择属性分别重建。选择本身不修改文档，属性面板复用最近一次校验结果。子部件提交仍通过完整渲染更新内部结构。普通编辑的 Undo/Redo 保留完整布局与行为快照；几何恢复在确认非几何字段与行为未变化后复用设计项和预览树，纯位置/尺寸变更不重建图层列表。非几何比较使用 JSON 元素比较，避免为每个控件构建可变 JSON 树并再次输出字符串；本次校验结果直接供属性网格使用，避免重复全量校验。

图层列表按下鼠标只记录候选条目，点击释放时才选择；超过拖动阈值立即显示指针虚影，拖动期间不加载属性和行为编辑器。目标位置另放一个参与列表布局的半透明条目，两者同时可见；占位仅属于 View，不加入文档、图层模型或撤销历史。进入条目前后、图层空白处或新建顶层/底层区域时更新候选位置，自动滚动时重新命中目标；离开列表、取消或关闭窗口时清理占位。Drop 只提交一次真实移动，保留无关图层节点和视觉实例，调整现有 hitbox 的层级；列表完成落位后通过 Background DispatcherOperation 选择来源控件，该操作在下一次鼠标按下或窗口关闭时取消。

图层重排使用独立的内存历史条目，仅保存移动控件的原 ZIndex、完整控件名顺序及所属 BO 状态，避免在松手时序列化全部控件和行为文档。该历史与普通编辑的 JSON 快照共用撤销/重做栈；跨 BO 状态撤销只写回原状态，不切换当前编辑状态，也不覆盖其他控件内容。

行为动画部件会在 `FrontedV3ControlHost` 外增加装饰 Grid。预览坐标与 ZIndex 更新、重排必须通过 `FrontedEffectHostFactory.ResolveLayoutCarrier` 找到该直接画布子元素，确保图片、边框和动画部件一起移动，撤销后立即恢复。尺寸从显式值恢复为自动时，也必须清除旧的 Width/Height。完整重建预览时，renderer 递归注销旧生成树中的名称，包括动画辅助部件，防止窗口 NameScope 持有已删除的预览树。

选择不读取图片文件，鼠标移动不序列化整份布局/行为、不重建全树；一次提交只调度一次必要工作。复杂内容/子部件变更允许完整渲染，不能为性能跳过插件生命周期或数据恢复。上述预算不是整个进程内存上限。诊断方法见[内存诊断](../build/memory-baseline-and-diagnostics.md)；不保留特定 commit 的一次性性能数字作当前保证。

## ViewModel 代码组织

`FrontedDesignerWindowViewModel` 使用分部类按业务职责维护。主文件 `FrontedDesignerWindowViewModel.cs` 只保留共享状态、构造函数、可观察属性和变更回调；新增业务逻辑应放入对应的 `FrontedDesignerWindowViewModel.*.cs`，不应再回填到主文件。

| 分部文件 | 职责 |
| --- | --- |
| `AnimationParts` | 动画部件编辑与部件图片导入 |
| `LayoutLifecycle` | 布局加载、保存、重载和重置 |
| `ControlsAndTemplates` | 控件增删复制、样式传递和布局模板 |
| `CanvasAndWindowOptions` | Canvas/BO 状态、背景和窗口选项 |
| `SelectionAndLayers` | 选中模型、过滤和图层树 |
| `Geometry` | 移动、缩放、吸附和图层投放 |
| `PropertyEditing` | 属性网格构建、转换、校验与提交 |
| `Resources` | 本地资源引用跟踪与清理 |
| `Behaviors` | 行为文档和预览动画作用域 |
| `ValidationAndPreview` | 校验消息、调度和预览渲染请求 |
| `History` | Undo/Redo 快照、恢复和几何快速修补 |
| `Zoom` | 缩放预设、自适应和手动缩放 |
| `DocumentState` | 设计文档重建和配置深拷贝 |
| `Utilities` | 通用值转换、规范化和窗口显示名解析 |

与 ViewModel 配套的窗口选项、缩放预设、剪贴板 payload 和预览事件参数等类型统一放在 `FrontedDesignerModels.cs`。
