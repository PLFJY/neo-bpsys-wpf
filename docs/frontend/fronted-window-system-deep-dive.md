# 前台窗口系统深度解析

本文档从源码层面完整覆盖前台窗口（Fronted Window）的：启动链路、注册链路、窗口创建链路、窗口管理逻辑、窗口配置读取逻辑、窗口配置应用逻辑、窗口生命周期管理。

---

## 1. 术语与角色

| 角色 | 类 / 接口 | 职责 |
|---|---|---|
| 窗口注册 | `FrontedWindowRegistration`（基类） | 窗口的强类型元数据：Canonical ID、LocalId、PackageId、IsBuiltIn、DisplayName、Kind |
| XAML 窗口注册 | `FrontedXamlWindowRegistration` | 承载 WPF `Window` CLR 类型的注册，额外含 `WindowType`，Kind 固定 `Xaml` |
| v3 布局窗口注册 | `FrontedV3LayoutWindowRegistration` | 由宿主 v3 布局渲染器承载的窗口注册，无额外字段，Kind 固定 `V3Layout` |
| 窗口注册表 | `IFrontedWindowRegistry` / `FrontedWindowRegistryService` | 从 DI 接收 `FrontedWindowRegistration` 集合，按 Canonical ID 索引并提供查询 |
| 窗口管理器 | `IFrontedWindowService` / `FrontedWindowService` | 窗口实例的按需创建、显示、隐藏、布局重载 |
| 窗口基类 | `FrontedWindowBase` | v3 布局宿主的 WPF 基类，管理布局加载、BehaviorRuntime 附加/分离 |
| 布局配置服务 | `IFrontedLayoutService` / `FrontedLayoutService` | 读取/保存窗口级 v3 布局 JSON 配置 |
| 窗口选项服务 | `IFrontedWindowLayoutOptionsService` / `FrontedWindowLayoutOptionsService` | 仅用于非 v3 / legacy XAML 窗口选项；v3 layout window 使用 `FrontedWindowConfig.WindowSettings` |
| 管理页 ViewModel | `FrontManagePageViewModel` | 用户在后台管理前台窗口的入口 |
| 插件注册作用域 | `FrontedPluginRegistrationContext`（internal static） | 在插件 `Initialize` 期间通过 `AsyncLocal<string?>` 携带当前插件包 ID |
| 身份生成 | `FrontedWindowIdentity` | 按 `plugin:{PackageId}/{LocalId}` / `{LocalId}` 规则生成 Canonical ID |
| v3 身份校验 | `FrontedV3LayoutWindowIdValidator` | 验证局部窗口标识，拒绝路径分隔符、`plugin:` 前缀、纯空白等形式 |
| v3 空模板工厂 | `FrontedV3LayoutWindowConfigFactory` | 在布局加载不到时返回合法内存空模板，不立即写磁盘 |
| v3 路径映射 | `FrontedV3LayoutWindowPathHelper` | 将 Canonical ID 映射为文件系统安全的布局路径 |

### 窗口类型枚举：FrontedWindowType

在 [FrontedWindowType.cs](../neo-bpsys-wpf.Core/Enums/FrontedWindowType.cs) 中定义，仅表示内置窗口：

| 枚举值 | 对应窗口 | Canonical ID |
|---|---|---|
| `BpWindow` | BP 展示窗口 | `BpWindow` |
| `CutSceneWindow` | 过场动画窗口 | `CutSceneWindow` |
| `ScoreWindow` | 复合操作（非真实窗口） | `Guid.Empty` |
| `ScoreGlobalWindow` | 全局比分窗口 | `ScoreGlobalWindow` |
| `ScoreSurWindow` | 求生者比分窗口 | `ScoreSurWindow` |
| `ScoreHunWindow` | 监管者比分窗口 | `ScoreHunWindow` |
| `GameDataWindow` | 游戏数据窗口 | `GameDataWindow` |
| `BpOverviewWindow` | BP 总览窗口 | `BpOverviewWindow` |
| `MapV2Window` | 地图 v2 窗口 | `MapV2Window` |

v3 内置窗口的 Canonical ID 直接使用枚举名（如 `BpWindow`），不再先映射 GUID 再查注册表。XAML 内置窗口仍可使用 GUID，由 `LegacyFrontedWindowIdMap.GetLegacyGuid` 提供。映射逻辑见 [LegacyFrontedWindowIdMap.cs](../neo-bpsys-wpf.Core/Helpers/LegacyFrontedWindowIdMap.cs)。

### 窗口承载方式枚举：FrontedWindowRegistrationKind

在 [FrontedWindowRegistrationKind.cs](../neo-bpsys-wpf.Core/Models/FrontedLayout/Registrations/FrontedWindowRegistrationKind.cs) 中定义：

| 枚举值 | 含义 |
|---|---|
| `Xaml` | 由提供方直接给出 WPF `Window` CLR 类型的窗口（含内置与插件） |
| `V3Layout` | 由宿主 v3 布局渲染器承载的窗口（含内置与插件） |

窗口承载方式只有 `Xaml` / `V3Layout` 两种 Kind，来源归属由 `FrontedWindowRegistration.IsBuiltIn` 区分。二者是正交的两个维度。

---

## 2. 启动链路

启动链路描述从应用程序启动到前台窗口可用（窗口实例按需创建并可由 `EnsureWindowCreated` 获取）的完整过程。

### 2.1 DI 注册环节（`App.Services.xaml.cs` → `ConfigureServices`）

[App.Services.xaml.cs](../neo-bpsys-wpf/App.Services.xaml.cs) 中注册内置 v3 布局窗口：

```csharp
// 注册内置 v3 Layout 前台窗口（Canonical ID = LocalId，无 PackageId）
services.AddFrontedV3LayoutWindow("BpWindow", isBuiltIn: true);
services.AddFrontedV3LayoutWindow("CutSceneWindow", isBuiltIn: true);
services.AddFrontedV3LayoutWindow("ScoreSurWindow", isBuiltIn: true);
services.AddFrontedV3LayoutWindow("ScoreHunWindow", isBuiltIn: true);
services.AddFrontedV3LayoutWindow("ScoreGlobalWindow", isBuiltIn: true);
services.AddFrontedV3LayoutWindow("GameDataWindow", isBuiltIn: true);
services.AddFrontedV3LayoutWindow("BpOverviewWindow", isBuiltIn: true);
services.AddFrontedV3LayoutWindow("MapV2Window", isBuiltIn: true);

// 随后初始化插件，插件在自身 Initialize 内注册窗口
PluginService.InitializePlugins(context, services);
```

同时注册了相关的依赖服务：

- `IFrontedWindowService` → `FrontedWindowService`（Singleton）
- `IFrontedWindowRegistry` → `FrontedWindowRegistryService`（从 DI 接收 `IEnumerable<FrontedWindowRegistration>`）
- `IFrontedLayoutService` → `FrontedLayoutService`
- `IFrontedWindowLayoutOptionsService` → `FrontedWindowLayoutOptionsService`
- `IFrontedBehaviorRuntime` → `FrontedBehaviorRuntime`
- `IFrontedLayoutPackageManager` → `FrontedLayoutPackageManager`
- 等

### 2.2 `AddFrontedV3LayoutWindow` 扩展方法

在 [FrontedV3LayoutWindowRegistryExtensions.cs](../neo-bpsys-wpf.Core/Extensions/Registry/FrontedV3LayoutWindowRegistryExtensions.cs) 中：

1. 调用 `FrontedV3LayoutWindowIdValidator.EnsureValidLocalWindowId(windowId)` 验证局部窗口标识（含 null、空串、纯空白、路径分隔符、`..`、`plugin:` 前缀等）
2. 读取 `FrontedPluginRegistrationContext.CurrentPackageId` 获取当前插件包 ID；若非空则校验其可作为 canonical path segment
3. 调用 `FrontedWindowIdentity.BuildCanonicalId(windowId, packageId, isBuiltIn)` 生成 Canonical ID
4. 创建 `FrontedV3LayoutWindowRegistration` 并向 DI 注册为 `FrontedWindowRegistration`

