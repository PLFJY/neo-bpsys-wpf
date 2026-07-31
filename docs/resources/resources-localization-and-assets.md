# 资源、本地化与素材

## 资源分类

项目中有两类容易混淆的资源：

| 目录 | 构建方式 | 用途 |
| --- | --- | --- |
| `neo-bpsys-wpf/Assets` | 多数作为 WPF `Resource` 嵌入程序集 | 应用图标、首页图、字体、主题图标等 pack URI 资源 |
| `neo-bpsys-wpf/Resources` | `<None Include="Resources\**" CopyToOutputDirectory="PreserveNewest" />` | 运行时文件资源，按文件路径读取 |
| `neo-bpsys-wpf.SmartBp.Module/Resources` | `<Content Include="Resources\**" CopyToOutputDirectory="PreserveNewest" />` | SmartBP 模块自有运行时资源，随模块复制到模块输出目录 |

`GameRule.json` 单独设置为 `CopyToOutputDirectory=Always`，输出到应用基目录，供 `GameGuidanceService` 读取。

## Resources 目录

常见子目录：

| 目录 | 用途 |
| --- | --- |
| `bpui` | 前台窗口 UI 背景、比分图、锁图、阵营图标 |
| `data` | `CharacterList.json` 及多语言角色列表 |
| `FrontedLayouts` | v3 Window-centric 内置前台窗口默认布局 |
| `FrontedBehaviors` | 内置前台窗口默认行为与动画图 |
| `surBig/surHalf/surHeader/surHeader_singleColor` | 求生者不同展示尺寸/样式图片 |
| `hunBig/hunHalf/hunHeader/hunHeader_singleColor` | 监管者不同展示尺寸/样式图片 |
| `map/map_singleColor/map_square` | 地图图片 |
| `talent` / `trait` | 天赋和辅助特质图标 |

`ImageHelper` 使用 `AppConstants.ResourcesPath` 拼接这些目录，按文件路径加载。新增运行时图片时，应确认文件被放在 `Resources` 下并能复制到输出目录。

SmartBP 模块自有资源不放在主程序 `neo-bpsys-wpf/Resources` 下，应放在 `neo-bpsys-wpf.SmartBp.Module/Resources` 并由模块项目复制到输出目录。当前包括 `Resources/SmartBp` 下的 RapidOCR 模型 manifest、内置测试帧、BP 识别区域默认配置、OCR 角色别名，以及 `Resources/SmartBpDefaultConfigs` 下的赛后数据 OCR 默认区域配置。模型管理 UI 从相应 OCR manifest 动态读取 profile。

v3 默认布局采用 Window-centric 一级路径，位于 `Resources/FrontedLayouts/{WindowTypeName}.json`。每个 v3 layout window 运行时固定生成 `ViewBox -> Canvas BaseCanvas`，Canvas 不再是资源路径或包管理单位。CutScene 背景使用 `Resources/cutScene.png`（解析到运行目录 `Resources/bpui/cutScene.png`），GameData 背景使用 `Resources/gameData.png`，BpWindow 背景使用 `Resources/bp.png`。`WidgetsWindow` 和 MapV1 已删除；旧 `BpOverViewCanvas` 迁移为 `BpOverviewWindow.json`，旧 `MapV2Canvas` 迁移为 `MapV2Window.json`，MapV2 背景继续使用 `Resources/mapBpV2.png`。内置业务控件复用这些资源目录：`TalentTraitDisplay` 通过 `ImageHelper.GetTalentImageSource` / `GetTraitImageSource` 读取 `Resources/talent` 和 `Resources/trait`，以图标 alpha 作为遮罩应用 `Color` 单色覆盖，默认且必填为白色；Ban 位默认布局使用通用 `Image` 绑定角色 `HeaderImageSingleColor`，锁定覆盖层优先使用 `LockImagePath`，为空时回退内置锁图；pick 呼吸边框优先使用 `PickingBorderImagePath`，为空时回退内置 BP 选择边框图；`CurrentBanDisplay`、`BanSlotDisplay` 和 `PickingBorderOverlay` 已移除；`MapV2Display` 复用现有 `MapV2Presenter` 并使用 v3 运行时默认样式。旧 Config.json 中可映射的图片会迁移到 v3 layout，旧前台设置不再作为 active Settings 运行时来源。

