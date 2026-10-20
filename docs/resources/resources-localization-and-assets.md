# 图片、字体与运行时素材

本文负责素材归属、复制、图片加载与缓存。本地化字典及 resx 契约统一见[localization.md](localization.md)，包内 URI、安全校验与资源隔离统一见 [BPUI](../frontend/bpui-package-v3.md)。源码入口：[ImageHelper](../../neo-bpsys-wpf.Core/Helpers/ImageHelper.cs)、[资源 resolver](../../neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedResourceResolver.cs)、[主项目](../../neo-bpsys-wpf/neo-bpsys-wpf.csproj)。

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

内置布局与行为目录的路径契约见[布局模型](../frontend/fronted-layout-v3-window-centric.md)。Image/BorderedImage、overlay、MapV2 和文本控件的语义统一见[控件运行时](../frontend/fronted-designer-v3.md)，不在素材目录说明中维护第二套控件规格。

## 图片导入与解码

图片进入本地资源、包导入、包导出或 resolver 解码前都会走安全校验。支持扩展名为 png、jpg、jpeg、bmp、gif、webp、ico、tif、tiff。Canvas 背景图最大 2.5 MiB、长边 4096、像素 4096×4096；控件 UI 图片最大 1 MiB、长边 2048、像素 2048×2048。超限或无法安全解码的图片默认会被拒绝：本地资源不会复制，`BackgroundImage` / 编辑缓冲不会更新，resolver 运行时返回 `null` 并记录 warning，预览和前台不会因为坏图崩溃。Resource Browser 缩略图也使用安全解码，超限图片不做完整加载。通过队伍 Logo、成员照片或 Designer v3 的本地选图、资源浏览器、路径输入并应用超限图片时，会额外提供“压缩并应用”选项；用户确认后仅在临时副本中使用 SkiaSharp 等比缩放、重编码，原图片保持不变，压缩成功后才写入本地资源或应用到队伍信息。

`ImagePath`、`PickingBorderImagePath`、`BanLockImagePath`、`BorderImagePath`、`LockImageSource` 等控件级图片字段、动画图片部件以及队伍 LOGO 按普通 UI 图片限制校验；`BackgroundImage` 按背景图限制校验。Designer 的 Resource Browser 选择普通图片控件静态图时写入 `ImagePath`，不会写入 `BindingPath`。资源浏览器允许选中体积或尺寸超限的本地图片，但不加载其缩略图；应用选择后，由图片属性、画布背景或动画部件的导入流程按实际用途提示压缩，并以同一用途执行压缩、存储与运行时校验。动画图片部件直接输入绝对路径后点击应用，也通过该导入流程保存为本地资源 URI。既有未指定用途的本地资源存储 API 保留背景图限制。


URI 和 local/包目录规则见 [BPUI 资源规范](../frontend/bpui-package-v3.md)。Designer pending resource 的撤销/放弃清理规则见[编辑器](../frontend/fronted-designer-editor.md)。普通导入默认校验；用户确认压缩后才操作暂存副本、更新资源摘要，源文件不变。legacy 显式转换的压缩规则见[迁移](../frontend/legacy-fronted-layout-migration.md)。


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

## 模块资产

SmartBP OCR 不维护模块内角色别名表；OCR 只解析区域和槽位，角色名匹配统一交给 `ICharacterSelectionService` / `CharacterSelectionService`，并且匹配时必须限定在传入阵营内，不得跨阵营查询。Tesseract traineddata 属于托管模型资产，固定下载到 SmartBP 模块目录的 `OCRModels/Tesseract/tessdata/`。RapidOCR profile 由 `Resources/SmartBp/RapidOcrModelManifest.json` 声明，安装到 `OCRModels/RapidOCR/Models/{profileId}/`。中、日、英模型及字典的完整 ModelScope 地址摘自 RapidOCR 官方 `python/rapidocr/default_models.yaml`；模型 SHA-256 使用官方值，字典则固定校验官方文件内容，不得在代码中拼接地址。模块内下载经 `SmartBpParallelDownload` 适配到宿主 `IFileDownloadService`，暂停或取消后保留旁路分片供下次续传。AppData 只保存 SmartBP 配置，不保存托管模型文件。

RapidOCR manifest 的 `version` 和每个资产的下载契约共同生成安装指纹；安装目录内的 `.smartbp-install.json` 用于判断当前模型是否落后于随模块发布的 manifest。更新官方模型条目时必须同步提升版本或更新资产契约，并更新对应测试。


本地化资源族、字典归属、Designer 显示层与不变量保存值见[本地化规范](localization.md)。


## 添加新素材

添加或替换前台素材时：

1. 确认素材属于嵌入 `Assets` 还是输出 `Resources`。
2. 如果代码用 `ImageHelper.GetUiImageSource("bp")`，文件应在 `Resources/bpui/bp.png`。
3. 如果代码用 `ImageSourceKey.surHalf`，文件应在 `Resources/surHalf/{name}.png`。
4. 新默认布局写 Resources/FrontedLayouts；旧默认位置文件只用于历史转换，不作为新增素材的运行时入口。
5. v3 JSON 中 `Resources/xxx.png` 会解析到运行目录 `Resources/bpui/xxx.png`，新增默认背景时要确认对应文件存在于 `Resources/bpui` 并会复制到输出目录。
6. SmartBP 新增 OCR 模型或测试资源时，确认其位于模块 `Resources` 下并会复制到模块输出目录；赛后数据 OCR 不再依赖独立的区域配置文件。

## 常见坑

1. 放进 `Assets` 的文件不会自动成为 `ResourcesPath` 下的运行时文件。
2. 放进 `Resources` 的文件不会自动有 pack URI。
3. 字体文件名和字体族名称不一定一致，pack URI 的 `#` 后面要用字体族名称。
4. 缺少 `CopyToOutputDirectory` 会导致本地调试可见、发布后缺文件。
5. 某语言 resx 缺 key 时界面可能显示 key 本身。
6. 布局图片与字体按包或 local 命名空间保存，普通设置重置不应删除包资源。

## 缓存边界

FrontedResourceResolver 同时限制 256 项和约 128 MiB 解码像素，BackgroundImageTintProcessor 同时限制 32 项和约 64 MiB，均按 LRU 驱逐。超大单项及活跃视觉引用可能使总进程占用高于缓存预算，不能把预算当整个应用内存上限。染色动画中间态不应全部塞入长期缓存；变更文件后要让元数据失效，关闭/换布局释放视觉引用。诊断步骤见[内存诊断](../build/memory-baseline-and-diagnostics.md)。