```csharp
// 核心代码示意
FrontedV3LayoutWindowIdValidator.EnsureValidLocalWindowId(windowId);

var packageId = FrontedPluginRegistrationContext.CurrentPackageId;
FrontedWindowRegistryExtensions.EnsureSafePackageId(packageId);

var canonicalId = FrontedWindowIdentity.BuildCanonicalId(windowId, packageId, isBuiltIn);

var registration = new FrontedV3LayoutWindowRegistration
{
    Id = canonicalId,
    LocalId = windowId,
    PackageId = packageId,
    IsBuiltIn = isBuiltIn,
    DisplayName = windowId
};

services.AddSingleton<FrontedWindowRegistration>(registration);
```

来源分组（BuiltIn / Plugin / External）由 UI 层基于 `IsBuiltIn + PackageId` 推导；顺序使用 DI 注册顺序或 UI 按 `LocalId` 排序；内置窗口的本地化显示名由 UI 层通过现有 resx（`Designer.Window.{LocalId}`）解析。基类不再有 `GroupKey` / `DisplayOrder` / `I18nDisplayNames` 字段。

**Canonical ID 生成规则**（见 [FrontedWindowIdentity.cs](../neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedWindowIdentity.cs)）：

- `isBuiltIn = true` 或 `packageId is null`：`canonicalId = localWindowId`
- 否则：`canonicalId = $"plugin:{packageId}/{localWindowId}"`

**局部 ID 校验规则**（见 [FrontedV3LayoutWindowIdValidator.cs](../neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedV3LayoutWindowIdValidator.cs)）：

- 拒绝 null、空串、纯空白（`string.IsNullOrWhiteSpace`）
- 禁止包含 `/`、`\`、`:`、`.`
- 禁止包含 `Path.GetInvalidFileNameChars()` 中的字符
- 拒绝 `plugin:package/window` 完整形式
- 违规时在注册入口直接抛出 `ArgumentException`，不 warning 后跳过

当 `packageId` 非空时，`FrontedWindowRegistryExtensions.EnsureSafePackageId` 还会校验插件包 ID 可作为 canonical path segment（与 `FrontedV3LayoutWindowPathHelper.IsSafePathSegment` 一致），避免路径分隔符、`..` 等字符在 LayoutService 拼接路径时才报错。

### 2.3 `AddFrontedWindow<TView,TViewModel>()` 扩展方法（XAML 窗口）

在 [FrontedWindowRegistryExtensions.cs](../neo-bpsys-wpf.Core/Extensions/Registry/FrontedWindowRegistryExtensions.cs) 中，用于注册有 WPF `Window` CLR 类型的窗口（典型为插件 XAML 窗口）：

1. 从 `TView` 类型的 `[FrontedWindowInfo]` 特性中提取 `Id`（不再强制要求 GUID，只需非空白且不与已注册窗口重复）、`Name`、`IsBuiltIn`
2. 校验 ID 可作为 Canonical ID 的安全片段（`FrontedWindowIdentity.EnsureValidWindowLocalId`：拒绝空/空白、前后空白、路径分隔符 `/` `\`、冒号 `:` 与控制字符）
3. 设置 `info.WindowType = typeof(TView)`
4. 读取 `FrontedPluginRegistrationContext.CurrentPackageId`；若非空则校验其可作为 canonical path segment
5. 生成 Canonical ID：内置或无 PackageId 时为 Attribute ID 本身；插件时为 `plugin:{PackageId}/{AttributeId}`
6. 向 DI 容器注册 `TViewModel`、`TView`（工厂创建，显式设置 `DataContext`）
7. 创建 `FrontedXamlWindowRegistration` 并向 DI 注册为 `FrontedWindowRegistration`

```csharp
// 核心代码示意
info.WindowType = type;

var packageId = FrontedPluginRegistrationContext.CurrentPackageId;
var isBuiltIn = info.IsBuiltIn;
EnsureSafePackageId(packageId);
var canonicalId = FrontedWindowIdentity.BuildCanonicalId(info.Id, packageId, isBuiltIn);

services.AddSingleton<TViewModel>();
services.AddSingleton<TView>(sp =>
{
    var view = ActivatorUtilities.CreateInstance<TView>(sp);
    view.DataContext = sp.GetRequiredService<TViewModel>();
    return view;
});

services.AddSingleton<FrontedWindowRegistration>(new FrontedXamlWindowRegistration
{
    Id = canonicalId,
    LocalId = info.Id,
    PackageId = packageId,
    IsBuiltIn = isBuiltIn,
    DisplayName = info.Name,
    WindowType = type
});
```

`FrontedXamlWindowRegistration` 只暴露 `WindowType`；不再保留 `ViewModelType`。XAML 窗口由 `AddFrontedWindow` 注册工厂在创建时一次性设置 `DataContext`，`CreateXamlWindow` 不再二次解析 ViewModel 或再次设置 `DataContext`，也不再有 `ActivatorUtilities.CreateInstance` fallback。XAML 窗口的 PackageId 仅表示来源，不参与 v3 layout / Designer。

### 2.4 `FrontedWindowRegistryService` 构造

当 DI 容器解析 `IFrontedWindowRegistry` 时，`FrontedWindowRegistryService` 的构造函数从 DI 接收 `IEnumerable<FrontedWindowRegistration>` 集合并构建索引（见 [FrontedWindowRegistryService.cs](../neo-bpsys-wpf.Core/Services/Registry/FrontedWindowRegistryService.cs)）：

1. **收集 registration**：遍历 DI 注入的所有 `FrontedWindowRegistration`
2. **空 ID fail-fast**：空或空白 Canonical ID 直接抛 `InvalidOperationException`，不记录警告后跳过
3. **重复 Canonical ID 检测**：使用 `StringComparer.OrdinalIgnoreCase` 比较 Canonical ID（与 Windows 文件系统默认大小写不敏感语义一致）；若仅大小写不同的 Canonical ID 已存在，启动时 fail fast，抛出 `InvalidOperationException`，异常信息含 ID、PackageId、IsBuiltIn、Kind、XAML WindowType（若存在）
4. **构建索引字典**：`_byCanonicalId` — 按 Canonical ID 索引（OrdinalIgnoreCase）
5. **派生缓存列表**：`_windows`（全部窗口）、`_v3LayoutWindows`（仅 `FrontedV3LayoutWindowRegistration`）

```csharp
// 核心代码示意
_byCanonicalId = new Dictionary<string, FrontedWindowRegistration>(StringComparer.OrdinalIgnoreCase);

foreach (var registration in registrationList)
{
    if (string.IsNullOrWhiteSpace(registration.Id))
    {
        throw new InvalidOperationException(
            $"Fronted window registration has an empty Canonical ID. ...");
    }

    if (_byCanonicalId.TryGetValue(registration.Id, out var existing))
    {
        throw new InvalidOperationException(
            $"Duplicate fronted window Canonical ID '{registration.Id}'. "
            + $"Existing: ... Duplicate: ...");
    }

    _byCanonicalId[registration.Id] = registration;
}

