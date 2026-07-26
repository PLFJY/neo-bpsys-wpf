# 命名规范

类（Class）、属性（Property）、方法（Methods）统一采用大驼峰命名法（upper camel case / PascalCase）。

字段（Field）统一采用 `_` + 小驼峰命名法（lower camel case / camelCase）。

## 与游戏名词相关的术语

| 术语 | 含义 |
| --- | --- |
| Ban | 禁用 |
| Pick | 选择 |
| Sur | 求生者 |
| Hun | 监管者 |
| Camp | 阵营 |
| Talent | 天赋 |
| Trait | 辅助特质 |
| Game | 对局，单独出现时是单个游戏对局，在 GameProgress 中是指一个回合 |
| Character / Chara | 角色 |
| Map | 地图 |
| GameProgress | 对局进度，如 Game1FirstHalf 第一局上半场 |
| Team | 队伍 |
| Main | 主队 |
| Away | 客队 |
| Member | 队伍成员 |
| Player | 选手（包含 Member） |
| OnField | 已上场 |
| Score | 比分 |
| Win | 胜 |
| Lose | 负 |
| Tie | 平 |
| MinorPoint | 小比分 |
| MajorPoint | 大比分 |
| Front | 前台（bp 展示界面） |
| Interlude | 过场画面，展示阵容和天赋 |
| GameData | 赛后数据 |

## 命名顺序约定

禁用（Ban）相关的命名统一采用动词在前，例如 `BanHun`（禁用监管者）。

其余均为名词在前，例如 `SurPick`（求生者选择）。

## 与框架激活语义的关系

`IsActive` 专用于内部框架/运行时的激活语义（尤其是 CommunityToolkit.Mvvm `ObservableRecipient.IsActive`），不得用于布局/包/设置/业务数据。详见 [AGENTS.md](../AGENTS.md) 的「命名规则：请勿使用通用的 IsActive」章节。
