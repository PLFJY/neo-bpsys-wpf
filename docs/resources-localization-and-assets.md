# 资源、本地化与素材

## 资源分类

项目中有两类容易混淆的资源：

| 目录 | 构建方式 | 用途 |
| --- | --- | --- |
| `neo-bpsys-wpf/Assets` | 多数作为 WPF `Resource` 嵌入程序集 | 应用图标、首页图、字体、主题图标等 pack URI 资源 |
| `neo-bpsys-wpf/Resources` | `<None Include="Resources\**" CopyToOutputDirectory="PreserveNewest" />` | 运行时文件资源，按文件路径读取 |

`GameRule.json` 单独设置为 `CopyToOutputDirectory=Always`，输出到应用基目录，供 `GameGuidanceService` 读取。

## Resources 目录

常见子目录：

| 目录 | 用途 |
| --- | --- |
| `bpui` | 前台窗口 UI 背景、比分图、锁图、阵营图标 |
| `data` | `CharacterList.json` 及多语言角色列表 |
| `FrontedDefaultPositions` | 内置前台窗口默认布局 |
| `SmartBpDefaultConfigs` | SmartBP 默认区域配置 |
| `surBig/surHalf/surHeader/surHeader_singleColor` | 求生者不同展示尺寸/样式图片 |
| `hunBig/hunHalf/hunHeader/hunHeader_singleColor` | 监管者不同展示尺寸/样式图片 |
| `map/map_singleColor/map_square` | 地图图片 |
| `talent` / `trait` | 天赋和辅助特质图标 |

`ImageHelper` 使用 `AppConstants.ResourcesPath` 拼接这些目录，按文件路径加载。新增运行时图片时，应确认文件被放在 `Resources` 下并能复制到输出目录。

CutScene v3 默认布局位于 `Resources/FrontedLayouts/CutSceneWindow/BaseCanvas.json`，背景使用 `Resources/cutScene.png`（解析到运行目录 `Resources/bpui/cutScene.png`）。GameData v3 默认布局位于 `Resources/FrontedLayouts/GameDataWindow/BaseCanvas.json`，背景使用 `Resources/gameData_withText.png`（解析到运行目录 `Resources/bpui/gameData_withText.png`）。WidgetsWindow v3 是多 Canvas 布局，默认文件为 `Resources/FrontedLayouts/WidgetsWindow/MapBpCanvas.json`、`Resources/FrontedLayouts/WidgetsWindow/BpOverViewCanvas.json`、`Resources/FrontedLayouts/WidgetsWindow/MapV2Canvas.json`，背景分别使用 `Resources/mapBp.png`、`Resources/bpOverview.png`、`Resources/mapBpV2.png`。BpWindow v3 默认布局位于 `Resources/FrontedLayouts/BpWindow/BaseCanvas.json`，背景使用 `Resources/bp.png`。内置业务控件复用这些资源目录：`TalentTraitDisplay` 通过 `ImageHelper.GetTalentImageSource` / `GetTraitImageSource` 读取 `Resources/talent` 和 `Resources/trait`，并跟随 `CutSceneWindowSettings.IsBlackTalentAndTraitEnable` 切换黑白图标；`CurrentBanDisplay` 读取角色 `HeaderImageSingleColor` 并使用 WidgetsWindow 设置中的 `CurrentBanLockImage`；`BanSlotDisplay` 读取当前局/全局 Ban 角色 `HeaderImageSingleColor` 并使用 BpWindow 设置中的 `CurrentBanLockImage` / `GlobalBanLockImage`；`PickingBorderOverlay` 使用 BpWindow 设置中的 `PickingBorderImage` 和 `PickingBorderBrush`；`MapV2Display` 复用现有 `MapV2Presenter` 和 Map BP v2 设置。不要在 v3 JSON 中硬编码单个天赋、辅助特质、Ban 锁覆盖层或 Map BP v2 展示控件内部图片路径。

## Designer v3 资源 URI

Designer v3 layout 和 `.bpui v3` 包标准允许以下资源 URI 形式，完整包规格见 [bpui-package-v3.md](bpui-package-v3.md)。

