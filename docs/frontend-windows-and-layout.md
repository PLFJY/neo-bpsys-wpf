# 前台窗口与布局

## 前台窗口是什么

“前台窗口”对应代码中的 `FrontedWindow`，是独立 WPF 窗口，用于直播软件捕获。它不是 Web frontend。

内置前台窗口包括 BP、过场、求生者/监管者比分、全局比分、赛后数据、小组件等。OBS 工作流通常是：后台控制数据，前台窗口显示画面，OBS 捕获前台窗口。

## 注册

前台窗口类通过 `FrontedWindowInfo` 标注：

```csharp
[FrontedWindowInfo("窗口 GUID", "窗口显示名称", new[] { "BaseCanvas", "MapBpCanvas|地图 BP" })]
```

注册使用：

```csharp
services.AddFrontedWindow<TView, TViewModel>();
```

`AddFrontedWindow` 会：

1. 读取 `FrontedWindowInfo`。
2. 检查窗口 ID 是否重复。
3. 把信息写入 `FrontedWindowRegistryService.RegisteredWindow`。
4. 以 singleton 注册 ViewModel 和 Window。
5. 构造 Window 后设置 `DataContext`。

插件也使用同一扩展注册前台窗口。

## FrontedWindowService

`FrontedWindowService` 在构造时接收内置窗口实例，然后：

1. 从 `FrontedWindowRegistryService.RegisteredWindow` 注册所有窗口和 Canvas。
2. 注册全局比分控件。
3. 加载插件或宿主注入的外部控件。
4. DEBUG 下记录初始位置。
5. 从配置文件加载元素位置。
6. 订阅共享数据中的 BO3 和全局比分间距变化。

核心状态：

| 字段 | 说明 |
| --- | --- |
| `FrontedWindows` | `windowId -> Window` |
| `FrontedWindowStates` | 窗口是否已显示 |
| `FrontedCanvas` | `(windowId, canvasName)` 列表 |
| `InjectedControls` | 来自 Core 注册表的插件/宿主注入控件 |

## 显示与隐藏

窗口由 `ShowWindow` / `HideWindow` / `AllWindowShow` / `AllWindowHide` 管理。关闭前台窗口时，`FrontedWindowBase.OnClosing` 会取消关闭并改为 `Hide()`，避免窗口实例被销毁后 DI singleton 状态和 OBS 捕获关系变得不可预期。

`FrontedWindowBase` 还会：

1. 自动用 `Viewbox` 包裹内容，让内容按窗口宽高填充。
2. 默认无边框、不可调整大小、居中启动。
3. 非设计模式下支持鼠标拖动窗口。

## 设计者模式和布局文件

设计者模式通过 `DesignBehavior.IsDesignerMode` 给控件加 `CanvasAdorner`，让用户调整 Canvas 内元素位置。

Fronted Designer v3 的基础设施已经存在：`FrontedCanvasConfig` 可读取 root-level 控件 JSON，`IFrontedLayoutService` 按用户布局优先、内置默认布局兜底读取 `%APPDATA%\neo-bpsys-wpf\FrontedLayouts\{WindowTypeName}\{CanvasName}.json` 或 `Resources\FrontedLayouts\{WindowTypeName}\{CanvasName}.json`，`IFrontedRenderer` 可用注册的控件工厂生成 Text/Image 控件。`ScoreSurWindow` 是第一个接入 v3 renderer 的 pilot 窗口，内置默认布局位于 `Resources\FrontedLayouts\ScoreSurWindow\BaseCanvas.json`。其他真实前台窗口仍是 XAML-first；`FrontedWindowService` 不会读取旧 `FrontedDefaultPositions` 作为 v3 输入。

布局文件命名约定：

```text
%APPDATA%\neo-bpsys-wpf\{WindowTypeName}Config-{CanvasName}.json
%APPDATA%\neo-bpsys-wpf\{WindowTypeName}Config-{CanvasName}.default.json   # DEBUG 记录初始位置时可能生成
```

内置默认布局位于：

```text
neo-bpsys-wpf/Resources/FrontedDefaultPositions
```

插件前台窗口默认布局位于插件目录下：

```text
{pluginFolder}/FrontedDefaultPositions/{WindowTypeName}Config-{CanvasName}.default.json
```

恢复默认布局时，服务优先查内置资源；如果不是内置窗口，会通过 `PluginService.FrontedWindowAssemblyFolder` 找到插件目录。

## 插件注入控件

插件可调用：

```csharp
FrontedWindowHelper.InjectControlToFrontedWindow(
    "control-id",
    control,
    FrontedWindowType.BpWindow,
    "BaseCanvas",
    new ElementInfo(width, height, left, top));
```

该调用只是把 `InjectedControlInfo` 放入静态注册表。真正加入 Canvas 发生在 `FrontedWindowService.LoadInjectedControl()`。注入控件会绑定目标 Canvas DataContext 的 `IsDesignerMode`，因此能参与位置编辑。

注意：

1. 注入控件需要稳定、唯一的 ID。
2. 控件应有 `Name`，否则布局保存时不会记录。
3. 布局保存会跳过 `Tag == "nv"` 的元素。
4. 插件新增窗口或注入控件通常需要重启宿主才能进入当前 DI/注册表。

## 透明背景

部分窗口设置支持 `AllowsWindowTransparency`。代码中透明时背景 Brush 返回 `Transparent`，否则回退到默认绿幕色 `#00FF00` 或用户配置色。改前台窗口背景时要同时考虑 OBS 抠色、透明窗口和 WPF 渲染性能。
