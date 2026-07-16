# SmartBP 与 OCR

线程和后台任务注意事项见 [threading-dispatcher-and-async.md](threading-dispatcher-and-async.md)。SmartBP 默认配置和资源文件见 [resources-localization-and-assets.md](resources-localization-and-assets.md)。

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

两条线共享同一套窗口捕获、OCR 模型管理和粗裁剪区域配置基础设施，但区域配置（细区域 vs 粗区域）和结果写入路径完全独立。

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

实际实现 `SmartBpService` 位于主应用 `Services/` 目录，但依赖 `neo-bpsys-wpf.SmartBp.Module` 提供的 `IOcrService`、`ISmartBpRegionConfigService` 等。

### 模块入口与 DI

`SmartBpModuleEntryPoint` 是模块的单一入口点，实现 `ISmartBpModuleEntryPoint`：

- `CreateSmartBpContent(hostServices)`：构建独立的 `ServiceProvider`，注入所有模块级服务（OCR Provider、识别管线、debug log 等），创建 `SmartBpModuleContentView` 并绑定 `SmartBpModuleContentViewModel`
- `GetFeatureCommands()`：返回宿主可调用的功能命令，目前只有 `AutoFillGameData`（赛后数据回填）

模块 DI 容器注册了 OCR Provider（Paddle、Tesseract、RapidOCR）、BP 识别管线、场景门禁、状态管理、补录与应用等服务。

## 当前边界

| 能力 | 当前状态 |
| --- | --- |
| 赛后数据 OCR 自动回填 | 已经成熟且可用 |
| PaddleOCR BP 状态识别 | 默认 OCR Provider；读取文字与边界框，本地解析阶段、禁用、选择与玩家 ID |
| Tesseract BP 状态识别 | 可选 OCR Provider；可在 SmartBP 页面勾选下载 `chi_sim`/`eng`/`jpn` 到 SmartBP 模块目录，不会自动回退到 Paddle |
| RapidOCR BP 状态识别 | 可选本地 OCR Provider；使用 SmartBP 托管的中文 det/cls/rec/dict 资产，不会自动回退到其他 Provider |
| 本地视觉模型 + llama.cpp BP 状态识别 | 已从 SmartBP 模块移除；BP 状态识别仅支持 OCR Provider |
| GameGuidance 自动对齐 | 可选，默认关闭；只向前匹配当前或最近步骤 |
| 识别结果自动应用 | 可选，默认关闭；仅通过 `ICharacterSelectionService` 应用高置信度且已解析的角色操作 |
| 自由全同步（FreeFullSync） | 实验能力；不依赖 GameGuidance，识别四类角色槽位并通过 `ICharacterSelectionService` 无动画同步 |
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

Release 构建不会查询 GitHub latest release，而是通过 `gh-releases.plfjy.top` 转发 API 获取 release 列表，按当前应用版本 tag 精确匹配同一 release 下的 `SmartBpModuleManifest.json`。manifest 文件固定通过 `https://gh.plfjy.top/` 下载；官方模块 asset 是 `SmartBpModule.7z`，实际下载地址会套用设置中的 GitHub 下载镜像，跟随用户在设置页持久化保存的 `GhProxyMirror`。

远程版本检查只作为更新提示，不阻塞本地模块加载：ABI 兼容性由 `component.json` 的 `RuntimeAbiVersion` 硬性校验保证，本地模块只要通过目录、RID、ABI 和入口程序集校验就立即加载显示。加载成功后异步拉取远端 manifest，仅在本地版本低于要求版本时通过 `ModuleVersionOutdated` 事件触发 `IInfoBarService` 警告提示用户更新；拉取失败或网络不可达时静默跳过，不影响已加载模块使用。Preview 构建不进行在线检查，主要支持选择本地模块目录或导入 `SmartBpModule.7z` / 旧 `SmartBpModule.zip`。

