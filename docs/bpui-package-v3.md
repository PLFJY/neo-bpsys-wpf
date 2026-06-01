# Designer v3 `.bpui` 布局包标准

本文定义 Designer v3 使用的 `.bpui v3` 前台布局包格式。它是导入、导出、包管理、资源复制、热切换和 legacy 转换的规格依据；Phase 9D 已实现 v3 包导出、导入安装、激活复制和删除语义，Phase 9F 已实现 legacy `.bpui` 到 Designer v3 `.bpui` 的导入前转换。

## 1. 目的

`.bpui v3` 是可携带的 Fronted Designer v3 布局包。它用于在不同机器、不同导播项目或不同布局作者之间迁移前台窗口布局，而不是备份整个软件配置。

`.bpui v3` 可以打包：

1. v3 前台布局 JSON 文件。
2. 布局引用的图片、字体等资源。
3. manifest 元数据。
4. 可选预览图和说明文档。

`.bpui v3` 不是完整软件配置备份，不应打包或覆盖全局 `%APPDATA%/neo-bpsys-wpf/Config.json`。比赛数据、账号路径、OCR 配置、插件配置、日志、缓存和普通窗口设置都不属于 v3 布局包。

`.bpui v3` 必须服务于 Designer v3 的现有模型：

1. 布局文件对应 `FrontedLayouts/{WindowTypeName}/{CanvasName}.json`。
2. 运行时继续遵守用户布局优先、内置布局兜底。
3. 前台窗口由 v3 renderer 根据 JSON 创建控件。
4. layout JSON root-level key 就是控件名。
5. 包内资源通过 URI 解析，不依赖全局 `Config.json` 中的自定义 UI 设置。

## 2. legacy `.bpui` 格式摘要

旧 `.bpui` 导入导出位于 `SettingPageViewModel.UiPackage.cs`，文件选择位于 `FilePickerService.cs`。旧包本质上是一个 zip，典型结构为：

```text
legacy.bpui
├── Config.json
├── CustomUi/
└── FrontElementsConfig/
```

旧导出行为：

1. 先保存一次全局设置，确保 `Config.json` 中的路径已规范化。
2. 把 `%APPDATA%/neo-bpsys-wpf/Config.json` 复制到临时包根目录。
3. 从 `Settings` 对象中递归收集有效自定义 UI 图片路径。
4. 把这些图片复制到 `CustomUi/`。
5. 遍历旧前台 Canvas，把 `%APPDATA%/neo-bpsys-wpf/{WindowTypeName}Config-{CanvasName}.json` 复制到 `FrontElementsConfig/`。
6. 将临时目录压缩成 `.bpui`。

旧导入行为：

1. 选择 `.bpui` 或 `.zip`。
2. 解压到临时目录。
3. 用包内 `Config.json` 覆盖 AppData 的全局 `Config.json`。
4. 把 `CustomUi/` 文件复制到 `%APPDATA%/neo-bpsys-wpf/CustomUi`。
5. 把 `FrontElementsConfig/` 文件复制到 `%APPDATA%/neo-bpsys-wpf`。
6. 提示导入完成后重启应用。

旧格式不适合 Designer v3，原因包括：

1. 它覆盖全局设置，布局导入会影响与前台布局无关的软件配置。
2. 它把前台 UI、自定义图片和普通应用设置耦合在同一个 `Config.json` 中。
3. 它没有 manifest，无法声明包身份、版本、作者、内容清单和校验信息。
4. 它没有清晰的包身份，无法区分两个同名资源属于哪个布局包。
5. 它不能干净隔离包内资源，资源可能互相覆盖或遗留。
6. 它不能表达 v3 的 `FrontedLayouts/{Window}/{Canvas}.json` 文件结构。
7. 它不适合热切换布局包。
8. 它通常要求重启，即使布局重载已经足够。

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

## 4. 新包结构

标准结构：

