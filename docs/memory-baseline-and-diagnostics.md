# 内存基线与诊断方案

本目录文档面向维护者，记录内存回归验证方案、基线测量步骤和已知引用链。本文档对应内存治理任务包的阶段 0 输出。

## 目标

区分以下四类内存问题，避免伪装修复：

| 类型 | 特征 | 修复方向 |
| --- | --- | --- |
| 真正的对象泄漏 | 对象按次数累积，GC Heap 阶梯增长 | 解除强引用链 |
| 有上限但过大的常驻缓存 | 条目数稳定但总字节超预算 | 引入字节预算与 LRU |
| 高频大对象分配 | Working Set 膨胀，LOH 分配速率高 | 复用缓冲、减少分配 |
| WebRenderer 子进程常驻 | sidecar 进程独立内存 | 主进程与 sidecar 分开报告 |

## 诊断属性（阶段 0 新增）

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

## 工具准备

### 安装 dotnet 工具

```powershell
dotnet tool install -g dotnet-counters
dotnet tool install -g dotnet-gcdump
```

### 构建 Release x64

```powershell
dotnet build neo-bpsys-wpf.slnx -c Release -p:Platform=x64
```

不要使用 Preview 或 Debug 的内存数字与旧版 Release 直接比较。当前 Preview 为 `Optimize=false`。

## 场景 A：Designer v3 生命周期

### 复现步骤

连续执行五轮：

1. 启动应用，等待主窗口和后台页面加载完成。
2. 通过后台页面或入口打开 `FrontedDesignerWindow`。
3. 等待布局和教程加载完成（日志出现 `Designer sequence result` 或 `Initial preview render completed`）。
4. 关闭 `FrontedDesignerWindow`。
5. 等待 5 秒，让 Dispatcher 完成异步清理。

### 采集命令

在第一轮打开前采集基线：

```powershell
$mainPid = (Get-Process neo-bpsys-wpf).Id
dotnet-gcdump collect -p $mainPid -o before-designer.gcdump
dotnet-counters monitor -p $mainPid System.Runtime
```

在第五轮关闭后采集对比：

```powershell
dotnet-gcdump collect -p $mainPid -o after-designer-5rounds.gcdump
```

### 检查项

使用 PerfView 或 Visual Studio 打开两个 `.gcdump` 对比：

- `FrontedDesignerWindow` 实例是否仍然存活（预期：不存活）。
- `FrontedDesignerWindowViewModel` 是否仍然存活（预期：不存活）。
- Visual Tree、预览 Canvas、Undo 栈是否被一起保留。
- 每轮关闭后 GC Heap 是否阶梯增长。

### 预期引用链（阶段 1 修复目标）

当前代码中，以下两条强引用链会阻止已关闭窗口被 GC：

1. `ISettingsHostService`（Singleton）→ `LanguageSettingChanged` 事件 → `FrontedDesignerWindow` 实例。
   - 构造函数订阅：`settingsHostService.LanguageSettingChanged += OnLanguageSettingChanged;`
   - `OnClosed` 中没有显式取消订阅。
2. `TutorialPlaybackCoordinator`（Singleton）→ `_playbackGates` 字典 → `Window` 作为 key。
   - `ResolvePlaybackScope(owner)` 返回 `Window`。
   - 执行结束后没有从 `_playbackGates` 删除 key。

## 场景 B：资源和染色缓存

### 复现步骤

1. 启动应用。
2. 依次加载多个布局包（至少 3 个不同包）。
3. 在每个布局中切换多个图片和背景染色配置。
4. 触发背景染色动画（如 BO 呼吸效果）。
5. 记录缓存诊断属性。

### 采集命令

由于诊断属性是 `internal`，需要在测试或调试构建中通过 `InternalsVisibleTo` 访问。在生产环境可通过 `.gcdump` 直接观察字典大小：

```powershell
$mainPid = (Get-Process neo-bpsys-wpf).Id
dotnet-gcdump collect -p $mainPid -o after-layout-switch.gcdump
```

在 `.gcdump` 中查找：

- `FrontedResourceResolver._imageCache` 的条目数。
- `BackgroundImageTintProcessor._cache` 的条目数。
- 估算解码字节数。

### 当前理论上限

| 缓存 | 条目上限 | 单条目最大字节 | 理论最大字节 |
| --- | ---: | ---: | ---: |
| `FrontedResourceResolver._imageCache` | 256 | 1024×1024×4 = 4 MiB | 约 1 GiB |
| `BackgroundImageTintProcessor._cache` | 32 | 全尺寸 BGRA 位图 | 视原图尺寸而定 |

