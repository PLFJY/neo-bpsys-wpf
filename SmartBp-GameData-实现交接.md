# SmartBp GameData 识别区域编辑与自动填充实现交接

这份文档是给下一个维护者的交接说明。你不用先读完所有代码，按这里的链路走就能定位问题、改功能、加新场景。

## 1. 这套功能现在到底做了什么

我们现在完成的是 `GameData` 场景的完整闭环：

1. SmartBp 页面可以管理识别区域配置。
2. 配置独立落盘，不进全局 `Config.json`。
3. 编辑窗口是通用结构驱动，不绑定 GameData 固定行列。
4. 编辑器只看当前帧快照，不做实时预览。
5. 保存后识别逻辑按配置裁剪并 OCR，再回填赛后数据。
6. 比例状态会显示“未开始捕获 / 等待首帧 / 匹配 / 不匹配”。
7. 不匹配提示为红色，并给出“建议去编辑”的引导。
8. OCR 结果和角色匹配过程会写日志，便于定位误识别。

## 2. 一条完整调用链（从按钮到回填）

### 2.1 配置编辑链路

`SmartBpPage.xaml` 按钮 -> `SmartBpPageViewModel.OpenGameDataRegionEditorAsync()` -> `RegionEditorWindow` -> 回写 Profile -> `SmartBpRegionConfigService.TrySaveGameDataProfile()`。

关键点：

1. 必须先启动捕获，才能打开编辑器。
2. 编辑器拿的是当前帧冻结图（客户端区域），不是实时流。
3. ViewModel 先把 `SmartBpRegionProfile` 转成通用 `RegionLayoutDefinition`。
4. 用户编辑结束后，再从 `RegionLayoutDefinition` 回写到 `SmartBpRegionProfile`。
5. 保存前后都经过结构和坐标校验。

### 2.2 自动识别回填链路

`GameDataPage` 的“一键识别并填充” -> `SmartBpService.AutoFillGameDataAsync()` -> `CaptureAndRecognizeGameData()` -> `ApplyRecognizedData()`。

关键点：

1. 先检查 OCR 是否就绪，不就绪弹窗提示下载并启用模型。
2. 从 `IWindowCaptureService.GetCurrentFrame()` 拿当前帧。
3. 从 `ISmartBpRegionConfigService.GetCurrentGameDataProfile()` 拿当前配置。
4. 先按行裁剪，再按列裁剪，OCR 识别名字与数字。
5. 求生者先精确匹配角色名，失败再模糊匹配（JaroWinkler，阈值 0.50）。
6. 全过程有日志：原始 OCR 文本、解析结果、匹配成功/失败。

## 3. 主要文件在哪里

### 3.1 页面与交互

1. [SmartBpPage.xaml](./neo-bpsys-wpf/Views/Pages/SmartBpPage.xaml)
2. [SmartBpPageViewModel.cs](./neo-bpsys-wpf/ViewModels/Pages/SmartBpPageViewModel.cs)

### 3.2 编辑器（通用组件）

1. [RegionEditorWindow.xaml](./neo-bpsys-wpf/Views/Windows/RegionEditorWindow.xaml)
2. [RegionEditorWindow.xaml.cs](./neo-bpsys-wpf/Views/Windows/RegionEditorWindow.xaml.cs)
3. [RegionLayoutDefinition.cs](./neo-bpsys-wpf.Core/Models/RegionLayoutDefinition.cs)

### 3.3 配置模型与存储

1. [SmartBpRegionProfile.cs](./neo-bpsys-wpf.Core/Models/SmartBpRegionProfile.cs)
2. [RelativeRect.cs](./neo-bpsys-wpf.Core/Models/RelativeRect.cs)
3. [ISmartBpRegionConfigService.cs](./neo-bpsys-wpf.Core/Abstractions/Services/ISmartBpRegionConfigService.cs)
4. [SmartBpRegionConfigService.cs](./neo-bpsys-wpf/Services/SmartBpRegionConfigService.cs)
5. [SmartBpRegionDefaults.cs](./neo-bpsys-wpf/Services/SmartBpRegionDefaults.cs)
6. [GameDataRegions.16-9.default.json](./neo-bpsys-wpf/Resources/SmartBpDefaultConfigs/GameDataRegions.16-9.default.json)

### 3.4 识别与 OCR

1. [SmartBpService.cs](./neo-bpsys-wpf/Services/SmartBpService.cs)
2. [OcrService.cs](./neo-bpsys-wpf/Services/OcrService.cs)

## 4. 配置文件与资源路径

