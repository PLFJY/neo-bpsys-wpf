# Score System v2 设计文档

本文定义 Score System v2 的目标模型、计算规则、前台绑定方向和迁移计划。

当前实现状态：Score Phase 4 已完成全局比分窗口迁移。现有 `Core.Models.Game` 已持有 `MatchScoreState`，后台 `ScorePageViewModel` 的结果按钮通过 `IMatchScoreService.SetCurrentHalfResult(...)` / `ClearCurrentHalfResult()` 写入 `CurrentGame.MatchScore`，普通 UI 不再提供手动 Game/half 选择或“同步至前台”按钮；比分控制始终跟随全局 `CurrentGame.GameProgress`。`ScoreSurWindow` / `ScoreHunWindow` / `ScoreGlobalWindow` 的 v3 layout 已绑定 `CurrentGame.MatchScore` 派生字段，不再从 `Team.Score` 或 `FrontedWindowService` 动态控件读取比分。`GameGlobalInfoRecord` 仅作为从权威状态派生的调试/兼容视图。`.bpui` 尚未迁移，旧前台窗口仍可能读取 `Team.Score` 兼容镜像。

Score System v2 的核心目标是把权威比分状态放回现有 `Core.Models.Game`，让比分可以随对局导入、导出、回溯，并能在 `SharedDataService.NewGame()` 创建新对局时像 `MapV2Dictionary` 一样从上一局 `CurrentGame` 延续必要状态。

本文中的“小比分（MinorScore）”指每半场、每个比分系统 Game 或全场累计的分值。首次说明后，本文会使用“小比分”或 `MinorScore`。未来代码模型命名应优先使用 `MinorScore`，避免使用含糊的英文泛称。

## 1. 当前问题

当前比分相关状态分散在模型、页面 ViewModel、窗口 ViewModel 和前台窗口服务中，导致 v3 前台布局难以纯绑定渲染。

| 问题 | 当前位置 | 影响 |
| --- | --- | --- |
| `Team.Score` 同时承载大比分和当前小比分 | `Team.Score.Win`、`Tie`、`GameScores` | 队伍模型既像全场比分，又像当前半场/当前局内临时计分；语义混杂。 |
| 全局比分记录曾由页面 ViewModel 持有 | `ScorePageViewModel.GameGlobalInfoRecord` | Phase 2 后不再是权威数据，仅从 `CurrentGame.MatchScore` 派生供旧 UI/调试查看。 |
| 总小比分通过 messenger 推送 | `ScorePageViewModel.UpdateTotalGameScore()` -> `ScoreWindowViewModel.TotalMainGameScore` / `TotalAwayGameScore` | Phase 4 后 `ScoreGlobalWindow` 默认布局直接绑定 `MatchScoreState.HomeTotalMinorScore` / `AwayTotalMinorScore`；消息路径仅作旧 ViewModel 兼容。 |
| 全局比分 UI 曾由服务动态创建和直接修改 | `FrontedWindowService.GlobalScoreControlsReg()`、`SetGlobalScore()`、`SetGlobalScoreToBar()` | Phase 4 后默认全局比分窗口由 v3 `GlobalScoreRow` 控件从 `CurrentGame.MatchScore` 生成比分格，服务方法只保留兼容 no-op。 |
| 局内比分窗口已接入 v3 renderer 并完成 Phase 3 绑定迁移 | `ScoreSurWindow/BaseCanvas.json`、`ScoreHunWindow/BaseCanvas.json` | 当前默认布局绑定 `CurrentGame.MatchScore.*` 派生字段；用户旧布局如果仍保留 `CurrentGame.*Team.Score.*`，需要后续布局迁移或手动恢复默认。 |

## 2. 核心设计决策

权威比分状态必须由现有 `Core.Models.Game` 持有。

```text
ISharedDataService
  └─ CurrentGame
       ├─ SurTeam / HunTeam
       ├─ GameProgress
       ├─ MapV2Dictionary
       └─ MatchScoreState   <-- Score System v2 权威比分状态
```

这里需要区分两个“Game”：

| 名称 | 含义 |
| --- | --- |
| 现有 `Core.Models.Game` | 应用当前对局记录对象，保存队伍、BP 状态、地图、Ban、选手数据，并最终保存 `MatchScoreState`。 |
| Score System v2 的 Game | 比分系统领域术语，指 Game 1、Game 2、Game 3 Overtime 这样的计分单元，每个 Game 包含 First Half 和 Second Half。Identity V 赛事语境中常用 “Game <x> First Half / Second Half”。 |

