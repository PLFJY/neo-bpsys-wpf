# 设置、路径与用户数据

共享比赛状态见 [shared-data-and-state.md](shared-data-and-state.md)。资源、字体、本地化和默认布局文件见 [resources-localization-and-assets.md](resources-localization-and-assets.md)。

## AppConstants 路径

| 常量 | 路径 |
| --- | --- |
| `AppDataPath` | `%APPDATA%\neo-bpsys-wpf` |
| `AppOutputPath` | `%USERPROFILE%\Documents\neo-bpsys-wpf` |
| `ConfigFilePath` | `%APPDATA%\neo-bpsys-wpf\Config.json` |
| `AppTempPath` | `%TEMP%\neo-bpsys-wpf` |
| `CustomUiPath` | `%APPDATA%\neo-bpsys-wpf\CustomUi` |
| `FrontedLayoutsPath` | `%APPDATA%\neo-bpsys-wpf\FrontedLayouts` |
| `FrontedLayoutPackagesPath` | `%APPDATA%\neo-bpsys-wpf\FrontedLayoutPackages` |
| `FrontedLayoutLocalPackagePath` | `%APPDATA%\neo-bpsys-wpf\FrontedLayoutPackages\local` |
| `FrontedLayoutLocalImagesPath` | `%APPDATA%\neo-bpsys-wpf\FrontedLayoutPackages\local\resources\images` |
| `LogPath` | `%APPDATA%\neo-bpsys-wpf\Log` |
| `ResourcesPath` | `{AppBaseDirectory}\Resources` |
| `PluginPath` | `%APPDATA%\neo-bpsys-wpf\Plugins` |
| `BuiltInPluginPath` | `{AppBaseDirectory}\Plugins` |
| `PluginConfigsPath` | `%APPDATA%\neo-bpsys-wpf\PluginConfigs` |

用户可修改或运行时生成的状态主要在 AppData、Documents 输出目录和 Temp 中。安装目录下的 `Resources` 和内置插件通常来自构建/发布产物。

## Settings 顶层字段

| 字段 | 说明 |
| --- | --- |
| `Version` | 主设置配置版本；当前为 `3`，缺失或 `null` 表示 legacy 配置 |
| `ShowAfterUpdateTip` | 更新后提示是否显示 |
| `IsRecordGlobalBan` | 是否记录全局 Ban |
| `OcrModelKey` | 当前 OCR 模型键 |
| `GhProxyMirror` | GitHub 镜像前缀 |
| `PluginMarketSource` | 插件市场索引源 |
| `IsFindPreRelease` | 是否查找预发布版本，Beta 构建默认 true |
| `LogLevel` | Serilog 动态日志级别 |
| `Language` | `System` 或具体语言枚举 |
| `CultureInfo` | 由 `Language` 推导，JSON 忽略 |

窗口设置包括 `BpWindowSettings`、`CutSceneWindowSettings`、`ScoreWindowSettings`、`GameDataWindowSettings`、`WidgetsWindowSettings`。

启动加载 `Config.json` 时会先检查 raw JSON root：`Version` 缺失或为 `null` 时按 legacy 配置处理，先备份为 `Config.json.v2.backup` 或带时间戳的同类文件，再写回 `Version = 3`。这个 Phase 1 迁移只更新主设置版本并保留现有字段，不迁移前台布局文件，也不删除旧前台窗口设置。

## 前台窗口设置

各窗口设置通常包含：

1. `WindowSize`。
2. 背景图、锁图、边框图等 URI。
3. 透明背景开关或背景色。
4. `TextSettings`，包含颜色、字体、字号、字重。

图片加载通过 `ImageHelper.GetUiImageFromSetting(settingUri, fallbackKey)`。设置文件中保存路径时，`SettingsHostService.SaveConfigAsync()` 会把 AppData 实际路径替换为 `%APPDATA%`。

## 文本设置

`TextSettings` 保存：

| 字段 | 说明 |
| --- | --- |
| `Color` | 颜色字符串 |
| `FontFamilySite` | 字体路径或字体名 |
| `FontWeight` | 通过自定义 JSON converter 序列化 |
| `FontSize` | 字号 |

`Foreground` 和 `FontFamily` 是运行时属性，不直接写入 JSON。

## 其他用户数据

