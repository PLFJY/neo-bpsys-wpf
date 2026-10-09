# 测试策略指南

本文记录 `neo-bpsys-wpf.Tests` 的维护边界，尤其用于避免 Designer v3 和后台 WPF UI 调整后被脆弱结构测试牵着走。

## 核心原则

永久测试的准入和清理规则以根目录 `AGENTS.md` 的「测试策略」为准：默认不新增，开发验证可以临时大量执行，完成后清理临时资产。以下是可能具有长期价值的行为范围，不代表每项功能都应配套新增测试：

| 类型 | 示例 |
| --- | --- |
| 高风险 ViewModel 行为 | 撤销导致数据丢失、跨窗口设置误写、复杂状态协调；普通命令和属性展示优先临时验证 |
| 服务和模型 | `FrontedLayoutService` 加载路径、包导入导出、插件 registry、Score System v2、导入导出兼容 |
| v3 布局契约 | JSON schema、控件配置 roundtrip、缺失插件占位符、插件依赖扫描 |
| 数据迁移 | legacy `.bpui` 转换、旧 Game JSON 兼容、缺字段不崩溃 |

## XAML 测试边界

禁止新增样式/布局宽高类测试。测试不得断言视觉样式、坐标、窗口宽高、Canvas 宽高、控件位置、Margin/Padding、精确行列结构等展示细节；唯一例外是为了验证 WPF/XAML 语法、解析、或 code-behind 运行时必需命名部件是否正确。已有此类测试失败时，应删除或降级为行为/契约测试，不得为了通过测试回滚布局。

不要通过读取 `.cs` / `.xaml` 源文件、字符串搜索或正则冻结实现。确需保护 WPF/XAML 语法或运行时必需命名部件时，应实际解析或实例化控件验证；命令、属性和事件处理器名称存在性本身不足以成为永久测试。

Designer v3、`PluginPage`、`FrontedDesignerWindow` 等 UI 会随交互体验持续调整。测试不应强迫一个固定 XAML 布局，只应保护行为和 code-behind 必要命名契约。

## UI 变更时怎么处理

当 UI 意图发生变化时，AI agent 和维护者不应为了通过脆弱 XAML 测试而回滚 UI。正确处理顺序是：

1. 确认失败测试保护的是行为契约还是视觉实现细节。
2. 如果是行为契约，修复实现并复用已有测试；必要时用临时验证确认。
3. 如果只是 XAML 结构细节，删除低价值断言；运行时必需命名部件按实际解析行为验证。
4. 视觉调整以运行和人工验证为主，不自动扩充永久测试或截图基础设施。

文档-only 改动通常不需要完整 build，但提交前仍应至少运行 `git diff --check` 和 `git diff --stat`。涉及服务、模型、导入导出或 Designer v3 行为时，应运行对应测试；发布前运行 `dotnet build` 和 `dotnet test`。

## SmartBP / OpenCvSharp 本机依赖

部分 SmartBP / OCR 测试会间接初始化 OpenCvSharp。如果开发机缺少 `OpenCvSharpExtern` 或出现 `DllNotFoundException: Unable to load DLL 'OpenCvSharpExtern'`，通常不需要为普通功能改动处理。当前可用的 OpenCvSharp native DLL 主要面向 x86/x64，部分开发机是 ARM64，完整 `dotnet test` 可能因此在 SmartBP/OCR 相关用例中失败。遇到这类失败时，应优先确认本次改动范围；非 SmartBP/OCR 改动可以记录为本机 native runtime 缺失，并运行相关的非 OCR 定向测试完成验证。

## WPF UI 测试稳定性

WPF 控件测试应放入非并行 collection，避免多个 STA 窗口和 dispatcher pump 同时争用进程级 WPF 状态。测试里能关闭动画时应关闭，例如 `TransitionDuration=0`、滚动行为 `Duration=0`；窗口、事件订阅和附加行为必须在 `finally` 中关闭或解绑。

WPF/Dispatcher 测试必须使用 `neo_bpsys_wpf.Tests.Infrastructure.WpfTestThread` 运行 STA 代码，不要在测试文件里复制 `new Thread(...)`、`thread.Join()`、`new Thread(async () => ...)` 或裸 `TaskCompletionSource` 等待。已确认的超时规律是：手写 STA helper 若没有硬超时、没有 `Dispatcher.InvokeShutdown()`、或用 `async void` 形式的线程入口，测试失败时容易变成整套 `dotnet test` 挂起，而不是报告具体失败项。同步测试用 `WpfTestThread.Run(...)`，异步测试用 `WpfTestThread.RunAsync(...)`；如果测试动作超过默认 15 秒，应先检查消息泵、窗口关闭、事件解绑和后台任务收口，而不是简单增加超时时间。

滚轮测试优先构造轻量控件，不要为了验证滚动归属启动完整页面。`ModernScrollViewer` 的测试应覆盖普通内容、非约束 `ListView` / `ListBox` 内容、显式 `ModernScroll.Ownership="Self"` 区域和打开的 `ComboBox` / `Popup` 保护。不要写“所有 `ListView` / `ListBox` 都 self-scroll”的断言。

显式 self-scroll 控件应测试 `NestedSmoothScrollBehavior` 是否滚动自己的内部 `ScrollViewer`，以及到顶/到底时是否不强行吞事件。`ComboBoxDropdownSmoothScrollBehavior` 应测试下拉打开后能找到 dropdown `ScrollViewer`、滚轮只移动下拉、不移动外层页面，并在关闭/卸载时解绑。不要通过增加超时时间掩盖 dispatcher 或事件泄漏问题。
