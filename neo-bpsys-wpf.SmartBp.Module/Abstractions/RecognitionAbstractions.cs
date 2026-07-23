using System.Windows.Media.Imaging;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Abstractions;

/// <summary>安装并校验 Tesseract 语言数据。</summary>
public interface ITesseractDataAssetManager
{
    /// <summary>下载状态变化时触发。</summary>
    event EventHandler<SmartBpDownloadState>? StateChanged;
    /// <summary>获取当前语言数据状态。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>校验后的状态。</returns>
    Task<TesseractDataStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    /// <summary>获取 SmartBP 可管理的全部语言数据资产。</summary>
    /// <returns>可用的 Tesseract 语言资产。</returns>
    IReadOnlyList<TesseractLanguageAsset> GetAvailableLanguages();
    /// <summary>安装尚未安装的指定语言数据资产。</summary>
    /// <param name="languages">要安装的语言标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示安装操作的任务。</returns>
    Task InstallLanguagesAsync(IEnumerable<string> languages, CancellationToken cancellationToken = default);
    /// <summary>从有效 tessdata 目录删除指定的托管语言数据文件。</summary>
    /// <param name="languages">要删除的语言标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示删除操作的任务。</returns>
    Task DeleteAsync(IEnumerable<string> languages, CancellationToken cancellationToken = default);
    /// <summary>取消当前下载。</summary>
    void Cancel();
}

/// <summary>读取托管 AI 运行时可选的 NVIDIA GPU 遥测信息。</summary>
public interface ISmartBpAiPerformanceMonitor
{
    /// <summary>获取最新的 GPU 与 llama-server 进程快照。</summary>
    /// <param name="processId">托管 llama-server 进程标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>当前性能快照。</returns>
    Task<SmartBpAiPerformanceSnapshot> GetSnapshotAsync(int? processId, CancellationToken cancellationToken = default);
}

/// <summary>加载内置 Qwen 元数据。</summary>
public interface IQwenModelManifestProvider { /// <summary>加载并校验清单。</summary>
    Task<QwenModelManifest> LoadAsync(CancellationToken cancellationToken = default); }

/// <summary>加载内置本地视觉模型元数据。</summary>
public interface ILocalVisionModelManifestProvider
{
    /// <summary>加载并校验清单。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>本地视觉模型清单。</returns>
    Task<LocalVisionModelManifest> LoadAsync(CancellationToken cancellationToken = default);
}

/// <summary>加载内置 RapidOCR 模型元数据。</summary>
public interface IRapidOcrModelManifestProvider
{
    /// <summary>加载并校验 RapidOCR 清单。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>校验后的清单。</returns>
    Task<RapidOcrModelManifest> LoadAsync(CancellationToken cancellationToken = default);
}

