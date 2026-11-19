# 插件市场

## 服务职责

`PluginMarketService` 负责插件市场索引加载、README 加载、GitHub 镜像解析、下载队列、下载取消、SHA-256 校验和解压结果交付。实际安装逻辑在 `PluginPageViewModel.Market.cs` 中消费下载完成结果后执行。

默认市场索引：

```text
https://bpsys-plugin-index.plfjy.top/
```

插件市场页当前定义两个预设源：默认站点和 GitHub raw `PluginIndex.json`。页面把选中的值保存到 `Settings.PluginMarketSource`；服务层读取该设置，若为空则回退到内置默认源。服务层本身不关心来源是否来自预设列表，只把它当作插件市场索引 URL 使用。

## 页面与交互

`PluginMarketView` 在单一 `ModernFrame` 中承载目录、详情、下载队列和设置四个 `UserControl` 子视图，不嵌入 WPF Page。现有 `SelectedMarketPlugin`、`IsDownloadQueueOpen` 和 `IsPluginMarketSettingsOpen` 控制互斥的子视图；返回市场、Escape 和 Alt+Left 回到目录，已打开的 ComboBox 下拉优先处理 Escape。目录中的选择供键盘浏览，点击条目或按 Enter 才打开详情；返回保留目录滚动位置。

子视图使用有界视口并显式声明 Self 滚动归属：目录和下载列表启用 Recycling/Pixel 虚拟化，详情与设置各由一个 `ModernScrollViewer` 滚动，README 内容随详情页整体滚动，不单独声明 self-scroll。工具栏在窄窗口收起文字并保留图标、提示与无障碍名称；详情操作可换行，设置和下载操作不依赖固定宽度浮层。转场使用 333 ms 的方向滑动，异步内容与状态采用 83 ms 淡入，均遵从系统动画设置；宿主行为以 [ModernFrame](../backend/modern-frame.md) 为准。

README 的正文、加载、失败和空内容分开呈现，失败原因保存在非持久化的 `ReadmeErrorMessage`，不写入作者的 `ReadmeMarkdown`；失败后可重试。离开详情不取消下载，退场期间保留说明内容，转场完成或视图卸载后释放呈现引用。说明中的绝对 HTTP/HTTPS 链接可由用户点击打开浏览器；其他协议不交给系统启动。新增界面文案归属 PluginMarket 的中、英、日三份资源。

