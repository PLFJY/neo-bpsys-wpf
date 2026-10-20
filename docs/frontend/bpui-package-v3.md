# Designer v3 `.bpui` 布局包标准

本文定义 Designer v3 使用的 `.bpui v3` 前台布局包格式。它是导入、导出、包管理、资源复制、热切换和 legacy 转换的规格依据。当前 v3 包导出、导入安装、激活复制和删除已实现，legacy `.bpui` 到 Designer v3 `.bpui` 的导入前转换也已实现。

源码入口：[manifest 模型](../../neo-bpsys-wpf.Core/Models/FrontedLayout/PackageModels/FrontedLayoutPackageManifest.cs)、[导入校验](../../neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedLayoutPackageImporter.Validation.cs)、[归档安全](../../neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedLayoutPackageImporter.Archive.cs)、[导出器](../../neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedLayoutPackageExporter.cs)、[包管理器](../../neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedLayoutPackageManager.cs)、[限制常量](../../neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedLayoutLimits.cs)。

## 1. 目的

`.bpui v3` 是可携带的 Fronted Designer v3 布局包。它用于在不同机器、不同导播项目或不同布局作者之间迁移前台窗口布局，而不是备份整个软件配置。

`.bpui v3` 可以打包：

1. v3 前台布局 JSON 文件。
2. 布局引用的图片、字体等资源。
3. manifest 元数据。
4. 可选预览图和说明文档。

`.bpui v3` 不是完整软件配置备份，不应打包或覆盖全局 `%APPDATA%/neo-bpsys-wpf/Config.json`。比赛数据、账号路径、OCR 配置、插件配置、日志、缓存和普通窗口设置都不属于 v3 布局包。

`.bpui v3` 必须服务于 Designer v3 的现有模型：

1. 布局文件对应 `FrontedLayouts/{WindowTypeName}.json`。
2. 运行时加载优先级：内置 v3 窗口为活动包 → 内置资源 → 空模板；插件 v3 窗口为活动包 → 空模板。普通导入保持原始 layout 文件，未知扩展字段随文件保留（用户选择压缩时另有显式暂存重写）；未注册插件窗口和控件的 layout/behavior/manifest entry 原样保留，不因当前 Registry 缺失而删除。
3. 前台窗口由 v3 renderer 根据 JSON 创建控件。
4. 每个 v3 layout window 运行时固定生成 `ViewBox -> Canvas BaseCanvas`，Canvas 不再是包路径、manifest 或管理单位。
5. 包内资源通过 URI 解析，不依赖全局 `Config.json` 中的自定义 UI 设置。

Window-centric layout schema 使用 `WindowSettings`、`CanvasSettings` 和 `ControlLayout` 三段结构。默认/BO5 使用 root canvas settings；启用 `EnableBoModeStates` 后，`BoModeStates["Bo3"]` 可携带独立 BO3 `BackgroundImage`、`RequiredPlugins` 和 `Controls`。包导入、导出和资源重写必须保留完整 schema，包括 BO3 state、控件 `Visibility`、缺失插件控件和插件 `ExtensionData`。preview-only `BackgroundImageVariants` 已移除，v3 包不再生成或迁移该字段。

## 2. legacy 格式识别

旧包是含 Config.json、CustomUi/、FrontElementsConfig/ 的 ZIP。旧 SettingPageViewModel.UiPackage.cs 导入/导出入口已移除；当前只通过显式转换读取旧数据，绝不覆盖全局设置。检测、默认蓝图、字段映射、图片压缩与启动迁移统一见[旧前台布局迁移](legacy-fronted-layout-migration.md)。保留旧格式识别不能恢复旧覆盖式导入。

## 3. 新 `.bpui v3` 文件身份

`.bpui` 扩展名继续表示 zip archive。导入器不能只根据扩展名判断包代际。

v3 包通过根目录 manifest 识别：

1. 根目录存在有效 `manifest.json`。
2. `Format == "neo-bpsys-bpui"`。
3. `FormatVersion == 3`。

legacy 包通过缺少有效 manifest 且存在历史结构识别：

1. 没有有效 `manifest.json`。
2. 存在 `Config.json`。
3. 或存在 `CustomUi/`。
4. 或存在 `FrontElementsConfig/`。

如果包同时出现 v3 manifest 和 legacy 文件，导入器应按 v3 包校验，并把 `Config.json` 等禁止内容报告为错误。

## 3.1 文件关联与双击打开

安装包会注册 `.bpui` 扩展名到 `neo-bpsys-wpf.exe`。设置页提供「关联 .bpui 文件」开关；应用启动时会按该设置静默检查并修复当前用户的文件关联，不显示通知。用户在资源管理器中双击 `.bpui` 文件时：

1. 如果应用未运行，应用启动后导入该布局包。
2. 如果应用已运行，第二个进程会把文件路径转发给已运行实例，然后退出。
3. 导入成功后立即把该包设为激活布局方案，并重载已创建的 v3 前台窗口。
4. 同 `PackageId` 的已安装包会被替换；原始 `.bpui` 文件不会被修改。
5. 缺失插件控件保持占位元数据，安装对应插件并重启后可恢复使用。
6. legacy `.bpui` 会先转换为 Designer v3 包，再按普通包安装和激活。

