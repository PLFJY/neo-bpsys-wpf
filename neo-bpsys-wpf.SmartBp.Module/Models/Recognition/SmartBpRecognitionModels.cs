using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Abstractions.Services;
using OpenCvSharp;

namespace neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

/// <summary>支持的 AI 识别任务。</summary>
public enum SmartBpRecognitionTask { DetectStage, BanSur, BanHun, PickSur, PickHun, CharacterDistribution, FullBpScan }

/// <summary>用于门控 BP 识别的第五人格细粒度场景。</summary>
public enum SmartBpRecognitionScene
{
    Unknown, Lobby, RulesDialog, BanPickOrderDialog, Transition, CharacterBp,
    SurvivorTalent, HunterTalent, TalentLocked, AreaSelectionSurvivor,
    AreaSelectionHunter, WaitingGameStart, Loading, InGame, OutOfBp
}

/// <summary>BP 场景门控产出的决策。</summary>
public sealed record SmartBpSceneGateResult(
    SmartBpRecognitionScene Scene,
    bool IsBpRecognitionAllowed,
    bool IsCharacterOperationAllowed,
    bool ShouldPauseAutomaticRecognition,
    string Reason);

/// <summary>根据 OCR 文本本地识别的顶部中间角色 BP 生命周期分类。</summary>
public enum SmartBpLifecycleCategory
{
    /// <summary>生命周期状态无法可靠识别。</summary>
    Unknown,
    /// <summary>角色禁选正在进行。</summary>
    CharacterBpActive,
    /// <summary>求生者正在调整天赋和特质。</summary>
    SurvivorTalentAdjust,
    /// <summary>监管者正在调整天赋和特质。</summary>
    HunterTalentAdjust,
    /// <summary>角色 BP 正在转入区域选择。</summary>
    TransitionToAreaSelection
}

/// <summary>顶部中间 BP 生命周期状态的确定性本地分类。</summary>
public sealed class SmartBpLifecycleStatusResult
{
    /// <summary>获取最佳候选是否达到弱识别阈值。</summary>
    public bool IsRecognized { get; init; }
    /// <summary>获取匹配到的规范状态。</summary>
    public string Status { get; init; } = "未知";
    /// <summary>获取匹配到的生命周期分类。</summary>
    public SmartBpLifecycleCategory Category { get; init; }
    /// <summary>获取加权模糊匹配分数。</summary>
    public double Score { get; init; }
    /// <summary>获取原始 OCR 证据。</summary>
    public string Evidence { get; init; } = "";
    /// <summary>获取归一化后的 OCR 文本。</summary>
    public string NormalizedText { get; init; } = "";
    /// <summary>获取是否发现辅助去向行。</summary>
    public bool HasDestinationEvidence { get; init; }
    /// <summary>获取检测器诊断信息。</summary>
    public IReadOnlyList<string> Diagnostics { get; init; } = [];
}

/// <summary>场景/阶段控制器产出的决策。</summary>
public sealed class SmartBpScenePhaseDecision
{
    /// <summary>获取识别到的场景。</summary>
    public SmartBpRecognitionScene Scene { get; init; }
    /// <summary>获取识别到的阶段文本。</summary>
    public string Phase { get; init; } = "未知";
    /// <summary>获取该场景是否允许 BP 识别。</summary>
    public bool BpRecognitionAllowed { get; init; }
    /// <summary>获取该场景是否允许角色操作。</summary>
    public bool CharacterOperationAllowed { get; init; }
    /// <summary>获取自动识别是否应暂停。</summary>
    public bool ShouldPauseAutomaticRecognition { get; init; }
    /// <summary>获取下一步提取推荐的业务字段。</summary>
    public IReadOnlyList<string> RecommendedFields { get; init; } = [];
    /// <summary>获取便于阅读的决策原因。</summary>
    public string Reason { get; init; } = "";
}

/// <summary>SmartBP 粗粒度识别裁剪区域。</summary>
public enum SmartBpRecognitionRegion
{
    /// <summary>BP 阶段标题区域。</summary>
    PhaseTop,
    /// <summary>顶部中间角色 BP 生命周期/状态区域。</summary>
    TopCenterStatus,
    /// <summary>绝对左上角全局游戏状态区域。</summary>
    TopLeftStatus,
    /// <summary>左侧上方 BP 内容区域。</summary>
    LeftTop,
    /// <summary>右侧上方 BP 内容区域。</summary>
    RightTop,
    /// <summary>左侧下方 BP 内容区域。</summary>
    LeftBottom,
    /// <summary>右侧下方 BP 内容区域。</summary>
    RightBottom
}

/// <summary>控制单次快照识别多少个 BP 内容区域。</summary>
public enum SmartBpRegionSnapshotRecognitionMode { FullAllRegions, PendingAndCurrentRegions }

/// <summary>标识协调器在一个 tick 中使用的识别路径。</summary>
public enum SmartBpRecognitionPath
{
    /// <summary>由于没有请求业务字段，仅识别阶段裁剪图。</summary>
    PhaseOnly,
    /// <summary>对请求字段运行逐字段独立快照识别。</summary>
    FieldSnapshot,
    /// <summary>将四个业务字段都作为独立字段快照识别。</summary>
    FullFieldSnapshot,
    /// <summary>旧版多图快照增量识别（模型侧增量更新）。</summary>
    LegacyDelta
}

/// <summary>归一化识别裁剪矩形。</summary>
public sealed class SmartBpRecognitionRegionRect
{
    /// <summary>获取或设置归一化左坐标。</summary>
    public double X { get; set; }
    /// <summary>获取或设置归一化上坐标。</summary>
    public double Y { get; set; }
    /// <summary>获取或设置归一化宽度。</summary>
    public double Width { get; set; }
    /// <summary>获取或设置归一化高度。</summary>
    public double Height { get; set; }
}

/// <summary>SmartBP 粗粒度识别布局配置档。</summary>
public sealed class SmartBpRecognitionLayoutProfile
{
    /// <summary>获取或设置 schema 版本。</summary>
    public int SchemaVersion { get; set; } = 1;
    /// <summary>获取或设置配置档标识。</summary>
    public string Id { get; set; } = "idv-default-16x9";
    /// <summary>获取或设置基础宽高比标签。</summary>
    public string BaseAspectRatio { get; set; } = "16:9";
    /// <summary>获取或设置按 JSON 标识索引的归一化区域。</summary>
    public Dictionary<string, SmartBpRecognitionRegionRect> Regions { get; set; } = [];
    /// <summary>获取该配置档的运行时来源，用于裁剪诊断。</summary>
    [JsonIgnore]
    public string RuntimeSource { get; set; } = "default";
}

/// <summary>一张带诊断信息的识别裁剪帧。</summary>
public sealed record SmartBpCroppedFrame(
    SmartBpRecognitionRegion Region,
    BitmapSource Image,
    int X,
    int Y,
    int Width,
    int Height)
{
    /// <summary>获取紧凑的像素矩形描述。</summary>
    public string PixelRectText => $"x={X}, y={Y}, width={Width}, height={Height}";
    /// <summary>获取用于计算该裁剪图的布局来源。</summary>
    public string LayoutSource { get; init; } = "default";
    /// <summary>获取用于计算该裁剪图的归一化矩形。</summary>
    public string NormalizedRectText { get; init; } = "";
}

/// <summary>区域门控识别使用的仅阶段模型输出。</summary>
public sealed class SmartBpPhaseRecognitionResult
{
    /// <summary>获取或设置检测到的中文 BP 阶段。</summary>
    [JsonPropertyName("phase")] public string Phase { get; set; } = "未知";
}