普通图片展示有两个内置控件类型：`Image` 和 `BorderedImage`。`Image` 是通用图片控件，`Canvas.Left` / `Canvas.Top` / `Width` / `Height` / `ZIndex` 作用于承载主图和内部 overlay 的根元素。`BorderedImage` 是外层 `Border` + 内部图片层，适合需要外层容器裁剪、外框 resize 或内层对齐控制的图片区域，例如角色 pick 图。两者的图片路径解析规则相同：`BindingPath` 绑定到 `ISharedDataService` 上的动态 `ImageSource`，`ImagePath` 保存静态资源图片路径。`BindingPath` 非空时优先使用绑定并忽略 `ImagePath`；`BindingPath` 为空且 `ImagePath` 非空时，运行时按 v3 资源 resolver 加载静态图。两者也共享 `Lockable` 和 `PickingBorderAvailable` overlay 资源路径解析；锁定图和选择边框可以分别使用独立 Stretch，关闭对应独立设置时跟随主图 Stretch。

CutScene 默认布局的图片结构以 `v2.1.1+af0a4be` 旧 XAML 为准：`SurTeamLogo` / `HunTeamLogo` 保持 direct `Image`；`Map`、`SurPick0` 到 `SurPick3`、`HunPick` 使用 `BorderedImage` 复现旧 `Border -> Image`。其中 SurPick0-3 角色大图显式设置 `ImageWidth=556.5`、`ImageHeight=null`，由内层 `Image` 的 `UniformToFill`、水平居中、顶部对齐和外层 `ClipToBounds` 共同决定裁剪；HunPick 仍不设置内层图片宽高。

## Designer v3 资源 URI

Designer v3 layout 和 `.bpui v3` 包标准允许以下资源 URI 形式，完整包规格见 [bpui-package-v3.md](../frontend/bpui-package-v3.md)。

| 形式 | 含义 |
| --- | --- |
| `Resources/foo.png` | 内置前台文件资源，解析到运行目录 `Resources/bpui/foo.png`。 |
| `pack://application:,,,/Assets/Fonts/#Noto Sans` | WPF app pack resource，主要用于内置字体或 app-bundled asset。 |
| `bpui://local/resources/images/foo.png` | 编辑器本地资源命名空间，用于用户选择本地图片后的持久副本。 |
| `bpui://{PackageId}/resources/images/foo.png` | 已安装布局包资源，按包目录隔离。 |
| `bpui://{PackageId}/resources/fonts/font.ttf#FontFamilyName` | 包内字体 URI，`#` 后为字体族名。 |

绝对路径只应作为编辑时临时输入。的 Canvas Properties GUI 在用户选择本地背景图片后，会复制文件到本地资源目录，并在 layout JSON 中写入 `bpui://local/...`。的 `.bpui v3` 导出会把引用到的 `bpui://local/...`、其他已安装包资源和绝对路径资源复制进导出包，并重写为 `bpui://{PackageId}/...`；缺失的绝对路径资源会让导出失败并显示错误。`Resources/...` 和 `pack://application:,,,/...` 属于应用内置资源，导出时保持原样，不复制进包内。

Designer v3 中通过 Resource Browser 选择 Canvas 背景、`ScoreGlobalWindow/BaseCanvas` 的 BO3 背景、以及 `ImagePath` / `BorderImagePath` / `LockImageSource` 等静态资源字段时会立即应用到当前内存布局、记录 undo、标记 dirty 并刷新预览；手动输入文本仍需要 Enter 或 Apply。选择本地文件或 Resource Browser 返回绝对文件路径时，会先复制到本地资源目录并写入 `bpui://local/...`，不会把绝对路径保存到 layout。立即复制产生的新文件会记录为当前编辑会话的 pending resource：保存时保留仍被当前或其他已保存布局引用的文件并清理未引用文件；放弃修改、切换布局选择“不保存”或关闭窗口选择“不保存”时，会尽力删除本会话新建且未被任何已保存布局引用的文件。undo/redo 不会立即删除 pending resource，以便 redo 可以恢复引用。