### 已知问题（阶段 2 修复目标）

1. `FrontedResourceResolver`：
   - 只限制张数（256），不限制解码后像素字节。
   - `Queue<ImageCacheKey>` 不是真正 LRU，命中不会更新顺序。
   - 256 张 1024 长边图片可能占用数百 MB。

2. `BackgroundImageTintProcessor`：
   - 只限制张数（32），不限制染色位图字节。
   - Cache key 包含高精度 `double strength`，动画每约 16ms 更新一次会产生大量不同 key。
   - 中间动画帧不应全部进入长期缓存。

## 场景 C：WebRenderer

### 复现步骤

1. 启动应用（默认 `StartWithApplication = true`，sidecar 自动启动）。
2. 记录主进程 PID 和 sidecar PID。
3. 分别记录两个进程的内存。
4. 切换多个布局包，观察 `_ready` 等集合是否无限增长。

### 采集命令

```powershell
$mainPid = (Get-Process neo-bpsys-wpf).Id
# sidecar 进程名为 dotnet 或 neo-bpsys-wpf.WebRenderer.Host
$sidecarPid = (Get-Process -Name "neo-bpsys-wpf.WebRenderer.Host" -ErrorAction SilentlyContinue).Id
# 或通过 WebRendererSidecarService.Status.ProcessId 获取

# 分别采集
dotnet-counters monitor -p $mainPid System.Runtime
dotnet-counters monitor -p $sidecarPid System.Runtime
dotnet-gcdump collect -p $mainPid -o main-after-webrenderer.gcdump
dotnet-gcdump collect -p $sidecarPid -o sidecar-baseline.gcdump
```

不要把两个进程的内存混成一个数字。

### 检查项

- 主进程 `WebRuntimeAssetRegistry._ready` 条目数。
- 主进程 `WebRuntimeAssetRegistry._pending` 条目数。
- 主进程 `WebRuntimeAssetRegistry._remote` 条目数。
- sidecar 进程 Working Set 和 Private Bytes。

### 已知问题（阶段 3 修复目标）

1. `WebRuntimeAssetRegistry` 使用引用相等比较并强引用 `ImageSource`：
   - `_ready`、`_pending`、`_failures`、`_remote` 四个集合。
   - `ReplaceRemoteSources` 只清理远程资源，本地文件和 Frozen Bitmap 不会随 active snapshot 清除。
2. 异步编码竞态：布局切换后旧编码任务完成可能把图片插回 `_ready`。
3. `StartWithApplication` 默认为 `true`，实验性插件默认启动 sidecar 进程。

## 引用链优先级排序

按影响从高到低：

### 优先级 1：真实对象泄漏

- **TutorialPlaybackCoordinator._playbackGates**：Singleton 持有 `Window` 作为 Dictionary key，执行结束后不删除。影响所有触发过教程的窗口。
- **FrontedDesignerWindow 语言事件**：Singleton `ISettingsHostService` 通过事件反向持有已关闭窗口。每次开关 Designer 都会累积一个窗口实例。

### 优先级 2：受控但过大的缓存

- **FrontedResourceResolver._imageCache**：256 张图片无字节上限，Singleton 长期存在。
- **BackgroundImageTintProcessor._cache**：32 张染色位图无字节上限，动画中间帧全部缓存。

### 优先级 3：异步任务回写

- **WebRuntimeAssetRegistry.CompleteEncoding**：布局切换后旧编码任务完成会把图片插回 `_ready`，没有验证 source 是否仍 active。

### 优先级 4：高频大对象分配

- **WindowCaptureService.OnFrameArrived**：每帧创建新的 staging texture、`byte[]`、`BitmapSource`。1080p BGRA 一帧约 8 MiB，高帧率下分配速率极高。

### 优先级 5：子进程常驻

- **WebRenderer sidecar**：默认 `StartWithApplication = true`，主程序启动即产生 sidecar 进程。

## 验证原则

- 验证对象是否释放时，使用 `WeakReference`，不要在生产 Singleton 中建立"被监测对象列表"。
- 不要新增常驻轮询 Timer。
- 不要为了测试改变生产生命周期。
- 不要在 Release 中输出高频日志。
- 主进程与 sidecar 内存必须分开报告。
