# 内部开发文档导航

本目录面向维护者与 Coding Agent，记录当前实现、稳定契约和代码入口；普通用户说明见根 README。术语和全局安全约束以 [AGENTS.md](../AGENTS.md) 为准。

## 按需读取

1. 先读 AGENTS，再定位任务对应的源码与接口；不预读整个 docs。
2. 从下表选择该模块的一份主维护文档。
3. 触及格式、公开 ABI、迁移、持久化或安全时，再读对应规范。
4. 源码 Tour、历史审计表和专项诊断仅按需查。文档与代码不一致时检查合法兼容路径，不能猜测后删除旧契约。

## 架构与业务

| 工作范围 | 主文档 / 职责 |
| --- | --- |
| 项目与源码定位 | [模块地图](architecture/module-overview.md)；[项目定位与术语](architecture/project-positioning.md)供初次了解 |
| Host / DI / 启停 / 日志 | [运行时架构](architecture/runtime-architecture.md) |
| CurrentGame / Team / Ban / Pick / 状态传播 | [共享数据与状态流](architecture/shared-data-and-state.md)，含数据流图与引用替换 |
| Dispatcher / 下载回调 / 捕获 / 异步清理 | [线程与异步](architecture/threading-dispatcher-and-async.md) |
| 远程公告 | [公告同步与缓存](architecture/remote-announcements.md) |
| SmartBP / OCR / Provider / 追赶与应用 | [SmartBP](business/smartbp-and-ocr.md) |
| 比分计算 / Free 隔离 / 旧记录兼容 | [ScoreV2](business/score-system-v2.md) |
| 当前能力与真实缺口 | [已知边界](known-limitations-and-roadmap.md) |

## 前台与 Designer

各文档负责不同契约，不要求成组通读。

| 工作范围 | 权威文档 |
| --- | --- |
| 注册 / canonical identity / 创建显示 / 重载关闭 | [前台窗口运行时](frontend/frontend-windows-and-layout.md) |
| WindowConfig / BO state / 路径与活动包读写 | [Window-centric 布局](frontend/fronted-layout-v3-window-centric.md) |
| 渲染所有权 / 控件名称与 GUID / 内置控件 / 绑定 | [V3 控件运行时](frontend/fronted-designer-v3.md) |
| 选择缩放 / Property Grid / Browser / Undo / 保存安全与性能 | [独立编辑器](frontend/fronted-designer-editor.md) |
| 触发事件 / 图执行 / Loop / Transition / 动画持久化 | [Behavior](frontend/fronted-behavior-system.md) |
| manifest / 包身份 / URI / 依赖 / ZIP 安全 / 导入导出 | [BPUI 包规范](frontend/bpui-package-v3.md) |
| 旧 Config 与 BPUI 转换 / 字段映射 / 启动迁移 | [Legacy 迁移](frontend/legacy-fronted-layout-migration.md) |

## 后台、插件与资源

| 工作范围 | 主文档 / 职责 |
| --- | --- |
| 后台注册与宿主入口 | [后台页面与导航](backend/backend-pages-and-navigation.md) |
| Modern 菜单与 LocalTabs | [ModernNavigationView](backend/modern-navigation-view.md) |
| Page 承载 / journal / 转场 / 默认滚动宿主 | [ModernFrame](backend/modern-frame.md) |
| 滚轮 / 触摸 / Self ownership / Guidance 目标滚动 | [平滑滚动](backend/modern-smooth-scrolling.md) |
| Classic Shell / 共享 VM / 弹窗生命周期 | [Classic](backend/classic-mode.md) |
| BP 规则 / 步骤推进 / Guidance 事件 | [GameGuidance](backend/game-guidance.md) |
| 教学包 / Flow / Signal / 状态版本 | [ProductTour](backend/product-tour-and-onboarding.md)，与比赛推进服务分开 |
| 插件作者公开 API / 示例 / ABI 兼容 | [Plugin SDK](plugin/plugin-development.md) |
| 插件宿主发现、加载、DI 与信任模型 | [插件系统](plugin/plugin-system.md) |
| 市场索引、镜像、队列与安装消费 | [插件市场](plugin/plugin-market.md) |
| Web sidecar 协议与资产安全 | [WebRenderer 架构](plugin/web-renderer-experimental.md)；构建运行见[插件 README](../Built-inPlugins/neo-bpsys-wpf.WebRenderer/README.md) |
| resx / 字典 / i18n 调用 / 文化回退 | [本地化](resources/localization.md) |
| 图片 / 字体 / 输出文件与缓存 | [素材资源](resources/resources-localization-and-assets.md) |
| Settings / AppData / 模块路径与迁移 | [设置与用户数据](resources/settings-paths-and-data.md) |
| 布尔状态、游戏术语与代码命名 | [命名规范](naming-rules.md) |
| 跨模块 WPF 故障模式 | [WPF 陷阱](wpf-ui-pitfalls.md) |

## 构建与验证

| 需要做什么 | 文档 |
| --- | --- |
| 构建产物 / 发布 / 版本体系 | [构建与版本](build/build-release-and-versioning.md) |
| 永久测试准入、临时验证清理、WPF 测试约束 | [测试资产政策](build/testing-guidelines.md) |
| runner 命令 / 日志 / native 依赖 / 实际调试 | [验证与调试](build/testing-and-debugging.md) |
| 更新、下载、校验和重启机制 | [Updater](build/updater-and-downloads.md) |
| 维护者手动分支工作流 / 提交格式 | [仓库管理](build/repository-management.md) / [Commit 约定](build/commit-convention.md)；不授予 Agent Git 写操作许可 |
| 异常退出 / 日志 / WER / 私密 Dump | [故障收集](diagnostics/crash-collection.md) |

## 仅按需参考

- [前台源码阅读地图](frontend/fronted-window-system-deep-dive.md)：沿调用链定位，不是第二份运行时规格。
- [历史控件蓝图映射](frontend/reference/legacy-v3-control-blueprint-map.md)：固定旧 release 的命名/属性差异证据，仅修改迁移时查阅。
- [内存与生命周期诊断](build/memory-baseline-and-diagnostics.md)：复现场景和分进程采集，不是待完成阶段计划。
- 内置插件 [TeamJsonMaker](../Built-inPlugins/neo-bpsys-wpf.TeamJsonMaker/README.md)、[GameStopwatch](../Built-inPlugins/neo-bpsys-wpf.GameStopwatch/README.md)、[FocusKeeper](../Built-inPlugins/neo-bpsys-wpf.FocusKeeper/README.md) 保留各自独立数据或原生生命周期说明。

## 维护方式

只更新实际受影响的权威文档；公共 API、格式和兼容规则不能为精简丢失。已有模块优先补原文，新文档须有独立长期用途。同一规范用链接引用，不跨文件复制；源码链接必须有效。当前行为取代过时说明，不无限追加“新版补充”。完成的计划、Bug Fix 流程与验收结果留在 PR/commit/对话，不默认新建报告，也不把 Agent 调查记录存入仓库。
