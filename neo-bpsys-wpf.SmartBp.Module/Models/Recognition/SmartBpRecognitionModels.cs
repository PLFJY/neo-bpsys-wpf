using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Abstractions.Services;
using OpenCvSharp;

namespace neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

/// <summary>支持的 AI 识别任务。</summary>
public enum SmartBpRecognitionTask { DetectStage, BanSur, BanHun, PickSur, PickHun, CharacterDistribution, FullBpScan }

/// <summary>支持的 SmartBP BP 识别引擎。</summary>
public enum SmartBpRecognitionEngine { Ocr, AiQwen }

/// <summary>协调器和 UI 选择的一等 SmartBP 识别策略。</summary>
public enum SmartBpRecognitionStrategy
{
    /// <summary>只使用已选本地 OCR Provider。</summary>
    PureOcr
}

/// <summary>选择混合识别证据如何融合为 SmartBP 字段更新。</summary>
public enum SmartBpHybridFusionMode
{
    /// <summary>使用本地 C# 解析器/解释器并在进程内合并。</summary>
    LocalCSharp,
    /// <summary>请求业务 AI 模型将证据转换为结构化 BP 字段更新。</summary>
    BusinessAi
}

/// <summary>业务 AI 转写融合输出期望的 JSON 契约。</summary>
public enum SmartBpBusinessAiFusionOutputContract
{
    /// <summary>返回包含阶段和四个 BP 字段的完整 BP 业务状态对象。</summary>
    FullBusinessState,
    /// <summary>返回包含阶段和更新的快照增量对象。</summary>
    SnapshotDelta
}

/// <summary>托管本地视觉模型系列。</summary>
public enum LocalVisionModelFamily
{
    /// <summary>Qwen 3.5 视觉语言模型。</summary>
    Qwen35,
    /// <summary>GLM OCR 模型。</summary>
    GlmOcr,
    /// <summary>PaddleOCR-VL 模型。</summary>
    PaddleOcrVl,
    /// <summary>自定义或未知本地视觉模型系列。</summary>
    Custom
}

/// <summary>本地视觉模型预期承担的角色。</summary>
public enum LocalVisionModelRole
{
    /// <summary>用于场景、阶段和 BP 状态识别的业务 VLM。</summary>
    BusinessVlm,
    /// <summary>AI OCR 文本提取器，不负责 BP 业务解释。</summary>
    AiOcrTextExtractor,
    /// <summary>模型可同时用于业务识别和 AI OCR 提取。</summary>
    Both,
    /// <summary>角色未知。</summary>
    Unknown
}

/// <summary>标识 Qwen 模型配置档使用的下载来源。</summary>
public enum QwenModelSourceType { DirectUrl, HuggingFace }

/// <summary>描述 Qwen 视觉投影器的提供方式。</summary>
public enum QwenMmprojMode { Separate, Embedded, None }

/// <summary>描述本地视觉投影器的提供方式。</summary>
public enum VisionProjectorMode { Separate, Embedded, None }

/// <summary>托管 llama.cpp 视觉服务器角色。</summary>
public enum LlamaVisionServerRole
{
    /// <summary>用于场景、阶段和 BP 业务推理的业务 AI 服务器。</summary>
    BusinessAi,
    /// <summary>仅用于提取可见文本转写的 AI OCR 服务器。</summary>
    AiOcr
}

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

