# 插件宿主系统

本文面向宿主维护者，负责发现、加载、DI、目录、市场和信任边界。插件作者的 manifest、公开构造函数兼容、V3 控件/Part/Options、窗口与事件示例以 [Plugin SDK](plugin-development.md) 为准；不在此复制第二套开发教程。

源码入口：[PluginService](../../neo-bpsys-wpf/Services/PluginService.cs)、[PluginBase](../../neo-bpsys-wpf.Core/Abstractions/PluginBase.cs)、[注册扩展](../../neo-bpsys-wpf.Core/Extensions/Registry)、[SDK 打包目标](../../neo-bpsys-wpf.PluginSdk/neo-bpsys-wpf.PluginSdk.targets)。

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

## 版本与 manifest

manifest.yml 的 apiVersion 用于宿主兼容检查；version 是插件自身版本，亦用于包依赖最低版本和市场更新。SDK 源码引用提交提供编译 API/targets，不等于 apiVersion。v3 使用 ProjectReference + 显式 Import，不再发布 SDK NuGet 包。完整 schema 和作者接入见 [SDK](plugin-development.md)。

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

## 前台扩展与宿主边界

窗口身份和 registration 模型以[窗口运行时](../frontend/frontend-windows-and-layout.md)为准。插件在 Initialize 作用域注册 V3 控件、XAML/v3 窗口及语义事件；PackageId 自动绑定 manifest.id，不由插件伪造来源。固定 Part、PartCollection 与 Options 是 [SDK](plugin-development.md) 的公开模型，宿主应复用 schema/storage 和统一 Host，不重新引入 contributor/descriptor 注入方案。

缺插件的 layout/behavior 配置、ExtensionData、依赖和资源保留，待安装重启恢复；runtime 不创建误导观众的占位，Designer 才显示可编辑的缺插件占位。依赖扫描与导入规则见 [BPUI](../frontend/bpui-package-v3.md)，事件消费规则见 [Behavior](../frontend/fronted-behavior-system.md)。

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

主项目通过 BuiltinPlugin/MSBuild 复制内置插件，均走同一 PluginService 加载路径。[TeamJsonMaker](../../Built-inPlugins/neo-bpsys-wpf.TeamJsonMaker/README.md)、[GameStopwatch](../../Built-inPlugins/neo-bpsys-wpf.GameStopwatch/README.md)、[FocusKeeper](../../Built-inPlugins/neo-bpsys-wpf.FocusKeeper/README.md) 分别保留数据、计时和原生 Hook 说明；WebRenderer 的独立 sidecar 与构建入口见[架构](web-renderer-experimental.md)。

ExamplePlugin（plfjy.ExamplePlugin）是综合 SDK 示例，Debug 才加入输出，Release/Beta/Preview 默认不包含；不是发行功能。当前插件集合与条件以[主项目](../../neo-bpsys-wpf/neo-bpsys-wpf.csproj)为准。

Designer 保存和包导出时，依赖 MinVersion 使用已加载插件 manifest 的自身 version，不是宿主 API 版本；精确合并、缺失/过低版本及安装队列恢复规则以 [BPUI](../frontend/bpui-package-v3.md)为准。


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
