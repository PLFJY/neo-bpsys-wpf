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

插件 API 版本和 PluginSdk NuGet 包版本是两个概念：

| 名称 | 用途 |
| --- | --- |
| 插件 API 版本 | `manifest.yml` 的 `apiVersion`，用于宿主兼容性检查 |
| PluginSdk NuGet 包版本 | 插件项目引用的 SDK 包版本，用于编译期 API 和打包目标 |

不要把二者不一致当成版本错误。

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
| 插件前台窗口 | `services.AddFrontedWindowPluginContributor<TContributor>()` |
| Designer v3 插件控件 | `services.AddFrontedPluginControlContributor<TContributor>()` |
| 自定义服务 | 常规 `services.AddSingleton/AddTransient/...` |
| 配置文件 | `PluginBase.PluginConfigFolder` + `ConfigureFileHelper` |
| 共享数据访问 | 注入 `ISharedDataService` |

插件只在启动时加载。当前代码没有热加载机制，也不要假设复制文件到插件目录后当前进程会立刻发现新页面或窗口。

`Assembly.LoadFrom` 使用入口程序集路径加载插件。依赖解析依赖 .NET 默认加载上下文、插件输出目录和宿主已有程序集；插件包漏掉自身直接依赖时，常见表现是入口程序集加载失败或 `Initialize` 中类型解析失败。

## Designer v3 插件前台控件规划

起，插件前台系统围绕 Designer v3 / FrontedLayout v3 工作。旧的前台控件注入 API 已移除；插件前台能力分为 Designer v3 插件控件、Plugin XAML Window 和 Plugin v3 Layout Window。`.bpui` 导入遇到缺失插件窗口或插件控件时会保留 layout、资源和依赖元数据，不再物理删除缺失插件控件。

插件控件的 `ControlType` 必须使用命名空间：

```text
plugin:<PackageId>/<ControlTypeName>
```

示例：

```text
plugin:plfjy.ExamplePlugin/TeamCard
```

`PackageId` 必须匹配插件 `manifest.yml` 的 `id`，`ControlTypeName` 在插件内唯一。完整 `ControlType` 是稳定序列化 schema，不本地化，不使用显示名，也不能 shadow 内置控件类型。`.bpui v3` 中的 Canvas `RequiredPlugins` 和 manifest `PluginDependencies` 规则见 [bpui-package-v3.md](bpui-package-v3.md)。

入口如下：

```csharp
public interface IFrontedControlPluginContributor
{
    void RegisterFrontedControls(IFrontedControlPluginRegistry registry);
}

public interface IFrontedControlPluginRegistry
{
    void Register<TConfig>(FrontedPluginControlDescriptor<TConfig> descriptor)
        where TConfig : FrontedControlConfigBase;
}

public sealed class FrontedPluginControlDescriptor<TConfig>
    where TConfig : FrontedControlConfigBase
{
    public required string PackageId { get; init; }
    public required string ControlTypeName { get; init; }
    public string FullControlType => $"plugin:{PackageId}/{ControlTypeName}";
    public required Type ConfigType { get; init; }
    public required Func<string, TConfig, FrontedControlBuildContext, FrameworkElement> CreateControl { get; init; }
    public Func<TConfig>? CreateDefaultConfig { get; init; }
    public IReadOnlyList<FrontedPluginPropertyDescriptor>? Properties { get; init; }
    public Func<TConfig, IEnumerable<FrontedLayoutValidationMessage>>? Validate { get; init; }
    public string? DisplayNameKey { get; init; }
    public string? DescriptionKey { get; init; }
    public string? Icon { get; init; }
    public Version? MinHostVersion { get; init; }
    public int ConfigSchemaVersion { get; init; } = 1;
}
```

属性元数据采用声明式描述，而不是允许插件提供任意 PropertyGrid WPF 控件：

```csharp
public sealed class FrontedPluginPropertyDescriptor
{
    public required string PropertyName { get; init; }
    public string? DisplayNameKey { get; init; }
    public string? DescriptionKey { get; init; }
    public string GroupName { get; init; } = "Plugin";
    public FrontedPropertyEditorKind? EditorKind { get; init; }
    public IReadOnlyList<FrontedPropertyEditorOption>? Options { get; init; }
    public FrontedBindingTargetKind BindingTargetKind { get; init; } = FrontedBindingTargetKind.Any;
    public bool IsVisible { get; init; } = true;
    public bool IsReadOnly { get; init; }
}
```