SmartBP 模块在线安装和手动导入支持 `.7z` 与旧 `.zip` 包，归档格式通过文件内容探测，不只依赖扩展名。运行时解压使用 SharpCompress，用户不需要安装 7-Zip，也不需要 `7z.exe` 或 `7z.dll`。这只影响 SmartBP 模块包；`.bpui` / Designer v3 布局包导入导出行为不变。

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

## BP 状态 OCR 识别流程

BP 状态识别和赛后数据 OCR 是两条不同流程。BP 识别不直接写 `CurrentGame`，而是先生成 `SmartBpBusinessStateRecognitionResult` / `SmartBpSnapshotDeltaResult`，再进入已有状态合并、工作流补录、ledger 和 `SmartBpDetectedOperationApplier`。

默认引擎是 PaddleOCR：

1. 从当前捕获帧裁剪 `phase_top` 和 planner 请求的内容区域。
2. 默认把裁剪图按纵向拼成无文字标签的 OCR contact sheet，只运行一次 PaddleOCR。
3. 读取 `PaddleOcrResult.Regions` 中的文本、置信度和 `RotatedRect`，转换为按 `CenterY`、`CenterX` 排序的文本行与轴对齐边界框。
4. 按 contact sheet 坐标把文本行映射回 `phase_top`、`left_top`、`right_top`、`left_bottom`、`right_bottom`。
5. 本地规则根据 `phase_top` 文本和左右侧 X 坐标判断阶段；非活动侧 `等待中` 不覆盖活动阶段。
6. 本地解析四个粗区域：`right_top -> banned_sur`、`left_top -> banned_hun`、`left_bottom -> picked_sur`、`right_bottom -> picked_hun`。
7. 角色名只从 `ISharedDataService.SurCharaDict` / `HunCharaDict` 匹配；无法明确解析的 OCR 文本只进入诊断，不会应用为角色。

`UseOcrContactSheet = false` 时会逐区域 OCR，主要用于排查 contact sheet 映射问题。OCR 识别默认间隔较短，字段 stale 和回看步数使用 OCR 专用设置。旧配置中的 AI 策略值会在加载时回退为 `PureOcr`。

自动 BP 循环使用 `SmartBpRecognitionScene` 场景门禁。角色 BP 场景才允许生成和应用 Ban/Pick 操作；求生者/监管者天赋阶段只允许同步引导；大厅、规则、禁选顺序、转场不写入。区域选择、等待开始、加载和对局内会阻断当前帧的内容识别与新操作生成，并停止调度后续 tick；已经排队或正在应用的角色 BP 操作会继续完成，队列排空后才以 `SmartBpCharacterBpEnded` 正常完成 GameGuidance 和自动识别，不触发取消事件。区域选择不属于 MapBP 或角色 BP 识别范围。用户手动停止仍会立即取消当前识别。

PaddleOCR 模型、RapidOCR 模型和 Tesseract 语言文件统一使用 Downloader 的并行分片下载：单文件 8 个分片、最多 6 个并发连接、失败最多重试 5 次，并启用断点续传和下载前磁盘空间检查。多个安装资源仍按顺序处理，避免同时下载模型造成网络、内存与磁盘争抢；取消令牌会停止当前分片任务。

当前不识别 MapBP，不识别天赋结果，不直接修改 `CurrentGame`。

### 自动识别循环全流程

BP 状态自动识别的完整循环由 `SmartBpAutoRecognitionCoordinator` 协调，每个 tick 执行以下流程：

