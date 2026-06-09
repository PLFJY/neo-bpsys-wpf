任务：Fronted Layout v3 Window-centric 大重构
仓库：PLFJY/neo-bpsys-wpf
分支：dev-refactor/Designer-v3
## 背景
当前 v3 前台布局还是 Window + 多 Canvas 架构，尤其 WidgetsWindow 内有 MapBpCanvas / BpOverViewCanvas / MapV2Canvas。现在要改成 Window-centric：以后 v3 前台布局只以 Window 为管理单位，不再管理 Canvas。
3.0 仍在开发阶段，不需要兼容旧 v3 JSON。  
但 Legacy .bpui / 旧 Config.json 转换必须继续支持，并输出新格式。
## 核心规则
1. v3 layout 管理单位只有 Window。
2. 每个 v3 layout window 必须有且只有一个内部 BaseCanvas。
3. BaseCanvas 只是实现细节，不出现在用户概念、文件路径、FrontManagePage、包管理中。
4. 传统固定 XAML Window 不强制 BaseCanvas。
5. 删除 WidgetsWindow。
6. WidgetsWindow 拆分：
   - MapBpCanvas / MapV1：删除，不保留窗口。
   - BpOverViewCanvas：变成独立 v3 window，建议 `BpOverviewWindow`。
   - MapV2Canvas：变成独立 v3 window，建议 `MapV2Window`。
