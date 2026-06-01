# 插件市场

## 服务职责

`PluginMarketService` 负责插件市场索引加载、README 加载、GitHub 镜像解析、下载队列、下载取消、SHA-256 校验和解压结果交付。实际安装逻辑在 `PluginPageViewModel.Market.cs` 中消费下载完成结果后执行。

默认市场索引：

```text
https://bpsys-plugin-index.plfjy.top/
```

插件市场页当前定义两个预设源：默认站点和 GitHub raw `PluginIndex.json`。页面把选中的值保存到 `Settings.PluginMarketSource`；服务层读取该设置，若为空则回退到内置默认源。服务层本身不关心来源是否来自预设列表，只把它当作插件市场索引 URL 使用。

## 市场条目

市场索引反序列化为 `Dictionary<string, PluginMarketItem>`。服务会补齐空字段，并解析：

| 字段 | 处理 |
| --- | --- |
| `Icon` | 解析为 `ResolvedIconUrl` |
| `Readme` | 解析为 `ResolvedReadmeUrl` |
| `DownloadUrl` | 解析为 `ResolvedDownloadUrl` |
| `Sha256` | 下载完成后用于校验 |

README 加载后会重写相对链接和相对图片地址，避免 WPF Markdown 控件打开相对路径失败。

## GitHub 镜像

只有中文环境下才会对 GitHub/GitHubusercontent 地址应用镜像。判断依据是 `Settings.CultureInfo.Name` 是否以 `zh` 开头。

镜像优先使用 `Settings.GhProxyMirror`，失败后尝试 `DownloadMirrorPresets.GhProxyMirrorList` 中的其他候选。每个镜像会先用 4 秒超时探测可用性，成功后缓存。

非 GitHub 地址或非中文环境下直接使用原地址。

## 下载队列

下载请求会进入 `_pendingDownloads`，UI 看到的是只读 `DownloadQueue`。服务一次处理一个任务，状态通过 `DownloadStateChanged` 通知页面刷新。

支持：

| 功能 | 说明 |
| --- | --- |
| 队列去重 | 当前下载和等待队列中已有同 ID 插件时拒绝重复入队 |
| 当前任务取消 | `CancelDownload()` |
| 指定任务取消 | `CancelDownload(queueId)` |
| 进度/速度 | 来自 Downloader 的 `DownloadProgressChanged` |
| 完成消费 | `ConsumeCompletedDownload()` 返回解压目录 |

下载过程临时目录：

```text
%TEMP%\neo-bpsys-wpf\PluginMarket\{pluginId}\{queueId}
```

## SHA-256 校验

如果市场条目提供 `Sha256`，服务会在解压前计算下载 zip 的 SHA-256 并比较。比较前会去掉连字符并转小写。校验失败会中止流程并清理下载会话目录。

这保证“下载的包与市场声明一致”，但不等于插件是安全沙箱。插件仍是全信任代码。

## 安装消费

下载完成后，`PluginPageViewModel` 会：

1. 通过 `ConsumeCompletedDownload()` 取出解压目录。
2. 检查 `manifest.yml`。
3. 解析 manifest 并做 API 兼容性检查。
4. 如果插件已存在，移动到 `Plugins\.new\{id}`，标记重启后更新。
5. 如果是新插件，移动到 `Plugins\{id}`，加入页面集合并标记需要重启。
6. 清理临时目录。

市场能提供索引、下载和哈希校验；它不提供运行时隔离，也不保证插件没有逻辑风险。

## 与 `.bpui` 布局包的关系

Phase 13D 的 `.bpui v3` 导入只做插件依赖预检，不会调用插件市场下载或安装插件。导入器发现缺失插件控件时，当前 UI 只提供“取消导入”或“强制导入并删除缺失插件控件”；交互式安装 / 更新引导仍是 Phase 13E。

`.bpui` 包不得包含插件 DLL、插件 zip 或其他插件二进制。布局包只保存 Canvas `RequiredPlugins` 和 manifest `PluginDependencies` 这样的依赖元数据，插件安装仍必须走插件系统 / 插件市场流程并要求用户确认。