```text
捕获帧
  │
  ├─ 1. SmartBpFrameRingBuffer        ← 保存最近帧到滚动缓冲区
  ├─ 2. OCR TopCenterStatus / TopLeftStatus
  │     ├─ TopCenterStatus ← 阵营选择、天赋调整、即将进入区域选择等生命周期 gate
  │     └─ TopLeftStatus   ← 区域选择/等待开始 post-BP hard latch
  ├─ 3. SmartBpSnapshotRecognitionPlanner ← 根据工作流未完成步骤、字段新鲜度、最近阶段，规划需要刷新的内容区域
  ├─ 4. SmartBpOcrSnapshotDeltaRecognitionService
  │     ├─ SmartBpOcrContactSheetBuilder  ← 拼接 contact sheet
  │     ├─ IOcrService.RecognizeTextLines ← 一次所选 OCR Provider 推理
  │     ├─ SmartBpOcrContactSheetMapper   ← 文本行映射回区域
  │     ├─ SmartBpOcrPhaseClassifier      ← 本地规则判阶段
  │     └─ SmartBpOcrRegionParser         ← 本地解析 Ban/Pick 槽位与 player_id
  │
  ├─ 5. SmartBpRecognitionStateStore ← 使用 automatic guards 应用增量 delta
  ├─ 6. SmartBpSceneGateService      ← 场景门禁分类
  │     ├── CharacterBp → 允许生成角色操作
  │     ├── SurvivorTalent / HunterTalent → 仅允许同步引导
  │     ├── TalentLocked → 仅允许同步引导
  │     ├── 其他 → 阻断写入，可能暂停循环
  │
  ├─ 7. SmartBpCandidateOperationBuilder ← 从识别状态构建候选操作
  ├─ 8. SmartBpWorkflowBackfillService   ← 按工作流顺序补录未完成步骤
  ├─ 9. SmartBpDetectedOperationApplier  ← 应用高置信度操作（需用户启用）
  │     └── ICharacterSelectionService   ← 实际执行角色选择
  │
  └─ 10. SmartBpGuidanceSyncService      ← 同步 GameGuidance 步骤（需用户启用）
        └── SmartBpRecognitionLedger     ← 记录已完成操作，防止重复应用
```

### 场景门禁详解

`SmartBpSceneGateService.Classify()` 根据识别到的 phase 文本、业务状态和原始引擎响应，将当前画面分为以下场景：

| 场景 | 枚举值 | 角色操作 | 引导同步 | 暂停循环 | 触发条件 |
| --- | --- | --- | --- | --- | --- |
| 角色 BP | `CharacterBp` | 允许 | 允许 | 否 | 检测到屏蔽/选择/角色选择中等文本 |
| 求生者天赋 | `SurvivorTalent` | 禁止 | 允许 | 否 | 检测到"求生者天赋特质调整" |
| 监管者天赋 | `HunterTalent` | 禁止 | 允许 | 否 | 检测到"监管者天赋特质调整"/"监管者选择天赋中" |
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

### 工作流补录与操作应用

识别结果不是直接写入 `CurrentGame`，而是经过以下管线：

1. **`SmartBpCandidateOperationBuilder`**：从合并后的业务状态中构建候选操作（Ban 角色 / Pick 求生者 / Pick 监管者 / Swap 求生者），并关联到当前的 GameGuidance 工作流步骤
2. **`SmartBpWorkflowBackfillService`**：按工作流顺序，从尚未完成的角色步骤回填到当前步骤，生成 `SmartBpWorkflowBackfillPlan`
3. **`SmartBpDetectedOperationApplier`**：通过 `ICharacterSelectionService` 应用已解析的高置信度操作。应用模式包括：
   - `CurrentStep`：当前步骤操作（可播放动画）
   - `Backfill`：补录操作（默认不播放动画，可由用户开启）
   - `FreeSync`：无动画同步（不依赖 GameGuidance）
4. **`SmartBpRecognitionLedger`**：内存 ledger 记录已完成的操作 key，与当前状态 no-op 检查共同防止重复应用
5. **`SmartBpGuidanceSyncService`**：当识别到的阶段与当前 GameGuidance 步骤不一致时，尝试同步引导步骤。阶段快速进入天赋选择后，仍可利用画面中保留的角色结果补录上一选择步骤（通过短暂阶段切换提交屏障）

### OCR Provider

SmartBP 支持三种 OCR Provider，一次只运行所选 Provider，不提供自动 fallback 或跨 Provider ensemble：

