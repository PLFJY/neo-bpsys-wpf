# AGENTS.md

本仓库是 `neo-bpsys-wpf`，社区常称“第五人格 BP 展示工具”。架构上它是面向非官方第五人格赛事的 WPF 直播导播辅助系统：后台窗口供导播控制，前台窗口供 OBS 捕获，插件系统扩展页面、窗口、控件和服务。

## 关键术语

| 术语 | 含义 |
| --- | --- |
| BP display tool / BP 展示工具 | 社区名称，应保留 |
| 后台 / backend | 主 WPF 控制 UI，不是服务端 |
| 前台窗口 / FrontedWindow | WPF 输出窗口，不是 Web frontend |
| SmartBP | 当前成熟能力是赛后数据 OCR 自动回填；全流程自动 BP/自动切屏仍是 TODO |
| 插件 API 版本 | `manifest.yml` 的 `apiVersion`，用于宿主兼容性 |
| PluginSdk NuGet 包版本 | 插件项目引用的 SDK 包版本，和插件 API 版本不是同一概念 |

## 工作规则

1. 改代码前先读现有实现，不要发明架构。
2. 保持 WPF + Generic Host + DI 设计，页面、窗口、服务优先通过现有扩展注册。
3. 后台页面使用 `AddBackendPage<TView,TViewModel>()`。前台窗口使用强类型 registration 模型：XAML 窗口使用 `AddFrontedWindow<TView,TViewModel>()`，v3 layout 窗口使用 `AddFrontedV3LayoutWindow("WindowId", isBuiltIn: bool)`。内置前台窗口在 `App.Services.xaml.cs` 的 DI 配置中通过 `AddFrontedV3LayoutWindow(name, isBuiltIn: true)` 注册，由 `FrontedWindowBase` + v3 layout host 创建。每个 v3 layout window 内部固定一个 `BaseCanvas`，外部不再注册或选择 Canvas。`FrontedWindowRegistration` 基类只暴露 `Id`、`LocalId`、`PackageId`、`IsBuiltIn`、`DisplayName`、`Kind`；来源分组（BuiltIn / Plugin / External）由 UI 层基于 `IsBuiltIn + PackageId` 推导，不再有 `GroupKey`/`DisplayOrder`/`I18nDisplayNames`。前台控件使用统一 V3 Control API：控件继承 `FrontedV3ControlBase`、标注 `[FrontedV3Control("ControlId", IsBuiltIn = bool)]`、通过 `AddFrontedV3Control<TControl>()` 注册；控件不管理根布局（`Left`/`Top`/`Width`/`Height`/`ZIndex`/`Visibility`/`GaussianBlur` 由 `FrontedV3ControlHost` 统一负责），属性通过 `public static readonly FrontedV3Property<T>` 字段声明，Options 动态代理视图不进入 JSON。
4. WPF `Page` 只能作为 `Window`、`Frame` 或导航宿主的内容；严禁把 `Page` 直接放入 `ContentControl`、`Border`、`Grid`、`TabItem`、插件/模块内容 host 等嵌入容器。需要嵌入的页面片段必须用 `UserControl` 或普通 `Control`，否则会触发 `InvalidOperationException: Page can have only Window or Frame as parent.`
5. 不要把 FrontedWindow 理解成 Web 前端，也不要引入 Web 前端假设。
6. 不要随意大规模重构服务、ViewModel 或资源结构。
7. **默认保持既有行为与数据契约自然可运行，不要用读取期补字段、转换器、运行时猜测或 fallback 来掩盖新旧语义冲突。** 新功能应以增量方式加入：旧字段、旧字面量、旧流程继续按原有语义处理；只有实际使用新接入点、新字段或新模式时才启用新规则。除非开发者明确指定迁移或兼容策略，不得为了让旧数据通过新校验而手动补默认值、重写持久化内容，或把缺失字段猜成某个含义。需要区分模式时，应通过显式连接、明确字段或版本契约表达，并让校验在新能力被实际使用时给出准确错误。
8. 用户可见文本要考虑 `WPFLocalizeExtension` 和 `Locales/*.resx`，避免随手硬编码。
9. 插件安装/更新通常需要重启，因为插件在 Host build 前注入 DI。
10. 插件是全信任模型；安全边界依赖市场审核、微步云扫描、人工审查和小生态，不是沙箱。
11. 大部分按钮都需要有 `WPF-UI` 的图标。
12. 所有的公共属性和公共方法都需要写XML注释，包括参数、返回值、异常等。
13. **禁止在面向用户的 UI 文本中使用开发阶段占位表达**（如 "Phase 3"、"Phase 13E"、"Phase 9D" 等）。已实现功能的描述必须写实际行为；未实现功能的占位文本应写「将在后续版本中提供」而非内部阶段代号。真实 Placeholder（如 overlay 标签 `[Text]`）不在此限制内。
14. `IsActive` 只保留给框架/运行时激活语义，尤其是 CommunityToolkit.Mvvm `ObservableRecipient.IsActive`。布局、包、设置、业务状态、可见性、绑定 payload 和 behavior payload 不得使用泛名 `IsActive`，应使用 `IsActivePackage`、`IsVisible`、`IsEnabled`、`IsSelected` 等明确名称。`Visibility` 绑定不得直接绑定泛名 `IsActive`。
15. **禁止未经用户明确同意执行有副作用的 Git 命令**：包括但不限于 `git stash`、`git stash pop`、`git stash drop`、`git checkout .`、`git restore .`、`git reset`、`git clean`、`git switch`、`git checkout <branch>`、`git merge`、`git rebase`。本仓库可能处于无 initial commit 的状态，此时 `git stash` 会把大量未跟踪文件异常处理，导致工作区状态被搅乱、用户修改丢失。需要对比"改动前/后"行为时，应改用：直接读文件内容对比、用 `git diff`（只读）、或先询问用户如何验证。只允许执行只读类 git 命令（`git status`、`git diff`、`git log`、`git show` 等）。
16. **Diff 校验保护开发者修改**：在执行 `git diff --check`、`git diff --stat` 或阅读 diff 时，如发现本任务之外、但看起来合理且有业务逻辑的修改，应视为开发者正在进行的工作；不得擅自修改、格式化、回退或删除。如确实阻碍当前任务，应先向用户说明冲突并请求处理方向。
17. 禁止在 Page 的 Child 上使用 ScrollViewer，ModernFrame 已自带 ScrollViewer

