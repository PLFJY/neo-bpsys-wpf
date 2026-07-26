# 插件系统

前台行为过滤使用显式事件 payload 和稳定控件身份，不提供临时行为标签。后台引导高亮事件不暴露给前台行为触发器，插件前台动画应使用 `Guidance.StepChanged`。布局 package manager 存在时，活动包（包括 `builtin`）是布局和包资源的权威来源。

## 设计原则

插件系统是全信任模型，不做沙箱隔离。插件可以向 DI 注册服务、页面、窗口，也可以访问宿主暴露的服务和共享数据。当前风险控制依赖插件市场审核、微步云恶意文件扫描、人工审查，以及插件生态规模较小。

因此，插件能力强，但也必须把“安装插件等同于信任该代码”作为维护前提。

## 插件目录

| 类型 | 路径 |
| --- | --- |
| 用户插件 | `%APPDATA%\neo-bpsys-wpf\Plugins` |
| 内置插件 | `{AppBaseDirectory}\Plugins` |
| 插件配置 | `%APPDATA%\neo-bpsys-wpf\PluginConfigs\{pluginId}` |
| 暂存更新 | `%APPDATA%\neo-bpsys-wpf\Plugins\.new\{pluginId}` |

宿主启动时会先处理 `.new`，把暂存更新覆盖移动到正式插件目录，然后删除 `.new`。

## manifest.yml

每个插件目录必须包含 `manifest.yml`。核心字段见 `PluginManifest`：

| 字段 | 说明 |
| --- | --- |
| `id` | 插件唯一 ID |
| `name` | 显示名称 |
| `description` | 描述 |
| `entranceAssembly` | 入口程序集 |
| `url` | 项目地址，可选 |
| `version` | 插件自身版本 |
| `apiVersion` | 插件 API 版本 |
| `author` | 作者 |
| `icon` | 图标路径，默认 `icon.png` |

插件 API 版本和 PluginSdk 源码引用版本是两个概念：

| 名称 | 用途 |
| --- | --- |
| 插件 API 版本 | `manifest.yml` 的 `apiVersion`，用于宿主兼容性检查 |
| PluginSdk 源码引用版本 | 插件项目通过 `ProjectReference` 引用的 `neo-bpsys-wpf.PluginSdk` 所在仓库提交，用于编译期 API 和打包目标 |

v3 起不再发布或推荐使用 PluginSdk NuGet 包。插件作者应 clone 本仓库，在插件解决方案中包含 `neo-bpsys-wpf.PluginSdk` 项目，并显式引用同一份源码。不要把 `apiVersion` 和 SDK 源码提交不一致当成版本错误；真正的宿主兼容性仍由 `manifest.yml` 的 `apiVersion` 判断。

## 插件开发引用方式

v3 插件项目应手动包含 SDK 源码，而不是引用 NuGet 包。参考 `neo-bpsys-wpf.ExamplePlugin`：

```xml
<ItemGroup>
  <ProjectReference Include="..\neo-bpsys-wpf.PluginSdk\neo-bpsys-wpf.PluginSdk.csproj" Private="false" />
</ItemGroup>

<Import Project="..\neo-bpsys-wpf.PluginSdk\neo-bpsys-wpf.PluginSdk.targets" />
```

`ProjectReference` 提供插件 API 编译引用，`Import` 提供 `CreateZip` 打包 target。插件仓库若独立维护，建议把本仓库作为 submodule、sparse checkout 或固定提交的源码目录引入，并在升级 SDK 时同步验证宿主版本、`apiVersion` 和插件 zip 输出。

## 加载流程

`PluginService.InitializePlugins(context, services)` 在 Host build 前执行：

1. 创建用户插件目录。
2. 合并内置插件目录和用户插件目录。
3. 应用 `.new` 中的暂存更新。
4. 读取每个插件的 `manifest.yml`。
5. 构造 `PluginInfo`，记录插件目录、图标路径、内置标记。
6. 处理禁用、卸载标记、重复 ID。
7. 检查插件 API 兼容性。
8. `Assembly.LoadFrom(entranceAssembly)`。
9. 查找直接继承 `PluginBase` 的入口类型。
10. 创建入口实例，设置 `Info` 和 `PluginConfigFolder`。
11. 调用 `Initialize(context, services)`。
12. 把插件实例注册为 singleton。

插件的 `Initialize` 可以注册：

