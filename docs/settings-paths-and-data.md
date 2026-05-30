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
| `LogPath` | `%APPDATA%\neo-bpsys-wpf\Log` |
| `ResourcesPath` | `{AppBaseDirectory}\Resources` |
| `PluginPath` | `%APPDATA%\neo-bpsys-wpf\Plugins` |
| `BuiltInPluginPath` | `{AppBaseDirectory}\Plugins` |
| `PluginConfigsPath` | `%APPDATA%\neo-bpsys-wpf\PluginConfigs` |

用户可修改或运行时生成的状态主要在 AppData、Documents 输出目录和 Temp 中。安装目录下的 `Resources` 和内置插件通常来自构建/发布产物。

## Settings 顶层字段

| 字段 | 说明 |
| --- | --- |
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
| SmartBP 区域 | `%APPDATA%\neo-bpsys-wpf\SmartBp\GameDataRegions.json` |
| OCR 模型 | `Documents\neo-bpsys-wpf\OCRModels` |
| 插件配置 | `%APPDATA%\neo-bpsys-wpf\PluginConfigs\{pluginId}` |
| 插件市场临时下载 | `%TEMP%\neo-bpsys-wpf\PluginMarket\...` |

卸载脚本会询问是否删除 `%APPDATA%\neo-bpsys-wpf`，包括日志、自定义 UI 和设置。
