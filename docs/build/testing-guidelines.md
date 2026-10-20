# 测试策略指南

本文是测试资产政策的唯一详细规范，记录 `neo-bpsys-wpf.Tests` 的维护边界，尤其用于避免 Designer v3 和后台 WPF UI 调整后被脆弱结构测试牵着走。

## 测试资产准入与清理

**测试可以大量临时执行，但永久测试应当尽可能少。** 本项目不采用测试驱动开发，不要求 AI 在每次功能修改后扩充永久单元测试套件。测试保护稳定业务行为、数据契约和高风险回归，优先断言 externally observable behavior，不冻结 implementation detail。

### 临时验证与永久测试

开发中可以编写临时单元/集成测试、一次性验证程序或脚本，也可以运行现有测试，执行构建、启动和实际功能验证，通过日志、断点检查运行行为。充分验证是必要工作，但验证代码不默认成为仓库资产。

一般功能开发、UI 调整、ViewModel 改动和普通 Bug 修复，默认不增加永久测试文件，优先复用现有测试或采用临时验证。不创建新的测试工程、测试基础设施或额外门禁，不为每个类、方法、Command、属性配套测试，不以新增测试数量证明工作质量。

一次性验证必须先实际运行、确认结果并修复发现的问题，再删除临时测试代码及其产物。任务结束前清理临时脚本、验证工程、调试代码和测试产物；不得仅因测试是本次任务编写的，就默认保留或提交。不要删除用户文件、正式发布产物或无法确认归属的文件。

### 永久保留的门槛

永久测试必须有明确、独立的长期回归价值，通常限于：

- 用户数据安全、配置迁移和持久化兼容性；
- BPUI 包格式、导入导出和重要历史版本兼容性；
- 插件 SDK 的公开契约与向后兼容性；
- SmartBP 关键状态协调及复杂动画状态机；
- 其他一旦回归会导致重大功能故障，且编译或普通人工测试难以及时发现的行为。

即使属于上述类别，也应优先扩充已有测试的必要场景，避免新增文件、大量 Mock、Fixture 或通用基础设施。保留永久测试前必须明确回答：

> 未来代码发生什么类型的变更时，这个测试能够发现一个编译器和其他测试发现不了的重要问题？

不能明确回答时，原则上不永久保留。使用反射验证真实公开 ABI/插件二进制兼容契约，不等同于验证私有实现；必须按实际契约判断价值。

禁止新增或永久保留以下低价值验证：

- 读取 `.cs` / `.xaml` 源文件，通过字符串、Regex、IndexOf 等检查实现方式；
- 断言 private field、private method、局部变量名或内部数据结构；
- 要求算法必须使用指定 collection、helper 或具体语句顺序；
- 仅证明某次重构、Task、Phase 或 Fix 已完成；
- 仅检查硬编码常量、样式、坐标、Margin、Padding、Width、Height、Canvas 大小、Tooltip 或 XAML 排版；视觉实现的唯一例外是 WPF/XAML 语法或运行时必需命名部件；
- trivial property、constructor、forwarder、框架自身行为或只有命令/属性存在性的验证；
- 无有效业务断言、仅断言测试夹具自身数据，或机械新增的 regression test。

持久化几何、文件格式字面量、算法输出和公开 SDK 字符串契约不应仅因涉及坐标或字符串就当作视觉/常量测试删除。

### 清理已有测试

清理以降低长期维护负担为目标，不追求删除数量，也不只重组文件。逐项区分核心回归、一次性开发残留、重复契约、实现细节和特殊环境验证：无长期价值的直接删除；重要但膨胀的合并、简化并保留有效行为断言；需要设备、原生组件、大型模型或复杂外部环境的，评估改为显式诊断工具或可选集成验证。

不得为了补偿删除测试，再新增同等数量的测试、Mock、Fixture 或基础设施。不得通过跳过测试、移除有效断言或放宽构建标准使任务显示完成。已有低价值门禁失败时应删除或简化该门禁，不得为它回滚合理业务实现。

时间相关验证优先使用现有同步信号、可控时钟或有期限的状态等待，不靠延长 Sleep/Delay 解决脆弱性。模拟取消状态的可取消无限等待不等同于固定 Sleep。