| 能力 | API |
| --- | --- |
| 后台页面 | `services.AddBackendPage<TPage,TViewModel>()` |
| 插件前台窗口 | XAML 窗口使用 `services.AddFrontedWindow<TWindow,TViewModel>()`；v3 layout 窗口使用 `services.AddFrontedV3LayoutWindow("WindowId", isBuiltIn: false)` |
| Designer v3 插件控件 | `services.AddFrontedV3Control<TControl>()`（控件继承 `FrontedV3ControlBase` 并标注 `[FrontedV3Control]`） |
| 自定义服务 | 常规 `services.AddSingleton/AddTransient/...` |
| 配置文件 | `PluginBase.PluginConfigFolder` + `ConfigureFileHelper` |
| 共享数据访问 | 注入 `ISharedDataService` |

插件只在启动时加载。当前代码没有热加载机制，也不要假设复制文件到插件目录后当前进程会立刻发现新页面或窗口。

`Assembly.LoadFrom` 使用入口程序集路径加载插件。依赖解析依赖 .NET 默认加载上下文、插件输出目录和宿主已有程序集；插件包漏掉自身直接依赖时，常见表现是入口程序集加载失败或 `Initialize` 中类型解析失败。

## Designer v3 插件前台控件

插件前台系统围绕 Designer v3 / FrontedLayout v3 工作，使用统一 V3 Control API。旧的前台控件注入 API（`IFrontedControl`、`IFrontedControlPluginContributor`、`FrontedPluginControlDescriptor`、`AddFrontedPluginControlContributor<T>()` 等）已移除；插件前台能力分为 Designer v3 插件控件、Plugin XAML Window 和 Plugin v3 Layout Window。`.bpui` 导入遇到缺失插件窗口或插件控件时会保留 layout、资源和依赖元数据，不再物理删除缺失插件控件。

插件控件的 `ControlType`（Canonical Control Type）命名规则：

```text
plugin:<PackageId>/<ControlId>
```

示例：

```text
plugin:plfjy.ExamplePlugin/TeamCard
```

`PackageId` 必须匹配插件 `manifest.yml` 的 `id`，`ControlId` 在插件内唯一。完整 `ControlType` 是稳定序列化 schema，不本地化，不使用显示名，也不能 shadow 内置控件类型。`.bpui v3` 中的 Canvas `RequiredPlugins` 和 manifest `PluginDependencies` 规则见 [bpui-package-v3.md](../frontend/bpui-package-v3.md)。

### 创建与注册控件

插件控件必须继承 `FrontedV3ControlBase`（定义在 Core 程序集，命名空间为 `neo_bpsys_wpf.PluginSdk`）并标注 `[FrontedV3Control("ControlId")]`：

```csharp
[FrontedV3Control("TeamCard")]
public partial class TeamCardControl : FrontedV3ControlBase
{
    public TeamCardControl()
    {
        InitializeComponent();
    }

    protected override void OnInitializeFrontedV3(FrontedV3ControlContext context)
    {
        DataContext = context.Options;
    }
}
```

注册：

```csharp
services.AddFrontedV3Control<TeamCardControl>();
```

`AddFrontedV3Control<TControl>()` 只接受控件类型一个参数。`PackageId` 由宿主在插件初始化作用域内自动注入，控件作者不得传入 `CanonicalControlType`、`Config factory`、`CreateControl delegate` 或 `Property descriptor list`。`ControlId` 只接受安全的局部标识：非空、非纯空白，且不含 `/`、`\`、`:`，也不允许直接传入完整的 canonical ID。`IsBuiltIn` 仅供宿主注册代码设置为 `true`；插件设置该值为 `true` 会被拒绝。

### 声明属性

属性通过控件类上的 `public static readonly FrontedV3Property<T>` 字段声明，框架在注册时反射发现并校验：

```csharp
public static readonly FrontedV3Property<string> TextColorProperty =
    new("Appearance.TextColor", FrontedV3Storage.ExtensionData("TextColor"));

public static readonly FrontedV3Property<string> TeamNameProperty =
    new("Content.TeamName", FrontedV3Storage.ExtensionData("TeamName"));