### 测试策略：最小化永久测试资产，保留充分的开发验证

**测试可以大量临时执行，但永久测试应当尽可能少。** 本项目不采用测试驱动开发，不要求 AI 在每次功能修改后扩充永久单元测试套件。测试保护稳定业务行为、数据契约和高风险回归，优先断言 externally observable behavior，不冻结 implementation detail。

#### 临时验证与永久测试

开发中可以编写临时单元/集成测试、一次性验证程序或脚本，也可以运行现有测试，执行构建、启动和实际功能验证，通过日志、断点检查运行行为。充分验证是必要工作，但验证代码不默认成为仓库资产。

一般功能开发、UI 调整、ViewModel 改动和普通 Bug 修复，默认不增加永久测试文件，优先复用现有测试或采用临时验证。不创建新的测试工程、测试基础设施或额外门禁，不为每个类、方法、Command、属性配套测试，不以新增测试数量证明工作质量。

一次性验证必须先实际运行、确认结果并修复发现的问题，再删除临时测试代码及其产物。任务结束前清理临时脚本、验证工程、调试代码和测试产物；不得仅因测试是本次任务编写的，就默认保留或提交。不要删除用户文件、正式发布产物或无法确认归属的文件。

#### 永久保留的门槛

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

#### 清理已有测试

清理以降低长期维护负担为目标，不追求删除数量，也不只重组文件。逐项区分核心回归、一次性开发残留、重复契约、实现细节和特殊环境验证：无长期价值的直接删除；重要但膨胀的合并、简化并保留有效行为断言；需要设备、原生组件、大型模型或复杂外部环境的，评估改为显式诊断工具或可选集成验证。

不得为了补偿删除测试，再新增同等数量的测试、Mock、Fixture 或基础设施。不得通过跳过测试、移除有效断言或放宽构建标准使任务显示完成。已有低价值门禁失败时应删除或简化该门禁，不得为它回滚合理业务实现。

时间相关验证优先使用现有同步信号、可控时钟或有期限的状态等待，不靠延长 Sleep/Delay 解决脆弱性。模拟取消状态的可取消无限等待不等同于固定 Sleep。

