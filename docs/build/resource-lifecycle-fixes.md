# 内存保留与资源生命周期修复报告

基于远端最新 `dev`：`7f5dcf118714c3bfd51d295f918687130c63ad53`。工作区已获用户授权从 `266fd4c7` 快进到该提交；未执行 stash、restore、reset、clean、分支切换或提交。

## 修复范围

| 文件（同目录下以短文件名列出） | 修复内容 |
| --- | --- |
| `neo-bpsys-wpf/Views/Windows/FrontedDesignerWindow.xaml.cs`、`FrontedDesignerWindow.Preview.cs`、`FrontedDesignerWindow.Catalog.cs`；`ViewModels/Windows/FrontedDesignerWindowViewModel.Behaviors.cs` | 真正关闭时取消布局加载、待执行预览及选择器操作，停止既有计时器，释放动画会话并清空单例预览作用域。普通重绘保持原有流程。 |
| `neo-bpsys-wpf/Views/FrontedDesigner/GraphEditor/FrontedNodeGraphEditorView.xaml.cs`、`Views/FrontedDesigner/BehaviorPanelView.xaml.cs`；`ViewModels/FrontedDesigner/FrontedBehaviorAnimationEditorViewModel.cs` | 节点图属性事件改用 WPF WeakEventManager；行为面板幂等处理 Loaded/Unloaded 订阅；关闭动画编辑器时取消单次及循环预览，令牌由正在运行的任务释放。 |
| `neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedBehaviorRuntimeHostManager.cs`、`FrontedBehaviorRuntimeHost.cs`、`FrontedAnimationRuntime.cs`；`neo-bpsys-wpf.Core/Controls/FrontedWindowBase.cs` | 同 Window ID 的附加/分离协调及请求身份校验；取消、异常及 Dispose 后不能重新注册旧宿主；不同窗口的文档加载并发。关闭/隐藏取消未完成 Attach，关闭取消布局等待；分离异常可观察。宿主退出解订阅并取消所有图阶段，防止循环 finally 或已取消的动画动作重新创建 session。 |
| `neo-bpsys-wpf.SmartBp.Module/ViewModels/SmartBpModuleContentViewModel.cs`、`.Recognition.cs`、`.Cuda.cs`；`SmartBpModuleEntryPoint.cs`；`PostGameRecognitionProgressSourceAdapter.cs` | 将外部匿名事件改为可解订阅的处理器；模块容器退出时幂等清理 ViewModel、三个计时器、进度适配器及调试窗口；防止延迟状态通知继续更新退出中的 UI。普通页面卸载不释放模块单例。 |
| `neo-bpsys-wpf/Services/SmartBpModule/SmartBpModuleManager.cs`、`.Loading.cs` | 禁止并发加载、成功后重复加载及创建不可卸载 ALC 后的重复尝试；清理失败和退出路径上的模块容器。初始化失败后更换模块仍需重启，目录校验失败可以修正后重试。 |
| `neo-bpsys-wpf.SmartBp.Module/Services/OcrService.cs`、`PaddleOcrProvider.ModelManagement.cs`、`.Loading.cs`、`.Recovery.cs`、`RapidOcrNetProvider.cs`、`Services/Recognition/RapidOcrModelServices.cs` | 释放模块拥有的 Paddle predictor；阻止退出后的后台模型加载/恢复重新创建实例。RapidOCR 释放幂等，模型重建失败不再丢失未释放的旧实例；资产下载令牌由安装任务 finally 释放，目录清理异常也不能跳过。停止自动识别不释放 OCR Provider。 |
| `neo-bpsys-wpf.SmartBp.Module/Services/Recognition/SmartBpRegionSnapshotServices.cs`、`SmartBpAutoRecognitionCoordinator.cs` | 历史帧增加 192 MiB 估算像素预算，涵盖裁剪图持有的源位图；超预算淘汰最旧帧，允许保留单张超大当前帧。停止、完成、换源及模块退出时清空；协调取消与缓冲写入，保留时间和对局上下文过滤。 |
| `neo-bpsys-wpf/Services/WindowCaptureService.cs`、`.Wgc.cs`、`.Bitblt.cs` | 停止时在帧锁下清空缓存，使用会话身份/世代阻止旧回调写回；捕获目标 Closed 后在捕获所属 Dispatcher 停止会话。协调 WGC staging 资源释放，帧池销毁不持有回调资源锁。服务退出释放复用设备，设备创建失败释放半初始化资源。 |
| `Built-inPlugins/neo-bpsys-wpf.WebRenderer/Services/WebTransitionGateway.cs` | 保存 CancellationTokenRegistration，完成/取消时非阻塞注销；覆盖立即取消、注册返回前结束及回调重入。超时仍由调用方执行原有 Cancel/fail-open 策略，IPC 不变。 |
| `neo-bpsys-wpf.Tests/Services/FrontedBehaviorRuntimeHostTest.cs` | 仅新增一个永久回归用例，使用可控文档加载验证并发附加只触发一次行为，以及 Detach/Dispose 后不会复活。它保护编译器无法发现的事件重复执行和永久根引用风险。 |