双击打开是面向操作系统激活的快捷路径，不经过包管理页的逐步确认流程；应用会导航到前台管理页并切换到布局包页面，导入结果通过顶部 InfoBar 显示，无法导入或转换时记录日志。

布局包 tab 在软件内导入和文件关联双击导入期间显示半透明遮罩与不定进度环。两种入口共享导入状态；解压和校验在后台线程执行，结束或失败后清除遮罩，避免大包处理时界面看起来卡住。

## 4. 新包结构

标准结构：

```text
package.bpui
├── manifest.json
├── FrontedLayouts/
│   ├── BpWindow.json
│   ├── CutSceneWindow.json
│   ├── GameDataWindow.json
│   ├── ScoreSurWindow.json
│   ├── ScoreHunWindow.json
│   ├── ScoreGlobalWindow.json
│   ├── BpOverviewWindow.json
│   └── MapV2Window.json
├── FrontedBehaviors/
│   ├── BpWindow.behaviors.json
│   └── MapV2Window.behaviors.json
├── resources/
│   ├── images/
│   ├── fonts/
│   └── other/
├── preview/
│   ├── cover.png
│   └── screenshots/
└── docs/
    └── README.md
```

必需内容：

1. `manifest.json`。
2. `FrontedLayouts/` 下至少一个布局 JSON（内置/插件或声明的 custom window）。

可选内容：

1. `resources/`。
2. `preview/`。
3. `docs/`。

当前实现导出使用 Window-centric 格式，布局路径为 `FrontedLayouts/{WindowTypeName}.json`，behavior 路径为 `FrontedBehaviors/{WindowTypeName}.behaviors.json`。早期规格中的 Current Canvas 导出范围不再暴露。Legacy 转换输出的 v3 包只包含能够从旧 `FrontElementsConfig/` 明确映射到新窗口的布局；旧 MapV1 会跳过并记录 Info。

## 5. `manifest.json` schema

manifest 不包含 `App` 对象，不包含 `App.Name`、`App.ExportedVersion` 或 `App.MinVersion`。应用最低版本只使用根级 `MinVersion`。

manifest 可选的 `CreatedVersion` 保存生成包时的完整应用版本（例如 `v3.0.7+git-hash`）。比较时只比较数字版本；当前应用低于该版本时导入仍成功，但提供非阻断警告。缺少该字段的旧包不产生警告，`MinVersion` 仍负责硬拒绝。

`Content.Layouts` 保存内置和插件窗口，用户自定义窗口单独保存于 `Content.CustomWindows`。自定义条目的 `Window` 与 `Path` 必须同时匹配包 ID，例如：

```json
"CustomWindows": [{
  "Window": "custom:example-layout/custom-window",
  "Path": "FrontedLayouts/custom/example-layout/custom-window.json"
}]
```

旧软件会忽略未知的 `CustomWindows` 和布局 JSON `DisplayNames` 字段，因此混合包中的内置/插件布局仍按旧契约加载；旧软件不会加载自定义窗口。

示例：

```json
{
  "Format": "neo-bpsys-bpui",
  "FormatVersion": 3,
  "PackageId": "plfjy.default-layout.2026",
  "Name": "Default Designer v3 Layout",
  "Description": "A Designer v3 frontend layout package.",
  "Author": "PLFJY",
  "CreatedAt": "2026-05-31T10:00:00Z",
  "MinVersion": "3.0.0",
  "LayoutModel": "WindowCentric",
  "LayoutSchemaVersion": 3,
  "PluginDependencies": [
    {
      "PackageId": "top.plfjy.example.fronted",
      "MinVersion": "1.0.0",
      "DisplayName": "Example Fronted Controls",
      "MarketplaceId": "top.plfjy.example.fronted",
      "RequiredBy": [
        "CutSceneWindow"
      ]
    }
  ],
  "Content": {
    "Layouts": [
      {
        "Window": "BpWindow",
        "Path": "FrontedLayouts/BpWindow.json"
      },
      {
        "Window": "MapV2Window",
        "Path": "FrontedLayouts/MapV2Window.json"
      }
    ],
    "Resources": [
      {
        "Id": "bp-bg",
        "Kind": "Image",
        "Path": "resources/images/bp.png",
        "Uri": "bpui://plfjy.default-layout.2026/resources/images/bp.png",
        "Sha256": "..."
      }
    ],
    "Preview": {
      "Cover": "preview/cover.png"
    }
  },
  "ImportPolicy": {
    "OverwriteExistingUserLayouts": "Ask",
    "RequireRestart": false
  }
}
```

字段说明：