/// <summary>安装并校验托管 RapidOCR 中文模型资产。</summary>
public interface IRapidOcrModelAssetManager
{
    /// <summary>安装状态变化时触发。</summary>
    event EventHandler<SmartBpDownloadState>? StateChanged;
    /// <summary>获取最近一次计算出的模型状态。</summary>
    RapidOcrModelStatus Status { get; }
    /// <summary>获取已选配置档的状态。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已选模型状态。</returns>
    Task<RapidOcrModelStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    /// <summary>获取可用的 RapidOCR 配置档。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>可用配置档集合。</returns>
    Task<IReadOnlyList<RapidOcrModelProfile>> GetAvailableProfilesAsync(CancellationToken cancellationToken = default);
    /// <summary>根据 RapidOCR 官方在线清单检查指定配置档。</summary>
    /// <param name="profileId">要检查的配置档标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已安装、内置和官方版本的比较结果。</returns>
    Task<RapidOcrModelUpdateCheckResult> CheckForUpdatesAsync(
        string profileId,
        CancellationToken cancellationToken = default);
    /// <summary>安装一个 RapidOCR 模型配置档。</summary>
    /// <param name="profileId">配置档标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示安装操作的任务。</returns>
    Task InstallAsync(string profileId, CancellationToken cancellationToken = default);
    /// <summary>删除一个托管 RapidOCR 配置档。</summary>
    /// <param name="profileId">配置档标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示删除操作的任务。</returns>
    Task DeleteAsync(string profileId, CancellationToken cancellationToken = default);
    /// <summary>取消当前安装。</summary>
    void Cancel();
    /// <summary>获取已选配置档的校验后安装路径。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>安装路径。</returns>
    Task<RapidOcrInstalledPaths> GetInstalledPathsAsync(CancellationToken cancellationToken = default);
}
/// <summary>安装和移除 Qwen 模型资产。</summary>
public interface IQwenModelAssetManager
{
    /// <summary>下载状态变化时触发。</summary>
    event EventHandler<QwenDownloadState>? StateChanged;
    /// <summary>获取当前下载状态。</summary>
    QwenDownloadState State { get; }
    /// <summary>获取已选配置档。</summary>
    Task<QwenModelProfile> GetProfileAsync(CancellationToken cancellationToken = default);
    /// <summary>获取指定本地视觉模型配置档。</summary>
    /// <param name="modelId">模型配置档标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>匹配的配置档。</returns>
    Task<QwenModelProfile> GetProfileAsync(string modelId, CancellationToken cancellationToken = default);
    /// <summary>获取所有可选 Qwen 模型配置档。</summary>
    Task<IReadOnlyList<QwenModelProfile>> GetProfilesAsync(CancellationToken cancellationToken = default);
    /// <summary>检查已安装资产，包括哈希校验。</summary>
    Task<bool> IsInstalledAsync(CancellationToken cancellationToken = default);
    /// <summary>检查指定 Qwen 模型配置档的已安装资产，包括哈希校验。</summary>
    /// <param name="modelId">Qwen 模型配置档标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已选模型文件已安装且有效时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
    Task<bool> IsInstalledAsync(string modelId, CancellationToken cancellationToken = default);
    /// <summary>下载缺失资产。</summary>
    Task InstallAsync(CancellationToken cancellationToken = default);
    /// <summary>下载指定 Qwen 模型配置档的缺失资产。</summary>
    /// <param name="modelId">Qwen 模型配置档标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示下载操作的任务。</returns>
    Task InstallAsync(string modelId, CancellationToken cancellationToken = default);
    /// <summary>取消当前下载。</summary>
    void Cancel();
    /// <summary>删除已安装资产，不阻塞调用线程。</summary>
    Task DeleteAsync(CancellationToken cancellationToken = default);
    /// <summary>删除指定 Qwen 模型配置档的已安装资产，不阻塞调用线程。</summary>
    /// <param name="modelId">Qwen 模型配置档标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示删除操作的任务。</returns>
    Task DeleteAsync(string modelId, CancellationToken cancellationToken = default);
    /// <summary>获取已安装模型和投影器路径。</summary>
    Task<QwenInstalledPaths> GetInstalledPathsAsync(CancellationToken cancellationToken = default);
    /// <summary>获取指定本地视觉模型配置档的已安装模型和投影器路径。</summary>
    /// <param name="modelId">模型配置档标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>校验后的安装路径。</returns>
    Task<QwenInstalledPaths> GetInstalledPathsAsync(string modelId, CancellationToken cancellationToken = default);
}

/// <summary>安装和移除托管本地视觉模型资产。</summary>
public interface ILocalVisionModelAssetManager : IQwenModelAssetManager;
/// <summary>持久化识别设置。</summary>
public interface ISmartBpRecognitionSettingsService
{
    /// <summary>获取当前设置。</summary>
    SmartBpRecognitionSettings Settings { get; }
    /// <summary>保存当前设置。</summary>
    Task SaveAsync(CancellationToken cancellationToken = default);
}
/// <summary>加载内置 SmartBP 识别提示词配置档。</summary>
public interface ISmartBpPromptProfileProvider
{
    /// <summary>获取可用的内置配置档。</summary>
    Task<IReadOnlyList<SmartBpPromptProfile>> GetAvailableProfilesAsync(CancellationToken cancellationToken = default);
    /// <summary>按标识加载一个配置档。</summary>
    Task<SmartBpPromptProfile> LoadAsync(string profileId, CancellationToken cancellationToken = default);
}
/// <summary>在不提取字段的情况下分类场景和阶段。</summary>
public interface ISmartBpScenePhaseController
{
    /// <summary>从画面帧识别场景和阶段。</summary>
    /// <param name="frame">源画面帧。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>场景与阶段决策。</returns>
    Task<SmartBpScenePhaseDecision> RecognizeAsync(BitmapSource frame, CancellationToken cancellationToken = default);
}

