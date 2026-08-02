# SmartBP 与 OCR

线程和后台任务注意事项见 [threading-dispatcher-and-async.md](../architecture/threading-dispatcher-and-async.md)。SmartBP 默认配置和资源文件见 [resources-localization-and-assets.md](../resources/resources-localization-and-assets.md)。

## 架构概览

SmartBP 是面向第五人格赛事的直播导播辅助系统中的智能 BP 识别与赛后数据回填子系统。它采用**模块分离架构**：主应用只保留 SmartBP 页面壳、安装覆盖层、模块加载器、状态模型和功能命令抽象；真正的 SmartBP UI、OCR 引擎、OpenCvSharp 等重型依赖全部位于 `neo-bpsys-wpf.SmartBp.Module` 独立项目中。

```text
neo-bpsys-wpf (主应用)
├── Views/Pages/SmartBpPage.xaml          ← 页面壳 + 模块未加载覆盖层
├── ViewModels/Pages/SmartBpPageViewModel  ← 模块加载/安装/版本管理
├── Core/Models/SmartBpModule/             ← 模块 manifest、状态、命令抽象
└── Core/Abstractions/Services/ISmartBpService.cs  ← 服务契约（宿主侧）

neo-bpsys-wpf.SmartBp.Module (SmartBP 模块)
├── SmartBpModuleEntryPoint.cs             ← 模块入口，实现 ISmartBpModuleEntryPoint
│   ├── CreateSmartBpContent() → 构建 DI 容器并返回 SmartBpModuleContentView
│   └── GetFeatureCommands()   → 暴露 AutoFillGameData 命令
├── Services/
│   ├── SmartBpService.cs                  ← 赛后数据 OCR 回填（宿主侧服务）
│   ├── Recognition/
│   │   ├── SmartBpOcrRecognitionServices.cs  ← OCR BP 识别全流程
│   │   ├── SmartBpSceneGateService.cs        ← 场景门禁
│   │   ├── SmartBpAutoRecognitionCoordinator ← 自动循环主控
│   │   └── ... (更多识别/状态/应用管线)
│   └── OcrService.cs / PaddleOcrProvider.cs / TesseractOcrProvider.cs / RapidOcrNetProvider.cs
├── ViewModels/SmartBpModuleContentViewModel.cs ← 模块 UI 主 ViewModel
├── Abstractions/                          ← 模块内部接口
└── Models/Recognition/                    ← 识别数据模型
```

### 两条能力线

SmartBP 包含两条独立的能力线：

| 能力线 | 入口 | 引擎 | 产出 | 成熟度 |
| --- | --- | --- | --- | --- |
| 赛后数据 OCR 回填 | `ISmartBpService.AutoFillGameDataAsync` | PaddleOCR | 直接写 `CurrentGame` | 成熟可用 |
| BP 状态自动识别 | `SmartBpAutoRecognitionCoordinator` | OCR-only | 候选操作 → 应用管线 → `CurrentGame` | BP 状态 OCR 可用 |

两条线共享同一套窗口捕获和 OCR 模型管理基础设施。赛后数据回填直接基于完整捕获帧的 OCR 坐标重建表格；全流程 BP 状态识别则使用可编辑的粗裁剪区域。

### 宿主侧接口

宿主侧在 `Core.Abstractions.Services` 中定义 `ISmartBpService`：

```csharp
public interface ISmartBpService
{
    bool IsSmartBpRunning { get; }
    void StartSmartBp();
    void StopSmartBp();
    Task AutoFillGameDataAsync(CancellationToken cancellationToken = default);
}
```

实际实现 `SmartBpService` 位于 SmartBP 模块，依赖模块提供的 `IOcrService`、角色解析和捕获服务等。

### 模块入口与 DI

`SmartBpModuleEntryPoint` 是模块的单一入口点，实现 `ISmartBpModuleEntryPoint`：

- `CreateSmartBpContent(hostServices)`：构建独立的 `ServiceProvider`，注入所有模块级服务（OCR Provider、识别管线、debug log 等），创建 `SmartBpModuleContentView` 并绑定 `SmartBpModuleContentViewModel`
- `GetFeatureCommands()`：返回宿主可调用的功能命令，目前只有 `AutoFillGameData`（赛后数据回填）

模块 DI 容器注册了 OCR Provider（Paddle、Tesseract、RapidOCR）、BP 识别管线、场景门禁、候选操作和唯一的 Reconciliation 服务。SmartBP 不注册 Ban/Pick StateStore、进度评分器、回填服务或操作 ledger。

## 当前边界