```text
package.bpui
├── manifest.json
├── layouts/
│   ├── BpWindow/
│   │   └── BaseCanvas.json
│   ├── CutSceneWindow/
│   │   └── BaseCanvas.json
│   ├── GameDataWindow/
│   │   └── BaseCanvas.json
│   ├── ScoreSurWindow/
│   │   └── BaseCanvas.json
│   ├── ScoreHunWindow/
│   │   └── BaseCanvas.json
│   ├── ScoreGlobalWindow/
│   │   └── BaseCanvas.json
│   └── WidgetsWindow/
│       ├── MapBpCanvas.json
│       ├── BpOverViewCanvas.json
│       └── MapV2Canvas.json
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
2. `layouts/` 下至少一个布局 JSON。

可选内容：

1. `resources/`。
2. `preview/`。
3. `docs/`。

当前实现导出始终包含全部已迁移前台布局。早期规格中的 Current Canvas / Current Window 导出范围暂不再暴露。Phase 9F legacy 转换输出的 v3 包只包含能够从旧 `FrontElementsConfig/` 明确映射到 v3 窗口和 Canvas 的布局。

## 5. `manifest.json` schema

manifest 不包含 `App` 对象，不包含 `App.Name`、`App.ExportedVersion` 或 `App.MinVersion`。应用最低版本只使用根级 `MinVersion`。

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
  "LayoutSchemaVersion": 3,
  "PluginDependencies": [
    {
      "PackageId": "top.plfjy.example.fronted",
      "MinVersion": "1.0.0",
      "DisplayName": "Example Fronted Controls",
      "MarketplaceId": "top.plfjy.example.fronted",
      "RequiredBy": [
        "CutSceneWindow/BaseCanvas"
      ]
    }
  ],
  "Content": {
    "Layouts": [
      {
        "Window": "BpWindow",
        "Canvas": "BaseCanvas",
        "Path": "layouts/BpWindow/BaseCanvas.json"
      },
      {
        "Window": "WidgetsWindow",
        "Canvas": "MapBpCanvas",
        "Path": "layouts/WidgetsWindow/MapBpCanvas.json"
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
| `FormatVersion` | 必需，当前 v3 包为 `3`。它是包格式版本，不是 Canvas schema 版本。 |
| `PackageId` | 必需，包身份和资源命名空间。 |
| `Name` | 必需，面向用户显示的包名称。 |
| `Description` | 可选，包用途说明。 |
| `Author` | 可选，布局作者。 |
| `CreatedAt` | 推荐，UTC ISO 8601 时间。 |
| `MinVersion` | 根级字段，表示能使用该包的最低应用版本。 |
| `LayoutSchemaVersion` | 必需，当前 `FrontedCanvasConfig` layout schema 版本为 `3`。 |
| `PluginDependencies` | 可选，包级插件依赖摘要，用于导入预检 UI。完整规则见第 8 节。 |
| `Content.Layouts` | 必需，列出包内布局。至少一项。 |
| `Content.Resources` | 可选，列出包内资源、类型、URI 和可选 hash。 |
| `Content.Preview` | 可选，预览图信息。 |
| `ImportPolicy.OverwriteExistingUserLayouts` | 可选，建议值为 `Ask`，表示激活时是否覆盖同名用户布局需要询问。 |
| `ImportPolicy.RequireRestart` | 可选，布局-only 包应为 `false`。 |

版本概念必须分离：

1. `FormatVersion` 是 `.bpui` 包格式版本。
2. `LayoutSchemaVersion` 是 `FrontedCanvasConfig` / layout JSON schema 版本。
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

布局文件就是当前 v3 `FrontedCanvasConfig` JSON。每个布局文件必须有 `Version = 3`。

路径约定：

```text
layouts/{WindowTypeName}/{CanvasName}.json
```

JSON 结构保持当前 v3 root-level dictionary 模式：

1. root object 中的控件 JSON key 就是控件名。
2. 不把控件包进数组。
3. 不增加新的外层 `Controls` 对象。
4. 不在单个 config object 内存储重复的 `Name` 字段。
5. 导入后 v3 renderer 必须能直接加载并渲染。

示例：

```json
{
  "Version": 3,
  "CanvasWidth": 1440,
  "CanvasHeight": 810,
  "BackgroundImage": "bpui://plfjy.default-layout.2026/resources/images/bp.png",
  "SurTeamName": {
    "ControlType": "Text",
    "Left": 580,
    "Top": 720,
    "Width": 120,
    "BindingPath": "CurrentGame.SurTeam.Name",
    "TextAlignment": "Center",
    "FontSize": 28,
    "Color": "#FFFFFFFF",
    "ZIndex": 2
  }
}
```

图片控件 schema 值保持原始 `ControlType` 字符串：`Image` 表示 direct image，根元素就是 WPF `Image`，用于旧 direct XAML Image 行为和简单图片；`BorderedImage` 表示外层 `Border` + 内层 `Image`，用于需要外层容器、裁剪框或由外框承接设计器 resize 的图片区域。`BorderedImage` 的 `HorizontalAlignment` / `VerticalAlignment` / `Stretch` / `ImageWidth` / `ImageHeight` 作用于内层 `Image`，`Left` / `Top` / `Width` / `Height` / `ZIndex` 作用于外层 `Border`。包导入、导出和 roundtrip 不应翻译或重命名这些 `ControlType` 值。

## 8. 插件前台控件依赖

Phase 13B 已实现第三方插件前台控件的 core registry/runtime plumbing：布局 JSON 可读取 `plugin:*` 控件并保留插件专属属性，已安装插件可通过 descriptor/contributor API 注册运行时控件，缺失插件控件在前台 runtime 中跳过并记录 warning。Phase 13C 已实现 Designer Add Control 插件 UI、插件声明式属性编辑、Canvas `RequiredPlugins` 同步和缺失插件 Designer 占位符。Phase 13D 已实现 `.bpui` 依赖扫描、导出 manifest `PluginDependencies`、导入预检和强制导入删除缺失插件控件。插件市场安装流程仍留到 Phase 13E。

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

### 8.2 Canvas `RequiredPlugins`

Canvas layout JSON 可以声明本 Canvas 中插件控件依赖：

```json
{
  "Version": 3,
  "CanvasWidth": 1440,
  "CanvasHeight": 810,
  "BackgroundImage": "Resources/cutScene.png",
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
  "TeamCard1": {
    "ControlType": "plugin:top.plfjy.example.fronted/TeamCard",
    "Left": 100,
    "Top": 100,
    "Width": 260,
    "Height": 96,
    "TeamNameBindingPath": "CurrentGame.HomeTeam.Name",
    "LogoBindingPath": "CurrentGame.HomeTeam.Logo"
  }
}
```

`RequiredPlugins` 是保留元数据，不是控件 key。它只作用于单个 Canvas 文件，记录该 Canvas 中的控件依赖；导入器和导出器不能把它作为控件反序列化，也不能让用户创建同名控件。`Controls` 列出本 Canvas 使用的完整插件 `ControlType` 字符串。

字段：

| 字段 | 要求 |
| --- | --- |
| `PackageId` | 必需，插件 ID / package ID。 |
| `MinVersion` | 可选但推荐，最低插件版本。 |
| `DisplayName` | 可选，面向用户显示的插件名称。 |
| `Controls` | 推荐，本 Canvas 使用的完整插件控件类型列表。 |

导入器必须额外扫描实际控件的 `ControlType`，把所有以 `plugin:` 开头的控件纳入依赖分析。手写布局或旧导出器可能遗漏 `RequiredPlugins`，因此不能只信任该字段。

### 8.3 manifest `PluginDependencies`

`manifest.PluginDependencies` 是包级依赖摘要，由导出器扫描所有 Canvas 后生成，主要服务于导入预检 UI；精确到 Canvas 的声明仍在 `Canvas.RequiredPlugins`。

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
        "CutSceneWindow/BaseCanvas"
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
| `RequiredBy` | 推荐，依赖来源列表，格式为 `WindowTypeName/CanvasName`。 |

导入器不应盲目信任 manifest 或 Canvas 元数据。正确流程是合并 `PluginDependencies`、每个 Canvas 的 `RequiredPlugins`，并扫描实际插件 `ControlType`，再得到最终依赖列表。

### 8.4 缺失插件导入策略

导入 `.bpui` 时应先做插件依赖预检：

1. 读取 manifest `PluginDependencies`。
2. 读取每个 Canvas `RequiredPlugins`。
3. 扫描实际控件中 `ControlType` 以 `plugin:` 开头的项。
4. 合并依赖列表。
5. 检查已安装插件及版本。
6. Phase 13D 分类为：已满足、缺失插件控件。版本过低和插件市场可用性只记录为后续扩展点，不在本阶段硬阻断或自动安装。

用户选择：

1. 交互式安装或更新所需插件。
2. 跳过缺失插件并继续导入。
3. 取消导入。

硬规则：

1. `.bpui` 不能静默安装插件。
2. `.bpui` 不能携带插件 DLL。
3. 安装或更新插件可能要求重启，因为当前插件系统在启动期间、Host build 前加载插件。
4. 如果重启后插件才能生效，导入流程应说明：布局导入只能在插件可用后完整继续；用户也可以选择强制导入并删除缺失插件控件。
5. Phase 13D 不查询插件市场；如果有缺失插件控件，应提示用户“强制导入并删除缺失插件控件”或“取消导入”。

强制导入行为：

1. 删除所有依赖缺失插件的控件。
2. 保留其他控件。
3. 从导入后的活动布局 `RequiredPlugins` 中移除已不再满足或已无控件引用的依赖项。
4. 显示导入报告，列出被删除的控件名、完整 `ControlType` 和依赖插件。
5. 保持原始 `.bpui` 包或 archive 不变，方便用户以后安装插件后重新导入。

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

包强制导入时应删除缺失插件控件，而不是把占位符写入导入后的活动布局。

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
```