/// <summary>加载并持久化 SmartBP 粗粒度识别裁剪布局配置档。</summary>
public interface ISmartBpRecognitionRegionProfileService
{
    /// <summary>加载用户覆盖配置档；不存在覆盖时加载内置默认配置档。</summary>
    Task<SmartBpRecognitionLayoutProfile> LoadAsync(CancellationToken cancellationToken = default);
    /// <summary>保存用户覆盖配置档。</summary>
    Task SaveUserOverrideAsync(SmartBpRecognitionLayoutProfile profile, CancellationToken cancellationToken = default);
    /// <summary>删除用户覆盖，使系统重新使用内置默认配置档。</summary>
    Task ResetUserOverrideAsync(CancellationToken cancellationToken = default);
}
/// <summary>将 SmartBP 识别画面裁剪为粗粒度 BP 区域。</summary>
public interface ISmartBpRecognitionFrameCropper
{
    /// <summary>将画面裁剪到请求的粗粒度区域，并返回诊断信息。</summary>
    SmartBpCroppedFrame CropWithInfo(BitmapSource source, SmartBpRecognitionRegion region);
    /// <summary>将画面裁剪到请求的粗粒度区域。</summary>
    BitmapSource Crop(BitmapSource source, SmartBpRecognitionRegion region);
}
/// <summary>保留最近画面帧，用于转场最终确认。</summary>
public interface ISmartBpFrameRingBuffer
{
    /// <summary>添加一帧捕获画面。</summary>
    void AddFrame(long sequence, BitmapSource frame, DateTimeOffset timestamp);
    /// <summary>获取指定时间窗口内的最近画面帧。</summary>
    IReadOnlyList<SmartBpBufferedFrame> GetRecentFrames(TimeSpan window);
    /// <summary>获取指定区域最合适的最近画面帧。</summary>
    SmartBpBufferedFrame? GetBestFrameForRegion(SmartBpRecognitionRegion region, TimeSpan lookBehind);
}
/// <summary>检测裁剪识别区域是否变化到需要刷新。</summary>
public interface ISmartBpCropChangeDetector
{
    /// <summary>分析一张裁剪图，并返回轻量变化结果。</summary>
    SmartBpCropChangeResult Analyze(SmartBpRecognitionRegion region, BitmapSource crop, long sequence);
}
/// <summary>根据多个 SmartBP 识别区域构建单张 OCR 拼接图。</summary>
public interface ISmartBpOcrContactSheetBuilder
{
    /// <summary>构建无标签拼接图及坐标映射。</summary>
    /// <param name="frame">源画面帧。</param>
    /// <param name="regions">请求区域。</param>
    /// <returns>拼接图及映射信息。</returns>
    SmartBpOcrContactSheet Build(BitmapSource frame, IReadOnlyList<SmartBpRecognitionRegion> regions);
}
/// <summary>将 OCR 文本行解析为规范角色名称。</summary>
public interface ISmartBpOcrTextResolver
{
    /// <summary>将一行 OCR 文本解析为候选角色。</summary>
    /// <param name="text">OCR 文本。</param>
    /// <param name="camp">目标阵营。</param>
    /// <param name="slotIndex">视觉槽位索引。</param>
    /// <param name="provider">可选 OCR Provider名称。</param>
    /// <returns>已解析角色信息，或未解析详情。</returns>
    SmartBpNormalizedCharacter ResolveCharacterFromLine(string text, Core.Enums.Camp camp, int slotIndex, string? provider = null);
}
/// <summary>根据 PaddleOCR 文本和边界框识别 BP 状态。</summary>
public interface ISmartBpOcrBpRecognitionService
{
    /// <summary>运行一次 OCR BP 识别。</summary>
    /// <param name="frame">源画面帧。</param>
    /// <param name="request">请求的 OCR 区域。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>OCR 识别结果。</returns>
    Task<SmartBpOcrRecognitionResult> RecognizeAsync(
        BitmapSource frame,
        SmartBpOcrRecognitionRequest request,
        CancellationToken cancellationToken = default);
}
/// <summary>识别一次增量 OCR 快照增量。</summary>
public interface ISmartBpOcrSnapshotDeltaRecognitionService
{
    /// <summary>从单帧画面识别请求的 OCR 增量包。</summary>
    Task<(SmartBpSnapshotDeltaResult Delta, IReadOnlyDictionary<string, string> RawResponses, SmartBpCroppedFrame PhaseCrop, IReadOnlyList<SmartBpCroppedFrame> ContentCrops, IReadOnlyList<string> Diagnostics)> RecognizeDeltaAsync(
        BitmapSource frame,
        SmartBpSnapshotDeltaRequest request,
        long frameSequence,
        CancellationToken cancellationToken = default);
}
/// <summary>识别并本地合并阶段和四个粗粒度 BP 内容区域。</summary>
public interface ISmartBpRegionSnapshotRecognitionService
{
    /// <summary>识别一个受区域门控的 BP 快照。</summary>
    Task<SmartBpRegionSnapshot> RecognizeSnapshotAsync(BitmapSource frame, SmartBpRegionSnapshotRecognitionMode mode, CancellationToken cancellationToken = default);
}
/// <summary>识别一次多区域 SmartBP 增量快照增量。</summary>
public interface ISmartBpSnapshotDeltaRecognitionService
{
    /// <summary>从单帧画面识别请求的增量包。</summary>
    Task<(SmartBpSnapshotDeltaResult Delta, IReadOnlyDictionary<string, string> RawResponses, SmartBpCroppedFrame PhaseCrop, IReadOnlyList<SmartBpCroppedFrame> ContentCrops, IReadOnlyList<string> Diagnostics)> RecognizeDeltaAsync(
        BitmapSource frame,
        SmartBpSnapshotDeltaRequest request,
        long frameSequence,
        CancellationToken cancellationToken = default);
}
/// <summary>使用独立 AI 请求识别 BP 阶段和单个字段快照。</summary>
public interface ISmartBpAiFieldSnapshotRecognitionService
{
    /// <summary>仅识别阶段裁剪图，不产生业务字段更新。</summary>
    /// <param name="frame">源画面帧。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>仅阶段识别结果。</returns>
    Task<SmartBpAiPhaseOnlyResult> RecognizePhaseOnlyAsync(BitmapSource frame, CancellationToken cancellationToken = default);
    /// <summary>从裁剪区域识别一个业务字段当前可见快照。</summary>
    /// <param name="frame">源画面帧。</param>
    /// <param name="region">拥有该字段的粗粒度裁剪区域。</param>
    /// <param name="field">业务字段标识（banned_sur、banned_hun、picked_sur、picked_hun）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>字段快照识别结果。</returns>
    Task<SmartBpAiFieldSnapshotResult> RecognizeFieldAsync(
        BitmapSource frame,
        SmartBpRecognitionRegion region,
        string field,
        CancellationToken cancellationToken = default);
}
/// <summary>存储本地合并后的 SmartBP 增量识别状态。</summary>
public interface ISmartBpRecognitionStateStore
{
    /// <summary>获取完整业务状态快照。</summary>
    SmartBpBusinessStateRecognitionResult Snapshot { get; }
    /// <summary>将一个模型增量应用到本地状态。</summary>
    IReadOnlyList<string> ApplyDelta(SmartBpSnapshotDeltaResult delta, long frameSequence, DateTimeOffset timestamp);
    /// <summary>使用逐槽位合并规则将一个字段快照应用到本地状态。</summary>
    /// <param name="field">业务字段标识。</param>
    /// <param name="snapshot">携带 slot_state 证据的字段快照更新。</param>
    /// <param name="frameSequence">画面帧序号。</param>
    /// <param name="timestamp">应用时间戳。</param>
    /// <returns>逐槽位合并诊断信息。</returns>
    IReadOnlyList<string> ApplyFieldSnapshot(string field, SmartBpSnapshotFieldUpdate snapshot, long frameSequence, DateTimeOffset timestamp);
    /// <summary>
    /// 在求生者选择锁定后，将 picked_sur 视觉槽位证据替换为分配证据，而不按视觉槽位索引合并到 <c>PickedSur</c>。
    /// </summary>
    /// <param name="update">携带 picked_sur 视觉槽位的字段快照更新。</param>
    /// <param name="frameSequence">画面帧序号。</param>
    /// <param name="timestamp">应用时间戳。</param>
    /// <returns>分配证据更新诊断信息。</returns>
    IReadOnlyList<string> ApplyDistributionEvidence(SmartBpSnapshotFieldUpdate update, long frameSequence, DateTimeOffset timestamp);
    /// <summary>仅更新本地合并后的阶段。</summary>
    /// <param name="phase">识别到的阶段。</param>
    /// <param name="frameSequence">画面帧序号。</param>
    void ApplyPhase(string phase, long frameSequence);
    /// <summary>返回字段陈旧状态诊断。</summary>
    IReadOnlyList<string> GetStaleFieldDiagnostics(DateTimeOffset timestamp, int staleMilliseconds);
    /// <summary>重置所有本地合并状态。</summary>
    void Reset();
}
/// <summary>规划下一次增量请求应刷新哪些裁剪区域。</summary>
public interface ISmartBpSnapshotRecognitionPlanner
{
    /// <summary>构建下一次识别请求包。</summary>
    SmartBpSnapshotDeltaRequest BuildRequest(Core.Models.GameGuidanceRuntimeSnapshot guidanceSnapshot,
        SmartBpBusinessStateRecognitionResult currentLocalSnapshot,
        SmartBpRecognitionLedgerSnapshot ledgerSnapshot);
}
/// <summary>将独立内容区域输出合并为简化 BP 业务状态。</summary>
public interface ISmartBpBusinessStateMerger
{
    /// <summary>合并权威阶段和四个可选区域结果。</summary>
    SmartBpBusinessStateRecognitionResult Merge(SmartBpPhaseRecognitionResult phase,
        SmartBpFocusedBusinessExtractionResult? bannedSur,
        SmartBpFocusedBusinessExtractionResult? bannedHun,
        SmartBpFocusedBusinessExtractionResult? pickedSur,
        SmartBpFocusedBusinessExtractionResult? pickedHun);
}
/// <summary>分类当前第五人格场景并门控 BP 写入。</summary>
public interface ISmartBpSceneGateService
{
    /// <summary>在不修改游戏状态的情况下分类场景证据。</summary>
    SmartBpSceneGateResult Classify(
        SmartBpPhaseRecognitionResult phase,
        SmartBpBusinessStateRecognitionResult state,
        IReadOnlyDictionary<string, string> rawResponses,
        Core.Models.GameGuidanceRuntimeSnapshot guidanceSnapshot);
}
/// <summary>根据共享角色字典解析模型输出名称。</summary>
public interface ISmartBpCharacterResolver { /// <summary>安全解析角色名称。</summary>
    SmartBpNormalizedCharacter Resolve(string? rawName, Core.Enums.Camp camp, int slot, double confidence); }