/// <summary>控制识别到的操作如何与当前对局对齐。</summary>
public enum SmartBpRecognitionApplyMode
{
    /// <summary>根据活动 GameGuidance 工作流对齐识别结果。</summary>
    GuidedWorkflow,
    /// <summary>不依赖工作流上下文，同步识别到的角色槽位。</summary>
    FreeFullSync
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

/// <summary>控制 AI 客户端如何向 llama-server 请求结构化 JSON 输出。</summary>
public enum AiStructuredOutputMode
{
    /// <summary>发送 <c>response_format=json_schema</c> 并依赖服务器强制执行 schema。</summary>
    JsonSchemaStrict,
    /// <summary>不发送 <c>response_format</c>，在提示词中要求原始 JSON，并在本地修复 Markdown 围栏。</summary>
    JsonPromptAndRepair
}

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

/// <summary>Qwen 清单根对象。</summary>
public sealed class QwenModelManifest
{
    /// <summary>获取或设置 schema 版本。</summary>
    public int SchemaVersion { get; set; }
    /// <summary>获取或设置模型配置档集合。</summary>
    public List<QwenModelProfile> Models { get; set; } = [];
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

/// <summary>本地视觉模型清单根对象。</summary>
public sealed class LocalVisionModelManifest
{
    /// <summary>获取或设置 schema 版本。</summary>
    public int SchemaVersion { get; set; }
    /// <summary>获取或设置模型配置档集合。</summary>
    public List<LocalVisionModelProfile> Models { get; set; } = [];
}

/// <summary>一个 Qwen 模型及其匹配的视觉投影器。</summary>
public sealed class QwenModelProfile
{
    /// <summary>获取或设置配置档标识。</summary>
    public string Id { get; set; } = "";
    /// <summary>获取或设置显示名称。</summary>
    public string DisplayName { get; set; } = "";
    /// <summary>获取或设置模型系列。</summary>
    public LocalVisionModelFamily Family { get; set; } = LocalVisionModelFamily.Custom;
    /// <summary>获取或设置预期模型角色。</summary>
    public LocalVisionModelRole Role { get; set; } = LocalVisionModelRole.Unknown;
    /// <summary>获取或设置模型来源类型。</summary>
    public QwenModelSourceType SourceType { get; set; } = QwenModelSourceType.DirectUrl;
    /// <summary>获取或设置直链配置档的模型 URL。</summary>
    public string ModelUrl { get; set; } = "";
    /// <summary>获取或设置模型文件名。</summary>
    public string ModelFileName { get; set; } = "";
    /// <summary>获取或设置投影器 URL。</summary>
    public string? MmprojUrl { get; set; }
    /// <summary>获取或设置投影器文件名。</summary>
    public string? MmprojFileName { get; set; }
    /// <summary>获取或设置 HuggingFace 仓库标识。</summary>
    public string? HuggingFaceRepoId { get; set; }
    /// <summary>获取或设置 HuggingFace 修订版本。</summary>
    public string HuggingFaceRevision { get; set; } = "main";
    /// <summary>获取或设置视觉投影器的提供方式。</summary>
    public QwenMmprojMode MmprojMode { get; set; } = QwenMmprojMode.Separate;
    /// <summary>获取或设置使用通用本地视觉术语表达的视觉投影器提供方式。</summary>
    public VisionProjectorMode ProjectorMode
    {
        get => MmprojMode switch
        {
            QwenMmprojMode.Separate => VisionProjectorMode.Separate,
            QwenMmprojMode.Embedded => VisionProjectorMode.Embedded,
            QwenMmprojMode.None => VisionProjectorMode.None,
            _ => VisionProjectorMode.Separate
        };
        set => MmprojMode = value switch
        {
            VisionProjectorMode.Separate => QwenMmprojMode.Separate,
            VisionProjectorMode.Embedded => QwenMmprojMode.Embedded,
            VisionProjectorMode.None => QwenMmprojMode.None,
            _ => QwenMmprojMode.Separate
        };
    }
    /// <summary>获取或设置中文 UI 是否优先使用 HuggingFace 镜像。</summary>
    public bool UseHuggingFaceMirrorForChineseUi { get; set; } = true;
    /// <summary>获取或设置可选模型哈希。</summary>
    public string? Sha256 { get; set; }
    /// <summary>获取或设置可选投影器哈希。</summary>
    public string? MmprojSha256 { get; set; }
    /// <summary>获取或设置该配置档是否为其角色的推荐默认项。</summary>
    public bool Recommended { get; set; }
    /// <summary>获取或设置该配置档是否为实验性配置档。</summary>
    public bool Experimental { get; set; }
    /// <summary>获取或设置该模型的默认结构化输出模式。</summary>
    public AiStructuredOutputMode DefaultStructuredOutputMode { get; set; } = AiStructuredOutputMode.JsonPromptAndRepair;
}

/// <summary>一个本地视觉模型及其匹配的视觉投影器。</summary>
public sealed class LocalVisionModelProfile
{
    /// <summary>获取或设置配置档标识。</summary>
    public string Id { get; set; } = "";
    /// <summary>获取或设置显示名称。</summary>
    public string DisplayName { get; set; } = "";
    /// <summary>获取或设置模型系列。</summary>
    public LocalVisionModelFamily Family { get; set; } = LocalVisionModelFamily.Custom;
    /// <summary>获取或设置预期模型角色。</summary>
    public LocalVisionModelRole Role { get; set; } = LocalVisionModelRole.Unknown;
    /// <summary>获取或设置模型来源类型。</summary>
    public QwenModelSourceType SourceType { get; set; } = QwenModelSourceType.DirectUrl;
    /// <summary>获取或设置直链配置档的模型 URL。</summary>
    public string ModelUrl { get; set; } = "";
    /// <summary>获取或设置模型文件名。</summary>
    public string ModelFileName { get; set; } = "";
    /// <summary>获取或设置投影器 URL。</summary>
    public string? MmprojUrl { get; set; }
    /// <summary>获取或设置投影器文件名。</summary>
    public string? MmprojFileName { get; set; }
    /// <summary>获取或设置 HuggingFace 仓库标识。</summary>
    public string? HuggingFaceRepoId { get; set; }
    /// <summary>获取或设置 HuggingFace 修订版本。</summary>
    public string HuggingFaceRevision { get; set; } = "main";
    /// <summary>获取或设置视觉投影器的提供方式。</summary>
    public VisionProjectorMode ProjectorMode { get; set; } = VisionProjectorMode.Separate;
    /// <summary>获取或设置可选模型哈希。</summary>
    public string? Sha256 { get; set; }
    /// <summary>获取或设置可选投影器哈希。</summary>
    public string? MmprojSha256 { get; set; }
    /// <summary>获取或设置该配置档是否为其角色的推荐默认项。</summary>
    public bool Recommended { get; set; }
    /// <summary>获取或设置该配置档是否为实验性配置档。</summary>
    public bool Experimental { get; set; }
    /// <summary>获取或设置该模型的默认结构化输出模式。</summary>
    public AiStructuredOutputMode DefaultStructuredOutputMode { get; set; } = AiStructuredOutputMode.JsonPromptAndRepair;
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
    /// <summary>获取或设置 llama-server 路径。</summary>
    public string LlamaServerExecutablePath { get; set; } = "";
    /// <summary>获取或设置回环端口。</summary>
    public int LlamaServerPort { get; set; } = 18080;
    /// <summary>获取或设置业务 AI 服务器端口。</summary>
    public int BusinessAiServerPort { get; set; } = 18080;
    /// <summary>获取或设置 AI OCR 服务器端口。</summary>
    public int AiOcrServerPort { get; set; } = 18081;
    /// <summary>获取或设置单次 llama.cpp 推理请求超时时间。</summary>
    public int AiRequestTimeoutSeconds { get; set; } = 35;
    /// <summary>获取或设置 llama.cpp 启动超时时间。</summary>
    public int AiStartupTimeoutSeconds { get; set; } = 120;
    /// <summary>获取或设置中文 UI 是否使用 HuggingFace 镜像。</summary>
    public bool UseHuggingFaceMirrorForChineseUi { get; set; } = true;
    /// <summary>获取或设置可选 HuggingFace 端点覆盖值。</summary>
    public string HuggingFaceEndpointOverride { get; set; } = "";
    /// <summary>获取或设置 llama.cpp 上下文大小。</summary>
    public int LlamaContextSize { get; set; } = 8192;
    /// <summary>获取或设置已选 Qwen 配置档。</summary>
    public string SelectedQwenModelId { get; set; } = "qwen3.5-2b-q4km";
    /// <summary>获取或设置已选业务本地视觉模型配置档。</summary>
    public string SelectedBusinessAiModelId { get; set; } = "qwen3.5-2b-q4km";
    /// <summary>获取或设置已选 AI OCR 本地视觉模型配置档。</summary>
    public string SelectedAiOcrModelId { get; set; } = "paddleocr-vl-1.6-gguf";
    /// <summary>获取或设置模型不同时 AI OCR 是否使用独立 llama.cpp 服务器。</summary>
    public bool UseSeparateAiOcrServer { get; set; } = true;
    /// <summary>获取或设置 AI + OCR 如何将 OCR 证据融合为业务状态。</summary>
    public SmartBpHybridFusionMode AiWithOcrFusionMode { get; set; } = SmartBpHybridFusionMode.LocalCSharp;
    /// <summary>获取或设置 AI + AI OCR 如何将转写证据融合为业务状态。</summary>
    public SmartBpHybridFusionMode AiWithAiOcrFusionMode { get; set; } = SmartBpHybridFusionMode.BusinessAi;
    /// <summary>获取或设置业务 AI 融合如何向 llama.cpp 请求结构化输出。</summary>
    public AiStructuredOutputMode BusinessAiFusionStructuredOutputMode { get; set; } = AiStructuredOutputMode.JsonPromptAndRepair;
    /// <summary>获取或设置 AI + AI OCR 完整调试识别使用的业务 AI 融合输出契约。</summary>
    public SmartBpBusinessAiFusionOutputContract AiWithAiOcrFullDebugFusionContract { get; set; } = SmartBpBusinessAiFusionOutputContract.FullBusinessState;
    /// <summary>获取或设置已选投影器配置档标签。</summary>
    public string SelectedMmprojId { get; set; } = "mmproj-f16";
    /// <summary>获取或设置内置提示词配置档标识。</summary>
    public string PromptProfileId { get; set; } = "zh-CN";
    /// <summary>获取或设置托管 llama.cpp 运行时资产标识。</summary>
    public string SelectedLlamaRuntimeId { get; set; } = "";
    /// <summary>获取或设置最大编码宽度。</summary>
    public int MaxImageWidth { get; set; } = 1280;
    /// <summary>获取或设置图片编码格式。</summary>
    public string ImageFormat { get; set; } = "png";
    /// <summary>获取或设置推理温度。</summary>
    public double Temperature { get; set; }
    /// <summary>获取或设置聚焦识别 token 上限。</summary>
    public int FocusedMaxTokens { get; set; } = 1024;
    /// <summary>获取或设置全量扫描 token 上限。</summary>
    public int FullScanMaxTokens { get; set; } = 2048;
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
    public bool EnableAutoGuidanceSync { get; set; }
    /// <summary>获取或设置引导同步是否跟随后台页面导航。</summary>
    public bool EnableAutoGuidancePageNavigation { get; set; }
    /// <summary>获取或设置是否可以应用已接受操作。</summary>
    public bool EnableAutoApplyRecognition { get; set; }
    /// <summary>获取或设置识别结果应用策略。</summary>
    public SmartBpRecognitionApplyMode RecognitionApplyMode { get; set; } = SmartBpRecognitionApplyMode.GuidedWorkflow;
    /// <summary>获取或设置 AI 是否在移动引导前先完成前一步。</summary>
    public bool AiOneStepDelayedMode { get; set; } = true;
    /// <summary>获取或设置推断监管者天赋阶段所需的连续未知阶段帧数。</summary>
    public int AiUnknownPhaseTalentInferenceFrames { get; set; } = 2;
    /// <summary>获取或设置最小阶段置信度。</summary>
    public double StageConfidenceThreshold { get; set; } = 0.80;
    /// <summary>获取或设置引导对齐向前查找步数。</summary>
    public int GuidanceSyncLookAheadSteps { get; set; } = 4;
    /// <summary>获取或设置是否启用 SmartBP 进度智能诊断后的自动向前同步。</summary>
    public bool EnableSmartBpProgressAutoCorrection { get; set; }
    /// <summary>获取或设置自动进度同步前需要连续确认同一目标的次数。</summary>
    public int SmartBpProgressMismatchConfirmationCount { get; set; } = 2;
    /// <summary>获取或设置自动进度同步后的冷却时间（毫秒）。</summary>
    public int SmartBpProgressAutoCorrectionCooldownMs { get; set; } = 10000;
    /// <summary>获取或设置自动进度推断的最低置信分数。</summary>
    public double SmartBpProgressInferenceMinimumScore { get; set; } = 0.82;
    /// <summary>获取或设置自动进度推断最佳候选与次佳候选之间的最低分差。</summary>
    public double SmartBpProgressInferenceMinimumScoreMargin { get; set; } = 0.15;
    /// <summary>获取或设置延迟工作流回填是否应重放前端动画。</summary>
    public bool PlayBackfillAnimations { get; set; }
    /// <summary>获取或设置自动应用前所需的匹配快照数量。</summary>
    public int RequiredStableSnapshots { get; set; } = 1;
    /// <summary>获取或设置自动识别是否使用单次多图快照增量请求。</summary>
    public bool UseMultiImageSnapshotRequest { get; set; } = true;
    /// <summary>获取或设置是否使用旧版模型侧快照增量识别替代字段快照。</summary>
    public bool UseLegacySnapshotDeltaRecognition { get; set; }
    /// <summary>获取或设置 AI 客户端如何向 llama-server 请求结构化 JSON 输出。</summary>
    public AiStructuredOutputMode StructuredOutputMode { get; set; } = AiStructuredOutputMode.JsonSchemaStrict;
    /// <summary>获取或设置已选 BP 识别引擎。</summary>
    public SmartBpRecognitionEngine RecognitionEngine { get; set; } = SmartBpRecognitionEngine.Ocr;
    /// <summary>获取或设置已选 SmartBP 识别策略。</summary>
    public SmartBpRecognitionStrategy RecognitionStrategy { get; set; } = SmartBpRecognitionStrategy.PureOcr;
    /// <summary>获取或设置是否启用 OCR BP 识别。</summary>
    public bool EnableOcrBpRecognition { get; set; } = true;
    /// <summary>获取或设置 OCR BP 循环间隔。</summary>
    public int OcrRecognitionIntervalMs { get; set; } = 3000;
    /// <summary>获取或设置实测最小 OCR 间隔。</summary>
    public int MinimumOcrRecognitionIntervalMs { get; set; }
    /// <summary>获取或设置实测最小 AI 间隔。</summary>
    public int MinimumAiRecognitionIntervalMs { get; set; }
    /// <summary>获取或设置最近一次识别速度测量时间。</summary>
    public DateTimeOffset? LastRecognitionSpeedTestAt { get; set; }
    /// <summary>获取或设置最近一次速度测试使用的引擎标签。</summary>
    public string LastRecognitionSpeedTestEngine { get; set; } = "";
    /// <summary>获取或设置影响性能的配置指纹。</summary>
    public string LastRecognitionSpeedTestConfigurationHash { get; set; } = "";
    /// <summary>获取或设置 OCR 合并字段可保持新鲜的时长。</summary>
    public int OcrFieldStaleMilliseconds { get; set; } = 1500;
    /// <summary>获取或设置 OCR 回填规划考虑多少个前置工作流步骤。</summary>
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
    /// <summary>获取或设置规划内容区域刷新时考虑多少个前置工作流步骤。</summary>
    public int RecognitionBackfillLookBehindSteps { get; set; } = 2;
    /// <summary>获取或设置本地合并识别字段可保持新鲜的时长。</summary>
    public int RecognitionFieldStaleMilliseconds { get; set; } = 2500;
    /// <summary>获取或设置应用当前步骤动画操作前的可选延迟。</summary>
    public int RecognitionVisualBufferMilliseconds { get; set; }
    /// <summary>获取或设置 llama.cpp 并行槽位数量。</summary>
    public int LlamaParallelSlots { get; set; } = 1;
    /// <summary>获取或设置 llama.cpp GPU 层数；-1 表示自动。</summary>
    public int LlamaGpuLayers { get; set; } = -1;
    /// <summary>获取或设置是否启用 llama.cpp flash attention。</summary>
    public bool LlamaFlashAttention { get; set; } = true;
    /// <summary>获取或设置 llama.cpp batch size。</summary>
    public int LlamaBatchSize { get; set; } = 512;
    /// <summary>获取或设置 llama.cpp micro-batch size。</summary>
    public int LlamaUBatchSize { get; set; } = 512;
    /// <summary>获取或设置是否可自动结束过期的托管 llama-server 进程。</summary>
    public bool AutoKillStaleManagedLlamaServer { get; set; } = true;
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
    /// <summary>获取或设置 banned_sur 字段快照 token 预算。</summary>
    public int BannedSurFieldMaxTokens { get; set; } = 256;
    /// <summary>获取或设置 banned_hun 字段快照 token 预算。</summary>
    public int BannedHunFieldMaxTokens { get; set; } = 192;
    /// <summary>获取或设置 picked_sur 字段快照 token 预算。</summary>
    public int PickedSurFieldMaxTokens { get; set; } = 384;
    /// <summary>获取或设置 picked_hun 字段快照 token 预算。</summary>
    public int PickedHunFieldMaxTokens { get; set; } = 192;
    /// <summary>获取或设置将引导移动到新检测阶段前的短提交等待时间。</summary>
    public int PhaseTransitionCommitHoldMilliseconds { get; set; } = 350;
    /// <summary>获取或设置允许延迟回填前的最大提交等待时间。</summary>
    public int PhaseTransitionCommitHoldMaxMilliseconds { get; set; } = 800;
    /// <summary>获取或设置阶段移动后是否仍允许无动画延迟回填。</summary>
    public bool AllowLateBackfillAfterPhaseMoved { get; set; } = true;
    /// <summary>获取或设置滚动识别帧缓冲长度。</summary>
    public int RecognitionFrameBufferMilliseconds { get; set; } = 1500;
    /// <summary>获取或设置转场最终确认可向前回看画面帧的时长。</summary>
    public int RecognitionTransitionLookBehindMilliseconds { get; set; } = 800;
    /// <summary>获取或设置裁剪图变化阈值。</summary>
    public double RecognitionCropChangeThreshold { get; set; } = 0.035;
    /// <summary>获取或设置优先要求的稳定裁剪观测帧数。</summary>
    public int RecognitionCropStableFrames { get; set; } = 2;
    /// <summary>获取或设置是否启用 llama.cpp 运行时更新检查。</summary>
    public bool EnableLlamaRuntimeUpdateCheck { get; set; } = true;
    /// <summary>获取或设置 llama.cpp 运行时更新检查间隔（小时）。</summary>
    public int LlamaRuntimeUpdateCheckIntervalHours { get; set; } = 168;
    /// <summary>获取或设置自定义远程 llama.cpp 运行时清单 API URL。</summary>
    public string LlamaRuntimeManifestApiUrl { get; set; } = "";
    /// <summary>获取或设置最近一次 llama.cpp 运行时更新检查时间。</summary>
    public DateTimeOffset? LastLlamaRuntimeUpdateCheckAt { get; set; }
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
    /// <summary>获取或设置本地 OCR 匹配置信度；模型 JSON 不序列化该元数据。</summary>
    [JsonIgnore] public double RecognitionConfidence { get; set; } = 1;
    /// <summary>获取或设置本地 OCR 匹配是否可安全自动应用。</summary>
    [JsonIgnore] public bool IsAutoApplySafe { get; set; } = true;
    /// <summary>获取或设置 OCR 匹配诊断原因。</summary>
    [JsonIgnore] public string? RecognitionReason { get; set; }
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

/// <summary>仅阶段识别路径产出的 AI 识别结果。</summary>
public sealed class SmartBpAiPhaseOnlyResult
{
    /// <summary>获取识别到的阶段。</summary>
    public SmartBpPhaseRecognitionResult Phase { get; init; } = new();
    /// <summary>获取模型使用的阶段裁剪图。</summary>
    public SmartBpCroppedFrame Crop { get; init; } = default!;
    /// <summary>获取模型使用的绝对左上角全局状态裁剪图。</summary>
    public SmartBpCroppedFrame TopLeftStatusCrop { get; init; } = default!;
    /// <summary>获取模型原始 JSON 响应。</summary>
    public string RawJson { get; init; } = "";
    /// <summary>获取识别诊断信息。</summary>
    public IReadOnlyList<string> Diagnostics { get; init; } = [];
}

/// <summary>一个字段级 AI 快照识别结果。</summary>
public sealed class SmartBpAiFieldSnapshotResult
{
    /// <summary>获取业务字段标识（banned_sur、banned_hun、picked_sur、picked_hun）。</summary>
    public string Field { get; init; } = "";
    /// <summary>获取带 slot_state 证据的已解析字段快照槽位。</summary>
    public IReadOnlyList<SmartBpSnapshotDeltaSlot> Slots { get; init; } = [];
    /// <summary>字段为 picked_hun 时获取监管者选择槽位。</summary>
    public SmartBpSnapshotDeltaSlot? PickedHun { get; init; }
    /// <summary>获取从可见快照派生出的聚焦业务提取结果。</summary>
    public SmartBpFocusedBusinessExtractionResult FocusedResult { get; init; } = new();
    /// <summary>获取模型使用的内容裁剪图。</summary>
    public SmartBpCroppedFrame Crop { get; init; } = default!;
    /// <summary>获取模型原始 JSON 响应。</summary>
    public string RawJson { get; init; } = "";
    /// <summary>获取识别诊断信息。</summary>
    public IReadOnlyList<string> Diagnostics { get; init; } = [];
}

/// <summary>内存中的 SmartBP 本地合并识别状态。</summary>
public sealed class SmartBpRecognitionState
{
    /// <summary>获取或设置最新阶段。</summary>
    public string Phase { get; set; } = "未知";
    /// <summary>获取或设置已知求生者禁用。</summary>
    public List<SmartBpRecognizedCharacterSlot> BannedSur { get; set; } = DefaultBannedSur();
    /// <summary>获取或设置已知监管者禁用。</summary>
    public List<SmartBpRecognizedCharacterSlot> BannedHun { get; set; } = DefaultBannedHun();
    /// <summary>获取或设置已知求生者选择或分配。</summary>
    public List<SmartBpRecognizedPlayerCharacterSlot> PickedSur { get; set; } = DefaultPickedSur();
    /// <summary>获取或设置已知监管者选择。</summary>
    public SmartBpRecognizedPlayerCharacterSlot PickedHun { get; set; } = DefaultPickedHun();
    /// <summary>
    /// 获取或设置求生者分配阶段识别到的视觉槽位证据。
    /// 锁定后 picked_sur 不再按视觉槽位索引合并到 <see cref="PickedSur"/>，而是记录在此处供 player_id 分配使用。
    /// </summary>
    public List<SmartBpRecognizedPlayerCharacterSlot> DistributionEvidence { get; set; } = [];
    /// <summary>获取或设置每个字段的最近更新时间戳。</summary>
    public Dictionary<string, DateTimeOffset> FieldUpdatedAt { get; set; } = [];
    /// <summary>获取或设置最新已接受画面帧序号。</summary>
    public long LastFrameSequence { get; set; }
    /// <summary>获取或设置每个字段最新已接受画面帧序号。</summary>
    public Dictionary<string, long> FieldFrameSequences { get; set; } = [];

