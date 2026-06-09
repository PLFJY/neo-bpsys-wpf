# 旧前台布局迁移

Designer v3 / `FrontedLayouts` 是当前前台窗口自定义的唯一运行时来源。旧 `Config.json` 中的前台窗口字段已经从 active `Settings.cs` 移除，只保留为 legacy DTO，用于启动迁移和旧 `.bpui` 转换。

## 迁移入口

启动加载 `%APPDATA%\neo-bpsys-wpf\Config.json` 时，如果 `Version` 缺失或为 `null`，会先备份原文件，再读取旧前台字段：

| 旧字段 | v3 目标 |
| --- | --- |
| `BpWindowSettings.BgImageUri` | `FrontedLayouts/BpWindow.json` 的 `CanvasSettings.BackgroundImage` |
| `CutSceneWindowSettings.BgUri` | `FrontedLayouts/CutSceneWindow.json` 的 `CanvasSettings.BackgroundImage` |
| `ScoreWindowSettings.SurScoreBgImageUri` | `FrontedLayouts/ScoreSurWindow.json` 的 `CanvasSettings.BackgroundImage` |
| `ScoreWindowSettings.HunScoreBgImageUri` | `FrontedLayouts/ScoreHunWindow.json` 的 `CanvasSettings.BackgroundImage` |
| `ScoreWindowSettings.GlobalScoreBgImageUri` | `FrontedLayouts/ScoreGlobalWindow.json` 的 `CanvasSettings.BackgroundImage` |
| `GameDataWindowSettings.BgImageUri` | `FrontedLayouts/GameDataWindow.json` 的 `CanvasSettings.BackgroundImage` |
| `WidgetsWindowSettings.MapBpBgUri` | MapV1 已删除，跳过并记录 Info |
| `WidgetsWindowSettings.BpOverviewBgUri` | `FrontedLayouts/BpOverviewWindow.json` 的 `CanvasSettings.BackgroundImage` |
| `WidgetsWindowSettings.MapBpV2BgUri` | `FrontedLayouts/MapV2Window.json` 的 `CanvasSettings.BackgroundImage` |
| `AllowsWindowTransparency` / `AllowsScoreGlobalWindowTransparency` | `FrontedWindowConfig.WindowSettings.AllowsTransparency` |

无法明确迁移的旧行为字段会写入日志 warning，不会静默伪造 v3 行为。迁移后的 active `Config.json` 只保留应用级设置，不再写出旧前台窗口字段。

## 旧 `.bpui`

旧 `.bpui` 不带 v3 `manifest.json`，通常包含 `Config.json`、`CustomUi/` 或 `FrontElementsConfig/`。导入时应走 legacy 转换器：转换器从当前内置 v3 layout 起步，复制安全资源，应用可映射的背景图、旧 `TextSettings` 文本样式和旧 `ElementInfo` 几何，再生成干净的 v3 `.bpui` 包。

旧几何迁移按“精确控件名、窗口限定别名、聚合控件、已知旧 overlay 消费、规范化匹配、技术候选诊断”顺序处理。`ScoreGlobalWindow/BaseCanvas` 已显式兼容旧全局比分命名：

| 旧控件名 | v3 控件名 |
| --- | --- |
| `MainTeamName` | `HomeTeamName` |
| `MainScoreTotal` | `HomeScoreTotal` |
| `AwayTeamName` | `AwayTeamName` |
| `AwayScoreTotal` | `AwayScoreTotal` |

`ScoreGlobalWindow` 下还保留一个限定规则：旧名以 `Main` 开头时可映射为 v3 的 `Home` 前缀。旧版 `HomeTeamGame*FirstHalf` / `HomeTeamGame*SecondHalf`、`AwayTeamGame*FirstHalf` / `AwayTeamGame*SecondHalf` 以及 `Game*Overtime*Half` 不再逐个迁移为顶层控件，而是聚合到 `HomeGlobalScoreRow` / `AwayGlobalScoreRow` 的 `Cells` 子格中，并把旧绝对坐标换算为相对父行的 `X/Y/Width/Height`。间距不规则、overtime 单元迁入子格等细节只记录为内部诊断，不再为每个旧半场格子报 unmatched。

旧 `WidgetsWindow/BpOverViewCanvas` 会转换为 `BpOverviewWindow`，旧 `HunBanCurrentLock*` / `SurBanCurrentLock*` 锁定遮罩几何会合并到对应 v3 `HunBanCurrent*` / `SurBanCurrent*`。如果目标本体几何也存在，以目标本体为准；如果只有锁遮罩几何，则用它作为 fallback。旧 Config 中可解析到 `CustomUi/` 的 Ban 锁图和 BpWindow picking border 图片/颜色会写入 v3 控件配置。

转换结果中的 `Infos` 用于记录成功复制资源、正常聚合等信息，`Diagnostics` 用于记录技术细节和近似处理；`Warnings`、`UnsupportedProperties` 和 `MissingResources` 才表示需要用户留意的问题。UI 不应把纯 `Infos` / `Diagnostics` 当作警告弹出，用户弹窗也不应展示 `Closest candidates` 等原始技术诊断。

旧 `TextSettings` 迁移由 Core 中的共享样式迁移器处理，本地 `Config.json` 启动迁移和旧 `.bpui` 包转换使用同一套映射规则。转换器会把旧字体写法如 `./#汉仪第五人格体简`、`./#华康POP1体W5` 归一化为 `pack://application:,,,/Assets/Fonts/#...`，普通系统字体名保留；样式应用记录只进入 `Diagnostics`，不作为用户警告弹窗。

旧 `WidgetsWindow/MapV2Canvas` 会转换为 `MapV2Window`。`MapV2Display` 已提供地图名、队名、阵营文字三组字体/颜色/字号配置，以及 `PickingBorderImagePath`、`PickingBorderFillColor`。旧 `MapBpV2_*` 文本样式和 picking border 图片/颜色可以写入这些 v3 字段，不再需要提示“MapV2Display 无 v3 image/color config”。

manifest-based v3 `.bpui` 仍禁止包含 `Config.json`、`CustomUi/` 或 `FrontElementsConfig/`。