/// <summary>角色 BP 结束后状态检测的归一化结果。</summary>
public sealed class SmartBpPostBpStatusResult
{
    /// <summary>获取证据是否指向 BP 后场景。</summary>
    public bool IsPostBp { get; init; }
    /// <summary>获取归一化阶段。</summary>
    public string Phase { get; init; } = "未知";
    /// <summary>获取归一化场景。</summary>
    public SmartBpRecognitionScene Scene { get; init; } = SmartBpRecognitionScene.Unknown;
    /// <summary>获取检测原因。</summary>
    public string Reason { get; init; } = "";
    /// <summary>获取来源证据。</summary>
    public string Evidence { get; init; } = "";
    /// <summary>获取模糊匹配分数。</summary>
    public double Score { get; init; }
    /// <summary>获取用于匹配的归一化 OCR 文本。</summary>
    public string NormalizedText { get; init; } = "";
    /// <summary>获取状态裁剪图中发现的辅助证据标签。</summary>
    public IReadOnlyList<string> AuxiliaryEvidence { get; init; } = [];
}

/// <summary>托管 RapidOCR 模型配置档的根文档。</summary>
public sealed class RapidOcrModelManifest
{
    /// <summary>获取或设置清单 schema 版本。</summary>
    public int SchemaVersion { get; set; } = 1;
    /// <summary>获取或设置可用模型配置档集合。</summary>
    public List<RapidOcrModelProfile> Models { get; set; } = [];
}

/// <summary>一组托管 RapidOCR 检测器、方向分类器、识别器和字典。</summary>
public sealed class RapidOcrModelProfile
{
    /// <summary>获取或设置稳定配置档标识。</summary>
    public string Id { get; set; } = "";
    /// <summary>获取或设置面向用户的配置档名称。</summary>
    public string DisplayName { get; set; } = "";
    /// <summary>获取或设置面向用户的配置档描述。</summary>
    public string Description { get; set; } = "";
    /// <summary>获取或设置上游 RapidOCR 模型清单版本。</summary>
    public string Version { get; set; } = "";
    /// <summary>获取或设置检测器资产。</summary>
    public RapidOcrModelAsset Det { get; set; } = new();
    /// <summary>获取或设置方向分类器资产。</summary>
    public RapidOcrModelAsset Cls { get; set; } = new();
    /// <summary>获取或设置识别器资产。</summary>
    public RapidOcrModelAsset Rec { get; set; } = new();
    /// <summary>获取或设置识别字典资产。</summary>
    public RapidOcrModelAsset Dict { get; set; } = new();
}

/// <summary>一个可下载 RapidOCR 模型资产。</summary>
public sealed class RapidOcrModelAsset
{
    /// <summary>获取或设置安装后的文件名。</summary>
    public string FileName { get; set; } = "";
    /// <summary>获取或设置仓库相对源路径。</summary>
    public string RemotePath { get; set; } = "";
    /// <summary>获取或设置从 RapidOCR 默认模型清单复制的官方直链下载 URL。</summary>
    public string DownloadUrl { get; set; } = "";
    /// <summary>获取或设置下载源期望的 SHA-256。</summary>
    public string? Sha256 { get; set; }
    /// <summary>获取或设置确定性源转换；支持值为 Direct 和 PaddleCharacterDictionaryYaml。</summary>
    public string Transform { get; set; } = "Direct";
}

/// <summary>一个 RapidOCR 模型配置档的已解析安装路径。</summary>
/// <param name="ProfileId">配置档标识。</param>
/// <param name="Directory">配置档目录。</param>
/// <param name="DetPath">检测器路径。</param>
/// <param name="ClsPath">方向分类器路径。</param>
/// <param name="RecPath">识别器路径。</param>
/// <param name="DictPath">字典路径。</param>
public sealed record RapidOcrInstalledPaths(
    string ProfileId,
    string Directory,
    string DetPath,
    string ClsPath,
    string RecPath,
    string DictPath);

/// <summary>托管 RapidOCR 模型就绪信息。</summary>
/// <param name="ProfileId">已选配置档标识。</param>
/// <param name="ModelDirectory">已选配置档目录。</param>
/// <param name="IsInstalled">是否所有必需文件都已安装。</param>
/// <param name="MissingFiles">缺失的安装文件名。</param>
/// <param name="IsUsingFallback">是否正在使用内置兜底资产。</param>
/// <param name="InstalledVersion">已安装的上游模型版本（如果已记录）。</param>
/// <param name="LatestVersion">内置清单声明的版本。</param>
/// <param name="HasUpdate">已安装版本或配置档指纹是否过期。</param>
public sealed record RapidOcrModelStatus(
    string ProfileId,
    string ModelDirectory,
    bool IsInstalled,
    IReadOnlyList<string> MissingFiles,
    bool IsUsingFallback = false,
    string? InstalledVersion = null,
    string? LatestVersion = null,
    bool HasUpdate = false);

/// <summary>已安装 RapidOCR 配置档与内置/官方清单的比较结果。</summary>
/// <param name="InstalledVersion">已安装版本（如果已记录）。</param>
/// <param name="BundledVersion">SmartBP 内置清单中可用的版本。</param>
/// <param name="OfficialVersion">RapidOCR 官方清单当前引用的版本。</param>
/// <param name="HasInstallableUpdate">内置配置档是否可更新已安装文件。</param>
/// <param name="IsBundledManifestCurrent">SmartBP 内置配置档是否匹配官方清单版本。</param>
public sealed record RapidOcrModelUpdateCheckResult(
    string? InstalledVersion,
    string BundledVersion,
    string OfficialVersion,
    bool HasInstallableUpdate,
    bool IsBundledManifestCurrent);

