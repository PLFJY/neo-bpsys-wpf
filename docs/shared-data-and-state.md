# 共享数据与状态流

## 为什么 SharedDataService 是中心

`SharedDataService` 是导播后台、前台窗口、引导式 BP、比分、SmartBP 等功能的共享状态中心。它通过 `ISharedDataService` 暴露当前对局、主客队、角色字典、Ban 位可用状态、倒计时、BO3/BO5 状态和地图 V2 状态。

前台窗口 ViewModel 不应复制一份比赛状态，而应读取 `ISharedDataService`：

```csharp
public Game CurrentGame => _sharedDataService.CurrentGame;
public string RemainingSeconds => _sharedDataService.RemainingSeconds;
public ObservableCollection<bool> CanCurrentSurBanned => _sharedDataService.CanCurrentSurBannedList;
```

典型例子是 `BpWindowViewModel` 和 `WidgetsWindowViewModel`：它们订阅 `CurrentGameChanged`、`IsBo3ModeChanged`、`CountDownValueChanged` 等事件，然后触发自身属性更新，让 XAML 绑定刷新。

## 稳定对象与可替换对象

`HomeTeam` 和 `AwayTeam` 在接口注释中明确是“全场始终不变”的对象。导入队伍信息时使用 `Team.ImportTeamInfo(Team)` 更新对象内部内容，而不是替换 `HomeTeam` / `AwayTeam` 引用。

`CurrentGame` 则可能被替换：

| 操作 | 行为 |
| --- | --- |
| `NewGame()` | 根据当前主客队阵营创建新的 `Game`，保留地图和对局进度 |
| `ImportGameAsync(filePath)` | 导入对局 JSON，更新主客队信息，再构造新的 `Game` |
| `Game.Swap()` | 当前对局内部交换求生/监管队伍，并触发换边事件 |

`SharedDataService` 在替换 `CurrentGame` 时会先取消旧对局事件订阅，再订阅新对局事件，并触发 `CurrentGameChanged`。页面如果缓存了旧 `Game` 引用，可能会读到过期状态。

## Game / Team / Player 数据关系

| 模型 | 职责 |
| --- | --- |
| `Game` | 当前局信息：队伍、进度、地图、当前 Ban、上场选手、赛后数据 |
| `Team` | 主队/客队数据：名称、Logo、队员、比分、全局 Ban 记录 |
| `Player` | 当前上场选手：成员、角色、天赋、辅助特质、赛后数据 |
| `PlayerData` | 赛后数据字段，空值显示为 `-` |

`Game` 构造时会基于 `SurTeam.SurMemberOnFieldCollection` 和 `HunTeam.HunMemberOnField` 创建 `Player`，并在队员上场变化时重新装填成员引用。

## Ban 状态

`SharedDataService` 维护四个“是否可用”的 Ban 位列表：

| 列表 | 长度来源 |
| --- | --- |
| `CanCurrentSurBannedList` | `AppConstants.CurrentBanSurCount` |
| `CanCurrentHunBannedList` | `AppConstants.CurrentBanHunCount` |
| `CanGlobalSurBannedList` | `AppConstants.GlobalBanSurCount` |
| `CanGlobalHunBannedList` | `AppConstants.GlobalBanHunCount` |

`SetBanCount` 通过把前 N 个位置设为 `true` 来控制页面可用 Ban 位。列表项变化会触发 `BanCountChanged`。引导式 BP 启动时会根据 `GameRule.json` 调整这些列表。

当前局禁用角色存储在 `CurrentGame.CurrentSurBannedList` 和 `CurrentGame.CurrentHunBannedList`。

全局 Ban 有两层数据：

| 列表 | 语义 |
| --- | --- |
| `Team.GlobalBannedSurList` / `GlobalBannedHunList` | 当前已经生效的全局禁选，是前台显示和选择器禁用判断的权威源 |
| `Team.GlobalBannedSurRecordList` / `GlobalBannedHunRecordList` | 本轮该阵营 Pick 的暂存记录，供下一次队伍再次轮到该阵营时写入生效列表 |

