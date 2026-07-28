# 模块总览

## 解决方案项目

| 项目 | 职责 |
| --- | --- |
| `neo-bpsys-wpf` | 主 WPF 宿主。包含后台页面、前台窗口、业务服务、资源、主题、本地化和启动逻辑 |
| `neo-bpsys-wpf.ProductTour` | 教程与导览 WPF 控件库。包含首次 Welcome、Dialogue、Product Tour overlay、教程注册表、状态和 Signal 服务 |
| `neo-bpsys-wpf.SmartBp.Module` | SmartBP 运行时模块。包含真实 SmartBP 页面内容、ViewModel、OCR/SmartBP 服务、OpenCvSharp/PaddleOCR/PaddleInference 使用和 OCR 模型下载逻辑 |
| `neo-bpsys-wpf.Core` | 核心抽象、模型、枚举、特性、注册扩展、控件基类和辅助类。插件也依赖这里的公共 API |
| `neo-bpsys-wpf.PluginSdk` | 插件开发 SDK 和 MSBuild 打包目标。项目引用它后可使用 Core API 并创建插件 zip |
| `neo-bpsys-wpf.ExamplePlugin` | 示例插件，展示后台页面、插件 XAML 前台窗口、自定义服务和插件配置 |
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
| `Services` | 宿主业务服务，例如插件、前台窗口、SmartBP 模块加载、设置、共享数据 |
| `Controls` | 宿主专用 WPF 控件与样式 |
| `Resources` | 输出资源，包含角色/地图图片、默认前台布局、SmartBP 默认配置、`GameRule.json` 等 |
| `Locales` | `WPFLocalizeExtension` 使用的 resx 本地化资源 |
| `Themes` / `Styles` | WPF 资源字典、主题图标和样式 |
| `Helpers` / `Converters` | 宿主侧辅助逻辑与 XAML 转换器 |

## Core 目录

| 目录 | 说明 |
| --- | --- |
| `Abstractions` | `PluginBase`、`ViewModelBase`、服务接口 |
| `Attributes` | `BackendPageInfo`、内置前台窗口使用的 `FrontedWindowInfo` |
| `Extensions/Registry` | `AddBackendPage`、内置与插件共用的 `AddFrontedWindow<TWindow,TViewModel>()`（XAML 窗口）和 `AddFrontedV3LayoutWindow("WindowId", isBuiltIn:)`（v3 布局窗口）注册扩展 |
| `Services/Registry` | 后台页面静态注册表和前台窗口 registration registry（`IFrontedWindowRegistry`，提供 `GetWindows()`、`GetManageableWindows()`、`GetV3LayoutWindows()`、`TryGet()`） |
| `Models` | `Settings`、`Game`、`Team`、`Character`、插件模型、SmartBP 区域模型等 |
| `Controls` | `FrontedWindowBase`、设计器相关 adorner |
| `Helpers` | 前台窗口 GUID、配置文件、图片、字体等工具 |

## 修改入口建议

| 要改什么 | 优先看哪里 |
| --- | --- |
| 新增后台页面 | 页面类上的 `BackendPageInfo`，然后在 `App.Services.xaml.cs` 或插件 `Initialize` 中 `AddBackendPage<TView,TViewModel>()` |
| 新增内置前台窗口 | 窗口类上的 `FrontedWindowInfo`，继承 `FrontedWindowBase`，然后 `AddFrontedWindow<TView,TViewModel>()` |
| 新增插件前台窗口 | XAML 窗口：`[FrontedWindowInfo("GUID", "DisplayName", IsBuiltIn = false)]` + `services.AddFrontedWindow<TWindow,TViewModel>()`；v3 布局窗口：`services.AddFrontedV3LayoutWindow("WindowId", isBuiltIn: false)`（PackageId 由宿主自动注入） |
| 改前台布局保存/恢复 | `FrontedLayoutService`、`FrontedUserLayoutStore`、`FrontedWindowLayoutOptionsService`、`Resources/FrontedLayouts` |
| 改引导式 BP 流程 | `GameGuidanceService` 和 `GameRule.json` |
| 改 SmartBP/OCR 运行时 | `neo-bpsys-wpf.SmartBp.Module` 中的 `SmartBpService`、`OcrService`、`GameDataTableOcrParser` |
| 改 SmartBP 宿主安装/加载 | `neo-bpsys-wpf/Services/SmartBpModule`、`SmartBpPageViewModel`、`SmartBpPage.xaml` |
| 改插件加载 | `PluginService`、`PluginPageViewModel`、`PluginMarketService`、Core 插件模型 |
| 改构建/发布 | `neo-bpsys-wpf.csproj`、`build*.ps1`、`Installer/build_Installer.iss`、`PluginSdk.targets` |
| 改首次导览或页面教程 | `neo-bpsys-wpf.ProductTour`、`neo-bpsys-wpf/Tutorial`、`docs/backend/product-tour-and-onboarding.md` |

