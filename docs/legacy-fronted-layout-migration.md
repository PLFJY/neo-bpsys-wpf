# 旧前台布局迁移

Designer v3 / `FrontedLayouts` 是当前前台窗口自定义的唯一运行时来源。旧 `Config.json` 中的前台窗口字段已经从 active `Settings.cs` 移除，只保留为 legacy DTO，用于启动迁移和旧 `.bpui` 转换。

## 迁移入口

启动加载 `%APPDATA%\neo-bpsys-wpf\Config.json` 时，如果 `Version` 缺失或为 `null`，会先备份原文件，再读取旧前台字段：

| 旧字段 | v3 目标 |
| --- | --- |
| `BpWindowSettings.BgImageUri` | `FrontedLayouts/BpWindow/BaseCanvas.json` 的 `BackgroundImage` |
| `CutSceneWindowSettings.BgUri` | `FrontedLayouts/CutSceneWindow/BaseCanvas.json` 的 `BackgroundImage` |
| `ScoreWindowSettings.SurScoreBgImageUri` | `FrontedLayouts/ScoreSurWindow/BaseCanvas.json` 的 `BackgroundImage` |
| `ScoreWindowSettings.HunScoreBgImageUri` | `FrontedLayouts/ScoreHunWindow/BaseCanvas.json` 的 `BackgroundImage` |
| `ScoreWindowSettings.GlobalScoreBgImageUri` | `FrontedLayouts/ScoreGlobalWindow/BaseCanvas.json` 的 `BackgroundImage` |
| `GameDataWindowSettings.BgImageUri` | `FrontedLayouts/GameDataWindow/BaseCanvas.json` 的 `BackgroundImage` |
| `WidgetsWindowSettings.MapBpBgUri` | `FrontedLayouts/WidgetsWindow/MapBpCanvas.json` 的 `BackgroundImage` |
| `WidgetsWindowSettings.BpOverviewBgUri` | `FrontedLayouts/WidgetsWindow/BpOverViewCanvas.json` 的 `BackgroundImage` |
| `WidgetsWindowSettings.MapBpV2BgUri` | `FrontedLayouts/WidgetsWindow/MapV2Canvas.json` 的 `BackgroundImage` |
| `AllowsWindowTransparency` / `AllowsScoreGlobalWindowTransparency` | `FrontedLayouts/{WindowTypeName}/window.json` |

无法明确迁移的旧行为字段会写入日志 warning，不会静默伪造 v3 行为。迁移后的 active `Config.json` 只保留应用级设置，不再写出旧前台窗口字段。

## 旧 `.bpui`

旧 `.bpui` 不带 v3 `manifest.json`，通常包含 `Config.json`、`CustomUi/` 或 `FrontElementsConfig/`。导入时应走 legacy 转换器：转换器从当前内置 v3 layout 起步，复制安全资源，应用可映射的背景图和旧 `ElementInfo` 几何，再生成干净的 v3 `.bpui` 包。

旧几何迁移按“精确控件名、窗口限定别名、聚合控件、已知旧 overlay 消费、规范化匹配、技术候选诊断”顺序处理。`ScoreGlobalWindow/BaseCanvas` 已显式兼容旧全局比分命名：

| 旧控件名 | v3 控件名 |
| --- | --- |
| `MainTeamName` | `HomeTeamName` |
| `MainScoreTotal` | `HomeScoreTotal` |
| `AwayTeamName` | `AwayTeamName` |
| `AwayScoreTotal` | `AwayScoreTotal` |

`ScoreGlobalWindow` 下还保留一个限定规则：旧名以 `Main` 开头时可映射为 v3 的 `Home` 前缀。旧版 `HomeTeamGame*FirstHalf` / `HomeTeamGame*SecondHalf`、`AwayTeamGame*FirstHalf` / `AwayTeamGame*SecondHalf` 以及 `Game*Overtime*Half` 不再逐个迁移为独立控件，而是聚合或消费到 `HomeGlobalScoreRow` / `AwayGlobalScoreRow`，并从旧普通半场格子推导行位置、`HalfGameGap` 和 `MajorGameGap`。间距不规则、overtime 单元被消费等细节只记录为内部诊断，不再为每个旧半场格子报 unmatched。

`WidgetsWindow/BpOverViewCanvas` 等旧 `HunBanCurrentLock*` / `SurBanCurrentLock*` 锁定遮罩几何会合并到对应 v3 `HunBanCurrent*` / `SurBanCurrent*`。如果目标本体几何也存在，以目标本体为准；如果只有锁遮罩几何，则用它作为 fallback。旧 Config 中可解析到 `CustomUi/` 的 Ban 锁图和 BpWindow picking border 图片/颜色会写入 v3 控件配置。

转换结果中的 `Infos` 用于记录成功复制资源、正常聚合等信息，`Diagnostics` 用于记录技术细节和近似处理；`Warnings`、`UnsupportedProperties` 和 `MissingResources` 才表示需要用户留意的问题。UI 不应把纯 `Infos` / `Diagnostics` 当作警告弹出，用户弹窗也不应展示 `Closest candidates` 等原始技术诊断。

manifest-based v3 `.bpui` 仍禁止包含 `Config.json`、`CustomUi/` 或 `FrontElementsConfig/`。
