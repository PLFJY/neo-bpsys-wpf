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

`OcrService` 负责 PaddleOCR 模型和推理实例：

| 功能 | 代码位置 |
| --- | --- |
| 模型枚举 | `SmartBpOcrModelRegistry.Models` |
| 模型本地根目录 | `Documents\neo-bpsys-wpf\OCRModels` |
| 下载 | `DownloadModelAsync`，分 det/cls/rec 三步 |
| 删除 | `TryDeleteModel` |
| 切换 | `TrySwitchOcrModel` |
| 推理 | `RecognizeText(Mat img)` |
| 推理失败重建 | `RecognizeTextCore` 中捕获异常后 `TryRebuildCurrentOcrUnsafe()` |

可用模型键包括 `zh-cn-v5-mobile`、`en-v4-mobile`、`ja-v4-mobile`、`zh-cn-v4`、`zh-cn-v3-slim`。是否安装通过 det/cls/rec 目录下的 `inference.pdiparams` 和 `inference.pdmodel` 或 `inference.json` 判断。

OCR 下载失败会清理模型目录和字典残留；切换成功会把 `Settings.OcrModelKey` 持久化到配置文件。

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
