# 实验性 Web Renderer

Web Renderer 是独立内置插件 `top.plfjy.bpsys.WebRenderer`。它会把当前活动 `.bpui v3` 包的 Window-centric 布局发送给 Kestrel sidecar，并在浏览器中还原固定设计画布。它不替代现有 WPF 前台窗口；WPF 与网页会并行运行，比赛状态仍只由 WPF 的 `ISharedDataService` 权威维护。

插件本体不引用 ASP.NET Core；它会先检测 x64 的 `Microsoft.AspNetCore.App 10.*`，然后启动同目录 `Host` 下 framework-dependent 的 `net10.0` sidecar。缺少 runtime、进程启动错误、端口占用或 IPC 断开只会记录日志和显示非模态提示，不会中止主 WPF 应用。请安装 [ASP.NET Core Runtime 10 (x64)](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) 后点击“重新检测”。

默认地址为 `http://127.0.0.1:19527`，可使用：

```text
--web-host <IPv4>
--web-port <1-65535>
--web-no-start
--web-transition-exit-timeout-ms <1-30000>
--web-transition-enter-timeout-ms <1-30000>
```

默认监听 localhost。实验性 LAN 模式可以使用 `--web-host 0.0.0.0`，此模式没有访问认证，同一网络中的设备可读取页面与实时展示数据；请只在受信任网络使用，并通过系统防火墙限制端口。sidecar 提供 `/`、`/health`、`/render/{encodedFullWindowType}`、`/api/windows`、`/api/bootstrap/{encodedFullWindowType}`、`/bpui-assets/{resourceToken}` 和 `/ws`。`/assets/*.js` 与 `/assets/*.css` 仅用于 Vite 生成的 Web 客户端静态文件；`.bpui` 图片、字体等授权资源仅使用 `/bpui-assets/{resourceToken}`，两者不会共用 URL 前缀。

资源 URL 是每次 bootstrap 创建的随机 token；浏览器不会获得物理路径。插件只授权当前活动包、`local`、内置 `Resources/...` 和已知应用字体，拒绝绝对路径、跨包引用及编码路径穿越。切换包或 Designer 保存布局时会重新发送 bootstrap，页面通过 WebSocket 刷新。未知内置控件与没有 Web adapter 的 `plugin:*` 控件会显示诊断占位；纯 Binding 文本显示绑定路径占位。

每次 Web client 构建都会写入 client build id（提交标识与构建时间）。该值同时出现在最终 `index.html` 的 meta 标签、浏览器启动日志和 `/health` 的 `clientBuildId`。sidecar 启动前会验证最终 `Host/wwwroot/index.html`、build id 以及它引用的每一个本地 script/link 文件；任何缺失或越界引用都会让 sidecar 以明确错误退出，而不会在运行期间持续请求不存在的 hash 文件。

`/`、`/render/*` 和 `/index.html` 始终返回 `Cache-Control: no-store`，确保入口页不会缓存旧 bundle 引用。带内容 hash 的 `/assets/*.js` 与 `/assets/*.css` 使用 immutable 缓存策略。

IPC 使用 version 5，并采用显式会话状态：`Stopped`、`StartingProcess`、`WaitingForPipe`、`PipeConnected`、`BuildingBootstrap`、`WaitingForBootstrapAck`、`Ready`、`Stopping`、`Faulted`。主程序是状态权威；sidecar 必须先发送 `sidecar.ready`，主程序才发送 `host.hello` 和真实活动包的 `bootstrap.replace`。sidecar 会原子校验协议版本、generation、窗口结构与本地资源表，成功后返回 `bootstrap.applied`；只有该确认到达，管理页、`/health` 与 `/api/windows` 才会显示 `Ready` 和已发布窗口。连接断开时 sidecar 保持 HTTP 进程并以封顶退避重连，重连后的第一组消息始终是完整 bootstrap 和 runtime snapshot。

