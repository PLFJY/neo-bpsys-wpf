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

默认监听 localhost。实验性 LAN 模式可以使用 `--web-host 0.0.0.0`，此模式没有访问认证，同一网络中的设备可读取页面与实时展示数据；请只在受信任网络使用，并通过系统防火墙限制端口。sidecar 提供 `/`、`/health`、`/render/{encodedFullWindowType}`、`/api/windows`、`/api/bootstrap/{encodedFullWindowType}`、`/bpui-assets/{resourceToken}`、`/remote-assets/{token}` 和 `/ws`。`/assets/*.js` 与 `/assets/*.css` 仅用于 Vite 生成的 Web 客户端静态文件；`.bpui` 图片、字体等授权资源仅使用 `/bpui-assets/{resourceToken}`，在线图片仅使用 `/remote-assets/{token}`，这些 URL 前缀互不复用。

资源 URL 是每次 bootstrap 创建的随机 token；浏览器不会获得物理路径。插件只授权当前活动包、`local`、内置 `Resources/...` 和已知应用字体，拒绝绝对路径、跨包引用及编码路径穿越。切换包或 Designer 保存布局时会重新发送 bootstrap，页面通过 WebSocket 刷新。未知内置控件与没有 Web adapter 的 `plugin:*` 控件会显示诊断占位；纯 Binding 文本显示绑定路径占位。

每次 Web client 构建都会写入 client build id（提交标识与构建时间）。该值同时出现在最终 `index.html` 的 meta 标签、浏览器启动日志和 `/health` 的 `clientBuildId`。sidecar 启动前会验证最终 `Host/wwwroot/index.html`、build id 以及它引用的每一个本地 script/link 文件；任何缺失或越界引用都会让 sidecar 以明确错误退出，而不会在运行期间持续请求不存在的 hash 文件。

`/`、`/render/*` 和 `/index.html` 始终返回 `Cache-Control: no-store`，确保入口页不会缓存旧 bundle 引用。带内容 hash 的 `/assets/*.js` 与 `/assets/*.css` 使用 immutable 缓存策略。

IPC 使用 version 7，runtime value schema 使用 version 2，并采用显式会话状态：`Stopped`、`StartingProcess`、`WaitingForPipe`、`PipeConnected`、`BuildingBootstrap`、`WaitingForBootstrapAck`、`Ready`、`Stopping`、`Faulted`。主程序是状态权威；sidecar 必须先发送 `sidecar.ready`，主程序才发送 `host.hello` 和真实活动包的 `bootstrap.replace`。sidecar 会原子校验协议版本、generation、窗口结构与本地资源表，成功后返回 `bootstrap.applied`；只有该确认到达，管理页、`/health` 与 `/api/windows` 才会显示 `Ready` 和已发布窗口。连接断开时 sidecar 保持 HTTP 进程并以封顶退避重连，重连后的第一组消息始终是完整 bootstrap 和 runtime snapshot。

`/api/windows` 和 `/api/bootstrap/{encodedFullWindowType}` 在 IPC 未连接时返回 `503 IpcUnavailable`，bootstrap 尚未确认时返回 `503 BootstrapPending`，构建或校验失败时返回结构化 `503` 错误。`/render/{...}` 在等待状态显示“正在等待主程序布局数据”；仅 Ready 后才区分 `UnknownWindow` 与 `LayoutUnavailable`。管理页仅把 sidecar 已确认的窗口作为可用 Web Runtime，不会用 registry 候选窗口伪装布局已发布。

Image 与 BorderedImage 按 WPF 的语义树分离外层布局、内容 viewport、Lock/PickingBorder overlay 和行为生成部件。动态图片值明确区分 `resolved`、`pending`、`null`、`failed`；runtime asset 同时携带自然 DIP、像素尺寸与 DPI。浏览器只在新图片 decode 成功后原子切换，pending/failed 保留同 generation 的上一稳定图片，业务 null 则清空。

## 在线队伍图片

队伍 JSON 中的 HTTP/HTTPS Logo 与选手定妆照保留为 URI。WPF 继续通过 `BitmapImage.UriSource` 加载；主程序只发布规范化 URI 的不可变描述符，不下载图片、不读取图片字节，也不在 UI 线程等待网络。真实链路为：

```text
Team/Member JSON ImageUri
→ BitmapImage.UriSource
→ CurrentGame.SurTeam.Logo / CurrentGame.HunTeam.Logo
  或 CurrentGame.SurPlayerList[0..3].PictureShown / CurrentGame.HunPlayer.PictureShown
→ BindingPathObserver
→ WebRuntimeValueFactory
→ WebRuntimeAssetRegistry
→ remoteAsset.fetch IPC
→ sidecar RemoteAssetFetcher/cache
→ remoteAsset.resolved/failed IPC
→ runtime snapshot/bindingPatch IPC
→ RuntimeStore
→ ImageRenderer
→ DynamicImage
```

没有选择角色时，`PictureShown` 使用 `Member.Image`；选择角色后使用 `Character.HalfImage`；清除角色后重新使用当前 Member 的在线定妆照。两个队伍 Logo、四名求生者与一名监管者都走同一套绑定和资源链路，不含控件特判。

