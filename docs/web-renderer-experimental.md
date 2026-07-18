# 实验性 Web Renderer

Web Renderer 是独立内置插件 `top.plfjy.bpsys.WebRenderer`。它会把当前活动 `.bpui v3` 包的 Window-centric 布局发送给 Kestrel sidecar，并在浏览器中还原固定设计画布。它不替代现有 WPF 前台窗口；WPF 与网页会并行运行，比赛状态仍只由 WPF 的 `ISharedDataService` 权威维护。

插件本体不引用 ASP.NET Core；它会先检测 x64 的 `Microsoft.AspNetCore.App 10.*`，然后启动同目录 `Host` 下 framework-dependent 的 `net10.0` sidecar。缺少 runtime、进程启动错误、端口占用或 IPC 断开只会记录日志和显示非模态提示，不会中止主 WPF 应用。请安装 [ASP.NET Core Runtime 10 (x64)](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) 后点击“重新检测”。

默认地址为 `http://127.0.0.1:19527`，可使用：

```text
--web-host <IPv4>
--web-port <1-65535>
--web-no-start
```

实时数据仅允许 localhost 监听，`--web-host 0.0.0.0` 和其他非 loopback 地址会被拒绝；局域网访问须在未来与鉴权一并启用。sidecar 提供 `/`、`/health`、`/render/{encodedFullWindowType}`、`/api/windows`、`/api/bootstrap/{encodedFullWindowType}`、`/assets/{resourceToken}` 和 `/ws`。

资源 URL 是每次 bootstrap 创建的随机 token；浏览器不会获得物理路径。插件只授权当前活动包、`local`、内置 `Resources/...` 和已知应用字体，拒绝绝对路径、跨包引用及编码路径穿越。切换包或 Designer 保存布局时会重新发送 bootstrap，页面通过 WebSocket 刷新。未知内置控件与没有 Web adapter 的 `plugin:*` 控件会显示诊断占位；纯 Binding 文本显示绑定路径占位。

IPC 使用 version 3，并在 `bootstrap.replace` 中传输布局和资源表。WebSocket 首次连接会收到完整 `snapshot`，后续仅发送带 sequence 的 `bindingPatch`；布局变更会通知页面重取 bootstrap 并重新同步。只会解析当前布局实际使用且由设计器绑定目录声明的路径；未知成员、方法调用、越界索引和无法转换的对象返回 null 与诊断，不会序列化整个共享对象图。第三方 Web 控件目前只预留注册边界，不会执行或托管任意插件脚本。
