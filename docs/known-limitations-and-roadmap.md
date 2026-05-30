# 已知边界与路线图提醒

本文面向维护者和 AI agent，用于避免把当前边界误判为已完成能力。

## SmartBP

| 能力 | 状态 |
| --- | --- |
| 赛后数据 OCR 自动回填 | 成熟且可用 |
| 全流程自动 BP / 自动 BP 画面切换 | TODO |

`SmartBpService.StartSmartBp()` 当前有 `DispatcherTimer` 框架，但 `Timer_Tick` 中还没有完整自动 BP 识别/切屏流程。不要在未明确收到需求时补全这条链路，也不要在文档或 UI 中声称它已完成。

## 插件系统

插件是全信任模型，不是沙箱。当前风险控制是插件市场审核、微步云恶意文件扫描、人工审查和较小的插件生态。

插件加载发生在启动期间、Host build 前。安装或更新插件后需要重启，不能假设支持热加载或热更新。

`Assembly.LoadFrom` 加载入口程序集，依赖解析主要依赖插件目录、宿主已有程序集和 .NET 默认上下文。插件打包时不要漏掉自身直接依赖，也不要把宿主已有依赖重复打包成冲突版本。

## 版本概念

插件 API 版本和 PluginSdk NuGet 包版本是故意分离的：

| 概念 | 用途 |
| --- | --- |
| 插件 API 版本 | `manifest.yml` 的 `apiVersion`，宿主加载兼容性 |
| PluginSdk NuGet 包版本 | 插件项目编译和打包用的 SDK 包版本 |

不要把二者不同步当作错误。

## 前台窗口术语

`FrontedWindow` / 前台窗口是 WPF 输出窗口，用于 OBS 或其他直播软件捕获，不是 Web frontend。不要引入 Web 路由、浏览器布局或前后端分离假设。

Fronted Designer v3 当前已完成基础设施阶段，并将 `ScoreSurWindow` 和 `ScoreHunWindow` 作为局内比分 v3 renderer pilot 窗口。大多数前台窗口仍是 XAML-first，包括 `ScoreGlobalWindow`、`BpWindow`、`CutSceneWindow`、`GameDataWindow` 和 `WidgetsWindow`。旧设计者模式、旧 `config.json` 前台设置和旧 XAML-first 运行时仍保留。

## 文档边界

公开 VuePress 文档面向用户，可能落后于 UI 或内部实现。仓库内 `/docs` 面向维护者和 AI agent，应跟随代码架构变化更新。

## 代码中观察到的边界

1. `FrontedWindowService` 中窗口大小热更新相关代码目前是注释块，不应假设窗口尺寸设置已实时驱动所有前台窗口。
2. `neo-bpsys-wpf.Tests` 中 SmartBP 测试大多是注释中的手工调试样例，不能当作完整自动测试覆盖。
3. `App.xaml.cs` 更新检查条件写作 `#if !DEBUG && !Preview`，而项目配置定义 `PREVIEW`。这是代码观察到的命名 caveat；本文档不声称其运行时效果已经通过编译验证，本任务也不修改代码。
4. `GameRule.json` 是项目内规则配置，不是外部权威赛事规则源。
5. 前台默认布局依赖文件命名约定，插件窗口默认布局缺失时恢复默认会失败。

## Score System v2

比分系统正在迁移到现有 `Core.Models.Game` 持有权威状态，详见 [score-system-v2.md](score-system-v2.md)。Score Phase 2 已实现 `Game.MatchScore`、Score System v2 模型、`IMatchScoreService` / `MatchScoreService` 基础，并把后台 `ScorePageViewModel` 的比分写入和普通 UI 清理迁移到 service 驱动。当前代码仍存在这些边界：

| 边界 | 说明 |
| --- | --- |
| `Team.Score` 语义混杂 | 当前仍被旧窗口和旧页面使用；迁移期不要立即删除。新服务中的同步只是 transitional compatibility mirror，不是权威状态。 |
| `ScorePageViewModel.GameGlobalInfoRecord` 仍存在 | Phase 2 后它只是从 `MatchScoreState` 派生的调试/兼容视图，不是权威数据；普通 UI 不再提供手动选择或同步入口。 |
| `ScoreGlobalWindow` 仍依赖服务动态控件 | `FrontedWindowService` 会创建并直接修改全局比分格，阻碍干净的 v3 绑定渲染。 |
| `GameProgress.Free` 未定义比分语义 | Score System v2 暂把它记录为设计缺口。 |
| `Game3Overtime*` 与 `Game4*` enum 数值重叠 | `MatchScoreService` 结合 BO3/BO5 状态解析；缺少上下文的 `MatchScoreState.GetGame(progress)` 保守按 BO5 第四局解析。 |
