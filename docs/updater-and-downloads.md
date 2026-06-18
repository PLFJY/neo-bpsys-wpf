# 更新与下载

## 应用更新服务

`UpdaterService` 实现应用更新检查、安装包下载、SHA-256 校验和静默安装启动。它不是插件市场下载器，也不是 OCR 模型下载器。

更新信息来源当前写死为：

```text
https://gh-releases.plfjy.top/?repo=PLFJY/neo-bpsys-wpf&ua=neo-bpsys-wpf
```

如果 `IsFindPreRelease` 为 false，请求会追加 `&latest=true`，并按单个 `ReleaseInfo` 解析；如果为 true，会按 `ReleaseInfo[]` 解析并取第一个。

## 启动检查与手动检查

`App.OnStartup` 中的启动更新检查受条件编译控制。当前源码条件写作：

```csharp
#if !DEBUG && !PREVIEW
```

csproj 中 Preview 配置定义 `PREVIEW` 编译常量，因此 Debug 和 Preview 构建都会跳过启动自动检查；Release 和 Beta 构建会在启动后自动检查更新。

手动检查由设置页触发：

```csharp
await UpdaterService.UpdateCheck(false, Mirror);
```

启动检查发现新版本时使用 InfoBar 提示；手动检查发现新版本时弹确认框，确认后下载。

## 下载流程

应用更新下载固定寻找两个 release asset：

| 文件 | 用途 |
| --- | --- |
| `neo-bpsys-wpf_Installer.exe` | lite 默认安装包，也是旧 updater 固定目标 |
| `neo-bpsys-wpf_Installer.exe.sha256` | lite 默认安装包哈希，只对应 `neo-bpsys-wpf_Installer.exe` |

为保持旧 updater 兼容，不得重命名这两个 asset，也不得让 `neo-bpsys-wpf_Installer.exe.sha256` 指向 full 安装包或 SmartBP 模块。

Release 中还会发布：

| 文件 | 用途 |
| --- | --- |
| `neo-bpsys-wpf_Installer_full.exe` | 首次安装便利包，包含 lite 应用和 SmartBP 模块 staging 文件 |
| `neo-bpsys-wpf_Installer_full.exe.sha256` | full 安装包哈希，只对应 `neo-bpsys-wpf_Installer_full.exe` |
| `SmartBpModule.7z` | SmartBP 重型运行时模块官方 release artifact |
| `SmartBpModuleManifest.json` | SmartBP 模块兼容性、版本、大小和 SHA-256 信息 |

`SmartBpModuleManifest.json` 只用于 SmartBP 模块安装/加载，不参与主 installer 的 SHA-256 校验。
manifest 中的 `Asset.Name`、`Asset.Url`、`Asset.Size` 和 `Asset.Sha256` 指向同次构建生成的 `SmartBpModule.7z`。运行时仍接受旧 `SmartBpModule.zip` 包，用户不需要安装 7-Zip，也不需要 `7z.exe` 或 `7z.dll`。

下载位置在系统临时目录：

```text
%TEMP%\neo-bpsys-wpf_Installer.exe
%TEMP%\neo-bpsys-wpf_Installer.exe.sha256
```

流程：

1. 下载 installer。
2. 下载 `.sha256`。
3. 读取 `.sha256` 第一个 token，规范化为 64 位十六进制。
4. 计算 installer SHA-256。
5. 匹配后标记下载完成，并询问是否安装。
6. 安装时以 `/silent` 启动 installer，然后关闭当前应用。

构造 `UpdaterService` 时会清理残留 installer 和 sha256 文件。

## 镜像设置

应用更新、插件市场都使用 `DownloadMirrorPresets.GhProxyMirrorList` 作为 UI 候选；空字符串表示直连。设置页的 `Mirror` 变化会保存到 `Settings.GhProxyMirror`，并重置插件市场镜像缓存。

应用更新下载会把 `mirror` 直接拼在 release asset URL 前。插件市场则只在中文环境且目标 URL 是 GitHub/GitHubusercontent 时自动应用镜像，并会探测候选镜像可用性。

## 三类下载的差异

| 类型 | 服务 | 下载内容 | 校验 |
| --- | --- | --- | --- |
| 应用更新 | `UpdaterService` | 安装包和 `.sha256` | 必须校验 installer SHA-256 |
| 插件市场 | `PluginMarketService` + `PluginPageViewModel` | 插件 `.zip` 或 `.7z` | 市场条目有 `Sha256` 时校验归档文件 |
| SmartBP 模块 | `SmartBpModuleManager` | `SmartBpModuleManifest.json` 和 `SmartBpModule.7z`，兼容旧 `.zip` | manifest asset 有 `Sha256` 时校验归档文件 |
| OCR 模型 | 模块内 `OcrService` | PaddleOCR det/cls/rec 模型归档 | 依赖下载/解压和模型文件完整性检查 |

不要把这些下载的临时目录、状态字段或重启语义混在一起。插件安装/更新需要重启进入 DI；应用更新会运行安装包并关闭当前应用；SmartBP 模块在页面内动态加载；OCR 模型切换不需要重启。

SmartBP 模块和插件包的 `.zip` / `.7z` 解压由运行时共享归档服务完成，会拒绝绝对路径、`..` 遍历和规范化后逃逸目标目录的条目。`.bpui` / Designer v3 布局包行为不在这里改变，仍使用 BPUI 专用导入导出链路。

## 常见失败点

1. 更新 API 不可达或返回结构变化。
2. release 中缺少 installer 或 `.sha256` asset。
3. 镜像不可用，导致下载失败。
4. `.sha256` 内容为空、格式不合法或与 installer 不匹配。
5. 临时目录文件被占用，残留清理失败。
6. 安装包启动失败或被安全软件拦截。

相关日志写入 `%APPDATA%\neo-bpsys-wpf\Log`。设置页会显示下载进度、速度和下载完成状态。