/// <summary>已持久化的 AI 识别设置。</summary>
public sealed class SmartBpRecognitionSettings
{
    /// <summary>获取或设置 schema 版本。</summary>
    public int SchemaVersion { get; set; } = 1;
    /// <summary>获取或设置中文 UI 是否使用 HuggingFace 镜像。</summary>
    public bool UseHuggingFaceMirrorForChineseUi { get; set; } = true;
    /// <summary>获取或设置可选 HuggingFace 端点覆盖值。</summary>
    public string HuggingFaceEndpointOverride { get; set; } = "";
    /// <summary>获取或设置循环间隔。</summary>
    public int RecognitionIntervalMs { get; set; } = 1200;
    /// <summary>获取或设置推荐最小间隔。</summary>
    public int MinRecognitionIntervalMs { get; set; } = 500;
    /// <summary>获取或设置推荐最大间隔。</summary>
    public int MaxRecognitionIntervalMs { get; set; } = 5000;
    /// <summary>获取或设置所需稳定预览帧数。</summary>
    public int RequiredStableFrames { get; set; } = 2;
    /// <summary>获取或设置识别后的冷却时间。</summary>
    public int PostRecognitionCooldownMs { get; set; } = 1200;
    /// <summary>获取或设置忙碌时是否丢帧。</summary>
    public bool DropFrameWhenBusy { get; set; } = true;
    /// <summary>获取或设置进程优先级。</summary>
    public string ProcessPriority { get; set; } = "BelowNormal";
    /// <summary>获取或设置 CPU 线程数。</summary>
    public int CpuThreads { get; set; } = 2;
    /// <summary>获取或设置自动模式是否可以同步 GameGuidance。</summary>
    public bool EnableAutoGuidanceSync { get; set; } = true;
    /// <summary>获取或设置引导同步是否跟随后台页面导航。</summary>
    public bool EnableAutoGuidancePageNavigation { get; set; }
    /// <summary>获取或设置是否可以应用已接受操作。</summary>
    public bool EnableAutoApplyRecognition { get; set; } = true;
    /// <summary>获取或设置最小阶段置信度。</summary>
    public double StageConfidenceThreshold { get; set; } = 0.80;
    /// <summary>获取或设置自动应用前所需的匹配快照数量。</summary>
    public int RequiredStableSnapshots { get; set; } = 1;
    /// <summary>获取或设置自动识别是否使用单次多图快照增量请求。</summary>
    public bool UseMultiImageSnapshotRequest { get; set; } = true;
    /// <summary>获取或设置是否使用旧版模型侧快照增量识别替代字段快照。</summary>
    public bool UseLegacySnapshotDeltaRecognition { get; set; }
    /// <summary>获取或设置是否启用 OCR BP 识别。</summary>
    public bool EnableOcrBpRecognition { get; set; } = true;
    /// <summary>获取或设置 OCR BP 循环间隔。</summary>
    public int OcrRecognitionIntervalMs { get; set; } = 3000;
    /// <summary>获取或设置实测最小 OCR 间隔。</summary>
    public int MinimumOcrRecognitionIntervalMs { get; set; }
    /// <summary>获取或设置实测最小识别间隔。</summary>
    public int MinimumRecognitionIntervalMs { get; set; }
    /// <summary>获取或设置最近一次识别速度测量时间。</summary>
    public DateTimeOffset? LastRecognitionSpeedTestAt { get; set; }
    /// <summary>获取或设置最近一次速度测试使用的引擎标签。</summary>
    public string LastRecognitionSpeedTestEngine { get; set; } = "";
    /// <summary>获取或设置影响性能的配置指纹。</summary>
    public string LastRecognitionSpeedTestConfigurationHash { get; set; } = "";
    /// <summary>获取或设置自动落后追赶最多回看的前置工作流步骤数。</summary>
    public int OcrBackfillLookBehindSteps { get; set; } = 2;
    /// <summary>获取或设置 OCR 是否将裁剪图合成为一张拼接图。</summary>
    public bool UseOcrContactSheet { get; set; } = true;
    /// <summary>获取或设置是否启用 OCR 调试覆盖层输出。</summary>
    public bool EnableOcrDebugOverlay { get; set; }
    /// <summary>获取或设置显式选择的 OCR Provider。</summary>
    public SmartBpOcrProviderMode OcrProviderMode { get; set; } = SmartBpOcrProviderMode.Paddle;
    /// <summary>获取或设置基于策略识别时显式选择的 OCR Provider。</summary>
    public SmartBpOcrProviderMode SelectedOcrProviderMode { get; set; } = SmartBpOcrProviderMode.Paddle;
    /// <summary>获取或设置已选托管 RapidOCR 配置档标识。</summary>
    public string SelectedRapidOcrModelId { get; set; } = "ppocr-v5-zh-mobile";
    /// <summary>获取或设置 RapidOCR 检测器输入边距。</summary>
    public int RapidOcrPadding { get; set; }
    /// <summary>获取或设置 RapidOCR 旧版最长边缩放上限。</summary>
    public int RapidOcrMaxSideLen { get; set; } = 1024;
    /// <summary>获取或设置 RapidOCR DB 框分数阈值。</summary>
    public double RapidOcrBoxScoreThreshold { get; set; } = 0.5;
    /// <summary>获取或设置 RapidOCR DB 位图阈值。</summary>
    public double RapidOcrBoxThreshold { get; set; } = 0.3;
    /// <summary>获取或设置 RapidOCR DB 多边形扩张比例。</summary>
    public double RapidOcrUnclipRatio { get; set; } = 1.6;
    /// <summary>获取或设置 RapidOCR 是否运行方向分类器。</summary>
    public bool RapidOcrUseAngleClassifier { get; set; } = true;
    /// <summary>获取或设置 RapidOCR 是否额外尝试对比度增强灰度图。</summary>
    public bool RapidOcrUsePreprocessingVariants { get; set; }
    /// <summary>获取或设置旧版外部 Tesseract tessdata 目录值；托管下载会忽略该路径。</summary>
    public string TesseractDataPath { get; set; } = "";
    /// <summary>获取或设置 Tesseract 语言表达式。</summary>
    public string TesseractLanguages { get; set; } = "chi_sim+eng";
    /// <summary>获取或设置默认 Tesseract 页面分割模式。</summary>
    public int TesseractDefaultPsm { get; set; } = 6;
    /// <summary>获取或设置选中时是否允许使用 Tesseract。</summary>
    public bool EnableTesseractOcr { get; set; } = true;
    /// <summary>获取或设置 Tesseract 预处理变体最大数量。</summary>
    public int TesseractMaxPreprocessVariants { get; set; } = 3;
    /// <summary>获取或设置应用当前步骤动画操作前的可选延迟。</summary>
    public int RecognitionVisualBufferMilliseconds { get; set; }
    /// <summary>获取或设置多图识别失败时是否允许使用逐区域顺序请求。</summary>
    public bool AllowSequentialSnapshotFallback { get; set; } = true;
    /// <summary>获取或设置自动 JSON schema 是否使用完整候选枚举。</summary>
    public bool UseStrictCandidateEnumsInAutoSchema { get; set; }
    /// <summary>获取或设置阶段裁剪图最大编码宽度。</summary>
    public int PhaseCropMaxImageWidth { get; set; } = 640;
    /// <summary>获取或设置内容裁剪图最大编码宽度。</summary>
    public int ContentCropMaxImageWidth { get; set; } = 768;
    /// <summary>获取或设置仅阶段响应 token 预算。</summary>
    public int PhaseMaxTokens { get; set; } = 48;
    /// <summary>获取或设置增量快照增量 token 预算。</summary>
    public int SnapshotDeltaMaxTokens { get; set; } = 768;
    /// <summary>获取或设置滚动识别帧缓冲长度。</summary>
    public int RecognitionFrameBufferMilliseconds { get; set; } = 1500;
    /// <summary>获取或设置自动落后追赶可读取历史代表帧的时间窗口。</summary>
    public int RecognitionTransitionLookBehindMilliseconds { get; set; } = 800;
    /// <summary>获取或设置历史帧补充角色证据所需的最低置信度。</summary>
    public double RecognitionTransitionReplayMinimumConfidence { get; set; } = .95;
    /// <summary>获取或设置轻量画面采样间隔；该间隔独立于 OCR 周期。</summary>
    public int RecognitionSamplingIntervalMilliseconds { get; set; } = 150;
    /// <summary>获取或设置裁剪图变化阈值。</summary>
    public double RecognitionCropChangeThreshold { get; set; } = 0.035;
    /// <summary>获取或设置优先要求的稳定裁剪观测帧数。</summary>
    public int RecognitionCropStableFrames { get; set; } = 2;
}