设计与动效参考微软的 [Windows motion](https://learn.microsoft.com/en-us/windows/apps/design/motion/) 和[响应式布局](https://learn.microsoft.com/en-us/windows/apps/design/layout/responsive-design)指南，保持 WPF-UI 原有主题、控件状态与业务命令。

## 目录本地缓存与刷新

进入插件管理页、完成入场转场后即启动一次缓存优先的市场目录检查，无需先切换到市场。已安装页采用 ItemsControl 呈现虚拟化卡片，不提供列表选择状态；其模板内保留单一有界滚动宿主、主题颜色和入场动效，显示清单 Description，不加载 README。项目地址采用普通本地化标签加 `ui:HyperlinkButton`，按钮显示实际 URL，只有 URL 可点击；启停与更新操作可换行，卸载独立靠右。更新按钮打开对应的市场详情，继续复用市场安装流程。

当前目录中存在版本高于本地已安装（或已暂存待重启）版本、API 兼容且未标记卸载的插件时，已安装卡片显示更新提示，主窗口大导航的 PluginPage（插件）入口显示 `➊` 通知，插件市场内部标签不显示角标。通知随目录刷新、切源、卸载标记及下载安装完成重新计算；已暂存相同新版时不再重复提示更新，仍显示重启提示。

页面首次请求目录时先读取 `IPluginMarketCatalogCache`，呈现完整本地快照后，再通过 `IPluginMarketService` 异步获取最新目录。手动刷新沿用当前列表，不清空内容；网络失败时保留快照并显示刷新失败提示，没有快照时才显示页面中央加载状态。请求期间刷新按钮的图标切换为 `ProgressRing`，同时禁止重复刷新，完成或失败后恢复。

缓存存放于 `%APPDATA%\neo-bpsys-wpf\Cache\PluginMarket\{contextHash}.json`，按实际索引源、语言和配置镜像隔离。切换这些设置会取消旧上下文请求并加载匹配的缓存；迟到的旧响应不能覆盖新上下文。缓存没有过期淘汰，每次首次加载及手动刷新仍请求网络，因此离线时可继续查看上次成功获取的目录。

缓存格式版本为 `1`，保存获取时间、上下文和明确列出的目录元数据（包含已解析的资源 URL），不缓存图片文件、README 正文、安装状态、下载状态或错误。安装与兼容状态每次根据当前宿主重新计算；读取缓存不触发镜像探测。缓存缺失、损坏、版本不支持或超过 8 MiB 时视为不可用，继续网络加载，不修补旧数据。有效的空目录也是完整快照。

磁盘读写及 JSON 处理在后台执行，写入通过同目录临时文件原子替换；保存失败不影响已取得的目录。网络成功后按插件 ID 对集合增删、移动和替换，未变化的条目保留对象引用及选中状态，不执行整表 Reset。`PluginMarketService.GetMarketPluginsAsync` 仍直接请求网络，布局包依赖检查等其他调用方不改为读取页面缓存。

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
| 当前任务暂停/继续 | `PauseDownload()` / `ResumeDownload()` |
| 指定任务暂停/继续 | `PauseDownload(queueId)` / `ResumeDownload(queueId)`；只对当前传输项生效 |
| 当前任务取消 | `CancelDownload()` |
| 指定任务取消 | `CancelDownload(queueId)` |
| 进度/速度 | 来自 `IFileDownloadOperation.StateChanged` 的进度快照 |
| 完成消费 | `ConsumeCompletedDownload()` 返回解压目录 |

下载过程使用稳定缓存路径保存可续传分片，并用独立会话目录承载解压结果：

```text
%TEMP%\neo-bpsys-wpf\PluginMarket\Downloads\{requestHash}\package.archive.download.part
%TEMP%\neo-bpsys-wpf\PluginMarket\Sessions\{pluginId}\{queueId}\extract
```

取消或瞬时失败只清理解压会话，保留下载分片和来源元数据；重新提交相同插件版本与下载契约时会从已有字节继续。

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

`.bpui v3` 导入发现缺失插件控件或插件版本低于依赖 `MinVersion` 时，`FrontManagePage` 会按插件 `PackageId` 聚合依赖并查询插件市场索引。UI 会展示缺失 / 需更新插件、最低版本、已安装版本、受影响控件和市场可用状态，并让用户显式选择：

1. 从插件市场安装或更新可用插件。
2. 继续导入并保留缺失或不满足版本的插件窗口布局 / 控件配置。
3. 取消导入。

安装 / 更新仍复用插件市场下载队列：市场条目的下载地址解析、GitHub 镜像、SHA-256 校验和解压流程仍由 `PluginMarketService` 负责；已解压插件包的 manifest 校验、API 兼容性检查和 `.new` 暂存更新由共用安装服务处理。布局导入不会静默安装插件，也不会在安装后把插件当作当前进程已加载继续导入；当前插件系统需要重启，用户可重启后重新导入，也可先继续导入并保留缺失插件配置，等插件可用后恢复完整渲染。

`.bpui` 包不得包含插件 DLL、插件 zip、安装包或可执行脚本。布局包只保存 Canvas `RequiredPlugins` 和 manifest `PluginDependencies` 这样的依赖元数据，插件安装仍必须走插件系统 / 插件市场流程并要求用户确认。导入器会直接拒绝包含 `Plugins/`、`Plugin/`、`.dll`、`.exe`、`.msi`、`.ps1`、`.bat`、`.cmd`、`.sh`、`.vbs`、`.js` 或 `.jar` 的 `.bpui`。

依赖安装引导在队列结束后须校验每项插件已安装或已暂存；失败显示插件 ID 与错误，未完成项不能当成功。安装到磁盘仍不代表当前进程已加载。

源码入口：[PluginMarketService](../../neo-bpsys-wpf/Services/PluginMarketService.cs)、[市场页面安装消费](../../neo-bpsys-wpf/ViewModels/Pages/PluginPageViewModel.Market.cs)。依赖格式与缺失插件保留以 [BPUI](../frontend/bpui-package-v3.md)为准。
