# 线程、Dispatcher 与异步模式

## WPF 线程约束

WPF UI 对象只能在创建它们的 UI 线程访问。包括 `Window`、`Page`、`FrameworkElement`、`ObservableCollection` 绑定集合、`SymbolIcon`、InfoBar/Snackbar 控件等。

后台任务、下载器回调、OCR 推理线程、WGC 帧回调如果要更新 UI 或绑定集合，必须切回 `Application.Current.Dispatcher`。

## 已有安全模式

### PluginMarketService

插件市场下载队列由后台下载器回调驱动，但队列是 `ObservableCollection<PluginDownloadQueueItem>`。服务用 `RunOnUiThread` 包装集合写入和状态更新：

```csharp
if (Application.Current?.Dispatcher == null || Application.Current.Dispatcher.CheckAccess())
{
    action();
    return;
}

Application.Current.Dispatcher.Invoke(action);
```

以后修改下载队列时，应继续通过这个模式更新 `_downloadQueueInternal` 和队列项属性。

### SettingPageViewModel / PluginPageViewModel

这些 ViewModel 在处理下载状态或设置同步事件时先检查 `Dispatcher.CheckAccess()`，不在 UI 线程时用 `Dispatcher.Invoke(...)`。

### UpdaterService

更新下载完成回调是 `async void`，来自 Downloader 事件。它需要弹窗或安装提示时使用：

```csharp
await Application.Current.Dispatcher.InvokeAsync(async () => { ... });
```

这是事件处理器场景下的必要妥协，但异常处理必须在方法内部完成。

### WindowCaptureService

窗口捕获服务的帧缓存由捕获回调写入、SmartBP 或预览读取。代码用 `Lock _frameLock` 保护 `_currentFrame`。生成的 `BitmapSource` 会 `Freeze()`，便于跨线程读取。

当 `GetCurrentFrame()` 发现没有启动捕获时，它通过 Dispatcher 异步弹提示并导航到 SmartBP 页面。

## DispatcherTimer 与后台 Timer

项目中多个 UI 相关周期任务使用 `DispatcherTimer`：

| 位置 | 用途 |
| --- | --- |
| `SharedDataService` | 倒计时 |
| `SmartBpService` | SmartBP 自动流程定时框架 |
| `SmartBpPageViewModel` | 捕获比例刷新 |
| `WindowCaptureService` | BitBlt 拉帧、预览窗口刷新 |

`DispatcherTimer` 的 Tick 在 UI 线程执行，适合更新绑定状态或 UI。缺点是耗时工作会卡 UI，因此 SmartBP 的 OCR 识别使用 `Task.Run` 放到后台线程。

不要把 UI 更新逻辑迁移到 `System.Threading.Timer` 后直接操作 WPF 对象。

## OCR 与锁

`OcrService` 有两把锁：

| 锁 | 保护内容 |
| --- | --- |
| `_ocrLock` | PaddleOCR 推理实例 `_ocr` 的运行、切换、重建 |
| `_downloadLock` | 模型下载状态、取消令牌、进度字段 |

`RecognizeTextCore` 在 `_ocrLock` 内运行推理；失败时尝试重建当前 OCR predictor 并重试一次。下载模型、删除模型、切换模型都要避免和推理实例生命周期竞争。

不要在持有 `_ocrLock` 时调用可能需要 UI 线程或长期等待用户交互的代码。

## SmartBP 后台任务

`AutoFillGameDataAsync` 用 `Task.Run` 执行捕获帧裁切和 OCR：

```csharp
var recognizedData = await Task.Run(
    () => CaptureAndRecognizeGameData(cancellationToken),
    cancellationToken);
```

后台部分不要直接操作 UI 对象。它读取的是已冻结的 `BitmapSource` 和 OpenCvSharp `Mat`。识别完成后写回 `CurrentGame` 数据，调用方通常来自 UI 命令；如果未来从纯后台线程触发写回，需要重新检查绑定对象和集合更新线程。

## async void

当前代码中的 `async void` 主要出现在 WPF 生命周期/事件处理器：

| 位置 | 原因 |
| --- | --- |
| `App.OnStartup` / `App.OnExit` | WPF override 签名 |
| `App.OnDispatcherUnhandledException` | WPF 事件 |
| `UpdaterService.OnDownloadFileCompletedAsync` | Downloader 事件 |
| `PluginMarketService.OnDownloadFileCompletedAsync` | Downloader 事件 |

`async void` 不应出现在普通业务方法中。事件处理器内必须自行捕获异常、清理状态并通知 UI。

## 安全修改规则

1. 更新 WPF 控件、`ObservableCollection`、绑定属性前确认当前线程。
2. 下载器、OCR、捕获回调里不要直接弹窗，优先 Dispatcher。
3. 后台任务要支持取消令牌，至少不要吞掉 `OperationCanceledException` 后留下错误状态。
4. 使用锁保护共享状态时，不要在锁内执行长时间下载、OCR、弹窗或 Dispatcher 同步等待。
5. 新增事件订阅时考虑 singleton 生命周期和解绑。
6. 前台窗口和插件注入控件的创建/访问应在 UI 线程完成。
