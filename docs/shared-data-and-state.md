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

当前局禁用角色存储在 `CurrentGame.CurrentSurBannedList` 和 `CurrentGame.CurrentHunBannedList`。全局 Ban 记录在 `Team.GlobalBannedSurRecordList` / `GlobalBannedHunRecordList`，再由 `UpdateGlobalBanFromRecord()` 同步到显示列表。

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

`ScorePageViewModel` 通过 messenger 接收 BO3 状态变化，切换 BO3/BO5 的 `GameList` 并更新总分。`FrontedWindowService` 订阅 `IsBo3ModeChanged`，隐藏或显示部分全局比分控件，并调整 Total 位置。

`GlobalScoreTotalMargin` 也在共享服务中暴露，设置页修改后会同步给前台窗口服务。

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