角色选择服务自动记录 Pick 时只写入 `RecordList`，不会立即改变当前生效的全局禁选。队伍再次轮到对应阵营、新建对局或启动引导时，`UpdateGlobalBanFromRecord()` 才会把暂存记录中的非空角色覆盖到当前生效列表。

### 角色禁用状态联动

`CharaSelectViewModelBase` 提供了 `DisabledKeys` 属性（`ISet<string>`），实时计算当前对局中同阵营已被 Ban 或已被 Pick 的角色名集合，用于在 `CharacterSelector` 下拉列表中灰显这些选项。

| 场景 | 被禁用的角色 |
| --- | --- |
| 求生者 Ban / Pick 位 | 已启用的 `CurrentSurBannedList` + `CurrentGame.SurTeam.GlobalBannedSurList` + `SurPlayerList` 中已 Pick 的角色 |
| 监管者 Ban / Pick 位 | 已启用的 `CurrentHunBannedList` + `CurrentGame.HunTeam.GlobalBannedHunList` + `HunPlayer.Character`（若非空） |

数据流：

```
CurrentGame.BannedList / Player.Character 变化
  → CollectionChanged / PropertyChanged 事件（Game 级别）
    ─────────────────────────────────────────────
SharedDataService.HomeTeam/AwayTeam.GlobalBannedSur(Hun)List 变化
  → CollectionChanged 事件（当前生效列表为权威源）
    ─────────────────────────────────────────────
    → CharaSelectViewModelBase.UpdateDisabledKeys()
      → DisabledKeys 属性变更
        → CharacterSelector.DisabledKeys DP
          → ComboBox.ItemContainerStyle (MultiBinding)
            → ComboBoxItem.IsEnabled = false
```

`UpdateDisabledKeys` 只读取当前阵营队伍的 **生效列表（GlobalBanned*List）**，不会读取或订阅 `RecordList`。因此本轮 Pick 写入暂存记录后不会立即干扰 Ban/Pick；等队伍下一次轮到相同阵营、暂存记录覆盖到生效列表后，角色才会被禁用。读取时额外校验 `team.Camp` 与当前阵营一致，并在 `TeamSwapped` 完成态再次刷新，避免 `Game.Swap()` 先换 Camp、后换引用的中间态留下错误结果。

### 换边复现步骤实际验证什么

原始的五步复现不是要求换边时删除队伍自身保存的全局禁选，而是在验证：**选择器禁用状态必须跟随当前阵营上的队伍重新计算，不能残留上一支队伍的结果。**

假设主队 A 在求生者阵营 Pick 了“医生”和“律师”，角色选择服务把它们写入 A 的 `GlobalBannedSurRecordList`：

1. **写入暂存记录**：A 的求生者 `RecordList` 包含医生、律师，但它们尚未因此进入当前生效全局禁选。当前局是否可选仍由当前 Ban、当前 Pick 和生效列表决定。
2. **第一次换边，A：Sur → Hun**：客队 B 成为当前 `SurTeam`。求生者选择器必须只读取 B 的 `GlobalBannedSurList`，不能因为 A 的暂存记录而禁用医生、律师。
3. **打开 Ban 求生者页面**：用于确认页面初始化时也从当前 `SurTeam` 的生效列表派生状态，而不是保留旧缓存。
4. **第二次换边，A：Hun → Sur**：A 再次成为当前 `SurTeam`。A 的暂存记录已经覆盖到 A 的 `GlobalBannedSurList`，医生、律师此时应当不能再 Ban，也不能再 Pick。
5. **第三次换边，A：Sur → Hun**：B 再次成为当前 `SurTeam`。A 自己的 `GlobalBannedSurList` 中仍可保留医生、律师，但它们不属于当前求生者队伍，因此必须立即从求生者 `DisabledKeys` 中消失并恢复可选。

这五步同时验证三件事：

- `RecordList` 只是下一次同阵营对局的输入，不会直接影响当前选择器。
- `GlobalBanned*List` 跟随队伍保存，不因队伍暂时换到另一阵营而删除。
- `DisabledKeys` 跟随 `CurrentGame.SurTeam` / `HunTeam`，换边后不能残留上一支队伍的禁用角色。

