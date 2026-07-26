# neo-bpsys-wpf.TeamJsonMaker

[neo-bpsys-wpf](https://github.com/PLFJY/neo-bpsys-wpf) 的内置插件 —— 后台队伍 JSON 文件制作工具，用于在主程序外编辑并导出可被队伍信息导入功能读取的 `队伍信息.json`。

## 简介

TeamJsonMaker 提供一个后台页，让导播 / 赛事工作人员在不进入主对局流程的情况下编辑队伍信息：队伍名称、队伍 LOGO URI、队伍颜色，以及求生者与监管者选手列表（含选手名称与定妆照 URI）。编辑结果可导出为符合主程序 `Team` 模型契约的 JSON 文件，也可从已有 JSON 导入继续编辑。

主程序的"队伍信息导入"功能（位于后台队伍页）可直接读取本插件导出的 JSON 文件，将队伍信息一键填充到当前对局。

## 功能

- **队伍元信息编辑**：队伍名称、队伍 LOGO URI（图床链接）、队伍颜色（`#RRGGBB` 或 `#AARRGGBB`，含颜色选择器与十六进制文本双向同步）
- **求生者选手列表**：默认 4 名选手，可添加 / 删除（删除按钮在人数 ≤ 4 时禁用，保留 BP 必需的最少槽位）
- **监管者选手列表**：默认 1 名选手，可添加 / 删除（删除按钮在人数 ≤ 1 时禁用）
- **导入已有 JSON**：从文件载入并继续编辑，导入时会强制覆盖阵营字段（`Camp`）并补齐到最少槽位
- **导出 JSON**：保存为 `*.json`，默认目录 `AppConstants.AppOutputPath`，文件名默认取队伍名称；已存在文件会提示覆盖

## 数据模型

### 队伍（`Team`）

```json
{
  "Name": "Gr",
  "ColorHex": "#FFD98098",
  "ImageUri": "https://example.com/team-logo.png",
  "SurMemberList": [
    { "Name": "Gr_heart", "Camp": "Sur", "ImageUri": "https://example.com/p1.png" }
  ],
  "HunMemberList": [
    { "Name": "Gr_mlx", "Camp": "Hun", "ImageUri": "https://example.com/h1.png" }
  ]
}
```

### 选手（`Member`）

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Name` | string | 选手名称 |
| `Camp` | enum (`Sur` / `Hun`) | 选手所属阵营，序列化为字符串 |
| `ImageUri` | string? | 选手定妆照的图片 URI（可为 null） |

### 序列化约定

- 使用 `JsonStringEnumConverter` 将 `Camp` 枚举序列化为字符串（`"Sur"` / `"Hun"`），而非数字
- `WriteIndented = true`，输出便于人工查看与版本管理
- 导入时缺失的 `Name` / `ImageUri` 视为空字符串，`ColorHex` 缺失或非法时回退到默认 `#FF337FB9`（通过 `ColorHelper.NormalizeHexOrDefault` 处理）
- 旧版 JSON 中遗留的 `IsActive` 字段会被导入逻辑忽略（不参与序列化模型）

## 架构

### 入口与注册

`Plugin` 继承 `PluginBase`，在 `Initialize` 中调用 `services.AddBackendPage<TeamJsonMaker, TeamJsonMakerViewModel>()` 注册后台页。无前台窗口、无后台服务、无独立配置文件。

### ViewModel

`TeamJsonMakerViewModel` 使用 CommunityToolkit.Mvvm 的 `[RelayCommand]` 与 `partial` 源生成器生成命令：

- `AddSurMemberCommand` / `RemoveSurMemberCommand`：维护求生者列表，删除时弹出确认对话框（`Wpf.Ui.Controls.MessageBox`）
- `AddHunMemberCommand` / `RemoveHunMemberCommand`：维护监管者列表
- `ImportCommand`：`OpenFileDialog` 选择 JSON 文件，反序列化为 `ImportedTeamJson` 后通过 `CreateTeam` 重建 `Team`，强制覆盖阵营并补齐最少槽位
- `ExportCommand`：`SaveFileDialog` 选择保存路径，序列化 `CurrentTeam` 写入文件；已存在文件提示覆盖

### 模型

`Team` 继承 `ObservableObjectBase`，`ColorHex` setter 通过 `ColorHelper.TryNormalizeHex` 规范化输入并同步 `Color` 属性（供颜色选择器绑定）。`Member` 是不可变构造 + 可变属性的 POCO，`Camp` 在导入时由 `ReplaceMembers` 强制设置为对应阵营，避免外部 JSON 错配阵营导致数据混乱。

## 目录结构

```
neo-bpsys-wpf.TeamJsonMaker/
├── Plugin.cs                         # 插件入口，注册后台页
├── Team.cs                           # 队伍数据模型
├── Member.cs                         # 选手数据模型
├── TeamJsonMaker.xaml(.cs)           # 后台页 UI
├── TeamJsonMakerViewModel.cs         # 后台页 ViewModel
├── icon.png                          # 插件图标
├── manifest.yml                      # 插件清单
└── neo-bpsys-wpf.TeamJsonMaker.csproj
```

## 构建

```powershell
# 单独构建插件
dotnet build .\Built-inPlugins\neo-bpsys-wpf.TeamJsonMaker\neo-bpsys-wpf.TeamJsonMaker.csproj -c Debug

# 完整构建（包含主项目与所有插件）
.\build.ps1
```

插件依赖 `PixiEditor.ColorPicker` 用于颜色选择。构建产物输出到主程序的 `Plugins` 目录：托管 DLL、manifest、图标。

## 使用流程

1. 在主程序后台导航中进入"队伍 JSON 导入文件制作"页
2. 填写队伍名称、LOGO URI、队伍颜色
3. 添加 / 编辑求生者与监管者选手（每名选手填写名称与定妆照 URI）
4. 点击"导出"选择保存路径，或点击"导入已有 JSON"载入并修改既有文件
5. 在主程序后台队伍页使用"队伍信息导入"功能选择本插件导出的 JSON 文件

## 已知限制

- 仅支持单队伍编辑，不保存"队伍库"；如需编辑多支队伍请分别导出多个 JSON 文件
- 选手定妆照与队伍 LOGO 必须是可在线访问的图片 URI（HTTP / HTTPS），不支持本地文件路径
- 颜色仅支持 `#RRGGBB` 与 `#AARRGGBB` 格式，其他格式会被规范化或回退到默认色
- 删除最后一名监管者或最后四名求生者会被阻止（按钮禁用），以保留 BP 必需的最少槽位