| 字段 | 要求 |
| --- | --- |
| `Format` | 必需，固定为 `neo-bpsys-bpui`。 |
| `FormatVersion` | 必需，当前 v3 包为 `3`。它是包格式版本，不是 layout schema 版本。 |
| `LayoutModel` | 必需，Window-centric 包固定为 `WindowCentric`。旧开发版 v3 包不兼容。 |
| `PackageId` | 必需，包身份和资源命名空间。 |
| `Name` | 必需，面向用户显示的包名称。 |
| `Description` | 可选，包用途说明。 |
| `Author` | 可选，布局作者。 |
| `CreatedAt` | 推荐，UTC ISO 8601 时间。 |
| `CreatedVersion` | 可选，生成包时的完整应用版本，仅用于非阻断兼容性警告。 |
| `MinVersion` | 根级字段，表示能使用该包的最低应用版本。 |
| `LayoutSchemaVersion` | 必需，当前 `FrontedWindowConfig` layout schema 版本为 `3`。 |
| `PluginDependencies` | 可选，包级插件依赖摘要，用于导入预检 UI。完整规则见第 8 节。 |
| `Content.Layouts` | 内置/插件窗口清单；与 CustomWindows 合计至少一项。 |
| `Content.CustomWindows` | 可选，列出包作用域内的用户自定义 v3 窗口。 |
| `Content.Resources` | 可选，列出包内资源、类型、URI 和可选 hash。 |
| `Content.Preview` | 可选，预览图信息。 |
| `ImportPolicy.OverwriteExistingUserLayouts` | 保留的策略元数据，默认 `Ask`；当前导入/激活实现不读取它，不能用它保证覆盖前确认。 |
| `ImportPolicy.RequireRestart` | 保留的策略元数据，默认 `false`；当前实现不据此安排重启。 |

表中的格式/模型/版本固定值是输出契约。当前反序列化模型对缺省的 Format、FormatVersion、LayoutModel、LayoutSchemaVersion 分别使用上述默认值，校验比较反序列化后的值，并不逐项要求原始 JSON 显式出现这些 key；不能借此推断旧 canvas-centric 内容可用。

版本概念必须分离：

1. `FormatVersion` 是 `.bpui` 包格式版本。
2. `LayoutSchemaVersion` 是 `FrontedWindowConfig` / layout JSON schema 版本。
3. `MinVersion` 是能使用该包的最低应用版本。

## 6. `PackageId` 规则

`PackageId` 必填，既是包身份，也是 `bpui://` 资源命名空间。

推荐字符：

1. 小写字母。
2. 数字。
3. `.`。
4. `-`。
5. `_`。

禁止内容：

