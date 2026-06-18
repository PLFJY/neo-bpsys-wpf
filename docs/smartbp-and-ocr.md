# SmartBP 与 OCR

线程和后台任务注意事项见 [threading-dispatcher-and-async.md](threading-dispatcher-and-async.md)。SmartBP 默认配置和资源文件见 [resources-localization-and-assets.md](resources-localization-and-assets.md)。

## 当前边界

SmartBP 需要分清两个能力：

| 能力 | 当前状态 |
| --- | --- |
| 赛后数据 OCR 自动回填 | 已经成熟且可用 |
| 全流程自动 BP / 自动 BP 画面切换 | TODO。`SmartBpService.StartSmartBp()` 有定时器框架，但 `Timer_Tick` 中尚未实现完整自动 BP 逻辑 |

不要在文档、UI 或提交说明中把“全流程自动 BP”描述为已完成。

## OCR 模型管理

SmartBP 重型运行时不再随默认安装包进入主应用发布目录。主应用只保留 SmartBP 页面壳、安装覆盖层、模块加载器、状态模型和功能命令抽象；真实 SmartBP UI、`SmartBpService`、`OcrService`、OpenCvSharp、PaddleOCR、PaddleInference 和 OCR 模型下载逻辑位于 `neo-bpsys-wpf.SmartBp.Module`。

模块安装状态保存到：

```text
%APPDATA%\neo-bpsys-wpf\SmartBpModuleState.json
```

状态中的 `ModuleRoot` 指向 SmartBP 模块目录。Debug 构建可以直接加载 `neo-bpsys-wpf.SmartBp.Module\bin\Debug\net10.0-windows10.0.20348\`，并以 `DevelopmentDirectory` 记录，不要求 `component.json` 或版本比较。

`OcrService` 负责 PaddleOCR 模型和推理实例：

| 功能 | 代码位置 |
| --- | --- |
| 模型枚举 | `SmartBpOcrModelRegistry.Models` |
| 模型本地根目录 | `{SmartBpModuleRoot}\OCRModels` |
| 下载 | `DownloadModelAsync`，分 det/cls/rec 三步 |
| 删除 | `TryDeleteModel` |
| 切换 | `TrySwitchOcrModel` |
| 推理 | `RecognizeText(Mat img)` |
| 推理失败重建 | `RecognizeTextCore` 中捕获异常后 `TryRebuildCurrentOcrUnsafe()` |

可用模型键包括 `zh-cn-v5-mobile`、`en-v4-mobile`、`ja-v4-mobile`、`zh-cn-v4`、`zh-cn-v3-slim`。是否安装通过 det/cls/rec 目录下的 `inference.pdiparams` 和 `inference.pdmodel` 或 `inference.json` 判断。

OCR 下载失败会清理模型目录和字典残留；切换成功会把 `Settings.OcrModelKey` 持久化到配置文件。旧版本已经下载到 `Documents\neo-bpsys-wpf\OCRModels` 的模型会在模块首次安装或首次成功加载后迁移到 `{SmartBpModuleRoot}\OCRModels`，迁移使用 copy -> verify -> delete，且不会删除 `Documents\neo-bpsys-wpf` 根目录。

## 模块加载与安装

SmartBP 页面先显示宿主侧页面壳。模块未加载时，内容区域显示覆盖层；模块加载成功后，模块入口实现 `ISmartBpModuleEntryPoint` 并返回真实 SmartBP 内容。

模块目录通过 `component.json` 校验：

1. `ComponentId` 必须是 `SmartBpModule`。
2. `Rid` 必须匹配 `win-x64`。
3. `RuntimeAbiVersion` 必须匹配宿主 ABI。
4. 必须存在模块入口程序集 `neo-bpsys-wpf.SmartBp.Module.dll`。

Release 构建不会查询 GitHub latest release，而是通过 `gh-releases.plfjy.top` 转发 API 获取 release 列表，按当前应用版本 tag 精确匹配同一 release 下的 `SmartBpModuleManifest.json`。manifest 文件固定通过 `https://gh.plfjy.top/` 下载；模块 zip 的实际下载地址会套用设置中的 GitHub 下载镜像，跟随用户在设置页持久化保存的 `GhProxyMirror`。如果远端 manifest 拉取失败，只要本地模块通过目录、RID、ABI 和入口程序集校验，仍允许加载。Preview 构建不进行在线检查，主要支持选择本地模块目录或导入 `SmartBpModule.zip`。

## SmartBpService 赛后数据流程

`AutoFillGameDataAsync` 的主流程：

1. 检查 OCR 模型是否已选择且已安装。
2. 检查 `IWindowCaptureService.IsCapturing`。
3. 通过窗口捕获服务读取当前帧。
4. 使用 `SmartBpRegionConfigService.GetCurrentGameDataProfile()` 获取区域配置。
5. 裁切监管者行和求生者行。
6. 对名称区域做文本预处理并 OCR。
7. 对数据列做数字预处理并 OCR。
8. 将监管者字段直接写回 `CurrentGame.HunPlayer.Data`。
9. 将求生者数据按角色名匹配后写回 `CurrentGame.SurPlayerList`。

求生者匹配先做规范化精确匹配，再用 Jaro-Winkler 模糊匹配兜底，阈值当前为 `0.50`。

## 区域配置

当前 SmartBP 只管理 GameData 场景配置：

```text
%APPDATA%\neo-bpsys-wpf\SmartBp\GameDataRegions.json
```

默认配置优先来自：

```text
Resources/SmartBpDefaultConfigs/GameDataRegions.16-9.default.json
```

如果资源缺失，`SmartBpGameDataSceneDefinition` 会生成代码内 fallback 配置。配置保存前会校验：

1. `Scene` 必须是 `GameData`。
2. 根节点行数为 5。
3. 每行 6 个 cell。
4. 大框和小框相对坐标合法。
5. `BaseAspectRatio` 会被规范化，存储时优先保留比例基准。

## 图像预处理

名称文本使用 `PreprocessForText`：放大、灰度、背景抑制、二值化、形态学、反色。

数字使用 `PreprocessForDigits`：放大、灰度、归一化、Otsu 二值化、闭运算、反色。

数据列识别优先把 5 个数字列拼成 strip 一次 OCR；如果解析不出 5 个数字，再逐列 OCR 回退。

## 调试点

1. OCR 不工作先看是否已下载并切换模型。
2. 捕获不到数据先看窗口捕获服务是否处于 capturing。
3. 识别错位先导出/检查 `GameDataRegions.json` 与实际画面比例。
4. 角色匹配失败时看日志中的 `SmartBp Match failed` 和 OCR 原始名称。
5. 预处理图像可临时用 `SaveDebug` 输出到运行目录 `debug`，但不要把调试图片提交进仓库。

常见日志关键词：

| 关键词 | 含义 |
| --- | --- |
| `SmartBp AutoFill skipped: OCR model is not ready.` | 未选择或未安装 OCR 模型 |
| `SmartBp AutoFill skipped: capture is not running.` | 窗口捕获未启动 |
| `SmartBp OCR Survivor` / `SmartBp OCR Hunter` | 行识别结果和原始 OCR 文本 |
| `SmartBp Match success` | 求生者角色匹配成功，含 exact/fuzzy 模式 |
| `SmartBp Match failed` | 识别角色无法映射到当前对局求生者 |
| `OCR run failed, trying to rebuild OCR predictor and retry once.` | PaddleOCR 推理失败，正在重建后重试 |