1. 用户配置目录：`%APPDATA%\neo-bpsys-wpf\SmartBp\`
2. 用户配置文件：`GameDataRegions.json`
3. 内置重置模板：`Resources\SmartBpDefaultConfigs\GameDataRegions.16-9.default.json`

说明：

1. 用户配置与全局配置分离。
2. 内置模板只用于“重置配置（16:9）”。
3. 坐标全是 `0~1` 相对坐标，保证相同比例可复用。

## 5. 编辑器是如何通用化的

编辑器并不理解“GameData 5 行 6 列”这件事，它只处理树结构节点。

通用规则：

1. 传入 `RegionLayoutDefinition`。
2. 每个 `RegionLayoutNode` 有 `Id`、`Label`、`Rect`、`Children`。
3. `Rect` 永远相对父节点，根节点相对整帧。
4. 子节点可开启 `ClampToParent`，确保不能拖出父框。
5. 左侧树形可折叠。
6. 同模板组节点支持“一键应用”。

“一键应用”细节：

1. 只在同 `TemplateGroupId` 的根节点之间应用。
2. 大元素只套用 `W/H`，保留每个目标的大元素位置。
3. 小元素套用 `X/Y/W/H`，优先按 `Id` 对齐，缺失时按索引兜底。

## 6. 当前 GameData 的模板策略

你当时定的策略已经落地：监管者和求生者分两套模板组，名称也用语义名。

在 `CreateGameDataEditorStructure()` 里：

1. `row0_hunter` 属于 `hunter_rows`。
2. `row1_survivor` 到 `row4_survivor` 属于 `survivor_rows`。
3. 小框显示名是具体数据名，不再是“数据框1/2/3...”。

## 7. 比例状态与“等待首帧”逻辑

为什么之前会“第一次不匹配”：

1. 捕获刚启动时 `GetCurrentFrame()` 可能为空。
2. 这时比例文本是 `-`。
3. 如果直接判定，就会误显示“不匹配”。

现在的处理：

1. 未捕获：`未开始捕获`。
2. 已捕获但未拿到首帧：`等待首帧`。
3. 拿到首帧后：进入 `匹配 / 不匹配` 判断。
4. 增加 300ms 定时刷新，首帧到达后能快速切换状态。

## 8. OCR 稳定性与日志诊断

### 8.1 第二次识别崩溃的处理

`OcrService.RecognizeTextCore()` 已经做了“失败后重建并重试一次”：

1. `_ocr.Run(...)` 异常时记录 warning。
2. 尝试重建当前模型实例。
3. 重建成功后再跑一次。
4. 仍失败则写 error 并返回 `null`，不让流程硬崩。

### 8.2 GameData OCR 日志已经包含什么

`SmartBpService` 里现在会写：

1. 名称框原始 OCR 文本（监管者/求生者）。
2. 数字区批量 OCR 原始文本与解析结果。
3. 批量失败后的逐列 fallback 结果。
4. 每行最终解析出的玩家、角色、5个字段值。
5. 角色匹配成功/失败、匹配模式、模糊分数。

这部分是你排“角色对不上”的核心证据链。

## 9. 关键实现片段（节选）

### 9.1 编辑器开启条件与单帧快照

```csharp
if (!_windowCaptureService.IsCapturing)
{
    await MessageBoxHelper.ShowInfoAsync("请先启动捕获再编辑识别区域。");
    return;
}

var frame = _windowCaptureService.GetCurrentFrame();
if (frame == null)
    return;

var layout = BuildGameDataLayout(profile, GameDataEditorStructure);
var editor = new RegionEditorWindow(frame, layout);
```

### 9.2 比例状态的“等待首帧”分支

```csharp
if (!_windowCaptureService.IsCapturing)
{
    RegionAspectStatusText = "未开始捕获";
    RegionAspectHintText = "请先启动捕获后再检查比例。";
    return;
}

if (string.IsNullOrWhiteSpace(captureAspect) || captureAspect == "-")
{
    RegionAspectStatusText = "等待首帧";
    RegionAspectHintText = "捕获已启动，正在等待首帧用于比例匹配。";
    return;
}
```

### 9.3 同模板一键应用（保留大元素位置）

```csharp
// 大元素保留位置，仅应用尺寸（W/H）。
target.Node.Rect = target.Node.Rect with
{
    W = sourceRoot.Node.Rect.W,
    H = sourceRoot.Node.Rect.H
};

// 子元素应用位置和尺寸（X/Y/W/H）。
targetChild.Rect = rect;
```

### 9.4 OCR 异常后的重建重试

```csharp
try
{
    var r = _ocr.Run(bgr);
    return string.IsNullOrWhiteSpace(r.Text) ? null : r.Text;
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "OCR run failed, trying to rebuild OCR predictor and retry once.");
    if (!TryRebuildCurrentOcrUnsafe())
        return null;

    var retry = _ocr!.Run(bgr);
    return string.IsNullOrWhiteSpace(retry.Text) ? null : retry.Text;
}
```

## 10. 后续你最可能会改的点

1. 改结构：只动 `CreateGameDataEditorStructure()` 和 Profile<->Layout 映射。
2. 改默认框：优先改 `Resources/...GameDataRegions.16-9.default.json`。
3. 调匹配策略：改 `SmartBpService.ApplyRecognizedData()`。
4. 调 OCR 容错：改 `GetRowDataValues()` 和 `TryParseFiveNumbers()`。
5. 加新场景（例如自动BP）：复用 `RegionEditorWindow` 和 `RegionLayoutDefinition`，新增一个场景专属 Profile 适配器即可。

## 11. 维护约定（我建议继续保持）

1. 编辑器保持“通用结构驱动”，不要把 GameData 固定规则写进编辑器。
2. 业务配置与 UI 表示分离：Profile 是业务存储，Layout 是编辑展示。
3. 任何 OCR 失败都不要静默吞掉，至少要写日志。
4. 比例状态只做提示，不阻断识别流程。
5. 默认模板永远保留资源文件 + 代码兜底双保险。

如果你看到这里，已经可以独立接手这套代码了。
你只要记住一句话：

先看 `SmartBpPageViewModel` 管流程，再看 `RegionEditorWindow` 管编辑，再看 `SmartBpService` 管识别回填。
