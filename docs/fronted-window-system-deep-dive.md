# 前台窗口系统深度解析

本文档从源码层面完整覆盖前台窗口（Fronted Window）的：启动链路、注册链路、窗口创建链路、窗口管理逻辑、窗口配置读取逻辑、窗口配置应用逻辑、窗口生命周期管理。

---

## 1. 术语与角色

| 角色 | 类 / 接口 | 职责 |
|---|---|---|
| 窗口描述符 | `IFrontedWindowDescriptor` | 窗口的元数据：ID、类型名、布局标识、提供方式 |
| 内置窗口描述符 | `FrontedBuiltInWindowDescriptor` | 宿主内置窗口的描述符实现，`FullWindowType` 为窗口类型名（如 `BpWindow`） |
| 插件窗口描述符 | `FrontedPluginWindowDescriptor` | 插件贡献窗口的描述符实现，`FullWindowType` 为 `plugin:{PackageId}/{WindowTypeName}` |
| 窗口注册表 | `IFrontedWindowRegistry` / `FrontedWindowRegistryService` | 收集并索引所有已知窗口描述符 |
| 窗口管理器 | `IFrontedWindowService` / `FrontedWindowService` | 窗口实例的创建、显示、隐藏、布局重载 |
| 窗口基类 | `FrontedWindowBase` | v3 布局宿主的 WPF 基类，管理布局加载、BehaviorRuntime 附加/分离 |
| 布局配置服务 | `IFrontedLayoutService` / `FrontedLayoutService` | 读取/保存窗口级 v3 布局 JSON 配置 |
| 窗口选项服务 | `IFrontedWindowLayoutOptionsService` / `FrontedWindowLayoutOptionsService` | 仅用于非 v3 / legacy XAML 窗口选项；v3 layout window 使用 `FrontedWindowConfig.WindowSettings` |
| 管理页 ViewModel | `FrontManagePageViewModel` | 用户在后台管理前台窗口的入口 |

### 窗口类型枚举：FrontedWindowType

在 [FrontedWindowType.cs](../neo-bpsys-wpf.Core/Enums/FrontedWindowType.cs) 中定义：

| 枚举值 | 对应窗口 | GUID |
|---|---|---|
| `BpWindow` | BP 展示窗口 | `ACFC0F23-83F4-4607-B473-24D7DB292D23` |
| `CutSceneWindow` | 过场动画窗口 | `8716A6DB-3DEC-4D45-966B-ECD202DCFB0C` |
| `ScoreGlobalWindow` | 全局比分窗口 | `3A4F66F7-BAC7-47AF-AC45-11657C50F7DD` |
| `ScoreSurWindow` | 求生者比分窗口 | `4ED64F79-E47C-490D-B86A-AE396F279889` |
| `ScoreHunWindow` | 监管者比分窗口 | `EA69B342-DDA6-4394-BDFD-13368D76A6BA` |
| `GameDataWindow` | 游戏数据窗口 | `25378080-2085-4121-BE9A-94E987455CEC` |
| `BpOverviewWindow` | BP 总览窗口（纯 v3 布局） | `3F6AD6CC-9271-4FFB-A98A-91771F86C27F` |
| `MapV2Window` | 地图 v2 窗口（纯 v3 布局） | `9898D1EF-6E45-4968-8B18-2016389E4C3E` |

GUID 映射见 [FrontedWindowHelper.cs](../neo-bpsys-wpf.Core/Helpers/FrontedWindowHelper.cs)。

### 窗口提供方式枚举：FrontedWindowKind

在 [FrontedWindowKind.cs](../neo-bpsys-wpf.Core/Models/FrontedLayout/FrontedWindowKind.cs) 中定义：

| 枚举值 | 含义 |
|---|---|
| `BuiltIn` | 宿主内置窗口，布局存储在内置路径 |
| `PluginXaml` | 插件提供的纯 XAML 窗口，不可在 Designer 中编辑 |
| `PluginLayout` | 插件提供的 v3 布局窗口，可在 Designer 中编辑 |

---

## 2. 启动链路

启动链路描述从应用程序启动到前台窗口可用（窗口实例已创建并注册到字典）的完整过程。

### 2.1 DI 注册环节（`App.Services.xaml.cs` → `ConfigureServices`）

[App.Services.xaml.cs](../neo-bpsys-wpf/App.Services.xaml.cs) 中：

```csharp
// 第 65-66 行：注册前台窗口管理器
services.AddSingleton<IFrontedWindowService, FrontedWindowService>();

// 第 171-176 行：注册 6 个内建前台窗口
services.AddFrontedWindow<BpWindow, BpWindowViewModel>();
services.AddFrontedWindow<CutSceneWindow, CutSceneWindowViewModel>();
services.AddFrontedWindow<ScoreGlobalWindow, ScoreWindowViewModel>();
services.AddFrontedWindow<ScoreSurWindow, ScoreWindowViewModel>();
services.AddFrontedWindow<ScoreHunWindow, ScoreWindowViewModel>();
services.AddFrontedWindow<GameDataWindow, GameDataWindowViewModel>();
```

同时注册了相关的依赖服务（共约 80+ 行相关服务）：

- `IFrontedWindowRegistry` → `FrontedWindowRegistryService`
- `IFrontedLayoutService` → `FrontedLayoutService`
- `IFrontedWindowLayoutOptionsService` → `FrontedWindowLayoutOptionsService`
- `IFrontedBehaviorRuntime` → `FrontedBehaviorRuntime`
- `IFrontedLayoutPackageManager` → `FrontedLayoutPackageManager`
- 等

### 2.2 `AddFrontedWindow<TView,TViewModel>()` 扩展方法

在 [FrontedWindowRegistryExtensions.cs](../neo-bpsys-wpf.Core/Extensions/Registry/FrontedWindowRegistryExtensions.cs) 中：

1. 从 `TView` 类型的 `[FrontedWindowInfo]` 特性中提取注册信息
2. 检查 `FrontedWindowRegistryService.RegisteredWindow` 静态列表中是否已存在相同 ID
3. 将 `FrontedWindowInfo` 加入 `RegisteredWindow` 静态列表（同时设置 `info.WindowType = typeof(TView)`）
4. 向 DI 容器注册：
   - `services.AddSingleton<TViewModel>()`
   - `services.AddSingleton<TView>(sp => { ... })` — 工厂创建，显式设置 `DataContext`

```csharp
// 核心代码示意
info.WindowType = type;
FrontedWindowRegistryService.RegisteredWindow.Add(info);
services.AddSingleton<TViewModel>();
services.AddSingleton<TView>(sp => {
    var view = ActivatorUtilities.CreateInstance<TView>(sp);
    view.DataContext = sp.GetRequiredService<TViewModel>();
    return view;
});
```