| 能力 | 当前状态 |
| --- | --- |
| 赛后数据 OCR 自动回填 | 已经成熟且可用 |
| PaddleOCR BP 状态识别 | 默认 OCR Provider；读取文字与边界框，本地解析阶段、禁用、选择与玩家 ID |
| Tesseract BP 状态识别 | 可选 OCR Provider；可在 SmartBP 页面勾选下载 `chi_sim`/`eng`/`jpn` 到 SmartBP 模块目录，不会自动回退到 Paddle |
| RapidOCR BP 状态识别 | 可选本地 OCR Provider；使用 SmartBP 托管的中文 det/cls/rec/dict 资产，不会自动回退到其他 Provider |
| 本地视觉模型 + llama.cpp BP 状态识别 | 已从 SmartBP 模块移除；BP 状态识别仅支持 OCR Provider |
| GameGuidance 自动追赶 | 可选；按工作流槽位逐步前进，不跨过未完成的 Ban/Pick 步骤 |
| 识别结果自动应用 | 可选；仅通过 `ICharacterSelectionService` 应用高置信度且已解析的当前帧角色证据，或在引导落后时由同局短时帧安全补充的遗漏证据 |
| 手动强制同步 | 对当前帧执行独立全量 OCR；角色无动画写入主程序，再直接定位到当前画面对应步骤 |
| 全流程自动 BP 画面切换 | TODO。当前识别结果只进入现有候选操作/应用管线，不实现自动切屏 |
| MapBP 识别 | 不识别 |
| 天赋角色操作识别 | 不识别 |

不要在文档、UI 或提交说明中把"全流程自动 BP"或 MapBP 识别描述为已完成。

## SmartBP UI 页面

SmartBP 后台页面 (`SmartBpPage.xaml`) 是一个 WPF `Page`，包含两层结构：

1. **模块未加载层**：当 SmartBP 模块尚未安装或加载失败时，显示覆盖层，包含模块路径选择、在线安装/导入压缩包/选择本地目录等按钮
2. **模块内容层**：通过 `ContentControl` 承载 `SmartBpModuleEntryPoint.CreateSmartBpContent()` 返回的 `SmartBpModuleContentView`

`SmartBpModuleContentViewModel` 是模块 UI 的主 ViewModel，管理：
- 窗口捕获（WGC / Bitblt）和窗口选择
- OCR 模型下载/切换/删除
- 赛后数据区域配置编辑器（通过 `RegionEditorWindow`）
- OCR BP 自动识别启停、设置与 debug 日志展示
- 自动识别启停与 debug 日志展示

## OCR 模型管理

SmartBP 重型运行时不再随默认安装包进入主应用发布目录。主应用只保留 SmartBP 页面壳、安装覆盖层、模块加载器、状态模型和功能命令抽象；真实 SmartBP UI、`SmartBpService`、`OcrService`、OpenCvSharp、PaddleOCR、PaddleInference 和 OCR 模型下载逻辑位于 `neo-bpsys-wpf.SmartBp.Module`。当前代码库和构建流水线以 .NET 10 为基线。

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

宿主使用独立 `AssemblyLoadContext` 加载模块。托管依赖优先复用宿主程序集，再从模块根目录解析；OpenCvSharp、PaddleInference 等原生依赖则从模块的 `runtimes/{Rid}/native/` 目录解析，并兼容位于模块根目录的原生 DLL。模块发布和迁移时必须保留 `runtimes` 目录结构，否则托管包装程序集存在也无法初始化对应 native runtime。

Release 构建只检查当前应用版本对应的 tag，不会获取最新 release。优先直接访问 GitHub Releases 下载当前 tag 对应的 `SmartBpModuleManifest.json`（`https://github.com/PLFJY/neo-bpsys-wpf/releases/download/{tag}/SmartBpModuleManifest.json`），失败时回退到 `https://smartbp-module-manifest.plfjy.top/?tag={tag}` 获取。官方模块 asset 是 `SmartBpModule.7z`，实际下载地址会套用设置中的 GitHub 下载镜像。该镜像由软件更新、插件市场设置和 SmartBP 模块下载高级选项共用 `GhProxyMirror` 持久化字段，SmartBP 页面可直接测试候选镜像延迟。

远程版本检查只作为更新提示，不阻塞本地模块加载：ABI 兼容性由 `component.json` 的 `RuntimeAbiVersion` 硬性校验保证，本地模块只要通过目录、RID、ABI 和入口程序集校验就立即加载显示。加载成功后异步拉取远端 manifest，仅在本地版本低于要求版本时通过 `ModuleVersionOutdated` 事件触发 `IInfoBarService` 警告提示用户更新；拉取失败或网络不可达时静默跳过，不影响已加载模块使用。Preview 构建不进行在线检查，主要支持选择本地模块目录或导入 `SmartBpModule.7z` / 旧 `SmartBpModule.zip`。

SmartBP 模块在线安装和手动导入支持 `.7z` 与旧 `.zip` 包，归档格式通过文件内容探测，不只依赖扩展名。运行时解压使用随应用发布的官方 x64 7-Zip（位于 `<AppBase>/Tools/7Zip/`），用户不需要单独安装 7-Zip。这只影响 SmartBP 模块包；`.bpui` / Designer v3 布局包导入导出行为不变。

## SmartBpService 赛后数据流程

`AutoFillGameDataAsync` 的主流程：

