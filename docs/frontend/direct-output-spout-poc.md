# Direct Output / Spout2 实验验证

2026-10-16，在 `dev` 上完成最小实现和 Windows 实机验证。**真实 WPF v3 布局已持续送入 OBS**；当前环境没有达到 1920×1080 / 30 FPS，不能据此认定为生产可用方案。默认仍使用已有 Window Capture。

## 入口与文件

前台管理 → 前台窗口 → `Direct Output（实验性）`，选择一个 v3 窗口，开始/停止输出。显示 Sender 名称、运行状态、分辨率、实际 FPS、平均 WPF 生成耗时、全帧耗时及异常信息。只运行一个 Sender；不保存输出开关。

| 文件 | 职责 |
| --- | --- |
| `neo-bpsys-wpf/Services/DirectOutput/FrontedOffscreenRenderHost.cs` | 不可见 PresentationSource、持久 Canvas/Viewbox、布局与透明位图、真实行为附加/分离 |
| `neo-bpsys-wpf/Services/DirectOutput/SpoutFrameSender.cs` | 隔离 Spout2.NET Alpha API、D3D11 纹理、alpha 转换、上传、释放 |
| `neo-bpsys-wpf/Services/DirectOutput/FrontedDirectOutputService.cs` | 单输出生命周期、30 FPS 调度、BO 重载、包切换、统计及错误 |
| `App.Services.xaml.cs`、`ViewModels/Pages/FrontManagePageViewModel.cs` | 按需解析独立单例，不改变已有构造函数 |
| `Views/Pages/FrontManage/FrontedWindowsView.xaml`、`Locales/FrontManage*.resx` | 最小实验卡片及中英日文本 |
| `neo-bpsys-wpf.csproj` | 固定 `Spout2.NET 0.1.0-alpha` 与 `Vortice.Direct3D11 3.8.3` |

没有修改 Core/PluginSdk 公开接口、旧窗口显示/隐藏、Designer v3 schema 或 `.bpui` 格式。

## 开发者运行与 OBS

固定的 Alpha 包只携带 Windows x64 native shim，必须以 **x64 进程**运行。ARM64 原生进程会报告明确错误；没有静默回退 WARP、窗口截图或其他输出路线。可用现有 x64 构建流程，或：

```powershell
dotnet publish .\neo-bpsys-wpf\neo-bpsys-wpf.csproj -c Debug -r win-x64 --self-contained false -p:WebRendererSkipWebBuild=true
```

该命令显式跳过与本 PoC 无关的可选 WebRenderer Web 构建，不是正式发布验证命令。

