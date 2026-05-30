# neo-bpsys-wpf 内部开发文档

本目录是 `PLFJY/neo-bpsys-wpf` 的内部开发文档，面向维护者、插件作者和 AI coding agent。它不替代公开的 VuePress 用户文档；公开文档主要解释“怎么使用”，本目录主要解释“代码为什么这样组织、改哪里、改动时注意什么”。

建议先读：

| 文档 | 内容 |
| --- | --- |
| [project-positioning.md](project-positioning.md) | 项目定位、社区名称、后台/前台术语 |
| [runtime-architecture.md](runtime-architecture.md) | 启动流程、Generic Host、DI、日志、插件初始化时机 |
| [module-overview.md](module-overview.md) | 解决方案内各项目和目录的职责 |
| [frontend-windows-and-layout.md](frontend-windows-and-layout.md) | 前台窗口、OBS 捕获、布局保存/恢复、插件注入控件 |
| [backend-pages-and-navigation.md](backend-pages-and-navigation.md) | 后台页面注册、WPF-UI 导航约定 |
| [game-guidance.md](game-guidance.md) | 引导式 BP 的规则文件和工作流 |
| [smartbp-and-ocr.md](smartbp-and-ocr.md) | SmartBP、赛后数据 OCR、模型和区域配置 |
| [shared-data-and-state.md](shared-data-and-state.md) | 共享状态、CurrentGame、队伍、Ban、倒计时和前台绑定 |
| [plugin-system.md](plugin-system.md) | 插件生命周期、能力、安全边界、打包 |
| [plugin-market.md](plugin-market.md) | 插件市场、镜像、下载队列、SHA-256 校验 |
| [settings-paths-and-data.md](settings-paths-and-data.md) | AppData、Documents 输出、设置模型 |
| [resources-localization-and-assets.md](resources-localization-and-assets.md) | Resources、Assets、字体、resx、本地化和素材添加 |
| [threading-dispatcher-and-async.md](threading-dispatcher-and-async.md) | WPF UI 线程、Dispatcher、下载/OCR/捕获回调 |
| [updater-and-downloads.md](updater-and-downloads.md) | 应用更新、镜像、安装包校验、三类下载差异 |
| [wpf-ui-pitfalls.md](wpf-ui-pitfalls.md) | WPF-UI、DI、i18n、图标、资源和透明窗口坑点 |
| [build-release-and-versioning.md](build-release-and-versioning.md) | 构建、安装包、版本号、配置 |
| [testing-and-debugging.md](testing-and-debugging.md) | 测试现状、日志、SmartBP/OCR/插件调试 |
| [known-limitations-and-roadmap.md](known-limitations-and-roadmap.md) | 已知边界、TODO、不要误判的路线图提醒 |

按主题快速阅读：

| 主题 | 建议文档 |
| --- | --- |
| 架构入门 | `project-positioning.md`、`runtime-architecture.md`、`module-overview.md` |
| UI / 前台 / 后台 | `frontend-windows-and-layout.md`、`backend-pages-and-navigation.md`、`wpf-ui-pitfalls.md` |
| 业务流程 | `game-guidance.md`、`smartbp-and-ocr.md`、`shared-data-and-state.md` |
| 插件 | `plugin-system.md`、`plugin-market.md` |
| 资源 / 本地化 | `resources-localization-and-assets.md`、`settings-paths-and-data.md` |
| 构建 / 更新 / 调试 | `build-release-and-versioning.md`、`updater-and-downloads.md`、`testing-and-debugging.md` |

阅读方式：

1. 先确认术语。此项目在社区中常称为“第五人格 BP 展示工具”，但架构上也可以理解为非官方第五人格赛事的直播导播辅助系统。
2. 再按改动区域查文档。改前台窗口看 `frontend-windows-and-layout.md`，改插件看 `plugin-system.md` 和 `plugin-market.md`。
3. 遇到文档和代码不一致时，以代码为准，并在同一提交中修正文档。

文档中的判断尽量来自当前代码。标注“推断”的内容表示它是从代码结构、注释或调用关系得出的维护建议，不应被当成外部权威规则。
