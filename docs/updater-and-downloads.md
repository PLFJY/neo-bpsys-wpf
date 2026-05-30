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
#if !DEBUG && !Preview
```

csproj 中 Preview 配置定义的是 `PREVIEW`。由于 `Preview` / `PREVIEW` 命名不一致，本文不声称 Preview 构建一定被排除；本任务只记录这个代码观察到的 caveat，不修改代码。

手动检查由设置页触发：

```csharp
await UpdaterService.UpdateCheck(false, Mirror);
```

启动检查发现新版本时使用 InfoBar 提示；手动检查发现新版本时弹确认框，确认后下载。

## 下载流程

应用更新下载固定寻找两个 release asset：

| 文件 | 用途 |
| --- | --- |
| `neo-bpsys-wpf_Installer.exe` | 安装包 |
| `neo-bpsys-wpf_Installer.exe.sha256` | 安装包哈希 |

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
| 插件市场 | `PluginMarketService` + `PluginPageViewModel` | 插件 zip | 市场条目有 `Sha256` 时校验 |
| OCR 模型 | `OcrService` | PaddleOCR det/cls/rec 模型归档 | 依赖下载/解压和模型文件完整性检查 |

不要把三者的临时目录、状态字段或重启语义混在一起。插件安装/更新需要重启进入 DI；应用更新会运行安装包并关闭当前应用；OCR 模型切换不需要重启。

## 常见失败点

1. 更新 API 不可达或返回结构变化。
2. release 中缺少 installer 或 `.sha256` asset。
3. 镜像不可用，导致下载失败。
4. `.sha256` 内容为空、格式不合法或与 installer 不匹配。
5. 临时目录文件被占用，残留清理失败。
6. 安装包启动失败或被安全软件拦截。

相关日志写入 `%APPDATA%\neo-bpsys-wpf\Log`。设置页会显示下载进度、速度和下载完成状态。
