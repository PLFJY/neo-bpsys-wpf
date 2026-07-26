# Score System v2 设计文档

本文定义 Score System v2 的目标模型、计算规则、前台绑定方向和迁移计划。

当前实现状态：旧比分写入路径清理已完成。现有 `Core.Models.Game` 已持有 `MatchScoreState`，后台 `ScorePageViewModel` 的结果按钮通过 `IMatchScoreService.SetCurrentHalfResult(...)` / `ClearCurrentHalfResult()` 写入 `CurrentGame.MatchScore`，普通 UI 不再提供手动 Game/half 选择、手动 `Team.Score` 累加或"同步至前台"按钮；比分控制始终跟随全局 `CurrentGame.GameProgress`。后台 `ScorePage` 现在提供导播用只读比分预览表，表格行由 `CurrentGame.MatchScore.Games` 派生，按 BO3/BO5 显示对应 ScoreGame 和半场，并用状态列标记当前半场、已录入和未录入。`ScoreSurWindow` / `ScoreHunWindow` / `ScoreGlobalWindow` / `CutSceneWindow` / `GameDataWindow` / `BpOverviewWindow` / `BpWindow` 的 v3 layout 已绑定 `CurrentGame.MatchScore` 派生字段，不再从 `Team.Score`、`ScoreWindowViewModel` 总分字段或 `FrontedWindowService` 动态控件读取比分。`GameGlobalInfoRecord`、`ScoreWindowViewModel.TotalMainGameScore` / `TotalAwayGameScore` 和 `FrontedWindowService.SetGlobalScore*` / `ResetGlobalScore` 已移除。`.bpui` 已迁移到 Window-centric v3 package。

Score System v2 的核心目标是把权威比分状态放回现有 `Core.Models.Game`，让比分可以随对局导入、导出、回溯，并能在 `SharedDataService.NewGame()` 创建新对局时像 `MapV2Dictionary` 一样从上一局 `CurrentGame` 延续必要状态。

本文中的“小比分（MinorScore）”指每半场、每个比分系统 Game 或全场累计的分值。首次说明后，本文会使用“小比分”或 `MinorScore`。未来代码模型命名应优先使用 `MinorScore`，避免使用含糊的英文泛称。

## 1. 当前问题

当前比分相关状态分散在模型、页面 ViewModel、窗口 ViewModel 和前台窗口服务中，导致 v3 前台布局难以纯绑定渲染。

| 问题 | 当前位置 | 影响 |
| --- | --- | --- |
| `Team.Score` 同时承载大比分和当前小比分 | `Team.Score.Win`、`Tie`、`GameScores` | 队伍模型既像全场比分，又像当前半场/当前局内临时计分；语义混杂。 |
| 全局比分记录曾由页面 ViewModel 持有 | `ScorePageViewModel.GameGlobalInfoRecord` | 已移除；后台页面不再维护第二份半场完成状态。 |
| 总小比分曾通过 messenger 推送 | `ScorePageViewModel.UpdateTotalGameScore()` -> `ScoreWindowViewModel.TotalMainGameScore` / `TotalAwayGameScore` | 已移除；`ScoreGlobalWindow` 默认布局直接绑定 `MatchScoreState.HomeTotalMinorScore` / `AwayTotalMinorScore`。 |
| 全局比分 UI 曾由服务动态创建和直接修改 | `FrontedWindowService.GlobalScoreControlsReg()`、`SetGlobalScore()`、`SetGlobalScoreToBar()` | 当前全局比分窗口由 v3 `GlobalScoreRow.Cells` 配置比分格，再从 `CurrentGame.MatchScore` 填充文本和阵营图标；`SetGlobalScore*` / `ResetGlobalScore` 已从代码库中完全移除。 |
| 局内比分窗口已接入 v3 renderer 并完成绑定迁移 | `ScoreSurWindow/BaseCanvas.json`、`ScoreHunWindow/BaseCanvas.json` | 当前默认布局绑定 `CurrentGame.MatchScore.*` 派生字段；用户旧布局如果仍保留 `CurrentGame.*Team.Score.*`，需要后续布局迁移或手动恢复默认。 |

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
| Score System v2 的 Game | 比分系统领域术语，指 Game 1、Game 2、Game 3 Overtime 这样的计分单元，每个 Game 包含 First Half 和 Second Half。Identity V 赛事语境中常用 “Game {x} First Half / Second Half”。 |

