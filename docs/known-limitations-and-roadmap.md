# 已知边界与路线图提醒

本文面向维护者和 AI agent，用于避免把当前边界误判为已完成能力。

## SmartBP

| 能力 | 状态 |
| --- | --- |
| 赛后数据 OCR 自动回填 | 成熟且可用；整表 OCR 后按文本边界框重建行列 |
| PaddleOCR BP 状态识别 | 默认 OCR Provider；读取文字与边界框，本地解析阶段、禁用、选择与玩家 ID |
| PaddleOCR NVIDIA CUDA 加速 | 可选；拥有受支持的 NVIDIA GPU 时，用户可在 SmartBP 设置卡片下载匹配的 CUDA runtime 组件并启用 GPU 推理；CPU/GPU native runtime 物理隔离，故障自动回退 CPU，不预装进主安装包 |
| Tesseract BP 状态识别 | 可选 OCR Provider；可在 SmartBP 页面勾选下载 `chi_sim`/`eng`/`jpn` 到 SmartBP 模块目录，不会自动回退到 Paddle |
| 本地视觉模型 + llama.cpp BP 状态识别 | 已从 SmartBP 模块移除；BP 状态识别仅支持 OCR Provider |
| GameGuidance 自动追赶 | 可选；统一 Reconciliation 按槽位和前置业务状态逐步调用 `NextStepAsync()`；引导落后时会从同局短时原始帧补充遗漏角色，不允许跳过未完成步骤；原地等待优先于回退 |
| 识别结果自动应用 | 可选；仅对宿主 `Pending` 槽位通过 `ICharacterSelectionService` 应用高置信度角色或目标之前的明确空操作；自动选角播放正常动画 |
| 手动强制同步 | 当前帧固定全图解析四类 Ban/Pick 区域，不受当前 phase/Guidance Action 过滤；角色以 `playAnimation=false` 直接写入主程序，随后按宿主槽位直接定位 Guidance；不作为自动模式运行 |
| 自动 BP 画面切换 | TODO |

自动循环通过细分场景门禁限制写入：角色 BP 中应用当前角色操作；求生者/监管者天赋调整阶段只允许用画面中仍可见的对应阵营 Ban/Pick 字段完成最终角色补写并同步引导；区域选择、等待开始、加载和对局内会丢弃延迟结果并暂停。区域选择和 MapBP 均不在此识别范围内。

SmartBP 自动循环以区域门控 OCR 为入口，不使用手工任务选择作为正常入口。每个活动 BP tick 在识别当前 phase 后读取四类角色槽位，避免上一帧 phase 预先删掉本帧追赶所需的较早字段。OCR 默认使用 Paddle，也可由用户显式切换为 Tesseract 或 RapidOCR；一次只运行所选 Provider，不提供自动 fallback 或 ensemble。Provider 只读取文字、置信度和边界框；阶段、禁用、选择、玩家 ID 均由本地规则、区域 ID、固定槽位几何和角色候选字典解释。未识别槽位为 `Unknown`，只有明确空槽位证据才是 `Empty`，缺失的前位或中间位不会令后续 Ban 候选左移。

识别层不跨帧维护第二份 Ban/Pick 业务状态。轻量捕获按独立于 OCR 的较高频率写入绑定 GameGuid/GameProgress 的有界滚动原始帧缓冲；容量下限考虑 OCR 周期和已观测处理耗时，旧局帧不会作为新局证据。自动链路先比较 `Action + 规范化 Indexes`，并检查目标之前的宿主 Pending 槽位洞；对齐且没有新槽位证据时不会执行 Reconciliation 或历史 OCR。宿主槽位已提交不能单独证明同 Action 下一组已开始；只有下一组固定槽位被明确识别，或两组之间的其他 Action 已完成，才提高目标。中间 Ban 未完成时保留在前一 Pick 组；下一 Pick 槽出现后才允许据此确认并逐步跨过明确空 Ban。真正落后且 Guidance 未超过目标时，默认只回看目标之前两个工作流步骤，每个步骤最多选择一张代表帧，并只识别 Phase 和该 Action 对应区域；Phase/Action 不对齐的帧整帧拒绝。历史结果只为宿主 `Pending` 或 `CommittedEmpty` 且当前帧未明确选择的槽位补充高置信角色，不补 Empty、不覆盖当前角色或宿主 `CommittedCharacter`，也不将后续角色前移。安全角色可把同槽 `CommittedEmpty` 升级为 `CommittedCharacter`，该补充播放正常动画但不移动 Guidance。原地等待优先于回退；只有 Guidance 至少领先目标两个工作流步骤、当前 Action 已不同且目标槽位有安全 `Selected` 强证据时，才逐步回退到最早未满足前置步骤并重新逐步前进。补充结果只存在于本次对账，随后仍由 Reconciliation 按工作流顺序应用；若仍缺少某一步所需的明确证据，自动追赶会停在该最早未完成步骤。`DistributeChara` 之后允许把安全且唯一的缺失角色补进第一个宿主空位，再依据玩家 ID 做必要交换；没有实际补位或交换操作时不会重复触发。步骤变化继续复用 GameGuidance 原有导航、计时器、高亮与事件逻辑。手动强制同步不回看历史帧。地图 BP、天赋角色操作和自动切屏仍未实现。