`/api/windows` 和 `/api/bootstrap/{encodedFullWindowType}` 在 IPC 未连接时返回 `503 IpcUnavailable`，bootstrap 尚未确认时返回 `503 BootstrapPending`，构建或校验失败时返回结构化 `503` 错误。`/render/{...}` 在等待状态显示“正在等待主程序布局数据”；仅 Ready 后才区分 `UnknownWindow` 与 `LayoutUnavailable`。管理页仅把 sidecar 已确认的窗口作为可用 Web Runtime，不会用 registry 候选窗口伪装布局已发布。

行为文档随 bootstrap 一并从活动包加载。Web 页面只消费 `IFrontedEventBus` 桥接出的语义事件，在本页独立执行 OneShot、Loop 和 Transition 节点图；断线、刷新、包切换和布局重载都会取消页面的 delay 与动画。Transition 由插件装饰原始 WPF 编排器：WPF 与 Web 先各自运行 ExitGraph，浏览器确认后才允许原始 C# `commitAsync` 执行一次；commit 后两端并行运行 EnterGraph。浏览器从不拥有业务提交能力，Web 未连接、断线、异常或超过默认 2000ms 的可配置等待上限均 fail-open。

Web 动画支持全部当前公开属性：Opacity、Visibility、VisualOffsetX/Y、ClipInset、Scale、Rotation、Width、Height、Fill/Stroke/Text/Foreground 色彩、StrokeThickness、FontSize、TintColor、TintStrength、TextureStrength 与 GaussianBlurRadius。基值仅在当前页面 Runtime session 中捕获，Reset 和 Reset All 会取消对应动画并恢复捕获状态。

## 管理与排查

插件启用并重启应用后会在后台导航中出现 **Web Renderer** 实验页面。页面可以启动、停止和重启 sidecar，显示与 `/health` 一致的生命周期状态、本机/局域网 URL、连接客户端、已确认活动包、已确认窗口和最近错误，也可以复制或在默认浏览器打开本机 URL。Host、Port、随应用启动、Transition fail-open timeout 与协议摘要日志保存在插件自己的 `PluginConfigs/top.plfjy.bpsys.WebRenderer/Settings.json`，不会修改主程序 `Config.json`。保存配置会重启 sidecar。

命令行参数优先于插件设置：`--web-host`（或 `--host`）、`--web-port`（或 `--port`）、`--web-no-start` 与 `--web-log-protocol`。协议日志只记录连接和消息摘要，不记录动画逐帧数据。

OBS 可添加 Browser Source 并填入本机 URL 或指定窗口的 `/render/{encodedFullWindowType}` URL；浏览器字体栅格化与 WPF 可能略有不同，但布局坐标、资源和行为语义保持一致。`plugin:*` 控件只有提供受控 Web adapter 才能渲染，否则显示诊断占位。

## 部署验收

从仓库根目录运行以下命令可验证从干净构建到最终主程序插件目录的完整静态产物链路：

```powershell
.\tools\Test-WebRendererDeployment.ps1 -Configuration Release
```

脚本会在 `build/web-renderer-deployment-validation/app/Plugins/top.plfjy.bpsys.WebRenderer/Host/wwwroot/index.html` 检查最终入口页与所有引用资源，启动该目录中的 sidecar 并验证 HTTP 响应、缓存头和 build id。随后它会临时修改并自动还原 Web 样式源文件，第二次构建后确认新的 build id、hash 文件替换和旧文件清理。

常见问题：端口被占用时更换端口后保存重启；缺少 ASP.NET Core Runtime 10 (x64) 时按后台提示安装；Web 页面无法更新时确认活动包已保存并刷新页面。禁用或移除该插件后，主程序与 WPF 前台继续按原有方式运行，不依赖 sidecar 或 Node.js。

真实 IPC 与布局验收使用：

```powershell
.\tools\Test-WebRendererIpc.ps1 -Configuration Debug
```

该脚本发布并启动真实主程序，要求 sidecar 到达 `Ready`，验证 `BpWindow` 的真实 bootstrap 与 headless Edge 截图，并输出完整握手日志、health、windows 和 bootstrap 摘要。