这样可以保持编辑器 UI 一致、继续使用 Designer i18n、集中验证属性值，并减少插件直接注入任意编辑器控件带来的维护和安全风险。默认 fallback 可以反射公开 config 属性，但插件应优先提供明确属性元数据。要出现在 Designer Add Control 中，插件控件应提供 `CreateDefaultConfig`；如果没有提供，宿主只会在 config 类型有安全公共无参构造函数且能写入完整 `ControlType` 时才允许添加。

插件控件 config 建议：

1. 继承 `FrontedControlConfigBase`。
2. 构造函数设置完整插件 `ControlType`。
3. 插件专属属性必须能被 `System.Text.Json` 序列化。
4. 布局 JSON 不保存可执行状态。
5. 避免保存绝对本地路径；图片等资源优先使用 `.bpui` 支持的资源 URI。
6. `BindingPath` 保存原始不变量路径，不本地化。
7. 不要使用泛名 `IsActive` 表示控件业务状态、可见性或启用状态；`IsActive` 只保留给内部框架/运行时激活语义，尤其是 CommunityToolkit.Mvvm `ObservableRecipient.IsActive`。请使用 `IsVisible`、`IsEnabled`、`IsSelected`、`IsExpanded` 或更具体的名称。

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

运行时读取布局时，`plugin:*` 控件即使插件未安装也会反序列化为 `PluginFrontedControlConfig`，并通过 `JsonExtensionData` 保留插件专属属性，确保可读取、保存和 roundtrip。插件 descriptor 可用时，宿主 adapter 会把通用 config 序列化后再反序列化为 descriptor 声明的 typed config，然后调用 `CreateControl`。如果插件缺失，Designer 显示 Missing Plugin placeholder，前台 renderer 跳过该控件并记录 warning；未知非插件 `ControlType` 仍按无效内置控件处理并报错。

## 插件前台窗口 v3

插件窗口通过 `IFrontedWindowPluginContributor.GetFrontedWindows()` 返回 `FrontedPluginWindowDescriptor`。`FrontedWindowType` enum 只表示内置窗口；插件窗口不扩展该 enum。

标识模型：

| 名称 | 说明 |
| --- | --- |
| `WindowId` | 运行时窗口身份，稳定 GUID/string |
| `WindowTypeName` | 插件内短语义窗口类型名 |
| `FullWindowType` | 布局 / `.bpui` 身份；内置为 `BpWindow`，插件为 `plugin:{PackageId}/{WindowTypeName}` |
| `PackageId` | 插件 `manifest.yml` 的 `id` |

Plugin XAML Window 由插件提供 WPF `Window` 类型，出现在 FrontManage，不默认进入 Designer。Plugin v3 Layout Window 由宿主标准 `FrontedWindowBase` layout host 渲染，默认布局来自 `Plugins/{PackageId}/FrontedLayouts/{WindowTypeName}.json`；`Customizable=true` 的 v3 layout window 会进入 Designer。Canvas/BaseCanvas 只是运行时实现细节，不出现在插件默认布局路径或 manifest 中。

示例：

```csharp
public sealed class TeamCardFrontedControlConfig : FrontedControlConfigBase
{
    public TeamCardFrontedControlConfig()
    {
        ControlType = "plugin:plfjy.ExamplePlugin/TeamCard";
        Width = 260;
        Height = 96;
    }

    public string? TeamNameBindingPath { get; set; } = "CurrentGame.SurTeam.Name";
    public string? LogoBindingPath { get; set; } = "CurrentGame.SurTeam.Logo";
    public string BackgroundColor { get; set; } = "#AA000000";
    public string ForegroundColor { get; set; } = "#FFFFFFFF";
    public double CornerRadius { get; set; } = 12;
    public double LogoSize { get; set; } = 64;
    public double FontSize { get; set; } = 24;
    public string FontWeight { get; set; } = "Bold";
}
```

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