WPF/Dispatcher 的 STA helper、超时与释放要求见本文“WPF UI 测试稳定性”。

### 执行验证与报告

减少永久测试不降低验证要求：按改动选择合适的现有测试、临时程序、构建或功能验证，对新代码实际运行验证；失败后修复并再次验证。临时测试删除前必须实际执行。

最终报告说明执行的验证、结果及清理掉的临时资产。无法执行的验证必须说明原因和剩余风险，不得把未运行描述为通过。依赖缺失不得空执行后报告成功；可选验证要明确报告未执行，显式启用后的依赖缺失应准确报告失败。

Plan 阶段只调查并制定计划，不删除测试，不为证明删除建议而创建永久测试。报告列出建议删除、精简、保留或转为可选的项目、业务依据、风险和预期维护成本变化；用户明确授权的规范文档更新可单独执行。


## UI 修改的验证选择

前述视觉与源码匹配禁令适用于 Designer、PluginPage 等所有 WPF UI。若需保护 XAML 语法或运行时必需命名部件，应实际解析/实例化验证，而不是读取源文本。失败先区分业务契约与视觉细节：前者修实现并复用测试；后者删除低价值断言，不为脆弱布局测试回滚合理 UI。视觉调整以运行/人工验证为主，不自动新增截图基础设施。

文档-only 至少执行 git diff --check、git diff --stat 与内部链接检查；代码按风险运行相关测试或构建，发布前执行完整验证。当前 runner 命令与 .NET 10 入口差异统一见[验证与调试](testing-and-debugging.md)。

## SmartBP / OpenCvSharp 本机依赖

部分 SmartBP / OCR 测试会间接初始化 OpenCvSharp。如果开发机缺少 `OpenCvSharpExtern` 或出现 `DllNotFoundException: Unable to load DLL 'OpenCvSharpExtern'`，通常不需要为普通功能改动处理。当前可用的 OpenCvSharp native DLL 主要面向 x86/x64，部分开发机是 ARM64，完整 `dotnet test` 可能因此在 SmartBP/OCR 相关用例中失败。遇到这类失败时，应优先确认本次改动范围；非 SmartBP/OCR 改动可以记录为本机 native runtime 缺失，并运行相关的非 OCR 定向测试完成验证。

## WPF UI 测试稳定性

WPF 控件测试应放入非并行 collection，避免多个 STA 窗口和 dispatcher pump 同时争用进程级 WPF 状态。测试里能关闭动画时应关闭，例如 `TransitionDuration=0`、滚动行为 `Duration=0`；窗口、事件订阅和附加行为必须在 `finally` 中关闭或解绑。

WPF/Dispatcher 测试必须使用 `neo_bpsys_wpf.Tests.Infrastructure.WpfTestThread` 运行 STA 代码，不要在测试文件里复制 `new Thread(...)`、`thread.Join()`、`new Thread(async () => ...)` 或裸 `TaskCompletionSource` 等待。已确认的超时规律是：手写 STA helper 若没有硬超时、没有 `Dispatcher.InvokeShutdown()`、或用 `async void` 形式的线程入口，测试失败时容易变成整套 `dotnet test` 挂起，而不是报告具体失败项。同步测试用 `WpfTestThread.Run(...)`，异步测试用 `WpfTestThread.RunAsync(...)`；如果测试动作超过默认 15 秒，应先检查消息泵、窗口关闭、事件解绑和后台任务收口，而不是简单增加超时时间。

滚轮测试优先构造轻量控件，不要为了验证滚动归属启动完整页面。`ModernScrollViewer` 的测试应覆盖普通内容、非约束 `ListView` / `ListBox` 内容、显式 `ModernScroll.Ownership="Self"` 区域和打开的 `ComboBox` / `Popup` 保护。不要写“所有 `ListView` / `ListBox` 都 self-scroll”的断言。

显式 self-scroll 控件应测试 `NestedSmoothScrollBehavior` 是否滚动自己的内部 `ScrollViewer`，以及到顶/到底时是否不强行吞事件。`ComboBoxDropdownSmoothScrollBehavior` 应测试下拉打开后能找到 dropdown `ScrollViewer`、滚轮只移动下拉、不移动外层页面，并在关闭/卸载时解绑。不要通过增加超时时间掩盖 dispatcher 或事件泄漏问题。