sidecar 独立异步下载在线图片，并只允许 HTTP/HTTPS 的 PNG、JPEG、WebP 和 GIF。下载使用操作系统默认代理和路由设置，因此浏览器、WPF 与 Clash TUN/Fake-IP 模式共用同一网络路径；sidecar 不强制直连，也不要求额外的受信任主机配置。每次下载最多跟随 5 次重定向，连接超时 5 秒、总请求超时 20 秒、单项上限 10 MiB；相同资源的并发请求合并。成功结果进入 64 MiB 内存 LRU 与 512 MiB、最长 7 天的磁盘缓存，完整写入后才通过 `/remote-assets/{token}` 提供。失败不会持久缓存，绑定仍存在时会退避重试；URL 或 generation 改变时旧任务结果会被丢弃。

每次请求和重定向都会重新执行基本地址校验：拒绝 userinfo、非 HTTP/HTTPS、localhost、环回、链路本地、组播、未指定和 RFC1918 私有地址。Clash TUN 常用的 `198.18.0.0/15` 仅作为 Fake-IP 路由地址，允许交由操作系统代理/路由处理，不将其当作用户可访问的局域网图片服务器。日志只记录稳定诊断码、generation 和截短 token，不记录源 URL、查询参数或完整缓存路径。

## 字体分类

普通字体名称（例如 Arial、Segoe UI、Microsoft YaHei、Times New Roman、sans-serif、serif、monospace）直接交给浏览器解析，不进入 bootstrap resources、不生成 `@font-face`、不发起字体资源请求，也不会映射为 Noto Sans。只有 `pack://...#Family`、`bpui://...#Family` 和明确指向 `.ttf`、`.otf`、`.woff`、`.woff2` 的 `Resources/...` 引用才作为嵌入或包字体处理。

Web 文本将 WPF FontWeight 名称集中转换为 CSS 数值：Thin 100；ExtraLight/UltraLight 200；Light 300；Normal/Regular 400；Medium 500；DemiBold/SemiBold 600；Bold 700；ExtraBold/UltraBold 800；Black/Heavy 900；ExtraBlack 950。未知或空值不直接写入 CSS，由浏览器继承。

`.bpui v3` 的 Rectangle、Border、Image AnimationParts 会进入所属控件的局部 Above/Below overlay。Web 动画使用带单位的长度值，百分比 ClipInset 直接生成 `clip-path: inset(...)`，Transform 分量由同一元素状态合成。Transition 的 `transition.committed` 携带 required generation/sequence；浏览器应用到该 runtime sequence 后才启动 EnterGraph，超时、断线或 generation 变化时 fail-open，WPF 始终是唯一业务 commit 所有者。

这些能力仍属于实验性 Web Renderer。当前阶段不实现 BackgroundTint，也没有重写 Runtime Publisher 的完整线程模型；图片编码、runtime patch、IPC 和 Web 动画保持异步，WPF Renderer 的既有语义不变。

## 管理与排查

插件启用并重启应用后会在后台导航中出现 **Web Renderer** 实验页面。页面可以启动、停止和重启 sidecar，显示与 `/health` 一致的生命周期状态、本机/局域网 URL、连接客户端、已确认活动包、已确认窗口和最近错误，也可以复制或在默认浏览器打开本机 URL。Host、Port、随应用启动、Transition fail-open timeout 与协议摘要日志保存在插件自己的 `PluginConfigs/top.plfjy.bpsys.WebRenderer/Settings.json`，不会修改主程序 `Config.json`。保存配置会重启 sidecar。

命令行参数优先于插件设置：`--web-host`（或 `--host`）、`--web-port`（或 `--port`）、`--web-no-start` 与 `--web-log-protocol`。协议日志只记录连接和消息摘要，不记录动画逐帧数据。

OBS 可添加 Browser Source 并填入本机 URL 或指定窗口的 `/render/{encodedFullWindowType}` URL；浏览器字体栅格化与 WPF 可能略有不同。`plugin:*` 控件只有提供受控 Web adapter 才能渲染，否则显示诊断占位。

## 部署验收

从仓库根目录运行以下命令可验证从干净构建到最终主程序插件目录的完整静态产物链路：

```powershell
.\tools\Test-WebRendererDeployment.ps1 -Configuration Release
```

脚本会在 `build/web-renderer-deployment-validation/app/Plugins/top.plfjy.bpsys.WebRenderer/Host/wwwroot/index.html` 检查最终入口页与所有引用资源，启动该目录中的 sidecar 并验证 HTTP 响应、缓存头和 build id。随后它会临时修改并自动还原 Web 样式源文件，第二次构建后确认新的 build id、hash 文件替换和旧文件清理。

常见问题：端口被占用时更换端口后保存重启；缺少 ASP.NET Core Runtime 10 (x64) 时按后台提示安装；Web 页面无法更新时确认活动包已保存并刷新页面。禁用或移除该插件后，主程序与 WPF 前台继续按原有方式运行，不依赖 sidecar 或 Node.js。

停止、重启和保存后重启是互斥操作。停止会先请求 sidecar 关闭、关闭 IPC 会话并等待进程退出；超过关闭时限才终止当前会话所启动的 sidecar 进程树。sidecar 会优先加入 Windows Job Object；parent PID 及其启动时间监视是 Job 不可用时的兜底。管理页在操作期间禁用相关命令，并在成功、失败、取消或超时后恢复可操作状态。

真实 IPC 与布局验收使用：

```powershell
.\tools\Test-WebRendererIpc.ps1 -Configuration Debug
```

该脚本发布并启动真实主程序，要求 sidecar 到达 `Ready`，验证 `BpWindow` 的真实 bootstrap、AnimationParts/overlay DOM 身份与 headless Edge 截图，并输出 health、windows、bootstrap 摘要、浏览器控制台和 DOM 证据。