1. 检查 OCR 模型是否已选择且已安装。
2. 检查 `IWindowCaptureService.IsCapturing`。
3. 通过窗口捕获服务读取当前帧。
4. 对完整捕获帧只执行一次带边界框的 OCR。
5. 从“玩家 ID（角色名）”文本按 Y 坐标建立五个有效行；取角色文本右侧的数字和空值标记，按 X 坐标推断五个统计列，再按最近 Y 坐标归属。
6. 对整表 OCR 漏掉的数据格，按推断出的行列中心紧密裁剪小区域，并生成原图放大、CLAHE 对比度增强、Otsu 二值反色加粗三种变体。Paddle 模式下直接调用字符识别器而跳过文本位置检测，避免细窄数字 `1` 因没有检测框而消失；结果只有在多个变体一致，或单一变体置信度足够高时才并回整表重新解析。低置信度的干净 `1` 以及类似 `1r`、`1A` 的“数字加一个尾随噪声字符”都会触发排除底部表格线的窄中心裁剪识别，并检查中心高窄亮色笔画；它们和图像形态证据都不能各自单独回填，必须形成至少两份相互确认的证据。
7. 名称左侧的等级、天赋数字、徽章和其他残留文字不会参与名称、角色或数据列解析。
8. 将监管者字段直接写回 `CurrentGame.HunPlayer.Data`。
9. 将求生者数据按角色名匹配后写回 `CurrentGame.SurPlayerList`。

求生者匹配先做规范化精确匹配，再用 Jaro-Winkler 模糊匹配兜底，阈值当前为 `0.50`。

## BP 状态 OCR 识别流程

BP 状态识别和赛后数据 OCR 是两条不同流程。BP 识别先生成仅代表当前帧的 `SmartBpBusinessStateRecognitionResult`，再由统一 Reconciliation 读取主程序槽位状态并通过 `ICharacterSelectionService` 提交；SmartBP 不合并或持久化第二份 Ban/Pick 业务状态。

默认引擎是 PaddleOCR：

1. 从当前捕获帧裁剪 `phase_top` 和 planner 请求的内容区域。
2. 默认把裁剪图按纵向拼成无文字标签的 OCR contact sheet，只运行一次 PaddleOCR。
3. 读取 `PaddleOcrResult.Regions` 中的文本、置信度和 `RotatedRect`，转换为按 `CenterY`、`CenterX` 排序的文本行与轴对齐边界框。
4. 按 contact sheet 坐标把文本行映射回 `phase_top`、`left_top`、`right_top`、`left_bottom`、`right_bottom`。
5. 本地规则根据 `phase_top` 文本和左右侧 X 坐标判断阶段；非活动侧 `等待中` 不覆盖活动阶段。
6. 本地解析四个粗区域：`right_top -> banned_sur`、`left_top -> banned_hun`、`left_bottom -> picked_sur`、`right_bottom -> picked_hun`。
7. 角色名只从 `ISharedDataService.SurCharaDict` / `HunCharaDict` 匹配；无法明确解析的 OCR 文本只进入诊断，不会应用为角色。

`UseOcrContactSheet = false` 时会逐区域 OCR，主要用于排查 contact sheet 映射问题。OCR 识别默认间隔较短；短时回看窗口由帧缓冲长度、OCR 周期、最低 OCR 周期和 `RecognitionTransitionLookBehindMilliseconds` 共同限定。

自动 BP 循环使用 `SmartBpRecognitionScene` 场景门禁。角色 BP 场景才允许生成和应用 Ban/Pick 操作；求生者/监管者天赋阶段只允许同步引导；大厅、规则、禁选顺序、转场不写入。区域选择、等待开始、加载和对局内会阻断当前帧的内容识别与新操作生成，并停止调度后续 tick；已经排队或正在应用的角色 BP 操作会继续完成，队列排空后才以 `SmartBpCharacterBpEnded` 正常完成 GameGuidance 和自动识别，不触发取消事件。区域选择不属于 MapBP 或角色 BP 识别范围。用户手动停止仍会立即取消当前识别。

PaddleOCR 模型、RapidOCR 模型和 Tesseract 语言文件统一经 `SmartBpParallelDownload` 适配到宿主 `IFileDownloadService`：文件按顺序处理，单个文件支持暂停、继续、取消后保留分片，以及由 `Downloader` 管理的 HTTP Range 续传；百分比和速度直接采用 `Downloader` 原生进度回调。服务端不支持 Range 时会安全地从头下载。瞬时网络错误最多重试 5 次。多个安装资源仍按顺序处理，避免同时下载模型造成网络、内存与磁盘争抢。

当前不识别 MapBP，不识别天赋结果，不直接修改 `CurrentGame`。

### 自动识别循环全流程

BP 状态自动识别的完整循环由 `SmartBpAutoRecognitionCoordinator` 协调。业务状态只存在于主程序；一次 tick 的 OCR 结果只代表当前帧：

