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

CutScene v3 默认布局位于 `Resources/FrontedLayouts/CutSceneWindow/BaseCanvas.json`，背景使用 `Resources/cutScene.png`（解析到运行目录 `Resources/bpui/cutScene.png`）。内置业务控件复用这些资源目录：`TalentTraitDisplay` 通过 `ImageHelper.GetTalentImageSource` / `GetTraitImageSource` 读取 `Resources/talent` 和 `Resources/trait`，并跟随 `CutSceneWindowSettings.IsBlackTalentAndTraitEnable` 切换黑白图标。不要在 v3 JSON 中硬编码单个天赋或辅助特质图片路径。

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

`GameProgressText` 使用集中 helper 和资源 key 生成 `FREE GAME`、`GAME {n} FIRST HALF`、`GAME {n} OVERTIME SECOND HALF` 等文本，避免 BO3/BO5 进度文案散落在窗口 XAML 或 JSON 中。`MapNameText` 把 `CurrentGame.PickedMap` 枚举名作为本地化 key 查询地图名；新增地图时要同步补齐地图资源 key。

## 添加新素材

添加或替换前台素材时：

1. 确认素材属于嵌入 `Assets` 还是输出 `Resources`。
2. 如果代码用 `ImageHelper.GetUiImageSource("bp")`，文件应在 `Resources/bpui/bp.png`。
3. 如果代码用 `ImageSourceKey.surHalf`，文件应在 `Resources/surHalf/{name}.png`。
4. 旧 XAML-first 默认位置文件命名必须匹配 `{WindowTypeName}Config-{CanvasName}.default.json`；v3 默认布局使用 `Resources/FrontedLayouts/{WindowTypeName}/{CanvasName}.json`。
5. SmartBP 默认配置文件名和 `SmartBpGameDataSceneDefinition` 中的相对路径一致。

## 常见坑

1. 放进 `Assets` 的文件不会自动成为 `ResourcesPath` 下的运行时文件。
2. 放进 `Resources` 的文件不会自动有 pack URI。
3. 字体文件名和字体族名称不一定一致，pack URI 的 `#` 后面要用字体族名称。
4. 缺少 `CopyToOutputDirectory` 会导致本地调试可见、发布后缺文件。
5. 某语言 resx 缺 key 时界面可能显示 key 本身。
6. 自定义 UI 图片路径保存在设置中，重置窗口配置可能删除对应自定义图片文件。