| 形式 | 含义 |
| --- | --- |
| `Resources/foo.png` | 内置前台文件资源，解析到运行目录 `Resources/bpui/foo.png`。 |
| `pack://application:,,,/Assets/Fonts/#Noto Sans` | WPF app pack resource，主要用于内置字体或 app-bundled asset。 |
| `bpui://local/resources/images/foo.png` | 编辑器本地资源命名空间，用于用户选择本地图片后的持久副本。 |
| `bpui://{PackageId}/resources/images/foo.png` | 已安装布局包资源，按包目录隔离。 |
| `bpui://{PackageId}/resources/fonts/font.ttf#FontFamilyName` | 预留的包内字体 URI 形式，`#` 后为字体族名。 |

绝对路径只应作为编辑时临时输入。Phase 9B.0 的 Canvas Properties GUI 在用户选择本地背景图片后，会复制文件到本地资源目录，并在 layout JSON 中写入 `bpui://local/...`。Phase 9C 的 `.bpui v3` 导出会把引用到的 `bpui://local/...`、其他已安装包资源和绝对路径资源复制进导出包，并重写为 `bpui://{PackageId}/...`；缺失的绝对路径资源会让导出失败并显示错误。`Resources/...` 和 `pack://application:,,,/...` 属于应用内置资源，导出时保持原样，不复制进包内。

Phase 9B.0 起，`FrontedResourceResolver` 支持 `bpui://local/resources/images/foo.png` 和 `bpui://{PackageId}/resources/images/foo.png`，并拒绝不安全 `PackageId`、绝对路径和路径穿越。缺失文件按 unresolved 处理，不抛出异常。

Phase 10 起，图片进入本地资源、包导入、包导出或 resolver 解码前都会走安全校验。支持扩展名为 png、jpg、jpeg、bmp、gif、webp、ico、tif、tiff。Canvas 背景图最大 1 MiB、长边 4096、像素 4096×4096；控件 UI 图片最大 512 KiB、长边 2048、像素 2048×2048。超限或无法安全解码的图片会被拒绝：本地资源不会复制，`BackgroundImage` / 编辑缓冲不会更新，resolver 运行时返回 `null` 并记录 warning，预览和前台不会因为坏图崩溃。Resource Browser 缩略图也使用安全解码，超限图片不做完整加载。

推荐本地资源目录：

```text
%APPDATA%/neo-bpsys-wpf/FrontedLayoutPackages/local/resources/images/
```

已安装包资源必须按包隔离，不能合并进共享全局目录：

```text
%APPDATA%/neo-bpsys-wpf/FrontedLayoutPackages/{PackageId}/resources/
```

删除普通布局包时，应删除整个 `%APPDATA%/neo-bpsys-wpf/FrontedLayoutPackages/{PackageId}/` 目录，从而删除该包资源。不要只根据 manifest 逐个删除资源文件。`builtin` 是内置布局/资源的虚拟包 ID，`local` 是编辑器本地资源命名空间，二者都不能通过普通包删除流程删除。

Phase 9D 的导入校验会拒绝跨包资源引用和 `bpui://local/...` 引用。包 `package-a` 中的布局可以引用 `bpui://package-a/...`、`Resources/...`、`pack://application:,,,/...`；不应引用 `bpui://package-b/...`。导出前存在的 `bpui://local/...` 必须重写为导出包自己的 `PackageId`。导入安装时资源保持在 `%APPDATA%/neo-bpsys-wpf/FrontedLayoutPackages/{PackageId}/resources/`，不会合并到共享目录。

Phase 9F 的 legacy `.bpui` 转换会把旧 `CustomUi/` 中的图片复制到转换后包的 `resources/images/`，并在 manifest 的 `Content.Resources` 中记录 `Kind = Image` 和 `Sha256`。如果旧 `Config.json` 的明确前台图片字段指向这些文件，转换后的布局会改写为 `bpui://{PackageId}/resources/images/...`；缺失或无法安全映射的旧资源只产生 warning，不写入全局 `CustomUi`。

## Assets 与字体

字体位于：

```text
neo-bpsys-wpf/Assets/Fonts
```

设置默认字体使用 pack URI，例如：

```text
pack://application:,,,/Assets/Fonts/#Noto Sans
pack://application:,,,/Assets/Fonts/#华康POP1体W5
pack://application:,,,/Assets/Fonts/#汉仪第五人格体简
```

`Settings.TextSettings.FontFamily` 会根据 `FontFamilySite` 创建 `FontFamily`。新增字体时要同时确认：

1. `.ttf` 被加入 csproj 的 `Resource Include`。
2. pack URI 路径正确。
3. `#` 后的字体族名称和字体文件内部名称一致。
4. 设置页 `_systemFonts` 如需固定展示该字体，也要加入对应 `FontFamily`。

