# 前台窗口运行时

本文负责注册、身份、实例管理、显示/隐藏、重载和关闭。布局 JSON 与读取路径见 [Window-centric 契约](fronted-layout-v3-window-centric.md)，编辑交互见[编辑器](fronted-designer-editor.md)，包格式见 [BPUI](bpui-package-v3.md)。前台窗口是供 OBS 捕获的 WPF Window。

## 源码入口

- [App.Services.xaml.cs](../../neo-bpsys-wpf/App.Services.xaml.cs)：内置注册。
- [注册扩展](../../neo-bpsys-wpf.Core/Extensions/Registry/FrontedWindowRegistryExtensions.cs)、[v3 注册扩展](../../neo-bpsys-wpf.Core/Extensions/Registry/FrontedV3LayoutWindowRegistryExtensions.cs)：DI registration。
- [FrontedWindowRegistryService](../../neo-bpsys-wpf.Core/Services/Registry/FrontedWindowRegistryService.cs)：身份索引与可管理/可编辑集合。
- [FrontedWindowService](../../neo-bpsys-wpf/Services/FrontedWindowService.cs)：实例字典、显示状态、重载和透明窗口重建。
- [FrontedWindowBase](../../neo-bpsys-wpf.Core/Controls/FrontedWindowBase.cs)：配置驱动 host、渲染与 behavior attach/detach。

## 注册与身份

内置八个 v3 窗口在 App DI 中以 `AddFrontedV3LayoutWindow(name, isBuiltIn: true)` 注册：Bp、CutScene、ScoreSur、ScoreHun、ScoreGlobal、GameData、BpOverview、MapV2（均有 Window 后缀）。它们没有各自的窗口 XAML。

| 承载方式 | 注册与创建 | Designer / BPUI layout |
| --- | --- | --- |
| Xaml | `AddFrontedWindow<TWindow,TViewModel>()`，窗口带 `[FrontedWindowInfo]`，经 DI 创建 | 不参与 |
| V3Layout | `AddFrontedV3LayoutWindow("WindowId", isBuiltIn: false)`；标准 FrontedWindowBase host | 参与 |

XAML 扩展约束为 Window 与 ViewModelBase；使用 FrontedWindowBase 可获得项目的窗口生命周期行为。插件 PackageId 在 Initialize 注册作用域内由宿主注入。LocalId 必须稳定、安全且非空，不含路径分隔符、冒号、控制字符或前后空白；GUID 推荐但不强制。插件示例及公开 API 见 [SDK](../plugin/plugin-development.md)。

`FrontedWindowRegistration` 基类只暴露 Id、LocalId、PackageId、IsBuiltIn、DisplayName、Kind。Kind 由派生类固定为 Xaml/V3Layout；不承载统一 CLR WindowType，也没有 GroupKey、DisplayOrder 或 I18nDisplayNames。来源分组由 UI 根据 IsBuiltIn 与 PackageId 推导，不应把 UI 排序或本地化重新放入 registration 基类。

| 来源 | Canonical ID |
| --- | --- |
| 内置 / 非插件宿主直接注册 | LocalId（XAML 来自 Attribute ID） |
| 插件 XAML / v3 | `plugin:{PackageId}/{LocalId}`，XAML LocalId 来自 Attribute ID |
| 活动布局包自定义窗口 | `custom:{PackageId}/{WindowId}` |

Id 是运行时字典身份；只有 v3 窗口进入布局、Designer 与 manifest。不要把插件 XAML 的 Attribute ID 当成插件作用域外的完整 Canonical ID。重复身份由 registry 检查。

自定义窗口来自活动包布局 JSON，切包、导入、创建或改名后刷新 registry。切包会关闭并移除旧包作用域的自定义窗口。其三语 DisplayNames、路径及包复制重写规则见[布局契约](fronted-layout-v3-window-centric.md)和 [BPUI](bpui-package-v3.md)。

## 创建与显示

构造 FrontedWindowService 只保存依赖，不预先创建输出窗口。EnsureWindowCreated 按 registration.Kind 分派：v3 创建标准 host；XAML 解析窗口类型并经 DI 创建。FrontedWindows 保存已创建实例，FrontedWindowStates 保存显示状态。

1. ShowWindow 首次仅创建目标窗口；AllWindowShow 才遍历所有 registration。
2. HideWindow、名称查询、动画查找及 dirty 标记不创建窗口。
3. v3 Show 前 await EnsureInitialWindowSettingsAppliedAsync，使尺寸、位置、Topmost、AllowsTransparency、背景及 ViewboxStretch 在 source 初始化前应用。
4. 随后 Show；完整 CanvasSettings、控件、资源与 behavior 由 LoadOrReloadContentAsync 在 Show 后加载。
5. 已渲染且未 dirty 的窗口再次 Show 不重建控件。Hide/Unloaded 导致 behavior detach 时，下次显示只重新 attach。

运行时固定 host 结构和窗口/设计尺寸的关系见[布局契约](fronted-layout-v3-window-centric.md)。不要在普通 Hide/Show 中无条件 RenderToCanvas，或绕过 service 手动管理输出实例。

## 重载、透明度与事件

Designer 保存后标记窗口 dirty 并请求重载。BO3/BO5 切换使已创建 v3 窗口 dirty：可见窗口异步刷新，隐藏窗口下次 Show 刷新。活动包激活会完整刷新已创建窗口的窗口设置、画布、控件、资源和 behavior；不因此创建未打开的窗口。

AllowsTransparency 必须在 WPF source 初始化前设置。Reload 检测到变化时，仅重建对应已创建窗口：

- 可见：发布 hidden，真正关闭并移出字典，创建新实例，Show 前应用新设置，显示后重载并发布 shown。
- 隐藏：真正关闭并移除，下一次 Show 创建新实例。
- 从未创建：无需操作。

背景色、尺寸等可热应用选项不要求重启应用。调用方不能直接在已初始化窗口上设置 AllowsTransparency。

## 关闭与资源收口

FrontedWindowBase 的普通 Closing 取消关闭并 Hide，保留实例以稳定 OBS 捕获关系。service 真正关闭实例时调用 RequestServiceClose 绕过 close-to-hide；不要以普通 Close 代替释放。

隐藏、Unloaded、真正关闭和布局替换都必须协调 behavior 生命周期：过期 Attach 完成后不能复活隐藏/关闭窗口的 host。真正关闭还取消窗口 lifetime、解绑 BO 模式/语言/主题订阅；重载先释放旧视觉树的行为和动画引用。循环、transition 和取消后的清理契约见 [Behavior](fronted-behavior-system.md)。

需要沿源码调试时按[阅读地图](fronted-window-system-deep-dive.md)逐链定位，无需通读全部实现。