文档中的 Game 是比分系统领域术语；实现时为避免与现有 `Core.Models.Game` 冲突，建议使用 `ScoreGame` 作为类型名。

不应把权威比分状态放在：

| 不应存放处 | 原因 |
| --- | --- |
| `Team` | 队伍信息应可跨对局复用；比分记录属于某场比赛/某个对局进程，不是队伍静态属性。 |
| `ScorePageViewModel` | 页面 ViewModel 是 UI 控制层，不能作为比分数据库。 |
| `ScoreWindowViewModel` | 前台窗口 ViewModel 应暴露绑定状态，不应独占计算结果。 |
| `FrontedWindowService` | 服务可以操作当前比分，但不应通过控件属性保存比分。 |
| 前台 UI 控件 | 控件只能显示状态，不能成为状态来源。 |

`Team.Score` 暂时不能立即删除。旧窗口、旧 XAML 和若干绑定仍依赖 `Team.Score.MajorPointsOnFront`、`Team.Score.GameScores`，迁移期可把它作为兼容镜像，但新模型不再以它作为权威写入点。

## 3. 新模型概念

### 3.1 MatchScoreState

`MatchScoreState` 由 `Game` 持有，表示整场比赛的比分状态。

| 责任 | 说明 |
| --- | --- |
| 权威存储 | 保存所有已记录半场结果、队伍/阵营映射和可序列化比分数据。 |
| 可序列化 | 作为 `Game` 的一部分导入导出，支持赛后回溯。 |
| 可延续 | 新建 `Game` 时可从上一局 `CurrentGame.MatchScore` clone 或 carry。 |
| 派生显示字段 | 暴露前台和后台可绑定字段，避免 UI 层重复计算。 |
| 重算入口 | 在半场结果、队伍换边、BO 模式或进度变化后统一重算派生值。 |

已实现结构：

```text
MatchScoreState
  ├─ Games: collection<ScoreGame>
  ├─ HomeMajorWin
  ├─ HomeMajorTie
  ├─ AwayMajorWin
  ├─ AwayMajorTie
  ├─ HomeMajorText
  ├─ AwayMajorText
  ├─ HomeTotalMinorScore
  ├─ AwayTotalMinorScore
  ├─ CurrentSurTeamPreHalfMinorScore
  ├─ CurrentHunTeamPreHalfMinorScore
  ├─ CurrentSurTeamMajorText
  └─ CurrentHunTeamMajorText
```

派生字段使用重算后写入的 observable 属性，并以 `[JsonIgnore]` 排除在持久化之外。`Games` 内的半场结果、记录时主客队映射和 `ScoreGameKey` 是持久化数据。

### 3.2 ScoreGame

`ScoreGame` 表示比分系统中的一个 Game，例如 Game 1 Normal、Game 3 Overtime。每个 `ScoreGame` 由一上一下两个半场组成。

| 字段 | 说明 |
| --- | --- |
| `GameNumber` | Game 编号，范围 `1..5`。 |
| `GameKind` | `Normal` 或 `Overtime`，实现类型为 `ScoreGameKind`。 |
| `FirstHalf` | 第一半。 |
| `SecondHalf` | 第二半。 |
| `HomeMinorScore` / `AwayMinorScore` | 两半都有结果时计算出的 Game-level MinorScore。 |
| `MajorResult` | 两半都有结果时计算出的 Game major result，实现类型为 `ScoreGameMajorResult`。 |

这个 `ScoreGame` 只有在 `FirstHalf.Result != null` 且 `SecondHalf.Result != null` 时才完整。未完整时，不参与大比分计算。

### 3.3 ScoreHalf

`ScoreHalf` 表示某个 `ScoreGame` 的一半。

| 字段 | 说明 |
| --- | --- |
| `GameProgress` | 对应当前半场进度。 |
| `Result` | 可空 `GameResult`；`null` 表示未记录或已清空。 |
| `SurTeamTypeWhenRecorded` | 记录时求生者侧对应主队还是客队，计算主客得分时使用。 |
| `HunTeamTypeWhenRecorded` | 记录时监管者侧对应主队还是客队，计算主客得分时使用。 |
| `SurMinorScore` / `HunMinorScore` | 从 `GameResult` 派生出的求生者/监管者小比分。 |
| `HomeMinorScore` / `AwayMinorScore` | 根据记录时求生/监管侧与主客队映射派生出的主客小比分。 |

