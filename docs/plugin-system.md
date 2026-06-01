# 插件系统

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
10. 扫描带 `FrontedWindowInfo` 的窗口类型，用于之后恢复插件默认布局。
11. 创建入口实例，设置 `Info` 和 `PluginConfigFolder`。
12. 调用 `Initialize(context, services)`。
13. 把插件实例注册为 singleton。

插件的 `Initialize` 可以注册：

| 能力 | API |
| --- | --- |
| 后台页面 | `services.AddBackendPage<TPage,TViewModel>()` |
| 前台窗口 | `services.AddFrontedWindow<TWindow,TViewModel>()` |
| 注入控件 | `FrontedWindowHelper.InjectControlToFrontedWindow(...)` |
| 自定义服务 | 常规 `services.AddSingleton/AddTransient/...` |
| 配置文件 | `PluginBase.PluginConfigFolder` + `ConfigureFileHelper` |
| 共享数据访问 | 注入 `ISharedDataService` |

插件只在启动时加载。当前代码没有热加载机制，也不要假设复制文件到插件目录后当前进程会立刻发现新页面或窗口。

`Assembly.LoadFrom` 使用入口程序集路径加载插件。依赖解析依赖 .NET 默认加载上下文、插件输出目录和宿主已有程序集；插件包漏掉自身直接依赖时，常见表现是入口程序集加载失败或 `Initialize` 中类型解析失败。

## Designer v3 插件前台控件规划

Phase 13A 只定义文档和 schema，不实现完整插件控件运行时。后续 Phase 13B 起，插件可以贡献 Designer v3 前台控件，让这些控件像内置 `Text`、`Image`、`BorderedImage` 一样被 v3 renderer 和独立编辑器识别；控件运行时行为、config 类型、默认 config 和属性元数据由插件提供。

插件控件的 `ControlType` 必须使用命名空间：

```text
plugin:<PackageId>/<ControlTypeName>
```

示例：

```text
plugin:top.plfjy.example.fronted/TeamCard
```

`PackageId` 必须匹配插件 `manifest.yml` 的 `id`，`ControlTypeName` 在插件内唯一。完整 `ControlType` 是稳定序列化 schema，不本地化，不使用显示名，也不能 shadow 内置控件类型。`.bpui v3` 中的 Canvas `RequiredPlugins` 和 manifest `PluginDependencies` 规则见 [bpui-package-v3.md](bpui-package-v3.md)。

建议的 Phase 13B API 形状如下，具体命名和命名空间以后续实现为准：

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

属性元数据建议先采用声明式描述，而不是允许插件提供任意 PropertyGrid WPF 控件：

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

这样可以保持编辑器 UI 一致、继续使用 Designer i18n、集中验证属性值，并减少插件直接注入任意编辑器控件带来的维护和安全风险。默认 fallback 可以反射公开 config 属性，但插件应优先提供明确属性元数据。

插件控件 config 建议：

1. 继承 `FrontedControlConfigBase`。
2. 构造函数设置完整插件 `ControlType`。
3. 插件专属属性必须能被 `System.Text.Json` 序列化。
4. 布局 JSON 不保存可执行状态。
5. 避免保存绝对本地路径；图片等资源优先使用 `.bpui` 支持的资源 URI。
6. `BindingPath` 保存原始不变量路径，不本地化。

示例：

```csharp
public sealed class TeamCardFrontedControlConfig : FrontedControlConfigBase
{
    public TeamCardFrontedControlConfig()
    {
        ControlType = "plugin:top.plfjy.example.fronted/TeamCard";
        Width = 260;
        Height = 96;
    }

    public string? TeamNameBindingPath { get; set; } = "CurrentGame.HomeTeam.Name";
    public string? LogoBindingPath { get; set; } = "CurrentGame.HomeTeam.Logo";
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

这与现有全信任模型一致：插件不是沙箱，安装插件意味着信任该代码。布局导入器只能做依赖预检、安装引导、取消导入或强制导入并删除缺失插件控件，不能绕过插件生命周期。

## 内置插件

主项目 csproj 中通过 `BuiltinPlugin` 构建并复制 `TeamJsonMaker` 到输出/发布目录的 `Plugins\top.plfjy.bpsys.TeamJsonMaker`。它和用户插件使用同一加载机制，只是来源路径不同。

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