1. `/`。
2. `\`。
3. `:`。
4. `..`。
5. 任何路径穿越片段。
6. 空白字符。
7. URL escape 绕过，例如试图用 `%2f` 表示 `/`。

保留 `PackageId`：

| PackageId | 含义 |
| --- | --- |
| `builtin` | 系统内置布局和资源的虚拟包 ID，不作为普通包安装，不允许删除。 |
| `local` | 编辑器本地用户资源命名空间，不允许通过普通包删除；用户在导出前选择本地图片时使用。 |

## 7. 布局文件规则

布局文件就是当前 v3 `FrontedWindowConfig` JSON。每个布局文件必须有 `Version = 3`。

路径约定：

```text
FrontedLayouts/{WindowTypeName}.json
```

JSON 结构保持 Window-centric 三段模式：

1. `WindowSettings` 保存窗口尺寸、位置、透明、背景色、Topmost 和 `ViewboxStretch`。布局/行为 JSON 的具体字段模型见[布局契约](fronted-layout-v3-window-centric.md)和 [Behavior](fronted-behavior-system.md)。
2. `CanvasSettings` 保存内部 `BaseCanvas` 尺寸、背景图和 BO state；不包含 `BackgroundColor`。
3. `ControlLayout.RequiredPlugins` 保存控件依赖。
4. `ControlLayout.Controls` 中的 JSON key 就是控件名，不把控件包进数组。
5. 导入后 v3 renderer 必须能直接加载并渲染。

示例：

```json
{
  "Version": 3,
  "WindowSettings": {
    "WindowWidth": 1440,
    "WindowHeight": 810,
    "AllowsTransparency": true,
    "BackgroundColor": "#00000000",
    "Topmost": false,
    "ViewboxStretch": "Fill"
  },
  "CanvasSettings": {
    "CanvasWidth": 1440,
    "CanvasHeight": 810,
    "BackgroundImage": "bpui://plfjy.default-layout.2026/resources/images/bp.png",
    "EnableBoModeStates": false,
    "BoModeStates": {}
  },
  "ControlLayout": {
    "RequiredPlugins": [],
    "Controls": {
      "SurTeamName": {
        "ControlType": "Text",
        "Left": 580,
        "Top": 720,
        "Width": 120,
        "TextBinding": {
          "Sources": [
            { "Path": "CurrentGame.SurTeam.Name" }
          ]
        },
        "TextAlignment": "Center",
        "FontSize": 28,
        "Color": "#FFFFFFFF",
        "ZIndex": 2
      }
    }
  }
}
```

图片控件 schema 值保持原始 `ControlType` 字符串：`Image` 表示通用图片控件，运行时根元素承载主图和内部 overlay；`BorderedImage` 表示外层 `Border` + 内部图片层，用于需要外层容器、裁剪框或由外框承接设计器 resize 的图片区域。`BindingPath` 用于动态 `ImageSource` 绑定，`ImagePath` 用于静态资源图片；两者同时存在时 `BindingPath` 优先，`ImagePath` 不会被导入器清空。`Image` / `BorderedImage` 均可保存 `Lockable`、`LockImagePath`、`LockVisibilityBindingPath`、`LockVisibleWhen`、`PickingBorderAvailable`、`PickingBorderImagePath`、`LockZIndexOffset` 和 `PickingBorderZIndexOffset`；picking border 的运行时名称由控件名自动生成。旧字段继续兼容：`BanLockAvailable` 映射到 `Lockable`，`BanLockImagePath` 映射到 `LockImagePath`，`PickingBorder` 映射到 `PickingBorderAvailable`。包导入、导出和 roundtrip 不应翻译或重命名这些 `ControlType` 值，也不应丢弃新 overlay 字段或旧 alias 字段可解析出的含义。

## 8. 插件前台控件依赖

`.bpui` 支持 Designer v3 插件控件和插件 Layout 窗口依赖。布局 JSON 可读取 `plugin:*` 控件并保留插件专属属性，已安装插件通过统一 V3 Control API 注册运行时控件（公开 API 见 [SDK](../plugin/plugin-development.md)）；缺失插件控件在 Designer 显示占位符、在前台 runtime 中跳过并记录 warning。导入遇到缺失插件窗口或控件时保留 layout、资源和依赖元数据，不再删除缺失控件。插件市场安装 / 更新引导仍可用，但不会静默安装。

### 8.1 ControlType 命名标准

内置控件继续使用简单 `ControlType`：

```text
Text
Image
BorderedImage
```

插件控件必须使用命名空间形式：

```text
plugin:<PackageId>/<ControlTypeName>
```

示例：

```text
plugin:top.plfjy.example.fronted/TeamCard
```

规则：

1. 第三方控件必须使用 `plugin:` 前缀。
2. `PackageId` 必须匹配插件 `manifest.yml` 中的插件 ID / package ID。
3. `ControlTypeName` 只需在该插件内唯一。
4. 完整字符串是稳定序列化 schema，不能本地化，不能使用显示名代替。
5. 插件控件不能 shadow 内置 `ControlType`；`TeamCard` 这类无前缀值会被当作未知内置控件，而不是插件控件。

有效示例：

```text
plugin:top.plfjy.example.fronted/TeamCard
```

无效示例：

```text
TeamCard
plugin:TeamCard
top.plfjy.example.fronted.TeamCard
```

### 8.2 `ControlLayout.RequiredPlugins`

Window layout JSON 可以在 `ControlLayout.RequiredPlugins` 中声明本窗口的插件控件依赖：

```json
{
  "Version": 3,
  "WindowSettings": {},
  "CanvasSettings": {
    "CanvasWidth": 1440,
    "CanvasHeight": 810,
    "BackgroundImage": "Resources/cutScene.png"
  },
  "ControlLayout": {
    "RequiredPlugins": [
      {
        "PackageId": "top.plfjy.example.fronted",
        "MinVersion": "1.0.0",
        "DisplayName": "Example Fronted Controls",
        "Controls": [
          "plugin:top.plfjy.example.fronted/TeamCard"
        ]
      }
    ],
    "Controls": {
      "TeamCard1": {
        "ControlType": "plugin:top.plfjy.example.fronted/TeamCard",
        "Left": 100,
        "Top": 100,
        "Width": 260,
        "Height": 96,
        "TeamNameBindingPath": "CurrentGame.SurTeam.Name",
        "LogoBindingPath": "CurrentGame.SurTeam.Logo"
      }
    }
  }
}
```

`RequiredPlugins` 是保留元数据，不是控件 key。它只作用于单个 Window layout 文件，记录该窗口中控件依赖；导入器和导出器不能把它作为控件反序列化，也不能让用户创建同名控件。`Controls` 列出本窗口使用的完整插件 `ControlType` 字符串。

字段：

| 字段 | 要求 |
| --- | --- |
| `PackageId` | 必需，插件 ID / package ID。 |
| `MinVersion` | 可选但推荐，最低插件版本。 |
| `DisplayName` | 可选，面向用户显示的插件名称。 |
| `Controls` | 推荐，本窗口使用的完整插件控件类型列表。 |

导入器必须额外扫描实际控件的 `ControlType`，把所有以 `plugin:` 开头的控件纳入依赖分析。手写布局或旧导出器可能遗漏 `RequiredPlugins`，因此不能只信任该字段。

### 8.3 manifest `PluginDependencies`

`manifest.PluginDependencies` 是包级依赖摘要，由导出器扫描所有窗口 layout 后生成，主要服务于导入预检 UI；精确到窗口的声明仍在 `ControlLayout.RequiredPlugins`。

示例：

```json
{
  "Format": "neo-bpsys-bpui",
  "FormatVersion": 3,
  "PackageId": "my.layout.pack",
  "Name": "My Layout Pack",
  "MinVersion": "3.0.0",
  "LayoutSchemaVersion": 3,
  "PluginDependencies": [
    {
      "PackageId": "top.plfjy.example.fronted",
      "MinVersion": "1.0.0",
      "DisplayName": "Example Fronted Controls",
      "MarketplaceId": "top.plfjy.example.fronted",
      "RequiredBy": [
        "CutSceneWindow"
      ]
    }
  ],
  "Content": {
    "Layouts": []
  }
}
```

字段：

| 字段 | 要求 |
| --- | --- |
| `PackageId` | 必需，依赖插件 ID。 |
| `MinVersion` | 可选但推荐，最低插件版本。 |
| `DisplayName` | 可选，面向用户显示的插件名称。 |
| `MarketplaceId` | 可选，用于在插件市场中定位插件；缺省等于 `PackageId`。 |
| `Controls` | 可选，包中引用的 canonical 插件控件类型。 |
| `Events` | 可选，behavior 文档引用的 canonical 插件事件类型，例如 `plugin:foo.overlay/ShowCard`。 |
| `Reason` | 可选，`FrontedControl`、`FrontedWindow`、`BehaviorEvent`、`Both` 或 `Multiple`；旧 `Both` 值继续兼容。 |
| `RequiredBy` | 推荐，依赖来源列表，格式为 `WindowTypeName`。 |

导入器不应盲目信任 manifest 或 layout 元数据。正确流程是合并 `PluginDependencies`、每个窗口 `ControlLayout.RequiredPlugins`，扫描实际插件 `ControlType`，并扫描 behavior 的 `Trigger`、`StartTrigger`、`StopTriggers[]`、`TransitionTrigger` 中的 canonical plugin EventType，再得到最终依赖列表。Behavior event 使用与控件/窗口相同的插件版本策略；已安装插件版本写入合并后的 `MinVersion`。

### 8.4 缺失插件导入策略

导入 `.bpui` 时应先做插件依赖预检：

1. 读取 manifest `PluginDependencies`。
2. 读取每个窗口 `ControlLayout.RequiredPlugins`。
3. 扫描实际控件中 `ControlType` 以 `plugin:` 开头的项。
4. 扫描行为文件中所有触发器的 `plugin:<PackageId>/<EventId>` 引用。
5. 合并依赖列表。
6. 检查已安装插件、版本以及当前启动是否存在对应 event registration。
7. 分类为：已满足、缺失插件、已安装但版本过低、已安装但事件 registration 缺失、市场可安装 / 可更新、市场未找到、市场不可用。

用户选择：

1. 交互式安装或更新所需插件。
2. 跳过缺失插件并继续导入。
3. 取消导入。

硬规则：

1. `.bpui` 不能静默安装插件。
2. `.bpui` 不能携带插件 DLL。
3. 安装或更新插件可能要求重启，因为当前插件系统在启动期间、Host build 前加载插件。
4. 如果重启后插件才能生效，导入流程应说明：插件安装后通常需要重启；用户也可以继续导入并保留缺失插件布局 / 控件配置。
5. 会查询插件市场，但只做引导，不会静默安装、更新或热加载插件。

继续导入并保留缺失插件行为：

1. 保留所有依赖缺失插件或版本暂不满足的插件控件配置。
2. 保留插件窗口 layout、资源和 `RequiredPlugins` / `PluginDependencies` 元数据。
3. 保留 behavior 的 EventType、Filters 和 Graph，不在读取期重写 JSON。
4. Designer preview 对控件显示 MissingPlugin 占位符；未知事件显示 Missing plugin event 并保留 filter path。
5. 直播前台 runtime 跳过缺失插件控件；未发布的缺失插件事件自然不会触发行为。
6. 安装插件并重启后，保留的原始 `plugin:*` 控件和事件身份会自动重新 materialize / 识别。

### 8.5 运行时和编辑器缺失插件行为

前台窗口运行时：

1. 不能因插件控件缺失崩溃。
2. 应跳过缺失插件控件并记录 warning。
3. 直播前台窗口默认不渲染可能误导观众的占位符，除非后续有显式安全开关。

Designer：

1. 对已有用户布局，可以显示 `MissingPlugin` 占位符。
2. 占位符应显示 `PackageId`、`ControlTypeName` 和完整 `ControlType`。
3. 允许删除缺失插件控件。
4. 允许打开插件安装引导。
5. 在没有插件元数据时，不允许编辑插件专属属性。

占位符只是 Designer 视图，不会作为新的控件类型写入活动布局；活动布局保存的仍是原始 `plugin:*` 控件配置。

### 8.6 安全模型

插件控件是可执行代码。`.bpui` 包只是布局数据，必须不包含插件 DLL、不能静默安装插件，也不能静默启用插件。插件安装必须走现有插件系统 / 插件市场流程，UI 必须展示插件身份、版本、来源、权限信息（如果未来支持）、hash / signature 校验信息（如果支持），并要求用户确认。

插件系统是全信任模型，不是沙箱。安装插件意味着信任该代码；插件加载发生在启动期间，当前不支持热加载。`.bpui` 导入流程只能引导用户安装或更新插件，不能绕过插件系统生命周期。

## 9. 资源 URI 规则

允许的资源路径形式如下。

### 9.1 内置 bpui 文件资源

```text
Resources/foo.png
```

含义：解析到应用运行目录下的 `Resources/bpui/foo.png`。这是当前 v3 resolver 已使用的 legacy-compatible 简写，适合引用应用内置前台素材。

### 9.2 应用 pack 资源

```text
pack://application:,,,/Assets/Fonts/#Noto Sans
```

含义：WPF 应用程序集内嵌资源，主要用于内置字体或其他 app-bundled assets。字体 URI 的 `#` 后面是字体族名。