```text
捕获帧
  ├─ 高频轻量采样 → SmartBpFrameRingBuffer（有界、绑定 Game Guid + GameProgress）
  ├─ OCR TopCenterStatus / TopLeftStatus → 生命周期和结束门禁
  ├─ OCR PhaseTop → 得到本帧权威 phase
  ├─ 当前 phase 确认后识别四类角色槽位
  │    └─ SmartBpOcrRegionParser → 固定几何槽位、名称解析、置信度和安全元数据
  ├─ SmartBpSceneGateService → 决定本帧能否处理角色
  ├─ SmartBpCatchUpTriggerEvaluator
  │    ├─ 值比较 Action + 规范化 Indexes
  │    └─ 仅在位置不一致、前置 Pending 槽位洞或 Pending 目标槽已有新角色证据时触发
  ├─ 触发落后追赶时 SmartBpHistoricalFrameReviewService
  │    ├─ 默认只看目标之前 2 个工作流步骤，每步最多 1 张代表帧
  │    ├─ Phase/Action 必须和所补步骤严格对齐
  │    └─ 只补 Pending 且当前帧未明确选择的历史角色证据
  └─ SmartBpReconciliationService
       ├─ IGameGuidanceService.GetRuntimeSnapshot() → 工作流 Action/Indexes
       ├─ ICharacterSelectionService.GetCurrentBpSlotCommitState() → 主程序槽位真值
       ├─ SmartBpCandidateOperationBuilder → 当前帧安全候选
       ├─ ICharacterSelectionService → 角色或明确空操作提交
       └─ IGameGuidanceService → 自动逐步追赶或手动直接定位
```

### 场景门禁详解

`SmartBpSceneGateService.Classify()` 根据识别到的 phase 文本、业务状态和原始引擎响应，将当前画面分为以下场景：

| 场景 | 枚举值 | 角色操作 | 引导同步 | 暂停循环 | 触发条件 |
| --- | --- | --- | --- | --- | --- |
| 角色 BP | `CharacterBp` | 允许 | 允许 | 否 | 检测到屏蔽/选择/角色选择中等文本 |
| 求生者天赋 | `SurvivorTalent` | 允许补齐仍可见槽位 | 允许 | 否 | 检测到"求生者天赋特质调整" |
| 监管者天赋 | `HunterTalent` | 允许补齐仍可见槽位 | 允许 | 否 | 检测到"监管者天赋特质调整"/"监管者选择天赋中" |
| 天赋已锁定 | `TalentLocked` | 禁止 | 允许 | 是 | 检测到"天赋已锁定" |
| 区域选择-求生者 | `AreaSelectionSurvivor` | 禁止 | 禁止 | 是 | 检测到"求生者选择区域中" |
| 区域选择-监管者 | `AreaSelectionHunter` | 禁止 | 禁止 | 是 | 检测到"监管者选择区域中" |
| 等待开始 | `WaitingGameStart` | 禁止 | 禁止 | 是 | 检测到"等待游戏开始" |
| 加载中 | `Loading` | 禁止 | 禁止 | 是 | 检测到"加载中"/"正在加载" |
| 对局中 | `InGame` | 禁止 | 禁止 | 是 | 检测到对局内 HUD 文本 |
| 大厅 | `Lobby` | 禁止 | 禁止 | 否 | 检测到"大厅" |
| 规则设置 | `RulesDialog` | 禁止 | 禁止 | 否 | 检测到"规则设置" |
| 禁选顺序 | `BanPickOrderDialog` | 禁止 | 禁止 | 否 | 检测到"查看禁选顺序"/"选择禁用数量" |
| 转场 | `Transition` | 禁止 | 禁止 | 否 | 检测到"开始案件还原"/"阵容选择中"等 |

### 自动逐步追赶与手动强制同步

两种行为共享 `SmartBpReconciliationService` 和主程序槽位真值，但执行语义不同，禁止混用。

#### 自动逐步追赶

自动追赶用当前帧 phase 确定 Action 类别，用 `IGameGuidanceService.GetRuntimeSnapshot()` 中每个步骤的 `Indexes` 区分同一 Action 的多次出现，并将当前帧已识别槽位与主程序已提交槽位合并判断。`SmartBpWorkflowPosition` 对 `Action + 排序去重后的 Indexes` 实现值相等；相同 Action 但槽位集合不同仍然是不相等的位置。仅识别到前一组槽位刚刚选满时不会擅自跳到同 Action 的下一组；必须识别到下一组的槽位证据，或由两次同 Action 之间的其他业务步骤已经完成来证明阶段确实越过。例如画面为第二次 `PickSur` 且已经识别到求生者槽位 2，而引导尚未开始时，目标是 `PickSur[2]`，追赶顺序必须是：

```text
BanSur[0,1] → PickSur[0,1] → BanSur[2] → PickSur[2]
```

具体约束如下：