| Provider | 接口 | 适用场景 | 依赖 |
| --- | --- | --- | --- |
| PaddleOCR | `PaddleOcrProvider : IOcrProvider` | 默认引擎，BP 状态识别 + 赛后数据回填 | PaddleInference 原生库 + det/cls/rec 模型 |
| Tesseract | `TesseractOcrProvider : IOcrProvider` | 可选引擎，手动切换 | Tesseract 原生库 + `traineddata` 语言文件 |
| RapidOCR | `RapidOcrNetProvider : IOcrProvider` | 可选引擎，手动切换 | RapidOcrNet + 托管中文 det/cls/rec/dict |

`SmartBpOcrProviderSelector` 根据 `SmartBpRecognitionSettings.OcrProviderMode` 显式选择当前 Provider。Tesseract 的 `traineddata` 通过 `ITesseractDataAssetManager` 管理。RapidOCR 资产由 `IRapidOcrModelAssetManager` 下载到 `{SmartBpModuleRoot}\OCRModels\RapidOCR\Models\{profileId}`；只有选中 profile 的 det、cls、rec、dict 均存在且 RapidOcrNet 能初始化时才为 ready，不隐式使用包内拉丁模型。

RapidOCR manifest 预置中、日、英三个官方组合。检测、分类、识别模型和匹配字典的完整 ModelScope URL 摘自 RapidOCR 官方 `python/rapidocr/default_models.yaml`，不在代码中推导或拼接；模型使用官方 SHA-256，字典固定校验官方文件内容。下载复用 `SmartBpParallelDownload` 的并行分片、续传、重试和取消能力，并使用临时目录和安装完成后的原子提升。RapidOCR 将 `Mat` 在内存中转换为 `SKBitmap`，输出统一映射到输入粗区域坐标；可选预处理变体只在同尺寸图像上运行并去重。

每次安装会在 profile 目录写入 `.smartbp-install.json`，记录 profile、上游版本和内置 manifest 指纹。普通状态刷新比较安装记录与当前内置 manifest；版本或任一资产 URL、文件名、SHA-256、转换声明变化时提示更新。“检查模型更新”还会通过统一下载器临时读取 RapidOCR 官方 `default_models.yaml`，按当前识别模型的官方 ONNX URL 比较上游版本。若官方版本已领先内置 manifest，UI 会要求先更新 SmartBP 模块，不会把旧模型误标为可安装更新。旧版安装没有记录时标为“未知（旧版安装）”并提示更新，但现有完整模型仍可继续使用。

RapidOCR 与其他 OCR Provider 没有依赖关系。SmartBP 自动识别只使用所选 OCR Provider 读取文字与坐标；本地解释器、`CharacterSelectionService`、StateStore、门禁和应用管线继续承担业务语义与安全合并。

### 旧 AI 字段兼容

SmartBP 自动识别曾提供 Pure AI、AI+OCR、AI+AI OCR、业务 AI 融合和 AI OCR transcript 方案。这些路径已从生产运行链路移除。`SmartBpRecognitionSettings` 仍可能保留若干旧字段用于读取历史配置，但加载时会把缺失或不支持的策略归一化为 `PureOcr`，UI 不再暴露相关模式、模型或服务器状态。

### 识别设置总览

`SmartBpRecognitionSettings`（持久化到 `%APPDATA%\neo-bpsys-wpf\SmartBp\RecognitionSettings.json`）包含以下关键配置组：