### 9.3 已安装包资源

```text
bpui://{PackageId}/resources/images/foo.png
bpui://{PackageId}/resources/fonts/foo.ttf#FontFamilyName
```

含义：解析到已安装布局包目录下的资源。`PackageId` 决定资源命名空间，同名文件在不同包之间互不相同。

### 9.4 编辑时绝对路径

```text
D:\design\foo.png
```

绝对路径只允许作为临时编辑输入。保存或导出时，应将文件复制到本地资源或包资源目录，并把 layout JSON 中的路径重写为 `bpui://...`。

## 10. 本地 bpui 资源存储

本地图片存储可以沿用旧行为的思路：用户选择图片后复制一份到 AppData，而不是长期依赖原始绝对路径。但 v3 layout JSON 应统一存储为 `bpui://...`。

`bpui://local/...` 是编辑器本地资源命名空间。用户在编辑器中选择本地图片时：

1. 将图片复制到本地资源存储。
2. layout JSON 中写入 `bpui://local/resources/images/{safeFileNameOrHash}.png`。

推荐本地存储路径：

```text
%APPDATA%/neo-bpsys-wpf/FrontedLayoutPackages/local/resources/images/
%APPDATA%/neo-bpsys-wpf/FrontedLayoutPackages/local/resources/fonts/
```