1. 先执行廉价触发判定。只有以下任一条件成立才安排 Reconciliation：Guidance 尚未启动但允许自动同步；当前 `Action + Indexes` 与槽位推导目标不同；目标之前仍有 `Pending` 业务步骤；`Pending` 或 `CommittedEmpty` 槽位已有新的安全 `Selected` 证据；或 `DistributeChara` 有安全的分配证据需要恢复/交换。全部不成立时本 Tick 不调用 Reconciliation，也不调度历史 OCR。宿主槽位已经写入只表示该业务操作已提交，不能单独证明画面已进入同 Action 的下一组槽位。
2. 引导未启动或没有有效当前步骤时，调用 `StartGuidance()` 进入工作流首步。
3. 只有引导未超过槽位推导目标，且位置不一致或目标之前存在 Pending 槽位洞时才回看。`OcrBackfillLookBehindSteps` 默认 2，表示只考虑目标之前两个工作流步骤；每个待补步骤最多选择一张当前对局上下文中的历史代表帧，并只请求 Phase 加该 Action 对应的一个角色区域，避免旧实现每 Tick 最多 16 次四区域完整 OCR。引导已经在目标之后时，本轮优先依据当前帧决定等待或纠偏，不额外用历史 OCR 放大回跳判断。
4. 历史帧识别出的 Phase 必须映射为被补步骤的同一 Action，否则整帧拒绝。回看结果采用“补充合并”：宿主必须仍为 `Pending` 或 `CommittedEmpty`，当前帧已有明确角色时禁止覆盖，历史 Empty 不补入，也不把某槽候选顺延到相邻槽位。当前帧已经给出安全角色时直接进入 Reconciliation，不再额外调度历史 OCR。
5. 每次只处理当前 Guidance 步骤的 `Indexes`，且只写主程序中的可补槽位。安全角色允许把同槽 `Pending` 或 `CommittedEmpty` 升级为 `CommittedCharacter`，但绝不覆盖已有 `CommittedCharacter`。若 Guidance 已经越过该槽，安全角色以 `AutomaticSupplement` 模式补入同一固定槽位，仍播放正常角色动画，但补入本身不移动 Guidance。回看角色和当前帧角色进入同一候选校验与正常业务服务，不存在直接改集合的强行填入。
6. 普通 Ban/Pick 严格按视觉槽位 Index 写入。一个多槽 Ban 步骤中，若较早槽位具有明确、安全的 Empty 证据，且同一步骤更后槽位已经明确选择角色，则该空洞可提交为 `CommittedEmpty`；没有后续选择证明的当前/未来“未选择”仍保持 `Pending`。`CommittedEmpty` 会保留为空，除非同一固定槽位后来出现安全、明确的角色证据；这种证据只升级该空槽，不会移动角色或覆盖其他槽位。
7. 角色写入使用 `CurrentStep` 模式，向 `ICharacterSelectionService` 传 `playAnimation=true`；这就是识别选角应出现入场动画的路径。
8. 写入后重新读取 `BpSlotCommitState`。当前业务步骤仍未完成时立即停止，不能跨越。
9. 当前步骤完成且尚未到目标时，只调用一次 `NextStepAsync()`；循环逐步执行，自动路径禁止 `MoveToStepAsync()` 跳步。目标定位遵循“当前槽位证据 → 已完成的跨 Action 前置步骤 → 原地等待”的优先级：例如 `PickSur[0,1]` 已出现但 `BanSur[2]` 仍为 `Pending` 时，不能仅因 `PickSur[2]` 为空而前进；只有画面真正识别到槽位 2，才能据此证明中间 Ban 可能为空，并按 `BanSur[2] → PickSur[2]` 逐步追上。若中间 Ban 已由宿主确认完成，则停在 `PickSur[2]` 等待其角色出现，不回退。
10. 原地等待的优先级高于自动回退。同一 Action 的相邻出现即使 Indexes 不同，也不会自动回跳；较早 `CommittedEmpty` 槽位出现角色时只做同槽补充。只有 Guidance 至少领先槽位目标两个工作流步骤、当前 Action 已与目标 Action 不同，且目标槽位存在安全 `Selected` 强证据时，才认为引导明显超前。此时逐次调用 `PrevStepAsync()` 回到最早未满足的前置步骤，再按正常 `NextStepAsync()` 走到画面目标；仍禁止直接定位。

回看只服务真正触发的自动落后追赶。`OcrBackfillLookBehindSteps`、`RecognitionTransitionLookBehindMilliseconds`、`RecognitionTransitionReplayMinimumConfidence` 保留在高级参数中，默认分别为 2、800ms、0.95。切换捕获源会清空缓冲，对局 Guid 或进度不匹配的帧不会被读取。手动强制同步始终只使用用户触发时的当前帧，不读取回看结果。

#### 手动强制同步

强制同步由用户显式触发，对当前捕获帧执行一次独立、完整的四区域 OCR，不读写 SmartBP 累计业务状态：

1. 将本帧明确识别到的四类角色槽位直接通过 `ICharacterSelectionService` 写入主程序。
2. 所有角色选择、交换操作都传 `playAnimation=false`；强制同步不播放入场动画。
3. 当前或未来的“未选择”保持 `Pending`；仅目标步骤之前的明确 Empty 可提交为空，避免把尚未选择的后续槽位误判为空操作。
4. 角色写入后重新读取主程序槽位状态，在当前 phase 对应的同 Action 步骤中寻找最早未完成步骤。例如已选 2 名求生者、已 Ban 3 名求生者、已 Ban 2 名监管者，phase 为 `BanSur`，目标就是 `BanSur[3]`。
5. Guidance 对齐可直接调用 `MoveToStepAsync()`，不播放逐步追赶过程。角色同步和 Guidance 对齐分别返回结果；phase 无法解析时，明确角色仍保留在主程序中，Guidance 原位保持。

这里的“完整四区域 OCR”同时约束请求层与解析层。强制同步固定请求 `banned_sur`、`banned_hun`、`picked_sur`、`picked_hun`，并使用 `GlobalSnapshot` 字段解析上下文；当前 phase 和当前 Guidance Action 只参与最后的步骤定位，不得把非当前 Action 的区域过滤掉，也不得改变这些区域的行语义。例如当前画面为 `BanSur` 时，左下角已经可见的求生者选择仍按角色行/选手 ID 行解析，天赋或附加行只作为噪声忽略，然后与另外三个区域一起对账。只有本帧确实为 `Unknown` 的槽位才保持主程序原值。

