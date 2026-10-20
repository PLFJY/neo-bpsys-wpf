# 内存基线与生命周期诊断

本文为按需诊断操作手册，不是未完成修复计划。区分对象持续保留、有界缓存、瞬时大分配与 sidecar 常驻；Working Set 高并不能单独证明泄漏。

## 可用诊断属性

以下 `internal` 属性仅供诊断和测试使用，不改变生产生命周期，不会反向持有任何缓存对象。在已有锁下读取，按需计算，不缓存计算结果。

### FrontedResourceResolver

- `CachedEntryCount`：当前图片缓存条目数（上限 `MaxCachedImages = 256`）。
- `EstimatedCachedBytes`：缓存中所有图片解码后的像素字节总和。

### BackgroundImageTintProcessor

- `CachedEntryCount`：当前染色缓存条目数（上限 `MaxCacheEntries = 32`）。
- `EstimatedCachedBytes`：缓存中所有染色位图的像素字节总和。

### WebRuntimeAssetRegistry

- `ReadyAssetCount`：已编码完成并可供 sidecar 使用的资源数量。
- `PendingAssetCount`：仍在后台编码或下载的资源数量。
- `FailureAssetCount`：编码或下载失败的资源数量。
- `RemoteAssetCount`：通过远程 HTTP/HTTPS 引用的资源数量。
- `ReferenceCount`：sidecar 缓存目录中仍被引用的 token 数量。

字节估算公式：`PixelWidth * PixelHeight * ceil(BitsPerPixel / 8)`，使用 `long` 避免溢出，无法识别格式时按 4 bytes/pixel 保守计算，`null` 条目按 0 计算。


## 采集准备

使用与问题相同的构建配置、架构、布局与插件。Preview 的 Optimize=false，不与 Release 数字直接比较。工具需自行具备 dotnet-counters / dotnet-gcdump；未安装时可用 dotnet tool install -g 安装。构建入口见[构建与版本](build-release-and-versioning.md)。

```powershell
$mainPid = (Get-Process neo-bpsys-wpf).Id
dotnet-counters monitor -p $mainPid System.Runtime
dotnet-gcdump collect -p $mainPid -o before-designer.gcdump
# 完成下述重复场景、等待现有异步清理收口后采集
dotnet-gcdump collect -p $mainPid -o after-designer.gcdump
```

快照在 PerfView / Visual Studio 中比较存活对象与根引用链，同时记录 GC Heap、LOH 分配、Private Bytes、Working Set；不靠固定多等几秒或只看峰值判断。真实设备的持续捕获、OCR/CUDA 和大型插件控件仍需单独测量。

## 场景与当前应检查的边界

| 场景 | 操作与证据 | 当前约束 / 源码 |
| --- | --- | --- |
| Designer | 连续五轮打开、编辑、关闭；比较 Window/VM、Canvas、历史和动画树是否按轮累积 | [编辑器关闭与预览释放](../frontend/fronted-designer-editor.md)；Language 事件解除、DispatcherOperation 取消、单例 PreviewAnimationScope 清空 |
| 教程 | 多窗口触发教程后关闭，查看 owner 是否仍被协调器持有 | [TutorialPlaybackCoordinator](../../neo-bpsys-wpf.ProductTour/TutorialPlaybackCoordinator.cs) 的弱 owner/会话清理，不再沿用旧报告的永久 Dictionary<Window,...> 假设 |
| 图片/染色 | 切三组包、背景及颜色，观察条目数与估算解码字节 | [素材缓存](../resources/resources-localization-and-assets.md)：256 项/128 MiB 与 32 项/64 MiB LRU，核对驱逐后是否仍有活跃视觉引用 |
| 捕获 | WGC/BitBlt 启停、换源、目标关闭，检查旧帧与回调 | [线程与捕获生命周期](../architecture/threading-dispatcher-and-async.md)：帧锁、世代、停止清空；复用设备仅在服务退出释放 |
| SmartBP | 自动识别启停、换局、模块退出，区分历史帧和 native runtime | [SmartBP](../business/smartbp-and-ocr.md)：192 MiB 估算帧预算（允许当前单帧超限）、容器结束释放 Provider，ALC/native 预加载仍为进程寿命 |
| Behavior | 重载、Hide/Show、关闭与 Attach 交错 | [Behavior](../frontend/fronted-behavior-system.md)：旧请求失效、所有图阶段取消、Dispose 后不 Reset 复活 session |
| WebRenderer | 分别记录主进程与 sidecar；反复 bootstrap/换包 | [WebRuntimeValue.cs](../../Built-inPlugins/neo-bpsys-wpf.WebRenderer/Services/WebRuntimeValue.cs) 的资产 active 集合、generation 与异步编码收口；不把旧已修复竞态写成当前已知泄漏 |

## Web sidecar 独立采集

默认 StartWithApplication=false，未使用时没有 sidecar。旧用户保存的 true 保持有效。PID 优先从 WebRendererSidecarService.Status.ProcessId 获取，也可查进程：

```powershell
$sidecarPid = (Get-Process -Name 'neo-bpsys-wpf.WebRenderer.Host' -ErrorAction SilentlyContinue).Id
dotnet-counters monitor -p $sidecarPid System.Runtime
dotnet-gcdump collect -p $sidecarPid -o sidecar-baseline.gcdump
```

主进程与 sidecar 分开报告；下载缓存、浏览器进程和 OS 图形资源也不等同于托管堆。stop/restart 只终止当前会话所属 sidecar，不误杀其他 dotnet 进程。

## 验证方法

验证回收可临时使用 WeakReference 与现有会话完成信号、Dispatcher 排空；不要在生产 singleton 保存监测对象列表或新增常驻轮询 Timer。不要为测试改变生产生命周期。按[测试资产政策](testing-guidelines.md)选择必要回归，开发测量先运行后清理，不把某次环境的结果当当前性能承诺。崩溃/卡死转储收集见[故障诊断](../diagnostics/crash-collection.md)。