规则：

1. `local` 不是普通导入包。
2. `local` 资源由编辑器管理。
3. 普通包删除不能删除 `local`。
4. 导出包时，导出器应收集被选中布局引用的 `bpui://local/...` 资源，复制到导出包 `resources/`，并把引用重写为：

```text
bpui://{ExportedPackageId}/resources/images/...
bpui://{ExportedPackageId}/resources/fonts/...#FontFamilyName
```

Designer v3 字体属性导入 `.ttf`、`.otf`、`.ttc` 时直接写入当前可写布局包的 `resources/fonts/`，并在布局中保存 `bpui://{PackageId}/resources/fonts/...#FontFamilyName`。如果当前活动包是 `builtin`，会先复制为用户布局方案再写入字体。包内字体只在该活动包的字体列表顶部显示，带蓝色 BPUI 标记；切换布局包后不共享该字体列表。

## 11. 包资源隔离和删除

导入的图片和其他资源必须按包隔离。删除布局包必须删除该包自己的资源文件。

硬规则：

1. 每个已安装包有独立目录：

```text
%APPDATA%/neo-bpsys-wpf/FrontedLayoutPackages/{PackageId}/
├── manifest.json
├── FrontedLayouts/
└── resources/
```

2. 不把不同包的资源合并到共享全局目录。
3. `PackageId` 是资源命名空间。

示例：

```text
bpui://package-a/resources/images/bg.png
bpui://package-b/resources/images/bg.png
```

这两个 URI 表示两个不同文件，即使文件名都叫 `bg.png`。

URI 映射：

```text
bpui://{PackageId}/resources/images/bg.png
=>
%APPDATA%/neo-bpsys-wpf/FrontedLayoutPackages/{PackageId}/resources/images/bg.png
```

包内布局不应引用其他普通包的 `PackageId`。允许引用：

1. 自己的 `bpui://{PackageId}/...`。
2. 应用内置 `Resources/...`。
3. 应用 pack `pack://application:,,,/...`。
4. 导出前临时存在的 `bpui://local/...`。导出时必须重写为导出包的 `PackageId`。

导入校验拒绝跨包 `bpui://OtherPackageId/...` 引用。

删除包时：

1. 删除整个目录 `%APPDATA%/neo-bpsys-wpf/FrontedLayoutPackages/{PackageId}/`。
2. 不根据 manifest 逐个删除资源文件。
3. 目录级隔离删除可避免孤儿文件。

普通删除不允许删除 `builtin` 和 `local`。

删除当前激活包时：

1. 提示用户确认。
2. 如果确认，先切换活动包到 builtin。
3. 不修改旧 loose FrontedLayouts；活动包数据本来就保存在包目录。
4. 删除包目录。
5. 刷新已创建的前台窗口与自定义窗口注册。
6. 如果用户取消，不做任何修改。

导入已有 `PackageId` 的包时：

1. 视为替换或更新，不做 side-by-side 安装。
2. 询问用户确认。
3. 解压到 staging 目录。
4. 校验 manifest、布局和资源。
5. 校验成功后删除旧包目录，再把 staging 目录移动到目标位置。
6. 校验失败时保留旧包不变。

## 12. Zip-slip 和路径安全

导入安全规则：