半场必须保存“记录时”的队伍/阵营映射，不能只在显示时读取当前 `SurTeam` / `HunTeam`。原因是比分记录需要可回溯；后续换边或导入队伍信息不应改变过去半场的历史得分归属。

### 3.4 ScoreGameKey

`ScoreGameKey` 用于稳定定位一个 `ScoreGame`。

| 字段 | 说明 |
| --- | --- |
| `GameNumber` | `1..5`。 |
| `GameKind` | `Normal` / `Overtime`，实现类型为 `ScoreGameKind`。 |

示例：

```text
ScoreGame 3 Normal   -> { GameNumber: 3, GameKind: Normal }
ScoreGame 3 Overtime -> { GameNumber: 3, GameKind: Overtime }
ScoreGame 5 Overtime -> { GameNumber: 5, GameKind: Overtime }
```

### 3.5 IMatchScoreService / MatchScoreService

`IMatchScoreService` 是操作层，所有命令都作用于 `ISharedDataService.CurrentGame.MatchScore`。服务不拥有权威状态。

| 命令 | 行为 |
| --- | --- |
| `CurrentHalf` / `GetHalf(...)` | 根据 `CurrentGame.GameProgress` 定位当前 `ScoreHalf`。 |
| `SetCurrentHalfResult(GameResult result)` | 写入当前半场结果，并记录当时主客队阵营映射。 |
| `ClearCurrentHalfResult()` | 把当前半场结果设为 `null`。 |
| `Recalculate()` | 从所有 `ScoreGame` 重新计算大比分、总小比分和前台派生字段。 |
| `RefreshCurrentProgress()` | 结合当前 `GameProgress`、当前求生/监管队伍和 BO3/BO5 状态刷新局内显示字段。 |
| `SyncLegacyTeamScoreMirror()` | 迁移期把当前派生比分写回 `Team.Score`，仅用于旧窗口兼容。 |

```text
ScorePageViewModel
  └─ IMatchScoreService.SetCurrentHalfResult(...)
       └─ ISharedDataService.CurrentGame.MatchScore
            └─ Recalculate()
                 ├─ 通知后台页面刷新
                 ├─ 通知 ScoreSurWindow / ScoreHunWindow 绑定刷新
                 └─ 通知 ScoreGlobalWindow 绑定刷新
```

## 4. GameProgress 映射

`GameProgress.Free` 不对应任何确定半场，Score System v2 暂不解析它；这是已知设计缺口。

| `GameProgress` | Score System v2 mapping |
| --- | --- |
| `Game1FirstHalf` / `Game1SecondHalf` | ScoreGame 1 Normal |
| `Game2FirstHalf` / `Game2SecondHalf` | ScoreGame 2 Normal |
| `Game3FirstHalf` / `Game3SecondHalf` | ScoreGame 3 Normal |
| `Game3OvertimeFirstHalf` / `Game3OvertimeSecondHalf` | ScoreGame 3 Overtime |
| `Game4FirstHalf` / `Game4SecondHalf` | ScoreGame 4 Normal |
| `Game5FirstHalf` / `Game5SecondHalf` | ScoreGame 5 Normal |
| `Game5OvertimeFirstHalf` / `Game5OvertimeSecondHalf` | ScoreGame 5 Overtime |
| `Free` | Unresolved / not designed yet |

映射必须显式维护。当前 `GameProgress` enum 中 `Game4FirstHalf` 与 `Game3OvertimeFirstHalf` 共用数值 `6`，`Game4SecondHalf` 与 `Game3OvertimeSecondHalf` 共用数值 `7`。实现中 `MatchScoreState.GetGame(progress)` 在缺少上下文时保守按 BO5 第四局解析，`MatchScoreService` 会结合 `ISharedDataService.IsBo3Mode` 调用带上下文的解析来区分“Game 3 Overtime”和“Game 4 Normal”。

## 5. 计分规则

以下计分规则基于当前项目实现，不声明为官方赛事规则。

| `GameResult` | 求生者小比分 | 监管者小比分 |
| --- | ---: | ---: |
| `Escape4` | 5 | 0 |
| `Escape3` | 3 | 1 |
| `Tie` | 2 | 2 |
| `Out3` | 1 | 3 |
| `Out4` | 0 | 5 |

### 5.1 Null 结果行为

`ScoreHalf.Result == null` 表示未记录或已清空。