旧的 `SmartBpRecognitionStateStore`、启发式进度评分、`WorkflowBackfill`、`TransitionReplay`、操作 ledger 和 `FreeFullSync` 自动模式均已移除，不存在隐藏 fallback。

在角色分配画面中，视觉槽位永远不直接当作内部 Pick 槽位覆盖 `SurPlayerList`。进入 `DistributeChara` 后采用唯一的“空位补充”例外：任一当前帧高置信、安全、名称唯一的角色证据，只要主程序尚未持有该角色且仍有空求生者槽，就依画面顺序填入第一个空槽，不要求四个分配槽全部齐全；之后只有具备安全玩家 ID 匹配的证据才按玩家身份交换到固定内部玩家位。普通 Ban/Pick 不使用这个策略，仍严格按原视觉槽位 Index。重复角色、低置信角色、无空位或玩家 ID 冲突都保持原状态。

### OCR Provider

SmartBP 支持三种 OCR Provider，一次只运行所选 Provider，不提供自动 fallback 或跨 Provider ensemble：

| Provider | 接口 | 适用场景 | 依赖 |
| --- | --- | --- | --- |
| PaddleOCR | `PaddleOcrProvider : IOcrProvider` | 默认引擎，BP 状态识别 + 赛后数据回填 | PaddleInference 原生库 + det/cls/rec 模型 |
| Tesseract | `TesseractOcrProvider : IOcrProvider` | 可选引擎，手动切换 | Tesseract 原生库 + `traineddata` 语言文件 |
| RapidOCR | `RapidOcrNetProvider : IOcrProvider` | 可选引擎，手动切换 | RapidOcrNet + 托管中文 det/cls/rec/dict |

`SmartBpOcrProviderSelector` 根据 `SmartBpRecognitionSettings.OcrProviderMode` 显式选择当前 Provider。Tesseract 的 `traineddata` 通过 `ITesseractDataAssetManager` 管理。RapidOCR 资产由 `IRapidOcrModelAssetManager` 下载到 `{SmartBpModuleRoot}\OCRModels\RapidOCR\Models\{profileId}`；只有选中 profile 的 det、cls、rec、dict 均存在且 RapidOcrNet 能初始化时才为 ready，不隐式使用包内拉丁模型。

RapidOCR manifest 预置中、日、英三个官方组合。检测、分类、识别模型和匹配字典的完整 ModelScope URL 摘自 RapidOCR 官方 `python/rapidocr/default_models.yaml`，不在代码中推导或拼接；模型使用官方 SHA-256，字典固定校验官方文件内容。下载经 `SmartBpParallelDownload` 复用统一服务的暂停、续传、重试和取消能力，并使用稳定下载暂存目录和安装完成后的原子提升。RapidOCR 将 `Mat` 在内存中转换为 `SKBitmap`，输出统一映射到输入粗区域坐标；可选预处理变体只在同尺寸图像上运行并去重。

每次安装会在 profile 目录写入 `.smartbp-install.json`，记录 profile、上游版本和内置 manifest 指纹。普通状态刷新比较安装记录与当前内置 manifest；版本或任一资产 URL、文件名、SHA-256、转换声明变化时提示更新。“检查模型更新”还会通过统一下载器临时读取 RapidOCR 官方 `default_models.yaml`，按当前识别模型的官方 ONNX URL 比较上游版本。若官方版本已领先内置 manifest，UI 会要求先更新 SmartBP 模块，不会把旧模型误标为可安装更新。旧版安装没有记录时标为“未知（旧版安装）”并提示更新，但现有完整模型仍可继续使用。

RapidOCR 与其他 OCR Provider 没有依赖关系。SmartBP 自动识别只使用所选 OCR Provider 读取文字与坐标；本地解释器负责当前帧证据，`CharacterSelectionService` 和主程序槽位提交状态负责业务语义，场景门禁与 Reconciliation 负责安全应用。

### 旧 AI 字段兼容

SmartBP 自动识别曾提供 Pure AI、AI+OCR、AI+AI OCR、业务 AI 融合和 AI OCR transcript 方案。这些路径及其对应的 Llama 服务器、Qwen 模型、融合模式、结构化输出、运行时更新检查等设置字段、相关枚举（`SmartBpRecognitionEngine` / `SmartBpRecognitionStrategy` / `AiStructuredOutputMode` / `SmartBpHybridFusionMode` 等）、守卫方法（`IsGameDataAiRecognitionSelected()`）和 UI 文本（`SmartBpGameDataAiRecognitionNotImplemented` 等）已彻底从代码库中删除。开发阶段无需向后兼容，不再保留任何旧配置 JSON 兼容代码。

### 识别设置总览

`SmartBpRecognitionSettings`（持久化到 `%APPDATA%\neo-bpsys-wpf\SmartBp\RecognitionSettings.json`）包含以下关键配置组：