/// <summary>面向模型的 BP 业务状态识别结果。</summary>
public sealed class SmartBpBusinessStateRecognitionResult
{
    /// <summary>获取或设置检测到的 BP 阶段。</summary>
    [JsonPropertyName("phase")] public string Phase { get; set; } = "未知";
    /// <summary>获取或设置求生者禁用槽位。</summary>
    [JsonPropertyName("banned_sur")] public List<SmartBpRecognizedCharacterSlot> BannedSur { get; set; } = [];
    /// <summary>获取或设置监管者禁用槽位。</summary>
    [JsonPropertyName("banned_hun")] public List<SmartBpRecognizedCharacterSlot> BannedHun { get; set; } = [];
    /// <summary>获取或设置求生者选择或分配槽位。</summary>
    [JsonPropertyName("picked_sur")] public List<SmartBpRecognizedPlayerCharacterSlot> PickedSur { get; set; } = [];
    /// <summary>获取或设置监管者选择槽位。</summary>
    [JsonPropertyName("picked_hun")] public SmartBpRecognizedPlayerCharacterSlot PickedHun { get; set; } = new();
    /// <summary>
    /// 获取或设置求生者分配阶段识别到的视觉槽位证据。
    /// 仅在求生者选择锁定后使用，按 player_id 匹配内部玩家位置，不再按视觉槽位索引直接覆盖内部状态。
    /// </summary>
    [JsonIgnore] public List<SmartBpRecognizedPlayerCharacterSlot> DistributionEvidence { get; set; } = [];
}

/// <summary>一个已识别角色槽位。</summary>
public class SmartBpRecognizedCharacterSlot
{
    /// <summary>获取或设置视觉槽位索引。</summary>
    [JsonPropertyName("index")] public int Index { get; set; }
    /// <summary>获取或设置模型原始角色名称或“未选择”。</summary>
    [JsonPropertyName("character_name")] public string CharacterName { get; set; } = "未选择";
    /// <summary>获取或设置当前帧对该视觉槽位的证据状态。</summary>
    [JsonIgnore] public SmartBpRecognizedSlotState SlotState { get; set; } = SmartBpRecognizedSlotState.Unknown;
    /// <summary>获取或设置本地 OCR 匹配置信度；模型 JSON 不序列化该元数据。</summary>
    [JsonIgnore] public double RecognitionConfidence { get; set; }
    /// <summary>获取或设置本地 OCR 匹配是否可安全自动应用。</summary>
    [JsonIgnore] public bool IsAutoApplySafe { get; set; }
    /// <summary>获取或设置 OCR 匹配诊断原因。</summary>
    [JsonIgnore] public string? RecognitionReason { get; set; }
    /// <summary>获取或设置 OCR 文本框在裁剪区域局部坐标中的位置。</summary>
    [JsonIgnore] public Rect? BoundingBox { get; set; }
}

/// <summary>一个绑定玩家的已识别角色槽位。</summary>
public sealed class SmartBpRecognizedPlayerCharacterSlot : SmartBpRecognizedCharacterSlot
{
    /// <summary>获取或设置可见玩家 ID（如果存在）。</summary>
    [JsonPropertyName("player_id")] public string? PlayerId { get; set; }
}

/// <summary>单个业务裁剪区域的聚焦模型输出。</summary>
public sealed class SmartBpFocusedBusinessExtractionResult
{
    /// <summary>获取或设置选择该聚焦区域的阶段。</summary>
    [JsonPropertyName("phase")] public string Phase { get; set; } = "未知";
    /// <summary>获取或设置目标业务字段。</summary>
    [JsonPropertyName("target_field")] public string TargetField { get; set; } = "";
    /// <summary>获取或设置禁用或求生者选择区域的聚焦槽位。</summary>
    [JsonPropertyName("slots")] public List<SmartBpRecognizedPlayerCharacterSlot> Slots { get; set; } = [];
    /// <summary>获取或设置聚焦监管者选择槽位。</summary>
    [JsonPropertyName("picked_hun")] public SmartBpRecognizedPlayerCharacterSlot? PickedHun { get; set; }
}

/// <summary>根据阶段裁剪图和四个内容裁剪图生成的本地合并 BP 快照。</summary>
public sealed class SmartBpRegionSnapshot
{
    /// <summary>获取权威阶段识别结果。</summary>
    public SmartBpPhaseRecognitionResult Phase { get; init; } = new();
    /// <summary>获取右上求生者禁用提取结果。</summary>
    public SmartBpFocusedBusinessExtractionResult? BannedSurRegion { get; init; }
    /// <summary>获取左上监管者禁用提取结果。</summary>
    public SmartBpFocusedBusinessExtractionResult? BannedHunRegion { get; init; }
    /// <summary>获取左下求生者选择提取结果。</summary>
    public SmartBpFocusedBusinessExtractionResult? PickedSurRegion { get; init; }
    /// <summary>获取右下监管者选择提取结果。</summary>
    public SmartBpFocusedBusinessExtractionResult? PickedHunRegion { get; init; }
    /// <summary>获取合并后的简化业务状态。</summary>
    public SmartBpBusinessStateRecognitionResult BusinessState { get; init; } = new();
    /// <summary>获取全部裁剪诊断信息。</summary>
    public IReadOnlyList<string> Diagnostics { get; init; } = [];
    /// <summary>获取模型使用的阶段裁剪图。</summary>
    public SmartBpCroppedFrame? PhaseCrop { get; init; }
    /// <summary>获取模型使用的四个内容裁剪图。</summary>
    public IReadOnlyList<SmartBpCroppedFrame> ContentCrops { get; init; } = [];
    /// <summary>获取按逻辑区域索引的模型原始响应。</summary>
    public IReadOnlyDictionary<string, string> RawResponses { get; init; } = new Dictionary<string, string>();
}

/// <summary>多图快照请求中包含的一张裁剪区域图。</summary>
public sealed record SmartBpMultimodalRegionInput(string Id, SmartBpRecognitionRegion Region, string TargetField, string ImageDataUrl);

/// <summary>一个请求的增量快照识别包。</summary>
public sealed record SmartBpSnapshotDeltaRequest(IReadOnlyList<(SmartBpRecognitionRegion Region, string TargetField)> RequestedRegions,
    IReadOnlyList<string> Diagnostics,
    SmartBpBusinessStateRecognitionResult? CurrentKnownState = null)
{
    /// <summary>获取请求的业务内容字段。</summary>
    public IReadOnlyList<string> RequestedFields => RequestedRegions.Select(item => item.TargetField).Distinct(StringComparer.Ordinal).ToArray();
}

/// <summary>模型可见的单个增量快照槽位状态。</summary>
public enum SmartBpRecognizedSlotState
{
    /// <summary>裁剪图明确显示该槽位已有选中角色。</summary>
    Selected,
    /// <summary>裁剪图明确显示该槽位为空或未选择。</summary>
    Empty,
    /// <summary>裁剪图不够可靠，本地合并应保留先前状态。</summary>
    Unknown
}

/// <summary>快照增量响应中的单个槽位更新。</summary>
public sealed class SmartBpSnapshotDeltaSlot
{
    /// <summary>获取或设置视觉槽位索引。</summary>
    [JsonPropertyName("index")]
    public int Index { get; set; }

    /// <summary>获取或设置 selected、empty 或 unknown 槽位证据。</summary>
    [JsonPropertyName("slot_state")]
    public string SlotState { get; set; } = "unknown";

    /// <summary>获取或设置识别到的候选角色名称或“未选择”。</summary>
    [JsonPropertyName("character_name")]
    public string CharacterName { get; set; } = "未选择";

    /// <summary>获取或设置槽位属于已选角色时的可见玩家 ID。</summary>
    [JsonPropertyName("player_id")]
    public string? PlayerId { get; set; }

    /// <summary>获取或设置原始识别置信度；仅在本地流水线中传播。</summary>
    [JsonIgnore] public double RecognitionConfidence { get; set; }

    /// <summary>获取或设置该证据是否允许自动应用；仅在本地流水线中传播。</summary>
    [JsonIgnore] public bool IsAutoApplySafe { get; set; }