文档中的 Game 是比分系统领域术语；实现时为避免与现有 `Core.Models.Game` 冲突，建议使用 `ScoreGame` 作为类型名。

不应把权威比分状态放在：

| 不应存放处 | 原因 |
| --- | --- |
| `Team` | 队伍信息应可跨对局复用；比分记录属于某场比赛/某个对局进程，不是队伍静态属性。 |
| `ScorePageViewModel` | 页面 ViewModel 是 UI 控制层，不能作为比分数据库。 |
| `ScoreWindowViewModel` | 前台窗口 ViewModel 应暴露绑定状态，不应独占计算结果。 |
| `FrontedWindowService` | 服务可以操作当前比分，但不应通过控件属性保存比分。 |
| 前台 UI 控件 | 控件只能显示状态，不能成为状态来源。 |

`Team.Score` 暂时不能从模型层立即删除，因为旧 JSON 和旧 DTO 仍可能包含 `Team.Score.Win`、`Team.Score.Tie`、`Team.Score.GameScores`。它不再是运行时兼容镜像，`MatchScoreState` 不会把派生比分写回 `Team.Score`。新模型不以它作为权威写入点，后台 `ScorePage` 也不提供手动 `Team.Score` 累加入口；`CutSceneWindow` 和 `GameDataWindow` v3 默认布局已改为读取 `CurrentGame.MatchScore.CurrentSurTeamMajorText` / `CurrentGame.MatchScore.CurrentHunTeamMajorText`。

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
  ├─ CurrentSurTeamMinorScoreText
  ├─ CurrentHunTeamMinorScoreText
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

### 3.4.1 GlobalScoreRow cell 布局

`ScoreGlobalWindow` 的 v3 `GlobalScoreRow` 不再根据 `MajorGameGap` / `HalfGameGap` 自动展开比分格。布局 JSON 中的父级 `GlobalScoreRowControlConfig` 保存队伍方向和默认样式，`Cells` 中的每个 `GlobalScoreCellConfig` 通过 `GameNumber`、`GameKind`、`HalfKind` 指向一个 `ScoreHalf`，并用相对父行的 `X/Y/Width/Height` 定位。运行时只用这些 key 从 `CurrentGame.MatchScore.Games` 解析显示值；缺失半场或 `Result == null` 时显示 `-`。BO3 与 BO5 的差异由通用 Canvas state 保存，因此 BO3 state 可以有独立的 cell 列表和相对坐标。

### 3.5 IMatchScoreService / MatchScoreService

`IMatchScoreService` 是操作层，所有命令都作用于 `ISharedDataService.CurrentGame.MatchScore`。服务不拥有权威状态。

| 命令 | 行为 |
| --- | --- |
| `CurrentHalf` / `GetHalf(...)` | 根据 `CurrentGame.GameProgress` 定位当前 `ScoreHalf`。 |
| `SetCurrentHalfResult(GameResult result)` | 写入当前半场结果，并记录当时主客队阵营映射。 |
| `ClearCurrentHalfResult()` | 把当前半场结果设为 `null`。 |
| `Recalculate()` | 从当前 BO 模式可见的 `ScoreGame` 重新计算大比分、总小比分和前台派生字段。内部调用 `MatchScoreState.Recalculate(bool isBo3Mode)`，统计范围由 BO3/BO5 可见性决定，不依赖当前进度。 |
| `RefreshCurrentProgress()` | 结合当前 `GameProgress`、当前求生/监管队伍和 BO3/BO5 状态刷新局内显示字段。 |

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
| 局内小比分 | 当前半场所在 Game 的所有已记录半场（含当前半场）小比分累计；当前半场无结果时仅累计之前已记录的半场；当前半场为第一半且未记录时显示 `0`。 |

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
    majorResult = HomeWin