| 配置组 | 关键字段 | 默认值 |
| --- | --- | --- |
| OCR BP | `EnableOcrBpRecognition`, `UseOcrContactSheet`, `OcrRecognitionIntervalMs` | true / true / 3000ms |
| OCR Provider | `SelectedOcrProviderMode`, `OcrProviderMode` | `Paddle` |
| Tesseract | `EnableTesseractOcr`, `TesseractLanguages`, `TesseractDefaultPsm` | true / "chi_sim+eng" / 6 |
| RapidOCR 模型 | `SelectedRapidOcrModelId` | "ppocr-v5-zh-mobile" |
| RapidOCR 推理 | `RapidOcrPadding`, `RapidOcrMaxSideLen`, `RapidOcrBoxScoreThreshold`, `RapidOcrBoxThreshold`, `RapidOcrUnclipRatio`, `RapidOcrUseAngleClassifier`, `RapidOcrUsePreprocessingVariants` | 0 / 1024 / 0.5 / 0.3 / 1.6 / true / false |
| 循环控制 | `RecognitionIntervalMs`, `OcrRecognitionIntervalMs` | 1200ms / 3000ms |
| 自动应用 | `EnableAutoApplyRecognition`, `EnableAutoGuidanceSync` | false / false |
| 稳定确认 | `RequiredStableSnapshots` | 1 |
| 图像编码 | `PhaseCropMaxImageWidth`, `ContentCropMaxImageWidth` | 640 / 768 |
| 有界采样与回看 | `RecognitionFrameBufferMilliseconds`, `RecognitionSamplingIntervalMilliseconds`, `OcrBackfillLookBehindSteps`, `RecognitionTransitionLookBehindMilliseconds`, `RecognitionTransitionReplayMinimumConfidence` | 1500ms / 150ms / 2 步 / 800ms / 0.95 |

## 区域配置

### 全流程 BP 识别粗区域配置（Recognition Layout）

用于 BP 状态识别，定义 5 个粗裁剪区域：

```text
%APPDATA%\neo-bpsys-wpf\SmartBp\BpRecognitionLayoutProfile.json
```

| 区域 ID | 枚举值 | 画面对应位置 | 识别内容 |
| --- | --- | --- | --- |
| `phase_top` | `PhaseTop` | 顶部操作栏 | BP 阶段文本（屏蔽/选择/天赋等） |
| `top_left_status` | `TopLeftStatus` | 画面绝对左上角 | 角色 BP 结束后的区域选择和等待开始标题；默认 16:9 区域为 `(0, 0, 0.36, 0.11)` |
| `left_top` | `LeftTop` | 左上角 | 监管者 Ban 位 |
| `right_top` | `RightTop` | 右上角 | 求生者 Ban 位 |
| `left_bottom` | `LeftBottom` | 左下角 | 求生者 Pick 位 |
| `right_bottom` | `RightBottom` | 右下角 | 监管者 Pick 位 |

默认配置来自 SmartBP 模块 `Resources` 中的 `BpRecognitionLayoutProfile.json`。用户可通过 `RegionEditorWindow` 在当前捕获帧或内置测试图上可视化调整六个粗区域（包括 `TopLeftStatus`）。保存后的同一份用户 profile 同时供编辑器预览和本地 OCR 状态检测读取。

自动循环会先 OCR `TopCenterStatus` / `TopLeftStatus`。本地规则以“求生者选择区域中”“监管者选择区域中”“等待游戏开始”三个标题为强锚点，并使用关键词与编辑距离容忍少量 OCR 错字；“剩余…秒”和“前往【…】”只作为辅助证据。命中后设置 post-BP latch，并在任何角色内容区域识别和字段合并前进入排空队列停止流程。

## 图像预处理

赛后数据回填先对完整捕获帧执行一次可返回文本边界框的 OCR。解析器以 `玩家名（角色名）` 文本建立有效行，按 Y 坐标排序；再取角色文本右侧的数字/空值标记，按 X 坐标推断五个统计列、按最近 Y 坐标归属行。名称左侧的等级、天赋数字不会进入数据列，也不会参与玩家或角色解析。

只有整表解析确认缺失的数据格会进入局部识别。局部图像固定来自当前显式选择的 OCR Provider，不会静默切换引擎；Paddle 使用不经过检测器的单区域字符识别，其他 Provider 使用各自的单文本区域模式。每个变体的原始文本、规范化数字、置信度、投票数和最终接受/拒绝结果都会写入统一识别日志窗口。

## 调试点

### 赛后数据 OCR

1. OCR 不工作先看是否已下载并切换模型。
2. 捕获不到数据先看窗口捕获服务是否处于 capturing。
3. 查看全流程 BP 调试选项中的赛后调试表格和统一识别日志窗口：其中列出 OCR 原始文本、边界框、行聚类、列归属、被排除的名称列文本，以及 `numeric fallback candidate/accepted/rejected` 局部识别结果。
4. 角色匹配失败时看日志中的 `SmartBp Match failed` 和 OCR 原始名称。

### BP 状态识别