起，`FrontedResourceResolver` 支持 `bpui://local/resources/images/foo.png` 和 `bpui://{PackageId}/resources/images/foo.png`，并拒绝不安全 `PackageId`、绝对路径和路径穿越。缺失文件按 unresolved 处理，不抛出异常。

起，图片进入本地资源、包导入、包导出或 resolver 解码前都会走安全校验。支持扩展名为 png、jpg、jpeg、bmp、gif、webp、ico、tif、tiff。Canvas 背景图最大 2.5 MiB、长边 4096、像素 4096×4096；控件 UI 图片最大 1 MiB、长边 2048、像素 2048×2048。超限或无法安全解码的图片会被拒绝：本地资源不会复制，`BackgroundImage` / 编辑缓冲不会更新，resolver 运行时返回 `null` 并记录 warning，预览和前台不会因为坏图崩溃。Resource Browser 缩略图也使用安全解码，超限图片不做完整加载。

`ImagePath`、`PickingBorderImagePath`、`BanLockImagePath`、`BorderImagePath`、`LockImageSource` 等控件级图片字段以及队伍 LOGO 按普通 UI 图片限制校验；`BackgroundImage` 按背景图限制校验。Designer 的 Resource Browser 选择普通图片控件静态图时写入 `ImagePath`，不会写入 `BindingPath`。

推荐本地资源目录：

```text
%APPDATA%/neo-bpsys-wpf/FrontedLayoutPackages/local/resources/images/
%APPDATA%/neo-bpsys-wpf/FrontedLayoutPackages/local/resources/fonts/
```

已安装包资源必须按包隔离，不能合并进共享全局目录：

```text
%APPDATA%/neo-bpsys-wpf/FrontedLayoutPackages/{PackageId}/resources/
```

删除普通布局包时，应删除整个 `%APPDATA%/neo-bpsys-wpf/FrontedLayoutPackages/{PackageId}/` 目录，从而删除该包资源。不要只根据 manifest 逐个删除资源文件。`builtin` 是内置布局/资源的虚拟包 ID，`local` 是编辑器本地资源命名空间，二者都不能通过普通包删除流程删除。

的导入校验会拒绝跨包资源引用和 `bpui://local/...` 引用。包 `package-a` 中的布局可以引用 `bpui://package-a/...`、`Resources/...`、`pack://application:,,,/...`；不应引用 `bpui://package-b/...`。导出前存在的 `bpui://local/...` 必须重写为导出包自己的 `PackageId`。导入安装时资源保持在 `%APPDATA%/neo-bpsys-wpf/FrontedLayoutPackages/{PackageId}/resources/`，不会合并到共享目录。

的 legacy `.bpui` 转换会把旧 `CustomUi/` 中的图片复制到转换后包的 `resources/images/`，并在 manifest 的 `Content.Resources` 中记录 `Kind = Image` 和 `Sha256`。如果旧 `Config.json` 的明确前台图片字段指向这些文件，转换后的布局会改写为 `bpui://{PackageId}/resources/images/...`；缺失或无法安全映射的旧资源只产生 warning，不写入全局 `CustomUi`。

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

legacy `LegacyTextSettings.FontFamily` 会根据 `FontFamilySite` 创建 `FontFamily`，仅供旧 Config.json 迁移和旧工具控件兼容使用。新增字体时要同时确认：

1. `.ttf` 被加入 csproj 的 `Resource Include`。
2. pack URI 路径正确。
3. `#` 后的字体族名称和字体文件内部名称一致。
4. 设置页 `_systemFonts` 如需固定展示该字体，也要加入对应 `FontFamily`。

Designer v3 的字体属性下拉会把当前活动布局包 `resources/fonts/` 中的字体列在最上方，并用蓝色 BPUI 标记区分。用户从字体属性导入 `.ttf`、`.otf` 或 `.ttc` 时，字体会复制到当前可写布局包，布局字段保存为 `bpui://{PackageId}/resources/fonts/...#FontFamilyName`。这些字体不会进入系统字体或全局资源库，切换布局包后需要在新包重新导入；导出 `.bpui` 时会把被布局引用的包内字体一起打包。字体属性旁的“管理本包字体”入口只列出当前活动布局包的 `resources/fonts/`，未被当前包布局引用的字体文件可以删除；仍被引用的字体会禁用删除，需要先把对应布局属性改成其他字体。