_windows = _byCanonicalId.Values.ToArray();
_v3LayoutWindows = _windows.OfType<FrontedV3LayoutWindowRegistration>().ToArray();
```

`TryGet` 也使用同一 OrdinalIgnoreCase 字典；调用者传入大小写变体后，整条调用链使用 `registration.Id` 作为缓存键和事件 payload，不再使用调用者传入的原始字符串。`GetManageableWindows()` 按 `LocalId`（OrdinalIgnoreCase）排序，不再有 `GroupKey` / `DisplayOrder` 排序逻辑。

### 2.5 `FrontedWindowService` 构造（按需创建）

`FrontedWindowService`（在 [FrontedWindowService.cs](../neo-bpsys-wpf/Services/FrontedWindowService.cs)）被注册为 `Singleton`。与旧架构不同，**构造函数不再预创建所有窗口**，而是采用按需创建（lazy creation）策略：窗口实例在首次调用 `EnsureWindowCreated(windowId)` 时才创建。

```csharp
public FrontedWindowService(...)
{
    _services = services;
    _windowRegistry = windowRegistry;
    // ... 保存依赖
    if (!Directory.Exists(AppConstants.AppDataPath))
        Directory.CreateDirectory(AppConstants.AppDataPath);
}
```

窗口实例通过 `EnsureWindowCreated` 按需加入 `FrontedWindows` 字典，key 为 Canonical ID。

### 2.6 启动链路总结

```
App.Services.xaml.cs
  └─ services.AddFrontedV3LayoutWindow("BpWindow", isBuiltIn: true) × 8
      ├─ EnsureValidLocalWindowId(windowId)
      ├─ EnsureSafePackageId(packageId)
      ├─ BuildCanonicalId → Canonical ID
      └─ 注册 FrontedV3LayoutWindowRegistration 到 DI（含 Id/LocalId/PackageId/IsBuiltIn/DisplayName/Kind）
  └─ PluginService.InitializePlugins(context, services)
      └─ 每个插件：
          └─ using (FrontedPluginRegistrationContext.BeginScope(plugin.Manifest.Id))
              └─ plugin.Initialize(context, services)
                  ├─ services.AddFrontedWindow<TView,TViewModel>()  ← 插件 XAML 窗口
                  └─ services.AddFrontedV3LayoutWindow("WindowId")  ← 插件 v3 窗口
  └─ services.AddSingleton<IFrontedWindowService, FrontedWindowService>()
      └─ new FrontedWindowService()  ← 构造时不创建窗口（按需创建）
          └─ 获取 IFrontedWindowRegistry 实例
              └─ new FrontedWindowRegistryService(IEnumerable<FrontedWindowRegistration>)
                  ├─ 从 DI 接收所有 registration
                  ├─ 空 ID → fail fast
                  ├─ 重复 Canonical ID（仅大小写不同也视为重复）→ fail fast
                  └─ 构建 _byCanonicalId 索引（OrdinalIgnoreCase）

运行时：
  ShowWindow(canonicalId)
    └─ ShowWindowSafelyAsync(canonicalId)  ← Task 方法，完整捕获异常，非 async void
        └─ EnsureWindowCreated(canonicalId)
            ├─ registry.TryGet(canonicalId, out registration) → 用 registration.Id 规范化键
            └─ CreateWindow(registration)
                ├─ V3Layout → new FrontedWindowBase() + InitializeV3LayoutHost(canonicalId, displayName, ...)
                └─ Xaml → _services.GetRequiredService(xaml.WindowType)（DataContext 已在 AddFrontedWindow 工厂中设置）