### 2.3 `FrontedWindowRegistryService` 构造

当 DI 容器解析 `IFrontedWindowRegistry` 时，`FrontedWindowRegistryService` 的构造函数执行核心构建：

1. **内置窗口收集**（在 [FrontedWindowRegistryService.cs](../neo-bpsys-wpf.Core/Services/Registry/FrontedWindowRegistryService.cs)）：
   - 从 `RegisteredWindow` 静态列表（由 `AddFrontedWindow` 填充）中排除 `WidgetsWindow`
   - 通过 `FrontedBuiltInWindowDescriptor.FromInfo(info)` 转换为描述符
   - 追加 `GetAdditionalBuiltInV3Windows()` 返回的两个纯 v3 布局窗口（`BpOverviewWindow`、`MapV2Window`）

2. **插件窗口收集**：
   - 遍历注入的 `IFrontedWindowPluginContributor` 集合
   - 对每个贡献者调用 `GetFrontedWindows()` 获取描述符列表
   - 通过 `pluginMetadataProvider?.TryGetPluginFolder()` 解析插件文件夹
   - 调用 `descriptor.Validate(pluginFolder)` 验证（检查 GUID 有效性、XAML 窗口的 WindowType、PluginLayout 的默认布局文件存在性）
   - 验证失败的描述符被记录警告并跳过

3. **构建索引字典**：
   - `_byWindowId`：`Dictionary<string, IFrontedWindowDescriptor>` — 按 `WindowId`（GUID）索引
   - `_byFullWindowType`：按 `FullWindowType`（内置为类型名，插件为 `plugin:{PackageId}/{WindowTypeName}`）索引
   - 重复或空 key 被跳过并记录警告

### 2.4 `FrontedWindowService` 构造与窗口预创建

`FrontedWindowService`（在 [FrontedWindowService.cs](../neo-bpsys-wpf/Services/FrontedWindowService.cs)）被注册为 `Singleton`，在其构造函数中立即调用私有 `RegisterFrontedWindow()` 方法：

```csharp
public FrontedWindowService(...)
{
    // 确保 AppData 目录存在
    if (!Directory.Exists(AppConstants.AppDataPath))
        Directory.CreateDirectory(AppConstants.AppDataPath);
    
    RegisterFrontedWindow();  // ← 预创建所有窗口
}

private void RegisterFrontedWindow()
{
    foreach (var descriptor in _windowRegistry.GetWindows())
    {
        var window = CreateWindow(descriptor);
        if (window is null) continue;
        FrontedWindows.TryAdd(descriptor.WindowId, window);
        FrontedWindowStates[descriptor.WindowId] = false;
    }
}
```

**关键点**：所有前台窗口实例在 `FrontedWindowService` 构造时（即应用程序启动早期）就已全部创建完毕，但状态初始化为 `false`（隐藏）。窗口实例在 `FrontedWindows` 字典中以 `WindowId`（GUID）为 key 管理。

v3 移除了旧 `RegisterFrontedWindowAndCanvas` 公开 API，外部注册必须通过 `FrontedWindowInfo` + `AddFrontedWindow<TView,TViewModel>()`、`IFrontedWindowPluginContributor` 或 registry descriptor，不允许通过 `IFrontedWindowService` 手动塞入 Window 实例。

### 2.5 启动链路总结

```
App.Services.xaml.cs
  └─ services.AddFrontedWindow<TView,TViewModel>()
      ├─ 从 [FrontedWindowInfo] 特性读取元数据
      ├─ 检查 ID 唯一性
      ├─ 加入 RegisteredWindow 静态列表
      ├─ 注册 TViewModel 到 DI
      └─ 注册 TView 到 DI（工厂创建，设置 DataContext）
  └─ services.AddSingleton<IFrontedWindowService, FrontedWindowService>()
      └─ new FrontedWindowService()
          ├─ 获取 IFrontedWindowRegistry 实例
          │   └─ new FrontedWindowRegistryService(pluginContributors, ...)
          │       ├─ 从 RegisteredWindow 构建内置描述符
          │       ├─ 从 pluginContributors 收集插件描述符
          │       ├─ 验证插件描述符
          │       └─ 构建 byWindowId / byFullWindowType 索引
          ├─ 注册目录存在性检查
          └─ RegisterFrontedWindow()
              └─ 遍历 _windowRegistry.GetWindows()
                  └─ CreateWindow(descriptor)
                      ├─ IsV3LayoutWindow → new FrontedWindowBase().InitializeV3LayoutHost(...)
                      ├─ BuiltInDescriptor → DI 创建 XAML 窗口
                      ├─ PluginXaml → DI 创建插件 XAML 窗口
                      └─ 注册到 FrontedWindows 字典
```

---

## 3. 注册链路

注册链路是指窗口描述符如何被注册到系统中的完整过程。分为**内置窗口注册**和**插件窗口注册**两条路径。

### 3.1 内置窗口注册

通过 `AddFrontedWindow<TView,TViewModel>()` 扩展方法分两步完成：

**Step 1 — 编译期**：在窗口类上应用 `[FrontedWindowInfo]` 特性：

```csharp
[FrontedWindowInfo("ACFC0F23-83F4-4607-B473-24D7DB292D23", "BpWindow", true)]
public partial class BpWindow : FrontedWindowBase { ... }
```

`FrontedWindowInfo` 属性（在 [FrontedWindowInfo.cs](../neo-bpsys-wpf.Core/Attributes/FrontedWindowInfo.cs) 中定义）：

| 属性 | 含义 |
|---|---|
| `Id` | GUID 字符串，运行时唯一标识 |
| `Name` | 窗口类型名（如 `BpWindow`） |
| `WindowType` | `internal set` — 由 `AddFrontedWindow` 在注册时设置 |
| `IsBuiltIn` | 是否内置 |
| `Canvas` | 旧版画布元数据（v3 仅用 `BaseCanvas`） |

**Step 2 — 运行时**：`AddFrontedWindow` 调用时将 `FrontedWindowInfo` 加入 `FrontedWindowRegistryService.RegisteredWindow` 静态列表。

### 3.2 纯 v3 布局窗口注册

在 `FrontedWindowRegistryService.GetAdditionalBuiltInV3Windows()` 中硬编码了两个没有 XAML 视图的纯 v3 布局窗口：

- `BpOverviewWindow` — Id: `3F6AD6CC-9271-4FFB-A98A-91771F86C27F`
- `MapV2Window` — Id: `9898D1EF-6E45-4968-8B18-2016389E4C3E`

它们没有对应的 XAML 文件，由 `FrontedWindowBase` 基类动态创建。

### 3.3 插件窗口注册

插件通过实现 `IFrontedWindowPluginContributor` 接口来贡献窗口：