1. 自动识别不工作先检查 `EnableOcrBpRecognition`、捕获状态以及自动应用/Guidance 同步开关
2. 场景门禁阻断写入时查看 `SmartBpSceneGateResult` 的 `Reason` 字段
3. OCR contact sheet 映射异常时，可设置 `UseOcrContactSheet = false` 逐区域识别排查
4. 角色解析失败时查看日志中的 `ocr-match` 诊断行，包含 `raw`、`result`、`matchMode`、`score` 等信息
5. 可开启 `ISmartBpDebugLog.IsEnabled`，在 SmartBP 页面 UI 中查看当前帧 OCR、历史回看、槽位对账、逐步追赶和强制同步诊断
6. 追赶问题重点查看 `Historical review supplemented`、`merge_mode=supplement-only`、`Guided catch-up target`、逐步 `advanced one step` 和 `earliest incomplete step`；强制同步问题查看 `playAnimation=false` 与最终目标 Action/Indexes

### 通用

1. 模块加载失败时检查 `SmartBpModuleState.json` 中的 `ModuleRoot` 路径和 `component.json` 校验结果
2. 原生依赖缺失时确认 `runtimes/win-x64/native/` 目录结构完整
3. OCR 推理失败重建可通过日志 `OCR run failed, trying to rebuild OCR predictor and retry once.` 确认

### 常见日志关键词

| 关键词 | 含义 |
| --- | --- |
| `SmartBp AutoFill skipped: OCR model is not ready.` | 未选择或未安装 OCR 模型 |
| `SmartBp AutoFill skipped: capture is not running.` | 窗口捕获未启动 |
| `SmartBp OCR Survivor` / `SmartBp OCR Hunter` | 行识别结果和原始 OCR 文本 |
| `SmartBp OCR RowData batch` | 整行数字条带一次 OCR 的结果 |
| `SmartBp OCR RowData fallback` | 批量识别失败后的逐列 OCR 回退结果 |
| `SmartBp Match success` | 求生者角色匹配成功，含 exact/fuzzy 模式 |
| `SmartBp Match failed` | 识别角色无法映射到当前对局求生者 |
| `OCR run failed, trying to rebuild OCR predictor and retry once.` | PaddleOCR 推理失败，正在重建后重试 |
| `OCR contact sheet: regions=[...]` | contact sheet 拼接信息，含区域列表和 unmapped 行数 |
| `phase line:` / `ocr-match` | BP 阶段文本行和 OCR 角色匹配诊断 |
| `OCR provider selected:` | 当前活跃的 OCR Provider |
| `Frame sequence N:` | 自动识别 tick 的帧序号和请求字段 |

## 关键数据模型速查

### 识别结果模型

| 模型 | 用途 |
| --- | --- |
| `SmartBpPhaseRecognitionResult` | 阶段识别结果（仅 `Phase` 字段） |
| `SmartBpBusinessStateRecognitionResult` | 合并后的业务状态（banned_sur/hun + picked_sur/hun） |
| `SmartBpFocusedBusinessExtractionResult` | 单个粗区域的业务提取结果 |
| `SmartBpSnapshotDeltaResult` | 增量识别 delta（仅含请求字段的更新） |
| `SmartBpSnapshotFieldUpdate` | 单个字段更新（banned_sur/banned_hun/picked_sur/picked_hun） |
| `SmartBpOcrRecognitionResult` | OCR BP 识别完整结果（阶段 + 业务状态 + 区域文本 + 诊断） |
| `SmartBpAutoRecognitionTickResult` | 一次自动识别 tick 的完整结果 |

### 槽位证据与宿主状态模型

| 模型 | 用途 |
| --- | --- |
| `SmartBpRecognizedCharacterSlot` | 单个角色槽位（含识别置信度、自动应用安全标记） |
| `SmartBpRecognizedPlayerCharacterSlot` | 绑定玩家 ID 的角色槽位（继承 `SmartBpRecognizedCharacterSlot`） |
| `SmartBpNormalizedCharacter` | 规范化角色解析结果（原始名 → 标准名 → 匹配模式） |
| `BpSlotCommitStateSnapshot` | 主程序权威槽位状态，区分 Pending / CommittedEmpty / CommittedCharacter |

### 操作与对账模型

| 模型 | 用途 |
| --- | --- |
| `SmartBpDetectedOperation` | 检测到的候选操作（Ban/Pick/Swap + 关联的 GameGuidance 步骤） |
| `SmartBpGuidanceSyncResult` | 引导同步结果（是否变更、是否接受、目标步骤） |
| `SmartBpReconciliationResult` | 角色、明确空操作与 Guidance 三部分独立结果 |

## 文件路径速查

| 路径 | 内容 |
| --- | --- |
| `%APPDATA%\neo-bpsys-wpf\SmartBpModuleState.json` | 模块安装状态（ModuleRoot 路径） |
| `%APPDATA%\neo-bpsys-wpf\SmartBp\BpRecognitionLayoutProfile.json` | BP 识别粗区域配置 |
| `%APPDATA%\neo-bpsys-wpf\SmartBp\RecognitionSettings.json` | 识别引擎设置 |
| `{SmartBpModuleRoot}\OCRModels\{modelKey}\` | PaddleOCR 模型文件（det/cls/rec） |
| `{SmartBpModuleRoot}\tessdata\` | Tesseract traineddata 语言文件 |
| `{SmartBpModuleRoot}\Resources\` | 内置默认配置、prompt、测试帧等资源 |