### 前后状态示例

仍以求生者选择器为例，且所有相关 Ban 位均已启用：

| 数据 | 状态一：A 当前为求生者，记录尚未生效 | 状态二：A 再次成为求生者，记录已经生效 |
| --- | --- | --- |
| A 的 `GlobalBannedSurRecordList` | 医生、律师 | 医生、律师 |
| A 的 `GlobalBannedSurList` | 空 | 医生、律师 |
| 当前局 Ban | 祭司 | 祭司 |
| 当前局 Pick | 园丁 | 园丁 |
| 不能 Ban / Pick | 祭司、园丁 | 医生、律师、祭司、园丁 |
| 仍能 Ban / Pick | 医生、律师及其他未使用角色 | 其他未使用角色 |

如果状态二之后再换边，让 B 成为当前求生者队伍，则 A 的医生、律师仍保存在 A 的生效列表里，但不会出现在当前求生者 `DisabledKeys` 中。若某个 Ban 位关闭，即使该位置仍保存角色，也不应加入 `DisabledKeys`。

由此得到统一计算规则：

```
当前阵营 DisabledKeys
  = 当前阵营已启用的当局 Ban
  + 当前阵营队伍已启用的生效全局 Ban
  + 当前阵营本局已 Pick 的角色
```

`RecordList`、另一支队伍的生效全局 Ban、已关闭 Ban 位中的角色，都不参与当前阵营的 `DisabledKeys`。

导入对局（`ImportGameAsync`）恢复的是当前生效状态。`ImportTeamInfo` 在稳定的生效列表对象内更新内容，保证已有 ViewModel 的集合订阅继续有效；导入后调用 `ClearGlobalBanRecords()` 清空导入前的暂存记录，避免创建新 `Game` 时旧记录覆盖导入的生效列表。

设计原则：角色禁用状态是**派生数据**，不存储在 `Character` 模型上，避免在 NewGame / ImportGame 等操作时需要额外清理。

> **例外**：PickPage 中用于查看和手动调整暂存记录的 CharacterSelector（`HomeSur/HunGlobalBanRecordViewModel`、`AwaySur/HunGlobalBanRecordViewModel`）不绑定 `DisabledKeys`，否则暂存记录自身会阻止修正。

## 倒计时

倒计时使用 WPF `DispatcherTimer`，因此 Tick 发生在 UI 线程。状态字段是 `_remainingSeconds`：

| 方法/属性 | 行为 |
| --- | --- |
| `TimerStart(int? seconds)` | `null` 时不启动；否则设置秒数并启动计时器 |
| `TimerStop()` | 设置为 `-1`，停止计时器 |
| `RemainingSeconds` | 小于 0 显示 `VS`，否则显示数字 |
| `CountDownValueChanged` | 每次变化后触发 |

后台页面和前台窗口不要自己创建独立倒计时，否则会和全局状态不同步。

## BO3/BO5 与比分

`IsBo3Mode` 在 `SharedDataService` 中集中保存。变更时会：

1. 发送 `PropertyChangedMessage<bool>`。
2. 触发 `IsBo3ModeChanged`。

`ScorePageViewModel` 不再维护后台比分页专用的 BO3/BO5 Game/half 选择列表；比分控制跟随全局 `CurrentGame.GameProgress`。Designer v3 layout 可启用通用 BO3/BO5 Canvas states：root/default state 表示 BO5，`BoModeStates["Bo3"]` 表示 BO3。前台 v3 窗口在 `IsBo3ModeChanged` 后重载 layout，renderer 按 `ISharedDataService.IsBo3Mode` 选择对应 state；缺少 BO3 state 时回退 root/BO5 并记录 warning。`ScoreGlobalWindow` 只是该通用机制的一个使用者，背景、总分位置和控件配置不再通过 ScoreGlobal 专用 background variant 切换。`FrontedWindowService` 不再直接隐藏全局比分控件或移动 Total 控件。

