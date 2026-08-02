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
    /// <summary>暂停当前下载。</summary>
    void Pause();
    /// <summary>恢复当前下载。</summary>
    void Resume();
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
    /// <summary>暂停当前安装下载。</summary>
    void Pause();
    /// <summary>恢复当前安装下载。</summary>
    void Resume();
    /// <summary>获取已选配置档的校验后安装路径。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>安装路径。</returns>
    Task<RapidOcrInstalledPaths> GetInstalledPathsAsync(CancellationToken cancellationToken = default);
}

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
    /// <summary>获取当前保留帧数。</summary>
    int Count { get; }
    /// <summary>获取根据 OCR 周期、处理时间估计和转场窗口计算出的有界容量。</summary>
    int Capacity { get; }
    /// <summary>添加一帧捕获画面。</summary>
    void AddFrame(long sequence, BitmapSource frame, DateTimeOffset timestamp);
    /// <summary>获取指定时间窗口内的最近画面帧。</summary>
    IReadOnlyList<SmartBpBufferedFrame> GetRecentFrames(TimeSpan window);
    /// <summary>获取指定区域最合适的最近画面帧。</summary>
    SmartBpBufferedFrame? GetBestFrameForRegion(SmartBpRecognitionRegion region, TimeSpan lookBehind);
    /// <summary>清除全部缓冲帧，例如捕获源切换时隔离旧画面。</summary>
    void Reset();
    /// <summary>报告一次串行 OCR 的实际耗时，用于动态计算缓冲窗口下限。</summary>
    /// <param name="duration">本次处理耗时。</param>
    void ReportOcrProcessingDuration(TimeSpan duration);
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
/// <summary>规划当前帧需要识别的裁剪区域。</summary>
public interface ISmartBpSnapshotRecognitionPlanner
{
    /// <summary>基于主程序引导快照构建当前帧识别请求包。</summary>
    /// <param name="guidanceSnapshot">主程序对局引导快照。</param>
    /// <returns>当前帧区域请求。</returns>
    SmartBpSnapshotDeltaRequest BuildRequest(Core.Models.GameGuidanceRuntimeSnapshot guidanceSnapshot);
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

/// <summary>
/// 以主程序 BP 槽位提交状态为唯一业务状态源，统一执行角色、空操作和 Guidance 对账。
/// </summary>
public interface ISmartBpReconciliationService
{
    /// <summary>
    /// 将当前帧 Observation 投影与主程序权威状态对账。
    /// </summary>
    /// <param name="observed">当前帧或一次独立强制 OCR 的视觉证据。</param>
    /// <param name="mode">自动或手动强制同步模式。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>角色、明确空操作和 Guidance 相互独立的结构化结果。</returns>
    Task<SmartBpReconciliationResult> ReconcileAsync(
        SmartBpBusinessStateRecognitionResult observed,
        SmartBpReconciliationMode mode,
        CancellationToken cancellationToken = default);
}

/// <summary>通过角色选择服务应用本地校验后的候选操作。</summary>
public interface ISmartBpDetectedOperationApplier
{
    /// <summary>应用已接受且已解析的操作。</summary>
    Task<SmartBpOperationApplyResult> ApplyAsync(IReadOnlyList<SmartBpDetectedOperation> operations, CancellationToken cancellationToken = default);
}

/// <summary>协调阶段检测、引导对齐和聚焦提取。</summary>
public interface ISmartBpAutoRecognitionCoordinator
{
    /// <summary>获取自动模式是否正在运行。</summary>
    bool IsRunning { get; }
    /// <summary>在不启动 OCR 的情况下将一帧加入高频有界缓冲。</summary>
    /// <param name="frame">已冻结或可跨线程读取的捕获帧。</param>
    void SampleFrame(BitmapSource frame);
    /// <summary>捕获源变化时取消旧 OCR，并清除旧画面 Observation 与帧缓冲。</summary>
    void ResetCaptureContext();
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
    /// <param name="isDryRun">是否只返回当前帧识别结果而禁止应用和引导变化。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>完整 BP 快照识别结果。</returns>
    Task<SmartBpAutoRecognitionTickResult> RecognizeFullBpSnapshotAsync(
        BitmapSource frame,
        bool isDryRun,
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