规则：

1. `local` 不是普通导入包。
2. `local` 资源由编辑器管理。
3. 普通包删除不能删除 `local`。
4. 导出包时，导出器应收集被选中布局引用的 `bpui://local/...` 资源，复制到导出包 `resources/`，并把引用重写为：

```text
bpui://{ExportedPackageId}/resources/images/...
```

## 11. 包资源隔离和删除

导入的图片和其他资源必须按包隔离。删除布局包必须删除该包自己的资源文件。

硬规则：

1. 每个已安装包有独立目录：

```text
%APPDATA%/neo-bpsys-wpf/FrontedLayoutPackages/{PackageId}/
├── manifest.json
├── layouts/
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

第一版导入校验应拒绝跨包 `bpui://OtherPackageId/...` 引用。

删除包时：

1. 删除整个目录 `%APPDATA%/neo-bpsys-wpf/FrontedLayoutPackages/{PackageId}/`。
2. 不根据 manifest 逐个删除资源文件。
3. 目录级隔离删除可避免孤儿文件。

普通删除不允许删除 `builtin` 和 `local`。

删除当前激活包时：

1. 提示用户确认。
2. 如果确认，先切换活动包到 `builtin` 或无活动包。
3. 移除 `%APPDATA%/neo-bpsys-wpf/FrontedLayouts` 中由该包激活出来的用户布局。
4. 删除包目录。
5. 如可行，刷新已打开的前台窗口。
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