| 场景 | 行为 |
| --- | --- |
| 小比分合计 | 不参与小比分合计。 |
| 大比分计算 | 不参与大比分计算。 |
| 全局比分格 | 显示 `-`。 |
| 全局比分格阵营图标 | 隐藏。 |
| 局内第一半预分 | 显示 `0 / 0`。 |

空结果不等价于平局，也不等价于 0:0 已记录结果。

### 5.2 半场派生

```text
ScoreHalf.Result
  -> SurMinorScore / HunMinorScore
  -> 根据 SurTeamTypeWhenRecorded / HunTeamTypeWhenRecorded
  -> HomeMinorScore / AwayMinorScore
```

如果记录时主队是求生者：

```text
HomeMinorScore = SurMinorScore
AwayMinorScore = HunMinorScore
```

如果记录时主队是监管者：

```text
HomeMinorScore = HunMinorScore
AwayMinorScore = SurMinorScore
```

### 5.3 ScoreGame 派生

`ScoreGame` 只有两半都非空时才计算 Home/Away MinorScore 和 Game major result：

```text
HomeMinorScore = FirstHalf.HomeMinorScore + SecondHalf.HomeMinorScore
AwayMinorScore = FirstHalf.AwayMinorScore + SecondHalf.AwayMinorScore

if HomeMinorScore > AwayMinorScore:
    HomeMajorWin += 1
else if AwayMinorScore > HomeMinorScore:
    AwayMajorWin += 1
else:
    HomeMajorTie += 1
    AwayMajorTie += 1
```

如果任一半为 `null`，该 `ScoreGame` 不完整：

| 派生项 | 行为 |
| --- | --- |
| `HomeMinorScore` / `AwayMinorScore` | 不作为完整 ScoreGame 的小比分输出。 |
| 大比分胜/平 | 不计算。 |
| 全场总小比分 | 只累加已记录半场的小比分；空半场不累加。 |

### 5.4 全场派生

`MatchScoreState` 从所有 `ScoreHalf` / `ScoreGame` 派生以下字段：

| 字段 | 说明 |
| --- | --- |
| `HomeMajorWin` / `HomeMajorTie` | 主队大比分胜/平。 |
| `AwayMajorWin` / `AwayMajorTie` | 客队大比分胜/平。 |
| `HomeMajorText` / `AwayMajorText` | 前台大比分文本，建议保持当前 `W{Win}  D{Tie}` 风格。 |
| `HomeTotalMinorScore` / `AwayTotalMinorScore` | 所有已记录半场的主客小比分合计。 |
| `CurrentSurTeamPreHalfMinorScore` | 当前求生者队伍在当前半场窗口中应显示的上一半小比分。 |
| `CurrentHunTeamPreHalfMinorScore` | 当前监管者队伍在当前半场窗口中应显示的上一半小比分。 |
| `CurrentSurTeamMajorText` | 当前求生者队伍对应的大比分文本。 |
| `CurrentHunTeamMajorText` | 当前监管者队伍对应的大比分文本。 |

## 6. ScoreSurWindow / ScoreHunWindow 行为

`ScoreSurWindow` 和 `ScoreHunWindow` 已迁移为 v3 renderer pilot。Phase 3 后，内置默认 layout 已从旧的 `Team.Score` 兼容镜像切换到 `CurrentGame.MatchScore` 派生字段：

| 窗口 | 旧绑定 | Phase 3 默认绑定 |
| --- | --- | --- |
| `ScoreSurWindow` | `CurrentGame.SurTeam.Score.MajorPointsOnFront`、`CurrentGame.SurTeam.Score.GameScores` | `CurrentGame.MatchScore.CurrentSurTeamMajorText`、`CurrentGame.MatchScore.CurrentSurTeamPreHalfMinorScoreText` |
| `ScoreHunWindow` | `CurrentGame.HunTeam.Score.MajorPointsOnFront`、`CurrentGame.HunTeam.Score.GameScores` | `CurrentGame.MatchScore.CurrentHunTeamMajorText`、`CurrentGame.MatchScore.CurrentHunTeamPreHalfMinorScoreText` |

局内比分窗口显示规则：

| 当前半场 | 显示小比分 |
| --- | --- |
| 第一半 | 求生者/监管者都显示 `0 / 0` 对应的本侧值。 |
| 第二半 | 显示同一 `ScoreGame` 第一半已经记录的 MinorScore。 |