```csharp
public interface IFrontedWindowPluginContributor
{
    IEnumerable<FrontedPluginWindowDescriptor> GetFrontedWindows();
}
```

插件在 DI 注册环节通过扩展方法注册：

```csharp
services.AddFrontedWindowPluginContributor<TContributor>();
// 等效于 services.AddSingleton<IFrontedWindowPluginContributor, TContributor>();
```

插件窗口描述符 `FrontedPluginWindowDescriptor` 的核心属性：

| 属性 | 含义 |
|---|---|
| `PackageId` | 插件包 ID |
| `WindowId` | 稳定 GUID |
| `WindowTypeName` | 插件本地窗口类型名 |
| `FullWindowType` | 自动计算为 `plugin:{PackageId}/{WindowTypeName}` |
| `Kind` | `PluginXaml` 或 `PluginLayout` |
| `WindowType` | `PluginXaml` 时需要，指向 WPF Window 类型 |
| `DefaultLayoutRoot` | 默认布局文件根目录（默认为 `FrontedLayouts`）|
| `AllowBlankDefaultLayout` | `PluginLayout` 是否允许空默认布局 |

### 3.4 注册信息查询

`IFrontedWindowRegistry` 提供以下查询方法：

| 方法 | 用途 |
|---|---|
| `GetWindows()` | 获取所有已接受窗口 |
| `GetCustomizableLayoutWindows()` | 获取可在 Designer v3 中编辑的窗口 |
| `GetManageableWindows()` | 获取在后台管理页中显示的窗口（带排序和分组） |
| `TryGetByWindowId(windowId)` | 按 WindowId（GUID）查找 |
| `TryGetByFullWindowType(fullWindowType)` | 按布局标识查找 |
| `GetPluginWindows()` | 仅获取插件窗口 |
| `GetBuiltInWindows()` | 仅获取内置窗口 |

---

## 4. 窗口创建链路

窗口创建发生在 `FrontedWindowService` 构造时的 `RegisterFrontedWindowAndCanvas()` 中，在 `CreateWindow(descriptor)` 方法中根据描述符类型进行分派。

### 4.1 创建分派逻辑

```csharp
private Window? CreateWindow(IFrontedWindowDescriptor descriptor)
{
    return descriptor switch
    {
        { IsV3LayoutWindow: true }          => CreateV3LayoutHostWindow(descriptor),
        FrontedBuiltInWindowDescriptor bi   => CreateXamlWindow(bi.WindowType, null),
        FrontedPluginWindowDescriptor { Kind: PluginXaml } p =>
            CreateXamlWindow(p.WindowType, p.ViewModelType),
        _ => null
    };
}
```

### 4.2 v3 布局宿主窗口创建（`CreateV3LayoutHostWindow`）

```csharp
private Window CreateV3LayoutHostWindow(IFrontedWindowDescriptor descriptor)
{
    var window = new FrontedWindowBase();
    window.InitializeV3LayoutHost(
        descriptor,
        _services.GetRequiredService<IFrontedLayoutService>(),
        _services.GetRequiredService<IFrontedRenderer>(),
        _services.GetRequiredService<ISharedDataService>(),
        _services.GetService<IFrontedBehaviorRuntime>(),
        _logger);
    return window;
}
```

在 `FrontedWindowBase.InitializeV3LayoutHost()`（在 [FrontedWindowBase.cs](../neo-bpsys-wpf.Core/Controls/FrontedWindowBase.cs)）中：

1. **存储依赖**：保存 descriptor、layoutService、renderer、sharedDataService、behaviorRuntime、logger
2. **设置标志**：`_isV3LayoutHost = true`
3. **设置标题**：使用 `descriptor.DisplayName`
4. **创建 BaseCanvas**：一个 `Canvas` 控件，Background 为 Transparent
5. **包装到 Viewbox**：`Content = new Viewbox { Child = _baseCanvas }`，实现自适应缩放
6. **订阅事件**：`Loaded`, `Unloaded`, `Closed`, `IsVisibleChanged`

窗口基础样式设置：
```csharp
public FrontedWindowBase()
{
    MouseLeftButtonDown += OnMouseLeftButtonDown;  // 支持拖拽
    ResizeMode = ResizeMode.NoResize;
    SizeToContent = SizeToContent.Manual;
    WindowStyle = WindowStyle.None;                 // 无边框
    WindowStartupLocation = WindowStartupLocation.CenterScreen;
}
```

### 4.3 XAML 窗口创建（`CreateXamlWindow`）

适用于有 XAML 视图的内置窗口和 PluginXaml 类型窗口：

```csharp
private Window? CreateXamlWindow(Type? windowType, Type? viewModelType)
{
    if (windowType is null || !typeof(Window).IsAssignableFrom(windowType))
        return null;

    var window = (_services.GetService(windowType)       // 优先从 DI 获取
                  ?? ActivatorUtilities.CreateInstance(_services, windowType)) as Window;
    if (window is null) return null;

    if (viewModelType is not null)
        window.DataContext = _services.GetService(viewModelType)
                            ?? ActivatorUtilities.CreateInstance(_services, viewModelType);
    return window;
}
```

对于通过 `AddFrontedWindow` 注册的内置 XAML 窗口，`windowType` 已注册为 Singleton 到 DI 容器中，所以 `_services.GetService(windowType)` 会返回之前工厂创建的实例（其 DataContext 已在工厂中设置）。

### 4.4 窗口创建后处理

创建完成后，将窗口加入字典：

```csharp
if (FrontedWindows.TryAdd(windowId, window))
{
    FrontedWindowStates[windowId] = false;  // 初始状态：隐藏
}
```

### 4.5 创建链路总结

```
CreateWindow(descriptor)
│
├─ IsV3LayoutWindow == true ──────────────────────────┐
│   └─ CreateV3LayoutHostWindow()                     │
│       └─ new FrontedWindowBase()                    │
│           ├─ 设置无边框、不可调整大小、居中          │
│           └─ 绑定鼠标拖拽事件                        │
│       └─ .InitializeV3LayoutHost(descriptor, ...)   │
│           ├─ 存储依赖                                │
│           ├─ 创建 BaseCanvas (Canvas)                │
│           ├─ 包装到 Viewbox 作为 Content             │
│           ├─ 订阅 Loaded/Unloaded/Closed/VisibleChanged │
│           └─ 设置标题                                │
│           ← 返回 window                             │
│                                                      │
├─ FrontedBuiltInWindowDescriptor ───────────────────┐ │
│   └─ CreateXamlWindow(WindowType, null)            │ │
│       ├─ services.GetService(WindowType) 或         │─┤
│       │  ActivatorUtilities.CreateInstance          │ │
│       ├─ 设置 DataContext (ViewModel 已在工厂中设好) │ │
│       └─ 返回 window                                │ │
│                                                      │ │
├─ FrontedPluginWindowDescriptor { PluginXaml } ─────┐ │
│   └─ CreateXamlWindow(WindowType, ViewModelType)   │ │
│       └─ 同上，但额外设置 ViewModel 作为 DataContext│ │
│                                                      │ │
└─ 其他 → 返回 null                                   │
                                                       │
RegisterFrontedWindowAndCanvas(windowId, window)       │
  └─ FrontedWindows[windowId] = window                  │
  └─ FrontedWindowStates[windowId] = false ◄───────────┘
```

