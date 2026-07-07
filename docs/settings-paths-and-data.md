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
| `FrontedLayoutLocalFontsPath` | `%APPDATA%\neo-bpsys-wpf\FrontedLayoutPackages\local\resources\fonts` |
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

active `Settings.cs` 不再包含旧前台窗口设置。旧 `BpWindowSettings`、`CutSceneWindowSettings`、`ScoreWindowSettings`、`GameDataWindowSettings`、`WidgetsWindowSettings` 只由 legacy DTO 在迁移 / `.bpui` 转换流程读取。

启动加载 `Config.json` 时会先检查 raw JSON root：`Version` 缺失或为 `null` 时按 legacy 配置处理，先备份为 `Config.json.v2.backup` 或带时间戳的同类文件。启动迁移会把旧前台窗口设置转换为 `FrontedLayoutPackages/converted-v2-{hash}/` 普通 Designer v3 包，激活该包，然后写回干净的 `Version = 3` Settings。迁移后的主设置不再包含旧前台窗口字段。

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

`Foreground` 和 `FontFamily` 是 legacy 兼容属性，不直接写入 JSON。

## 其他用户数据

| 数据 | 路径 | 说明 |
| --- | --- | --- |
| v3 用户布局 | `%APPDATA%\neo-bpsys-wpf\FrontedLayouts\{WindowTypeName}.json` | 用户自定义布局 |
| v3 内置默认布局 | `{AppBaseDirectory}\Resources\FrontedLayouts\{WindowTypeName}.json` | 应用内置布局（只读） |
| v3 布局包根目录 | `%APPDATA%\neo-bpsys-wpf\FrontedLayoutPackages\` |
| v3 editor-local 资源 | `%APPDATA%\neo-bpsys-wpf\FrontedLayoutPackages\local\` |
| v3 已安装布局包 | `%APPDATA%\neo-bpsys-wpf\FrontedLayoutPackages\{PackageId}\` |
| v3 活动包状态 | `%APPDATA%\neo-bpsys-wpf\FrontedLayoutPackages\active-package.json` |
| SmartBP 区域 | `%APPDATA%\neo-bpsys-wpf\SmartBp\GameDataRegions.json` |
| SmartBP 模块状态 | `%APPDATA%\neo-bpsys-wpf\SmartBpModuleState.json` |
| 教程与导览状态 | `%APPDATA%\neo-bpsys-wpf\TutorialState.json` |
| SmartBP 模块目录迁移标记 | `%APPDATA%\neo-bpsys-wpf\SmartBpModuleMovePending.json` |
| SmartBP 模块卸载路径记录 | `HKCU\Software\neo-bpsys-wpf\SmartBpModule\ModuleRoot` |
| SmartBP 模块默认安装目录 | `%LOCALAPPDATA%\neo-bpsys-wpf\Components\SmartBpModule` |
| OCR 模型 | `{SmartBpModuleRoot}\OCRModels` |
| 插件配置 | `%APPDATA%\neo-bpsys-wpf\PluginConfigs\{pluginId}` |
| 插件市场临时下载 | `%TEMP%\neo-bpsys-wpf\PluginMarket\...` |

SmartBP 模块加载/安装目录可在设置页修改。若当前已有可用模块目录，保存时会先复制旧目录到新目录的 staging，验证复制结果后移动到目标目录，再写入 `SmartBpModuleMovePending.json` 标记和 `SmartBpModuleState.json` 的目标 `ModuleRoot`，同时把目标路径写入 `HKCU\Software\neo-bpsys-wpf\SmartBpModule\ModuleRoot` 供卸载器清理。下一次从目标目录成功加载模块并写回状态后，会尝试删除旧目录；如果删除失败，迁移标记保留并记录 cleanup 错误，后续成功加载目标目录时继续清理。路径校验沿用模块安装安全规则，拒绝系统目录、驱动器根目录、不可写目录、源目录父子路径，以及包含非 SmartBP 内容的目标目录。

`TutorialState.json` 由 `neo-bpsys-wpf.ProductTour` 的状态存储读写，记录 `CompletedFlows` 和 `CompletedPackages`。首次总导览完成时，flow 的 `IncludedPackageIds` 会以 `CoveredByFlow` 写入 package 状态；用户跳过 flow 时只记录 flow 的 `Skipped`，不覆盖 package。设置页的“重新启动首次导览”和“重置全部教程状态”会修改该文件对应状态，危险操作必须二次确认。

Debug 构建下修改 SmartBP 模块目录不复制模块文件，也不写迁移标记；目标目录只要通过开发模块目录校验，就直接以 `InstallKind = DevelopmentDirectory` 写入状态和注册表，并清理旧的迁移标记。

旧版本 OCR 模型目录 `Documents\neo-bpsys-wpf\OCRModels` 只作为迁移来源。SmartBP 模块首次安装或首次成功加载后会把已就绪模型复制到模块根下的 `OCRModels`，验证成功后再删除对应旧模型目录。

> **注意**：旧的 `{WindowName}Config-{CanvasName}.json` 位置保存/恢复功能已删除。这些 legacy 文件不再被运行时读取，只用于 legacy `.bpui` 转换流程。

v3 前台布局当前以 Window-centric “布局方案”读写。`builtin` 是唯一只读方案，读取应用内置 `Resources\FrontedLayouts`；普通已安装包同时也是可编辑布局方案，读写路径为 `%APPDATA%\neo-bpsys-wpf\FrontedLayoutPackages\{PackageId}\FrontedLayouts\{WindowTypeName}.json`。独立 Fronted Designer 在普通方案活动时直接保存到该包的 `FrontedLayouts/`；如果当前活动方案是 `builtin`，第一次保存会自动复制内置布局为本地可编辑 `用户布局方案 {i}` / `User Layout Scheme {i}`，激活该方案后再写入。Canvas 不再是管理单位。

旧版 `%APPDATA%\neo-bpsys-wpf\FrontedLayouts` 不再是包方案编辑的正常写入目标，也不会在切换包时被清空。为兼容旧数据，`IFrontedLayoutService` 在没有包管理器或需要读取旧工作副本时仍可把它作为 fallback；新的保存路径应迁移到活动可编辑包。

窗口级选项已迁移到 `FrontedWindowConfig.WindowSettings`，包括窗口尺寸、位置、透明、背景色、Topmost 和 `ViewboxStretch`。旧 `window.json` 只属于 legacy/临时迁移输入，不是新运行时主路径。

Designer v3 `.bpui` 包路径标准见 [bpui-package-v3.md](bpui-package-v3.md)。已安装包资源应放在各自包目录内，例如 `%APPDATA%\neo-bpsys-wpf\FrontedLayoutPackages\{PackageId}\resources\`，不要合并到共享资源目录。若旧讨论或临时代码提到 `%APPDATA%\neo-bpsys-wpf\FrontedLayoutResources\`，应视为已被包隔离方案取代，不作为新实现的首选路径。

`builtin` 是虚拟包 ID，映射到应用内置 `Resources\FrontedLayouts`，不在 `FrontedLayoutPackages` 下作为普通包安装，也不能删除。`local` 是编辑器本地资源命名空间，推荐路径为 `%APPDATA%\neo-bpsys-wpf\FrontedLayoutPackages\local\resources\`，用于保存用户选择本地图片后的副本；普通包删除不能删除 `local`。

Designer v3 字体属性支持把 `.ttf`、`.otf`、`.ttc` 导入当前活动布局包。导入时若当前活动包是 `builtin`，会先复制为可编辑用户布局方案，再把字体文件写入该包的 `resources/fonts/`。布局 JSON 保存 `bpui://{PackageId}/resources/fonts/{file}#FontFamilyName`，因此包内字体只随该布局包生效；切换到其他布局包后不会出现在字体列表中，需要在目标包重新导入。字体属性旁的管理入口可以删除当前包中未被布局 JSON 引用的字体文件；仍被引用的字体不能直接删除。