1. 启动应用，在实验卡片选择 `BpWindow` 并开始。不需要打开实际 BP 前台窗口。
2. 使用已经安装 [obs-spout2-plugin](https://github.com/Off-World-Live/obs-spout2-plugin) 的 OBS，在来源中添加 **Spout2 Capture**。
3. Sender 选择 **`neo-bpsys-wpf/BpWindow`**；其他 v3 窗口使用 `neo-bpsys-wpf/{CanonicalId}`。
4. **Composite mode 选择 `Default`**：发送的是 straight BGRA。不要选择 Opaque 或 Premultiplied Alpha。下方放一个颜色源即可检查透明区域。
5. OBS 与 Sender 应运行在同一显卡上。停止输出后 Sender 从注册表移除；再次开始仍用相同名称。

实现没有安装 OBS 插件，也没有把 OBS SDK/插件编入应用，没有 HTTP/WebSocket/浏览器中间层。[Spout2.NET 固定版本](https://github.com/Agash/Spout2.NET/tree/v0.1.0-alpha) 支持 .NET 10 且无额外 NuGet 依赖；后续 Alpha 包引入了 .NET 11 预发行日志依赖，因此没有跟随主线升级整个应用。当前适配已通过接收验证，尚无证据要求改用另一套原生封装。

## WPF 与生命周期

输出像素尺寸取 `WindowSettings.WindowWidth/WindowHeight`（向上取整），96 DPI；Canvas 保留 `CanvasSettings` 设计尺寸。外层背景与 `Viewbox.Stretch` 取窗口设置，Viewbox 负责缩放/居中，根容器裁剪到输出边界。位图始终保留 alpha；文字使用灰度抗锯齿以避免 ClearType 的不透明背景要求。

每帧只做 `Measure/Arrange/UpdateLayout`、清除并复用 `RenderTargetBitmap`、`CopyPixels`；不重建控件。开始、BO 状态切换及活动包切换才重新加载。活动包切换会重建此单输出会话以更新尺寸、资源和行为；若窗口注册消失则停止并提示。设计器单独保存布局后的即时重载尚未接入，需停止/再次开始。

**当前实现必须借助 HWND。** 纯 Visual 对照测试在同一个有消息泵的 Dispatcher 上进行：即使定时 `RenderTargetBitmap.Render`，`Loaded=false`，200 ms DoubleAnimation 在 1 秒期限内仍停在初值，Completed 未触发。连接不可见 HwndSource 后，`Loaded=true`，动画到终值并完成。实现使用 `WS_POPUP`，没有 `WS_VISIBLE`，另有 `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`；实际 `IsWindowVisible` 检查为 false。不创建 Window，也不读取桌面/隐藏窗口像素。

真实 `ISharedDataService`、renderer、资源 resolver、控件工厂和 Behavior Runtime 都被复用，`IsDesignerPreview` 保持 false。行为实例 ID 使用 `direct-output:{CanonicalId}`，布局类型仍为 Canonical ID，以便与可见窗口同时存在而不互相 detach。沿用 CanvasLoaded，并在输出开始/停止发布对应实例的 WindowShown/WindowHidden；共享事件与真实选角 TransitionOrchestrator 仍走原链路。定向到旧窗口实例 ID 的外部手动事件不会自动复制给输出实例。

WPF 控件留在主 Dispatcher，避免跨线程访问现有业务控件。PeriodicTimer 控制目标帧率，串行等待一次 Dispatcher 渲染再上传，错过的 tick 合并；不会累积未完成渲染。CPU alpha 转换、D3D 上传和 Spout 调用在工作任务执行。停止取消调度，等待在途帧，分离行为/BO 订阅并释放资源；应用退出不会同步等待一个正在等待 Dispatcher 的任务。

## 实际验证

环境：Windows 11 ARM64 虚拟机，6 vCPU，Parallels Display Adapter (WDDM)，驱动 `20.18.2703.58680`，.NET 10.0.12；Sender/OBS 均运行于 x64 仿真。用户提供的 OBS 目录名称带 arm64，但 PE 实际为 x64，日志版本为 OBS 32.1.2。已有 x64 win-spout 插件成功加载。

| 项目 | 结果 |
| --- | --- |
| 静态 BpWindow | 53 个真实 v3 控件，无初始化错误；背景、文字、图片及 alpha 像素均已实际渲染 |
| 尺寸/缩放 | 1440×810 Canvas 输出为 1920×1080；另以非同比尺寸验证 Uniform 留边、Fill 拉伸及半透明红色 |
| 真实绑定 | SharedDataService 队名、Logo 更新改变帧内容，原控件对象保持不变 |
| BO3/BO5 | 在内存测试布局显式加入 BO3 state，真实 IsBo3Mode 切换后通过服务重载并显示/移除该 state；没有改写持久化布局。内置 BpWindow 默认没有 BO state |
| Spout | Sender 可枚举；接收纹理为 BGRA8 1920×1080，Row Pitch 7680；GPU staging 读回与上传字节完全一致 |
| 跨进程接收 | 两次独立接收进程会话，收到实际不同图像；退出和重连不影响 Sender |
| 现有行为 | 真实 CharacterSelectionService 执行内置 SurPick 过渡图，完整完成业务提交和动画；帧采样具有多个不同中间画面 |
| detach/重启 | 三轮输出开始/停止，行为宿主移除、Sender 注册消失，重新开始仍可发送；另验证包切换替换输出根 Canvas |
| OBS | OBS 中的 `spout_capture` 实际连接指定 Sender；OBS 自己渲染导出的 12 帧成功解码，包含 6 种不同画面；透明中心像素正确合成到下方纯绿颜色源，队名与选角动画可见 |
| OBS/输出重启 | Sender 停止/开始后 OBS 重连；结束并重新启动独立 OBS 进程后恢复接收，Sender 应用未崩溃 |

OBS 验证使用已有 OBS 程序和已安装插件的临时便携副本，以及 OBS 内置 Lua 接口；不是屏幕截图，也不是模拟接收器。computer-use 本机连接不可用，因此未执行人工式点击测试。便携配置、Lua、输出帧均仅用于本次验证，未改用户原有 OBS 场景。重启测试结束的是自己创建的隔离进程，用户打开的 OBS 保持运行。临时目录删除受到执行环境审批策略阻止，详见文末清理状态。

## 实测性能与复制

稳态三次约一秒统计窗口，1920×1080，目标 30 FPS：

| 实际 FPS | 平均 WPF 生成 | 平均完整帧 |
| --- | --- | --- |
| 16.5 | 27.92 ms | 60.61 ms |
| 22.2 | 27.50 ms | 45.06 ms |
| 21.8 | 27.44 ms | 45.78 ms |

同一稳态采样段，发送进程 CPU 为 **1.656 核，约占 6 vCPU 的 27.60%**；主 Dispatcher 的 16 ms 心跳最大间隔 **53.0 ms**。最后一次独立 OBS 接收会话统计为 **22.0 FPS / WPF 27.83 ms / 完整帧 45.40 ms**。这里 FPS 统计成功完成的 Send 调用；固定 Alpha API 未暴露原生 mutex 争用丢帧计数，不能当作 OBS 展示帧率。完整帧包含 Dispatcher 等待、WPF 生成、CPU 转换、上传及 SDK 调用，不能把它解释成纯 GPU 耗时。GPU 占用没有可靠测量值；没有拿虚拟机结果外推实体显卡性能。

确定发生的复制链路：WPF Visual → RenderTargetBitmap(Pbgra32) → `CopyPixels` 到复用 CPU 缓冲 → 原地取消 alpha 预乘 → `UpdateSubresource` CPU→D3D11 纹理 → 原生 SDK `SendTexture` 的 GPU `CopyResource` 到共享纹理 → OBS 读取。WPF 内部渲染/可能读回成本包含在 WPF 统计内，未单独计量。**这条链路不是零拷贝**。

原生 [Spout2 SDK](https://github.com/leadedge/Spout2) 的 SendTexture 用共享纹理访问 mutex 与 D3D Flush 协调；本适配不绕过 SDK 直接写共享资源，也不把 Flush 声称为 CPU 等待 GPU 完成的 fence。单设备命令串行、输入纹理持续存活；跨进程读回及 OBS 合成均已实际验证。

## 验证边界与后续

尚未完成：实体显卡上的 1080p30、长时间稳定性与 GPU/内存泄漏分析、多 GPU/设备丢失恢复、全量插件控件、人工操作主窗口的体验验证、ARM64 原生 Sender、所有行为类型/长期循环以及正式发布打包。

当前主 Dispatcher 每帧仍花约 27 ms，已经可能影响交互；该 PoC 未证明“生产级主窗口无卡顿”。正式化需要先评估 WPF 渲染与读回成本、减少复制或设计有明确共享数据线程边界的专用渲染 Dispatcher，不能直接把现有控件搬到后台线程。还需要完善设备诊断、用户取消与包切换并发协调、设计器保存的即时重载、性能预算和长时间资源验证。固定 Alpha 包和原生依赖也需重新审查后再决定正式适配层。

构建使用 `dotnet build neo-bpsys-wpf/neo-bpsys-wpf.csproj -m:1 -p:WebRendererSkipWebBuild=true`。本仓库当前 .NET 10 `dotnet test` 的 VSTest 路径会报告 Microsoft.Testing.Platform 不支持，因此实际通过已构建测试 DLL 的 xUnit v3 独立 runner 执行，没有修改测试基础设施。

定向回归实际执行 **78 项已有测试 + 7 项临时验证，85 项通过、0 失败**；另有两次由父验证启动的独立接收进程用例通过。临时接收进程入口标为 Explicit，不在主 runner 中重复执行，报告中的 1 项 Not Run 对应这个入口，不是跳过失败门禁。已有覆盖包括 renderer 身份、动画 runtime、行为 host/loop、选角 transition、窗口 Canonical ID 与透明设置重启。

临时测试使用现有 `WpfTestThread`，没有新增测试工程。已经删除仓库中的临时测试源文件并重新构建测试程序集，辅助 OBS/接收器进程也已结束。完整测试集/正式发布未执行，不以它们作为本次通过依据。

**清理未完成项：** 执行环境自动审批拒绝删除本次创建的 `C:\Users\plfjy\AppData\Local\Temp\neo-direct-output-research`，包括已校验绝对路径的目录删除和逐文件清理方案，返回 `blocked by policy`，没有提供进一步理由。因此临时便携 OBS 副本、Lua、截图、下载的调研文件和日志仍在该目录；它们没有加入仓库，也不属于交付的永久测试资产。当前会话无法提升审批权限，不能报告为已清理。