/// <summary>将 OCR 识别到的玩家 ID 文本匹配到内部求生者玩家位置。</summary>
public interface ISmartBpPlayerIdentityMatcher
{
    /// <summary>将原始 player_id 文本匹配到当前对局内部求生者玩家。</summary>
    /// <param name="rawPlayerId">OCR 识别到的玩家 ID 文本。</param>
    /// <returns>匹配结果，包含内部索引、匹配名称、分数、是否安全及原因。</returns>
    SmartBpPlayerIdentityMatchResult MatchSurvivorPlayer(string? rawPlayerId);
}
/// <summary>运行并归一化一次识别请求。</summary>
public interface ISmartBpAiRecognitionService { /// <summary>识别一帧画面。</summary>
    Task<SmartBpRecognitionPreview> RecognizeAsync(BitmapSource frame, SmartBpRecognitionTask task, CancellationToken cancellationToken = default); }

/// <summary>为 SmartBP AI 流水线发布有界且用户可见的诊断信息。</summary>
public interface ISmartBpDebugLog
{
    /// <summary>写入诊断行时触发。</summary>
    event EventHandler<SmartBpDebugMessageEventArgs>? MessageWritten;
    /// <summary>写入一条诊断行；当 <see cref="IsEnabled"/> 为 false 时不执行任何操作。</summary>
    /// <param name="source">短子系统名称。</param>
    /// <param name="message">诊断消息。</param>
    void Write(string source, string message);
    /// <summary>为 false 时，<see cref="Write"/> 不执行任何操作且不触发事件。</summary>
    bool IsEnabled { get; set; }
}