## 13. Canvas Background GUI 标准

当前 Canvas 背景编辑 GUI 尚未完成，后续应提供 Canvas 级属性编辑器。它应支持：

1. `CanvasWidth`。
2. `CanvasHeight`。
3. `BackgroundImage`。
4. 清除背景图。
5. 浏览资源。
6. 选择本地图片。
7. 立即预览背景变化。

规则：

1. Canvas 背景不是普通控件。
2. 它属于当前 `FrontedCanvasConfig`。
3. UI 应放在 Canvas Properties 面板或等价的编辑器区域。
4. 它参与 dirty state、undo/redo、validation、save 和 package export。
5. 选择本地图片时，应复制到本地资源并写为 `bpui://local/...`。
6. 导入包资源应写为 `bpui://{PackageId}/...`。
7. 内置资源可以继续使用 `Resources/...`。
8. `BackgroundImage` 应通过资源 resolver 校验。

## 14. 窗口透明选项标准

后续应增加“允许窗口透明”开关。透明设置是窗口级选项，不是控件级属性。

显示位置：

1. 单 Canvas 窗口可在 Canvas Properties 附近显示。
2. `WidgetsWindow` 等多 Canvas 窗口中，它作用于整个窗口，而不是某个 Canvas。

推荐存储为窗口级 options 文件：