## 本地化资源

本地化资源已从单一 `Locales/Lang*.resx` 拆分为按功能域拥有的资源族。完整规则、字典选择、XAML/C# 引用模式、模块归属、文化与回退、审计方法等见 [localization.md](localization.md)。

主程序本地化文件（每个族均包含 neutral + `.en-us` + `.ja-jp`）：

```text
Locales/Common.resx        Locales/Shell.resx       Locales/Team.resx
Locales/Game.resx          Locales/Bp.resx          Locales/Score.resx
Locales/FrontManage.resx   Locales/Designer.resx    Locales/AnimationEditor.resx
Locales/Settings.resx      Locales/PluginMarket.resx
```

模块自有本地化文件：

```text
neo-bpsys-wpf.ProductTour/Locales/Tour.resx (+ .en-us, .ja-jp)
neo-bpsys-wpf.SmartBp.Module/Locales/SmartBp.resx (+ .en-us, .ja-jp)
```

所有 resx 作为 `EmbeddedResource` 嵌入各自程序集，不再使用 `PublicResXFileCodeGenerator` 或 `Lang.Designer.cs`。迁移前的三种文化快照与 key map 由 `neo-bpsys-wpf.Tests/TestData/I18nMigration/` 追踪，测试不得依赖被忽略的 `artifacts/` 目录。

XAML 常见写法（需指定 `DefaultAssembly` 和 `DefaultDictionary`）：

```xaml
lex:ResxLocalizationProvider.DefaultAssembly="neo-bpsys-wpf"
lex:ResxLocalizationProvider.DefaultDictionary="neo_bpsys_wpf.Locales.Shell"
Text="{lex:Loc SomeKey}"
```

后台代码常见写法（需指定字典常量）：

```csharp
I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "SomeKey")
```

前台布局控件等无法预先确定归属字典的场景使用全量查找：

```csharp
I18nHelper.GetLocalizedStringFromAnyHostDictionary("SomeKey")
```

`I18nHelper` 找不到 key 时返回原始 key，便于界面降级显示和定位缺失翻译。新增用户可见文本时至少添加对应功能族 neutral resx，并尽量补齐 `.en-us`、`.ja-jp`，避免用户看到裸 key。禁止重新引入单一 `Lang.resx`。

SmartBP OCR 不维护模块内角色别名表；OCR 只解析区域和槽位，角色名匹配统一交给 `ICharacterSelectionService` / `CharacterSelectionService`，并且匹配时必须限定在传入阵营内，不得跨阵营查询。Tesseract traineddata 属于托管模型资产，固定下载到 SmartBP 模块目录的 `OCRModels/Tesseract/tessdata/`。RapidOCR profile 由 `Resources/SmartBp/RapidOcrModelManifest.json` 声明，安装到 `OCRModels/RapidOCR/Models/{profileId}/`。中、日、英模型及字典的完整 ModelScope 地址摘自 RapidOCR 官方 `python/rapidocr/default_models.yaml`；模型 SHA-256 使用官方值，字典则固定校验官方文件内容，不得在代码中拼接地址。模块内下载经 `SmartBpParallelDownload` 适配到宿主 `IFileDownloadService`，暂停或取消后保留旁路分片供下次续传。AppData 只保存 SmartBP 配置，不保存托管模型文件。

RapidOCR manifest 的 `version` 和每个资产的下载契约共同生成安装指纹；安装目录内的 `.smartbp-install.json` 用于判断当前模型是否落后于随模块发布的 manifest。更新官方模型条目时必须同步提升版本或更新资产契约，并更新对应测试。