else if AwayMinorScore > HomeMinorScore:
    majorResult = AwayWin
else:
    majorResult = Tie
```

如果任一半为 `null`，该 `ScoreGame` 不完整：

| 派生项 | 行为 |
| --- | --- |
| `HomeMinorScore` / `AwayMinorScore` | 不作为完整 ScoreGame 的小比分输出。 |
| 大比分胜/平 | 不计算。 |
| 全场总小比分 | 只累加已记录半场的小比分；空半场不累加。 |

#### 大比分统计范围

`MatchScoreState.Recalculate(bool isBo3Mode)` 统计当前 BO 模式下可见的 `ScoreGame`，不依赖当前 `GameProgress`，也不使用 `Games` 的索引位置或 `GameNumber` 当作遍历边界。

```text
foreach game in Games where ScoreGameVisibility.IsVisibleInBoMode(game.Key, isBo3Mode):
    count MajorResult from game
    count recorded half minor scores from game
```

BO3 可见范围是 Game 1、Game 2、Game 3、Game 3 Overtime。BO5 可见范围是 Game 1、Game 2、Game 3、Game 4、Game 5、Game 5 Overtime。即使当前进度是 `GameProgress.Free`，重算也会按传入 BO 模式重新派生总分；没有可见已记录结果时，总分会归零。

### 5.4 全场派生

`MatchScoreState` 从所有 `ScoreHalf` / `ScoreGame` 派生以下字段：

| 字段 | 说明 |
| --- | --- |
| `HomeMajorWin` / `HomeMajorTie` | 主队大比分胜/平。 |
| `AwayMajorWin` / `AwayMajorTie` | 客队大比分胜/平。 |
| `HomeMajorText` / `AwayMajorText` | 前台大比分文本，建议保持当前 `W{Win}  D{Tie}` 风格。 |
| `HomeTotalMinorScore` / `AwayTotalMinorScore` | 所有已记录半场的主客小比分合计。 |
| `CurrentSurTeamMinorScoreText` | 当前求生者队伍在当前半场窗口中应显示的累计小比分文本（同 Game 内从第一半到当前半场已记录小比分之和）。 |
| `CurrentHunTeamMinorScoreText` | 当前监管者队伍在当前半场窗口中应显示的累计小比分文本（同 Game 内从第一半到当前半场已记录小比分之和）。 |
| `CurrentSurTeamMajorText` | 当前求生者队伍对应的大比分文本。 |
| `CurrentHunTeamMajorText` | 当前监管者队伍对应的大比分文本。 |

## 6. ScoreSurWindow / ScoreHunWindow 行为

`ScoreSurWindow` 和 `ScoreHunWindow` 已迁移为 v3 renderer pilot。内置默认 layout 已从旧的 `Team.Score` 字段切换到 `CurrentGame.MatchScore` 派生字段：

| 窗口 | 旧绑定 | 当前默认绑定 |
| --- | --- | --- |
| `ScoreSurWindow` | `CurrentGame.SurTeam.Score.MajorPointsOnFront`、`CurrentGame.SurTeam.Score.GameScores` | `CurrentGame.MatchScore.CurrentSurTeamMajorText`、`CurrentGame.MatchScore.CurrentSurTeamMinorScoreText` |
| `ScoreHunWindow` | `CurrentGame.HunTeam.Score.MajorPointsOnFront`、`CurrentGame.HunTeam.Score.GameScores` | `CurrentGame.MatchScore.CurrentHunTeamMajorText`、`CurrentGame.MatchScore.CurrentHunTeamMinorScoreText` |

局内比分窗口显示规则：

| 当前半场 | 显示小比分 |
| --- | --- |
| 当前半场为第一半 | 显示第一半已记录的 MinorScore（按当前阵营映射）；未记录显示 `0`。 |
| 当前半场为第二半 | 显示同 Game 内第一半 + 第二半已记录 MinorScore 之和（按当前阵营映射）；第二半未记录时只累计第一半。 |
| `Free` 进度 / 无对应 Game | 显示 `0`。 |

累计小比分按当前阵营映射。每个 `ScoreHalf` 的 `HomeMinorScore` / `AwayMinorScore` 由记录时阵营派生，多半场累加以 Home/Away 为稳定身份，再按当前阵营映射到求生者/监管者窗口；记录后发生换边时，历史得分归属仍正确（与全局比分格的阵营映射方式一致）。

例：第一半录入 `Escape4`（5:0），切到第二半未录入时显示 `5:0`；第二半录入 `Tie`（2:2）后显示 `7:2`。

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

当前后台实现不再暴露普通 UI 的 `IsGameFinished`、手动 Game/half 选择、手动 `Team.Score` 累加或“同步至前台”流程。清除当前半场比分按钮位于旧“小比分清零”按钮位置，会把全局 `CurrentGame.GameProgress` 对应半场的 `ScoreHalf.Result` 设为 `null`；后台预览表和 `ScoreGlobalWindow` 都由 `CurrentGame.MatchScore` 绑定自动刷新。

后台 `ScorePage` 的预览表是导播检查用只读 UI。其 `ScorePreviewRow` 集合从 `CurrentGame.MatchScore.Games` 重建，BO3 显示 Game 1、Game 2、Game 3 和 Game 3 Overtime，BO5 显示 Game 1 到 Game 5 以及 Game 5 Overtime。表格使用 `ScoreGameKey` / 现有显式可见性规则区分 Game 3 Overtime 与 Game 4，不把 `GameProgress` 原始数值作为唯一判断依据。行内结果、阵营和主客小比分均来自对应 `ScoreHalf` 的已记录结果及记录时主客队映射；空结果显示 `-`。它不提供行点击切换、手动 Game/half 选择或编辑能力，也不替代 Score System v2 的权威状态。

## 8. ScoreGlobalWindow 行为

`ScoreGlobalWindow` 已迁移到 v3 renderer，默认布局位于 `Resources/FrontedLayouts/ScoreGlobalWindow.json`。它绑定现有 `Core.Models.Game` 持有的 `MatchScoreState`，不依赖 `ScoreWindowViewModel` 独有字段或 `FrontedWindowService` 直接变更控件。

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

全局比分格表示 `ScoreGame` 内部的 `ScoreHalf` 结果，由内置 v3 控件 `GlobalScoreRow` 的 `Cells` 显式配置。总分显示从 `MatchScoreState` 派生，不再从 `ScoreWindowViewModel` 独有字段或 `FrontedWindowService` UI mutation 派生。每个 cell 用 `ScoreGameKey` 和 `ScoreHalfKind` 定位比分，避免依赖 `GameProgress` 原始数值。阵营图标颜色由 `CampIconColor` 控制，支持黑/白两种填充色；运行时基于原始阵营图标资源的 alpha 直接填充颜色，不需要维护额外黑色图标资源。`ScoreGlobalWindow/BaseCanvas` 使用 Designer v3 通用 Canvas BO states：BO5 是 root/default state，BO3 是 `BoModeStates["Bo3"]`，背景、总分位置、父行框和 cell 列表都由对应 state 决定；窗口订阅 `IsBo3ModeChanged` 后会重新应用 v3 布局，让 BO3/BO5 state 即时刷新。

## 9. 导入、导出与新建对局

`MatchScoreState` 是现有 `Core.Models.Game` 的一部分，因此：

| 场景 | 行为 |
| --- | --- |
| 导出对局 | 序列化 `Game.MatchScore`。 |
| 导入对局 | 从 JSON 恢复 `Game.MatchScore`，并在队伍信息导入后保持历史半场的阵营映射；有效 `MatchScore` 不会被旧 `Team.Score` 字段覆盖。 |
| 新建对局 | 从旧 `CurrentGame.MatchScore` clone/carry 到新 `Game`。 |
| 回溯对局 | 不依赖页面 ViewModel 是否还存在，不依赖前台窗口是否打开。 |

新建对局的 carry 行为应和当前地图状态类似：`SharedDataService.NewGame()` 先读取旧 `CurrentGame` 的可延续状态，再构造新的 `Core.Models.Game`。实现时需要明确哪些比分字段可延续；建议延续整场 `MatchScoreState`，并由当前 `GameProgress` 决定后续编辑位置。

## 10. 兼容策略

| 旧能力 | 迁移期策略 |
| --- | --- |
| `Team.Score.GameScores` | 仅作为旧 JSON/旧 DTO 反序列化兼容字段保留；运行时不再从 `MatchScoreState` 派生写回。后台 `ScorePageViewModel` 和默认 v3 布局不把它作为权威写入点。 |
| `Team.Score.MajorPointsOnFront` | 仅随旧 `Team.Score.Win` / `Tie` 自身格式化；局内比分 v3 默认布局读取 `MatchScoreState` 派生字段。 |
| `ScorePageViewModel.GameGlobalInfoRecord` | 已移除。 |
| `ScoreWindowViewModel.TotalMainGameScore` / `TotalAwayGameScore` | 已移除；总小比分直接绑定 `CurrentGame.MatchScore.HomeTotalMinorScore` / `AwayTotalMinorScore`。 |
| `FrontedWindowService.SetGlobalScore*` / `ResetGlobalScore` | 已完全移除；全局比分窗口状态由 `CurrentGame.MatchScore` 驱动。 |

## 11. 已实现功能总览

Score System v2 的所有环节（Score 当前实现）已全部完成。核心进展：

| 层次 | 实现内容 |
| --- | --- |
| 基础模型 | `MatchScoreState`、`ScoreGame`、`ScoreHalf`、`ScoreGameKey` 已实现，权威比分状态由 `Core.Models.Game` 持有。 |
| 服务层 | `IMatchScoreService` / `MatchScoreService` 提供结果写入、清除、BO 模式重算和队伍映射。 |
| 后台 UI | `ScorePageViewModel` 结果按钮直接操作 service；移除手动 Game/half 选择、手动累加和同步按钮；只读预览表由 `MatchScoreState.Games` 派生。 |
| 前台绑定 | `ScoreSurWindow`、`ScoreHunWindow`、`ScoreGlobalWindow`、`CutSceneWindow`、`GameDataWindow` 的 v3 layout 绑定 `MatchScoreState` 派生字段。 |
| 全局比分 | 内置 `GlobalScoreRow` 控件，由 `Cells` 显式配置比分格，运行时从 `MatchScoreState` 解析；BO3/BO5 由通用 Canvas states 控制。 |
| 清理 | `GameGlobalInfoRecord`、`ScoreWindowViewModel` 总分字段、`FrontedWindowService.SetGlobalScore*` 调用链已完全移除。 |

## 12. 重要警告

1. 不要把权威比分状态存到 UI 控件里。
2. 不要继续让 `ScorePageViewModel` 作为比分数据库。
3. 不要把新的 v3 前台控件绑定到 `ScoreWindowViewModel` 独有字段。
4. 不要把 `MatchScoreState` 派生值同步回 `Team.Score`；旧字段只用于旧数据兼容。
5. `GameProgress.Free` 不能写入当前半场结果，但总分重算仍必须按 BO 模式执行。
6. `Game3Overtime*` 与 `Game4*` 当前 enum 数值重叠，映射实现必须显式处理上下文。

## 13. 待确认问题

| 问题 | 当前建议 |
| --- | --- |
| `Free` 模式是否允许手动写比分 | 暂不支持，记录为设计缺口。暂时在Free下禁用相关按钮，且对外显示全部为 0 |
| BO3 中第三场加赛与 BO5 第四场的持久化 key | 使用 `ScoreGameKey`，避免只靠 `GameProgress` 数值。 |
| 旧 `Team.Score` 字段何时删除 | 等旧 DTO 和 legacy 导入路径完全收口后再删除。旧记录中的 `Team.Score` 无法安全还原完整 per-Game/per-Half 历史；导入器不会伪造半场结果。 |
| 全局比分 v3 控件类型 | 已新增内置 `GlobalScoreRow`，通过统一 V3 Control API（`FrontedV3ControlBase` + `[FrontedV3Control]` + `AddFrontedV3Control<TControl>()`）注册并由 JSON `ControlType = "GlobalScoreRow"` 使用。 |