第二半显示值必须映射到“当前求生者队伍/当前监管者队伍”，不能盲目复制“第一半求生者小比分/第一半监管者小比分”。如果第一半主队是求生者、第二半换边后主队变成监管者，第二半的求生者窗口应显示当前求生者队伍在第一半对应的历史得分。

## 7. 后台 ScorePage 行为

后台 ScorePage 的编辑对象由 `CurrentGame.GameProgress` 决定。页面按钮只表达“给当前半场写入一个结果”，不再直接累加 `Team.Score.GameScores`。

| 按钮 | 新行为 |
| --- | --- |
| `Escape4` | `SetCurrentHalfResult(GameResult.Escape4)` |
| `Escape3` | `SetCurrentHalfResult(GameResult.Escape3)` |
| `Tie` | `SetCurrentHalfResult(GameResult.Tie)` |
| `Out3` | `SetCurrentHalfResult(GameResult.Out3)` |
| `Out4` | `SetCurrentHalfResult(GameResult.Out4)` |
| `Clear` | `ClearCurrentHalfResult()`，即把当前半场结果设为 `null`。 |

新模型不需要 `IsGameFinished` 作为核心概念。是否完成由 `GameResult != null` 表达。迁移期 UI 可以保留兼容字段或旧交互，但它们应映射到 nullable result，而不是成为新的模型字段。

当前后台实现不再暴露普通 UI 的 `IsGameFinished`、手动 Game/half 选择或“同步至前台”流程。清除当前半场比分按钮位于旧“小比分清零”按钮位置，会把全局 `CurrentGame.GameProgress` 对应半场的 `ScoreHalf.Result` 设为 `null`，并同步刷新旧 `Team.Score` 镜像；`ScoreGlobalWindow` 由 v3 绑定自动刷新。

## 8. ScoreGlobalWindow 行为

`ScoreGlobalWindow` 已迁移到 v3 renderer，默认布局位于 `Resources/FrontedLayouts/ScoreGlobalWindow/BaseCanvas.json`。它绑定现有 `Core.Models.Game` 持有的 `MatchScoreState`，不依赖 `ScoreWindowViewModel` 独有字段或 `FrontedWindowService` 直接变更控件。

目标状态流：

```text
ScorePage button
  -> IMatchScoreService
  -> CurrentGame.MatchScore
  -> ScoreGlobalWindow binding
```

全局比分格规则：

| 状态 | 文本 | 阵营图标 |
| --- | --- | --- |
| `GameResult == null` | `-` | 隐藏 |
| 有结果且该队当半为求生者 | 对应小比分 | 显示求生者图标 |
| 有结果且该队当半为监管者 | 对应小比分 | 显示监管者图标 |

全局比分格表示 `ScoreGame` 内部的 `ScoreHalf` 结果，由内置 v3 控件 `GlobalScoreRow` 生成。总分显示从 `MatchScoreState` 派生，不再从 `ScoreWindowViewModel` 独有字段或 `FrontedWindowService` UI mutation 派生。BO3/BO5 可见性从 `ISharedDataService.IsBo3Mode` 和显式 `ScoreGameKey` 规则派生，避免依赖 `GameProgress` 原始数值。当前实现不做完整条件布局引擎：BO3 下比分格会隐藏 BO5 后续格，但总分位置保持默认 BO5 布局位置，背景仍使用 v3 layout 的 `BackgroundImage`。

## 9. 导入、导出与新建对局

`MatchScoreState` 是现有 `Core.Models.Game` 的一部分，因此：

| 场景 | 行为 |
| --- | --- |
| 导出对局 | 序列化 `Game.MatchScore`。 |
| 导入对局 | 从 JSON 恢复 `Game.MatchScore`，并在队伍信息导入后保持历史半场的阵营映射。 |
| 新建对局 | 从旧 `CurrentGame.MatchScore` clone/carry 到新 `Game`。 |
| 回溯对局 | 不依赖页面 ViewModel 是否还存在，不依赖前台窗口是否打开。 |

新建对局的 carry 行为应和当前地图状态类似：`SharedDataService.NewGame()` 先读取旧 `CurrentGame` 的可延续状态，再构造新的 `Core.Models.Game`。实现时需要明确哪些比分字段可延续；建议延续整场 `MatchScoreState`，并由当前 `GameProgress` 决定后续编辑位置。

## 10. 兼容策略