---

## 5. 窗口管理逻辑

`FrontedWindowService` 管理所有窗口的显示、隐藏、状态跟踪。

### 5.1 状态管理

| 属性 | 类型 | 说明 |
|---|---|---|
| `FrontedWindows` | `Dictionary<string, Window>` | WindowId → Window 实例 |
| `FrontedWindowStates` | `Dictionary<string, bool>` | WindowId → 是否显示（true=显示中） |

### 5.2 显示/隐藏方法

| 方法 | 行为 |
|---|---|
| `ShowWindow(windowType)` | 通过枚举查找 GUID，调用 `ShowWindow(string)` |
| `ShowWindow(windowId)` | 查找窗口 → 应用布局选项 → 准备（v3 布局重载）→ Show() → 更新状态 → 发布事件 |
| `HideWindow(windowType)` | 通过枚举查找 GUID，调用 `HideWindow(string)` |
| `HideWindow(windowId)` | 查找窗口 → Hide() → 更新状态 → 发布事件 |
| `AllWindowShow()` | 遍历所有未显示的窗口，逐个准备并显示 |
| `AllWindowHide()` | 遍历所有已显示的窗口，逐个隐藏 |

### 5.3 显示流程详解

`ShowWindow(string windowId)` 的完整流程：

1. **按需创建窗口**：调用 `EnsureWindowCreated(windowId)`；只创建指定窗口，不创建其他前台窗口
2. **如果已显示**：调用 `window.Activate()` 激活窗口，不重复加载
3. **预应用 v3 WindowSettings**：v3 host 调用 `EnsureInitialWindowSettingsAppliedAsync()`，只应用尺寸、位置、Topmost、AllowsTransparency、BackgroundColor 和 ViewboxStretch
4. **应用非 v3 布局选项**：调用 `ApplyWindowLayoutOptions(windowId, window)` — 只对非 v3 布局窗口生效
5. **显示窗口**：`window.Show()`
6. **更新状态**：`FrontedWindowStates[windowId] = true`
7. **发布事件**：通过 `IFrontedEventBus` 发布 `WindowShown` 事件
8. **异步加载内容**：v3 host fire-and-forget 调用 `LoadOrReloadContentAsync(force: false)`；异常 catch/log，不阻塞窗口 shell 显示

### 5.4 事件发布

窗口显示/隐藏时通过 `IFrontedEventBus` 发布 `FrontedBehaviorEvent`：

```csharp
// 事件示例
{
    EventType = "WindowShown" or "WindowHidden",
    WindowId = windowId,
    Source = "WindowLifecycle",
    Timestamp = DateTimeOffset.UtcNow
}
```

这些事件被 `FrontedBehaviorRuntime` 系统消费，用于响应窗口状态变化。

### 5.5 全局布局重载

`ReloadFrontedLayoutsAsync()` 只遍历已经创建的窗口，通过反射调用这些窗口的 `ReloadFrontedLayoutAsync()` 方法；它不会创建尚未显示过的前台窗口：

```csharp
public async Task ReloadFrontedLayoutsAsync()
{
    foreach (var window in FrontedWindows.Values)
    {
        var method = window.GetType().GetMethod("ReloadFrontedLayoutAsync");
        if (method is null) continue;
        if (method.Invoke(window, null) is Task task)
            await task;
    }
}
```

这个方法在以下场景被调用：
- 包激活/删除/复制后（由 `FrontManagePageViewModel` 触发）
- 包激活时（`ActivatePackageAsync`）
- 布局包变更事件接收后（`FrontedLayoutPackagesChangedMessage`）

### 5.6 窗口尺寸和背景管理

| 方法 | 行为 |
|---|---|
| `ApplyWindowBackgroundColor(fullWindowType)` | v3 窗口从 `WindowSettings` 读取背景色，非 v3 窗口从旧 options 读取，并应用到窗口 Background 属性 |
| `ApplyWindowSize(fullWindowType)` | 从选项读取宽高并应用到窗口 Width/Height 属性 |
| `GetWindowSize(fullWindowType)` | 获取窗口当前宽高，不可见时返回 null |

所有 UI 属性的设置都通过 `Dispatcher.Invoke` 确保在 UI 线程执行。

---

## 6. 窗口配置读取逻辑

窗口配置分为两个层次：
- **布局配置（Layout Config）**：窗口内容布局（控件、画布、行为等），由 `FrontedLayoutService` 管理
- **WindowSettings**：v3 窗口级外观设置（尺寸、位置、透明度、背景色、Topmost、ViewboxStretch），保存在 `FrontedLayouts/{WindowTypeName}.json`
- **布局选项（Layout Options）**：非 v3 / legacy XAML 窗口级外观设置，由 `FrontedWindowLayoutOptionsService` 管理

### 6.1 布局配置数据结构

在 [FrontedWindowConfig.cs](../neo-bpsys-wpf.Core/Models/FrontedLayout/FrontedWindowConfig.cs) 中：

```csharp
public sealed class FrontedWindowConfig
{
    public int Version { get; set; } = 3;
    public FrontedWindowSettings WindowSettings { get; set; }  // 窗口级设置
    public FrontedCanvasSettings CanvasSettings { get; set; }  // 画布级设置
    public FrontedControlLayout ControlLayout { get; set; }    // 控件布局
}
```

`FrontedWindowSettings` 包含：
- `WindowWidth` / `WindowHeight` — 窗口尺寸
- `WindowLeft` / `WindowTop` — 窗口位置（可选）
- `AllowsTransparency` — 允许透明
- `BackgroundColor` — 背景色（`#AARRGGBB`）
- `Topmost` — 是否置顶
- `ViewboxStretch` — ViewBox 拉伸模式

### 6.2 布局配置加载回退链

`FrontedLayoutService.LoadWindowConfigWithMetadataAsync()` 实现了一个多层回退链：

