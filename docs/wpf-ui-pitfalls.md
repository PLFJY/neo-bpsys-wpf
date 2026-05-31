# WPF-UI 坑点记录

线程和异步相关规则另见 [threading-dispatcher-and-async.md](threading-dispatcher-and-async.md)。资源、字体和本地化规则另见 [resources-localization-and-assets.md](resources-localization-and-assets.md)。

## 导航和 DI

后台页面、前台窗口、ViewModel 和多数服务都通过 DI 注册。不要绕开：

```csharp
services.AddBackendPage<MyPage, MyViewModel>();
services.AddFrontedWindow<MyWindow, MyWindowViewModel>();
```

手动 `new Page()` 或 `new Window()` 会丢失 DataContext、服务注入、注册表信息和 WPF-UI page provider 集成。插件也应使用这些扩展。

## 生命周期

当前注册模式中：

1. 后台页面是 singleton。
2. 后台页面 ViewModel 是 singleton。
3. 前台窗口是 singleton。
4. 前台窗口 ViewModel 是 singleton。
5. 大部分业务服务是 singleton。

这适合导播工具的长期状态，但也意味着页面构造函数、事件订阅和计时器要谨慎。不要在页面构造里做不可重复释放的重操作；如果订阅全局事件，要考虑是否会泄漏或重复处理。

因为页面/ViewModel 是 singleton，构造函数里的事件订阅通常只发生一次；但如果在命令、页面 Loaded、弹窗或临时对象中订阅事件，就要有解绑策略。否则长时间直播中会出现重复响应或对象无法释放。

## 本地化

项目使用 `WPFLocalizeExtension`：

```xaml
Text="{lex:Loc SomeKey}"
```

启动时设置 `LocalizeDictionary.Instance.Culture`，并将 `Application.Current.Resources["CurrentLanguage"]` 设为当前 `XmlLanguage`。

坑点：

1. 新增用户可见文本应优先添加到 `Locales/Lang.resx`、`Lang.en-us.resx`、`Lang.ja-jp.resx`。
2. 后台代码提示文本应通过 `I18nHelper.GetLocalizedString(...)`。
3. 硬编码中文只适合内部日志、临时调试或明确不本地化的标识。
4. 语言切换相关逻辑会监听 `Settings.Language` 和 `CultureInfo`，不要直接改 `CultureInfo` 私有 setter。
5. `I18nHelper` 找不到 key 时会返回 key 本身；如果界面显示类似 `SomeMissingKey`，优先检查 resx 是否缺项或默认字典配置是否正确。

## WPF-UI 图标

后台代码中创建 WPF-UI 图标的安全模式可参考主窗口：

```csharp
new SymbolIcon(SymbolRegular.Info24, 24D)
```

或：

```csharp
new SymbolIcon { Symbol = SymbolRegular.ArrowExit20 }
```

`BackendPageInfo` 的图标字段直接使用 `SymbolRegular`。创建或更新 `SymbolIcon` 属于 WPF UI 对象操作，应在 UI 线程执行。后台下载、OCR、插件市场回调更新 UI 时，参考 `PluginMarketService.RunOnUiThread` 或 `Application.Current.Dispatcher.Invoke`。

不要在后台线程中创建 `NavigationViewItem`、`SymbolIcon`、`Page` 或 `Window` 并交给 UI 绑定集合。

## InfoBar 和 Snackbar

主窗口构造时把控件交给服务：

```csharp
infoBarService.SetInfoBarControl(InfoBar);
snackbarService.SetSnackbarPresenter(SnbPre);
```

页面或服务应通过 `IInfoBarService` / `ISnackbarService` 访问，不要跨层直接找主窗口控件。错误、警告和下载失败等适合 InfoBar；更新后提示使用 Snackbar。

## ObservableCollection

绑定到 UI 的 `ObservableCollection` 必须在 UI 线程更新。插件市场下载队列已经用 Dispatcher 包装；新下载器回调、OCR 回调或捕获回调如果要改集合，应复用同类模式。直接从后台线程 Add/Remove 会导致 WPF 线程异常或间歇性 UI 崩溃。

## 资源字典和主题

启动时默认深色主题，主题变化会更新 `IconThemesDictionary.Theme`。如果新增图标资源或主题资源，需要确认它在浅色/深色下都可用。

`MainWindow.xaml` 大量依赖动态资源和 WPF-UI 样式。修改全局样式前先确认前台窗口是否也引用了同名资源，避免导播输出窗口被后台样式意外影响。