/// <summary>将模型阶段输出与权威 GameGuidance 工作流进行对齐。</summary>
public interface ISmartBpGuidanceSyncService
{
    /// <summary>同步到当前步骤或最近的兼容未来步骤。</summary>
    Task<SmartBpGuidanceSyncResult> SyncAsync(SmartBpBusinessStateRecognitionResult businessState, CancellationToken cancellationToken = default);
}

/// <summary>根据完整 SmartBP 业务快照推断最符合的 GameGuidance 工作流步骤。</summary>
public interface ISmartBpProgressInferenceService
{
    /// <summary>推断当前画面最符合的 GameGuidance 工作流步骤。</summary>
    /// <param name="observed">观察到的完整 SmartBP 业务状态。</param>
    /// <param name="guidanceSnapshot">当前 GameGuidance 运行时快照。</param>
    /// <param name="options">可选推断阈值和范围。</param>
    /// <returns>进度推断结果。</returns>
    SmartBpProgressInferenceResult Infer(
        SmartBpBusinessStateRecognitionResult observed,
        GameGuidanceRuntimeSnapshot guidanceSnapshot,
        SmartBpProgressInferenceOptions? options = null);
}

/// <summary>执行 SmartBP 精确进度对齐检查和强制同步。</summary>
public interface ISmartBpProgressSyncService
{
    /// <summary>检查观察到的 SmartBP 业务状态与 GameGuidance 当前步骤是否一致。</summary>
    /// <param name="observed">观察到的完整 SmartBP 业务状态。</param>
    /// <param name="guidanceSnapshot">当前 GameGuidance 运行时快照。</param>
    /// <param name="options">可选推断阈值和范围。</param>
    /// <returns>对齐检查结果。</returns>
    SmartBpProgressAlignmentResult CheckAlignment(
        SmartBpBusinessStateRecognitionResult observed,
        GameGuidanceRuntimeSnapshot guidanceSnapshot,
        SmartBpProgressInferenceOptions? options = null);