    /// <summary>获取或设置识别或拒绝原因；仅在本地流水线中传播。</summary>
    [JsonIgnore] public string? RecognitionReason { get; set; }

    /// <summary>获取或设置 OCR 文本框在裁剪区域局部坐标中的位置。</summary>
    [JsonIgnore] public Rect? BoundingBox { get; set; }
}

/// <summary>包含阶段和请求字段更新的增量模型输出。</summary>
public sealed class SmartBpSnapshotDeltaResult
{
    /// <summary>获取或设置检测到的当前阶段。</summary>
    [JsonPropertyName("phase")] public string Phase { get; set; } = "未知";
    /// <summary>获取或设置请求字段更新集合。</summary>
    [JsonPropertyName("updates")] public List<SmartBpSnapshotFieldUpdate> Updates { get; set; } = [];
}

/// <summary>快照增量结果中的单个字段更新。</summary>
public sealed class SmartBpSnapshotFieldUpdate
{
    /// <summary>获取或设置业务字段标识。</summary>
    [JsonPropertyName("field")] public string Field { get; set; } = "";
    /// <summary>获取或设置 banned_sur、banned_hun 或 picked_sur 的槽位集合。</summary>
    [JsonPropertyName("slots")] public List<SmartBpSnapshotDeltaSlot>? Slots { get; set; }
    /// <summary>字段为 picked_hun 时，获取或设置监管者选择槽位。</summary>
    [JsonPropertyName("picked_hun")] public SmartBpSnapshotDeltaSlot? PickedHun { get; set; }
}

/// <summary>面向模型的旧版 BP 阶段检测结果。</summary>
public sealed class SmartBpStageDetectionResult
{
    /// <summary>获取或设置 schema 版本。</summary>
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; set; }
    /// <summary>获取或设置已识别动作。</summary>
    [JsonPropertyName("recognized_action")] public string RecognizedAction { get; set; } = "Unknown";
    /// <summary>获取或设置活动侧。</summary>
    [JsonPropertyName("active_side")] public string ActiveSide { get; set; } = "unknown";
    /// <summary>获取或设置操作区域。</summary>
    [JsonPropertyName("operation_region")] public string OperationRegion { get; set; } = "unknown";
    /// <summary>获取或设置操作归属方。</summary>
    [JsonPropertyName("operation_owner")] public string OperationOwner { get; set; } = "unknown";
    /// <summary>获取或设置目标阵营。</summary>
    [JsonPropertyName("target_camp")] public string TargetCamp { get; set; } = "unknown";
    /// <summary>获取或设置左上标题。</summary>
    [JsonPropertyName("left_top_title")] public string? LeftTopTitle { get; set; }
    /// <summary>获取或设置右上标题。</summary>
    [JsonPropertyName("right_top_title")] public string? RightTopTitle { get; set; }
    /// <summary>获取或设置主状态。</summary>
    [JsonPropertyName("main_status")] public string? MainStatus { get; set; }
    /// <summary>获取或设置置信度。</summary>
    [JsonPropertyName("confidence")] public double Confidence { get; set; }
    /// <summary>获取或设置证据。</summary>
    [JsonPropertyName("evidence")] public List<string> Evidence { get; set; } = [];
    /// <summary>获取或设置警告。</summary>
    [JsonPropertyName("warnings")] public List<string> Warnings { get; set; } = [];
}

/// <summary>聚焦 BP 操作提取结果。</summary>
public sealed class SmartBpFocusedExtractionResult
{
    /// <summary>获取或设置 schema 版本。</summary>
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; set; }
    /// <summary>获取或设置任务。</summary>
    [JsonPropertyName("task")] public string Task { get; set; } = "";
    /// <summary>获取或设置操作区域。</summary>
    [JsonPropertyName("operation_region")] public string OperationRegion { get; set; } = "unknown";
    /// <summary>获取或设置目标阵营。</summary>
    [JsonPropertyName("target_camp")] public string TargetCamp { get; set; } = "unknown";
    /// <summary>获取或设置提取出的槽位。</summary>
    [JsonPropertyName("slots")] public List<SmartBpVisionSlot> Slots { get; set; } = [];
    /// <summary>获取或设置警告。</summary>
    [JsonPropertyName("warnings")] public List<string> Warnings { get; set; } = [];
}

/// <summary>本地控制的已检测操作类型。</summary>
public enum SmartBpDetectedOperationKind
{
    /// <summary>提交 Ban 角色。</summary>
    BanCharacter,
    /// <summary>提交求生者角色。</summary>
    PickSurvivor,
    /// <summary>提交监管者角色。</summary>
    PickHunter,
    /// <summary>交换已经提交的求生者角色。</summary>
    SwapSurvivors,
    /// <summary>提交明确为空的 Ban 槽位。</summary>
    CommitEmptyBan,
    /// <summary>提交明确为空的求生者 Pick 槽位。</summary>
    CommitEmptySurvivorPick,
    /// <summary>提交明确为空的监管者 Pick 槽位。</summary>
    CommitEmptyHunterPick
}

/// <summary>控制单个已检测操作的工作流校验和动画行为。</summary>
public enum SmartBpDetectedOperationApplyMode
{
    /// <summary>按当前对局引导步骤应用操作，并播放正常角色过渡动画。</summary>
    CurrentStep,
    /// <summary>以自动识别的强槽位证据补充先前提交为空的槽位，播放动画但不改变当前引导步骤。</summary>
    AutomaticSupplement,
    /// <summary>强制同步当前画面，不播放角色动画且不校验当前工作流步骤。</summary>
    FreeSync
}

/// <summary>根据聚焦视觉提取派生出的预览候选操作。</summary>
public sealed record SmartBpDetectedOperation(SmartBpDetectedOperationKind Kind, GameAction SourceGuidanceAction,
    IReadOnlyList<int> SourceGuidanceIndexes, Camp Camp, int SlotIndex, string? RawCharacterName,
    string? ResolvedCharacterName, string? PlayerId, double Confidence, string Reason,
    int? SourceWorkflowStepIndex = null,
    SmartBpDetectedOperationApplyMode ApplyMode = SmartBpDetectedOperationApplyMode.CurrentStep,
    string? DependencyGroup = null,
    bool RequireEmptySurvivorSlot = false);

/// <summary>
/// 一条不可变、短生命周期且绑定对局上下文的视觉识别证据。
/// </summary>
/// <param name="GameGuid">识别时的对局标识。</param>
/// <param name="GameProgress">识别时的对局进度。</param>
/// <param name="FrameSequence">捕获帧序号。</param>
/// <param name="Timestamp">捕获或识别时间。</param>
/// <param name="Phase">当前帧识别阶段。</param>
/// <param name="Field">业务字段。</param>
/// <param name="VisualSlotIndex">固定视觉槽位索引。</param>
/// <param name="SlotState">槽位证据状态。</param>
/// <param name="CharacterCandidate">角色候选名称。</param>
/// <param name="PlayerId">可见玩家标识。</param>
/// <param name="Confidence">原始识别置信度。</param>
/// <param name="IsAutoApplySafe">是否允许自动应用。</param>
/// <param name="Reason">识别或拒绝原因。</param>
/// <param name="BoundingBox">OCR 文本框局部坐标。</param>
public sealed record SmartBpObservation(
    Guid GameGuid,
    GameProgress GameProgress,
    long FrameSequence,
    DateTimeOffset Timestamp,
    string Phase,
    string Field,
    int VisualSlotIndex,
    SmartBpRecognizedSlotState SlotState,
    string? CharacterCandidate,
    string? PlayerId,
    double Confidence,
    bool IsAutoApplySafe,
    string Reason,
    Rect? BoundingBox);