    /// <summary>创建默认求生者禁用槽位。</summary>
    public static List<SmartBpRecognizedCharacterSlot> DefaultBannedSur() => Enumerable.Range(0, 4).Select(i => new SmartBpRecognizedCharacterSlot { Index = i, CharacterName = "未选择" }).ToList();
    /// <summary>创建默认监管者禁用槽位。</summary>
    public static List<SmartBpRecognizedCharacterSlot> DefaultBannedHun() => Enumerable.Range(0, 2).Select(i => new SmartBpRecognizedCharacterSlot { Index = i, CharacterName = "未选择" }).ToList();
    /// <summary>创建默认求生者选择槽位。</summary>
    public static List<SmartBpRecognizedPlayerCharacterSlot> DefaultPickedSur() => Enumerable.Range(0, 4).Select(i => new SmartBpRecognizedPlayerCharacterSlot { Index = i, CharacterName = "未选择" }).ToList();
    /// <summary>创建默认监管者选择槽位。</summary>
    public static SmartBpRecognizedPlayerCharacterSlot DefaultPickedHun() => new() { Index = 0, CharacterName = "未选择" };
}

/// <summary>只读识别台账快照。</summary>
public sealed record SmartBpRecognitionLedgerSnapshot(IReadOnlyCollection<SmartBpWorkflowOperationKey> CompletedKeys);

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
public enum SmartBpDetectedOperationKind { BanCharacter, PickSurvivor, PickHunter, SwapSurvivors }

/// <summary>控制单个已检测操作的工作流校验和动画行为。</summary>
public enum SmartBpDetectedOperationApplyMode
{
    /// <summary>应用与当前引导步骤关联的操作。</summary>
    CurrentStep,
    /// <summary>应用与较早工作流步骤关联的延迟操作。</summary>
    Backfill,
    /// <summary>应用不带动画且不进行工作流校验的操作。</summary>
    FreeSync
}

/// <summary>根据聚焦视觉提取派生出的预览候选操作。</summary>
public sealed record SmartBpDetectedOperation(SmartBpDetectedOperationKind Kind, GameAction SourceGuidanceAction,
    IReadOnlyList<int> SourceGuidanceIndexes, Camp Camp, int SlotIndex, string? RawCharacterName,
    string? ResolvedCharacterKey, string? ResolvedCharacterName, string? PlayerId, double Confidence, string Reason,
    int? SourceWorkflowStepIndex = null,
    SmartBpDetectedOperationApplyMode ApplyMode = SmartBpDetectedOperationApplyMode.CurrentStep);

/// <summary>一个工作流派生角色操作的稳定台账身份。</summary>
public sealed record SmartBpWorkflowOperationKey(GameProgress GameProgress, int StepIndex, GameAction Action,
    int SlotIndex, Camp Camp, string? ResolvedCharacterKey);

/// <summary>与一个不可变 GameGuidance 工作流步骤关联的候选操作集合。</summary>
public sealed record SmartBpWorkflowStepCandidateSet(int StepIndex, GameAction Action, IReadOnlyList<int> Indexes,
    IReadOnlyList<SmartBpDetectedOperation> Operations, string Reason);

/// <summary>根据合并区域快照构建的有序角色回填计划。</summary>
public sealed record SmartBpWorkflowBackfillPlan(IReadOnlyList<SmartBpWorkflowStepCandidateSet> StepCandidates,
    IReadOnlyList<string> Diagnostics);

/// <summary>将已检测阶段与 GameGuidance 对齐后的结果。</summary>
public sealed record SmartBpGuidanceSyncResult(bool Changed, bool IsAccepted, string Reason, GameAction? TargetAction,
    IReadOnlyList<int> TargetIndexes, int? TargetStepIndex);

/// <summary>SmartBP 精确进度同步模式。</summary>
public enum SmartBpProgressSyncMode
{
    /// <summary>用户手动触发，允许向前或向后同步。</summary>
    Manual,
    /// <summary>自动识别诊断触发，只允许保守地向前同步。</summary>
    AutomaticDiagnostic
}

/// <summary>控制 SmartBP 精确进度推断的阈值和搜索范围。</summary>
/// <param name="AllowBackwardSync">是否允许选择当前步骤之前的候选。</param>
/// <param name="MaxForwardDistance">允许选择的最大前进步数；<see langword="null"/> 表示不限制。</param>
/// <param name="MinimumScore">接受候选的最低分数。</param>
/// <param name="MinimumScoreMargin">最佳候选和次佳候选之间的最低分差。</param>
public sealed record SmartBpProgressInferenceOptions(
    bool AllowBackwardSync,
    int? MaxForwardDistance,
    double MinimumScore,
    double MinimumScoreMargin);

/// <summary>单个 GameGuidance 工作流候选步骤的 SmartBP 进度推断分数。</summary>
/// <param name="StepIndex">候选步骤索引。</param>
/// <param name="Action">候选步骤动作。</param>
/// <param name="Indexes">候选步骤索引集合。</param>
/// <param name="Score">候选总分。</param>
/// <param name="Reason">候选评分说明。</param>
public sealed record SmartBpProgressCandidateScore(
    int StepIndex,
    GameAction Action,
    IReadOnlyList<int> Indexes,
    double Score,
    string Reason);

/// <summary>SmartBP 精确进度推断结果。</summary>
/// <param name="IsConfident">推断是否满足置信阈值。</param>
/// <param name="TargetStepIndex">推荐目标步骤索引。</param>
/// <param name="TargetAction">推荐目标动作。</param>
/// <param name="TargetIndexes">推荐目标步骤索引集合。</param>
/// <param name="Score">最佳候选分数。</param>
/// <param name="SecondBestScore">次佳候选分数。</param>
/// <param name="Reason">推断结果说明。</param>
/// <param name="Candidates">所有候选分数。</param>
/// <param name="Diagnostics">详细诊断信息。</param>
public sealed record SmartBpProgressInferenceResult(
    bool IsConfident,
    int? TargetStepIndex,
    GameAction? TargetAction,
    IReadOnlyList<int> TargetIndexes,
    double Score,
    double SecondBestScore,
    string Reason,
    IReadOnlyList<SmartBpProgressCandidateScore> Candidates,
    IReadOnlyList<string> Diagnostics);

/// <summary>SmartBP 识别状态与 GameGuidance 当前步骤的对齐检查结果。</summary>
/// <param name="IsAligned">当前引导步骤是否与推断步骤一致。</param>
/// <param name="IsAmbiguous">推断证据是否不足。</param>
/// <param name="IsMisaligned">当前引导步骤是否与可信推断不一致。</param>
/// <param name="Inference">底层推断结果。</param>
/// <param name="Reason">对齐检查说明。</param>
/// <param name="Diagnostics">详细诊断信息。</param>
public sealed record SmartBpProgressAlignmentResult(
    bool IsAligned,
    bool IsAmbiguous,
    bool IsMisaligned,
    SmartBpProgressInferenceResult Inference,
    string Reason,
    IReadOnlyList<string> Diagnostics);

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
    SmartBpWorkflowBackfillPlan? BackfillPlan = null,
    IReadOnlyList<SmartBpCroppedFrame>? ContentCrops = null,
    SmartBpSceneGateResult? SceneGate = null,
    SmartBpProgressAlignmentResult? ProgressAlignment = null,
    SmartBpProgressSyncResult? ProgressSync = null);

/// <summary>一次 llama.cpp 响应返回的性能信息。</summary>
public sealed record LlamaCppResponseMetrics(
    int? PromptTokens,
    int? CompletionTokens,
    int? TotalTokens,
    double? TokensPerSecond,
    long ElapsedMilliseconds);

/// <summary>一个 Qwen 模型配置档的校验后安装路径。</summary>
public sealed record QwenInstalledPaths(string ModelPath, string? MmprojPath, QwenMmprojMode MmprojMode);

/// <summary>步骤提交调度器返回的结果。</summary>
public sealed record SmartBpStepCommitResult(SmartBpBusinessStateRecognitionResult Snapshot,
    SmartBpWorkflowBackfillPlan Plan,
    SmartBpOperationApplyResult? ApplyResult,
    SmartBpGuidanceSyncResult? GuidanceSync,
    IReadOnlyList<string> Diagnostics);

/// <summary>SmartBP 滚动识别帧缓冲中保留的一帧画面。</summary>
public sealed record SmartBpBufferedFrame(long Sequence, BitmapSource Frame, DateTimeOffset Timestamp);

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

/// <summary>从 AI OCR 模型响应中提取出的一行技术文本。</summary>
public sealed class SmartBpAiOcrTranscriptLine
{
    /// <summary>获取或设置为传输和调试提取出的可见文本。</summary>
    public string Text { get; set; } = "";
}

/// <summary>AI OCR 转写识别结果。</summary>
public sealed class SmartBpAiOcrTranscriptResult
{
    /// <summary>获取未进行业务解释的技术转写行。</summary>
    public IReadOnlyList<SmartBpAiOcrTranscriptLine> Lines { get; init; } = [];
    /// <summary>获取 AI OCR 模型返回的原始输出。</summary>
    public string RawJson { get; init; } = "";
    /// <summary>获取有界诊断信息。</summary>
    public IReadOnlyList<string> Diagnostics { get; init; } = [];
}

/// <summary>单个粗粒度 BP 业务区域的 AI OCR 转写证据。</summary>
public sealed class SmartBpAiOcrTranscriptRegionEvidence
{
    /// <summary>获取来源 SmartBP 粗粒度区域。</summary>
    public SmartBpRecognitionRegion Region { get; init; }
    /// <summary>获取该区域代表的 SmartBP 业务字段。</summary>
    public string Field { get; init; } = "";
    /// <summary>获取产生该证据的 AI OCR 模型标识。</summary>
    public string AiOcrModel { get; init; } = "";
    /// <summary>获取 AI OCR 模型返回的原始输出。</summary>
    public string RawOutput { get; init; } = "";
    /// <summary>获取未经语义清理的技术转写行。</summary>
    public IReadOnlyList<string> TechnicalLines { get; init; } = [];
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