```
1. 包管理器的活跃包路径（当有非内置包激活时）
   └─ _packageManager.GetPackageLayoutPath(activeState.PackageId, windowTypeName)
   └─ 如果文件存在且可读 → 返回 FrontedLayoutSource.User

2. 用户布局存储
   └─ _userLayoutStore.GetLayoutPath(windowTypeName)
   └─ _userLayoutStore.Exists(windowTypeName) → LoadAsync()
   └─ 如果成功 → 返回 FrontedLayoutSource.User

3. 插件默认布局
   └─ TryGetPluginDefaultLayout(windowTypeName)
   └─ 需要 _windowRegistry 存在且描述符为 PluginLayout 类型
   └─ 路径: pluginFolder/DefaultLayoutRoot/WindowTypeName.json
   └─ 如果成功 → 返回 FrontedLayoutSource.PluginDefault

4. 内置默认布局
   └─ GetBuiltInDefaultWindowLayoutPath(windowTypeName)
   └─ 路径: {ResourcesPath}/FrontedLayouts/{windowTypeName}.json
   └─ 如果成功 → 返回 FrontedLayoutSource.BuiltIn

5. 全部失败
   └─ 返回 FrontedLayoutSource.MissingOrError, Config = null
```

**每个层级失败时都会记录错误并收集 `userLoadError`**，最终调用方可以通过 `FrontedLayoutLoadResult.Error` 获取详细信息。

`FrontedLayoutLoadResult` 结构在 [FrontedLayoutLoadResult.cs](../neo-bpsys-wpf.Core/Models/FrontedLayout/FrontedLayoutLoadResult.cs) 中：

```csharp
public sealed class FrontedLayoutLoadResult
{
    public FrontedWindowConfig? Config { get; init; }
    public FrontedLayoutSource Source { get; init; }  // User / BuiltIn / PluginDefault / MissingOrError
    public string? Path { get; init; }                // 实际加载路径
    public string? Error { get; init; }               // 加载错误信息
}
```

### 6.3 布局选项读取

`FrontedWindowLayoutOptionsService.LoadOptions()` 的读取路径（在 [FrontedWindowLayoutOptionsService.cs](../neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedWindowLayoutOptionsService.cs) 中）：

```
1. 包管理器感知（当 _packageManager 非 null 时）
   └─ 获取活跃包状态
   └─ 如果活跃包不是 BuiltIn
       └─ 包选项路径: {packageLayoutsRoot}/{windowTypeName}/options.json
       └─ 如果文件存在 → 从包路径读取
   └─ 否则 → 从旧版路径读取

2. 旧版路径（Legacy Options Path）
   └─ {FrontedLayoutsPath}/{windowTypeName}/options.json
```

选项 JSON 结构（[FrontedWindowLayoutOptions.cs](../neo-bpsys-wpf.Core/Models/FrontedLayout/FrontedWindowLayoutOptions.cs)）：

```json
{
    "Version": 3,
    "WindowWidth": 1440,
    "WindowHeight": 810,
    "AllowTransparency": true,
    "BackgroundColor": "#00000000"
}
```

### 6.4 运行时默认值

[FrontedWindowRuntimeSettings.cs](../neo-bpsys-wpf/ViewModels/Windows/FrontedWindowRuntimeSettings.cs) 提供硬编码的运行时默认值：

```csharp
WindowSize = (1440, 810)            // 默认窗口尺寸
ScoreInGameWindowSize = (480, 152) // 赛中比分窗口尺寸
ScoreGlobalWindowSize = (1440, 195) // 全局比分窗口尺寸
AllowsWindowTransparency = true     // 默认允许透明
BackgroundBrush = TransparentBlack  // 透明黑色背景 (ARGB=0,0,0,0)
```

### 6.5 配置读取链路总结

```
FrontedWindowBase.ReloadFrontedLayoutAsync()
  └─ _layoutService.LoadWindowConfigAsync(fullWindowType)
       └─ LoadWindowConfigWithMetadataAsync()
            │
            ├─ [活跃包路径] → 成功? → User
            ├─ [用户布局] → 成功? → User
            ├─ [插件默认布局] → 成功? → PluginDefault
            ├─ [内置默认布局] → 成功? → BuiltIn
            └─ → MissingOrError

FrontedWindowService.ApplyWindowLayoutOptions()
  └─ _windowLayoutOptionsService.LoadOptions(fullWindowType)
       ├─ [包管理器活跃] → 包路径下 options.json
       └─ [旧版/无包管理器] → FrontedLayouts/{name}/options.json
```

---

## 7. 窗口配置应用逻辑

配置应用发生在两个不同的时机和场景。

### 7.1 v3 布局窗口的配置应用（`ReloadFrontedLayoutAsync`）

v3 layout host 将配置应用拆成两个环节。`ReloadFrontedLayoutAsync()` 保留为手动强制重载入口，内部先预应用窗口设置，再强制加载内容：

```csharp
public async Task ReloadFrontedLayoutAsync()
{
    await EnsureInitialWindowSettingsAppliedAsync();
    await LoadOrReloadContentAsync(force: true);
}
```

`EnsureInitialWindowSettingsAppliedAsync()`：

- 加载 `FrontedWindowConfig` 或其中的 `WindowSettings`
- 只调用 `ApplyWindowSettings`
- 必须在第一次 `Show()` 前执行，因为 `AllowsTransparency` 只能在 WPF source 创建前设置
- 不渲染控件，不 attach behavior

`LoadOrReloadContentAsync(force: false)`：

```csharp
if (!force && IsContentRendered && !IsLayoutDirty)
{
    if (!IsBehaviorAttached)
        await AttachBehaviorRuntimeAsync();
    return;
}

var config = await _layoutService.LoadWindowConfigAsync(_v3Descriptor.FullWindowType);

await Dispatcher.InvokeAsync(async () =>
{
    ApplyCanvasSettings(config.CanvasSettings);
    await DetachBehaviorRuntimeAsync();
    _renderer.RenderToCanvas(_baseCanvas, config, renderContext);
    IsContentRendered = true;
    IsLayoutDirty = false;
    await AttachBehaviorRuntimeAsync();
});
```

**重要语义**：
- `ApplyWindowSettings` 中如果窗口未加载，`AllowsTransparency` 可以设置；如果已加载，新值将在下次窗口重建时生效（通过日志提示）
- 普通 Hide/Show 不会清空已渲染控件，也不会把 `IsContentRendered` 置回 false
- Hide/Unloaded 可以 detach behavior runtime；下次 Show 只重新 attach behavior，不重新 render
- v3 配置读取、保存、包导入和导出必须保留 `WindowSettings.WindowWidth` / `WindowHeight`；`SyncWindowSizeToCanvas()` 只用于显式 legacy canvas-centric 转换

### 7.2 非 v3 布局窗口的配置应用（`ApplyWindowLayoutOptions`）

在 `FrontedWindowService.ApplyWindowLayoutOptions(windowId, window)` 中：

