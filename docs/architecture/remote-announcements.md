# 远程公告中心

远程公告是 WPF 后台的独立运行数据。客户端只访问 GitCode 的公开 Raw JSON，基础地址集中定义在 `AppConstants.GitCodeAnnouncementRawBaseUrl`：`https://raw.gitcode.com/PLFJY/neo-bpsys-announce-source/raw/main/`。客户端不使用 GitCode REST API、Token 或中间服务。

启动时 `App.OnStartup` 在主窗口显示后启动受异常保护的异步任务：先读取本地 `manifest.json`、`state.json` 和有效正文缓存，再请求 Raw 基础地址下的 `manifest.json`。正文 URL 优先由同一基础地址与 manifest entry 的相对 `path` 拼接；缺失或空白 `path` 时使用 `announcements/{id}.json`。显式填写但不安全的路径会被拒绝。公告 HttpClient 使用 Chrome User-Agent 请求 Raw JSON，且不跟随重定向，以保证请求只发往 Raw 域名。每次启动只自动检查一次，不定时轮询；主页按钮打开公告层时会触发一次联网刷新，公告层右上角的刷新按钮也可手动同步。`RemoteAnnouncementService` 是 HTTP、过滤、SHA-256 校验及文件持久化的唯一责任方；公告 UI 通过共享的 `AnnouncementCenterViewModel` 观察它。更新服务不参与公告流程。

缓存位于 `%APPDATA%/neo-bpsys-wpf/RemoteAnnouncements/`，包含 `manifest.json`、`state.json` 和 `cache/{id}.json`。路径由 `AppConstants` 集中定义。正文原始字节的 SHA-256 必须匹配清单；仅缺失或 hash 变化的适用公告需要下载。新正文先完成校验，必要下载全部成功后才写入正文缓存和最近成功的 manifest。文件采用同目录临时文件与覆盖移动，失败时保留上次成功的 manifest 与可用缓存。远端停用或删除公告不清理缓存文件。

正常启动只请求一次 Raw manifest。缓存 hash 全部匹配时不请求正文；每条新增或 hash 变化的适用公告各增加一次 Raw 正文请求。

适用性由 `enabled`、编译通道（`BETA`、`PREVIEW`、其它为 `release`）和包含边界的应用版本范围决定。版本取 `AppConstants.AppVersion` 的开头数字版本部分；异常版本范围只跳过相应公告。`state.json` 只保存已读公告 ID。正文修订或 hash 改变不会再次提示同一个 ID。

`MainWindow.xaml` 的根 Grid 承载 `AnnouncementOverlay` UserControl；启动同步结束且有未读公告时，等待启动遮罩 Storyboard 完成后自动打开。公告开启期间主应用内容使用轻量 BlurEffect，公告层本身不模糊。公告按发布时间倒序，打开时最新一条及全部未读条目展开，其他条目折叠；未读条目显示主题色提示点，主页公告入口显示未读数。点击「已读」写入当前可显示公告 ID、折叠全部条目并关闭；点击「稍后阅读」只关闭，保留未读数。背景和 ESC 不关闭。正文沿用 `WPF-UI.Markdown`。网络或缓存错误仅记录日志，不向用户弹出错误提示。
