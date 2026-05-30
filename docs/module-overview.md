# 模块总览

## 解决方案项目

| 项目 | 职责 |
| --- | --- |
| `neo-bpsys-wpf` | 主 WPF 宿主。包含后台页面、前台窗口、业务服务、资源、主题、本地化和启动逻辑 |
| `neo-bpsys-wpf.Core` | 核心抽象、模型、枚举、特性、注册扩展、控件基类和辅助类。插件也依赖这里的公共 API |
| `neo-bpsys-wpf.PluginSdk` | 插件开发 SDK 和 MSBuild 打包目标。项目引用它后可使用 Core API 并创建插件 zip |
| `neo-bpsys-wpf.ExamplePlugin` | 示例插件，展示后台页面、前台窗口、注入控件、自定义服务和插件配置 |
| `Built-inPlugins/neo-bpsys-wpf.TeamJsonMaker` | 内置插件，提供队伍 JSON 制作工具后台页面 |
| `neo-bpsys-wpf.Tests` | xUnit 测试项目。当前 SmartBP 测试多为注释中的手工调试样例 |
| `neo-bpsys-wpf.CropDebugger` | 独立 WPF 调试工具，从命名看用于裁剪/区域调试 |
| `neo-bpsys-wpf.DocsGenerator` | 文档生成辅助项目；当前不属于本内部 `/docs` 的生成链 |

## 主应用目录

| 目录 | 说明 |
| --- | --- |
| `Views/Pages` | 后台页面，通常配套 `ViewModels/Pages` |
| `Views/Windows` | 主窗口和前台窗口，前台窗口一般继承 Core 中的 `FrontedWindowBase` |
| `ViewModels` | 页面/窗口 ViewModel，集中处理命令、绑定状态和服务调用 |
| `Services` | 宿主业务服务，例如插件、前台窗口、OCR、SmartBP、设置、共享数据 |
| `Controls` | 宿主专用 WPF 控件与样式 |
| `Resources` | 输出资源，包含角色/地图图片、默认前台布局、SmartBP 默认配置、`GameRule.json` 等 |
| `Locales` | `WPFLocalizeExtension` 使用的 resx 本地化资源 |
| `Themes` / `Styles` | WPF 资源字典、主题图标和样式 |
| `Helpers` / `Converters` | 宿主侧辅助逻辑与 XAML 转换器 |

## Core 目录

| 目录 | 说明 |
| --- | --- |
| `Abstractions` | `PluginBase`、`ViewModelBase`、服务接口 |
| `Attributes` | `BackendPageInfo`、`FrontedWindowInfo` |
| `Extensions/Registry` | `AddBackendPage`、`AddFrontedWindow` 注册扩展 |
| `Services/Registry` | 后台页面和前台窗口的静态注册表 |
| `Models` | `Settings`、`Game`、`Team`、`Character`、插件模型、SmartBP 区域模型等 |
| `Controls` | `FrontedWindowBase`、设计器相关 adorner |
| `Helpers` | 前台窗口 GUID、配置文件、图片、字体等工具 |

## 修改入口建议

| 要改什么 | 优先看哪里 |
| --- | --- |
| 新增后台页面 | 页面类上的 `BackendPageInfo`，然后在 `App.Services.xaml.cs` 或插件 `Initialize` 中 `AddBackendPage<TView,TViewModel>()` |
| 新增前台窗口 | 窗口类上的 `FrontedWindowInfo`，继承 `FrontedWindowBase`，然后 `AddFrontedWindow<TView,TViewModel>()` |
| 改前台布局保存/恢复 | `FrontedWindowService`、`Resources/FrontedDefaultPositions`、插件的 `FrontedDefaultPositions` |
| 改引导式 BP 流程 | `GameGuidanceService` 和 `GameRule.json` |
| 改 SmartBP/OCR | `SmartBpService`、`OcrService`、`SmartBpRegionConfigService`、`SmartBpGameDataSceneDefinition` |
| 改插件加载 | `PluginService`、`PluginPageViewModel`、`PluginMarketService`、Core 插件模型 |
| 改构建/发布 | `neo-bpsys-wpf.csproj`、`build*.ps1`、`Installer/build_Installer.iss`、`PluginSdk.targets` |

维护原则：先沿用现有注册扩展和服务抽象，不要在页面、窗口或插件中手动 `new` 一套并绕开 DI。

## 主要服务速查

| 服务 | 职责 | 注意 |
| --- | --- | --- |
| `SharedDataService` | 当前对局、主客队、角色字典、Ban 位、倒计时、BO3/BO5、地图 V2 状态 | 不要在页面 ViewModel 中复制第二份比赛状态 |
| `SettingsHostService` | `Config.json` 读写、窗口设置重置、语言设置事件 | 保存时会处理 `%APPDATA%` 路径替换 |
| `FrontedWindowService` | 前台窗口注册、显示隐藏、布局保存恢复、注入控件、全局比分控件 | 前台窗口不要绕开它直接生命周期管理 |
| `GameGuidanceService` | 根据 `GameRule.json` 推进引导式 BP、导航页面、启动计时器和发送高亮消息 | 自由赛当前不支持引导 |
| `SmartBpService` | 窗口捕获帧裁切、OCR 识别赛后数据、写回 `CurrentGame` | 全流程自动 BP 仍是 TODO |
| `OcrService` | PaddleOCR 模型下载、删除、切换、推理和失败重建 | 受 `_ocrLock` 和 `_downloadLock` 保护 |
| `PluginService` | 启动时扫描、校验、加载插件并调用 `Initialize` | 不支持运行时热加载假设 |
| `PluginMarketService` | 市场索引、README、镜像、下载队列、SHA-256 校验 | UI 集合更新必须回到 Dispatcher |
| `WindowCaptureService` | WGC/BitBlt 窗口捕获、帧缓存、预览窗口 | 帧对象跨线程读取依赖锁和 `Freeze()` |
| `SmartBpRegionConfigService` | SmartBP GameData 区域配置读写、导入导出、校验和默认配置 | 配置路径在 AppData 的 `SmartBp` 子目录 |

这些服务是模块边界。新增功能应优先组合它们，而不是直接操作窗口、文件、共享集合或插件目录。