```csharp
// 仅对非 v3 布局窗口生效
// 1. 读取选项文件（如果不存在则跳过）
var options = _windowLayoutOptionsService.LoadOptions(descriptor.FullWindowType);

// 2. 应用透明度
window.AllowsTransparency = options.AllowTransparency;

// 3. 应用背景色
if (TryCreateBackgroundBrush(options.BackgroundColor, out var brush))
    window.SetCurrentValue(Window.BackgroundProperty, brush);
```

**差异对比**：

| 方面 | v3 布局窗口 | 非 v3 布局窗口 |
|---|---|---|
| 配置来源 | `FrontedWindowConfig`（布局 JSON） | `FrontedWindowLayoutOptions`（选项 JSON） |
| 应用时机 | Show 前轻量应用 WindowSettings，Show 后异步加载内容 | 每次显示时（`ShowWindow`） |
| 应用内容 | Show 前：尺寸、位置、透明、背景；Show 后：画布、控件、行为 | 透明、背景色 |
| 尺寸同步 | 自动同步画布到窗口 | 通过独立的 `ApplyWindowSize()` 方法 |

### 7.3 主动配置应用

除了显示时的自动应用，也可以通过以下方法主动触发：

```csharp
// 立即应用背景色
_frontedWindowService.ApplyWindowBackgroundColor("BpWindow");
// 立即应用窗口尺寸
_frontedWindowService.ApplyWindowSize("BpWindow");
```

这些方法在 `FrontManagePageViewModel` 或 `FrontedDesignerWindowViewModel` 中被调用，用于实时预览用户配置。

---

## 8. 窗口生命周期管理

### 8.1 v3 布局窗口生命周期（`FrontedWindowBase`）

在 [FrontedWindowBase.cs](../neo-bpsys-wpf.Core/Controls/FrontedWindowBase.cs) 中管理：

```
构造
  ├─ 设置无边框、不可调整大小、居中
  └─ 绑定鼠标拖拽事件

InitializeV3LayoutHost()  ← 由 FrontedWindowService 调用
  ├─ 存储所有依赖（descriptor、layoutService、renderer 等）
  ├─ 创建 BaseCanvas（Canvas 控件，背景透明）
  ├─ 包装到 Viewbox（实现自适应缩放）
  └─ 订阅事件：
      ├─ Loaded     → OnV3HostLoaded
      ├─ Unloaded   → OnV3HostUnloaded
      ├─ Closed     → OnV3HostClosed
      └─ IsVisibleChanged → OnV3HostIsVisibleChanged

ShowWindow（FrontedWindowService）
  ├─ EnsureWindowCreated(windowId) ← 首次显示才创建 shell
  ├─ EnsureInitialWindowSettingsAppliedAsync()
  ├─ Show()
  └─ fire-and-forget LoadOrReloadContentAsync(force:false)

Loaded（OnV3HostLoaded）
  └─ SubscribeBoModeChanged()     ← 订阅 ISharedDataService.IsBo3ModeChanged

VisibleChanged（OnV3HostIsVisibleChanged）
  ├─ 变为可见 → SubscribeBoModeChanged()
  └─ 变为不可见 → UnsubscribeBoModeChanged() + DetachBehaviorRuntime()

Unloaded（OnV3HostUnloaded）
  ├─ UnsubscribeBoModeChanged()
  └─ DetachBehaviorRuntime()

Closed（OnV3HostClosed）
  ├─ UnsubscribeBoModeChanged()
  ├─ DetachBehaviorRuntime()
  └─ 取消订阅 IsVisibleChanged

BO 模式切换（OnBoModeChanged）
  ├─ MarkLayoutDirty()
  └─ 窗口可见时 Dispatcher 调度 → LoadOrReloadContentAsync(force:false)

OnClosing（OnClosing 重写）
  └─ e.Cancel = true  ← 阻止真正关闭
  └─ Hide()           ← 仅隐藏窗口

关闭按钮点击
  └─ OnClosing → Cancel + Hide
  └─ 窗口仍在 FrontedWindows 字典中，下次 Show() 可重新显示
```

**关键设计**：
- 窗口被关闭时通过 `Cancel = true` 阻止真正销毁，改为 `Hide()`，确保窗口实例可复用
- BO 模式切换时自动重新加载布局，支持通过 `Dispatcher` 跨线程调度
- 不可见时自动分离 BehaviorRuntime 以减少资源占用

### 8.2 内建 XAML 窗口生命周期（以 `BpWindow` 为例）

`BpWindow` 继承自 `FrontedWindowBase`，但生命周期逻辑由各窗口自行管理。其生命周期模式与 `FrontedWindowBase` 相似但各窗口独立实现：

```
构造（通过 DI 注入依赖）
  ├─ _layoutService、_renderer、_sharedDataService、_logger、_behaviorRuntime
  ├─ InitializeComponent()
  └─ 订阅 Loaded、Unloaded、Closed 事件

Loaded
  ├─ SubscribeBoModeChanged()
  └─ 如果未渲染过 → ReloadFrontedLayoutAsync()
       ├─ LoadWindowConfigAsync(nameof(BpWindow))
       ├─ DetachAsync()
       ├─ RenderToCanvas(BaseCanvas, config, context)
       └─ AttachAsync()

Unloaded
  ├─ UnsubscribeBoModeChanged()
  └─ DetachBehaviorHost()

Closed
  ├─ UnsubscribeBoModeChanged()
  └─ DetachBehaviorHost()

BO 模式切换 → Dispatcher → ReloadFrontedLayoutAsync()
```

**注意** `ScoreGlobalWindow` 在 `ReloadFrontedLayoutAsync()` 中有重入保护（`_isReloadingLayout` / `_reloadRequested` 机制），其他窗口没有。

### 8.3 窗口关闭行为

所有前台窗口（包括 v3 布局宿主和 XAML 窗口）都重写了 `OnClosing`：

```csharp
protected override void OnClosing(CancelEventArgs e)
{
    e.Cancel = true;
    Hide();
    base.OnClosing(e);
}
```

这意味着：
- **前台窗口永远不会被真正关闭/销毁**
- 关闭按钮只会隐藏窗口
- 窗口实例在 `FrontedWindows` 字典中一直存在
- 每次 `Show()` 可以重新显示

### 8.4 全局生命周期（`FrontedWindowService` 视角）

```
FrontedWindowService 构造
  └─ RegisterFrontedWindowAndCanvas()
       └─ 创建所有窗口实例，存入 FrontedWindows 字典
       └─ 状态初始化为 false（隐藏）

Shutdown / Dispose
  └─ 窗口随 Application 生命周期结束自然销毁
```

**注意**：没有显式的 Dispose 或 Shutdown 逻辑。窗口实例在应用程序退出时由 WPF 框架清理。

---

## 9. 关键代码位置索引

