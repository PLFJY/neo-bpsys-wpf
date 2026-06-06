# 测试策略指南

本文记录 `neo-bpsys-wpf.Tests` 的维护边界，尤其用于避免 Designer v3 和后台 WPF UI 调整后被脆弱结构测试牵着走。

## 核心原则

单元测试优先保护业务逻辑、稳定契约和可回归的服务行为。适合保留的测试包括：

| 类型 | 示例 |
| --- | --- |
| ViewModel 行为 | 命令、筛选、撤销重做、图层排序、属性编辑、拖拽提交等 |
| 服务和模型 | `FrontedLayoutService` 加载路径、包导入导出、插件 registry、Score System v2、导入导出兼容 |
| v3 布局契约 | JSON schema、控件配置 roundtrip、缺失插件占位符、插件依赖扫描 |
| 数据迁移 | legacy `.bpui` 转换、旧 Game JSON 兼容、缺字段不崩溃 |

## XAML 测试边界

XAML 文本测试只能作为 smoke test，保护 code-behind 或运行时真正依赖的稳定契约：

| 允许 | 不建议 |
| --- | --- |
| 必需命名部件存在，例如 `LayerPanelScrollViewer`、`LayerTopDropZone`、`LayerBottomDropZone`、`LayerDragGhost` | 断言精确 `RowDefinition` 数量 |
| code-behind 直接引用的事件处理器名存在，例如 `LayerTopDropZone_OnDrop` | 断言具体 `Grid.Row`、嵌套层级、`Margin`、`Padding`、宽高 |
| 关键命令或绑定入口存在，例如保存、撤销、打开浏览器按钮 | 断言视觉细节如 `TextTrimming`、某个控件必须用某种容器实现 |

Designer v3、`PluginPage`、`FrontedDesignerWindow` 等 UI 会随交互体验持续调整。测试不应强迫一个固定 XAML 布局，只应保护行为和 code-behind 必要命名契约。

## UI 变更时怎么处理

当 UI 意图发生变化时，AI agent 和维护者不应为了通过脆弱 XAML 测试而回滚 UI。正确处理顺序是：

1. 确认失败测试保护的是行为契约还是视觉实现细节。
2. 如果是行为契约，优先修实现或新增 ViewModel/服务级测试。
3. 如果只是 XAML 结构细节，降级为命名部件 / 事件 smoke，或删除该测试。
4. 视觉 polish 当前以人工验证为主；未来如需自动化，应优先引入截图回归测试，而不是继续扩张 XAML 字符串断言。

文档-only 改动通常不需要完整 build，但提交前仍应至少运行 `git diff --check` 和 `git diff --stat`。涉及服务、模型、导入导出或 Designer v3 行为时，应运行对应测试；发布前运行 `dotnet build` 和 `dotnet test`。

## WPF UI 测试稳定性

WPF 控件测试应放入非并行 collection，避免多个 STA 窗口和 dispatcher pump 同时争用进程级 WPF 状态。测试里能关闭动画时应关闭，例如 `TransitionDuration=0`、滚动行为 `Duration=0`；窗口、事件订阅和附加行为必须在 `finally` 中关闭或解绑。

滚轮测试优先构造轻量控件，不要为了验证滚动归属启动完整页面。`ModernScrollViewer` 的测试应覆盖普通内容、非约束 `ListView` / `ListBox` 内容、显式 `ModernScroll.Ownership="Self"` 区域和打开的 `ComboBox` / `Popup` 保护。不要写“所有 `ListView` / `ListBox` 都 self-scroll”的断言。

显式 self-scroll 控件应测试 `NestedSmoothScrollBehavior` 是否滚动自己的内部 `ScrollViewer`，以及到顶/到底时是否不强行吞事件。`ComboBoxDropdownSmoothScrollBehavior` 应测试下拉打开后能找到 dropdown `ScrollViewer`、滚轮只移动下拉、不移动外层页面，并在关闭/卸载时解绑。不要通过增加超时时间掩盖 dispatcher 或事件泄漏问题。