```text
layouts/{WindowTypeName}/window.json
```

用户存储路径：

```text
%APPDATA%/neo-bpsys-wpf/FrontedLayouts/{WindowTypeName}/window.json
```

示例：

```json
{
  "Version": 3,
  "AllowTransparency": true
}
```

WPF 的 `AllowsTransparency` / 透明窗口行为可能需要重新创建窗口或重启应用。UI 在该设置变化时应提示“需要重启”。

重启流程：

1. 用户切换 `AllowTransparency`。
2. UI 显示“restart required”提示。
3. 提供按钮：`Later`、`Restart Now`。
4. 如果用户选择 `Restart Now` 且设计器有未保存修改，继续询问：`Save`、`Discard`、`Cancel`。
5. `Save` 失败则取消重启。
6. `Discard` 继续重启。
7. `Cancel` 中止重启。
8. 重启应复用现有机制，例如可用时调用 `AppBase.Current.Restart()`。

## 15. FrontManagePage Tab 布局标准

新版 v3 布局包管理页应位于 `FrontManagePage`，并使用顶部 tabs。不要通过 `SettingPage` 管理 v3 包。

当前 tabs：

| Tab | 内容 |
| --- | --- |
| `Frontend Windows` | 现有前台窗口打开、关闭、管理功能，并保留 `FrontedDesignerWindow` 入口。 |
| `Layout Packages` | 新增 v3 `.bpui` 包导入、导出、激活、删除和查看。 |

`SettingPage` 中旧 `.bpui` 导入导出属于 legacy 行为。新的 Designer v3 布局包应通过 `FrontManagePage` 的 `Layout Packages` tab 管理。旧 SettingPage 流程可以暂时保留，待新管理器完成后再弃用。

## 16. Layout Package Manager 标准

包管理器必备能力：

1. 列出已安装包。
2. 包含系统内置选项。
3. 导入 `.bpui v3`。
4. 删除已安装包。
5. 激活或热切换包。
6. 导出包。
7. 显示 manifest 字段。
8. 显示校验状态。
9. 需要时打开包目录。

系统内置选项：

| 字段 | 值 |
| --- | --- |
| `PackageId` | `builtin` |
| `Name` | `System Built-in` |
| 是否可删除 | 否 |
| 来源 | 应用 `Resources/FrontedLayouts` |

已安装包路径：

```text
%APPDATA%/neo-bpsys-wpf/FrontedLayoutPackages/{PackageId}/
├── manifest.json
├── layouts/
└── resources/
```

活动包状态推荐保存到：

```text
%APPDATA%/neo-bpsys-wpf/FrontedLayoutPackages/active-package.json
```

示例：

```json
{
  "PackageId": "plfjy.default-layout.2026",
  "ActivatedAt": "2026-05-31T10:00:00Z"
}
```

## 17. 热切换激活行为

第一版激活包不需要大改 `FrontedLayoutService`。它可以复用当前运行时“用户布局优先、内置布局兜底”的优先级。

激活普通包：

1. 校验已安装包。
2. 将包内 layout 复制到：

```text
%APPDATA%/neo-bpsys-wpf/FrontedLayouts/{WindowTypeName}/{CanvasName}.json
```

3. 保存 `active-package.json`。
4. 如可行，刷新已打开的前台窗口。
5. 仅布局变更不要求重启。

激活 `builtin`：

1. 清空活动包状态，或设置 `PackageId = builtin`。
2. 删除或清理 `%APPDATA%/neo-bpsys-wpf/FrontedLayouts/` 中由包激活产生的用户布局。
3. 运行时回退到应用内置 `Resources/FrontedLayouts`。
4. 如可行，刷新已打开的前台窗口。

