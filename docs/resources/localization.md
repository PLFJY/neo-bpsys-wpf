# 本地化架构

本文档描述 `neo-bpsys-wpf` 的本地化（i18n）资源架构，包括资源族划分、归属规则、XAML/C# 引用模式、模块归属、文化与回退、审计方法，以及禁止事项。

## 概述

项目使用 [WPFLocalizeExtension](https://github.com/KarnaughTrial/WPFLocalizeExtension) 3.10.0 进行本地化。本地化资源已从单一 `Locales/Lang*.resx` 拆分为按功能域拥有的资源族，每个 key 有且仅有一个归属字典。所有 resx 作为 `EmbeddedResource` 嵌入各自程序集，不再使用 `PublicResXFileCodeGenerator` 或 `Lang.Designer.cs`。

## 宿主资源族

主程序（`neo-bpsys-wpf` 程序集）拥有以下 12 个资源族，均位于 `neo-bpsys-wpf/Locales/` 目录下，每个族包含 neutral + `.en-us` + `.ja-jp` 三个文件：

| 字典常量 (`AppI18nDictionaries`) | 文件前缀 | 覆盖域 |
| --- | --- | --- |
| `Common` | `Locales.Common` | 真正跨多域共用的通用键（如 OK、Cancel、Save） |
| `Shell` | `Locales.Shell` | 外壳/导航层（MainWindow、页面导航、首选项） |
| `Team` | `Locales.Team` | 队伍信息页 |
| `Game` | `Locales.Game` | 对局进度、地图名、GameData 显示 |
| `Bp` | `Locales.Bp` | BP 流程（Ban/Pick） |
| `Score` | `Locales.Score` | 比分页 |
| `FrontManage` | `Locales.FrontManage` | 前台管理页、布局包管理 |
| `Designer` | `Locales.Designer` | 前台设计器（Designer.* 前缀键） |
| `AnimationEditor` | `Locales.AnimationEditor` | 动画编辑器 |
| `Settings` | `Locales.Settings` | 设置页 |
| `PluginMarket` | `Locales.PluginMarket` | 插件市场 |
| `TourContent` | `Locales.TourContent` | Tutorial 步骤内容（标题/描述/对话台词）；en-us/ja-jp 暂为中文占位；排除出迁移审计 |

`AppI18nDictionaries` 类提供所有字典名常量和 `AllDictionaries` 数组。代码中引用字典名时必须使用常量，不得硬编码字符串。

## 模块自有资源

独立程序集拥有自己的资源族，不依赖宿主 `Locales.*` 字典：

| 程序集 | 字典 | 路径 | 覆盖域 |
| --- | --- | --- | --- |
| `neo-bpsys-wpf.ProductTour` | `Locales.Tour` | `neo-bpsys-wpf.ProductTour/Locales/Tour*.resx` | 产品导览框架 chrome（Next/Previous/Skip/Finish 等） |
| `neo-bpsys-wpf.SmartBp.Module` | `Locales.SmartBp` | `neo-bpsys-wpf.SmartBp.Module/Locales/SmartBp*.resx` | SmartBP 模块内 UI 文案 |

模块通过各自程序集的 `ResourceManager` 或 `LocalizeDictionary` 解析自有资源，XAML 中设置 `DefaultAssembly` 为模块程序集名。

## 归属规则

为 key 分配归属字典时，按以下优先级判断：

1. **程序集归属**：模块自有 UI 文案归属模块字典，不放入宿主。
2. **功能域使用**：key 被单一功能域使用时归属该域字典。
3. **XAML/View 路径**：key 被某 View/ViewModel 使用时归属该 View 所在域。
4. **key 前缀**：如 `Designer.*` 归属 Designer，`GameProgress*` 归属 Game。
5. **值文案**：以上均无法判断时，按文案语义归入最接近的域。

`Common` 仅收录真正跨多个不相关域且语义相同的键，保持小而精。不得把仅被一个域使用的键放入 `Common`。

### 动态 key

动态 key（通过 `.ToString()`、变量拼接等方式生成的 key）需要枚举完整可能 key 集合并分配归属字典。已知动态模式：

- `MapNameDisplayHelper`：`Map` 枚举名（ArmsFactory、TheRedChurch 等）→ `Locales.Game`
- `GameProgressDisplayHelper`：`FirstHalf` / `SecondHalf` → `Locales.Game`
- 前台布局 `LocalizedText` 控件：`LocalizationKey` 来自布局 JSON，可指向任意域 → 使用 `GetLocalizedStringFromAnyHostDictionary` 全量查找

## XAML 引用模式

每个 XAML 根元素需设置 `DefaultAssembly` 和 `DefaultDictionary`：

```xaml
<Window lex:ResxLocalizationProvider.DefaultAssembly="neo-bpsys-wpf"
        lex:ResxLocalizationProvider.DefaultDictionary="neo_bpsys_wpf.Locales.Shell">
    <TextBlock Text="{lex:Loc SomeKey}" />
</Window>
```

`DefaultDictionary` 必须是程序集嵌入资源的完整基名，而非代码侧的短字典常量。主程序资源使用
`neo_bpsys_wpf.Locales.<Family>`；SmartBP 模块也使用 `neo_bpsys_wpf.Locales.SmartBp`，因为其
项目根命名空间为 `neo_bpsys_wpf`。短名 `Locales.<Family>` 仅适用于 `I18nHelper`，直接用于
`lex:Loc` 会导致界面显示 `Key: ...`。

当某 key 归属不同字典时，在该元素上显式指定：

```xaml
<TextBlock Text="{lex:Loc CommonKey}"
           lex:ResxLocalizationProvider.DefaultDictionary="neo_bpsys_wpf.Locales.Common" />
```

模块 XAML 设置 `DefaultAssembly` 为模块程序集名（如 `neo-bpsys-wpf.ProductTour`）。SmartBP 模块通过
`SmartBpLocalizationProvider` 将模块程序集中的资源暴露为模块内短字典名 `Locales.SmartBp`；该短名是
专用 Provider 的契约，不适用于宿主 `ResxLocalizationProvider`，也不得替换为宿主功能字典。

### ResourceDictionary 样式文件

`DefaultAssembly` / `DefaultDictionary` 是附加属性（attached property），只能设置在 `DependencyObject` 上。`ResourceDictionary` 是 `DispatcherObject` 而非 `DependencyObject`，因此**禁止**在 `<ResourceDictionary>` 根元素上设置这两个属性，否则会在 BAML 加载时抛出 `ArgumentException: Object of type 'System.Windows.ResourceDictionary' cannot be converted to type 'System.Windows.DependencyObject'`。

纯样式 `ResourceDictionary` 文件（如 `Controls/*Style.xaml`）应将这两个属性设置到 `ControlTemplate` 内的根元素上（`Grid` / `StackPanel` 等 `DependencyObject`），附加属性以 `Inherits` 方式向子元素传播：

```xaml
<ResourceDictionary xmlns:lex="..." ...>
    <Style TargetType="{x:Type local:MyControl}">
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type local:MyControl}">
                    <Grid lex:ResxLocalizationProvider.DefaultAssembly="neo-bpsys-wpf"
                          lex:ResxLocalizationProvider.DefaultDictionary="neo_bpsys_wpf.Locales.Game">
                        <TextBlock Text="{lex:Loc SomeKey}" />
                    </Grid>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
</ResourceDictionary>
```

## C# 引用模式

已知归属字典的调用使用显式字典常量：

```csharp
I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "SomeKey")
I18nHelper.GetLocalizedString(AppI18nDictionaries.Game, "GameProgressFree", culture)
```

无法预先确定归属字典的场景（如前台布局控件按配置 `LocalizationKey` 解析任意域文本）使用全量查找：

```csharp
I18nHelper.GetLocalizedStringFromAnyHostDictionary("SomeKey")
I18nHelper.GetLocalizedStringFromAnyHostDictionary("SomeKey", culture)
```

`I18nHelper` 解析顺序：
1. 先尝试 `LocalizeDictionary.Instance.GetLocalizedObject`（WPF 运行时由 XAML 初始化 provider）
2. 若返回 null（如未配置 provider 的测试上下文），回退到从程序集嵌入资源创建的 `ResourceManager` 读取
3. 仍找不到时返回原始 key

`GetLocalizedStringFromAnyHostDictionary` 遍历 `AppI18nDictionaries.AllDictionaries` 依次查找，返回首个命中。由于每个 key 有且仅有一个归属字典，不存在歧义。

## 文化与回退

支持的文化：

| 文件后缀 | 文化 | 用途 |
| --- | --- | --- |
| （无后缀） | neutral（简体中文） | 默认/回退 |
| `.en-us` | en-US | 英文 |
| `.ja-jp` | ja-JP | 日文 |

回退行为：当请求的文化资源不存在时，`ResourceManager` 自动回退到 neutral。例如请求 `zh-CN` 时找不到 `Game.zh-CN.resx`，会回退到 `Game.resx`（neutral）。

## csproj 配置

所有 resx 作为 `EmbeddedResource` 嵌入。文化文件通过 `DependentUpon` 嵌套在 neutral 文件下：

```xml
<EmbeddedResource Update="Locales\Score.en-us.resx">
    <DependentUpon>Score.resx</DependentUpon>
</EmbeddedResource>
```

不再使用 `PublicResXFileCodeGenerator`、`Lang.Designer.cs` 或任何生成器条目。

## 审计

### 审计测试

`neo-bpsys-wpf.Tests/Services/I18nResourceAuditTest.cs` 包含 23 项审计测试，覆盖：

- 迁移完整性（key-map 总数、每条 key 存在于目标字典、无本地化孤儿 key、无 key 丢失）
- 字典归属（每个 key 仅一个宿主 owner、无空字典、Common 不重复进入功能字典）
- 资源族结构（每个文化文件有 neutral 对应、仅支持 en-us/ja-jp 后缀、resx XML 合法）
- 查找行为（helper 拒绝空参数、已知 key 可解析、未知 key 返回自身、neutral 回退）
- 源码清理（无 XAML/C# 引用 `Locales.Lang`、无 `Lang.Designer` 项目引用、旧单参数 helper 已移除）
- 模块归属（ProductTour 不依赖宿主 Lang、SmartBP 模块拥有资源、模块不解析宿主功能字典）

### key-map.csv

`neo-bpsys-wpf.Tests/TestData/I18nMigration/key-map.csv` 记录每个 key 的迁移映射（Key, SourceDictionary, TargetAssembly, TargetDictionary, ReferenceCount, ReferenceDomains, MappingReason, IsDynamic），是可从干净检出独立验证的归属决策基线。

### 运行审计

```powershell
dotnet test .\neo-bpsys-wpf.slnx --filter "FullyQualifiedName~I18nResourceAuditTest"
```

## 禁止事项

1. **禁止重新引入单一 `Lang.resx`**：所有新 key 必须放入对应功能域字典。
2. **禁止硬编码字典名字符串**：使用 `AppI18nDictionaries` 常量。
3. **禁止使用 `PublicResXFileCodeGenerator` 或 designer 类**：资源通过 `I18nHelper` 或 `lex:Loc` 访问。
4. **禁止跨模块引用**：模块不得引用宿主 `Locales.*` 字典，反之亦然。
5. **禁止在 `Common` 放入仅被一个域使用的 key**：`Common` 只收录真正跨域共用的键。