Designer v3 的显示层本地化统一使用 `Designer.*` key 前缀，归属 `Locales.Designer` 字典。代码侧通过 `IFrontedDesignerLocalizationService` 访问，WPF 宿主实现再委托 `I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, key)`；Core 中的默认实现只返回原始值，避免 Core 反向引用 WPF 项目。常用命名包括 `Designer.Property.*`、`Designer.PropertyGroup.*`、`Designer.ControlType.*`、`Designer.Option.{Property}.{Value}`、`Designer.Window.*`、`Designer.Canvas.*`、`Designer.Binding.*` 和 `Designer.BindingType.*`。

后，常用命名还包括 `Designer.Value.*`、`Designer.Editor.*` 和 `Designer.Validation.*`，用于只读布尔值、Binding Browser / Resource Browser 和属性校验提示。

这些 key 只影响编辑器 UI 显示，不改变布局文件。`.bpui` / v3 JSON 中的 schema 字段名、控件 `Name`、`ControlType`、`BindingPath`、资源 URI 和 `FontFamily` 仍写入原始契约值；例如中文界面 ComboBox 显示“居中”，保存仍是 `"HorizontalAlignment": "Center"`。Binding Browser 可以显示本地化节点名，但界面必须保留原始路径，选择结果也必须写回原始 `BindingPath`。Resource Browser 可以显示本地化来源和类型，但选中区域必须保留原始资源 URI 或文件路径。

`GameProgressText` 使用集中 helper 和资源 key 生成 `FREE GAME`、`GAME {n} FIRST HALF`、`GAME {n} OVERTIME SECOND HALF` 等文本，避免 BO3/BO5 进度文案散落在窗口 XAML 或 JSON 中。默认是单行文本（`DisplayMode = Inline`）；正式预设包括单行、双行、横排局数、横排半场、竖排、竖排双行、竖排局数、竖排半场。`DisplayLanguage = FollowApp` 时按 `WPFLocalizeExtension` 的当前应用语言生成文本，中文应用语言下应显示中文局数和半场文案。`MapNameText` 默认把 `CurrentGame.PickedMap` 枚举名作为本地化 key 查询地图名（地图 key 归属 `Locales.Game`），也可以通过 `BindingPath` 指向其他地图字段，例如当前对局的 picked / banned map 数据；新增地图时要同步补齐地图资源 key。`LocalizedText` 用 `LocalizationKey` 经 `I18nHelper.GetLocalizedStringFromAnyHostDictionary` 查询普通 resx 文案，适合 GameData 表头等静态标签；如果 key 缺失会显示 `FallbackText` 或 key 本身。普通 `Text.Text` 仍是原样静态文本，不会自动本地化。

## 添加新素材

添加或替换前台素材时：

1. 确认素材属于嵌入 `Assets` 还是输出 `Resources`。
2. 如果代码用 `ImageHelper.GetUiImageSource("bp")`，文件应在 `Resources/bpui/bp.png`。
3. 如果代码用 `ImageSourceKey.surHalf`，文件应在 `Resources/surHalf/{name}.png`。
4. 旧 XAML-first 默认位置文件命名必须匹配 `{WindowTypeName}Config-{CanvasName}.default.json`（`CanvasName` 是旧多 Canvas 概念）；v3 默认布局使用 `Resources/FrontedLayouts/{WindowTypeName}.json`。
5. v3 JSON 中 `Resources/xxx.png` 会解析到运行目录 `Resources/bpui/xxx.png`，新增默认背景时要确认对应文件存在于 `Resources/bpui` 并会复制到输出目录。
6. SmartBP 新增 OCR 模型或测试资源时，确认其位于模块 `Resources` 下并会复制到模块输出目录；赛后数据 OCR 不再依赖独立的区域配置文件。

## 常见坑

1. 放进 `Assets` 的文件不会自动成为 `ResourcesPath` 下的运行时文件。
2. 放进 `Resources` 的文件不会自动有 pack URI。
3. 字体文件名和字体族名称不一定一致，pack URI 的 `#` 后面要用字体族名称。
4. 缺少 `CopyToOutputDirectory` 会导致本地调试可见、发布后缺文件。
5. 某语言 resx 缺 key 时界面可能显示 key 本身。
6. 自定义 UI 图片路径保存在设置中，重置窗口配置可能删除对应自定义图片文件。