注意：如果用户手工编辑过 `%APPDATA%/neo-bpsys-wpf/FrontedLayouts`，包管理器需要区分“由包激活复制出的布局”和“用户自己编辑的布局”。第一版可通过活动包状态或激活标记文件保守处理，避免静默删除用户改动。

## 18. 导出 manifest 对话框标准

导出布局包时应打开 manifest 字段对话框。

字段：

1. `PackageId`。
2. `Name`。
3. `Description`。
4. `Author`。
5. `MinVersion`。
6. Export Scope：
   - 当前实现固定为 `All Frontend Layouts`，不再在导出对话框中显示导出范围。
7. 可选 `Cover image`。
8. 可选 `README`。

校验：

1. `PackageId` 必填且必须安全。
2. `Name` 必填。
3. `MinVersion` 可选但推荐填写。
4. 输出文件已存在时必须确认覆盖。

导出行为：

1. 选择输出 `.bpui`。
2. 填写 manifest 字段。
3. 选择导出范围。
4. 从用户布局 store 收集选中布局。
5. 如果用户布局不存在，full export 或 current window export 可以允许使用内置 fallback。
6. 收集布局引用资源。
7. 将绝对路径、`bpui://local/...` 或其他可复制包资源复制到导出包 `resources/`。
8. 将复制后的引用重写为 `bpui://{PackageId}/...`。
9. 生成 `manifest.json`。
10. 压缩为 `.bpui`。

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
10. 合并 manifest `PluginDependencies`、Canvas `RequiredPlugins` 和实际插件 `ControlType` 扫描结果，执行插件依赖预检。
11. 校验资源存在。
12. 校验没有跨包引用。
13. 如果有插件依赖未满足，进入第 8.4 节的安装 / 跳过 / 取消 / 强制导入流程。
14. 如果 `PackageId` 已存在，询问是否替换或更新。
15. 用户确认后，在校验成功的前提下原子替换包目录。
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

如果未来支持插件拥有的前台窗口布局，包可以包含插件窗口的 layout JSON，并在 manifest 中声明依赖插件；但包仍不得包含插件 DLL。导入布局包不得静默安装或启用插件，必须通过插件系统 / 插件市场流程完成用户确认、来源展示、版本检查和 hash / signature 校验（如果支持）。

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
| 跨包 `bpui://OtherPackageId/...` 引用 | 第一版 Error |
| manifest 未知字段 | Warning 并忽略 |
| layout 未知内置 `ControlType` | Error。 |
| 插件 `ControlType` 格式无效 | Error，例如 `plugin:TeamCard` 或空 `PackageId`。 |
| 插件 `ControlType` 格式有效但依赖未满足 | RequiresPlugin / Warning，按导入选择进入安装、跳过、取消或强制导入流程。 |

layout 层校验仍应遵守现有 Designer v3 规则：Canvas 尺寸必须有效，`Version` 必须为 3，root-level 控件 key 是控件名，运行时关键控件名不能静默丢失。

Canvas root-level 保留字段包括 `Version`、`CanvasWidth`、`CanvasHeight`、`BackgroundImage` 和 `RequiredPlugins`。这些字段不能作为控件名处理；导入、导出、设计器和校验器都应把 `RequiredPlugins` 视为 Canvas 元数据。

Phase 10 起，导入器增加硬安全限制：`.bpui` 压缩包最大 50 MiB，解压后总大小最大 100 MiB，单 entry 最大 10 MiB，entry 数最多 1000；`manifest.json` 最大 256 KiB，layout JSON 最大 2 MiB，`window.json` 最大 64 KiB，JSON 最大深度为 32。外部导入的 manifest/layout/window 字符串超长或 Canvas 控件数超过 256 会拒绝导入，不会静默截断或丢弃控件。Canvas 控件数达到 160 开始给出 warning。

图片资源在复制或导入前会校验扩展名、文件大小和像素尺寸。Canvas 背景图限制为 1 MiB、长边 4096、像素 4096×4096；普通 UI 图片限制为 512 KiB、长边 2048、像素 2048×2048；包内未知用途图片按包资源入口限制处理并仍需能安全解码。超限图片会整体拒绝，不会复制进包或生成 `bpui://` URI。

