using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

/// <summary>
/// 根据 OCR 识别证据和当前引导状态判断是否允许自动写入 BP 数据。
/// </summary>
internal sealed class SmartBpSceneGateService : ISmartBpSceneGateService
{
    /// <inheritdoc />
    public SmartBpSceneGateResult Classify(
        SmartBpPhaseRecognitionResult phase,
        SmartBpBusinessStateRecognitionResult state,
        IReadOnlyDictionary<string, string> rawResponses,
        GameGuidanceRuntimeSnapshot guidanceSnapshot)
    {
        var evidence = string.Join('\n', rawResponses.Values.Append(phase.Phase).Append(state.Phase));
        if (Contains(evidence, "对局中", "密码机尚未破译", "地窖未刷新", "监管者投降"))
            return PostBp(SmartBpRecognitionScene.InGame);
        if (Contains(evidence, "监管者选择区域中"))
            return PostBp(SmartBpRecognitionScene.AreaSelectionHunter);
        if (Contains(evidence, "求生者选择区域中"))
            return PostBp(SmartBpRecognitionScene.AreaSelectionSurvivor);
        if (Contains(evidence, "即将进入区域选择", "区域选择"))
            return PostBp(SmartBpRecognitionScene.OutOfBp);
        if (Contains(evidence, "等待游戏开始"))
            return PostBp(SmartBpRecognitionScene.WaitingGameStart);
        if (Contains(evidence, "加载中", "正在加载"))
            return PostBp(SmartBpRecognitionScene.Loading);
        if (Contains(evidence, "天赋已锁定"))
            return GuidanceOnly(SmartBpRecognitionScene.TalentLocked, false, "talent is locked; BP cleanup may still be pending");
        if (Contains(evidence, "求生者天赋特质调整", "求生者选择天赋中"))
            return VisibleCharacterRecovery(SmartBpRecognitionScene.SurvivorTalent,
                "survivor talent adjustment keeps survivor Ban/Pick evidence visible for current-frame recovery");
        if (Contains(evidence, "监管者天赋特质调整", "监管者选择天赋中"))
            return VisibleCharacterRecovery(SmartBpRecognitionScene.HunterTalent,
                "hunter talent adjustment keeps hunter Ban/Pick evidence visible for current-frame recovery");
        if (Contains(evidence, "查看禁选顺序", "选择禁用数量"))
            return Block(SmartBpRecognitionScene.BanPickOrderDialog, false, "pre-BP ban/pick order dialog");
        if (Contains(evidence, "规则设置"))
            return Block(SmartBpRecognitionScene.RulesDialog, false, "pre-BP rules settings scene");
        if (Contains(evidence, "大厅"))
            return Block(SmartBpRecognitionScene.Lobby, false, "pre-BP lobby scene");
        if (Contains(evidence, "开始案件还原", "阵容选择中", "前往【"))
            return Block(SmartBpRecognitionScene.Transition, false, "pre-BP transition scene");
        if (Contains(evidence, "屏蔽求生者", "屏蔽监管者", "选择求生者", "求生者选择角色中") ||
            SmartBpAutomaticMapping.TryMapPhase(state.Phase, out _))
            return new(SmartBpRecognitionScene.CharacterBp, true, true, false, "character BP scene detected");
        if (!guidanceSnapshot.IsStarted)
            return Block(SmartBpRecognitionScene.Lobby, false, "pre-BP lobby scene");
        return Block(SmartBpRecognitionScene.Unknown, false, "scene evidence is insufficient; BP writes are blocked");
    }

    private static bool Contains(string text, params string[] hints) =>
        hints.Any(hint => text.Contains(hint, StringComparison.OrdinalIgnoreCase));

    private static SmartBpSceneGateResult Block(SmartBpRecognitionScene scene, bool pause, string reason) =>
        new(scene, false, false, pause, reason);

    private static SmartBpSceneGateResult GuidanceOnly(SmartBpRecognitionScene scene, bool pause, string reason) =>
        new(scene, true, false, pause, reason);

    private static SmartBpSceneGateResult VisibleCharacterRecovery(SmartBpRecognitionScene scene, string reason) =>
        new(scene, true, true, false, reason);

    private static SmartBpSceneGateResult PostBp(SmartBpRecognitionScene scene) =>
        new(scene, false, false, true,
            "character BP has ended; automatic recognition should stop after queued operations drain");
}