    /// <summary>从上下文解析 picked_sur 解析模式。</summary>
    /// <returns>解析模式。</returns>
    public SmartBpPickedSurOcrParseMode ResolvePickedSurParseMode()
    {
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
public record SmartBpDownloadState(bool IsDownloading, double? Progress, string Status,
    string? CurrentFileName = null,
    long? BytesReceived = null,
    long? TotalBytes = null,
    double? BytesPerSecond = null,
    TimeSpan? Eta = null,
    string? ErrorMessage = null);

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

/// <summary>一个可选 AI 运行时性能采样。</summary>
/// <param name="GpuName">GPU 显示名称。</param>
/// <param name="GpuUtilizationPercent">GPU 利用率百分比。</param>
/// <param name="VramUsedBytes">已用显存。</param>
/// <param name="VramTotalBytes">总显存。</param>
/// <param name="ProcessId">托管 llama-server 进程标识。</param>
/// <param name="UpdatedAt">采样时间戳。</param>
/// <param name="IsAvailable">NVML 遥测是否可用。</param>
public sealed record SmartBpAiPerformanceSnapshot(string GpuName, uint? GpuUtilizationPercent,
    ulong? VramUsedBytes, ulong? VramTotalBytes, int? ProcessId, DateTimeOffset UpdatedAt, bool IsAvailable);

/// <summary>暴露给 UI 的 Qwen 下载状态。</summary>
public sealed record QwenDownloadState(bool IsDownloading, double? Progress, string Status,
    string? CurrentFileName = null,
    long? BytesReceived = null,
    long? TotalBytes = null,
    double? BytesPerSecond = null,
    TimeSpan? Eta = null,
    string? ErrorMessage = null) : SmartBpDownloadState(IsDownloading, Progress, Status, CurrentFileName, BytesReceived, TotalBytes, BytesPerSecond, Eta, ErrorMessage);

/// <summary>一个内置识别提示词配置档。</summary>
public sealed record SmartBpPromptProfile(string Id, string DisplayName, string SystemPrompt);

/// <summary>llama.cpp 运行时清单根对象。</summary>
public sealed class LlamaCppRuntimeManifest
{
    /// <summary>获取或设置 schema 版本。</summary>
    public int SchemaVersion { get; set; }
    /// <summary>获取或设置上游运行时版本。</summary>
    public string RuntimeVersion { get; set; } = "";
    /// <summary>获取或设置发布页。</summary>
    public string ReleasePage { get; set; } = "";
    /// <summary>获取或设置运行时资产集合。</summary>
    public List<LlamaCppRuntimeAsset> Assets { get; set; } = [];
    /// <summary>获取或设置清单中声明的可选检查间隔。</summary>
    public int? CheckIntervalHours { get; set; }
}

/// <summary>一个可安装 llama.cpp 运行时压缩包。</summary>
public sealed class LlamaCppRuntimeAsset
{
    /// <summary>获取或设置资产标识。</summary>
    public string Id { get; set; } = "";
    /// <summary>获取或设置显示名称。</summary>
    public string DisplayName { get; set; } = "";
    /// <summary>获取或设置 CPU 架构。</summary>
    public string Architecture { get; set; } = "";
    /// <summary>获取或设置后端。</summary>
    public string Backend { get; set; } = "";
    /// <summary>获取或设置压缩包 URL。</summary>
    public string Url { get; set; } = "";
    /// <summary>获取或设置可选 SHA256。</summary>
    public string? Sha256 { get; set; }
    /// <summary>获取或设置可执行文件名。</summary>
    public string? EntryExe { get; set; }
    /// <summary>获取或设置必需的额外资产标识。</summary>
    public List<string> RequiredExtraAssets { get; set; } = [];
    /// <summary>获取或设置 URL 是否已经指向最终可下载文件。</summary>
    public bool UrlIsDirectDownload { get; set; }
}

/// <summary>托管 llama.cpp 运行时安装状态。</summary>
public sealed record LlamaCppRuntimeInstallState(bool IsDownloading, double? Progress, string Status,
    string? CurrentFileName = null,
    long? BytesReceived = null,
    long? TotalBytes = null,
    double? BytesPerSecond = null,
    TimeSpan? Eta = null,
    string? ErrorMessage = null) : SmartBpDownloadState(IsDownloading, Progress, Status, CurrentFileName, BytesReceived, TotalBytes, BytesPerSecond, Eta, ErrorMessage);

/// <summary>llama.cpp 运行时更新检查结果。</summary>
public sealed record LlamaCppRuntimeUpdateCheckResult(bool Checked, bool HasUpdate, string CurrentVersion,
    string? LatestVersion, IReadOnlyList<LlamaCppRuntimeAsset> LatestAssets, string Message);

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
public sealed record SmartBpNormalizedCharacter(string? RawCharacterName, string? ResolvedCharacterKey,
    string? ResolvedCharacterName, Camp Camp, int SlotIndex, double Confidence, IReadOnlyList<string> Warnings,
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