```

---

## 3. 注册链路

注册链路是指窗口 `FrontedWindowRegistration` 如何被注册到系统中的完整过程。分为 **XAML 窗口注册** 和 **v3 布局窗口注册** 两条路径，二者均通过 DI 注册，由 `FrontedWindowRegistryService` 统一收集。

### 3.1 v3 布局窗口注册

通过 `AddFrontedV3LayoutWindow(string windowId, bool isBuiltIn = false)` 完成。这是内置前台窗口和插件 v3 窗口共享的注册路径。

**内置窗口注册**（在 `App.Services.xaml.cs` 中）：

```csharp
services.AddFrontedV3LayoutWindow("BpWindow", isBuiltIn: true);
```

- `LocalId = "BpWindow"`，`PackageId = null`（不在插件作用域内）
- `Canonical ID = "BpWindow"`（`isBuiltIn = true`）
- `IsBuiltIn = true`，`Kind = V3Layout`
- `DisplayName = "BpWindow"`（默认回退到 LocalId；内置窗口的本地化显示名由 UI 层通过 resx `Designer.Window.{LocalId}` 解析）
- 来源分组由 UI 层基于 `IsBuiltIn + PackageId` 推导为 BuiltIn；顺序使用 DI 注册顺序或 UI 按 `LocalId` 排序

**插件窗口注册**（在插件 `Initialize` 内）：

```csharp
// 假设插件包 ID 为 "a"
services.AddFrontedV3LayoutWindow("Overlay");
```

- `LocalId = "Overlay"`，`PackageId = "a"`（来自 `FrontedPluginRegistrationContext`）
- `Canonical ID = "plugin:a/Overlay"`
- `IsBuiltIn = false`，`Kind = V3Layout`
- `DisplayName` 默认回退到 `LocalId`

### 3.2 XAML 窗口注册

通过 `AddFrontedWindow<TView, TViewModel>()` 完成，要求 `TView` 标注 `[FrontedWindowInfo]` 特性。

**Step 1 — 编译期**：在窗口类上应用特性：

```csharp
// 插件 XAML 窗口示例
[FrontedWindowInfo("3363BFE1-1393-4765-B926-001B6848FAF7", "Example XAML Window")]
public partial class ExampleXamlWindow : FrontedWindowBase { ... }
```

`FrontedWindowInfo` 属性（在 [FrontedWindowInfo.cs](../neo-bpsys-wpf.Core/Attributes/FrontedWindowInfo.cs) 中定义）：

| 属性 | 含义 |
|---|---|
| `Id` | 稳定字符串，XAML 窗口的稳定标识（推荐 GUID，但不强制） |
| `Name` | 窗口显示名 |
| `WindowType` | `internal set` — 由 `AddFrontedWindow` 在注册时设置为 `typeof(TView)` |
| `IsBuiltIn` | 是否内置，attribute named argument，默认 `false` |

内置窗口设 `IsBuiltIn = true`，插件窗口保持默认 `false`。`IsBuiltIn` 不再通过构造函数第三参数传递，而是通过命名参数 `[FrontedWindowInfo("...", "...", IsBuiltIn = true)]` 设置。

**Step 2 — 运行时**：`AddFrontedWindow` 读取特性、生成 Canonical ID、注册 `FrontedXamlWindowRegistration` 到 DI。

### 3.3 Canonical Identity 模型

Canonical ID 是窗口在系统中的唯一稳定标识，用于注册表索引、窗口缓存、布局路径映射、`.bpui` 包导入/导出。

| 来源 | Canonical ID 规则 | 示例 |
|---|---|---|
| 内置 v3 窗口 | `LocalId` | `BpWindow` |
| 内置 XAML 窗口 | Attribute ID | `ACFC0F23-83F4-4607-B473-24D7DB292D23` |
| 插件 v3 窗口 | `plugin:{PackageId}/{LocalId}` | `plugin:a/Overlay` |
| 插件 XAML 窗口 | `plugin:{PackageId}/{AttributeId}` | `plugin:a/3363BFE1-...` |

**插件 ID 隔离**：不同插件注册相同 `LocalId` 不会冲突。插件 A 与插件 B 都注册 `"Overlay"` 时，分别得到 `plugin:a/Overlay` 与 `plugin:b/Overlay`。

### 3.4 插件注册作用域

`FrontedPluginRegistrationContext`（在 [FrontedPluginRegistrationContext.cs](../neo-bpsys-wpf.Core/Services/Registry/FrontedPluginRegistrationContext.cs)，internal static class）使用 `AsyncLocal<string?>` 在异步执行流中携带当前插件包 ID：

- `PluginService` 在调用 `plugin.Initialize(context, services)` 前通过 `BeginScope(plugin.Info.Manifest.Id)` 建立作用域
- 作用域通过 `IDisposable` 在 `using` 语句结束时恢复上一层值
- **异常退出时仍恢复**（`using` 语句保证 `Dispose` 被调用）
- PackageId 不会泄漏到下一个插件
- 注册扩展方法通过 `FrontedPluginRegistrationContext.CurrentPackageId` 读取，**PackageId 不作为 API 参数暴露**

```csharp
// PluginService 中的核心逻辑
using (FrontedPluginRegistrationContext.BeginScope(info.Manifest.Id))
{
    entranceObj.Initialize(context, services);
}
```

### 3.5 注册信息查询

`IFrontedWindowRegistry` 提供以下查询方法（见 [IFrontedWindowRegistry.cs](../neo-bpsys-wpf.Core/Abstractions/Services/IFrontedWindowRegistry.cs)）：

| 方法 | 用途 |
|---|---|
| `GetWindows()` | 获取所有已注册窗口 |
| `GetManageableWindows()` | 获取在后台管理页中显示的窗口（按 `LocalId` 排序） |
| `GetV3LayoutWindows()` | 获取所有 v3 Layout host 窗口注册（`FrontedV3LayoutWindowRegistration`） |
| `TryGet(string canonicalId, out FrontedWindowRegistration registration)` | 按 Canonical ID 查找（OrdinalIgnoreCase） |

`GetManageableWindows()` 的排序逻辑：

1. 按 `LocalId` 排序（`StringComparer.OrdinalIgnoreCase`）

来源分组（BuiltIn / Plugin / External）和类型显示（`Xaml` / `v3 Layout`）由 FrontManage UI 层基于 `IsBuiltIn + PackageId` 和 `registration.Kind` 独立推导，不再有 `GroupKey` / `DisplayOrder` 排序字段。

---

## 4. 窗口创建链路

窗口创建发生在 `FrontedWindowService.EnsureWindowCreated(windowId)` 中，由 `CreateWindow(registration)` 根据 `FrontedWindowRegistration.Kind` 进行分派。窗口实例采用按需创建策略：首次显示时才创建。

### 4.1 创建分派逻辑

```csharp
private Window CreateWindow(FrontedWindowRegistration registration)
{
    return registration switch
    {
        // v3 layout host 窗口（含内置 v3 窗口和插件 v3 窗口）
        FrontedV3LayoutWindowRegistration v3 => CreateV3LayoutHostWindow(v3),

        // XAML 窗口（含内置与插件）— DataContext 已在 AddFrontedWindow 注册工厂中设置
        FrontedXamlWindowRegistration xaml => CreateXamlWindow(xaml.WindowType),

        // 系统只有两个 sealed 派生类，未知类型直接抛异常
        _ => throw new InvalidOperationException(...)
    };
}
```

只有两个分支：`V3Layout` 和 `Xaml`。来源归属（`IsBuiltIn`）不影响创建分派。未知 registration 类型不返回 null，而是抛 `InvalidOperationException`。

### 4.2 v3 布局宿主窗口创建（`CreateV3LayoutHostWindow`）

```csharp
private Window CreateV3LayoutHostWindow(FrontedV3LayoutWindowRegistration registration)
{
    var window = new FrontedWindowBase();
    window.InitializeV3LayoutHost(
        registration.Id,                 // CanonicalWindowId
        registration.DisplayName,        // DisplayName
        _services.GetRequiredService<IFrontedLayoutService>(),
        _services.GetRequiredService<IFrontedRenderer>(),
        _services.GetRequiredService<ISharedDataService>(),
        _services.GetService<IFrontedBehaviorRuntime>(),
        _services.GetService<ILogger<FrontedWindowBase>>(),
        _services.GetService<ISettingsHostService>());
    return window;
}
```

在 `FrontedWindowBase.InitializeV3LayoutHost()`（在 [FrontedWindowBase.cs](../neo-bpsys-wpf.Core/Controls/FrontedWindowBase.cs)）中：

1. **存储依赖**：保存 CanonicalWindowId、DisplayName、layoutService、renderer、sharedDataService、behaviorRuntime、settingsHostService、logger。`FrontedWindowBase` 不再持有整个 `FrontedWindowRegistration`，避免 Registry/UI 元数据泄漏到渲染层
2. **设置标志**：`_isV3LayoutHost = true`
3. **设置标题**：使用 DisplayName（由 `RefreshV3WindowTitle` 处理本地化）
4. **创建 BaseCanvas**：一个 `Canvas` 控件，Name 为 `BaseCanvas`，应用渲染质量选项
5. **包装到 Viewbox**：`Content = new Viewbox { Child = _baseCanvas, Stretch = Fill }`，实现自适应缩放
6. **订阅事件**：`Loaded`, `Unloaded`, `Closed`, `IsVisibleChanged`；若 `settingsHostService` 非 null，订阅 `LanguageSettingChanged`

窗口基础样式设置（构造函数）：
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

v3 host 内部使用 CanonicalWindowId（即 `registration.Id`）作为布局加载、事件发布的窗口标识：

```csharp
var config = await _layoutService.LoadWindowConfigAsync(_canonicalWindowId);
```

### 4.3 XAML 窗口创建（`CreateXamlWindow`）

适用于 `FrontedXamlWindowRegistration` 注册的窗口（典型为插件 XAML 窗口）：

```csharp
private Window CreateXamlWindow(Type windowType)
{
    // DataContext 已在 AddFrontedWindow 注册工厂中设置，这里只解析窗口实例
    var window = _services.GetRequiredService(windowType) as Window;
    return window!;
}
```

对于通过 `AddFrontedWindow` 注册的窗口，`windowType` 已注册为 Singleton 到 DI 容器中，工厂创建时已设置 `DataContext`，因此 `_services.GetRequiredService(windowType)` 返回的实例已具备正确的 ViewModel。`CreateXamlWindow` 不再二次解析 ViewModel、不再设置 DataContext、不再有 `ActivatorUtilities.CreateInstance` fallback。

### 4.4 窗口创建后处理

创建完成后，将窗口加入字典（key 为 Canonical ID）：

```csharp
private void RegisterFrontedWindow(string windowId, Window window)
{
    if (FrontedWindows.TryAdd(windowId, window))
    {
        FrontedWindowStates[windowId] = false;  // 初始状态：隐藏
    }
}
```

`EnsureWindowCreated` 完整流程包含异常捕获：创建失败时记录警告并返回 `null`，调用方据此区分"未注册"与"创建失败"。

```csharp
public Window? EnsureWindowCreated(string windowId)
{
    if (FrontedWindows.TryGetValue(windowId, out var existingWindow))
        return existingWindow;

    if (!_windowRegistry.TryGet(windowId, out var registration))
        return null;

    try
    {
        var window = CreateWindow(registration);
        if (window is null) return null;
        RegisterFrontedWindow(registration.Id, window);
        return window;
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to create fronted window {WindowId}.", registration.Id);
        return null;
    }
}
```

### 4.5 创建链路总结

```
EnsureWindowCreated(canonicalId)
│
├─ 已存在 FrontedWindows[canonicalId] → 返回已有实例
│
├─ registry.TryGet(canonicalId) 失败 → 返回 null（未注册）
│
└─ CreateWindow(registration)
    │
    ├─ FrontedV3LayoutWindowRegistration ─────────────────┐
    │   └─ CreateV3LayoutHostWindow()                     │
    │       └─ new FrontedWindowBase()                    │
    │           ├─ 设置无边框、不可调整大小、居中          │
    │           └─ 绑定鼠标拖拽事件                        │
    │       └─ .InitializeV3LayoutHost(registration, ...)  │
    │           ├─ 存储 registration（用 registration.Id） │
    │           ├─ 创建 BaseCanvas (Canvas)                │
    │           ├─ 包装到 Viewbox 作为 Content             │
    │           ├─ 订阅 Loaded/Unloaded/Closed/VisibleChanged │
    │           └─ 设置标题（本地化）                      │
    │           ← 返回 window                             │
    │                                                      │
    ├─ FrontedXamlWindowRegistration ─────────────────────┐ │
    │   └─ CreateXamlWindow(WindowType, ViewModelType)    │ │
    │       ├─ services.GetService(WindowType) 或         │─┤
    │       │  ActivatorUtilities.CreateInstance          │ │
    │       ├─ 设置 DataContext (ViewModel 已在工厂中设好) │ │
    │       └─ 返回 window                                │ │
    │                                                      │
    └─ 其他 → 返回 null                                   │
                                                           │
RegisterFrontedWindow(canonicalId, window)                 │
  └─ FrontedWindows[canonicalId] = window                  │
  └─ FrontedWindowStates[canonicalId] = false ◄────────────┘

异常时：
  └─ catch → 记录警告，返回 null（创建失败）