```

`OptionsPath`（如 `Appearance.TextColor`）只是 Designer 属性网格与 StyleTransfer 的逻辑路径，**不进入 JSON**；实际读写位置由 `Storage` 访问器决定。`FrontedV3Storage` 提供三种存储访问器：

| 存储访问器 | 用途 | JSON 位置 |
| --- | --- | --- |
| `FrontedV3Storage.ExtensionData("key")` | 插件控件默认存储，读写 `PluginFrontedControlConfig.ExtensionData` 字典 | 序列化后平铺到 JSON 根级字段 |
| `FrontedV3Storage.ClrProperty("PropertyName")` | 内置控件使用，反射读写 Config 的 CLR 属性 | 由 Config 类的 JSON 序列化决定 |
| `FrontedV3Storage.CollectionItemProperty(...)` | 读写 PartCollection 集合项的 CLR 属性 | 由集合项的 JSON 序列化决定 |

存储访问器不得覆盖根级保留字段（`Left`/`Top`/`Width`/`Height`/`ZIndex`/`Visibility`/`BehaviorGuid`/`GaussianBlur`/`ControlType`），该校验在注册时完成。

### Options 动态代理视图

`Options` 是由属性 Schema 构建的动态代理视图，**不进入 JSON**，**不缓存独立值**。XAML 中将 `DataContext` 设置为 `Options`，绑定路径 `{Binding Appearance.TextColor}` 通过 `ICustomTypeDescriptor` 发现动态属性，最终委托到对应存储访问器，直接读写当前 Config 的根级字段。

### 根布局由 Host 管理

控件 **不** 管理自身的 Canvas 坐标（`Left`/`Top`/`Width`/`Height`/`ZIndex`/`Visibility`/`GaussianBlur`），这些由 `FrontedV3ControlHost` 统一负责。运行时结构为 `Canvas → FrontedV3ControlHost → FrontedV3ControlBase`。控件只负责矩形区域内的视觉内容。

### 固定 Part 与 PartCollection

固定 Part 系统管理控件内部固定区域（如 BorderedImage 的内层 Image），通过 `public static readonly FrontedV3Part` 字段声明，XAML 中用 `fronted:FrontedV3.PartId="Logo"` 标记 Part Visual。PartCollection 系统管理模板或动态集合（如 GlobalScoreRow 的 Cells），通过 `public static readonly FrontedV3Parts` 字段声明，支持 `FixedTemplate`、`Dynamic`、`ReadOnly` 三种策略。详见 [fronted-designer-v3.md](../frontend/fronted-designer-v3.md)。

### StyleTransfer

`FrontedV3StyleTransferService` 提供父-子继承与同 peer 传播。Peer 传播仅匹配完全相同的 `CanonicalControlType`；默认仅传播 `Appearance` 语义的属性；`DataIdentity` 语义（MapKey、TeamType、BindingPath、ControlName 等）和根级保留字段**永远不传播**。属性语义通过 `FrontedV3PropertyMetadata.Semantic` 声明，默认为 `Other`（不参与传播）。

### 缺失插件数据不会丢失

运行时读取布局时，`plugin:*` 控件即使插件未安装也会反序列化为 `PluginFrontedControlConfig`，并通过 `JsonExtensionData` 保留插件专属属性，确保可读取、保存和 roundtrip。缺失插件时：

- `ExtensionData` 原样保留，不会丢失。
- Designer 显示 Missing Plugin placeholder，根控件仍可选择、移动、缩放和删除。
- 运行时默认跳过该控件并记录 warning，不让前台窗口崩溃。
- 导出会继续保留控件 JSON、`ExtensionData` 和依赖元数据。
- 不会写入任何默认值掩盖缺失。
- 安装插件并重启后，schema 恢复可用。

### 插件控件属性建议

1. 插件专属属性使用 `FrontedV3Property<T>` + `FrontedV3Storage.ExtensionData("key")` 声明，值存储到 `ExtensionData` 字典并序列化为 JSON 根级字段。
2. 布局 JSON 不保存可执行状态。
3. 避免保存绝对本地路径；图片等资源优先使用 `.bpui` 支持的资源 URI。
4. `BindingPath` 保存原始不变量路径，不本地化。
5. 不要使用泛名 `IsActive` 表示控件业务状态、可见性或启用状态；`IsActive` 只保留给内部框架/运行时激活语义，尤其是 CommunityToolkit.Mvvm `ObservableRecipient.IsActive`。请使用 `IsVisible`、`IsEnabled`、`IsSelected`、`IsExpanded` 或更具体的名称。

## Naming rule: do not use generic IsActive

`IsActive` is reserved for internal framework/runtime activation semantics, especially CommunityToolkit.Mvvm `ObservableRecipient.IsActive`.

Do not use `IsActive` for layout/package/settings/business data.

Use explicit names:
- `IsActivePackage`
- `IsCurrentPackage`
- `IsVisible`
- `IsBadgeVisible`
- `IsEnabled`
- `IsSelected`
- `IsExpanded`
- `IsVisibleInFrontManage`

Legacy note:
Old `.bpui` packages may contain `IsActive` inside `TextSettings` because old settings classes inherited `ObservableRecipient`.
That field is serialization leakage and must be ignored by LegacyConverter.

Visibility bindings must use `IsVisible` or a specific visibility-oriented property. Do not bind `Visibility` to generic `IsActive`.

运行时读取布局时，`plugin:*` 控件即使插件未安装也会反序列化为 `PluginFrontedControlConfig`，并通过 `JsonExtensionData` 保留插件专属属性，确保可读取、保存和 roundtrip。插件已注册时，宿主通过 `FrontedV3ControlRegistry` 查找 `FrontedV3ControlRegistration`，由 `FrontedV3ControlHost` 创建控件实例并应用根布局。如果插件缺失，Designer 显示 Missing Plugin placeholder，前台 renderer 跳过该控件并记录 warning；未知非插件 `ControlType` 仍按无效内置控件处理并报错。

## 插件前台窗口 v3

插件窗口通过两个公开 API 注册，对应 `FrontedWindowRegistration` 派生类型：`services.AddFrontedWindow<TWindow, TViewModel>()` 注册 XAML 窗口（`FrontedXamlWindowRegistration`，含 `WindowType`），`services.AddFrontedV3LayoutWindow("WindowId", isBuiltIn: false)` 注册 v3 layout 窗口（`FrontedV3LayoutWindowRegistration`，无额外字段）。`FrontedWindowRegistrationKind` 枚举只有 `Xaml` / `V3Layout`。Registry 接口提供 `GetWindows()`、`GetManageableWindows()`、`GetV3LayoutWindows()`、`TryGet()`。

标识模型（Canonical ID）：

| 名称 | 说明 |
| --- | --- |
| Canonical ID | 窗口身份；内置为 `BpWindow`，插件 v3 layout 为 `plugin:{PackageId}/{LocalWindowId}`，XAML 窗口为 Attribute ID（插件为 `plugin:{PackageId}/{AttributeId}`，宿主直接注册的为 `{AttributeId}`；推荐 GUID 但不强制） |
| `PackageId` | 插件 `manifest.yml` 的 `id`，由宿主自动注入 |

XAML 窗口（`FrontedWindowRegistrationKind.Xaml`）由插件提供 WPF `Window` 类型，出现在 FrontManage，不默认进入 Designer。v3 Layout 窗口（`FrontedWindowRegistrationKind.V3Layout`）使用宿主标准 `FrontedWindowBase` layout host；加载优先级为活动包 → 空模板，宿主不从插件安装目录加载默认 v3 Layout。该选择不由 `manifest.yml` 指定。Canvas/BaseCanvas 只是运行时实现细节，不出现在插件布局路径或 manifest 中。

示例（插件控件完整声明）：

```csharp
[FrontedV3Control("TeamCard")]
public partial class TeamCardControl : FrontedV3ControlBase
{
    public static readonly FrontedV3Property<string> TextColorProperty =
        new("Appearance.TextColor", FrontedV3Storage.ExtensionData("TextColor"));

