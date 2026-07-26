# neo-bpsys-wpf.GameStopwatch

[neo-bpsys-wpf](https://github.com/PLFJY/neo-bpsys-wpf) 的内置插件 —— 提供比赛秒表前台窗口与后台控制页，用于在直播导播场景中展示对局计时。

## 简介

比赛秒表插件注册一个独立的前台窗口（透明、无标题栏、不显示在任务栏）和一个后台设置页。后台页负责秒表的开始 / 暂停 / 重置以及显示样式配置，前台窗口通过 `FrontedWindowBase` 暴露给 OBS 捕获，仅渲染当前计时文本。

计时核心基于 `System.Diagnostics.Stopwatch`，UI 刷新由 WPF `DispatcherTimer` 以 250ms 间隔触发 `PropertyChanged`，避免阻塞 UI 线程的同时保证显示文本平滑更新。

## 功能

- **计时控制**：开始、暂停、重置，状态变更立即通知前台窗口
- **前台窗口**：透明背景、`SizeToContent="Height"`、固定宽度可配置，适配 OBS 浏览器源 / 窗口捕获
- **样式配置**：文字颜色（ARGB）、字体（系统字体族）、字号（12–300）、窗口宽度（80–4096）
- **持久化设置**：所有样式配置保存在插件配置目录的 `Settings.json`

## 架构

### 服务

`GameStopwatchService` 实现 `IGameStopwatchService`（继承 `INotifyPropertyChanged`），同时承担计时与设置持久化职责：

- 内部使用 `Stopwatch` 跟踪经过时间，`DispatcherTimer` 周期触发 `DisplayText` 属性变更通知
- `TextColor`、`FontFamilyName`、`FontSize`、`WindowSize` 的 setter 通过 `SetSetting<T>` 统一处理：值变更后立即写回 `Settings.json` 并触发属性通知
- `FontSize` 与 `WindowSize` 在写入时进行 `Math.Clamp` 范围约束，防止异常值进入持久化文件
- 设置文件损坏时静默保留代码默认值，不猜测旧字段含义（遵循仓库规则 7）

### 前台窗口

`GameStopwatchWindow` 继承 `FrontedWindowBase`，通过 `AddFrontedWindow<GameStopwatchWindow, GameStopwatchWindowViewModel>()` 强类型注册。窗口的 `Width` 绑定到 `WindowSize`，`Height` 由 `SizeToContent="Height"` 自适应；`TextBlock` 绑定 `DisplayText`、`FontFamily`、`FontSize`、`Foreground`（`TextBrush`）。

`GameStopwatchWindowViewModel` 订阅 `IGameStopwatchService.PropertyChanged`，把服务属性映射到窗口自身的可绑定属性，`TextBrush` 由颜色十六进制值经 `BrushConverter` 转换并 `Freeze()` 后返回。

### 后台页

`GameStopwatchSettingsPage` 通过 `AddBackendPage<GameStopwatchSettingsPage, GameStopwatchSettingsPageViewModel>()` 注册，标注 `[BackendPageInfo("B4A6C6B0-5D54-4F43-9E6F-1F5D4BDA7F38", "比赛秒表", SymbolRegular.Timer24)]`。ViewModel 通过 `IFrontedWindowService.EnsureWindowCreated` / `ShowWindow` 打开前台窗口（使用固定 canonical id `plugin:neo_bpsys_wpf.GameStopwatch/A6B4CB0B-354B-4B66-8AB8-2E94C3F0E5D2`）。

## 目录结构

```
neo-bpsys-wpf.GameStopwatch/
├── GameStopwatchEntry.cs                                # 插件入口，注册服务、前台窗口与后台页
├── IGameStopwatchService.cs                             # 服务接口（INotifyPropertyChanged）
├── Services/
│   └── GameStopwatchService.cs                          # 计时与设置持久化实现
├── ViewModels/
│   ├── GameStopwatchSettingsPageViewModel.cs            # 后台设置页 ViewModel
│   └── GameStopwatchWindowViewModel.cs                  # 前台窗口 ViewModel
├── Views/
│   ├── GameStopwatchSettingsPage.xaml(.cs)              # 后台设置页 UI
│   └── GameStopwatchWindow.xaml(.cs)                    # 前台窗口 UI
├── Locales/                                             # 本地化资源（中 / 英 / 日）
│   ├── GameStopwatch.resx
│   ├── GameStopwatch.en-us.resx
│   └── GameStopwatch.ja-jp.resx
├── manifest.yml                                         # 插件清单
└── neo-bpsys-wpf.GameStopwatch.csproj
```

## 构建

```powershell
# 单独构建插件
dotnet build .\Built-inPlugins\neo-bpsys-wpf.GameStopwatch\neo-bpsys-wpf.GameStopwatch.csproj -c Debug

# 完整构建（包含主项目与所有插件）
.\build.ps1
```

插件依赖 `PixiEditor.ColorPicker` 用于后台页的颜色选择器。构建产物输出到主程序的 `Plugins` 目录：托管 DLL、manifest 与图标。

## 配置

无独立命令行参数。所有配置通过后台设置页 UI 进行，自动写入：

```
<PluginConfigFolder>\neo_bpsys_wpf.GameStopwatch\Settings.json
```

`Settings.json` 结构（损坏时回退到代码默认值）：

```json
{
  "TextColor": "#FFFFFFFF",
  "FontFamilyName": "Arial",
  "FontSize": 48,
  "WindowSize": 320
}
```

## 已知限制

- 仅支持单一秒表实例，不支持多窗口并行计时
- 前台窗口宽度通过 `WindowSize` 配置，高度随字号自适应
- 计时基于 `Stopwatch`，长时间运行后系统时钟漂移可能引入微小误差（不影响导播场景）
- 重置不会自动开始计时，需手动点击"开始"