```

---

## 5. 窗口管理逻辑

`FrontedWindowService` 管理所有窗口的显示、隐藏、状态跟踪。窗口缓存与状态字典统一使用 Canonical ID 作为 key。

### 5.1 状态管理

| 属性 | 类型 | 说明 |
|---|---|---|
| `FrontedWindows` | `Dictionary<string, Window>` | Canonical ID → Window 实例（按需填充） |
| `FrontedWindowStates` | `Dictionary<string, bool>` | Canonical ID → 是否显示（true=显示中） |

### 5.2 显示/隐藏方法

| 方法 | 行为 |
|---|---|
| `ShowWindow(FrontedWindowType)` | 通过 `FrontedWindowHelper.GetFrontedWindowCanonicalId` 获取 Canonical ID，调用 `ShowWindow(string)` |
| `ShowWindow(string)` | 委托 `ShowWindowAsync`，错误通过 `ContinueWith` 记录日志（**不再是 async void**） |
| `HideWindow(FrontedWindowType)` | 通过枚举获取 Canonical ID，调用 `HideWindow(string)` |
| `HideWindow(string)` | 查找窗口 → Hide() → 更新状态 → 发布事件 |
| `AllWindowShow()` | 遍历所有注册窗口，逐个 `ShowWindowAsync` |
| `AllWindowHide()` | 遍历所有已显示的窗口，逐个隐藏 |

### 5.3 显示流程详解

`ShowWindow(string windowId)` 调用 `ShowWindowAsync(windowId)`，完整流程：

1. **按需创建窗口**：调用 `EnsureWindowCreated(windowId)`；创建失败返回 `null` 时弹出"未注册窗口"错误并记录日志，**区分"未注册"与"创建失败"**
2. **如果已显示**：调用 `window.Show()` + `window.Activate()` 激活窗口，不重复加载
3. **预应用 v3 WindowSettings**：v3 host 调用 `EnsureInitialWindowSettingsAppliedAsync()`，只应用尺寸、位置、Topmost、AllowsTransparency、BackgroundColor 和 ViewboxStretch
4. **应用非 v3 布局选项**：调用 `ApplyWindowLayoutOptions(windowId, window)` — 只对 `Kind != V3Layout` 的窗口生效
5. **显示窗口**：`window.Show()`
6. **更新状态**：`FrontedWindowStates[windowId] = true`
7. **发布事件**：通过 `IFrontedEventBus` 发布 `WindowShown` 事件
8. **异步加载内容**：v3 host fire-and-forget 调用 `LoadOrReloadContentAsync(force: false)`；异常 catch/log，不阻塞窗口 shell 显示

**安全异步**：`ShowWindow(string)` 不再是 `async void`，而是通过 `ContinueWith` 捕获 `Task` 异常并记录日志，避免未观察异常导致宿主崩溃。插件窗口创建/显示异常被捕获并记录，不影响宿主和其他窗口。

### 5.4 事件发布

窗口显示/隐藏时通过 `IFrontedEventBus` 发布 `FrontedBehaviorEvent`：

```csharp
{
    EventType = "WindowShown" or "WindowHidden",
    WindowId = canonicalId,
    Source = "WindowLifecycle",
    Timestamp = DateTimeOffset.UtcNow
}
```

这些事件被 `FrontedBehaviorRuntime` 系统消费，用于响应窗口状态变化。事件发布自身也有 try/catch 保护。

### 5.5 全局布局重载

`ReloadFrontedLayoutsAsync()` 只遍历已经创建的 v3 布局窗口，不创建尚未显示过的窗口：

```csharp
public async Task ReloadFrontedLayoutsAsync()
{
    _services.GetService<IFrontedResourceResolver>()?.ClearCache();

    foreach (var pair in FrontedWindows.ToArray())
    {
        if (pair.Value is not FrontedWindowBase frontedWindow
            || !_windowRegistry.TryGet(pair.Key, out var registration)
            || registration.Kind != FrontedWindowRegistrationKind.V3Layout)
            continue;

        try
        {
            // 若透明度设置变化，需重启窗口（AllowsTransparency 只能在 source 创建前设置）
            var requestedTransparency = await frontedWindow.GetRequestedAllowsTransparencyAsync();
            if (requestedTransparency.HasValue
                && requestedTransparency.Value != frontedWindow.AllowsTransparency)
            {
                await RestartWindowForTransparencyChangeAsync(registration.Id);
                continue;
            }

            await frontedWindow.ReloadFrontedLayoutAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to reload fronted v3 layout for {WindowId}.", registration.Id);
        }
    }
}
```

`AllowsTransparency` 变化时通过 `RestartWindowForTransparencyChangeAsync` 静默重启受影响窗口（移除旧实例 → 重新 `EnsureWindowCreated` → 重新 Show）。

该方法在以下场景被调用：
- 包激活/删除/复制后（由 `FrontManagePageViewModel` 触发）
- 包激活时（`ActivatePackageAsync`）
- 布局包变更事件接收后（`FrontedLayoutPackagesChangedMessage`）

### 5.6 窗口尺寸和背景管理

| 方法 | 行为 |
|---|---|
| `ApplyWindowBackgroundColorAsync(canonicalId)` | v3 窗口从 `WindowSettings` 读取背景色，非 v3 窗口从旧 options 读取，应用到窗口 Background 属性 |
| `ApplyWindowSizeAsync(canonicalId)` | v3 从 `WindowSettings` 读取宽高，非 v3 从 options 读取，应用到窗口 Width/Height |
| `GetWindowSize(canonicalId)` | 获取窗口当前宽高，不可见时返回 null |
| `RestartWindowForTransparencyChangeAsync(canonicalId)` | 重启窗口使透明度设置生效 |

所有 UI 属性的设置都通过 `Dispatcher.InvokeAsync` 确保在 UI 线程执行。v3 / 非 v3 的读取分支由 `registration.Kind == FrontedWindowRegistrationKind.V3Layout` 决定。

---

## 6. 窗口配置读取逻辑

窗口配置分为两个层次：
- **布局配置（Layout Config）**：窗口内容布局（控件、画布、行为等），由 `FrontedLayoutService` 管理
- **WindowSettings**：v3 窗口级外观设置（尺寸、位置、透明度、背景色、Topmost、ViewboxStretch），保存在窗口级布局 JSON 中
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

### 6.2 布局配置加载优先级

`FrontedLayoutService.LoadWindowConfigWithMetadataAsync()`（在 [FrontedLayoutService.cs](../neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedLayoutService.cs)）实现了加载优先级链，所有路径以 **Canonical ID** 为 key：

```
Step 1：尝试从激活包加载
  └─ packageManager.GetPackageLayoutPath(activeState.PackageId, canonicalWindowId)
  └─ 如果文件存在且可读 → 返回
     ├─ 激活包为内置包 → Source = BuiltIn
     └─ 激活包为非内置包 → Source = User

Step 2：仅对内置窗口（非 plugin: Canonical ID），且激活包不是内置包时，回退到内置资源
  └─ 排除插件窗口：TryParsePluginCanonicalWindowId 失败
  └─ packageManager.GetPackageLayoutPath(BuiltInPackageId, canonicalWindowId)
  └─ 如果文件存在且可读 → 返回 Source = BuiltIn

Step 3：返回内存空模板
  └─ configFactory.CreateEmptyConfig(canonicalWindowId)
  └─ 返回 Source = EmptyTemplate, Path = null
```

**关键差异**：
- **内置窗口**：激活包 → 内置资源 → 空模板（三级回退）
- **插件窗口**：激活包 → 空模板（两级回退，**不从插件安装目录加载默认布局**）

**空模板**由 `FrontedV3LayoutWindowConfigFactory`（见 [FrontedV3LayoutWindowConfigFactory.cs](../neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedV3LayoutWindowConfigFactory.cs)）提供：
- 不立即写磁盘
- 不为每个窗口硬编码空 JSON
- 不要求插件安装目录有默认布局
- Designer 首次保存时才创建 JSON
- 空模板可正常渲染和打开 Designer

`FrontedLayoutLoadResult` 结构在 [FrontedLayoutLoadResult.cs](../neo-bpsys-wpf.Core/Models/FrontedLayout/FrontedLayoutLoadResult.cs) 中：

```csharp
public sealed class FrontedLayoutLoadResult
{
    public FrontedWindowConfig? Config { get; init; }
    public FrontedLayoutSource Source { get; init; }  // User / BuiltIn / EmptyTemplate / ...
    public string? Path { get; init; }                // 实际加载路径
    public string? Error { get; init; }               // 加载错误信息
}
```

`FrontedLayoutSource` 枚举值：`User`、`BuiltIn`、`EmptyTemplate`。`PluginDefault` / `MissingOrError` 枚举值已删除（无运行时来源）。

### 6.3 v3 布局路径映射

`FrontedV3LayoutWindowPathHelper`（在 [FrontedV3LayoutWindowPathHelper.cs](../neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedV3LayoutWindowPathHelper.cs)）将 Canonical ID 映射为文件系统安全路径：

| Canonical ID | 相对布局路径 |
|---|---|
| `BpWindow`（内置） | `FrontedLayouts/BpWindow.json` |
| `plugin:{PackageId}/{LocalWindowId}`（插件） | `FrontedLayouts/plugin/{PackageId}/{LocalWindowId}.json` |

内部使用 `TryParsePluginCanonicalWindowId` 解析 `plugin:` 前缀，并通过 `SafeSegmentRegex`（`^[A-Za-z0-9._-]+$`）确保路径段安全，拒绝 `..` 路径穿越。

### 6.4 布局选项读取

`FrontedWindowLayoutOptionsService.LoadOptions()` 的读取路径（在 [FrontedWindowLayoutOptionsService.cs](../neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedWindowLayoutOptionsService.cs) 中），仅用于非 v3 / legacy XAML 窗口：

```
1. 包管理器感知（当 _packageManager 非 null 时）
   └─ 获取活跃包状态
   └─ 如果活跃包不是 BuiltIn
       └─ 包选项路径: {packageLayoutsRoot}/{canonicalWindowId}/window.json
       └─ 如果文件存在 → 从包路径读取
   └─ 否则 → 从旧版路径读取