`GlobalScoreTotalMargin` 仍在共享服务中暴露并保留设置项，但 v3 `ScoreGlobalWindow` 不依赖它；BO5/BO3 总分位置分别来自 root state 和 `BoModeStates["Bo3"]`。

Score System v2 的设计方向见 [score-system-v2.md](score-system-v2.md)。Phase 5 后，权威比分状态由现有 `Core.Models.Game.MatchScore` 持有，类型为 `MatchScoreState`；`IMatchScoreService` 只操作 `ISharedDataService.CurrentGame.MatchScore`，页面 ViewModel、前台窗口 ViewModel、`FrontedWindowService` 和 UI 控件都不能成为比分数据库。后台 `ScorePageViewModel` 的比分按钮已改为写入 `IMatchScoreService.CurrentHalf`，普通 UI 不再提供手动 Game/half 选择、手动 `Team.Score` 累加或“同步至前台”按钮；清除按钮位于旧“小比分清零”位置，会把当前半场结果设为 `null`。后台 `ScorePage` 的导播比分预览表只读显示 `CurrentGame.MatchScore.Games` 派生行，跟随全局 `CurrentGame.GameProgress` 和 BO3/BO5 状态，不提供手动场次选择；`ScoreSurWindow`、`ScoreHunWindow` 和 `ScoreGlobalWindow` 默认 v3 布局均读取 `CurrentGame.MatchScore`。迁移期 `Team.Score` 仍作为旧窗口兼容镜像存在，`MatchScoreService.SyncLegacyTeamScoreMirror()` 从 `MatchScoreState` 派生写回，新功能不应继续把 `Team.Score` 当作权威写入点。

`SharedDataService.NewGame()` 创建新 `Game` 时会 clone 当前 `CurrentGame.MatchScore`，避免新旧对局共享同一个可变比分实例。导入旧 JSON 时如果没有 `MatchScore` 字段，`Game` 会创建默认 `MatchScoreState`，以兼容旧保存记录；如果旧记录只有 `Team.Score` 值，当前实现不会尝试反推出完整 per-Game/per-Half 历史，只会记录 warning 并尽量保留旧 `Team.Score` 镜像显示。导入新格式时，任何有效 `MatchScore` 都不会被旧镜像覆盖。

## 事件模式

`ISharedDataService` 暴露的事件包括：

| 事件 | 常见用途 |
| --- | --- |
| `CurrentGameChanged` | 页面/前台窗口刷新 `CurrentGame` |
| `BanCountChanged` | Ban 页或前台控件刷新可用位 |
| `CountDownValueChanged` | 倒计时显示刷新 |
| `TeamSwapped` | 队伍换边后刷新阵营相关 UI |
| `IsBo3ModeChanged` | 切换 BO3/BO5 布局与比分 |
| `PickedMapChanged` | 地图显示刷新 |
| `MapV2BannedChanged` | 地图 V2 禁用状态刷新 |
| `IsMapV2BreathingChanged` / `IsMapV2CampVisibleChanged` | 地图 V2 前台表现刷新 |

因为页面和 ViewModel 多为 singleton，订阅事件时要避免重复订阅，也要注意长期订阅导致对象被持有。构造函数中订阅 singleton 服务事件通常可接受；动态创建对象订阅时需要考虑解绑。

## 维护坑点

1. 不要在页面 ViewModel 中复制 `CurrentGame` 的深层状态作为第二数据源。
2. 替换 `CurrentGame` 后，旧对象引用不会再收到共享服务事件。
3. 导入队伍应通过 `ImportTeamInfo`，不要替换 `HomeTeam` / `AwayTeam`。
4. 全局 Ban 有“记录列表”和“显示列表”两层，修改时要确认目标是哪一层。
5. `ObservableCollection` 应在 UI 线程更新；后台回调更新集合前先看 [threading-dispatcher-and-async.md](threading-dispatcher-and-async.md)。
6. SmartBP 写回赛后数据时直接修改 `CurrentGame` 中的 `PlayerData`，不要再维护 OCR 专用数据副本。
7. 导入对局（`ImportGameAsync`）后必须清空旧的 `RecordList` 暂存记录，不能把导入的生效列表反写到记录列表。