| 旧能力 | 迁移期策略 |
| --- | --- |
| `Team.Score.GameScores` | 仅作为仍未迁移旧窗口的 transitional compatibility mirror，`MatchScoreService.SyncLegacyTeamScoreMirror()` 由 `MatchScoreState` 派生写回；后台 `ScorePageViewModel` 和 `ScoreSurWindow` / `ScoreHunWindow` 不再把它作为权威写入点。 |
| `Team.Score.MajorPointsOnFront` | 仅作为仍未迁移旧窗口的 transitional compatibility mirror；局内比分 v3 默认布局已改为读取 `MatchScoreState` 派生字段。 |
| `ScorePageViewModel.GameGlobalInfoRecord` | Phase 2 后不再作为权威数据；Phase 2/3 可作为 UI 临时缓存。 |
| `ScoreWindowViewModel.TotalMainGameScore` / `TotalAwayGameScore` | Phase 4 后默认全局比分布局不再读取这些字段；它们仅作为旧消息路径兼容字段。 |
| `FrontedWindowService.SetGlobalScore*` | Phase 4 后不再作为比分状态入口，当前为兼容 no-op；全局比分窗口状态由 `CurrentGame.MatchScore` 驱动。 |

## 11. 分阶段迁移计划

| 阶段 | 范围 | 明确不做 |
| --- | --- | --- |
| Score Phase 0 | 已完成：新增本文档，更新文档索引和相关提醒。 | 不改运行时代码、XAML、模型或 ViewModel。 |
| Score Phase 1 | 已完成基础层：增加 `MatchScoreState`、`ScoreGame`、`ScoreHalf` 和 `IMatchScoreService` / `MatchScoreService`，权威状态由现有 `Core.Models.Game` 持有。 | 不迁移 UI。 |
| Score Phase 2 | 已完成：将 `ScorePageViewModel` 的结果写入、当前半场清除、总小比分消息和旧 `Team.Score` 镜像刷新迁移到 service / `MatchScoreState`，并移除普通 UI 的手动 Game/half 选择和同步按钮。 | 未迁移全局比分窗口，仍保留 `FrontedWindowService.SetGlobalScore*` 兼容刷新。 |
| Score Phase 3 | 已完成：`ScoreSurWindow` / `ScoreHunWindow` v3 layout 绑定到 `MatchScoreState` 派生字段，局内第一半预分显示 `0`，第二半按当前求生/监管队伍映射显示同一 `ScoreGame` 第一半 MinorScore；若第一半结果为 `null`，预分显示 `0`。 | 不改 `ScoreGlobalWindow`；不迁移 `.bpui`；不删除 `Team.Score`。 |
| Score Phase 4 | 已完成：迁移 `ScoreGlobalWindow` 到 v3 renderer，增加 `GlobalScoreRow` 控件，由 `CurrentGame.MatchScore` 生成全局比分格。 | 未实现完整条件布局引擎；BO3 总分位置和背景仍采用固定 v3 layout。 |
| Score Phase 5 | 移除或废弃旧 `Team.Score` 的比分写入职责。 | 不破坏仍未迁移窗口的兼容显示。 |

## 12. 重要警告

1. 不要把权威比分状态存到 UI 控件里。
2. 不要继续让 `ScorePageViewModel` 作为比分数据库。
3. 不要把新的 v3 前台控件绑定到 `ScoreWindowViewModel` 独有字段。
4. 不要立即删除 `Team.Score`；迁移期它可能仍是旧窗口兼容镜像。
5. `GameProgress.Free` 的比分行为尚未解决，不能假装已支持。
6. `Game3Overtime*` 与 `Game4*` 当前 enum 数值重叠，映射实现必须显式处理上下文。

## 13. 待确认问题

| 问题 | 当前建议 |
| --- | --- |
| `Free` 模式是否允许手动写比分 | 暂不支持，记录为设计缺口。暂时在Free下禁用相关按钮，且对外显示全部为 0 |
| BO3 中第三场加赛与 BO5 第四场的持久化 key | 使用 `ScoreGameKey`，避免只靠 `GameProgress` 数值。 |
| 旧 `Team.Score` 镜像何时删除 | 等 `BpWindow`、`CutSceneWindow`、`GameDataWindow`、`WidgetsWindow` 等旧绑定迁移后再删除。 |
| 全局比分 v3 控件类型 | 已新增内置 `GlobalScoreRow`，通过 `IFrontedControl` 注册并由 JSON `ControlType = "GlobalScoreRow"` 使用。 |