2. 旧版路径（Legacy Options Path）
   └─ {FrontedLayoutsPath}/{canonicalWindowId}/window.json
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

### 6.5 运行时默认值

[FrontedWindowRuntimeSettings.cs](../neo-bpsys-wpf/ViewModels/Windows/FrontedWindowRuntimeSettings.cs) 提供硬编码的运行时默认值：

```csharp
WindowSize = (1440, 810)            // 默认窗口尺寸
ScoreInGameWindowSize = (480, 152) // 赛中比分窗口尺寸
ScoreGlobalWindowSize = (1440, 195) // 全局比分窗口尺寸
AllowsWindowTransparency = true     // 默认允许透明
BackgroundBrush = TransparentBlack  // 透明黑色背景 (ARGB=0,0,0,0)
```

### 6.6 配置读取链路总结

```
FrontedWindowBase.ReloadFrontedLayoutAsync()
  └─ _layoutService.LoadWindowConfigAsync(_v3Registration.Id)
       └─ LoadWindowConfigWithMetadataAsync(canonicalWindowId)
            │
            ├─ [激活包] → 成功? → User / BuiltIn
            ├─ [内置资源，仅内置窗口] → 成功? → BuiltIn
            └─ [空模板] → EmptyTemplate

FrontedWindowService.ApplyWindowLayoutOptions()
  └─ 仅 Kind != V3Layout 的窗口
  └─ _windowLayoutOptionsService.LoadOptions(canonicalId)
       ├─ [包管理器活跃] → 包路径下 window.json
       └─ [旧版/无包管理器] → FrontedLayouts/{canonicalId}/window.json
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

- 通过 `_layoutService.LoadWindowConfigAsync(_v3Registration.Id)` 加载 `FrontedWindowConfig` 或其中的 `WindowSettings`
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

var config = await _layoutService.LoadWindowConfigAsync(_v3Registration.Id);

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

在 `FrontedWindowService.ApplyWindowLayoutOptions(windowId, window)` 中，仅对 `registration.Kind != V3Layout` 的窗口生效：

```csharp
// 1. v3 窗口直接跳过
if (registration.Kind == FrontedWindowRegistrationKind.V3Layout)
    return;

// 2. 读取选项文件（如果不存在则跳过）
if (!File.Exists(_windowLayoutOptionsService.GetUserOptionsPath(registration.Id)))
    return;

var options = _windowLayoutOptionsService.LoadOptions(registration.Id);

// 3. 应用透明度（source 创建后可能抛 InvalidOperationException，catch 后仅 Debug 日志）
window.AllowsTransparency = options.AllowTransparency;

// 4. 应用背景色
if (TryCreateBackgroundBrush(options.BackgroundColor, out var brush))
    window.SetCurrentValue(Window.BackgroundProperty, brush);
```

**差异对比**：

| 方面 | v3 布局窗口 | 非 v3 布局窗口 |
|---|---|---|
| 配置来源 | `FrontedWindowConfig`（布局 JSON） | `FrontedWindowLayoutOptions`（选项 JSON） |
| 应用时机 | Show 前轻量应用 WindowSettings，Show 后异步加载内容 | 每次显示时（`ShowWindow`） |
| 应用内容 | Show 前：尺寸、位置、透明、背景；Show 后：画布、控件、行为 | 透明、背景色 |
| 尺寸同步 | 自动同步画布到窗口 | 通过独立的 `ApplyWindowSizeAsync()` 方法 |

### 7.3 主动配置应用

除了显示时的自动应用，也可以通过以下方法主动触发：

```csharp
// 立即应用背景色
await _frontedWindowService.ApplyWindowBackgroundColorAsync("BpWindow");
// 立即应用窗口尺寸
await _frontedWindowService.ApplyWindowSizeAsync("BpWindow");
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

InitializeV3LayoutHost()  ← 由 FrontedWindowService.CreateV3LayoutHostWindow 调用
  ├─ 存储所有依赖（registration、layoutService、renderer 等）
  ├─ 创建 BaseCanvas（Canvas 控件，背景透明）
  ├─ 包装到 Viewbox（实现自适应缩放）
  └─ 订阅事件：
      ├─ Loaded     → OnV3HostLoaded
      ├─ Unloaded   → OnV3HostUnloaded
      ├─ Closed     → OnV3HostClosed
      └─ IsVisibleChanged → OnV3HostIsVisibleChanged

ShowWindow（FrontedWindowService）
  ├─ EnsureWindowCreated(canonicalId) ← 首次显示才创建 shell
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

### 8.2 内建 v3 窗口生命周期

所有内置前台窗口（`BpWindow`、`CutSceneWindow`、`ScoreSurWindow`、`ScoreHunWindow`、`ScoreGlobalWindow`、`GameDataWindow`、`BpOverviewWindow`、`MapV2Window`）均为 v3 布局窗口，由 `FrontedWindowBase` 统一承载，共享上述生命周期逻辑。它们没有各自的 XAML 窗口类，由 `AddFrontedV3LayoutWindow(name, isBuiltIn: true)` 注册，运行时通过 `new FrontedWindowBase()` + `InitializeV3LayoutHost` 创建。

### 8.3 窗口关闭行为

所有前台窗口（v3 布局宿主和 XAML 窗口）都重写了 `OnClosing`：

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

服务侧的强制关闭通过 `RequestServiceClose()`（v3 host）或 `Close()`（XAML 窗口）绕过 `OnClosing` 取消逻辑，用于透明度变化重启窗口场景。

### 8.4 全局生命周期（`FrontedWindowService` 视角）

```
FrontedWindowService 构造
  └─ 不预创建窗口（按需创建策略）
  └─ 确保 AppData 目录存在

运行时
  └─ ShowWindow(canonicalId) → EnsureWindowCreated → CreateWindow → 存入 FrontedWindows
  └─ HideWindow(canonicalId) → window.Hide() + 状态更新
  └─ ReloadFrontedLayoutsAsync() → 遍历已创建 v3 窗口重载

Shutdown / Dispose
  └─ 窗口随 Application 生命周期结束自然销毁
```

**注意**：没有显式的 Dispose 或 Shutdown 逻辑。窗口实例在应用程序退出时由 WPF 框架清理。

---

## 9. 关键代码位置索引