/// <summary>将已检测阶段与 GameGuidance 对齐后的结果。</summary>
public sealed record SmartBpGuidanceSyncResult(bool Changed, bool IsAccepted, string Reason, GameAction? TargetAction,
    IReadOnlyList<int> TargetIndexes, int? TargetStepIndex);

/// <summary>SmartBP 精确进度同步结果。</summary>
/// <param name="Succeeded">同步流程是否成功完成。</param>
/// <param name="Moved">是否实际移动了 GameGuidance 步骤。</param>
/// <param name="PreviousStepIndex">同步前步骤索引。</param>
/// <param name="TargetStepIndex">目标步骤索引。</param>
/// <param name="TargetAction">目标动作。</param>
/// <param name="TargetIndexes">目标步骤索引集合。</param>
/// <param name="Message">用户可读结果消息。</param>
/// <param name="Diagnostics">详细诊断信息。</param>
public sealed record SmartBpProgressSyncResult(
    bool Succeeded,
    bool Moved,
    int? PreviousStepIndex,
    int? TargetStepIndex,
    GameAction? TargetAction,
    IReadOnlyList<int> TargetIndexes,
    string Message,
    IReadOnlyList<string> Diagnostics);

/// <summary>SmartBP 手动对局状态同步结果。</summary>
/// <param name="ProgressSync">对局引导进度同步结果。</param>
/// <param name="ApplyResult">角色状态操作应用结果；进度同步失败时为 <see langword="null"/>。</param>
/// <param name="Diagnostics">完整诊断信息。</param>
public sealed record SmartBpGameStateSyncResult(
    SmartBpProgressSyncResult ProgressSync,
    SmartBpOperationApplyResult? ApplyResult,
    IReadOnlyList<string> Diagnostics,
    SmartBpOperationApplyResult? EmptyApplyResult = null);

/// <summary>统一对账的触发模式。</summary>
public enum SmartBpReconciliationMode
{
    /// <summary>自动识别对账，只允许安全向前移动 Guidance。</summary>
    Automatic,
    /// <summary>用户强制同步，角色与 Guidance 结果分别返回。</summary>
    ManualForceSync
}

/// <summary>
/// SmartBp Observation 与主程序权威状态的一次统一对账结果。
/// </summary>
/// <param name="CharacterApplyResult">角色提交结果。</param>
/// <param name="EmptyApplyResult">明确空操作提交结果。</param>
/// <param name="GuidanceResult">Guidance 对齐结果。</param>
/// <param name="Diagnostics">完整诊断。</param>
public sealed record SmartBpReconciliationResult(
    SmartBpOperationApplyResult CharacterApplyResult,
    SmartBpOperationApplyResult EmptyApplyResult,
    SmartBpProgressSyncResult GuidanceResult,
    IReadOnlyList<string> Diagnostics);

/// <summary>构建预览候选操作的结果。</summary>
public sealed record SmartBpCandidateOperationBuildResult(
    IReadOnlyList<SmartBpDetectedOperation> Operations,
    IReadOnlyList<string> Messages);

/// <summary>OCR player_id 与内部求生者玩家的匹配结果。</summary>
/// <param name="IsMatched">是否匹配到唯一内部玩家。</param>
/// <param name="Index">匹配到的内部求生者玩家索引；未匹配为 -1。</param>
/// <param name="DisplayName">匹配到的内部玩家显示名；未匹配为 <see langword="null"/>。</param>
/// <param name="Score">匹配分数，范围 [0, 1]。</param>
/// <param name="IsSafe">是否可安全用于自动应用。</param>
/// <param name="Reason">匹配或拒绝原因。</param>
public sealed record SmartBpPlayerIdentityMatchResult(
    bool IsMatched,
    int Index,
    string? DisplayName,
    double Score,
    bool IsSafe,
    string Reason)
{
    /// <summary>创建一个未匹配的默认结果。</summary>
    /// <param name="reason">未匹配原因。</param>
    /// <returns>未匹配结果。</returns>
    public static SmartBpPlayerIdentityMatchResult Unmatched(string reason) => new(false, -1, null, 0, false, reason);
}

/// <summary>应用已接受候选操作的结果。</summary>
public sealed record SmartBpOperationApplyResult(int AppliedCount, int SkippedCount, IReadOnlyList<string> Messages);

/// <summary>一次自动识别流水线结果。</summary>
public sealed record SmartBpAutoRecognitionTickResult(SmartBpBusinessStateRecognitionResult? BusinessState,
    SmartBpPhaseRecognitionResult? PhaseResult, SmartBpFocusedBusinessExtractionResult? FocusedResult,
    SmartBpCroppedFrame? PhaseCrop, SmartBpCroppedFrame? FocusedCrop,
    SmartBpGuidanceSyncResult? GuidanceSync, GameGuidanceRuntimeSnapshot GuidanceSnapshot,
    IReadOnlyList<SmartBpDetectedOperation> Operations, IReadOnlyList<string> CandidateMessages,
    SmartBpOperationApplyResult? ApplyResult, string RawJson, string? Error,
    SmartBpRegionSnapshot? RegionSnapshot = null,
    IReadOnlyList<SmartBpCroppedFrame>? ContentCrops = null,
    SmartBpSceneGateResult? SceneGate = null,
    SmartBpProgressSyncResult? ProgressSync = null);

/// <summary>SmartBP 滚动识别帧缓冲中保留的一帧画面。</summary>
/// <param name="Sequence">帧序号。</param>
/// <param name="Frame">捕获画面。</param>
/// <param name="Timestamp">捕获时间。</param>
/// <param name="GameGuid">捕获时的对局标识。</param>
/// <param name="GameProgress">捕获时的对局进度。</param>
public sealed record SmartBpBufferedFrame(
    long Sequence,
    BitmapSource Frame,
    DateTimeOffset Timestamp,
    Guid GameGuid,
    GameProgress GameProgress);

/// <summary>轻量裁剪图变化分析结果。</summary>
public sealed record SmartBpCropChangeResult(SmartBpRecognitionRegion Region, long Sequence, double Difference, bool IsChanged, bool IsStable);

/// <summary>按单个 SmartBP 粗粒度识别区域分组的 OCR 文本。</summary>
public sealed class SmartBpOcrRegionText
{
    /// <summary>获取来源粗粒度区域。</summary>
    public SmartBpRecognitionRegion Region { get; init; }
    /// <summary>获取使用区域局部坐标的 OCR 文本行。</summary>
    public IReadOnlyList<OcrTextLine> Lines { get; init; } = [];
}

/// <summary>基于 OCR 的 SmartBP BP 识别结果。</summary>
public sealed class SmartBpOcrRecognitionResult
{
    /// <summary>获取本地分类出的 BP 阶段。</summary>
    public SmartBpPhaseRecognitionResult Phase { get; init; } = new();
    /// <summary>获取针对请求 OCR 区域本地解析出的业务状态。</summary>
    public SmartBpBusinessStateRecognitionResult BusinessState { get; init; } = new();
    /// <summary>获取按粗粒度区域分组的 OCR 文本。</summary>
    public IReadOnlyList<SmartBpOcrRegionText> Regions { get; init; } = [];
    /// <summary>请求顶部中间区域时，获取该区域的生命周期分类。</summary>
    public SmartBpLifecycleStatusResult? LifecycleStatus { get; init; }
    /// <summary>请求左上区域时，获取该区域的强确认 BP 后状态。</summary>
    public SmartBpPostBpStatusResult? PostBpStatus { get; init; }
    /// <summary>获取有界识别诊断信息。</summary>
    public IReadOnlyList<string> Diagnostics { get; init; } = [];
}