7. 删除所有 MapV1 相关代码、布局、资源、测试。
8. 保留 MapV2 相关代码。
9. 不为 BpOverviewWindow / MapV2Window 创建独立 XAML。
10. 不新增 FrontedWindowBaseNew。
11. 直接改现有 FrontedWindowBase，让它支持配置驱动 v3 layout host。
12. v3 host 结构：
   ```text
   FrontedWindowBase
     -> ViewBox
        -> Canvas BaseCanvas

13. WindowSettings 应用于 Window。
14. CanvasSettings 应用于 BaseCanvas。
15. ControlLayout 交给 FrontedRenderer 渲染。
16. ViewBox 负责缩放，控件坐标不随窗口大小变化而重写。
17. BehaviorRuntime 按 WindowTypeName 绑定，不再按 CanvasName 管理。
18. 如果内部必须保留 CanvasName，只允许固定常量 BaseCanvas。
19. FrontManagePage 不再管理 Canvas，直接从 Registry 获取可管理 Window。
20. 窗口分组由 Registry descriptor 提供，沿用现有分组规则。
21. Legacy converter 必须输出新 FrontedWindowConfig。
22. Legacy WidgetsWindow 转换：

* MapBpCanvas / MapV1：跳过并记录 Info。
* BpOverViewCanvas：转 BpOverviewWindow。
* MapV2Canvas：转 MapV2Window。

23. MapV1 跳过不能导致导入失败。

新 Config

新增/重构为：

FrontedWindowConfig
{
    Version,
    WindowSettings,
    CanvasSettings,
    ControlLayout
}

WindowSettings：

WindowWidth
WindowHeight
AllowsTransparency
BackgroundColor
Topmost
ViewboxStretch

注意：BackgroundColor 属于 WindowSettings，不属于 CanvasSettings。

CanvasSettings：

CanvasWidth
CanvasHeight
BackgroundImage
EnableBoModeStates
BoModeStates

CanvasSettings 不要有 BackgroundColor。Canvas 内纯色背景用 Rectangle / Shape 控件实现。

ControlLayout：

RequiredPlugins
Controls

JSON 示例：

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
    "BackgroundImage": null,
    "EnableBoModeStates": false,
    "BoModeStates": {}
  },
  "ControlLayout": {
    "RequiredPlugins": [],
    "Controls": {}
  }
}

新路径

删除旧路径：

FrontedLayouts/{WindowTypeName}/{CanvasName}.json

使用新路径：

FrontedLayouts/{WindowTypeName}.json
behaviors/{WindowTypeName}.behaviors.json

插件默认布局：

Plugins/{PackageId}/FrontedLayouts/{WindowTypeName}.json

包内：

FrontedLayouts/{WindowTypeName}.json
behaviors/{WindowTypeName}.behaviors.json
resources/...
manifest.json

需要重点改的部分

LayoutService

把主 API 改成 Window 级：

LoadWindowConfigAsync(windowTypeName)
LoadWindowConfigWithMetadataAsync(windowTypeName)
SaveWindowConfigAsync(windowTypeName, FrontedWindowConfig)
DeleteUserWindowLayoutAsync(windowTypeName)
UserWindowLayoutExists(windowTypeName)
GetUserWindowLayoutPath(windowTypeName)
LoadBuiltInDefaultWindowLayoutAsync(windowTypeName)
GetBuiltInDefaultWindowLayoutPath(windowTypeName)
GetPluginDefaultWindowLayoutPath(pluginFolder, windowTypeName)

旧 Canvas API 最终调用点全部迁到 Window API。不要保留 Canvas 路径主逻辑。

FrontedWindowBase

改现有 FrontedWindowBase，不新增 FrontedWindowBaseNew。

它要支持两种模式：

1. 传统 XAML window：保持原行为。
2. v3 layout host：后台创建 ViewBox + BaseCanvas，加载 FrontedWindowConfig，渲染控件并 attach behavior runtime。

Registry / FrontedWindowService

Registry descriptor 需要能表达：

WindowId
WindowTypeName
FullWindowType
DisplayNameKey
GroupKey
DisplayOrder
IsVisibleInFrontManage
IsV3LayoutWindow
Customizable
Kind

FrontedWindowService：

* 传统 XAML window 继续按原逻辑创建。
* Built-in v3 layout window 不需要 XAML，创建 FrontedWindowBase host。
* PluginLayout window 尽量共用同一套 host。
* 删除 WidgetsWindow 创建路径。
* 新增 BpOverviewWindow / MapV2Window descriptor。

FrontManagePage

改为：

registry.GetManageableWindows()
-> 按 GroupKey 分组
-> 按 DisplayOrder 排序
-> 显示 Window 卡片

不再显示 Canvas / BaseCanvas。

Designer v3

Designer 只选择 Window，不选择 Canvas。

删除：

* Canvas selector
* Canvas list
* Window + Canvas 双层管理 UI
* CanvasName 用户概念

新增/调整三块：

1. WindowSettings
2. CanvasSettings
3. ControlLayout

Preview 用 ViewBox + BaseCanvas。保存 FrontedWindowConfig 和 window-level behavior 文件。

Renderer / Behavior / Animation

Renderer 输入改为 FrontedWindowConfig 或 ControlLayout + CanvasSettings。
BehaviorService 路径改为 window-level。
BehaviorRuntime context 改为 Window scope。
TargetResolver 继续从 BaseCanvas root 搜索 BehaviorGuid。

WidgetsWindow / MapV1

删除：

* WidgetsWindow.xaml
* WidgetsWindow.xaml.cs
* WidgetsWindowViewModel，如只服务 WidgetsWindow
* FrontedWindowType.WidgetsWindow
* WidgetsWindow 注册和入口
* MapBpCanvas
* MapV1 相关服务、控件、布局、资源、测试

保留：

* MapV2 相关
* BpOverview 相关

新增：

* BpOverviewWindow descriptor
* MapV2Window descriptor

不要新增：

* BpOverviewWindow.xaml
* MapV2Window.xaml

Legacy .bpui converter

Legacy 输入继续支持，输出 FrontedWindowConfig。

映射：

* 旧窗口尺寸 / 透明 / 背景色 -> WindowSettings
* 旧 Canvas 宽高 / 背景图 / BO 状态 -> CanvasSettings
* 旧控件布局 / RequiredPlugins -> ControlLayout
* 资源继续复制，路径转 bpui://
* TextSettings 迁移保留
* GlobalScoreRow 聚合保留
* BO5 overtime 消费保留
* Missing plugin placeholder 规则保留

WidgetsWindow legacy：

* MapBpCanvas / MapV1：跳过，记录 Info。
* BpOverViewCanvas：转 BpOverviewWindow.json。
* MapV2Canvas：转 MapV2Window.json。

PluginLayout

旧：

Plugins/{PackageId}/FrontedLayouts/{WindowTypeName}/{CanvasName}.json

新：

Plugins/{PackageId}/FrontedLayouts/{WindowTypeName}.json

缺插件窗口 / 控件规则不变：保留数据，不删除。

测试重点

不要写大量 full UI 测试，优先模型 / 服务 / 轻量 WPF。

必须覆盖：

* FrontedWindowConfig 默认值和 JSON roundtrip
* BackgroundColor 属于 WindowSettings
* CanvasSettings 没有 BackgroundColor
* LayoutService 使用一级路径
* Built-in / package / plugin path 都是 {WindowTypeName}.json
* v3 host 创建 ViewBox + BaseCanvas
* v3 host 应用 WindowSettings / CanvasSettings
* v3 host 渲染控件并 attach/detach behavior runtime
* Registry 返回 v3 layout windows
* FrontManagePage 从 registry 分组，不暴露 Canvas
* WidgetsWindow 从 registry 删除
* BpOverviewWindow / MapV2Window 注册为 v3 layout
* MapV1 不注册
* behavior path 是 window-level
* Legacy WidgetsWindow 转换到 BpOverviewWindow / MapV2Window
* Legacy MapV1 被跳过并记录 Info
* bpui import/export 使用新结构

文档

更新 docs，写明：

* v3 layout 改成 Window-centric
* Canvas 不再是管理单位
* 每个 v3 layout window 固定 BaseCanvas
* WindowSettings / CanvasSettings / ControlLayout
* BackgroundColor 在 WindowSettings
* 新路径 FrontedLayouts/{WindowType}.json
* 新 behavior 路径 behaviors/{WindowType}.behaviors.json
* WidgetsWindow 删除
* MapV1 删除
* BpOverviewWindow / MapV2Window 是配置驱动窗口
* v3 layout window 不需要独立 XAML
* FrontedWindowBase 支持配置驱动 host
* Legacy .bpui 转换规则

建议实施顺序

1. 新模型
2. LayoutService / UserLayoutStore / PackageManager 路径和 API
3. BehaviorService window-level path
4. Renderer 输入和 RenderContext
5. FrontedWindowBase v3 host
6. Registry descriptor
7. FrontedWindowService 创建 v3 host
8. BpOverviewWindow / MapV2Window descriptor
9. 删除 WidgetsWindow / MapV1
10. FrontManagePage registry-driven
11. Designer Window-only UI
12. behavior panel / animation editor scope
13. bpui import/export
14. Legacy converter
15. PluginLayout 默认路径
16. 测试
17. 文档
18. build/test

验收标准

1. v3 layout 管理单位只剩 Window。
2. FrontManagePage 不再显示/管理 Canvas。
3. Designer 不再选择 Canvas。
4. 每个 v3 layout window 有唯一 BaseCanvas。
5. WindowSettings / CanvasSettings / ControlLayout 正确保存。
6. BackgroundColor 在 WindowSettings。
7. CanvasSettings 没有 BackgroundColor。
8. FrontedLayouts 使用 {WindowType}.json。
9. behaviors 使用 {WindowType}.behaviors.json。
10. WidgetsWindow 删除。
11. MapV1 删除。
12. BpOverviewWindow / MapV2Window 通过 descriptor + FrontedWindowBase host 工作。
13. 不存在 BpOverviewWindow.xaml / MapV2Window.xaml。
14. FrontedWindowBase 仍支持传统 XAML window。
15. Renderer 能渲染 BaseCanvas。
16. BehaviorRuntime 能在 Window scope 下工作。
17. ViewBox 缩放窗口时控件坐标不被重写。
18. bpui import/export 使用新结构。
19. Legacy .bpui 能转换到新 FrontedWindowConfig。
20. Legacy MapV1 被跳过并记录 Info，不导致导入失败。
21. PluginLayout 使用新路径。
22. build 通过。
23. 关键测试通过。