| 功能 | 文件 |
|---|---|
| DI 注册入口 | [App.Services.xaml.cs](../neo-bpsys-wpf/App.Services.xaml.cs) |
| v3 窗口注册扩展 | [FrontedV3LayoutWindowRegistryExtensions.cs](../neo-bpsys-wpf.Core/Extensions/Registry/FrontedV3LayoutWindowRegistryExtensions.cs) |
| XAML 窗口注册扩展 | [FrontedWindowRegistryExtensions.cs](../neo-bpsys-wpf.Core/Extensions/Registry/FrontedWindowRegistryExtensions.cs) |
| `[FrontedWindowInfo]` 特性 | [FrontedWindowInfo.cs](../neo-bpsys-wpf.Core/Attributes/FrontedWindowInfo.cs) |
| 注册基类 | [FrontedWindowRegistration.cs](../neo-bpsys-wpf.Core/Models/FrontedLayout/Registrations/FrontedWindowRegistration.cs) |
| XAML 窗口注册 | [FrontedXamlWindowRegistration.cs](../neo-bpsys-wpf.Core/Models/FrontedLayout/Registrations/FrontedXamlWindowRegistration.cs) |
| v3 布局窗口注册 | [FrontedV3LayoutWindowRegistration.cs](../neo-bpsys-wpf.Core/Models/FrontedLayout/Registrations/FrontedV3LayoutWindowRegistration.cs) |
| 承载方式枚举 | [FrontedWindowRegistrationKind.cs](../neo-bpsys-wpf.Core/Models/FrontedLayout/Registrations/FrontedWindowRegistrationKind.cs) |
| 窗口注册表服务 | [FrontedWindowRegistryService.cs](../neo-bpsys-wpf.Core/Services/Registry/FrontedWindowRegistryService.cs) |
| 窗口注册表接口 | [IFrontedWindowRegistry.cs](../neo-bpsys-wpf.Core/Abstractions/Services/IFrontedWindowRegistry.cs) |
| 插件注册作用域 | [FrontedPluginRegistrationContext.cs](../neo-bpsys-wpf.Core/Services/Registry/FrontedPluginRegistrationContext.cs) |
| v3 Canonical ID 生成 | [FrontedWindowIdentity.cs](../neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedWindowIdentity.cs) |
| v3 局部 ID 校验 | [FrontedV3LayoutWindowIdValidator.cs](../neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedV3LayoutWindowIdValidator.cs) |
| v3 空模板工厂 | [FrontedV3LayoutWindowConfigFactory.cs](../neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedV3LayoutWindowConfigFactory.cs) |
| v3 路径映射 | [FrontedV3LayoutWindowPathHelper.cs](../neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedV3LayoutWindowPathHelper.cs) |
| 窗口管理器实现 | [FrontedWindowService.cs](../neo-bpsys-wpf/Services/FrontedWindowService.cs) |
| 窗口管理器接口 | [IFrontedWindowService.cs](../neo-bpsys-wpf.Core/Abstractions/Services/IFrontedWindowService.cs) |
| 窗口基类（v3 布局宿主） | [FrontedWindowBase.cs](../neo-bpsys-wpf.Core/Controls/FrontedWindowBase.cs) |
| 布局配置模型 | [FrontedWindowConfig.cs](../neo-bpsys-wpf.Core/Models/FrontedLayout/FrontedWindowConfig.cs) |
| 布局配置服务 | [FrontedLayoutService.cs](../neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedLayoutService.cs) |
| 布局配置接口 | [IFrontedLayoutService.cs](../neo-bpsys-wpf.Core/Abstractions/Services/IFrontedLayoutService.cs) |
| 布局选项模型 | [FrontedWindowLayoutOptions.cs](../neo-bpsys-wpf.Core/Models/FrontedLayout/FrontedWindowLayoutOptions.cs) |
| 布局选项服务 | [FrontedWindowLayoutOptionsService.cs](../neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedWindowLayoutOptionsService.cs) |
| 布局选项接口 | [IFrontedWindowLayoutOptionsService.cs](../neo-bpsys-wpf.Core/Abstractions/Services/IFrontedWindowLayoutOptionsService.cs) |
| 枚举到 Canonical ID 映射 | [FrontedWindowHelper.cs](../neo-bpsys-wpf.Core/Helpers/FrontedWindowHelper.cs) |
| 设计器布局目录 | [FrontedDesignerLayoutCatalog.cs](../neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedDesignerLayoutCatalog.cs) |
| 运行时默认设置 | [FrontedWindowRuntimeSettings.cs](../neo-bpsys-wpf/ViewModels/Windows/FrontedWindowRuntimeSettings.cs) |
| 前台管理页 ViewModel | [FrontManagePageViewModel.cs](../neo-bpsys-wpf/ViewModels/Pages/FrontManagePageViewModel.cs) |
| 前台管理页视图 | [FrontedWindowsView.xaml](../neo-bpsys-wpf/Views/Pages/FrontManage/FrontedWindowsView.xaml) |
| 插件服务（作用域建立） | [PluginService.cs](../neo-bpsys-wpf/Services/PluginService.cs) |
| LoadResult 模型 | [FrontedLayoutLoadResult.cs](../neo-bpsys-wpf.Core/Models/FrontedLayout/FrontedLayoutLoadResult.cs) |
| 插件 XAML 窗口示例 | [ExampleXamlWindow.xaml.cs](../neo-bpsys-wpf.ExamplePlugin/Views/ExampleXamlWindow.xaml.cs) |
| 插件注册示例 | [ExamplePlugin.cs](../neo-bpsys-wpf.ExamplePlugin/ExamplePlugin.cs) |

---

## 10. 架构总图