    /// <summary>将 GameGuidance 强制同步到根据观察状态推断出的精确步骤。</summary>
    /// <param name="observed">观察到的完整 SmartBP 业务状态。</param>
    /// <param name="mode">同步模式。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>同步结果。</returns>
    Task<SmartBpProgressSyncResult> ForceSyncAsync(
        SmartBpBusinessStateRecognitionResult observed,
        SmartBpProgressSyncMode mode,
        CancellationToken cancellationToken = default);
}

/// <summary>执行手动 SmartBP 对局状态同步。</summary>
public interface ISmartBpGameStateSyncService
{
    /// <summary>根据完整 BP 快照同步对局引导与可靠的角色状态。</summary>
    /// <param name="observed">观察到的完整 SmartBP 业务状态。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>进度对齐和角色状态应用的汇总结果。</returns>
    Task<SmartBpGameStateSyncResult> ForceSyncAsync(
        SmartBpBusinessStateRecognitionResult observed,
        CancellationToken cancellationToken = default);
}

/// <summary>通过角色选择服务应用本地校验后的候选操作。</summary>
public interface ISmartBpDetectedOperationApplier
{
    /// <summary>应用已接受且已解析的操作。</summary>
    Task<SmartBpOperationApplyResult> ApplyAsync(IReadOnlyList<SmartBpDetectedOperation> operations, CancellationToken cancellationToken = default);
}

/// <summary>在阶段切换时从帧缓冲构建上一角色步骤的高置信回看纠正计划。</summary>
public interface ISmartBpTransitionReplayService
{
    /// <summary>构建一次不修改状态的回看计划。</summary>
    /// <param name="sourceGuidance">切换前的引导快照。</param>
    /// <param name="targetAction">当前识别到的目标动作。</param>
    /// <param name="currentFrameSequence">当前帧序号；该帧不会被作为历史帧回看。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>有序候选与诊断。</returns>
    Task<SmartBpTransitionReplayResult?> BuildAsync(GameGuidanceRuntimeSnapshot sourceGuidance,
        Core.Enums.GameAction targetAction, long currentFrameSequence, CancellationToken cancellationToken = default);
}

/// <summary>根据完整合并后的 BP 快照构建有序工作流回填候选。</summary>
public interface ISmartBpWorkflowBackfillService
{
    /// <summary>在不修改引导状态的情况下，根据当前工作流构建计划。</summary>
    SmartBpWorkflowBackfillPlan BuildPlan(SmartBpBusinessStateRecognitionResult snapshot, Core.Models.GameGuidanceRuntimeSnapshot guidanceSnapshot);
}