BP 识别路径读取 SmartBP 模块资源中的 `BpRecognitionLayoutProfile.json` 粗裁剪配置。用户可通过通用 `RegionEditorWindow` 在当前捕获帧或内置测试图上可视化调整全部区域，结果保存为 AppData 下的 SmartBP recognition profile 覆盖；运行时与编辑器读取同一份配置。自动循环每 tick 先 OCR `TopCenterStatus`，用归一化、编辑距离和关键词覆盖评分识别阵营选择、双方天赋调整及即将进入区域选择；未知结果只跳过本 tick，弱 transition 需连续两次确认，强 transition 会立即阻止新内容识别并进入排空停止。`TopLeftStatus` 保留为稍晚出现的区域选择/等待开始硬确认，两种区域不会合并使用。post-BP latch 设置后，后续阶段波动不会恢复内容 OCR、字段合并或候选操作生成。这套配置独立于 PaddleOCR 赛后数据 OCR 细区域。Tesseract traineddata、Paddle/RapidOCR 模型、内置测试帧、OCR 别名和 SmartBP 默认区域配置均随 SmartBP 模块 `Resources` 或模块目录管理，不放在主程序 `Resources`。AppData 中只保存纯配置（如 `SmartBp/RecognitionSettings.json`、`SmartBp/BpRecognitionLayoutProfile.json`）。默认仍为识别预览；只有用户显式启用自动应用后，才会通过角色选择服务应用已解析候选。不识别 MapBP。

## 插件系统

插件是全信任模型，不是沙箱。当前风险控制是插件市场审核、微步云恶意文件扫描、人工审查和较小的插件生态。

插件加载发生在启动期间、Host build 前。安装或更新插件后需要重启，不能假设支持热加载或热更新。

`Assembly.LoadFrom` 加载入口程序集，依赖解析主要依赖插件目录、宿主已有程序集和 .NET 默认上下文。插件打包时不要漏掉自身直接依赖，也不要把宿主已有依赖重复打包成冲突版本。

## 版本概念

插件 API 版本和 PluginSdk 源码引用版本是故意分离的：

| 概念 | 用途 |
| --- | --- |
| 插件 API 版本 | `manifest.yml` 的 `apiVersion`，宿主加载兼容性 |
| PluginSdk 源码引用版本 | 插件项目编译和打包用的 SDK 源码提交 |

v3 起不再发布 PluginSdk NuGet 包；插件项目应 clone 本仓库并通过 `ProjectReference` 包含 `neo-bpsys-wpf.PluginSdk`。不要把 `apiVersion` 和 SDK 源码提交不同步当作错误。

## Designer v3 当前状态

Fronted Designer v3 已完成基础设施环节，所有内置前台窗口均已接入 v3 renderer：

| 窗口 | v3 layout host | 状态 |
| --- | --- | --- |
| `ScoreSurWindow` / `ScoreHunWindow` | `FrontedWindowBase -> ViewBox -> BaseCanvas` | v3 renderer，绑定 `MatchScore` |
| `ScoreGlobalWindow` | `FrontedWindowBase -> ViewBox -> BaseCanvas` | v3 renderer + `GlobalScoreRow`，BO3/BO5 canvas states |
| `CutSceneWindow` | `FrontedWindowBase -> ViewBox -> BaseCanvas` | v3 renderer，业务控件封装 |
| `GameDataWindow` | `FrontedWindowBase -> ViewBox -> BaseCanvas` | v3 renderer，表头 `LocalizedText` |
| `BpOverviewWindow` | `FrontedWindowBase -> ViewBox -> BaseCanvas` | v3 renderer，原 Widgets overview 独立窗口 |
| `MapV2Window` | `FrontedWindowBase -> ViewBox -> BaseCanvas` | v3 renderer，保留 MapV2 |
| `BpWindow` | `FrontedWindowBase -> ViewBox -> BaseCanvas` | v3 renderer + 内置 Transition / Loop 行为 |

Designer v3 独立编辑器（`FrontedDesignerWindow`）已实现并作为设计编辑器唯一入口。旧版真实窗口设计器模式、SettingPage 旧前台自定义入口和旧位置保存/恢复 API 已移除。

