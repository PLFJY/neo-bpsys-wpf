# AGENTS.md

本仓库是 `neo-bpsys-wpf`，社区称“第五人格 BP 展示工具”，面向非官方第五人格赛事的 WPF 直播导播辅助系统。

## 定位与术语

- 后台 / backend 是导播控制 UI，不是服务端；前台窗口 / FrontedWindow 是供 OBS 捕获的独立 WPF 输出窗口，不是 Web frontend。
- 技术栈为 WPF + Generic Host + DI + CommunityToolkit.Mvvm；沿用现有服务、注册扩展与 MVVM 边界，不另造架构或随意大规模重构。
- SmartBP 包含赛后 OCR 和可选的 BP 状态识别、应用及引导追赶；全流程自动导播与自动切屏尚未实现。具体边界见[能力清单](docs/known-limitations-and-roadmap.md)。
- 插件 `manifest.yml.apiVersion` 是宿主兼容契约；SDK 源码引用提交是编译/打包来源，两者不同。v3 使用源码 ProjectReference，不再发布 SDK NuGet 包。

## 开发约束

1. 改动前先读对应源码和接口，再按下表选一份主文档；不要把旧文档或历史方案当作当前实现。
2. 页面、窗口、控件和服务通过现有 DI 扩展注册。后台使用 `AddBackendPage<TView,TViewModel>()`；前台注册与控件扩展分别见[窗口运行时](docs/frontend/frontend-windows-and-layout.md)和[插件 SDK](docs/plugin/plugin-development.md)。
3. WPF `Page` 只能由 Window、Frame 或正确的导航宿主承载；禁止直接放入 ContentControl、Border、Grid、TabItem 或插件/模块嵌入 host。嵌入片段用 UserControl/Control，否则会触发 `Page can have only Window or Frame as parent.`。Page 子内容不要另包 ScrollViewer，ModernFrame 已提供滚动宿主。
4. **保持既有行为和数据契约自然可运行。** 新功能增量接入：旧字段、字面量和流程保持原语义，只有使用新接入点、字段或模式才启用新规则。未经明确指定迁移策略，不得通过读取期补字段、转换器、运行时猜测、fallback、手工补默认值或重写持久化来掩盖新旧语义冲突。模式差异用显式字段、连接或版本契约表达，并在实际使用新能力时准确校验。
5. 用户可见文本使用 WPFLocalizeExtension 与对应 Locales/*.resx，遵循[本地化契约](docs/resources/localization.md)。禁止用 Phase 等内部开发阶段代号作 UI 占位；未实现能力写“将在后续版本中提供”。真实预览标签如 `[Text]` 不受此限。大部分按钮应有 WPF-UI 图标。
6. `IsActive` 仅用于框架/运行时激活语义；业务字段用 IsVisible、IsEnabled、IsSelected、IsActivePackage 等明确名称，Visibility 不得绑定泛名 IsActive。详见[命名规范](docs/naming-rules.md)。
7. 所有公共属性、公共方法须有 XML 注释，按适用情况说明参数、返回值及异常。
8. UI 更新遵守 Dispatcher 规则；插件在 Host build 前注入 DI，安装/更新通常需重启。插件是全信任代码，审核、微步云扫描和人工审查构成风险控制，不是沙箱。

## 工作区与 Git 安全

- **未经用户明确同意，只执行只读 Git 命令**，如 status、diff、log、show、ls-remote。禁止擅自 stash/pop/drop、switch、checkout、restore、reset、clean、merge、rebase、提交或推送。不借文档中的开发者命令示例取得授权。
- 不用 stash 做前后对比；无 initial commit 时尤其可能搅乱大量未跟踪文件。用文件内容或只读 diff 比较。
- 先记录工作区状态。阅读 diff、执行 diff --check / --stat 时，任务外合理修改视为开发者工作，不得格式化、覆盖、回退或删除；若阻碍任务，说明冲突并询问方向。

## 验证与测试资产

默认不为普通功能、UI、ViewModel 或 Bug Fix 新增永久测试、测试工程、基础设施或门禁；优先现有测试、构建和临时验证。永久测试仅用于编译器及现有测试难以发现的高风险业务、数据安全、格式、ABI 或兼容回归，须说明独立长期价值。

禁止源码字符串/Regex 检查、私有实现断言、视觉排版冻结及 trivial 属性/命令存在性等低价值永久测试。临时验证必须先实际运行、修复问题并确认结果，再清理自身代码和产物；不删用户文件或正式产物。真实报告已运行、失败、未执行及依赖缺失，不通过跳过有效断言或放宽标准制造成功。详细准入、清理及 WPF/Dispatcher 测试要求统一见[测试策略](docs/build/testing-guidelines.md)。

## 按任务阅读

先定位源码，再选主文档；格式、ABI、迁移和安全规则按需追加，不要求整组通读。完整索引见 [docs/README.md](docs/README.md)。

| 改动 | 主文档 |
| --- | --- |
| 共享比赛状态 / 前台绑定传播 | [共享数据](docs/architecture/shared-data-and-state.md) |
| async / OCR 后台任务 / UI 更新 | [线程与异步](docs/architecture/threading-dispatcher-and-async.md) |
| 前台创建、显示、重载 | [窗口运行时](docs/frontend/frontend-windows-and-layout.md) |
| v3 JSON、路径、布局读取 | [Window-centric 契约](docs/frontend/fronted-layout-v3-window-centric.md) |
| 控件渲染 / 编辑器 | [控件运行时](docs/frontend/fronted-designer-v3.md) / [编辑器](docs/frontend/fronted-designer-editor.md) |
| BPUI / 历史迁移 | [包格式](docs/frontend/bpui-package-v3.md) / [迁移规则](docs/frontend/legacy-fronted-layout-migration.md) |
| 插件作者 / 宿主加载 | [SDK](docs/plugin/plugin-development.md) / [插件系统](docs/plugin/plugin-system.md) |
| 图片、字体 / 本地化 / 设置路径 | [素材](docs/resources/resources-localization-and-assets.md) / [本地化](docs/resources/localization.md) / [用户数据](docs/resources/settings-paths-and-data.md) |
| SmartBP / 比分 / 后台组件 | 从[模块索引](docs/README.md)选择对应文档 |

## 构建入口

解决方案：`neo-bpsys-wpf.slnx`。构建发布见[构建与版本](docs/build/build-release-and-versioning.md)，实际测试 runner 与环境诊断见[验证与调试](docs/build/testing-and-debugging.md)。

前端依赖统一使用 pnpm，提交 pnpm-lock.yaml；禁止 npm（包括 install、ci、run）和 package-lock.json。构建调用 `pnpm install --frozen-lockfile` 与 `pnpm run`。

```powershell
dotnet publish .\neo-bpsys-wpf\neo-bpsys-wpf.csproj -c Release -o .\build\neo-bpsys-wpf
.\build.ps1
git diff --check
git diff --stat
```

文档-only 通常不需要完整 .NET 构建/测试，但应检查 diff 与内部相对链接。

## 文档维护

内部文档默认中文。行为、架构或契约变化时只更新受影响的权威文档；不跨文件复制规范。已有模块优先补现有文档，新文档须有独立长期用途。不默认为 Bug Fix 创建报告，不在活跃文档保留已完成计划、修复流水账或重复 PR/commit 记录；过时内容直接更新或删除。源码链接须可用，不堆积 Agent 调查与验收记录。