/// <summary>跟踪当前对局进度中已成功完成的工作流操作。</summary>
public interface ISmartBpRecognitionLedger
{
    /// <summary>返回操作是否已经完成。</summary>
    bool IsStepOperationCompleted(SmartBpWorkflowOperationKey key);
    /// <summary>在应用成功或确认无需操作后，将操作标记为完成。</summary>
    void MarkCompleted(SmartBpWorkflowOperationKey key);
    /// <summary>记录非终止性跳过原因，但不将操作标记为完成。</summary>
    void MarkSkipped(SmartBpWorkflowOperationKey key, string reason);
    /// <summary>清除当前对局的所有识别状态。</summary>
    void ResetForCurrentGame();
    /// <summary>返回用于规划的只读快照。</summary>
    SmartBpRecognitionLedgerSnapshot GetSnapshot();
}

/// <summary>协调阶段检测、引导对齐和聚焦提取。</summary>
public interface ISmartBpAutoRecognitionCoordinator
{
    /// <summary>获取自动模式是否正在运行。</summary>
    bool IsRunning { get; }
    /// <summary>启动自动模式。</summary>
    Task StartAsync(CancellationToken cancellationToken = default);
    /// <summary>停止自动模式。</summary>
    Task StopAsync();
    /// <summary>完成自动模式，但不取消当前 tick 令牌。</summary>
    Task CompleteAsync();
    /// <summary>运行一次感知阶段的自动识别 tick。</summary>
    Task<SmartBpAutoRecognitionTickResult> RunOneTickAsync(BitmapSource frame, CancellationToken cancellationToken = default);
    /// <summary>运行一次感知阶段的识别 tick，但不应用增量、角色操作或引导同步。</summary>
    Task<SmartBpAutoRecognitionTickResult> RunOneTickDryRunAsync(BitmapSource frame, CancellationToken cancellationToken = default);
    /// <summary>以所有 BP 业务字段为请求范围运行已选识别策略，用于调试。</summary>
    /// <param name="frame">要识别的画面帧。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>完整策略识别结果。</returns>
    Task<SmartBpAutoRecognitionTickResult> RunFullRecognitionDebugAsync(BitmapSource frame, CancellationToken cancellationToken = default);
    /// <summary>识别当前帧的完整 BP 业务快照，可选合并到自动识别状态仓库。</summary>
    /// <param name="frame">要识别的画面帧。</param>
    /// <param name="mergeIntoStateStore">是否将识别结果合并到状态仓库。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>完整 BP 快照识别结果。</returns>
    Task<SmartBpAutoRecognitionTickResult> RecognizeFullBpSnapshotAsync(
        BitmapSource frame,
        bool mergeIntoStateStore,
        CancellationToken cancellationToken = default);
    /// <summary>使用自动规划器请求形态运行已选策略，但不应用操作或引导变化。</summary>
    /// <param name="frame">要识别的画面帧。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>增量策略预览结果。</returns>
    Task<SmartBpAutoRecognitionTickResult> RunIncrementalRecognitionDebugAsync(BitmapSource frame, CancellationToken cancellationToken = default);
    /// <summary>仅运行阶段和场景识别，用于调试。</summary>
    /// <param name="frame">要识别的画面帧。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>仅阶段识别结果。</returns>
    Task<SmartBpAutoRecognitionTickResult> RunPhaseOnlyDebugAsync(BitmapSource frame, CancellationToken cancellationToken = default);
}

/// <summary>在不修改游戏状态的情况下，分类顶部中间 BP 生命周期区域的 OCR 文本。</summary>
public interface ISmartBpLifecycleStatusDetector
{
    /// <summary>使用归一化模糊评分分类顶部中间 OCR 行。</summary>
    /// <param name="lines">来自 <see cref="Models.Recognition.SmartBpRecognitionRegion.TopCenterStatus"/> 的 OCR 行。</param>
    /// <returns>确定性的生命周期分类和诊断信息。</returns>
    SmartBpLifecycleStatusResult Detect(IReadOnlyList<Core.Abstractions.Services.OcrTextLine> lines);
}

/// <summary>拥有一次自动步骤提交事务。</summary>
public interface ISmartBpStepCommitScheduler
{
    /// <summary>通过识别、应用和可选引导同步处理一帧画面。</summary>
    Task<SmartBpStepCommitResult> ProcessTickAsync(BitmapSource frame, CancellationToken cancellationToken = default);
}