```
┌──────────────────────────────────────────────────────────┐
│                    App.Services.xaml.cs                   │
│  DI 容器注册                                              │
│  ┌─────────────────────────────────────────────────────┐ │
│  │ AddFrontedV3LayoutWindow("BpWindow", isBuiltIn:true)│ │
│  │ AddFrontedV3LayoutWindow("CutSceneWindow", ...)     │ │
│  │ ... (8 个内置 v3 窗口)                               │ │
│  │                                                     │ │
│  │ PluginService.InitializePlugins(context, services)  │ │
│  │   └─ using BeginScope(plugin.Manifest.Id)           │ │
│  │       └─ plugin.Initialize(...)                     │ │
│  │           ├─ AddFrontedWindow<TView,TViewModel>()   │ │
│  │           └─ AddFrontedV3LayoutWindow("WindowId")   │ │
│  │                                                     │ │
│  │ AddSingleton<IFrontedWindowService, FrontedWindow...│ │
│  │ AddSingleton<IFrontedWindowRegistry, FrontedWindow..│ │
│  │ AddSingleton<IFrontedLayoutService, FrontedLayout..│ │
│  │ AddSingleton<IFrontedWindowLayoutOptionsService,...│ │
│  │ AddSingleton<IFrontedBehaviorRuntime, FrontedBehav..│ │
│  └─────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────┘
                            │
                            ▼
┌──────────────────────────────────────────────────────────┐
│                FrontedWindowRegistryService               │
│  注册表（单例）— 从 DI 接收 IEnumerable<Registration>     │
│  ┌─────────────────────────────────────────────────────┐ │
│  │ FrontedV3LayoutWindowRegistration（内置）            │ │
│  │   ├─ BpWindow          Id="BpWindow"                │ │
│  │   ├─ CutSceneWindow    Id="CutSceneWindow"          │ │
│  │   ├─ ScoreGlobalWindow Id="ScoreGlobalWindow"       │ │
│  │   ├─ ScoreSurWindow    Id="ScoreSurWindow"          │ │
│  │   ├─ ScoreHunWindow    Id="ScoreHunWindow"          │ │
│  │   ├─ GameDataWindow    Id="GameDataWindow"          │ │
│  │   ├─ BpOverviewWindow  Id="BpOverviewWindow"        │ │
│  │   └─ MapV2Window       Id="MapV2Window"             │ │
│  │                                                     │ │
│  │ FrontedV3LayoutWindowRegistration（插件）            │ │
│  │   └─ Id="plugin:{PackageId}/{LocalWindowId}"        │ │
│  │                                                     │ │
│  │ FrontedXamlWindowRegistration（插件 XAML）           │ │
│  │   └─ Id="plugin:{PackageId}/{AttributeId}" 或 Id    │ │
│  │                                                     │ │
│  │ 索引：_byCanonicalId（重复 ID → fail fast）          │ │
│  │ 查询：GetWindows / GetManageableWindows /            │ │
│  │       GetV3LayoutWindows / TryGet(canonicalId)      │ │
│  └─────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────┘
                            │
                            ▼
┌──────────────────────────────────────────────────────────┐
│                FrontedWindowService                       │
│  窗口管理器（单例）— 按需创建                             │
│  ┌─────────────────────────────────────────────────────┐ │
│  │ FrontedWindows: Dictionary<canonicalId, Window>     │ │
│  │   （首次 ShowWindow 时才填充）                       │ │
│  │                                                     │ │
│  │ FrontedWindowStates: Dictionary<canonicalId, bool>   │ │
│  │                                                     │ │
│  │ EnsureWindowCreated(canonicalId)                    │ │
│  │   └─ CreateWindow(registration)                     │ │
│  │       ├─ V3Layout → new FrontedWindowBase()         │ │
│  │       │             + InitializeV3LayoutHost        │ │
│  │       └─ Xaml     → DI 获取窗口 + 设置 ViewModel     │ │
│  │                                                     │ │
│  │ ShowWindow → ShowWindowAsync（安全异步，非 async void）│ │
│  │ HideWindow / AllWindowShow / AllWindowHide          │ │
│  │ ReloadFrontedLayoutsAsync()  ← 仅已创建 v3 窗口      │ │
│  │ RestartWindowForTransparencyChangeAsync()           │ │
│  └─────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────┘
                            │
                            ▼
┌──────────────────────────────────────────────────────────┐
│  FrontedWindowBase（v3 布局宿主，所有内置窗口共享）       │
│  ┌─────────────────────────────────────────────────────┐ │
│  │ InitializeV3LayoutHost(registration, ...)           │ │
│  │   └─ 用 registration.Id 作为布局加载 / 事件标识      │ │
│  │ ReloadFrontedLayoutAsync()                          │ │
│  │   ├─ EnsureInitialWindowSettingsAppliedAsync()      │ │
│  │   └─ LoadOrReloadContentAsync(force)                │ │
│  │ BO 模式订阅 → MarkLayoutDirty → 重载                │ │
│  │ 行为运行时管理（attach / detach）                    │ │
│  │ 关闭时 Cancel+Hide（RequestServiceClose 绕过）       │ │
│  └─────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────┘
                            │
                            ▼
┌──────────────────────────────────────────────────────────┐
│  FrontedLayoutService        FrontedWindowLayoutOptions   │
│  布局配置服务                Service                      │
│  ┌─────────────────────┐   ┌───────────────────────────┐ │
│  │ LoadWindowConfig()  │   │ LoadOptions()             │ │
│  │   优先级：           │   │   仅 Kind != V3Layout     │ │
│  │   ① 激活包          │   │   包感知路径 → 旧版路径    │ │
│  │   ② 内置资源(内置)  │   │ SaveOptions()             │ │
│  │   ③ 空模板          │   └───────────────────────────┘ │
│  │ SaveWindowConfig()  │                                  │
│  └─────────────────────┘                                  │
│                                                           │
│  FrontedV3LayoutWindowPathHelper                          │
│  ┌─────────────────────────────────────────────────────┐ │
│  │ BpWindow → FrontedLayouts/BpWindow.json             │ │
│  │ plugin:{Pkg}/{Local} → FrontedLayouts/plugin/{Pkg}/{Local}.json │ │
│  └─────────────────────────────────────────────────────┘ │
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

所有内置前台窗口均为 v3 布局窗口，通过 `AddFrontedV3LayoutWindow(name, isBuiltIn: true)` 注册。来源分组统一为 BuiltIn（由 `IsBuiltIn = true` 推导）；顺序使用 DI 注册顺序或 UI 按 `LocalId` 排序；内置窗口的本地化显示名由 UI 层通过现有 resx（`Designer.Window.{LocalId}`）解析。

| 窗口 | `FrontedWindowType` | Canonical ID | 注册方式 | 有 XAML 类 |
|---|---|---|---|---|
| BpWindow | `BpWindow` | `BpWindow` | `AddFrontedV3LayoutWindow` | ❌ |
| CutSceneWindow | `CutSceneWindow` | `CutSceneWindow` | `AddFrontedV3LayoutWindow` | ❌ |
| ScoreSurWindow | `ScoreSurWindow` | `ScoreSurWindow` | `AddFrontedV3LayoutWindow` | ❌ |
| ScoreHunWindow | `ScoreHunWindow` | `ScoreHunWindow` | `AddFrontedV3LayoutWindow` | ❌ |
| ScoreGlobalWindow | `ScoreGlobalWindow` | `ScoreGlobalWindow` | `AddFrontedV3LayoutWindow` | ❌ |
| GameDataWindow | `GameDataWindow` | `GameDataWindow` | `AddFrontedV3LayoutWindow` | ❌ |
| BpOverviewWindow | `BpOverviewWindow` | `BpOverviewWindow` | `AddFrontedV3LayoutWindow` | ❌ |
| MapV2Window | `MapV2Window` | `MapV2Window` | `AddFrontedV3LayoutWindow` | ❌ |

## 附录 B：插件窗口注册示例

```csharp
// 插件 Initialize 内（此时 FrontedPluginRegistrationContext.CurrentPackageId 为该插件包 ID）

// 1. XAML 窗口
[FrontedWindowInfo("3363BFE1-1393-4765-B926-001B6848FAF7", "Example XAML Window")]
public partial class ExampleXamlWindow : FrontedWindowBase { ... }

services.AddFrontedWindow<ExampleXamlWindow, ExampleXamlWindowViewModel>();
// → FrontedXamlWindowRegistration { Id="plugin:{Pkg}/3363BFE1-...", Kind=Xaml, IsBuiltIn=false }

// 2. v3 布局窗口
services.AddFrontedV3LayoutWindow("ExampleLayoutOverlay");
// → FrontedV3LayoutWindowRegistration { Id="plugin:{Pkg}/ExampleLayoutOverlay", Kind=V3Layout, IsBuiltIn=false }
```

PackageId 由宿主通过 `FrontedPluginRegistrationContext` 自动注入，**不作为 API 参数暴露**。

## 附录 C：依赖关系图

```
FrontedWindowService
  ├─ IFrontedWindowRegistry → FrontedWindowRegistryService
  │    └─ IEnumerable<FrontedWindowRegistration>（由 DI 收集）
  ├─ IFrontedWindowLayoutOptionsService → FrontedWindowLayoutOptionsService
  │    └─ IFrontedLayoutPackageManager (optional)
  ├─ IFrontedEventBus → FrontedEventBus
  └─ IServiceProvider

FrontedWindowRegistryService
  └─ IEnumerable<FrontedWindowRegistration>（来自 DI）
       ├─ FrontedV3LayoutWindowRegistration（内置 + 插件 v3）
       └─ FrontedXamlWindowRegistration（插件 XAML）

FrontedWindowBase (per window instance)
  ├─ FrontedWindowRegistration（v3 host 存储引用，用 registration.Id 加载布局）
  ├─ IFrontedLayoutService
  │    ├─ IFrontedUserLayoutStore
  │    ├─ IFrontedLayoutPackageManager
  │    └─ FrontedV3LayoutWindowConfigFactory（空模板）
  ├─ IFrontedRenderer
  ├─ ISharedDataService
  ├─ IFrontedBehaviorRuntime (optional)
  ├─ ISettingsHostService (optional)
  └─ ILogger (optional)

FrontedLayoutService
  ├─ IFrontedUserLayoutStore
  ├─ IFrontedLayoutPackageManager
  ├─ FrontedV3LayoutWindowConfigFactory
  └─ FrontedV3LayoutWindowPathHelper（静态，路径映射）

FrontedDesignerLayoutCatalog
  └─ IFrontedWindowRegistry
       └─ GetV3LayoutWindows()（XAML 窗口不进入 Designer）

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

## 附录 D：`.bpui` 包契约

`.bpui` 包对外契约在新架构下**完全不变**：

- `FormatVersion = 3`
- `LayoutSchemaVersion = 3`
- `Content.Layouts[].Window` / `Content.Layouts[].Path` / `Content.Resources` / `Content.Preview`
- `PluginDependencies` / `ImportPolicy`
- 包导入/导出使用 Registry Canonical ID
- round-trip 不得改写 `plugin:a/Overlay` 等插件窗口标识
- Importer 保留未知插件布局

路径映射规则不变：`BpWindow` → `FrontedLayouts/BpWindow.json`；`plugin:{PackageId}/{LocalWindowId}` → `FrontedLayouts/plugin/{PackageId}/{LocalWindowId}.json`。