| 功能 | 文件 |
|---|---|
| DI 注册入口 | [App.Services.xaml.cs](../neo-bpsys-wpf/App.Services.xaml.cs) |
| `AddFrontedWindow` 扩展 | [FrontedWindowRegistryExtensions.cs](../neo-bpsys-wpf.Core/Extensions/Registry/FrontedWindowRegistryExtensions.cs) |
| 插件窗口注册扩展 | [FrontedPluginWindowRegistryExtensions.cs](../neo-bpsys-wpf.Core/Extensions/Registry/FrontedPluginWindowRegistryExtensions.cs) |
| `[FrontedWindowInfo]` 特性 | [FrontedWindowInfo.cs](../neo-bpsys-wpf.Core/Attributes/FrontedWindowInfo.cs) |
| 窗口描述符接口 | [IFrontedWindowDescriptor.cs](../neo-bpsys-wpf.Core/Models/FrontedLayout/IFrontedWindowDescriptor.cs) |
| 内置窗口描述符 | [FrontedBuiltInWindowDescriptor.cs](../neo-bpsys-wpf.Core/Models/FrontedLayout/FrontedBuiltInWindowDescriptor.cs) |
| 插件窗口描述符 | [FrontedPluginWindowDescriptor.cs](../neo-bpsys-wpf.Core/Models/FrontedLayout/FrontedPluginWindowDescriptor.cs) |
| 窗口提供方式枚举 | [FrontedWindowKind.cs](../neo-bpsys-wpf.Core/Models/FrontedLayout/FrontedWindowKind.cs) |
| 窗口注册表服务 | [FrontedWindowRegistryService.cs](../neo-bpsys-wpf.Core/Services/Registry/FrontedWindowRegistryService.cs) |
| 窗口注册表接口 | [IFrontedWindowRegistry.cs](../neo-bpsys-wpf.Core/Abstractions/Services/IFrontedWindowRegistry.cs) |
| 窗口管理器实现 | [FrontedWindowService.cs](../neo-bpsys-wpf/Services/FrontedWindowService.cs) |
| 窗口管理器接口 | [IFrontedWindowService.cs](../neo-bpsys-wpf.Core/Abstractions/Services/IFrontedWindowService.cs) |
| 窗口基类（v3 布局宿主） | [FrontedWindowBase.cs](../neo-bpsys-wpf.Core/Controls/FrontedWindowBase.cs) |
| 布局配置模型 | [FrontedWindowConfig.cs](../neo-bpsys-wpf.Core/Models/FrontedLayout/FrontedWindowConfig.cs) |
| 布局配置服务 | [FrontedLayoutService.cs](../neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedLayoutService.cs) |
| 布局配置接口 | [IFrontedLayoutService.cs](../neo-bpsys-wpf.Core/Abstractions/Services/IFrontedLayoutService.cs) |
| 布局选项模型 | [FrontedWindowLayoutOptions.cs](../neo-bpsys-wpf.Core/Models/FrontedLayout/FrontedWindowLayoutOptions.cs) |
| 布局选项服务 | [FrontedWindowLayoutOptionsService.cs](../neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedWindowLayoutOptionsService.cs) |
| 布局选项接口 | [IFrontedWindowLayoutOptionsService.cs](../neo-bpsys-wpf.Core/Abstractions/Services/IFrontedWindowLayoutOptionsService.cs) |
| GUID 映射 | [FrontedWindowHelper.cs](../neo-bpsys-wpf.Core/Helpers/FrontedWindowHelper.cs) |
| 运行时默认设置 | [FrontedWindowRuntimeSettings.cs](../neo-bpsys-wpf/ViewModels/Windows/FrontedWindowRuntimeSettings.cs) |
| 前台管理页 ViewModel | [FrontManagePageViewModel.cs](../neo-bpsys-wpf/ViewModels/Pages/FrontManagePageViewModel.cs) |
| 前台管理页视图 | [FrontedWindowsView.xaml](../neo-bpsys-wpf/Views/Pages/FrontManage/FrontedWindowsView.xaml) |
| BpWindow（内置窗口示例） | [BpWindow.xaml.cs](../neo-bpsys-wpf/Views/Windows/BpWindow.xaml.cs) |
| LoadResult 模型 | [FrontedLayoutLoadResult.cs](../neo-bpsys-wpf.Core/Models/FrontedLayout/FrontedLayoutLoadResult.cs) |
| 插件贡献者示例 | [ExampleFrontedWindowContributor.cs](../neo-bpsys-wpf.ExamplePlugin/ExampleFrontedWindowContributor.cs) |

---

## 10. 架构总图