WPF/Dispatcher 测试必须使用 `neo_bpsys_wpf.Tests.Infrastructure.WpfTestThread`，不要复制手写 `new Thread(...)`、裸 `thread.Join()` 或 `new Thread(async () => ...)`；相关超时规律见 `docs/build/testing-guidelines.md`。

#### 执行验证与报告

减少永久测试不降低验证要求：按改动选择合适的现有测试、临时程序、构建或功能验证，对新代码实际运行验证；失败后修复并再次验证。临时测试删除前必须实际执行。

最终报告说明执行的验证、结果及清理掉的临时资产。无法执行的验证必须说明原因和剩余风险，不得把未运行描述为通过。依赖缺失不得空执行后报告成功；可选验证要明确报告未执行，显式启用后的依赖缺失应准确报告失败。

Plan 阶段只调查并制定计划，不删除测试，不为证明删除建议而创建永久测试。报告列出建议删除、精简、保留或转为可选的项目、业务依据、风险和预期维护成本变化；用户明确授权的规范文档更新可单独执行。

## 命名规则：请勿使用通用的 IsActive

`IsActive` 专用于内部框架/运行时的激活语义，尤其是 CommunityToolkit.Mvvm 中的 `ObservableRecipient.IsActive`。

请勿将 `IsActive` 用于布局/包/设置/业务数据。

请使用明确的名称：
- `IsActivePackage`
- `IsCurrentPackage`
- `IsVisible`
- `IsBadgeVisible`
- `IsEnabled`
- `IsSelected`
- `IsExpanded`
- `IsVisibleInFrontManage`

遗留说明：
旧版 `.bpui` 包的 `TextSettings` 中可能包含 `IsActive`，这是因为旧版设置类继承了 `ObservableRecipient`。
该字段属于序列化泄漏，`LegacyConverter` 必须将其忽略。

可见性绑定必须使用 `IsVisible` 或特定的可见性相关属性。请勿将 `Visibility` 绑定到通用的 `IsActive`。

## 改动前必读

| 改动类型 | 先读 |
| --- | --- |
| UI 状态、对局数据、前台绑定传播 | `docs/architecture/shared-data-and-state.md` |
| 前台窗口设计者模式、v3 布局配置、独立编辑器、`.bpui` 迁移设计 | `docs/frontend/fronted-designer-v3.md`、`docs/frontend/fronted-designer-editor.md` |
| async、下载回调、OCR 后台任务、UI 更新 | `docs/architecture/threading-dispatcher-and-async.md` |
| 图片、字体、resx、本地化、默认布局资源 | `docs/resources/resources-localization-and-assets.md` |
| TODO、当前能力边界、不要误判的路线图 | `docs/known-limitations-and-roadmap.md` |
| 本地化、用户文本、resx 更新 | `docs/resources/resources-localization-and-assets.md` |

## 文档规则

架构、路径、插件生命周期、SmartBP/OCR、构建发布等发生变化时，同步更新 `/docs`。内部开发文档默认使用中文，除非用户明确要求其他语言。

## 构建与测试

前端依赖管理统一使用 `pnpm`。**禁止使用 npm**（包括 `npm install`、`npm ci`、`npm run` 和提交 `package-lock.json`）；前端项目必须提交并使用 `pnpm-lock.yaml`，构建脚本应调用 `pnpm install --frozen-lockfile` 与 `pnpm run`。

常用命令：

```powershell
dotnet publish .\neo-bpsys-wpf\neo-bpsys-wpf.csproj -c Release -o .\build\neo-bpsys-wpf
dotnet test .\neo-bpsys-wpf.Tests\neo-bpsys-wpf.Tests.csproj
.\build.ps1
```

文档-only 改动通常不需要完整 build，但提交前至少运行：

```powershell
git diff --check
git diff --stat
```

解决方案文件是 `neo-bpsys-wpf.slnx`

更多内部说明见 `/docs/README.md`，尤其是 `/docs/known-limitations-and-roadmap.md`。

补充规则：前台行为过滤使用显式事件 payload 和稳定控件身份，不提供临时行为标签；后台引导高亮变化与清除事件不暴露给前台行为触发器，使用 `Guidance.StepChanged`。内置布局是 `PackageId = builtin` 的普通活动包，package manager 模式不得回退旧用户布局存储；切换包需完整重载已创建 v3 窗口，`AllowsTransparency` 变化时仅静默重启受影响窗口。
