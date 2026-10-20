# 设置、路径与用户数据

共享比赛状态见 [shared-data-and-state.md](../architecture/shared-data-and-state.md)。资源、字体、本地化和默认布局文件见 [resources-localization-and-assets.md](resources-localization-and-assets.md)。

源码入口：[AppConstants](../../neo-bpsys-wpf.Core/AppConstants.cs)、[Settings](../../neo-bpsys-wpf.Core/Models/Settings.cs)、[SettingsHostService](../../neo-bpsys-wpf/Services/SettingsHostService.cs)。

## AppConstants 路径

| 常量 | 路径 |
| --- | --- |
| `AppDataPath` | `%APPDATA%\neo-bpsys-wpf` |
| `AppOutputPath` | `%USERPROFILE%\Documents\neo-bpsys-wpf` |
| `ConfigFilePath` | `%APPDATA%\neo-bpsys-wpf\Config.json` |
| `AppTempPath` | `%TEMP%\neo-bpsys-wpf` |
| `FrontedLayoutsPath` | `%APPDATA%\neo-bpsys-wpf\FrontedLayouts`（保留路径常量，普通 runtime 不读写） |
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
| `LogLevel` | 正式构建的 `FileLoggerProvider` 动态日志级别，默认 `Warning`；Debug 与 Preview 构建固定为 `Information`，设置页不可修改 |
| `Language` | `System` 或具体语言枚举 |
| `CultureInfo` | 由 `Language` 推导，JSON 忽略 |

active `Settings.cs` 不再包含旧前台窗口设置。旧 `BpWindowSettings`、`CutSceneWindowSettings`、`ScoreWindowSettings`、`GameDataWindowSettings`、`WidgetsWindowSettings` 只由 legacy DTO 在迁移 / `.bpui` 转换流程读取。

启动加载 `Config.json` 时会先检查 raw JSON root：`Version` 缺失或为 `null` 时按 legacy 配置处理，先备份为 `Config.json.v2.backup` 或带时间戳的同类文件。启动迁移会把旧前台窗口设置转换为 `FrontedLayoutPackages/converted-v2-{hash}/` 普通 Designer v3 包，激活该包，然后写回干净的 `Version = 3` Settings。迁移后的主设置不再包含旧前台窗口字段。

## 前台配置归属

活动 Settings 不再保存各前台窗口的图片、TextSettings、尺寸和透明度。它们属于 FrontedWindowConfig.WindowSettings / CanvasSettings / ControlLayout；旧 BpWindowSettings 等 DTO 仅供显式迁移。旧 TextSettings 的颜色、字体、字重和序列化泄漏见[迁移规则](../frontend/legacy-fronted-layout-migration.md)。

SettingsHostService.SaveConfigAsync 仍会将 AppData 实际路径替换为 %APPDATA%。无参数重置保存应用默认设置，不改变活动布局包；旧窗口级重置/迁移入口是 Obsolete 空壳，不应据旧 UI 说明删除用户图片。

## 其他用户数据