`IFrontedLayoutPackageManager` 会读取 `%APPDATA%\neo-bpsys-wpf\FrontedLayoutPackages`，始终列出虚拟 `builtin` 包，跳过保留的 `local` 目录，并读取普通已安装包目录下的 `manifest.json`。缺少或损坏 manifest 的包会以校验错误显示，不会让管理器崩溃。`active-package.json` 缺失时默认视为 `builtin` 活动；激活普通包只写入 active state，不会复制布局到全局 `FrontedLayouts`，激活 `builtin` 只切换活动状态，不删除任意可编辑包或 legacy 用户布局。删除活动包会先切回 `builtin` 再删除包目录。

`FrontManagePage` 的 Layout Packages 页支持导出和导入 v3 `.bpui` 包。导出会从 `IFrontedLayoutService` 按“用户布局优先、内置兜底”加载全部可管理前台布局，生成 `manifest.json`、`FrontedLayouts/`、`FrontedBehaviors/` 和 `resources/`，但不会包含全局 `%APPDATA%\neo-bpsys-wpf\Config.json`，也不会包含 legacy `CustomUi/` 或 `FrontElementsConfig/`。导入会先解压到 staging 目录并完成校验，再安装到 `FrontedLayoutPackages/{PackageId}`；替换已有包时，旧包只会在新包校验成功后删除。

legacy `.bpui` 导入不会再调用 SettingPage 的旧导入覆盖流程。`Config.json` 只作为转换输入读取明确前台图片字段，不会复制到 AppData，不会覆盖当前设置，也不会要求为了 layout-only 转换重启。转换输出的 v3 包仍安装到 `%APPDATA%\neo-bpsys-wpf\FrontedLayoutPackages\{PackageId}`，激活后按包内 `FrontedLayouts/` 作为读写方案，不再复制到全局 `FrontedLayouts`。

用户布局、窗口选项和布局包读取会在反序列化前检查文件大小，并使用 JSON 最大深度 32：layout JSON 最大 2 MiB，`window.json` 最大 64 KiB，manifest 最大 256 KiB，legacy `Config.json` 读取路径最大 2 MiB。布局包 zip 还限制压缩包 50 MiB、解压总量 100 MiB、单 entry 10 MiB、最多 1000 entries，并继续保留 zip-slip 检查。超过限制的外部文件会拒绝读取或导入，不会截断。

卸载脚本总是尝试删除 SmartBP 模块目录，路径来源依次为注册表 `ModuleRoot`、`SmartBpModuleState.json` 和默认模块目录；随后再询问是否删除 `%APPDATA%\neo-bpsys-wpf`，包括日志、自定义 UI 和设置。卸载器会拒绝删除磁盘根、系统目录、安装目录和整个 AppData 配置目录。