同步更新了 `docs/architecture/threading-dispatcher-and-async.md`、`docs/business/smartbp-and-ocr.md`、`docs/frontend/fronted-designer-editor.md`。

## 问题分类

**确认存在的保留路径**：单例预览作用域持有关闭窗口的 Canvas；并发 Attach 覆盖未 Dispose 的宿主后，总线继续持有旧宿主；循环 finally 在宿主释放后 Reset 重建动画 session；已完成 Web 会话被长生命周期取消令牌继续持有；模块容器缺少退出清理，外部事件和 DispatcherTimer 无释放闭环；Paddle predictor 缺少所属容器退出入口；RapidOCR 初始化失败覆盖旧实例引用而未释放。

**风险与有界保留**：节点图/行为面板的 ViewModel 比 View 活得更久时，普通强事件可能保留旧 View；窗口隐藏/关闭与 Attach 交错可能留下 Host；WGC/BitBlt 停止与回调交错可能写回旧帧。这些属于条件性竞态，已补足对应保护。256 帧缓冲和 StopCapture 后的一张残留图像是有界保留，不称为无限增长泄漏；分别增加预算和及时清空。

**刻意保留并保持原状**：不可卸载的模块 ALC、模块的正常进程级单例、原生预加载句柄和 DLL 搜索目录；捕获设备的跨会话复用；FrontedResourceResolver 的 256 项/128 MiB LRU；BackgroundImageTintProcessor 的 32 项/64 MiB LRU；前台事件调试器默认 300 条记录。检查确认图片缓存会移除旧引用，调试服务有 Dispose，Web BindingPathObserver 重建及退出会清理订阅，FrontedBindingFactory 使用 WPF 弱事件，ModernFrame 会移除动画并清空旧 presenter，ProductTour 使用受控取消注册及会话结束清理。GameStopwatch 为进程级秒表，Pause/Reset 停止计时器；FocusKeeper 已有退出卸载 Hook 和 NativeLibrary 的配对路径，未改动。

## 验证

构建和最终测试结果见下方更新记录。测试使用既有 xUnit v3 可执行 runner；当前 .NET 10 下直接 `dotnet test` 的旧 VSTest 入口被测试平台拒绝，未因此更改工程门禁。ARM64 runner 的 x64 原生依赖/Host 加载失败，改用本机既有 x64 .NET runtime 后通过。

已实际运行并删除临时 `LifecycleTemporaryVerification.cs`：6 个用例全部通过，覆盖预算驱逐/超大当前帧/尺寸变化/Reset、窗口 Hide 与 Close 期间完成 Attach、隐藏后重新附加、节点图三次 Unloaded/Loaded、重复 Dispose 后循环不得 Reset、Web 已完成及预先取消会话。模块归档测试中的临时错误诊断已恢复，没有保留新增验证工程或脚手架。

尚未执行真实赛事窗口的 WGC/BitBlt 捕获压力测试、完整 Designer 连续开关的堆快照比较、Paddle/CUDA 模型推理及原生 OCR 独立探针；不能据这些测试宣称实际内存分析已通过。帧预算是保守像素估算，并非整个进程的总内存上限。

公共插件 API 签名、布局/行为 JSON、WebRenderer IPC 和超时 fail-open 策略未变。可观察变化限于生命周期修复：过期 Attach 失效；已创建不可卸载 ALC 的失败尝试需要重启；内存压力下历史帧数量减少，当前帧及已有补偿识别逻辑保留。

## 最终验证记录

- `dotnet build neo-bpsys-wpf.Tests/neo-bpsys-wpf.Tests.csproj --no-restore -p:WebRendererSkipWebBuild=true -v:quiet`：成功，0 error；最后一次增量构建有 396 个 warning，未扩大范围清理。该项目依赖构建覆盖主程序、Core、SmartBP Module 和 WebRenderer C# 项目；未执行未改动的 Web 客户端构建。
- 使用 `C:/Program Files/dotnet/x64/dotnet.exe` 运行既有测试程序集，按本次修改选择行为/动画运行时、Designer、前台窗口、动态绑定、资源缓存、SmartBP 协调/帧回溯、模块归档及依赖解析、RapidOCR/Tesseract 资产、Web 转场、ModernFrame 和教程协调测试：307 项，306 通过、0 失败、1 跳过。跳过的是需要 `RUN_SMARTBP_NATIVE_TESTS=1` 和真实模块目录的原生 OCR 独立探针。
- 临时 6 个生命周期验证先运行通过，再删除源文件；最终编译后的测试程序集已确认不再包含临时测试类。
- `git diff --check`：通过。收尾再次读取远端 dev，仍为 `7f5dcf11`。
