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
