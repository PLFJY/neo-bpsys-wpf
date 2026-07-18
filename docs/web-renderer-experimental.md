# 实验性 Web Renderer

Web Renderer 是独立内置插件 `top.plfjy.bpsys.WebRenderer`，用于验证由 Kestrel 提供的本地 Web 状态页。它不是现有 WPF 前台窗口的替代品，也不会读取 `.bpui`、执行实时 Binding 或运行前台行为动画。

插件本体不引用 ASP.NET Core；它会先检测 x64 的 `Microsoft.AspNetCore.App 10.*`，然后启动同目录 `Host` 下 framework-dependent 的 `net10.0` sidecar。缺少 runtime、进程启动错误、端口占用或 IPC 断开只会记录日志和显示非模态提示，不会中止主 WPF 应用。请安装 [ASP.NET Core Runtime 10 (x64)](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) 后点击“重新检测”。

默认地址为 `http://127.0.0.1:19527`，可使用：

```text
--web-host <IPv4>
--web-port <1-65535>
--web-no-start
```

`--web-host 0.0.0.0` 会监听所有 IPv4 网卡，供局域网访问；本实验阶段没有访问令牌，接入比赛数据前必须重新评估认证策略。sidecar 提供 `/`、`/health` 和 `/ws`，并通过每次启动生成的命名管道与插件交换带 `protocolVersion`、`sequence`、`type`、`payload` 的 JSON Lines 消息。主程序关闭或 IPC 管道断开时，sidecar 会退出。