```
┌──────────────────────────────────────────────────────────┐
│                    App.Services.xaml.cs                   │
│  DI 容器注册                                              │
│  ┌─────────────────────────────────────────────────────┐ │
│  │ AddFrontedWindow<BpWindow, BpWindowViewModel>()     │ │
│  │ AddFrontedWindow<CutSceneWindow, ...>()             │ │
│  │ ... (6 次)                                          │ │
│  │ AddSingleton<IFrontedWindowService, FrontedWindowService>││
│  │ AddSingleton<IFrontedWindowRegistry, FrontedWindow...>  ││
│  │ AddSingleton<IFrontedLayoutService, FrontedLayoutService>││
│  │ AddSingleton<IFrontedWindowLayoutOptionsService, ...>   ││
│  │ AddSingleton<IFrontedBehaviorRuntime, FrontedBehavior...>││
│  └─────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────┘
                            │
                            ▼
┌──────────────────────────────────────────────────────────┐
│                FrontedWindowRegistryService               │
│  注册表（单例）                                           │
│  ┌─────────────────────────────────────────────────────┐ │
│  │ RegisteredWindow (static) ← AddFrontedWindow 填充    │ │
│  │                                                     │ │
│  │ FrontedBuiltInWindowDescriptor  ← 内置窗口属性       │ │
│  │   ├─ BpWindow          (ACFC0F23-...)               │ │
│  │   ├─ CutSceneWindow    (8716A6DB-...)               │ │
│  │   ├─ ScoreGlobalWindow (3A4F66F7-...)               │ │
│  │   ├─ ScoreSurWindow    (4ED64F79-...)               │ │
│  │   ├─ ScoreHunWindow    (EA69B342-...)               │ │
│  │   ├─ GameDataWindow    (25378080-...)               │ │
│  │   ├─ BpOverviewWindow  (3F6AD6CC-...) 纯 v3 布局    │ │
│  │   └─ MapV2Window       (9898D1EF-...) 纯 v3 布局    │ │
│  │                                                     │ │
│  │ FrontedPluginWindowDescriptor ← 插件贡献者           │ │
│  │   ├─ PluginXaml 类型                                  │ │
│  │   └─ PluginLayout 类型                                │ │
│  │                                                     │ │
│  │ 索引：_byWindowId / _byFullWindowType                │ │
│  └─────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────┘
                            │
                            ▼
┌──────────────────────────────────────────────────────────┐
│                FrontedWindowService                       │
│  窗口管理器（单例）                                       │
│  ┌─────────────────────────────────────────────────────┐ │
│  │ 构造时预创建所有窗口                                  │ │
│  │                                                     │ │
│  │ FrontedWindows: Dictionary<string, Window>           │ │
│  │   ├─ "ACFC0F23-..." → BpWindow 实例                  │ │
│  │   ├─ "8716A6DB-..." → CutSceneWindow 实例            │ │
│  │   ├─ ...                                             │ │
│  │   └─ "9898D1EF-..." → FrontedWindowBase 实例         │ │
│  │                                                     │ │
│  │ FrontedWindowStates: Dictionary<string, bool>         │ │
│  │   └─ 全部初始化为 false                               │ │
│  │                                                     │ │
│  │ CreateWindow(descriptor)                             │ │
│  │   ├─ IsV3LayoutWindow  → FrontedWindowBase           │ │
│  │   ├─ BuiltIn           → DI 获取 XAML 窗口           │ │
│  │   └─ PluginXaml        → DI 获取插件 XAML 窗口       │ │
│  │                                                     │ │
│  │ ShowWindow / HideWindow / AllWindowShow / ...        │ │
│  │ ReloadFrontedLayoutsAsync()  ← 反射调用              │ │
│  └─────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────┘
                            │
         ┌──────────────────┼──────────────────┐
         ▼                  ▼                  ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│  FrontedWindowBase │ │  BpWindow        │ │  ScoreGlobal... │
│  (v3 布局宿主)    │ │  (继承自 Base)   │ │  (继承自 Base)  │
│                   │ │                  │ │                  │
│ InitializeV3Layout│ │ ReloadFronted    │ │ ReloadFronted    │
│ Host()            │ │ LayoutAsync()    │ │ LayoutAsync()    │
│ ReloadFronted     │ │                  │ │ (有重入保护)     │
│ LayoutAsync()     │ │ BO 模式订阅      │ │                  │
│                   │ │ 行为运行时管理    │ │                  │
│ BO 模式订阅       │ │                  │ │                  │
│ 行为运行时管理    │ │                  │ │                  │
│ 关闭时 Cancel+Hide│ │                  │ │                  │
└─────────────────┘ └─────────────────┘ └─────────────────┘
                            │
                            ▼
┌──────────────────────────────────────────────────────────┐
│  FrontedLayoutService        FrontedWindowLayoutOptions   │
│  布局配置服务                Service                      │
│  ┌─────────────────────┐   ┌───────────────────────────┐ │
│  │ LoadWindowConfig()  │   │ LoadOptions()             │ │
│  │   回退链：           │   │   包感知路径 → 旧版路径    │ │
│  │   ① 活跃包          │   │ SaveOptions()             │ │
│  │   ② 用户布局        │   │ ResetOptions()            │ │
│  │   ③ 插件默认        │   └───────────────────────────┘ │
│  │   ④ 内置默认        │                                  │
│  │ SaveWindowConfig()  │                                  │
│  └─────────────────────┘                                  │
└──────────────────────────────────────────────────────────┘
                            │
                            ▼
┌──────────────────────────────────────────────────────────┐
│  FrontManagePageViewModel                                 │
│  前台管理页（用户交互入口）                                │
│  ┌─────────────────────────────────────────────────────┐ │
│  │ ShowWindow / HideWindow (单窗口)                     │ │
│  │ ShowAllWindows / HideAllWindows                     │ │
│  │ ActivatePackage → ReloadFrontedLayoutsAsync()       │ │
│  │ ImportPackage / ExportPackage                       │ │
│  │ DuplicatePackage → ReloadFrontedLayoutsAsync()      │ │
│  │ DeletePackage → ReloadFrontedLayoutsAsync()         │ │
│  └─────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────┘
```

---

## 附录 A：内置窗口注册信息汇总

| 窗口 | `FrontedWindowType` | GUID | `[FrontedWindowInfo]` | 有 XAML | 继承自 | v3 布局 |
|---|---|---|---|---|---|---|
| BpWindow | `BpWindow` | `ACFC0F23-...` | ✅ | ✅ | `FrontedWindowBase` | ✅ |
| CutSceneWindow | `CutSceneWindow` | `8716A6DB-...` | ✅ | ✅ | `FrontedWindowBase` | ✅ |
| ScoreGlobalWindow | `ScoreGlobalWindow` | `3A4F66F7-...` | ✅ | ✅ | `FrontedWindowBase` | ✅ |
| ScoreSurWindow | `ScoreSurWindow` | `4ED64F79-...` | ✅ | ✅ | `FrontedWindowBase` | ✅ |
| ScoreHunWindow | `ScoreHunWindow` | `EA69B342-...` | ✅ | ✅ | `FrontedWindowBase` | ✅ |
| GameDataWindow | `GameDataWindow` | `25378080-...` | ✅ | ✅ | `FrontedWindowBase` | ✅ |
| BpOverviewWindow | `BpOverviewWindow` | `3F6AD6CC-...` | ❌（硬编码） | ❌ | `FrontedWindowBase` | ✅ |
| MapV2Window | `MapV2Window` | `9898D1EF-...` | ❌（硬编码） | ❌ | `FrontedWindowBase` | ✅ |

## 附录 B：依赖关系图

```
FrontedWindowService
  ├─ IFrontedWindowRegistry → FrontedWindowRegistryService
  │    ├─ FrontedWindowInfo (static entry)
  │    └─ IFrontedWindowPluginContributor[]
  ├─ IFrontedWindowLayoutOptionsService → FrontedWindowLayoutOptionsService
  │    └─ IFrontedLayoutPackageManager (optional)
  ├─ IFrontedEventBus → FrontedEventBus
  └─ IServiceProvider

FrontedWindowBase (per window instance)
  ├─ IFrontedWindowDescriptor
  ├─ IFrontedLayoutService
  │    ├─ IFrontedUserLayoutStore
  │    ├─ IFrontedLayoutPackageManager
  │    └─ IFrontedWindowRegistry
  ├─ IFrontedRenderer
  ├─ ISharedDataService
  ├─ IFrontedBehaviorRuntime (optional)
  └─ ILogger

FrontManagePageViewModel
  ├─ IFrontedWindowService
  ├─ ISharedDataService
  ├─ IFrontedLayoutPackageManager
  ├─ IFrontedLayoutPackageExporter
  ├─ IFrontedLayoutPackageImporter
  ├─ IFrontedLayoutPackageLegacyConverter
  ├─ IFrontedWindowRegistry
  ├─ IPluginMarketService
  ├─ IPluginInstallService
  └─ IServiceProvider
```