| 配置组 | 关键字段 | 默认值 |
| --- | --- | --- |
| 识别策略 | `RecognitionStrategy` | `PureOcr` |
| 兼容引擎字段 | `RecognitionEngine` | `Ocr` |
| OCR BP | `EnableOcrBpRecognition`, `UseOcrContactSheet`, `OcrRecognitionIntervalMs` | true / true / 300ms |
| OCR Provider | `SelectedOcrProviderMode`, `OcrProviderMode` | `Paddle` |
| Tesseract | `EnableTesseractOcr`, `TesseractLanguages`, `TesseractDefaultPsm` | true / "chi_sim+eng" / 6 |
| RapidOCR 模型 | `SelectedRapidOcrModelId` | "ppocr-v5-zh-mobile" |
| RapidOCR 推理 | `RapidOcrPadding`, `RapidOcrMaxSideLen`, `RapidOcrBoxScoreThreshold`, `RapidOcrBoxThreshold`, `RapidOcrUnclipRatio`, `RapidOcrUseAngleClassifier`, `RapidOcrUsePreprocessingVariants` | 0 / 1024 / 0.5 / 0.3 / 1.6 / true / false |
| 旧 AI 字段兼容 | 历史 AI 策略、模型与端口字段 | 加载后不参与 SmartBP 自动识别，策略回退为 `PureOcr` |
| 循环控制 | `RecognitionIntervalMs`, `OcrRecognitionIntervalMs` | 1200ms / 300ms |
| 自动应用 | `EnableAutoApplyRecognition`, `EnableAutoGuidanceSync` | false / false |
| 应用模式 | `RecognitionApplyMode` | `GuidedWorkflow` |
| 补录 | `PlayBackfillAnimations`, `AllowLateBackfillAfterPhaseMoved` | false / true |
| 状态管理 | `OcrFieldStaleMilliseconds`, `OcrBackfillLookBehindSteps`, `RequiredStableSnapshots` | 1500ms / 2 / 1 |
| 图像编码 | `MaxImageWidth`, `PhaseCropMaxImageWidth`, `ContentCropMaxImageWidth` | 1280 / 640 / 768 |
| 帧缓冲 | `RecognitionFrameBufferMilliseconds`, `RecognitionCropChangeThreshold` | 1500ms / 0.035 |

## 区域配置

SmartBP 管理两套独立的区域配置：

### 赛后数据细区域配置（GameData）

用于赛后数据 OCR 回填，定义每行（监管者行 + 4 个求生者行）内各单元格的精确位置：

```text
%APPDATA%\neo-bpsys-wpf\SmartBp\GameDataRegions.json
```

默认配置优先来自：

```text
neo-bpsys-wpf.SmartBp.Module/Resources/SmartBpDefaultConfigs/GameDataRegions.16-9.default.json
```

运行时从 SmartBP 模块输出目录读取该文件；如果资源缺失，`SmartBpGameDataSceneDefinition` 会生成代码内 fallback 配置。配置保存前会校验：

1. `Scene` 必须是 `GameData`。
2. 根节点行数为 5（1 监管者 + 4 求生者）。
3. 每行 6 个 cell（1 名称 + 5 数据列）。
4. 大框和小框相对坐标合法。
5. `BaseAspectRatio` 会被规范化，存储时优先保留比例基准。

用户可通过 `SmartBpModuleContentViewModel` 中的"编辑识别区域"按钮打开 `RegionEditorWindow`，在当前捕获帧上可视化调整各单元格位置。

### BP 识别粗区域配置（Recognition Layout）

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

默认配置来自 SmartBP 模块 `Resources` 中的 `BpRecognitionLayoutProfile.json`。用户可通过 `RegionEditorWindow` 在当前捕获帧或内置测试图上可视化调整六个粗区域（包括 `TopLeftStatus`）。保存后的同一份用户 profile 同时供编辑器预览和本地 OCR 状态检测读取。这套配置独立于赛后数据细区域配置。

自动循环会先 OCR `TopCenterStatus` / `TopLeftStatus`。本地规则以“求生者选择区域中”“监管者选择区域中”“等待游戏开始”三个标题为强锚点，并使用关键词与编辑距离容忍少量 OCR 错字；“剩余…秒”和“前往【…】”只作为辅助证据。命中后设置 post-BP latch，并在任何角色内容区域识别和字段合并前进入排空队列停止流程。

## 图像预处理

名称文本使用 `PreprocessForText`：放大、灰度、背景抑制、二值化、形态学、反色。

