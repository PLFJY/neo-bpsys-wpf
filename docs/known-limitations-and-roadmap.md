# 当前能力边界与未解决事项

本文用于快速确认“已可用什么、仍缺什么”；算法、格式和兼容规则只在模块权威文档维护，不把完成流水账或后续计划永久追加在此。

## SmartBP

赛后数据 OCR 自动回填可用。BP 状态识别使用用户显式选择的 Paddle/Tesseract/RapidOCR Provider，本地解析文字、区域和固定槽位，可选自动应用与 GameGuidance 逐步追赶；手动强制同步是另一条明确路径。默认预览，启用自动应用才写业务状态。

未实现：MapBP 识别、天赋角色操作、自动画面切换和全流程自动导播。本地视觉模型/llama.cpp 路径已移除。OCR Unknown 不等于 Empty，不按后续角色填前位；自动链路不能跨过未完成步骤或直接定位 Guidance。完整机制及历史帧补偿见 [SmartBP](business/smartbp-and-ocr.md)。真实赛事截图、缩放和 Provider 组合仍需设备与样本验证。

## 前台与编辑器

内置窗口使用 v3，独立 FrontedDesignerWindow 是唯一编辑器。布局、行为、包管理、自定义窗口、缺插件数据保留及 legacy 转换均已接入；SettingPage 旧导入导出与真实输出窗口内编辑入口已移除。详细规范分别见[窗口](frontend/frontend-windows-and-layout.md)、[布局](frontend/fronted-layout-v3-window-centric.md)、[编辑器](frontend/fronted-designer-editor.md)与 [BPUI](frontend/bpui-package-v3.md)。

当前需要明确的实现边界：

- **重复控件 JSON key 尚未在所有入口阻断。** [FrontedControlLayoutJsonConverter.Read](../neo-bpsys-wpf.Core/Models/FrontedLayout/Json/FrontedControlLayoutJsonConverter.cs) 对 Controls 字典赋值，后项覆盖前项；Designer validator 只能验证已构建的设计项，不能找回被覆盖值。旧文档所称“raw JSON 一律拒绝重复 key”不成立。修复须在原始读取阶段检查，覆盖真实输入行为。
- **不存在旧关键名称 catalog 保护。** 当前 [PropertyEditing](../neo-bpsys-wpf/ViewModels/Windows/FrontedDesignerWindowViewModel.PropertyEditing.cs) / [ControlsAndTemplates](../neo-bpsys-wpf/ViewModels/Windows/FrontedDesignerWindowViewModel.ControlsAndTemplates.cs) 依赖可编辑性及 incoming reference 检查；不能假设 SurPick 等名称被统一锁定。内置配套 BehaviorGuid 与行为引用仍须维护。
- Reset 读取对应 builtin 快照；插件或 custom 窗口没有对应内置布局时报告缺失。Save As 尚未提供。资源浏览/导入与实时输出的差别见编辑器规范。
- LayoutService 的读取回退检查反序列化失败，不等于完整语义校验；普通 v3 数据不做 legacy 猜测或读取期重写。

- **包替换非原子。** [FrontedLayoutPackageImporter.Import.cs](../neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedLayoutPackageImporter.Import.cs) 的 ImportStagingAsync 校验后删除旧目录，再移动 staging；移动失败没有备份回滚。不能据校验成功承诺旧安装目录绝对安全。
- **未知字段保留有范围。** [FrontedWindowConfig](../neo-bpsys-wpf.Core/Models/FrontedLayout/FrontedWindowConfig.cs) 不含 root JsonExtensionData，[DocumentState](../neo-bpsys-wpf/ViewModels/Windows/FrontedDesignerWindowViewModel.DocumentState.cs) 重构强类型配置。原始文件导入保留未知字段，插件 ExtensionData 有明确保留契约；Designer 编辑保存不保证任意未知根字段 roundtrip。

Behavior 的 Queue/AllowParallel 重入和 LoopPolicy.AutoReverse 当前未实现执行语义；枚举/字段存在不等于能力可用，见[Behavior runtime](frontend/fronted-behavior-system.md)。

## 插件与 WebRenderer

插件是全信任代码，启动时在 Host build 前加载；安装/更新后通常需重启，无热加载。apiVersion 与 SDK 源码提交分别表示兼容契约和构建来源，见 [SDK](plugin/plugin-development.md)与[宿主](plugin/plugin-system.md)。

WebRenderer 是额外的实验性插件输出，不改变 WPF 前台的定位；需独立 ASP.NET Core runtime，插件控件缺 Web adapter 时显示 unsupported。LAN 模式无认证，WPF 仍是唯一业务 commit 所有者。协议、安全与构建入口见[架构](plugin/web-renderer-experimental.md)。Team/Member 的 Core 异步下载已与 Web URI/sidecar 下载分成两条实际链路，不能把后者的地址过滤和大小限制推定为前者也具备。

## 公告网络边界

[App.Services.xaml.cs](../neo-bpsys-wpf/App.Services.xaml.cs) 为 RemoteAnnouncements 配置默认 HttpClientHandler，AllowAutoRedirect 未关闭；[RemoteAnnouncementService](../neo-bpsys-wpf/Services/RemoteAnnouncementService.cs) 校验初始源相对路径并验证内容 SHA-256，但不应把初始路径限制描述成 HTTP 重定向后的同源保证。详见[公告架构](architecture/remote-announcements.md)。

## 比分与规则

ScoreV2 和自由模式各自持久化，切换不互算；Team.Score 只保留旧 JSON/DTO 兼容，不能还原不存在的半场历史。Game3Overtime 与 Game4 数值重叠，须结合 BO 上下文解析，见[比分规范](business/score-system-v2.md)。

GameGuidance 不支持 Free；GameRule.json 是项目配置，不是外部权威赛事规则源。ProductTour 的教学状态与 GameGuidance 的比赛推进不可混为一套服务。

公开用户文档可能滞后，内部维护以当前源码和对应规范为准；不据历史 TODO 推断已实现行为，也不据新版路径删除仍合法的兼容 API。