## 前台窗口透明和背景

部分前台窗口支持透明背景。透明时返回 `Transparent`，非透明时默认绿色 `#00FF00`。OBS 场景可能依赖绿幕色或透明窗口，改默认颜色和 `AllowsWindowTransparency` 行为时需要兼顾直播工作流。

`FrontedWindowBase` 会把内容自动包进 `Viewbox`，并设置 `Stretch.Fill`。前台窗口内部布局应以固定画布和绑定宽高为基础，不要假设窗口内容原样作为根元素存在。

Designer v3 的 `Image` 控件有 `Auto`、`FillContainer`、`OverflowCrop` 三种 `SizingMode`。旧 XAML 同时存在 direct fixed-size `ui:Image`、`Border + Image + ClipToBounds`、默认 `Border` 内图片和自定义 `MapV2Presenter`，迁移时必须逐个按旧结构选择模式。队标和 MapBp v1 地图通常用 `FillContainer`；角色裁剪图通常用 `OverflowCrop`；GameData 求生者表头头像这类旧默认 `Image` 应保留 `Auto`。BpWindow 的求生者 pick 使用 `OverflowCrop + UniformToFill`，监管者 pick 保留旧 XAML 中本地 `Stretch="Uniform"` 的效果。`CornerRadius` 只负责圆角裁剪，不应顺手把所有图片改成填满容器。

BpWindow 已由 v3 renderer 生成控件。`AnimationService` 仍依赖 `window.FindName(...)` 查找 `SurPick0..3`、`HunPick`、`SurPickingBorder0..3` 和 `HunPickingBorder`，因此修改 renderer 名称注册或 BpWindow 默认布局时必须保留这些独立命名根元素。不要把 picking border 藏进 pick 图片控件内部；应继续使用 `PickingBorderOverlay`。

## Fronted Designer 编辑器

独立编辑器的详细规格见 [fronted-designer-editor.md](fronted-designer-editor.md)。实现时特别注意这些 WPF 坑点：

