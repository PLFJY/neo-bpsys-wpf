# neo-bpsys-wpf 内部开发文档

本目录是 `PLFJY/neo-bpsys-wpf` 的内部开发文档，面向维护者、插件作者和 AI coding agent。它不替代公开的 VuePress 用户文档；公开文档主要解释“怎么使用”，本目录主要解释“代码为什么这样组织、改哪里、改动时注意什么”。

## 软件框架和技术栈

| 项 | 内容 |
| --- | --- |
| 框架 | .NET 10.0 WPF（`net10.0-windows10.0.20348`） |
| UI 库 | [WPF-UI](https://wpfui.lepo.co/) |
| IOC 容器 | [Microsoft.Extensions.DependencyInjection](https://learn.microsoft.com/zh-cn/dotnet/core/extensions/dependency-injection-usage) |
| 拼音库 | [hyjiacan.pinyin4net](https://gitee.com/hyjiacan/Pinyin4Net) |
| MVVM | [CommunityToolkit.Mvvm](https://learn.microsoft.com/zh-cn/dotnet/communitytoolkit/mvvm/) |
| 下载器 | [Downloader](https://github.com/bezzad/Downloader) |

构建与版本细节见 [build/build-release-and-versioning.md](build/build-release-and-versioning.md)。

## 开发工具

Visual Studio 2026、JetBrains Rider。

Xaml Styler 插件仅适用于 Visual Studio——Rider 整理出来的格式不一样，不方便跟踪更改。

## 文档索引

建议先读：

### 架构（architecture/）

| 文档 | 内容 |
| --- | --- |
| [project-positioning.md](architecture/project-positioning.md) | 项目定位、社区名称、后台/前台术语 |
| [runtime-architecture.md](architecture/runtime-architecture.md) | 启动流程、Generic Host、DI、日志、插件初始化时机 |
| [module-overview.md](architecture/module-overview.md) | 解决方案内各项目和目录的职责 |
| [data-flow.md](architecture/data-flow.md) | Core 模型、SharedDataService 和 WPF 绑定系统的数据流架构与示例 |
| [shared-data-and-state.md](architecture/shared-data-and-state.md) | 共享状态、CurrentGame、队伍、Ban、倒计时和前台绑定 |
| [threading-dispatcher-and-async.md](architecture/threading-dispatcher-and-async.md) | WPF UI 线程、Dispatcher、下载/OCR/捕获回调 |

### 前台窗口与布局（frontend/）

| 文档 | 内容 |
| --- | --- |
| [frontend-windows-and-layout.md](frontend/frontend-windows-and-layout.md) | 前台窗口、OBS 捕获、v3 布局、插件前台窗口 |
| [fronted-window-system-deep-dive.md](frontend/fronted-window-system-deep-dive.md) | 前台窗口启动链路、注册、创建、配置读取、配置应用与生命周期的源码级解析 |
| [fronted-layout-v3-window-centric.md](frontend/fronted-layout-v3-window-centric.md) | Fronted Layout v3 Window-centric schema、路径、BaseCanvas、behavior、package 和 legacy 转换规则 |
| [fronted-designer-v3.md](frontend/fronted-designer-v3.md) | Fronted Designer v3 的配置驱动重构设计、兼容策略和实现历史 |
| [fronted-designer-editor.md](frontend/fronted-designer-editor.md) | Designer v3 独立编辑器技术设计，包括 Window-centric 选择、JSON key = 控件名、透明 hitbox、Property Grid、Binding/Resource Browser 和保存策略 |
| [fronted-behavior-system.md](frontend/fronted-behavior-system.md) | Fronted Behavior Graph System 设计 — BehaviorGuid、window-level behaviors 文件结构、事件总线候选来源和实现说明 |
| [bpui-package-v3.md](frontend/bpui-package-v3.md) | Designer v3 `.bpui` 布局包标准，包括 manifest、资源 URI、包隔离、导入导出与包管理规格 |
| [legacy-fronted-layout-migration.md](frontend/legacy-fronted-layout-migration.md) | 旧 `Config.json` 前台字段和旧 `.bpui` 到 Designer v3 布局的迁移规则 |
| [legacy-v3-control-blueprint-map.md](frontend/legacy-v3-control-blueprint-map.md) | Legacy v3 控件蓝图映射审计，记录旧 XAML 名称到新模型的聚合状态 |

### 后台页面与导航（backend/）

| 文档 | 内容 |
| --- | --- |
| [backend-pages-and-navigation.md](backend/backend-pages-and-navigation.md) | 后台页面注册、WPF-UI 导航约定 |
| [classic-mode.md](backend/classic-mode.md) | Classic Mode 导播台定位、弹窗入口、导航边界、间距和重启规范 |
| [modern-frame.md](backend/modern-frame.md) | ModernFrame 项目本地现代内容宿主的设计与目标，不依赖 iNKORE 包 |
| [modern-navigation-view.md](backend/modern-navigation-view.md) | ModernNavigationView 作为 `MainWindow.RootNavigation` 兼容替换的定位与行为 |
| [modern-smooth-scrolling.md](backend/modern-smooth-scrolling.md) | 项目本地 Modern 平滑滚动基础设施、opt-in 边界和后续 GameGuidance 复用方式 |
| [game-guidance.md](backend/game-guidance.md) | 引导式 BP 的规则文件和工作流 |
| [product-tour-and-onboarding.md](backend/product-tour-and-onboarding.md) | 首次导览、页面教程包、Product Tour overlay、教程状态和 Signal 边界 |

### 业务流程（business/）

| 文档 | 内容 |
| --- | --- |
| [smartbp-and-ocr.md](business/smartbp-and-ocr.md) | SmartBP、赛后数据 OCR、模型和区域配置 |
| [score-system-v2.md](business/score-system-v2.md) | Score System v2 的 `Core.Models.Game` 持有比分状态、小比分（MinorScore）计算规则、前台绑定和兼容策略 |

### 插件（plugin/）

| 文档 | 内容 |
| --- | --- |
| [plugin-system.md](plugin/plugin-system.md) | 插件生命周期、能力、安全边界、打包 |
| [plugin-market.md](plugin/plugin-market.md) | 插件市场、镜像、下载队列、SHA-256 校验 |
| [plugin-development.md](plugin/plugin-development.md) | 插件开发完整指南：manifest、SDK 引用、后台页面、Designer v3 控件、前台窗口、标识模型、迁移说明 |
| [web-renderer-experimental.md](plugin/web-renderer-experimental.md) | 实验性 Web Renderer sidecar、runtime 前置条件和 IPC 边界 |

### 资源与本地化（resources/）

| 文档 | 内容 |
| --- | --- |
| [resources-localization-and-assets.md](resources/resources-localization-and-assets.md) | Resources、Assets、字体、resx、本地化和素材添加 |
| [localization.md](resources/localization.md) | 本地化资源按功能域拆分、字典选择、XAML/C# 引用模式、模块归属、文化与回退、审计方法 |
| [settings-paths-and-data.md](resources/settings-paths-and-data.md) | AppData、Documents 输出、设置模型 |

### 构建与测试（build/）

| 文档 | 内容 |
| --- | --- |
| [build-release-and-versioning.md](build/build-release-and-versioning.md) | 构建、安装包、版本号、配置 |
| [version-iteration-rules.md](build/version-iteration-rules.md) | 版本号迭代原则（首位/第二位/第三位/构建元数据的跟进规则） |
| [updater-and-downloads.md](build/updater-and-downloads.md) | 应用更新、镜像、安装包校验、三类下载差异 |
| [testing-and-debugging.md](build/testing-and-debugging.md) | 测试现状、日志、SmartBP/OCR/插件调试 |
| [testing-guidelines.md](build/testing-guidelines.md) | 单元测试边界、XAML smoke test 规则、UI 变更时如何处理脆弱测试 |
| [memory-baseline-and-diagnostics.md](build/memory-baseline-and-diagnostics.md) | 内存回归验证方案、基线测量步骤和已知引用链 |
| [commit-convention.md](build/commit-convention.md) | Commit 提交规范、类型列表（feat/fix/refactor/docs 等）、BREAKING CHANGE 用法 |
| [repository-management.md](build/repository-management.md) | 仓库分支管理流程：main/dev 分支结构、PR 合并、feature 分支、squash merge |

### 跨切关注点（根级）

| 文档 | 内容 |
| --- | --- |
| [known-limitations-and-roadmap.md](known-limitations-and-roadmap.md) | 已知边界、TODO、不要误判的路线图提醒 |
| [wpf-ui-pitfalls.md](wpf-ui-pitfalls.md) | WPF-UI、DI、i18n、图标、资源和透明窗口坑点 |
| [naming-rules.md](naming-rules.md) | 命名规范：大/小驼峰、游戏术语表、Ban 动词在前约定 |

---

## 按主题快速阅读

| 主题 | 建议文档 |
| --- | --- |
| 架构入门 | `architecture/project-positioning.md`、`architecture/runtime-architecture.md`、`architecture/module-overview.md` |
| 数据流 / 共享状态 | `architecture/data-flow.md`、`architecture/shared-data-and-state.md` |
| UI / 前台 / 后台 | `frontend/frontend-windows-and-layout.md`、`frontend/fronted-layout-v3-window-centric.md`、`frontend/fronted-designer-v3.md`、`frontend/fronted-designer-editor.md`、`frontend/fronted-behavior-system.md`、`frontend/bpui-package-v3.md`、`backend/backend-pages-and-navigation.md`、`backend/classic-mode.md`、`backend/modern-smooth-scrolling.md`、`wpf-ui-pitfalls.md` |
| 业务流程 | `backend/game-guidance.md`、`backend/product-tour-and-onboarding.md`、`business/smartbp-and-ocr.md`、`architecture/shared-data-and-state.md`、`business/score-system-v2.md` |
| 插件 | `plugin/plugin-system.md`、`plugin/plugin-market.md`、`plugin/plugin-development.md` |
| 资源 / 本地化 | `resources/resources-localization-and-assets.md`、`resources/settings-paths-and-data.md`、`frontend/bpui-package-v3.md` |
| 构建 / 打包 / 更新 / 调试 | `build/build-release-and-versioning.md`、`build/version-iteration-rules.md`、`frontend/bpui-package-v3.md`、`build/updater-and-downloads.md`、`build/testing-and-debugging.md`、`build/testing-guidelines.md` |
| 贡献流程 | `build/commit-convention.md`、`build/repository-management.md`、`naming-rules.md` |

## 阅读方式

1. 先确认术语。此项目在社区中常称为“第五人格 BP 展示工具”，但架构上也可以理解为非官方第五人格赛事的直播导播辅助系统。
2. 再按改动区域查文档。改前台窗口看 `frontend/frontend-windows-and-layout.md`，改插件看 `plugin/plugin-system.md` 和 `plugin/plugin-development.md`。
3. 遇到文档和代码不一致时，以代码为准，并在同一提交中修正文档。

文档中的判断尽量来自当前代码。标注“推断”的内容表示它是从代码结构、注释或调用关系得出的维护建议，不应被当成外部权威规则。