| 数据 | 路径 |
| --- | --- |
| 前台布局 | `%APPDATA%\neo-bpsys-wpf\*Config-*.json` |
| v3 前台布局 | `%APPDATA%\neo-bpsys-wpf\FrontedLayouts\{WindowTypeName}\{CanvasName}.json` |
| v3 内置默认布局 | `{AppBaseDirectory}\Resources\FrontedLayouts\{WindowTypeName}\{CanvasName}.json` |
| v3 布局包根目录 | `%APPDATA%\neo-bpsys-wpf\FrontedLayoutPackages\` |
| v3 editor-local 资源 | `%APPDATA%\neo-bpsys-wpf\FrontedLayoutPackages\local\` |
| v3 已安装布局包 | `%APPDATA%\neo-bpsys-wpf\FrontedLayoutPackages\{PackageId}\` |
| v3 活动包状态 | `%APPDATA%\neo-bpsys-wpf\FrontedLayoutPackages\active-package.json` |
| SmartBP 区域 | `%APPDATA%\neo-bpsys-wpf\SmartBp\GameDataRegions.json` |
| OCR 模型 | `Documents\neo-bpsys-wpf\OCRModels` |
| 插件配置 | `%APPDATA%\neo-bpsys-wpf\PluginConfigs\{pluginId}` |
| 插件市场临时下载 | `%TEMP%\neo-bpsys-wpf\PluginMarket\...` |

v3 前台布局的加载优先级是用户布局优先、内置默认布局兜底。`IFrontedUserLayoutStore` 负责 `%APPDATA%\neo-bpsys-wpf\FrontedLayouts` 下的用户布局读写；`IFrontedLayoutService` 会先尝试用户布局，如果用户 JSON 不可读或无效，会记录警告并回退到内置 `Resources\FrontedLayouts`。独立 Fronted Designer 编辑器保存普通用户改动时只写入 `%APPDATA%\neo-bpsys-wpf\FrontedLayouts\{WindowTypeName}\{CanvasName}.json`，不应直接覆盖安装目录或源码中的 `Resources\FrontedLayouts`。多 Canvas 窗口按 `{CanvasName}.json` 分文件保存；“重置为内置”会删除当前 Canvas 对应的用户布局文件。

Phase 9B.0 起，窗口级选项保存到 `%APPDATA%\neo-bpsys-wpf\FrontedLayouts\{WindowTypeName}\window.json`，当前包含 `AllowTransparency`。该文件是窗口级，不属于任意单个 Canvas；重置某个 Canvas 布局不会删除 `window.json`。

Designer v3 `.bpui` 包路径标准见 [bpui-package-v3.md](bpui-package-v3.md)。已安装包资源应放在各自包目录内，例如 `%APPDATA%\neo-bpsys-wpf\FrontedLayoutPackages\{PackageId}\resources\`，不要合并到共享资源目录。若旧讨论或临时代码提到 `%APPDATA%\neo-bpsys-wpf\FrontedLayoutResources\`，应视为已被包隔离方案取代，不作为新实现的首选路径。

`builtin` 是虚拟包 ID，映射到应用内置 `Resources\FrontedLayouts`，不在 `FrontedLayoutPackages` 下作为普通包安装，也不能删除。`local` 是编辑器本地资源命名空间，推荐路径为 `%APPDATA%\neo-bpsys-wpf\FrontedLayoutPackages\local\resources\`，用于保存用户选择本地图片后的副本；普通包删除不能删除 `local`。

Phase 9D 起，`IFrontedLayoutPackageManager` 会读取 `%APPDATA%\neo-bpsys-wpf\FrontedLayoutPackages`，始终列出虚拟 `builtin` 包，跳过保留的 `local` 目录，并读取普通已安装包目录下的 `manifest.json`。缺少或损坏 manifest 的包会以校验错误显示，不会让管理器崩溃。`active-package.json` 缺失时默认视为 `builtin` 活动；激活普通包会把包内布局复制到 `%APPDATA%\neo-bpsys-wpf\FrontedLayouts` 并写入 active state，激活 `builtin` 会删除 active state 并清空 `FrontedLayouts` 以回退到内置布局。删除活动包会先切回 `builtin` 再删除包目录。

Phase 9D 起，`FrontManagePage` 的 Layout Packages 页支持导出和导入 v3 `.bpui` 包。导出会从 `IFrontedLayoutService` 按“用户布局优先、内置兜底”加载全部已迁移前台布局，生成 `manifest.json`、`layouts/` 和 `resources/`，但不会包含全局 `%APPDATA%\neo-bpsys-wpf\Config.json`，也不会包含 legacy `CustomUi/` 或 `FrontElementsConfig/`。导入会先解压到 staging 目录并完成校验，再安装到 `FrontedLayoutPackages/{PackageId}`；替换已有包时，旧包只会在新包校验成功后删除。

卸载脚本会询问是否删除 `%APPDATA%\neo-bpsys-wpf`，包括日志、自定义 UI 和设置。