### 已知边界

1. v3 layout window 的窗口 Width/Height 已由 `WindowSettings.WindowWidth` / `WindowHeight` 独立保存。`CanvasSettings.CanvasWidth` / `CanvasHeight` 只表示内部设计画布尺寸；legacy canvas-centric 转换缺少窗口尺寸时才用 Canvas 尺寸初始化窗口尺寸。传统固定 XAML window 仍由原窗口逻辑管理尺寸。
2. SettingPage 旧 `.bpui` import/export UI 入口已删除。旧 `.bpui` 现在通过 `FrontManagePage` 的 Layout Packages 管理，会触发 v3 转换，不再覆盖全局 Config.json。旧 Config 字段已移入 legacy DTO / 转换器 / 迁移代码，不再作为 active `Settings.cs` 运行时属性。
3. Resource Browser 控件级浏览不复制/导入外部图片。
4. 运行时关键控件名称只读且不能删除。被其他控件引用的普通控件在 reference-aware rename/delete 实现前阻止改名和删除。
5. `CurrentBanDisplay`、`BanSlotDisplay` 和 `PickingBorderOverlay` 已移除，不再作为兼容控件读取。新布局推荐使用 `Image` / `BorderedImage` 的 `Lockable` 与 `PickingBorderAvailable` overlay，它们的内部覆盖层不作为普通可选/可编辑/可添加控件。
6. `.bpui v3` 导出、导入/安装、激活复制和删除已实现；导出固定为全部前台布局。
7. v3 导出器不会写入全局 `Config.json`、`CustomUi/` 或 `FrontElementsConfig/`。
8. 编辑器手写输入按上限截断，外部导入超限 JSON/manifest/layout/package 会拒绝。
9. 图片按用途限制大小和像素，resolver 对坏图安全返回 `null`。
10. Canvas 控件数 160 warning、256 hard limit。
11. 编辑器支持内部 `Ctrl+C` / `Ctrl+V` 控件复制粘贴且不抢文本输入控件的普通复制粘贴。
12. 插件 `ControlType` 标准为 `plugin:<PackageId>/<ControlTypeName>`，Canvas 可声明并由编辑器/导出器同步 `RequiredPlugins`，`.bpui` manifest 汇总 `PluginDependencies`。
13. 缺失插件窗口布局和控件配置会保留，Designer 显示 MissingPlugin 占位符，直播前台 runtime 跳过缺失插件控件并记录 warning。安装/更新后仍需重启，不把新插件当作当前进程已加载继续导入。
14. `.bpui` 不得包含插件 DLL、安装包或脚本；导入器会拒绝这类条目。

## 文档边界

公开 VuePress 文档面向用户，可能落后于 UI 或内部实现。仓库内 `/docs` 面向维护者和 AI agent，应跟随代码架构变化更新。

## 代码中观察到的边界

1. SmartBP 已覆盖固定 Ban 槽位、Unknown/Empty、Observation TTL、跨局隔离、缓冲容量和 Guidance 补偿等自动回归；真实赛事画面、不同缩放与 OCR Provider 组合仍需持续扩充样本。
2. `App.xaml.cs` 更新检查条件写作 `#if !DEBUG && !Preview`，而项目配置定义 `PREVIEW`。这是代码观察到的命名 caveat；本文档不声称其运行时效果已经通过编译验证，本任务也不修改代码。
3. `GameRule.json` 是项目内规则配置，不是外部权威赛事规则源。
4. 前台默认布局依赖文件命名约定，插件窗口默认布局缺失时恢复默认会失败。

## Score System v2

比分系统已迁移到现有 `Core.Models.Game` 持有权威状态，详见 [score-system-v2.md](business/score-system-v2.md)。当前代码仍存在这些边界：

| 边界 | 说明 |
| --- | --- |
| `Team.Score` 语义混杂 | 运行时不再从 `MatchScoreState` 同步它；仅保留旧 JSON/旧 DTO 反序列化兼容，不得重新作为权威状态。 |
| `ScoreGlobalWindow` BO3/BO5 状态 | v3 已使用通用 Canvas BO states，不依赖旧 `MajorGameGap` / `HalfGameGap`。 |
| `GameProgress.Free` 未定义比分语义 | Score System v2 暂把它记录为设计缺口。 |
| `Game3Overtime*` 与 `Game4*` enum 数值重叠 | `MatchScoreService` 结合 BO3/BO5 状态解析；缺少上下文的保守按 BO5 第四局解析。 |
| 旧记录 `Team.Score` 无法还原完整历史 | 旧 JSON 没有 `MatchScore` 时会创建默认 `MatchScoreState`，不会从 `Team.Score` 反推出 per-Game/per-Half 结果。 |