/// <summary>单个 OCR 粗粒度区域的详细本地解析结果。</summary>
public sealed class SmartBpOcrParsedRegionResult
{
    /// <summary>获取解析出的业务结果。</summary>
    public SmartBpFocusedBusinessExtractionResult Result { get; init; } = new();
    /// <summary>获取解析器和名称解析器诊断信息。</summary>
    public IReadOnlyList<string> Diagnostics { get; init; } = [];
    /// <summary>获取是否仍有必需角色槽位未解析。</summary>
    public bool HasCriticalUnresolvedField { get; init; }
    /// <summary>获取所有已解析槽位是否都可安全自动应用。</summary>
    public bool IsAutoApplySafe { get; init; }
}

/// <summary>OCR BP 识别请求。</summary>
/// <param name="ContentRegions">本次 tick 要解析的内容区域。</param>
/// <param name="IncludePhase">是否包含阶段区域。</param>
/// <param name="ParseContext">可选的 OCR 字段解析上下文，用于传递阶段/动作/锁定信息。</param>
public sealed record SmartBpOcrRecognitionRequest(
    IReadOnlyList<SmartBpRecognitionRegion> ContentRegions,
    bool IncludePhase = true,
    SmartBpOcrFieldParseContext? ParseContext = null);

/// <summary>picked_sur OCR 解析模式，决定行语义分类策略。</summary>
public enum SmartBpPickedSurOcrParseMode
{
    /// <summary>全局业务快照：不依赖当前阶段或 Guidance 动作，从完整画面中解析角色、选手 ID，并忽略天赋等附加行。</summary>
    GlobalSnapshot,
    /// <summary>求生者选择角色阶段：character row + player-id row，无 talent 行。</summary>
    PickSur,
    /// <summary>角色分配阶段：character row + player-id row，后续行为 talent。</summary>
    DistributeChara,
    /// <summary>求生者天赋阶段：character row + player-id row + talent/extra 行。</summary>
    SurvivorTalent,
    /// <summary>未知模式，回退到旧行为（物理行索引语义）。</summary>
    Unknown
}

/// <summary>OCR 字段解析上下文，将阶段/动作/锁定信息传递给行解析器。</summary>
public sealed class SmartBpOcrFieldParseContext
{
    /// <summary>获取或设置权威识别阶段名。</summary>
    public string AuthoritativePhase { get; init; } = "未知";

    /// <summary>获取或设置当前 GameGuidance 动作（若已启动）。</summary>
    public GameAction? CurrentGuidanceAction { get; init; }

    /// <summary>获取或设置求生者选择是否已锁定。</summary>
    public bool SurvivorPickLocked { get; init; }

    /// <summary>获取或设置是否为自动识别模式。</summary>
    public bool IsAutomaticMode { get; init; }

    /// <summary>获取或设置是否按全局业务快照解析全部字段；该模式不使用当前阶段或 Guidance 动作裁剪字段语义。</summary>
    public bool IsGlobalSnapshot { get; init; }

    /// <summary>从上下文解析 picked_sur 解析模式。</summary>
    /// <returns>解析模式。</returns>
    public SmartBpPickedSurOcrParseMode ResolvePickedSurParseMode()
    {
        if (IsGlobalSnapshot)
            return SmartBpPickedSurOcrParseMode.GlobalSnapshot;
        if (CurrentGuidanceAction == GameAction.PickSur && !SurvivorPickLocked)
            return SmartBpPickedSurOcrParseMode.PickSur;
        if (CurrentGuidanceAction == GameAction.DistributeChara || SurvivorPickLocked)
            return SmartBpPickedSurOcrParseMode.DistributeChara;
        if (CurrentGuidanceAction == GameAction.PickSurTalent)
            return SmartBpPickedSurOcrParseMode.SurvivorTalent;

        // 无 guidance 动作时，按权威阶段名判断。
        return AuthoritativePhase switch
        {
            "选择求生者" or "求生者选择角色中" when !SurvivorPickLocked => SmartBpPickedSurOcrParseMode.PickSur,
            "求生者选择天赋中" => SmartBpPickedSurOcrParseMode.SurvivorTalent,
            "选择监管者" or "监管者选择天赋中" or "天赋已锁定" => SmartBpPickedSurOcrParseMode.DistributeChara,
            _ => SmartBpPickedSurOcrParseMode.Unknown
        };
    }
}

/// <summary>一张包含多个 OCR 裁剪图的拼接图。</summary>
/// <param name="Image">堆叠后的 OCR 图片。</param>
/// <param name="Regions">从拼接图空间到 SmartBP 区域的坐标映射。</param>
public sealed record SmartBpOcrContactSheet(
    Mat Image,
    IReadOnlyList<SmartBpOcrContactSheetRegion> Regions) : IDisposable
{
    /// <summary>释放底层 OpenCV 图片。</summary>
    public void Dispose() => Image.Dispose();
}

/// <summary>单个 OCR 拼接图裁剪块的映射。</summary>
/// <param name="Region">来源 SmartBP 区域。</param>
/// <param name="SheetRect">拼接图坐标中的区域矩形。</param>
/// <param name="OriginalFrameRect">原始画面帧坐标中的区域矩形。</param>
public sealed record SmartBpOcrContactSheetRegion(
    SmartBpRecognitionRegion Region,
    Rect SheetRect,
    Rect OriginalFrameRect);

/// <summary>暴露给 UI 的下载状态。</summary>
/// <param name="IsDownloading">当前是否正在下载。</param>
/// <param name="Progress">已知时的整体进度百分比。</param>
/// <param name="Status">本地化资源键或状态文本。</param>
/// <param name="CurrentFileName">当前文件名。</param>
/// <param name="BytesReceived">已下载字节数。</param>
/// <param name="TotalBytes">预期总字节数。</param>
/// <param name="BytesPerSecond">估算下载速度。</param>
/// <param name="Eta">估算剩余时间。</param>
/// <param name="ErrorMessage">操作失败时的详细错误消息。</param>
/// <param name="IsPaused">下载是否已暂停。</param>
public record SmartBpDownloadState(bool IsDownloading, double? Progress, string Status,
    string? CurrentFileName = null,
    long? BytesReceived = null,
    long? TotalBytes = null,
    double? BytesPerSecond = null,
    TimeSpan? Eta = null,
    string? ErrorMessage = null,
    bool IsPaused = false);

/// <summary>描述一个可下载 Tesseract 语言数据资产。</summary>
/// <param name="Language">Tesseract 语言标识。</param>
/// <param name="DisplayNameKey">显示用本地化资源键。</param>
public sealed record TesseractLanguageAsset(string Language, string DisplayNameKey);

/// <summary>描述一个 tessdata 目录中的必需 Tesseract 语言数据。</summary>
/// <param name="IsInstalled">是否所有必需语言都已安装。</param>
/// <param name="DataPath">有效 tessdata 目录。</param>
/// <param name="MissingLanguages">缺失的必需语言标识。</param>
/// <param name="InstalledLanguages">已安装的必需语言标识。</param>
public sealed record TesseractDataStatus(bool IsInstalled, string DataPath,
    IReadOnlyList<string> MissingLanguages, IReadOnlyList<string> InstalledLanguages);