1. zip entry 不能是绝对路径。
2. zip entry 不能包含 `..`。
3. manifest 中的路径不能包含路径穿越。
4. 解压后的最终路径必须仍位于 staging 目录内。
5. `PackageId` 必须按第 6 节规则校验和净化。
6. `PackageId` 中不能出现 slash、backslash 或 colon。
7. 导入器不能写出 AppData layout package 目录范围。
8. 替换已有包前必须先完成校验。
9. 导入过程必须使用 staging 目录，不能直接覆盖目标包目录。

## 13. 布局字段保存边界

完整 JSON 模型见[布局契约](fronted-layout-v3-window-centric.md)，编辑交互见[编辑器](fronted-designer-editor.md)。导入、导出、复制必须保留 WindowSettings（独立于设计尺寸）、CanvasSettings、BO3 state、DisplayNames、ControlLayout、Visibility 与插件扩展字段。仅 legacy canvas-centric 转换缺窗口尺寸时允许以设计尺寸初始化；普通读写不能覆盖已有窗口尺寸。背景图参与资源扫描、重写和 hash 校验；透明度是窗口设置，不是控件属性。





## 16. 已安装包与活动状态

普通包安装在 %APPDATA%/neo-bpsys-wpf/FrontedLayoutPackages/{PackageId}/，包含 manifest.json、FrontedLayouts/、可选 FrontedBehaviors/ 和 resources/。不得写成旧方案中的 layouts/。

active-package.json 位于 FrontedLayoutPackages 根，保存 PackageId 与 ActivatedAt；缺失默认 builtin。builtin 映射应用内置 Resources/FrontedLayouts，是只读方案；local 是资源命名空间，不是可激活方案。具体读取优先级只在[布局契约](fronted-layout-v3-window-centric.md)维护。

## 17. 激活、编辑、复制与删除

- 激活普通包保存 active state，后续读写直接进入该包，不复制到全局 loose FrontedLayouts。切回 builtin 只改活动状态，不删除其他包或 legacy 文件。
- UI 切包前通过 PrepareDesignerForPackageChangeAsync 处理未保存文档，再停止旧循环、刷新 custom registrations、重载已创建窗口；透明窗口重建见[窗口运行时](frontend-windows-and-layout.md)。布局切换本身不要求重启应用。
- 普通包直接可写；builtin 首次保存先复制为本地化“用户布局方案 {i}”及安全 user-layout-scheme-{i} 身份，激活后写入，不能覆盖发布资源。
- 复制普通包深拷贝目录，重写 manifest 身份、名称、描述、时间与 Content，同时重写自身资源 URI、custom 窗口 canonical ID、路径和行为 WindowType。重命名/改描述仅改 Name/Description，不改变 PackageId、目录或 URI。
- 删除普通包删除整个隔离目录；活动包先切回 builtin。builtin/local 不可通过普通包删除。未知插件窗口或控件不能因 registry 缺失而删除。

## 18. 导出

FrontManagePage 的导出对话框填写 PackageId、Name、Description、Author、MinVersion 等，输出存在时确认覆盖。UI 固定导出所选包的全部布局；服务层 ExportScope.CurrentWindow 仍保留，不能因 UI 未暴露就删除 API。

Exporter 优先使用请求 SourcePackageId，缺省使用当前活动包；枚举该源包的 FrontedLayouts，不切换活动包，也不拼入其他包或 loose store。custom 窗口必须由源 manifest 声明，导出时换为目标包身份。不要把运行时“用户包缺内置窗口时回退 builtin”的规则套到导出快照。

收集布局、BO 状态、behavior 与实际引用资源，将绝对路径、bpui://local 和可复制包资源写入目标 resources/，重写为目标包 URI；Resources/... 和应用 pack 资源保持原样。保留缺插件数据，汇总控件/窗口/事件依赖，生成 manifest 后打包 ZIP；资源缺失或安全校验失败必须报告。不能打包全局 Config.json。

## 19. 导入行为标准

导入 `.bpui v3`：

1. 选择 `.bpui`。
2. 解压到 staging 目录。
3. 校验 zip 路径安全。
4. 读取 manifest。
5. 校验 `Format` 和 `FormatVersion`。
6. 校验 `PackageId`。
7. 校验 `LayoutSchemaVersion`。
8. 校验 manifest 列出的布局文件存在。
9. 校验每个 layout `Version == 3`。
10. 合并 manifest PluginDependencies、各状态 RequiredPlugins、实际插件 ControlType 及所有 behavior 触发事件扫描结果，执行插件依赖预检。
11. 校验资源存在。
12. 校验没有跨包引用。
13. 如果有插件依赖未满足，进入第 8.4 节的安装 / 继续保留 / 取消流程。
14. 如果 `PackageId` 已存在，询问是否替换或更新。
15. 用户确认后，校验成功才进入替换。当前 ImportStagingAsync 先 Directory.Delete 旧安装目录，再 Directory.Move staging 到目标；没有旧包备份或回滚，移动失败可能丢失原安装目录。校验先行不等于原子替换，此风险见[已知边界](../known-limitations-and-roadmap.md)。
16. 不覆盖全局 `Config.json`。
17. layout-only 且无新增插件安装时不要求重启。
18. 可在导入完成后询问是否立即激活。

## 20. v3 包禁止内容

v3 `.bpui` 包不得包含：

