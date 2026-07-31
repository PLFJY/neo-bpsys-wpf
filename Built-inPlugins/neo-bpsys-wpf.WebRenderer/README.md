# neo-bpsys-wpf.WebRenderer

[neo-bpsys-wpf](https://github.com/PLFJY/neo-bpsys-wpf) 的内置插件 —— 实验性 Web Renderer，将当前活动 `.bpui v3` 布局包通过 Kestrel sidecar 推送到浏览器，供 OBS 浏览器源捕获。与现有 WPF 前台窗口并行运行，不替换既有渲染路径。

> **状态**：实验性（`version: 0.1.0.0`）。完整架构、协议、资源授权与排查细节见 [docs/plugin/web-renderer-experimental.md](../../docs/plugin/web-renderer-experimental.md)。

## 简介

WebRenderer 插件把主程序的 Window-centric v3 布局、控件语义树、本地化文本与运行时数据通过命名管道 IPC 发送给同目录下的 ASP.NET Core sidecar（`Host`），sidecar 通过 WebSocket 把布局与 runtime snapshot 广播给已连接的浏览器。浏览器使用 React + Vite 构建的客户端还原固定设计画布。

关键特性：

- **并行运行**：WPF 前台窗口照常工作，Web 前台只是额外输出通道，比赛状态仍由 WPF 的 `ISharedDataService` 权威维护
- **资源隔离**：`.bpui` 包内图片、字体、应用内置资源通过随机 token 的 `/bpui-assets/{resourceToken}` 提供，浏览器无法获得物理路径；在线图片通过 `/remote-assets/{token}` 经 sidecar 异步下载与缓存
- **运行时检测与一键安装**：自动检测 x64 `Microsoft.AspNetCore.App 10.*`；缺失时管理页提供下载 → 校验 → 静默安装 → 重启引导
- **Transition fail-open**：WPF 始终是业务 commit 的唯一所有者，浏览器 transition 超时或断线时 fail-open，不阻塞主程序

## 架构概览

```
主程序 (WPF)
  ├─ WebRendererPlugin (本插件)
  │   ├─ WebRendererSidecarService         # sidecar 进程与 IPC 会话管理
  │   ├─ WebRendererBootstrapBuilder       # 构建活动包 bootstrap snapshot
  │   ├─ WebRendererRuntimeStatePublisher  # 推送 runtime / localization / transition
  │   ├─ WebRendererRuntimeDetector        # 检测 ASP.NET Core Runtime 10 (x64)
  │   ├─ WebRendererRuntimeSetupService    # 下载 / 校验 / 安装 runtime
  │   ├─ WebRendererLifecycleOperationCoordinator  # 互斥的 start/stop/restart 状态机
  │   ├─ WebTransitionOrchestratorDecorator       # 装饰原 TransitionOrchestrator
  │   └─ WebRendererManagementPage / ViewModel    # 后台管理页
  │
  └─ 命名管道 IPC (version 8)
        ↓
Sidecar (Host/neo-bpsys-wpf.WebRenderer.Host, net10.0, win-x64)
  ├─ Kestrel HTTP / WebSocket 服务
  ├─ StaticClientVerifier               # 校验 wwwroot/index.html 与引用资源
  ├─ RemoteAssetFetcher                 # 在线图片下载、缓存、SSRF 防护
  └─ WebRendererHostState               # bootstrap / runtime / localization 广播
        ↓
浏览器 (React + Vite 客户端)
  ├─ /render/{encodedFullWindowType}    # 单窗口渲染页
  ├─ /ws                                # WebSocket 接收 runtime snapshot
  └─ /bpui-assets/* /remote-assets/*    # 通过 token 获取授权资源
```

### 后台管理页

`WebRendererManagementPage` 通过 `AddBackendPage<WebRendererManagementPage, WebRendererManagementViewModel>()` 注册，提供：

- **窗口地址列表**：每个已确认发布的 v3 layout 窗口显示 displayName、URL、OBS 尺寸提示与诊断信息，一键复制
- **生命周期控制**：启动 / 停止 / 重启 sidecar（互斥操作，操作期间禁用按钮）
- **运行时引导**：runtime 缺失时顶部显示引导区域，支持下载并安装、重新检测、打开官方下载页
- **高级设置**：Host、Port、Exit/Enter 超时、随应用启动、协议日志；保存后自动重启 sidecar
- **诊断导出**：把当前 sidecar 状态（PID、地址、客户端数、bootstrap generation、最近错误等）导出为 JSON 文件

## 运行时依赖

### ASP.NET Core Runtime 10 (x64)

sidecar 是 framework-dependent 的 `net10.0` 应用，不携带 runtime。`WebRendererRuntimeDetector` 通过查询 Windows 注册表（`HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall` 与 `HKLM\SOFTWARE\WOW6432Node\...`）确认 `Microsoft.AspNetCore.App 10.*` x64 是否存在。

缺失时 `WebRendererRuntimeSetupService` 提供一键安装流程：

| 阶段 | 行为 |
| --- | --- |
| `FetchingRelease` | `WebRendererRuntimeReleaseFeed` 查询 Microsoft 官方 release metadata，解析最新 10.0.x 版本、win-x64 installer 直链与 SHA-512；网络失败时回退到内置常量 `KnownFallbackVersion`（当前 `10.0.10`）与稳定 CDN URL pattern |
| `Downloading` | 通过宿主 `IFileDownloadService` 下载到 `%TEMP%\neo-bpsys-wpf_WebRenderer\`，实时回传进度并支持暂停、取消后续传 |
| `Paused` | 下载已暂停，保留分片；继续后从已有字节恢复 |
| `Verifying` | 若 release metadata 提供 SHA-512，使用 `SHA512.HashDataAsync` 校验；hash 缺失则跳过并记录 warning |
| `Installing` | 以 `/quiet /norestart` 参数、`Verb=runas`（UAC 提升）唤起 installer 静默安装；UAC 拒绝或退出码非 0 时回退为手动安装引导 |
| `AwaitingRestart` | 设置 `IGlobalRestartService.IsRestartRequired = true`，应用标题栏出现"需要重启"按钮，由用户点击触发 `App.Current.Restart()` |

### pnpm + Node.js（仅构建期）

Web 前端（`Web/`）由 Host sidecar 的 MSBuild target 调用 `pnpm` 构建。构建机未安装 pnpm/Node.js 或前端构建失败时，MSBuild 不会中断主程序构建，而是发出警告并跳过该插件的打包（不复制到 `Plugins\`），主程序及其他内置插件照常构建。

运行期主程序、sidecar 进程启动与浏览器渲染均不依赖 Node.js。

## 命令行参数

参数优先级高于插件设置，可在主程序启动时传入：

| 参数 | 说明 | 默认值 |
| --- | --- | --- |
| `--web-host`（或 `--host`） | 监听 IPv4 地址，必须是合法 IPv4 | `127.0.0.1` |
| `--web-port`（或 `--port`） | 监听端口，1–65535 | `19527` |
| `--web-no-start` | 不随应用启动自动启动 sidecar | 由 `StartWithApplication` 决定 |
| `--web-log-protocol` | 记录 IPC 协议摘要日志 | `false` |
| `--web-transition-exit-timeout-ms` | Exit fail-open 超时（1–30000ms） | `2000` |
| `--web-transition-enter-timeout-ms` | Enter fail-open 超时（1–30000ms） | `2000` |

实验性 LAN 模式可使用 `--web-host 0.0.0.0`：此模式无访问认证，同一网络中的设备可读取页面与实时数据。请只在受信任网络使用，并通过系统防火墙限制端口。

## HTTP 端点

sidecar 默认监听 `http://127.0.0.1:19527`，提供以下端点：

| 路径 | 说明 |
| --- | --- |
| `/` | 入口页（`index.html`，`Cache-Control: no-store`） |
| `/health` | sidecar 健康状态（含 `clientBuildId`、生命周期状态、客户端数等） |
| `/render/{encodedFullWindowType}` | 单窗口渲染页（`encodedFullWindowType` 为 Base64 URL-safe 编码的窗口类型） |
| `/api/windows` | 已发布窗口列表（IPC 未连接返回 `503 IpcUnavailable`，bootstrap 未确认返回 `503 BootstrapPending`） |
| `/api/bootstrap/{encodedFullWindowType}` | 单窗口 bootstrap 数据 |
| `/bpui-assets/{resourceToken}` | 当前活动包内授权资源（图片、字体等） |
| `/remote-assets/{token}` | 在线图片（经 sidecar 下载、校验、缓存后提供） |
| `/ws` | WebSocket，接收 runtime snapshot、localization、transition 推送 |
| `/assets/*.js`、`/assets/*.css` | Vite 生成的带 hash 静态文件（`Cache-Control: public, max-age=31536000, immutable`） |

## 目录结构

```
neo-bpsys-wpf.WebRenderer/
├── WebRendererPlugin.cs                         # 插件入口，注册服务与后台页
├── WebRendererManagementPage.xaml(.cs)          # 后台管理页 UI
├── WebRendererManagementViewModel.cs            # 后台管理页 ViewModel
├── WebRendererBooleanInverter.cs                # Boolean 反转转换器
├── Protocol/
│   └── WebRendererIpcMessage.cs                 # IPC 消息定义（主程序与 sidecar 共享）
├── Services/
│   ├── WebRendererSidecarService.cs             # sidecar 进程与 IPC 会话管理（IHostedService）
│   ├── WebRendererBootstrapBuilder.cs           # 构建 bootstrap snapshot
│   ├── WebRendererRuntimeStatePublisher.cs      # runtime / localization / transition 推送
│   ├── WebRendererLaunchOptions.cs              # 启动选项解析与验证
│   ├── WebRendererPluginSettings.cs             # 插件私有设置（Settings.json）
│   ├── WebRendererRuntimeDetector.cs            # ASP.NET Core Runtime 10 (x64) 检测
│   ├── WebRendererRuntimeReleaseFeed.cs         # 在线查询 runtime release metadata
│   ├── WebRendererRuntimeSetupService.cs        # runtime 下载 / 校验 / 安装引导
│   ├── WebRendererLifecycleOperationCoordinator.cs  # 互斥生命周期操作
│   ├── WebRendererSidecarJob.cs                 # Windows Job Object 进程树绑定
│   ├── WebTransitionGateway.cs                  # Web transition 网关
│   ├── WebTransitionOrchestratorDecorator.cs    # 装饰原 TransitionOrchestrator
│   ├── WebControlRegistry.cs                   # Web 控件注册表
│   ├── WebBehaviorEventMessage.cs              # 行为事件消息
│   └── WebRuntimeValue.cs                      # Web runtime 值工厂
├── Host/                                        # ASP.NET Core sidecar（独立项目，net10.0, win-x64）
│   ├── Program.cs                               # Kestrel 启动、路由、WebSocket
│   ├── RemoteAssetFetcher.cs                    # 在线图片下载与缓存
│   ├── StaticClientVerifier.cs                  # wwwroot 入口页与引用资源校验
│   ├── Properties/                              # launchSettings.json
│   └── neo-bpsys-wpf.WebRenderer.Host.csproj
├── Web/                                         # React + Vite 前端源码
│   ├── src/
│   │   ├── app/                                 # WebRendererApp、CanvasRuntime
│   │   ├── behavior/                            # 行为协议、动画状态机、transition barrier
│   │   ├── protocol/                            # bootstrap / runtime TypeScript 类型
│   │   ├── renderer/                            # 控件、图片、动画部件渲染
│   │   └── runtime/                             # RuntimeStore、LocalizationStore
│   ├── index.html
│   ├── package.json
│   ├── pnpm-lock.yaml
│   ├── vite.config.ts
│   └── vitest.config.ts
├── manifest.yml                                 # 插件清单
└── neo-bpsys-wpf.WebRenderer.csproj
```

## 构建

```powershell
# 单独构建插件（包含 Host sidecar 与 Web 前端）
dotnet build .\Built-inPlugins\neo-bpsys-wpf.WebRenderer\neo-bpsys-wpf.WebRenderer.csproj -c Release

# 完整构建（包含主项目与所有插件）
.\build.ps1
```

构建流程：

1. `neo-bpsys-wpf.WebRenderer.csproj` 的 `BuildWebRendererSidecar` target 调用 `Host\neo-bpsys-wpf.WebRenderer.Host.csproj`（`RuntimeIdentifier=win-x64`、`SelfContained=false`）
2. Host csproj 的 `BuildWebRendererClient` target 调用 `pnpm install --frozen-lockfile` 与 `pnpm run build` 构建 Web 前端到 `Web\dist\`
3. Host csproj 的 `CopyWebRendererClient` target 把 `Web\dist\**` 复制到 Host 输出目录的 `wwwroot\`
4. WebRenderer csproj 的 `CopyWebRendererSidecar` target 把 Host 构建产物复制到插件输出目录的 `Host\`

若 pnpm 不可用或前端构建失败，MSBuild 发出警告并跳过该插件打包（主程序及其他内置插件照常构建）。

### 部署验收

```powershell
# 静态产物链路验证（从干净构建到最终 Plugins 目录）
.\tools\Test-WebRendererDeployment.ps1 -Configuration Release

# 真实 IPC 与布局验收（发布并启动真实主程序，要求 sidecar 到达 Ready）
.\tools\Test-WebRendererIpc.ps1 -Configuration Debug
```

## 配置

插件私有设置保存在 `PluginConfigs/top.plfjy.bpsys.WebRenderer/Settings.json`，不修改主程序 `Config.json`：

```json
{
  "Host": "127.0.0.1",
  "Port": 19527,
  "StartWithApplication": false,
  "ExitTimeoutMs": 2000,
  "EnterTimeoutMs": 2000,
  "LogProtocol": false
}
```

`StartWithApplication` 默认为 `false`：sidecar 不随应用启动自动运行，避免未使用 Web Renderer 时产生常驻子进程内存占用。已有 `Settings.json` 的旧用户升级后保留原值，行为不变。

## 在线图片处理

队伍 JSON 中的 HTTP/HTTPS Logo 与选手定妆照保留为 URI，主程序只发布规范化 URI 的不可变描述符，不下载图片字节。sidecar 通过 `RemoteAssetFetcher` 独立异步下载：

- **协议与格式**：仅允许 HTTP/HTTPS 的 PNG、JPEG、WebP、GIF
- **超时与限制**：连接超时 5 秒、总请求超时 20 秒、单项上限 10 MiB，最多跟随 5 次重定向
- **缓存**：成功结果进入 64 MiB 内存 LRU 与 512 MiB、最长 7 天的磁盘缓存；失败不持久缓存
- **SSRF 防护**：每次请求与重定向都重新校验地址，拒绝 userinfo、非 HTTP/HTTPS、localhost、环回、链路本地、组播、未指定与 RFC1918 私有地址；Clash TUN 常用的 `198.18.0.0/15` 仅作为 Fake-IP 路由地址允许交由操作系统代理处理
- **日志**：只记录稳定诊断码、generation 与截短 token，不记录源 URL、查询参数或完整缓存路径

## 已知限制

- 实验性功能，API 与协议可能随版本调整
- `plugin:*` 控件必须提供受控的 Web adapter 才能渲染，否则显示诊断占位
- 浏览器字体栅格化与 WPF 可能略有不同
- 默认监听 localhost，LAN 模式无访问认证
- sidecar 退出 / 主程序异常终止时，已下载的在线图片缓存保留在 sidecar 输出目录
- WPF 始终是业务 commit 的唯一所有者；浏览器 transition 超时 / 断线时 fail-open，不阻塞主程序

## 进一步阅读

- [实验性 Web Renderer（完整架构、协议、资源授权、排查）](../../docs/plugin/web-renderer-experimental.md)
- [插件系统](../../docs/plugin/plugin-system.md)
- [前台窗口系统](../../docs/frontend/fronted-window-system-deep-dive.md)
