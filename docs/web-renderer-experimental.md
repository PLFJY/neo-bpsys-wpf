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

默认监听 localhost。实验性 LAN 模式可以使用 `--web-host 0.0.0.0`，此模式没有访问认证，同一网络中的设备可读取页面与实时展示数据；请只在受信任网络使用，并通过系统防火墙限制端口。sidecar 提供 `/`、`/health`、`/render/{encodedFullWindowType}`、`/api/windows`、`/api/bootstrap/{encodedFullWindowType}`、`/assets/{resourceToken}` 和 `/ws`。

资源 URL 是每次 bootstrap 创建的随机 token；浏览器不会获得物理路径。插件只授权当前活动包、`local`、内置 `Resources/...` 和已知应用字体，拒绝绝对路径、跨包引用及编码路径穿越。切换包或 Designer 保存布局时会重新发送 bootstrap，页面通过 WebSocket 刷新。未知内置控件与没有 Web adapter 的 `plugin:*` 控件会显示诊断占位；纯 Binding 文本显示绑定路径占位。

IPC 使用 version 4，并在 `bootstrap.replace` 中传输布局和资源表。WebSocket 首次连接会收到完整 `snapshot`，后续仅发送带 sequence 的 `bindingPatch`；布局变更会通知页面重取 bootstrap 并重新同步。只会解析当前布局实际使用且由设计器绑定目录声明的路径；未知成员、方法调用、越界索引和无法转换的对象返回 null 与诊断，不会序列化整个共享对象图。第三方 Web 控件目前只预留注册边界，不会执行或托管任意插件脚本。

行为文档随 bootstrap 一并从活动包加载。Web 页面只消费 `IFrontedEventBus` 桥接出的语义事件，在本页独立执行 OneShot、Loop 和 Transition 节点图；断线、刷新、包切换和布局重载都会取消页面的 delay 与动画。Transition 由插件装饰原始 WPF 编排器：WPF 与 Web 先各自运行 ExitGraph，浏览器确认后才允许原始 C# `commitAsync` 执行一次；commit 后两端并行运行 EnterGraph。浏览器从不拥有业务提交能力，Web 未连接、断线、异常或超过默认 2000ms 的可配置等待上限均 fail-open。

Web 动画支持全部当前公开属性：Opacity、Visibility、VisualOffsetX/Y、ClipInset、Scale、Rotation、Width、Height、Fill/Stroke/Text/Foreground 色彩、StrokeThickness、FontSize、TintColor、TintStrength、TextureStrength 与 GaussianBlurRadius。基值仅在当前页面 Runtime session 中捕获，Reset 和 Reset All 会取消对应动画并恢复捕获状态。

## 管理与排查

插件启用并重启应用后会在后台导航中出现 **Web Renderer** 实验页面。页面可以启动、停止和重启 sidecar，显示本机/局域网 URL、连接客户端、当前活动包、公开窗口和最近错误，也可以复制或在默认浏览器打开本机 URL。Host、Port、随应用启动、Transition fail-open timeout 与协议摘要日志保存在插件自己的 `PluginConfigs/top.plfjy.bpsys.WebRenderer/Settings.json`，不会修改主程序 `Config.json`。保存配置会重启 sidecar。

命令行参数优先于插件设置：`--web-host`（或 `--host`）、`--web-port`（或 `--port`）、`--web-no-start` 与 `--web-log-protocol`。协议日志只记录连接和消息摘要，不记录动画逐帧数据。

OBS 可添加 Browser Source 并填入本机 URL 或指定窗口的 `/render/{encodedFullWindowType}` URL；浏览器字体栅格化与 WPF 可能略有不同，但布局坐标、资源和行为语义保持一致。`plugin:*` 控件只有提供受控 Web adapter 才能渲染，否则显示诊断占位。

常见问题：端口被占用时更换端口后保存重启；缺少 ASP.NET Core Runtime 10 (x64) 时按后台提示安装；Web 页面无法更新时确认活动包已保存并刷新页面。禁用或移除该插件后，主程序与 WPF 前台继续按原有方式运行，不依赖 sidecar 或 Node.js。