1. 编辑器窗口应使用 WPF-UI `FluentWindow` 和项目既有 `CustomTitleBar`，标题栏必须单独占一行。不要把 toolbar、preview 或验证面板放到标题栏同一行；否则关闭按钮会被内容覆盖。编辑器默认隐藏 `CustomTitleBar` 的主题切换按钮，保留最小化、最大化和关闭。
2. 不要把真实前台窗口当作设计 surface。原生标题栏、窗口 chrome 和 `FrontedWindowBase` 的 `Viewbox` 包裹会让坐标混入内容区以外的高度，造成纵向偏移。编辑器应使用纯 `Canvas`，尺寸精确等于 `FrontedCanvasConfig.CanvasWidth` / `CanvasHeight`。
3. Phase 8D zoom/pan 修正后，编辑器预览不再用 `ViewBox` 控制 Fit 或手动缩放。结构应为 `ScrollViewer -> PreviewWorkspace -> PreviewZoomHost -> DesignSurfaceGrid`，`PreviewZoomHost.LayoutTransform` 绑定唯一缩放来源 `ZoomScale`，这样放大后 `ScrollViewer` 才能获得真实可滚动 extent。
4. `PreviewCanvas` 负责真实渲染，`InteractionLayer` 负责 hitbox、选择框、拖拽、缩放和键盘微调。两层尺寸都必须等于 `FrontedCanvasConfig.CanvasWidth` / `CanvasHeight`，鼠标位置使用 `e.GetPosition(InteractionLayer)` 得到逻辑 Canvas 坐标，不要乘除 zoom，也不要把窗口标题栏或真实前台窗口尺寸纳入坐标计算。
5. 透明或空内容控件不可靠。空 `Text`、`Source = null` 的 `Image`、透明 `Border`、初始隐藏的 `PickingBorderOverlay` 和没有当前业务数据的控件都可能难以命中。编辑器应在独立 `InteractionLayer` 为每个设计项创建透明 hitbox；hitbox 是 editor-only，不写入 layout JSON。
6. v3 JSON 的 root-level 控件 key 就是控件名。该名称同时参与 `FrameworkElement.Name` 和 WPF namescope 注册。不要在 config 类里再加 `Name` 字段，也不要让编辑器只改生成控件的 `Name` 而忘记 dictionary key。
7. 空图片和空文本在 preview 中应通过编辑器 overlay 或设计时 placeholder 辅助识别。placeholder 只属于编辑器预览，不应写入 layout JSON 或运行时设置。
8. Phase 8D 的方向键移动步长默认是 `0.5`。键盘事件应避开 `TextBox`、`ComboBox`、`DataGrid` 等编辑控件，避免后续 Property Grid 实现后抢输入焦点。
9. Phase 8D owner validation 后，编辑器主区域是左侧控件列表、中间设计 surface、右侧选中/校验面板。左侧列表用于选中被遮挡或低 ZIndex 控件；筛选文本在切换窗口、切换 Canvas 或成功重载布局时清空。
10. 鼠标选择语义是“单击选择，拖拽不切换选择”。`MouseLeftButtonDown` 只记录候选控件和起点；超过 3-5 logical px 阈值后，只有候选项本来就是当前选中项才开始拖拽。拖到未选中控件上不应改变焦点。
11. 被选中控件的 hitbox、outline 和 handles 会使用 editor-only 高 ZIndex 放在其他 hitbox 上方，以便拖动重叠下层控件。该值不能写入 v3 JSON，也不能改变 preview/runtime `ZIndex`。
12. 拖拽和缩放过程中要同步更新生成 preview root element 的 `Canvas.Left` / `Canvas.Top` / `Width` / `Height`，不要等 mouse-up 重渲染后才看到真实预览移动。mouse-up 可再重渲染一次保证一致。
13. 选择边界优先使用显式 `Width` / `Height`；缺失时使用渲染 root element 的 `ActualWidth` / `ActualHeight`；再不可用才回退到 `40x24`。这对无 `Height` 的文本控件尤其重要。
14. `PickingBorderOverlay` 是 linked runtime overlay：编辑器中不生成普通 hitbox、不进入普通控件列表、不允许直接拖拽或缩放。移动/缩放它的 `TargetControlName` 目标控件时同步 overlay 几何，保持运行时独立命名目标不变。
15. `BanSlotDisplay` 的锁定覆盖层是控件内部视觉层，不是独立设计项。不要把 ban lock overlay 拆成单独控件或单独 hitbox。
16. 视口导航优先于选择：Fit 模式根据 `ScrollViewer` viewport 和 Canvas 尺寸计算 `ZoomScale`；`Ctrl + mouse wheel` 进入手动缩放并保持 25% 到 200%；右键拖拽或 `Space + left mouse drag` 只平移 `ScrollViewer` offset。这些操作不能写回 layout 坐标，也不能改变当前选中控件。
17. Phase 8E 的 Property Grid 基于 `ItemsControl`，编辑的是 `FrontedControlDesignItem` 和其 `Config`。`Name` 仍是设计项/JSON key，不能加到 config 类；运行时关键 `Name` 只读，被其他控件引用的普通控件在 8E 也阻止改名。
18. Phase 8E owner validation 后，Property Grid 行编辑器通过模板按需实例化，不要恢复成“TextBox、CheckBox、ComboBox 全部创建再用 Visibility 隐藏”的结构；否则切换选中控件时会出现未套样式的原生控件闪烁。
19. 属性编辑事件必须避开绑定初始化：ComboBox 用 `DropDownClosed` 提交，TextBox 用 `LostFocus` 或 Enter 提交，CheckBox 用 Click 提交，ColorPicker 只在用户更改颜色后提交。属性网格重建和 layout pass 期间应抑制提交，避免 BpWindow / CutSceneWindow 选中控件时递归触发 `ApplyPropertyEdit`。
20. Phase 8F 的 `FontFamily` 行使用可编辑 ComboBox，初始化、选中项同步和手写提交都必须尊重同一套提交抑制逻辑。内置字体选项保存 pack URI 原值，预览字体时沿用运行时的 `Uri + "./#FontName"` 构造方式，不要把显示名写回布局。
21. Phase 8G 中 `BindingPath` 和图片/资源路径仍是显式提交文本框，但旁边会显示 Binding Browser 或 Resource Browser 的 `...` 按钮。浏览器选择只能写入 `FrontedPropertyEditorItem.EditText`，不能直接调用 `ApplyPropertyEdit`，也不能写入 config、推 Undo snapshot 或刷新真实前台窗口；用户按 Apply 或 Enter 后才提交。颜色字符串使用项目已有 `PortableColorPicker`，仍按 `#AARRGGBB` 存储，并保留文本 fallback；ColorPicker 选色只同步 Hex 编辑缓冲，必须由 Apply 或 Enter 显式提交，避免初始化/选色时绕过显式提交模型。
22. 验证详情表不在右侧属性面板常驻显示；右侧应主要保留选中控件摘要和 Property Grid。底部左侧验证摘要可点击打开非模态验证详情窗口。
23. 拖拽和缩放 live edit 中不要运行完整校验、不要重建 Property Grid、不要强制完整重渲染。只更新几何、linked overlay、preview element、hitbox/adorner、选中几何摘要和 dirty 状态；mouse-up/commit 后再统一校验和刷新。
24. Property Grid 输入控件获得键盘焦点时，方向键不应触发设计 surface 微调。新增编辑器控件后要继续更新 `ShouldIgnoreKeyboardInput()` 的排除列表。
25. Phase 8F 的 Add Control 只添加内存设计项并重渲染编辑器 preview，不保存用户布局。新控件应放在当前滚动视口中心附近，并避免普通菜单暴露 `PickingBorderOverlay`。
26. Phase 8F owner validation 后，Delete Control 只在设计 surface 焦点下响应 Delete 键；焦点位于 `TextBox`、`ComboBox`、`DataGrid`、ColorPicker 或属性编辑器内部时必须忽略，避免编辑文本时误删控件。左侧控件列表右键菜单和 Property Grid 底部删除按钮都应调用同一个删除命令，继续复用运行时关键控件和 incoming reference 的删除保护。
27. `Name`、`BindingPath` 和普通文本/资源路径属性使用显式提交：文本框绑定 `EditText`，按 Enter 或 Check/Apply 按钮提交。Enter 处理必须直接读取 `TextBox.Text`，不能依赖 `UpdateSourceTrigger=LostFocus` 后的 `Value`，否则会提交旧值或空值。
28. 属性编辑失败时不要重建到丢失输入。应保留 `EditText`、设置 `HasEditError` / `EditError`、显示红色边框和行内错误消息；失败提交不应触发 preview render。
29. `FontFamily` 的可编辑 ComboBox 不应在下拉打开时由 LostFocus 触发提交。下拉选择保存 `FrontedFontFamilyOption.Value`，手写字体按 Enter 或真正失焦保存 `ComboBox.Text`，内置字体 pack URI 不能被显示名替换。
30. 右侧 Property Grid 面板通过中间 `GridSplitter` 调整宽度。拖动 splitter 只改变编辑器窗口布局，不写回 v3 layout JSON，也不需要在 Phase 8F 持久化。
31. 设计器 preview 使用独立 `DesignerPreviewSharedDataService`，只通过 `FrontedRenderContext.SharedDataServiceOverride` 传给 renderer。不要为了预览调用真实共享数据服务的 `NewGame()` 或修改真实 `CurrentGame`，否则会污染导播运行时状态。
32. Undo/Redo 快捷键只在设计 surface、列表或编辑器背景获得焦点时执行布局撤销/重做；焦点在 `TextBox`、`ComboBox`、ColorPicker 等属性编辑器内时必须让控件自身处理文本撤销。切换窗口/Canvas 或 reload 必须清空 undo/redo 栈。
33. Binding Browser 使用 curated `ISharedDataService` 树和固定常用集合索引，不应对任意对象做无边界深反射。绑定树节点必须保留真实 `ValueType`，树过滤和搜索都要使用同一 `FrontedBindingTypeFilter`：文本控件只允许字符串/数字，图片控件只允许 `ImageSource` 兼容值，`GameProgressText` 只允许 `GameProgress`，`MapNameText` 只允许 `Map` / `Map?`。浏览器选择仍只能写入属性行 `EditText`，不能绕过 Apply/Enter 直接提交 config。Resource Browser 读取 `Resources/bpui` 时缩略图必须用 `BitmapImage.CacheOption=OnLoad` 等方式避免锁文件；外部绝对路径只引用原文件，Phase 8G 不复制到用户布局目录或 `.bpui` 包。

## 插件 UI

插件注册的页面/窗口也进入同一 DI 和 WPF UI 环境。插件作者应：

1. 使用 `BackendPageInfo` / `FrontedWindowInfo`。
2. 避免和宿主或其他插件重复 ID。
3. 注入控件时给控件稳定 `Name` 和合理默认 `ElementInfo`。
4. 用户可见文本同样考虑本地化。

## NavigationView

WPF-UI 默认的 `NavigationView` 样式和行为有坑点。Page被加载后外面会自动包一层 `ScrollViewer`，不要重复包裹，否则就导致页面无法滚动