## 22. legacy 关系和迁移路线图

旧 SettingPage `.bpui` 导入导出是 legacy。新的 v3 package manager 将替代它用于 Designer v3 布局。

legacy 包检测：

1. 没有有效 `manifest.json`。
2. 存在 `Config.json`、`CustomUi/` 或 `FrontElementsConfig/`。

legacy 转换是后续阶段，不在 Phase 9A 实现。Phase 9A 不修改旧导入导出流程，不实现 legacy conversion。
Phase 9F 起，`FrontManagePage` 导入 legacy `.bpui` 时会先询问是否转换。转换器会安全解压旧 zip 到 staging，复制 `CustomUi/` 资源到 `resources/images/`，生成 `manifest.json`，并从当前内置 v3 布局起步应用旧 `ElementInfo` 几何覆盖。旧 `Config.json` 只读取明确可映射的前台图片字段，不会写入 `%APPDATA%/neo-bpsys-wpf/Config.json`，也不会复制到新包或 AppData。未知旧布局文件只产生 warning 并跳过；如果没有任何可映射布局，转换失败并显示错误。转换后的包再走现有 v3 importer，因此安装、重复 PackageId 替换、资源隔离和激活行为与普通 v3 包一致。

路线图：

| 阶段 | 范围 |
| --- | --- |
| Phase 9A | 文档和规格 only。 |
| Phase 9B.0: Canvas Properties GUI, local bpui resource normalization, toolbar cleanup, and Window Options foundation | 已实现 Canvas Properties GUI、本地 `bpui://local` 图片规范化、`bpui://` resolver、工具栏二级菜单和窗口级 `AllowTransparency` 选项基础。 |
| Phase 9B.1: FrontManagePage Layout Package Manager UI skeleton | 已新增 `FrontManagePage` 的 `Frontend Windows` / `Layout Packages` 顶层 tabs、Layout Packages UI skeleton、布局包列表服务基础和活动包状态文件读取/写入骨架。独立编辑器入口保留在 `Frontend Windows` 页，不单独占用 tab。导入、导出、legacy 转换和资源打包仍未实现。 |
| Phase 9C | 已实现 v3 `.bpui` 包导出、导出 manifest 对话框、All Frontend Layouts 导出范围、资源收集/重写和包管理器 UI 打磨。导出包不会包含 `Config.json`、`CustomUi/` 或 `FrontElementsConfig/`；`Resources/...` 与 `pack://application:,,,/...` 保持原样，`bpui://local/...`、其他已安装包资源和绝对路径资源会复制到包内并重写为当前 `PackageId`。导入/安装仍未实现。 |
| Phase 9D | 已实现 v3 包导入、安装、激活复制到用户布局目录、删除普通包和删除活动包时切回内置。导出范围固定为全部前台布局。 |
| Phase 9E | legacy 包转换入口设计和更细的迁移提示。 |
| Phase 9F | 已实现 legacy 到 v3 转换器、转换确认入口、CustomUi 资源复制、旧位置几何覆盖和转换后 v3 importer 安装。 |
| Phase 13A | 文档和 schema：插件前台控件 `ControlType` 命名、Canvas `RequiredPlugins`、manifest `PluginDependencies`、缺失插件导入策略和安全边界。 |
| Phase 13B | 已实现插件前台控件 registry、描述符 API、通用 plugin config roundtrip 和 runtime renderer 缺失插件跳过。 |
| Phase 13C | 已实现 Designer 插件控件支持，包括 Add Control、属性元数据、Canvas `RequiredPlugins` 同步和缺失插件占位符。 |
| Phase 13C.5 | 示例插件清理，验证插件控件作者体验。 |
| Phase 13D | 已实现 `.bpui` 依赖扫描、导入、导出和强制导入删除缺失控件；新增 DEBUG-only 示例前台控件插件。 |
| Phase 13E | 插件市场交互式安装 / 更新引导。 |
| Phase 13F | 安全、版本兼容和自动测试收口。 |