数字使用 `PreprocessForDigits`：放大、灰度、归一化、Otsu 二值化、闭运算、反色。

数据列识别优先把 5 个数字列拼成 strip 一次 OCR；如果解析不出 5 个数字，再逐列 OCR 回退。

## 调试点

### 赛后数据 OCR

1. OCR 不工作先看是否已下载并切换模型。
2. 捕获不到数据先看窗口捕获服务是否处于 capturing。
3. 识别错位先导出/检查 `GameDataRegions.json` 与实际画面比例。
4. 角色匹配失败时看日志中的 `SmartBp Match failed` 和 OCR 原始名称。
5. 预处理图像可临时用 `SaveDebug` 输出到运行目录 `debug`，但不要把调试图片提交进仓库。

### BP 状态识别

1. 自动识别不工作先检查 `SmartBpRecognitionSettings` 中 `RecognitionStrategy` 是否符合预期；纯 OCR 时还要检查 `EnableOcrBpRecognition` 是否为 `true`
2. 场景门禁阻断写入时查看 `SmartBpSceneGateResult` 的 `Reason` 字段
3. OCR contact sheet 映射异常时，可设置 `UseOcrContactSheet = false` 逐区域识别排查
4. 角色解析失败时查看日志中的 `ocr-match` 诊断行，包含 `raw`、`result`、`matchMode`、`score` 等信息
5. 可开启 `ISmartBpDebugLog.IsEnabled`，在 SmartBP 页面 UI 中查看 OCR 识别与状态合并诊断日志
6. 识别状态可通过 `SmartBpRecognitionStateStore.GetStaleFieldDiagnostics()` 查看各字段新鲜度

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

### 识别状态模型

| 模型 | 用途 |
| --- | --- |
| `SmartBpRecognitionState` | 内存中本地合并的识别状态（含字段新鲜度时间戳） |
| `SmartBpRecognizedCharacterSlot` | 单个角色槽位（含识别置信度、自动应用安全标记） |
| `SmartBpRecognizedPlayerCharacterSlot` | 绑定玩家 ID 的角色槽位（继承 `SmartBpRecognizedCharacterSlot`） |
| `SmartBpNormalizedCharacter` | 规范化角色解析结果（原始名 → 标准名 → 匹配模式） |

### 操作/补录模型

| 模型 | 用途 |
| --- | --- |
| `SmartBpDetectedOperation` | 检测到的候选操作（Ban/Pick/Swap + 关联的 GameGuidance 步骤） |
| `SmartBpWorkflowStepCandidateSet` | 一个工作流步骤的一组候选操作 |
| `SmartBpWorkflowBackfillPlan` | 按工作流顺序排列的补录计划 |
| `SmartBpWorkflowOperationKey` | 操作 ledger 的唯一标识（GameProgress + StepIndex + Action + SlotIndex + Camp + CharacterKey） |
| `SmartBpGuidanceSyncResult` | 引导同步结果（是否变更、是否接受、目标步骤） |

## 文件路径速查

| 路径 | 内容 |
| --- | --- |
| `%APPDATA%\neo-bpsys-wpf\SmartBpModuleState.json` | 模块安装状态（ModuleRoot 路径） |
| `%APPDATA%\neo-bpsys-wpf\SmartBp\GameDataRegions.json` | 赛后数据细区域配置 |
| `%APPDATA%\neo-bpsys-wpf\SmartBp\BpRecognitionLayoutProfile.json` | BP 识别粗区域配置 |
| `%APPDATA%\neo-bpsys-wpf\SmartBp\RecognitionSettings.json` | 识别引擎设置 |
| `{SmartBpModuleRoot}\OCRModels\{modelKey}\` | PaddleOCR 模型文件（det/cls/rec） |
| `{SmartBpModuleRoot}\tessdata\` | Tesseract traineddata 语言文件 |
| `{SmartBpModuleRoot}\Resources\` | 内置默认配置、prompt、测试帧等资源 |