/// <summary>一个内置识别提示词配置档。</summary>
public sealed record SmartBpPromptProfile(string Id, string DisplayName, string SystemPrompt);

/// <summary>模型返回的视觉提取结果。</summary>
public sealed class SmartBpVisionExtractionResult
{
    /// <summary>获取或设置 schema 版本。</summary>
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; set; }
    /// <summary>获取或设置场景信息。</summary>
    [JsonPropertyName("scene")] public SmartBpVisionScene Scene { get; set; } = new();
    /// <summary>获取或设置可见队伍。</summary>
    [JsonPropertyName("teams")] public List<SmartBpVisionTeam> Teams { get; set; } = [];
    /// <summary>获取或设置扁平化可见角色集合。</summary>
    [JsonPropertyName("all_characters")] public List<SmartBpVisionCharacter> AllCharacters { get; set; } = [];
    /// <summary>获取或设置扁平化玩家 ID 集合。</summary>
    [JsonPropertyName("all_player_ids")] public List<SmartBpVisionPlayerId> AllPlayerIds { get; set; } = [];
    /// <summary>获取或设置识别警告。</summary>
    [JsonPropertyName("warnings")] public List<string> Warnings { get; set; } = [];
}

/// <summary>视觉场景元数据。</summary>
public sealed class SmartBpVisionScene
{
    /// <summary>获取或设置游戏名称。</summary>
    [JsonPropertyName("game")] public string Game { get; set; } = "";
    /// <summary>获取或设置界面类型。</summary>
    [JsonPropertyName("interface_type")] public string InterfaceType { get; set; } = "";
    /// <summary>获取或设置任务。</summary>
    [JsonPropertyName("task")] public string Task { get; set; } = "";
    /// <summary>获取或设置主状态文本。</summary>
    [JsonPropertyName("main_status")] public string? MainStatus { get; set; }
    /// <summary>获取或设置暂停状态文本。</summary>
    [JsonPropertyName("pause_status")] public string? PauseStatus { get; set; }
    /// <summary>获取或设置暂停剩余秒数。</summary>
    [JsonPropertyName("pause_remaining_seconds")] public double? PauseRemainingSeconds { get; set; }
}

/// <summary>一个视觉队伍区域。</summary>
public sealed class SmartBpVisionTeam
{
    /// <summary>获取或设置屏幕侧。</summary>
    [JsonPropertyName("side")] public string Side { get; set; } = "unknown";
    /// <summary>获取或设置阵营。</summary>
    [JsonPropertyName("faction")] public string Faction { get; set; } = "unknown";
    /// <summary>获取或设置标题文本。</summary>
    [JsonPropertyName("title_text")] public string? TitleText { get; set; }
    /// <summary>获取或设置副标题文本。</summary>
    [JsonPropertyName("subtitle_text")] public string? SubtitleText { get; set; }
    /// <summary>获取或设置槽位集合。</summary>
    [JsonPropertyName("slots")] public List<SmartBpVisionSlot> Slots { get; set; } = [];
}

/// <summary>一个视觉槽位。</summary>
public sealed class SmartBpVisionSlot
{
    /// <summary>获取或设置槽位索引。</summary>
    [JsonPropertyName("slot_index")] public int SlotIndex { get; set; }
    /// <summary>获取或设置槽位状态。</summary>
    [JsonPropertyName("slot_state")] public string SlotState { get; set; } = "unknown";
    /// <summary>获取或设置原始候选角色名称。</summary>
    [JsonPropertyName("character_name")] public string? CharacterName { get; set; }
    /// <summary>获取或设置玩家 ID。</summary>
    [JsonPropertyName("player_id")] public string? PlayerId { get; set; }
    /// <summary>获取或设置禁用/不可用标记。</summary>
    [JsonPropertyName("is_banned_or_unavailable")] public bool IsBannedOrUnavailable { get; set; }
    /// <summary>获取或设置全部可见原始文本。</summary>
    [JsonPropertyName("raw_visible_text")] public string? RawVisibleText { get; set; }
    /// <summary>获取或设置置信度。</summary>
    [JsonPropertyName("confidence")] public double Confidence { get; set; }
}

/// <summary>一个扁平化视觉角色。</summary>
public sealed class SmartBpVisionCharacter
{
    /// <summary>获取或设置角色名称。</summary>
    [JsonPropertyName("character_name")] public string? CharacterName { get; set; }
    /// <summary>获取或设置阵营。</summary>
    [JsonPropertyName("faction")] public string Faction { get; set; } = "unknown";
    /// <summary>获取或设置玩家 ID。</summary>
    [JsonPropertyName("player_id")] public string? PlayerId { get; set; }
    /// <summary>获取或设置侧向。</summary>
    [JsonPropertyName("side")] public string Side { get; set; } = "unknown";
    /// <summary>获取或设置槽位索引。</summary>
    [JsonPropertyName("slot_index")] public int SlotIndex { get; set; }
    /// <summary>获取或设置状态。</summary>
    [JsonPropertyName("slot_state")] public string SlotState { get; set; } = "unknown";
    /// <summary>获取或设置置信度。</summary>
    [JsonPropertyName("confidence")] public double Confidence { get; set; }
}

/// <summary>一个扁平化视觉玩家 ID。</summary>
public sealed class SmartBpVisionPlayerId
{
    /// <summary>获取或设置玩家 ID。</summary>
    [JsonPropertyName("player_id")] public string? PlayerId { get; set; }
    /// <summary>获取或设置角色名称。</summary>
    [JsonPropertyName("character_name")] public string? CharacterName { get; set; }
    /// <summary>获取或设置侧向。</summary>
    [JsonPropertyName("side")] public string Side { get; set; } = "unknown";
    /// <summary>获取或设置槽位索引。</summary>
    [JsonPropertyName("slot_index")] public int SlotIndex { get; set; }
    /// <summary>获取或设置置信度。</summary>
    [JsonPropertyName("confidence")] public double Confidence { get; set; }
}

/// <summary>一个归一化角色出现项。</summary>
public sealed record SmartBpNormalizedCharacter(string? RawCharacterName, string? ResolvedCharacterName,
    Camp Camp, int SlotIndex, double Confidence, IReadOnlyList<string> Warnings,
    string MatchMode = "none", bool IsAutoApplySafe = false, string? RecognitionReason = null);

/// <summary>返回给 UI 的识别预览。</summary>
public sealed record SmartBpRecognitionPreview(string RawResponse, string ParsedVisualSummary,
    string ResolvedCharacterSummary, long ElapsedMilliseconds, int RecommendedIntervalMilliseconds, string? Error);

/// <summary>内置识别样例。</summary>
public sealed record SmartBpTestFrame(string Id, string FileName, SmartBpRecognitionTask Task);

/// <summary>一条带时间戳的 AI 流水线诊断消息。</summary>
public sealed class SmartBpDebugMessageEventArgs : EventArgs
{
    /// <summary>初始化诊断消息。</summary>
    /// <param name="timestamp">消息时间戳。</param>
    /// <param name="source">子系统名称。</param>
    /// <param name="message">消息文本。</param>
    public SmartBpDebugMessageEventArgs(DateTimeOffset timestamp, string source, string message)
    {
        Timestamp = timestamp;
        Source = source;
        Message = message;
    }
    /// <summary>获取时间戳。</summary>
    public DateTimeOffset Timestamp { get; }
    /// <summary>获取子系统名称。</summary>
    public string Source { get; }
    /// <summary>获取消息文本。</summary>
    public string Message { get; }
}