    public static readonly FrontedV3Property<string> TeamNameProperty =
        new("Content.TeamName", FrontedV3Storage.ExtensionData("TeamName"));

    public TeamCardControl()
    {
        InitializeComponent();
    }

    protected override void OnInitializeFrontedV3(FrontedV3ControlContext context)
    {
        DataContext = context.Options;
    }
}
```

插件不再需要手写 typed Config 类。`ControlType` 由 `[FrontedV3Control]` 特性自动推导为 `plugin:{PackageId}/{ControlId}`，插件专属属性通过 `FrontedV3Property<T>` + `FrontedV3Storage.ExtensionData("key")` 声明，值存储到 `PluginFrontedControlConfig.ExtensionData` 字典并序列化为 JSON 根级字段（如 `TextColor`、`TeamName`）。

## 重启要求

插件安装或更新后需要重启，原因是插件向 DI 注入页面、窗口、服务发生在 Host build 前。当前进程的 DI 容器已经构建后，不能把新插件完整接入 WPF-UI 导航和前台窗口服务。

市场安装新插件时会移动到正式插件目录并标记 `IsRestartRequired`。更新已存在插件时会移动到 `.new`，等下次启动覆盖。

## 打包

`neo-bpsys-wpf.PluginSdk.targets` 提供 `CreateZip` target：

```powershell
dotnet publish -p:CreateZip=true
```

它会检查 publish 输出中是否存在 `manifest.yml`，然后计算依赖排除列表。默认 `PluginPackageExcludeDependencyClosure=true`，根为：

```text
neo-bpsys-wpf.PluginSdk;neo-bpsys-wpf.Core
```

这意味着由 SDK/Core 带入的宿主已有依赖会被排除，但插件自己直接引用的第三方包会被保留，避免误删插件真正需要的运行时文件。

`neo-bpsys-wpf.PluginSdk.targets` 随 SDK 源码一起引用，不再通过 NuGet `buildTransitive` 自动导入。插件项目必须显式写 `<Import ... />`，路径按实际仓库布局调整。

`.bpui v3` 布局包不得包含插件 DLL 或插件 zip。布局包只声明插件依赖；插件安装、更新、校验和重启提示必须走插件系统 / 插件市场流程。

## `.bpui` 依赖和安全边界

插件控件是可执行代码。导入 `.bpui` 布局包时，即使布局文件只包含 JSON，也可能引用插件控件；宿主必须把“安装插件”和“导入布局”分开处理：

1. `.bpui` 不能静默安装、更新或启用插件。
2. `.bpui` 不能携带插件二进制。
3. 插件市场或插件安装 UI 必须展示插件身份、版本、来源、权限信息（如果未来支持）、hash / signature 校验信息（如果支持）。
4. 用户确认后才能安装或更新插件。
5. 安装或更新插件后仍遵守当前加载模型，通常需要重启后插件控件才会变为可用。

这与现有全信任模型一致：插件不是沙箱，安装插件意味着信任该代码。布局导入器只能做依赖预检和安装引导，不能绕过插件生命周期，也不能静默安装插件。缺失插件窗口和缺失插件控件会被保留，用户可在 Designer 中手动删除 placeholder。

## 内置插件

主项目 csproj 中通过 `BuiltinPlugin` 构建并复制 `TeamJsonMaker` 到输出/发布目录的 `Plugins\top.plfjy.bpsys.TeamJsonMaker`。它和用户插件使用同一加载机制，只是来源路径不同。

`ExamplePlugin`（插件 ID `plfjy.ExamplePlugin`）是全功能参考插件，整合了原先的 `ExampleFrontedControls`，作为插件前台控件、插件前台窗口、Designer v3 集成和 `.bpui` 依赖管理的完整示例。该插件注册示例控件（如 `plugin:plfjy.ExamplePlugin/TeamCard`），主项目在 `Debug` 配置下把它加入 `BuiltinPlugin` 并复制到输出目录；Release、Beta、Preview 默认不包含该示例插件。该插件用于手工验证 Designer v3 插件全流程作者体验，不是发行功能。

`top.plfjy.bpsys.WebRenderer` 是所有配置都会复制的实验性内置插件。它携带独立 framework-dependent Web sidecar，不向 WPF 宿主引入 `Microsoft.AspNetCore.App` 依赖；详见 [web-renderer-experimental.md](web-renderer-experimental.md)。

Designer 保存和 `.bpui` 导出会在插件已安装 / 已加载时把 Canvas `RequiredPlugins.MinVersion` 和 manifest `PluginDependencies.MinVersion` 写成插件 `manifest.yml` 中的插件自身 `version`，例如 `1.0.0.0`。这不是 descriptor 的 `MinHostVersion`，也不是插件 API 版本。导入 `.bpui` 时如果已安装版本低于 `MinVersion`，会进入插件市场安装 / 更新引导；导入本身仍可成功并保留缺失插件内容。安装引导会在下载 / 安装队列结束后校验所有待处理插件都已安装或暂存；失败项会显示插件 ID 和错误信息，未完成项不会被当作成功。

`.bpui` 只传输布局、资源和依赖元数据，不传输插件 DLL、安装包或脚本。插件安装仍必须走现有插件系统 / 插件市场流程，并在需要时通过重启让 Host build 前的 DI 注入生效。

## 加载失败检查清单

1. 插件目录是否位于用户插件路径或内置插件路径。
2. 是否存在 `manifest.yml`，字段名是否符合 camelCase。
3. `entranceAssembly` 是否指向真实 DLL。
4. DLL 中是否有直接继承 `PluginBase` 的导出类型。
5. `apiVersion` 是否可解析且通过宿主兼容性检查。
6. 插件 ID 是否和已加载插件重复。
7. 插件是否被禁用或标记卸载。
8. 插件直接依赖是否随包发布，或是否被 `CreateZip` 排除策略误判。
9. 前台窗口/后台页面 ID 是否与宿主或其他插件重复。
10. 安装或更新后是否已经重启宿主。