维护原则：先沿用现有注册扩展和服务抽象，不要在页面、窗口或插件中手动 `new` 一套并绕开 DI。

## 主要服务速查

| 服务 | 职责 | 注意 |
| --- | --- | --- |
| `SharedDataService` | 当前对局、主客队、角色字典、Ban 位、倒计时、BO3/BO5、地图 V2 状态 | 不要在页面 ViewModel 中复制第二份比赛状态 |
| `SettingsHostService` | `Config.json` 读写、窗口设置重置、语言设置事件 | 保存时会处理 `%APPDATA%` 路径替换 |
| `FrontedWindowService` | 前台窗口注册、显示隐藏、v3 布局重载、插件窗口管理、全局比分兼容适配 | 前台窗口不要绕开它直接生命周期管理 |
| `GameGuidanceService` | 根据 `GameRule.json` 推进引导式 BP、导航页面、启动计时器和发送高亮消息 | 自由赛当前不支持引导 |
| `TutorialService` | 运行页面教程包和总导览 flow，记录 Completed / Skipped / CoveredByFlow 状态 | 不替代 `GameGuidanceService`，flow 内部应引用 package |
| `TutorialSignalService` | 在业务动作和交互式教程步骤之间传递 signal | 教程不应直接读取业务对象内部状态来判断用户动作 |
| `SmartBpModuleManager` | SmartBP 模块目录校验、zip 导入、Release manifest 检查、动态加载、状态写入和旧 OCR 模型迁移 | Release 使用当前 app tag 的 manifest，不查询 latest release；模块 native 解析会显式包含模块自带的 CPU Paddle runtime 目录 |
| `SmartBpService` | 模块内服务，对完整捕获帧 OCR 并按文本坐标重建赛后数据、写回 `CurrentGame` | 全流程自动 BP 仍是 TODO |
| `OcrService` / `PaddleRuntime` | 模块内服务，PaddleOCR 模型下载、删除、切换、推理和失败重建；Paddle CPU/CUDA native runtime 由模块管理，CUDA Toolkit 是系统级 NVIDIA 前置条件 | CPU 固定随模块发布于 `Runtime/Paddle/cpu/`；启用 CUDA 时模块下载 Paddle native 包，并通过 UAC 打开 NVIDIA CUDA 11.8 图形安装程序、等待用户完成安装，再以隐藏的提权 PowerShell 把已静态链接 zlib 的 cuDNN 8.9.6 DLL 安装到 CUDA 的系统 `bin`、写入明确版本标记并确保该目录在系统 PATH；CUDA/cuDNN 安装包缓存在模块的 `Runtime/Paddle/Downloads/NVIDIA/`，重试时先校验哈希并复用，不把 CUDA/cuDNN DLL 写入主程序目录 |
| `PluginService` | 启动时扫描、校验、加载插件并调用 `Initialize` | 不支持运行时热加载假设 |
| `PluginMarketService` | 市场索引、README、镜像、下载队列、SHA-256 校验 | UI 集合更新必须回到 Dispatcher |
| `WindowCaptureService` | WGC/BitBlt 窗口捕获、帧缓存、预览窗口 | 帧对象跨线程读取依赖锁和 `Freeze()` |

这些服务是模块边界。新增功能应优先组合它们，而不是直接操作窗口、文件、共享集合或插件目录。
