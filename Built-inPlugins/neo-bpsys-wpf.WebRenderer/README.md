# neo-bpsys-wpf.WebRenderer

实验性内置插件 top.plfjy.bpsys.WebRenderer，将当前 v3 布局与数据提供给 OBS 浏览器源，与 WPF 输出并行。本文负责构建、运行与验收；协议、资产授权、图片分类、状态确认和安全边界以[内部架构](../../docs/plugin/web-renderer-experimental.md)为准。

## 构建

从仓库根目录运行，需要 .NET 10 SDK、Node.js 与 pnpm：

```powershell
dotnet build .\Built-inPlugins\neo-bpsys-wpf.WebRenderer\neo-bpsys-wpf.WebRenderer.csproj -c Release
.\build.ps1
```

插件项目构建 Host sidecar；Host 的 MSBuild target 执行 pnpm install --frozen-lockfile 与 pnpm run build，再把 Web/dist 复制为最终 Host/wwwroot。禁止 npm 和 package-lock.json。

普通 Debug 缺 pnpm 或 Web 构建失败会警告并省略整个插件；Release/Beta/Preview Build 与默认 Publish 缺完整产物会失败。显式 WebRendererSkipWebBuild=true 可用于精简本地调试；RequireWebRenderer=true 与跳过冲突。官方 build.ps1 强制完整产物并检查文件，不能用旧 wwwroot 冒充成功。详细规则见[构建发布](../../docs/build/build-release-and-versioning.md)。

最终插件目录包含托管 DLL、manifest 与 Host/ 下 sidecar、wwwroot/index.html 和全部引用的 hash 资源。sidecar 启动前验证 build id 和引用文件；只看到 wwwroot 目录不足以验收。

## 运行依赖与入口

WPF 主程序不引用 ASP.NET Core；framework-dependent sidecar 需要 x64 Microsoft.AspNetCore.App 10.*，运行时不需要 Node.js。缺依赖时管理页提供“下载并安装 / 重新检测 / 打开官方下载页”：从官方 release metadata 解析版本与 SHA-512，经统一下载服务下载、校验，用户同意 UAC 后安装；成功检测后提示重启主程序，由用户操作，不自动丢弃未保存状态。离线/检测失败保留 WPF 正常工作。

启用插件并重启后打开后台 Web Renderer 页，可启动/停止/重启、复制窗口地址、导出诊断。默认 http://127.0.0.1:19527；OBS Browser Source 使用管理页确认的 /render/{encodedFullWindowType} 地址。/health 与 /api/windows 可检查 Ready/错误，未确认 bootstrap 不等于已发布窗口。

## 配置与参数

配置保存于 PluginConfigs/top.plfjy.bpsys.WebRenderer/Settings.json：

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

默认不随主程序启动，旧用户保存的 true 保持有效。保存设置会重启 sidecar；命令行优先：

| 参数 | 范围 |
| --- | --- |
| --web-host / --host | IPv4；默认 127.0.0.1 |
| --web-port / --port | 1–65535；默认 19527 |
| --web-no-start | 禁止自动启动 |
| --web-log-protocol | 记录连接/消息摘要，不记录动画逐帧数据 |
| --web-transition-exit-timeout-ms | 1–30000 ms |
| --web-transition-enter-timeout-ms | 1–30000 ms |

LAN 的 --web-host 0.0.0.0 无访问认证，只用于受信任网络，并由系统防火墙限制端口。插件控件没有 Web adapter 时显示 unsupported；浏览器字体栅格化与 WPF 可能不同。WPF 是唯一业务 commit 所有者，Web transition 超时/断线 fail-open。

## 验收与排查

```powershell
.\tools\Test-WebRendererDeployment.ps1 -Configuration Release
.\tools\Test-WebRendererIpc.ps1 -Configuration Debug
```

Deployment 脚本验证最终发布目录的入口、资源、HTTP 缓存头与 build id，并临时修改/还原样式验证新 bundle 和旧资源清理；执行会产生构建和发布产物。IPC 脚本发布并启动真实主程序，等待 Ready，检查 BpWindow bootstrap、overlay DOM 与 headless Edge 截图。所需运行环境未具备时不能把未执行描述为通过。

端口占用先改端口并保存；bundle 不匹配核对 /health clientBuildId 与最终 index，不能只刷新浏览器掩盖构建缺件。完整消息状态、安全地址过滤与分进程内存诊断见[架构](../../docs/plugin/web-renderer-experimental.md)及[诊断手册](../../docs/build/memory-baseline-and-diagnostics.md)。