## 本地化资源

本地化文件：

```text
Locales/Lang.resx
Locales/Lang.en-us.resx
Locales/Lang.ja-jp.resx
Locales/Lang.Designer.cs
```

csproj 使用 `PublicResXFileCodeGenerator` 生成 designer，并把 resx 作为 `EmbeddedResource`。

XAML 常见写法：

```xaml
Text="{lex:Loc SomeKey}"
```

后台代码常见写法：

```csharp
I18nHelper.GetLocalizedString("SomeKey")
```

`I18nHelper` 找不到 key 时返回原始 key，便于界面降级显示和定位缺失翻译。新增用户可见文本时至少添加默认 `Lang.resx`，并尽量补齐英文、日文资源，避免用户看到裸 key。

Designer v3 的显示层本地化统一使用 `Designer.*` key 前缀。代码侧通过 `IFrontedDesignerLocalizationService` 访问，WPF 宿主实现再委托 `I18nHelper.GetLocalizedString(key)`；Core 中的默认实现只返回原始值，避免 Core 反向引用 WPF 项目。常用命名包括 `Designer.Property.*`、`Designer.PropertyGroup.*`、`Designer.ControlType.*`、`Designer.Option.{Property}.{Value}`、`Designer.Window.*`、`Designer.Canvas.*`、`Designer.Binding.*` 和 `Designer.BindingType.*`。

这些 key 只影响编辑器 UI 显示，不改变布局文件。`.bpui` / v3 JSON 中的 schema 字段名、控件 `Name`、`ControlType`、`BindingPath`、资源 URI 和 `FontFamily` 仍写入原始契约值；例如中文界面 ComboBox 显示“居中”，保存仍是 `"HorizontalAlignment": "Center"`。Binding Browser 可以显示本地化节点名，但界面必须保留原始路径，选择结果也必须写回原始 `BindingPath`。

`GameProgressText` 使用集中 helper 和资源 key 生成 `FREE GAME`、`GAME {n} FIRST HALF`、`GAME {n} OVERTIME SECOND HALF` 等文本，避免 BO3/BO5 进度文案散落在窗口 XAML 或 JSON 中。默认是单行文本；`WidgetsWindow/BpOverViewCanvas.json` 使用 `UseLineBreak=true` 把 Game / Overtime 和 half 分为两行。`MapNameText` 默认把 `CurrentGame.PickedMap` 枚举名作为本地化 key 查询地图名，也可以通过 `BindingPath` 指向其他地图字段，例如 WidgetsWindow 的 picked / banned map 名称；新增地图时要同步补齐地图资源 key。`LocalizedText` 用 `LocalizationKey` 查询普通 resx 文案，适合 GameData 表头等静态标签；如果 key 缺失会显示 `FallbackText` 或 key 本身。普通 `Text.Text` 仍是原样静态文本，不会自动本地化。

## 添加新素材

添加或替换前台素材时：

1. 确认素材属于嵌入 `Assets` 还是输出 `Resources`。
2. 如果代码用 `ImageHelper.GetUiImageSource("bp")`，文件应在 `Resources/bpui/bp.png`。
3. 如果代码用 `ImageSourceKey.surHalf`，文件应在 `Resources/surHalf/{name}.png`。
4. 旧 XAML-first 默认位置文件命名必须匹配 `{WindowTypeName}Config-{CanvasName}.default.json`；v3 默认布局使用 `Resources/FrontedLayouts/{WindowTypeName}/{CanvasName}.json`。
5. v3 JSON 中 `Resources/xxx.png` 会解析到运行目录 `Resources/bpui/xxx.png`，新增默认背景时要确认对应文件存在于 `Resources/bpui` 并会复制到输出目录。
6. SmartBP 默认配置文件名和 `SmartBpGameDataSceneDefinition` 中的相对路径一致。

## 常见坑

1. 放进 `Assets` 的文件不会自动成为 `ResourcesPath` 下的运行时文件。
2. 放进 `Resources` 的文件不会自动有 pack URI。
3. 字体文件名和字体族名称不一定一致，pack URI 的 `#` 后面要用字体族名称。
4. 缺少 `CopyToOutputDirectory` 会导致本地调试可见、发布后缺文件。
5. 某语言 resx 缺 key 时界面可能显示 key 本身。
6. 自定义 UI 图片路径保存在设置中，重置窗口配置可能删除对应自定义图片文件。
