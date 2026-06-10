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
1. 保持 WPF + Generic Host + DI 设计，页面、窗口、服务优先通过现有扩展注册。
1. 后台页面使用 `AddBackendPage<TView,TViewModel>()`，XAML 前台窗口使用 `AddFrontedWindow<TView,TViewModel>()`。当前 built-in 前台窗口由 v3 descriptor (`GetBuiltInV3Windows()`) 注册，通过 `FrontedWindowBase` + v3 layout host 创建，不再保留旧 XAML 文件。每个 v3 layout window 内部固定一个 `BaseCanvas`，外部不再注册或选择 Canvas。
1. 不要把 FrontedWindow 理解成 Web 前端，也不要引入 Web 前端假设。
1. 不要随意大规模重构服务、ViewModel 或资源结构。
1. 用户可见文本要考虑 `WPFLocalizeExtension` 和 `Locales/*.resx`，避免随手硬编码。
1. 插件安装/更新通常需要重启，因为插件在 Host build 前注入 DI。
1. 插件是全信任模型；安全边界依赖市场审核、微步云扫描、人工审查和小生态，不是沙箱。
1. 大部分按钮都需要有 `WPF-UI` 的图标。
1. 所有的公共属性和公共方法都需要写XML注释，包括参数、返回值、异常等。
1. **禁止在面向用户的 UI 文本中使用开发阶段占位表达**（如 "Phase 3"、"Phase 13E"、"Phase 9D" 等）。已实现功能的描述必须写实际行为；未实现功能的占位文本应写「将在后续版本中提供」而非内部阶段代号。真实 Placeholder（如 overlay 标签 `[Text]`）不在此限制内。
1. `IsActive` 只保留给框架/运行时激活语义，尤其是 CommunityToolkit.Mvvm `ObservableRecipient.IsActive`。布局、包、设置、业务状态、可见性、绑定 payload 和 behavior payload 不得使用泛名 `IsActive`，应使用 `IsActivePackage`、`IsVisible`、`IsEnabled`、`IsSelected` 等明确名称。`Visibility` 绑定不得直接绑定泛名 `IsActive`。

## Naming rule: do not use generic IsActive

`IsActive` is reserved for internal framework/runtime activation semantics, especially CommunityToolkit.Mvvm `ObservableRecipient.IsActive`.

Do not use `IsActive` for layout/package/settings/business data.

Use explicit names:
- `IsActivePackage`
- `IsCurrentPackage`
- `IsVisible`
- `IsBadgeVisible`
- `IsEnabled`
- `IsSelected`
- `IsExpanded`
- `IsVisibleInFrontManage`

Legacy note:
Old `.bpui` packages may contain `IsActive` inside `TextSettings` because old settings classes inherited `ObservableRecipient`.
That field is serialization leakage and must be ignored by LegacyConverter.

Visibility bindings must use `IsVisible` or a specific visibility-oriented property. Do not bind `Visibility` to generic `IsActive`.

## 改动前必读

| 改动类型 | 先读 |
| --- | --- |
| UI 状态、对局数据、前台绑定传播 | `docs/shared-data-and-state.md` |
| 前台窗口设计者模式、v3 布局配置、独立编辑器、`.bpui` 迁移设计 | `docs/fronted-designer-v3.md`、`docs/fronted-designer-editor.md` |
| async、下载回调、OCR 后台任务、UI 更新 | `docs/threading-dispatcher-and-async.md` |
| 图片、字体、resx、本地化、默认布局资源 | `docs/resources-localization-and-assets.md` |
| TODO、当前能力边界、不要误判的路线图 | `docs/known-limitations-and-roadmap.md` |
| 本地化、用户文本、resx 更新 | `docs/resources-localization-and-assets.md` |

## 文档规则

架构、路径、插件生命周期、SmartBP/OCR、构建发布等发生变化时，同步更新 `/docs`。内部开发文档默认使用中文，除非用户明确要求其他语言。

## 构建与测试

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