1. `Config.json`。
2. 插件二进制，包括插件 DLL、依赖 DLL、插件 zip 或安装包。
3. 插件配置。
4. OCR 配置。
5. SmartBP 模型文件。
6. 比赛、队伍、选手数据。
7. 日志。
8. 缓存。
9. 用户账号、路径、窗口位置等普通设置。

包已支持插件 v3 窗口的 layout JSON，并在 manifest 中声明依赖插件；但包仍不得包含插件 DLL。导入布局包不得静默安装或启用插件，必须通过插件系统 / 插件市场流程完成用户确认、来源展示、版本检查和 hash / signature 校验（如果支持）。

## 21. 校验规则

建议校验严重级别：

| 条件 | 级别 |
| --- | --- |
| 缺少 manifest 必填字段 | Error |
| `PackageId` 非法 | Error |
| 未来不支持的 `FormatVersion` | Error 或 RequiresNewerApp |
| `LayoutSchemaVersion != 3` | Error |
| layout JSON 无效 | Error |
| 控件名重复 | Error |
| manifest 声明资源缺失 | Error 或按导入模式降为 Warning |
| 跨包 `bpui://OtherPackageId/...` 引用 | Error |
| manifest 未知字段 | 按模型反序列化忽略；当前不保证逐字段 Warning |
| layout 未知内置 `ControlType` | Error。 |
| 插件 `ControlType` 格式无效 | Error，例如 `plugin:TeamCard` 或空 `PackageId`。 |
| 插件 `ControlType` 格式有效但依赖未满足 | RequiresPlugin / Warning，按导入选择进入安装、继续保留或取消流程。 |

layout 层须保持有效尺寸、Version=3 和控件 key 身份。当前重复控件 key 的原始读取缺口见[已知边界](../known-limitations-and-roadmap.md)，不能将下面的校验要求当成所有入口已完整实现。

Window-centric root-level 保留字段包括 `Version`、`WindowSettings`、`CanvasSettings` 和 `ControlLayout`。旧 canvas helper 中的 `CanvasWidth`、`CanvasHeight`、`BackgroundImage` 和 `RequiredPlugins` 只作为 legacy/临时转换字段处理；导入、导出、设计器和校验器都应把 `RequiredPlugins` 视为布局元数据，不能作为控件名。

Shape 控件使用 `ControlType: "Rectangle"` 或 `"Polygon"`。共享字段包括 `FillMode`、静态/绑定纯色配置、可分别绑定的渐变起始色与结束色、`GradientAngle`、`StrokeColor` 和 `StrokeThickness`。Polygon 的 `Points` 是 `{ "X": 0..1, "Y": 0..1 }` 数组，至少包含三个有效顶点；运行时将归一化坐标乘以控件宽高。

v3 导入器执行硬安全限制：`.bpui` 压缩包最大 50 MiB，解压后总大小最大 100 MiB，普通单 entry 最大 10 MiB，entry 数最多 1000；`manifest.json` 最大 256 KiB，layout JSON 最大 2 MiB，`window.json` 最大 64 KiB，JSON 最大深度为 32。外部导入的 manifest/layout/window 字符串超长或 Canvas 控件数超过 256 会拒绝导入，不会静默截断或丢弃控件。Canvas 控件数达到 160 开始给出 warning；包内布局（含 custom）最多 100 个、资源清单最多 500 项。导入器还会在解压入口拒绝插件目录、插件二进制和可执行脚本，例如 `Plugins/`、`Plugin/`、`.dll`、`.exe`、`.msi`、`.ps1`、`.bat`、`.cmd`、`.sh`、`.vbs`、`.js` 和 `.jar`。布局包可以携带图片、字体、JSON、Markdown 等布局资源，但不能携带插件可执行内容。

图片资源在复制或导入前会校验扩展名、文件大小和像素尺寸。Canvas 背景图限制为 2.5 MiB、长边 4096、像素 4096×4096；普通 UI 图片限制为 1 MiB、长边 2048、像素 2048×2048；包内未知用途图片按包资源入口限制处理并仍需能安全解码。普通导入默认拒绝超限图片；UI 汇总后可由用户明确选择“压缩后导入”，只在暂存副本重编码并更新 manifest SHA256，源包不变。未确认时不复制或生成 URI。

字体资源允许 `.ttf`、`.otf` 和 `.ttc`。导出时 manifest `Content.Resources` 中写 `Kind = "Font"`，导入校验按字体扩展名和包内路径安全处理，不走图片解码限制。

## 22. legacy 转换边界

启动迁移和旧包导入共用 legacy 输入抽象与转换核心。启动输入来自 AppData 的 Config.json、*Config-*.json、CustomUi；归档输入来自解压目录的 Config.json、FrontElementsConfig 与 CustomUi。均以旧默认值和显式蓝图从零生成，不 clone 当前内置 v3 JSON。转换输出再经普通 importer 安装；layout-only 转换不覆盖全局 Config、不要求重启。

字段映射、旧 archive 的更大预算、可压缩图片、警告/诊断分级和失败恢复统一见[迁移规范](legacy-fronted-layout-migration.md)。manifest-based v3 不启用读取期 legacy 猜测。