| 数据 | 路径 | 说明 |
| --- | --- | --- |
| v3 活动用户包布局 | `%APPDATA%\neo-bpsys-wpf\FrontedLayoutPackages\{PackageId}\FrontedLayouts\...` | 普通读写目标 |
| v3 内置默认布局 | `{AppBaseDirectory}\Resources\FrontedLayouts\{WindowTypeName}.json` | 应用内置布局（只读） |
| v3 布局包根目录 | `%APPDATA%\neo-bpsys-wpf\FrontedLayoutPackages\` |
| v3 editor-local 资源 | `%APPDATA%\neo-bpsys-wpf\FrontedLayoutPackages\local\` |
| v3 已安装布局包 | `%APPDATA%\neo-bpsys-wpf\FrontedLayoutPackages\{PackageId}\` |
| v3 活动包状态 | `%APPDATA%\neo-bpsys-wpf\FrontedLayoutPackages\active-package.json` |
| SmartBP 模块状态 | `%APPDATA%\neo-bpsys-wpf\SmartBpModuleState.json` |
| 教程与导览状态 | `%APPDATA%\neo-bpsys-wpf\TutorialState.json` |
| SmartBP 模块目录迁移标记 | `%APPDATA%\neo-bpsys-wpf\SmartBpModuleMovePending.json` |
| SmartBP 模块卸载路径记录 | `HKCU\Software\neo-bpsys-wpf\SmartBpModule\ModuleRoot` |
| SmartBP 模块默认安装目录 | `%LOCALAPPDATA%\neo-bpsys-wpf\Components\SmartBpModule` |
| OCR 模型 | `{SmartBpModuleRoot}\OCRModels` |
| 插件配置 | `%APPDATA%\neo-bpsys-wpf\PluginConfigs\{pluginId}` |
| 插件市场临时下载 | `%TEMP%\neo-bpsys-wpf\PluginMarket\...` |

SmartBP 模块加载/安装目录可在设置页修改。若当前已有可用模块目录，保存时会先复制旧目录到新目录的 staging，验证复制结果后移动到目标目录，再写入 `SmartBpModuleMovePending.json` 标记和 `SmartBpModuleState.json` 的目标 `ModuleRoot`，同时把目标路径写入 `HKCU\Software\neo-bpsys-wpf\SmartBpModule\ModuleRoot` 供卸载器清理。下一次从目标目录成功加载模块并写回状态后，会尝试删除旧目录；如果删除失败，迁移标记保留并记录 cleanup 错误，后续成功加载目标目录时继续清理。路径校验沿用模块安装安全规则，拒绝系统目录、驱动器根目录、不可写目录、源目录父子路径，以及包含非 SmartBP 内容的目标目录。

SmartBP 下载页“高级选项”的路径输入不走设置页的迁移询问，也不会立即复制模块程序文件；它会直接保存新的 `ModuleRoot`，并用 `SmartBpModuleMovePending.json` 记录旧目录与新目录。应用重启后先把旧目录中的 `OCRModels/`、`AI/` 合并复制到新目录（新目录已有文件优先），确认新模块加载成功后再删除整个旧目录。损坏、缺失或已被新操作取代的 pending 暂存不会阻止后续下载/迁移：管理器会放弃失效标记，并在安全范围内尽力清理旧暂存目录。

教程文件的 CompletedFlows/CompletedPackages、版本、会话跳过及永久跳过的准确语义见[ProductTour](../backend/product-tour-and-onboarding.md)。不要在设置路径文档复制另一份状态机。

Debug 构建下修改 SmartBP 模块目录不复制模块文件，也不写迁移标记；目标目录只要通过开发模块目录校验，就直接以 `InstallKind = DevelopmentDirectory` 写入状态和注册表，并清理旧的迁移标记。

旧版本 OCR 模型目录 `Documents\neo-bpsys-wpf\OCRModels` 只作为迁移来源。SmartBP 模块首次安装或首次成功加载后会把已就绪模型复制到模块根下的 `OCRModels`，验证成功后再删除对应旧模型目录。

## 布局方案与资源路径

当前活动包状态位于 FrontedLayoutPackages/active-package.json，缺失时默认 builtin。builtin 映射运行目录资源；local 仅为编辑器资源命名空间；普通可编辑包位于 FrontedLayoutPackages/{PackageId}。加载优先级、首次保存复制、激活/删除及内置回退的唯一完整说明见[布局读取契约](../frontend/fronted-layout-v3-window-centric.md)和 [BPUI](../frontend/bpui-package-v3.md)。

正常运行时没有“无包管理器时回退 loose store”的构造路径；旧 *Config-*.json、CustomUi 和缺版本 Config 只进入[启动迁移](../frontend/legacy-fronted-layout-migration.md)，源文件不能因普通切包而清空。已安装资源与字体隔离、URI、安全大小与解压限制以 BPUI 为准。

卸载脚本总是尝试删除 SmartBP 模块目录，路径依次来自注册表 ModuleRoot、SmartBpModuleState.json、默认目录；再询问是否删除应用 AppData（含日志、设置）。拒绝删除磁盘根、系统目录、安装目录和整个 AppData 配置目录。
