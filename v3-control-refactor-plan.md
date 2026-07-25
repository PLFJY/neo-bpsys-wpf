# Designer V3 Control 完整重构方案

## 1. 重构目标

本次重构的核心不是给现有插件 Contributor 再包一层语法糖，而是重新建立一套统一的 Designer V3 Control 模型，使内置控件和插件控件最终共用同一套：

* 注册模型；
* 属性模型；
* XAML Binding 模型；
* JSON 存储模型；
* Designer PropertyGrid；
* 根控件布局；
* 内部可编辑部件；
* 模板子控件集合；
* 样式继承；
* 同类型样式传播；
* 缺失插件兼容；
* XAML 和纯 C# 创建方式。

最终插件作者只需要完成三件事：

```text
1. 用 Attribute 声明控件元数据
2. 在 code-behind 中静态声明 Designer 属性和内部部件
3. 用 XAML 或纯 C# 实现控件视觉效果
```

插件初始化只保留：

```csharp
services.AddFrontedV3Control<TeamCardControl>();
```

---

# 2. 必须遵守的边界

## 2.1 JSON 结构不变

`Options` 只是运行时、Designer 和 Visual Studio 使用的属性命名空间，不能进入 JSON。

现有布局继续使用根级字段：

```json
{
  "ControlType": "plugin:plfjy.ExamplePlugin/TeamCard",
  "Left": 100,
  "Top": 80,
  "Width": 260,
  "Height": 96,
  "TextColor": "#FFFFFFFF",
  "TeamName": "Team A",
  "LogoWidth": 64,
  "LogoHeight": 64
}
```

禁止改成：

```json
{
  "Options": {
    "Appearance": {
      "TextColor": "#FFFFFFFF"
    }
  }
}
```

现有 `FrontedControlConfigBase` 已经定义了根控件的坐标、尺寸、层级、可见性、BehaviorGuid 和模糊效果，这些字段必须继续保持当前 JSON 语义。

插件未知字段继续通过 `PluginFrontedControlConfig.ExtensionData` 保留，保证插件未安装时也不会丢失控件配置。

---

## 2.2 Options 是属性投影，不是数据副本

例如：

```text
Options.Appearance.TextColor
```

映射到 JSON：

```text
TextColor
```

```text
Options.Content.TeamName
```

映射到 JSON：

```text
TeamName
```

因此每个 V3 属性必须拥有两个不同概念：

```csharp
OptionsPath
StorageAccessor
```

其中：

* `OptionsPath` 服务于 XAML Binding、Designer 分组和 IDE 联想；
* `StorageAccessor` 服务于当前 JSON 结构的读取和写入。

Options 不能保存一份脱离 Config 的独立值，否则会产生双数据源问题。

---

## 2.3 根控件布局完全由 Designer V3 Runtime 管理

V3 Control 本身不负责：

```text
Left
Top
Width
Height
ZIndex
Visibility
GaussianBlur
BehaviorGuid
Designer Selection
Resize Handles
Canvas Placement
```

这些全部由宿主的 `FrontedV3ControlHost` 处理。

控件作者只需要考虑：

> 给我一个指定大小的矩形区域，我应该在这个区域内显示什么。

当前内置控件工厂还需要自行执行 `Canvas.SetLeft`、`Canvas.SetTop`、`Panel.SetZIndex` 和 Width/Height 设置。

重构后必须删除控件内部的这类职责。

---

# 3. 插件作者最终 API

## 3.1 控件元数据 Attribute

```csharp
[FrontedV3Control(
    "TeamCard",
    "Team Card",
    DescriptionKey = "ExamplePlugin.TeamCard.Description",
    Icon = "PeopleTeam")]
public partial class TeamCardControl : FrontedV3ControlBase
{
}
```

Attribute 建议定义：

```csharp
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class FrontedV3ControlAttribute : Attribute
{
    public FrontedV3ControlAttribute(
        string id,
        string name)
    {
        Id = id;
        Name = name;
    }

    public string Id { get; }

    public string Name { get; }

    public string? DisplayNameKey { get; set; }

    public string? Description { get; set; }

    public string? DescriptionKey { get; set; }

    public string? Icon { get; set; }

    public bool IsBuiltIn { get; set; }

    public int DisplayOrder { get; set; } = int.MaxValue;
}
```

插件控件默认：

```csharp
IsBuiltIn = false
```

内置控件显式声明：

```csharp
[FrontedV3Control(
    "Text",
    "Text",
    IsBuiltIn = true)]
```

---

## 3.2 控件注册

```csharp
services.AddFrontedV3Control<TeamCardControl>();
```

注册扩展只负责：

* 读取 `FrontedV3ControlAttribute`；
* 从插件注册上下文获得 PackageId；
* 执行静态字段初始化；
* 扫描属性、Part、PartCollection 和传播 Profile；
* 注册控件类型到 DI；
* 创建统一的 `FrontedV3ControlRegistration`。

插件作者不能手动传入：

```text
PackageId
CanonicalControlType
属性 descriptor
CreateControl delegate
Config factory
```

插件控件最终 ID：

```text
plugin:plfjy.ExamplePlugin/TeamCard
```

内置控件最终 ID：

```text
Text
```

继续复用当前 `plugin:<PackageId>/<ControlTypeName>` 身份规则。

---

# 4. 属性声明模型

## 4.1 类似 DependencyProperty，但更简单

属性必须放在控件自己的 code-behind 中：

```csharp
public static readonly FrontedV3Property<string>
    TextColorProperty =
        FrontedV3Property.Register<
            TeamCardControl,
            string>(
            storage: "TextColor",
            optionsPath: "Appearance.TextColor",
            defaultValue: "#FFFFFFFF",
            editor: FrontedPropertyEditorKind.Color,
            semantic: FrontedV3PropertySemantic.Appearance,
            transfer: FrontedV3PropertyTransfer.SameControlType);
```

另一个属性：

```csharp
public static readonly FrontedV3Property<string>
    TeamNameProperty =
        FrontedV3Property.Register<
            TeamCardControl,
            string>(
            storage: "TeamName",
            optionsPath: "Content.TeamName",
            defaultValue: "Team",
            editor: FrontedPropertyEditorKind.Text,
            bindingTarget: FrontedBindingTargetKind.Text,
            semantic: FrontedV3PropertySemantic.Content);
```

插件作者不需要：

* CLR wrapper；
* `GetValue` / `SetValue`；
* `FrameworkPropertyMetadata`；
* PropertyChanged callback；
* 单独 Config 类；
* 单独 property descriptor；
* Contributor；
* CreateDefaultConfig。

---

## 4.2 属性定义结构

```csharp
public abstract class FrontedV3PropertyDefinition
{
    public required Type OwnerType { get; init; }

    public required string OptionsPath { get; init; }

    public required Type ValueType { get; init; }

    public required object? DefaultValue { get; init; }

    public required IFrontedV3StorageAccessor Storage { get; init; }

    public required FrontedV3PropertyMetadata Metadata { get; init; }
}
```

```csharp
public sealed class FrontedV3Property<T>
    : FrontedV3PropertyDefinition
{
}
```

元数据：

```csharp
public sealed class FrontedV3PropertyMetadata
{
    public FrontedPropertyEditorKind? EditorKind { get; init; }

    public FrontedBindingTargetKind BindingTargetKind { get; init; }

    public FrontedV3PropertySemantic Semantic { get; init; }

    public FrontedV3PropertyInheritance Inheritance { get; init; }

    public FrontedV3PropertyTransfer Transfer { get; init; }

    public string? DisplayNameKey { get; init; }

    public string? DescriptionKey { get; init; }

    public double? Minimum { get; init; }

    public double? Maximum { get; init; }

    public bool IsVisible { get; init; } = true;

    public bool IsReadOnly { get; init; }
}
```

---

## 4.3 Options 命名空间

建议内置约定：

```text
Appearance
Content
Data
Behavior
Advanced
```

含义：

| Namespace    | 用途                         |
| ------------ | -------------------------- |
| `Appearance` | 颜色、字体、边框、透明度、圆角、图片 Stretch |
| `Content`    | 文本、图片路径、标签内容               |
| `Data`       | TeamType、MapKey、数据源选择      |
| `Behavior`   | 控件自身的显示行为设置                |
| `Advanced`   | 低频、高级或调试属性                 |

禁止：

```text
Options.Layout
Options.Geometry
Options.Position
```

根布局由 Runtime 所有，控件不能重新声明。

---

# 5. Storage Accessor

JSON 不变的关键在于 Storage Accessor。

```csharp
public interface IFrontedV3StorageAccessor
{
    object? GetValue(
        FrontedControlConfigBase config,
        FrontedV3StorageContext context);

    void SetValue(
        FrontedControlConfigBase config,
        FrontedV3StorageContext context,
        object? value);
}
```

需要至少支持三种存储方式。

## 5.1 插件 ExtensionData Storage

插件声明：

```csharp
storage: "TextColor"
```

实际读写：

```csharp
PluginFrontedControlConfig.ExtensionData["TextColor"]
```

保存 JSON：

```json
"TextColor": "#FFFFFFFF"
```

---

## 5.2 内置 CLR Property Storage

内置控件已有：

```csharp
MapNameColor
FontSize
ImageWidth
```

可以使用强类型表达式：

```csharp
FrontedV3Storage.Clr<MapV2DisplayControlConfig, string?>(
    x => x.MapNameColor)
```

保存结构不变。

---

## 5.3 Collection Item Storage

用于：

```text
MapV2.InternalParts[Part == MapName].Width
GlobalScore.Cells[Id == Game1FirstHalf].Color
```

由 Collection Storage 定位目标 Item 后再读取字段。

---

# 6. Options Runtime 投影

## 6.1 控件运行上下文

```csharp
public sealed class FrontedV3ControlContext
{
    public required FrontedV3OptionsView Options { get; init; }

    public required ISharedDataService SharedData { get; init; }

    public required IFrontedResourceResolver Resources { get; init; }

    public required IServiceProvider Services { get; init; }

    public required string WindowId { get; init; }

    public required string ControlName { get; init; }

    public required bool IsDesignerPreview { get; init; }
}
```

## 6.2 基类

```csharp
public abstract class FrontedV3ControlBase : UserControl
{
    public FrontedV3ControlContext Context
    {
        get;
        private set;
    } = null!;

    public FrontedV3OptionsView Options =>
        Context.Options;

    internal void InitializeFrontedV3(
        FrontedV3ControlContext context)
    {
        Context = context;
        DataContext = context;
        OnFrontedV3Initialized();
    }

    protected virtual void OnFrontedV3Initialized()
    {
    }
}
```

XAML：

```xml
<TextBlock
    Foreground="{Binding Options.Appearance.TextColor}"
    Text="{Binding Options.Content.TeamName}" />
```

Options View 不能复制 Config 值。

它必须在读取和写入时直接通过：

```text
PropertyDefinition
    → StorageAccessor
    → 当前 Config
```

实现单一事实来源。

---

# 7. XAML 与纯 C# 控件统一

## 7.1 XAML

```xml
<fronted:FrontedV3ControlBase
    x:Class="neo_bpsys_wpf.ExamplePlugin.TeamCardControl"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:fronted="clr-namespace:neo_bpsys_wpf.Core.Controls;assembly=neo-bpsys-wpf.Core">

    <Border Padding="12">
        <TextBlock
            Foreground="{Binding Options.Appearance.TextColor}"
            Text="{Binding Options.Content.TeamName}" />
    </Border>
</fronted:FrontedV3ControlBase>
```

## 7.2 纯 C#

```csharp
[FrontedV3Control("StatusBadge", "Status Badge")]
public sealed class StatusBadgeControl
    : FrontedV3ControlBase
{
    public static readonly FrontedV3Property<string>
        TextProperty =
            FrontedV3Property.Register<
                StatusBadgeControl,
                string>(
                "Text",
                "Content.Text",
                "Status");

    public StatusBadgeControl()
    {
        var text = new TextBlock();

        text.SetBinding(
            TextBlock.TextProperty,
            new Binding("Options.Content.Text"));

        Content = text;
    }
}
```

两者注册完全相同：

```csharp
services.AddFrontedV3Control<StatusBadgeControl>();
```

---

# 8. FrontedV3ControlHost

Renderer 不应再直接把插件控件作为 Canvas 根元素。

结构：

```text
Canvas
└── FrontedV3ControlHost
    └── FrontedV3ControlBase
```

Host 统一负责：

* `Canvas.Left`；
* `Canvas.Top`；
* Width；
* Height；
* ZIndex；
* Visibility；
* Gaussian Blur；
* BehaviorGuid；
* Runtime generated marker；
* 注册 NameScope；
* Designer selection；
* 根控件移动；
* 根控件缩放；
* 错误边界；
* 缺失插件占位。

当前 Renderer 在调用工厂后统一设置 Visibility、Behavior 标记、NameScope 和效果包装。

这部分应进一步收敛到 Host。

Host 应隔离控件异常：

```text
构造失败
Initialize 失败
XAML 加载失败
Binding 异常
Part 扫描失败
```

插件控件失败不得导致整个前台窗口或 Designer 崩溃。

---

# 9. 固定内部部件模型

固定内部部件用于：

* `BorderedImage.Image`；
* `MapV2.TeamName`；
* `MapV2.MapCard`；
* `MapV2.MapName`；
* `MapV2.CampName`；
* `MapV2.PickingBorder`；
* 插件控件内固定 Logo、标题或内容区域。

## 9.1 Part Definition

```csharp
public static readonly FrontedV3PartDefinition
    LogoPart =
        FrontedV3Part.Register<TeamCardControl>(
            id: "Logo",
            displayName: "Logo",
            geometry:
                FrontedV3PartGeometry.Flat(
                    xStorage: "LogoX",
                    yStorage: "LogoY",
                    widthStorage: "LogoWidth",
                    heightStorage: "LogoHeight"),
            capabilities:
                FrontedV3PartCapabilities.Move |
                FrontedV3PartCapabilities.Resize);
```

## 9.2 XAML 标记

推荐使用 Attached Property，因为 C# Attribute 无法直接标记 XAML 中的任意内部元素：

```xml
<Image
    x:Name="PART_Logo"
    fronted:FrontedV3.PartId="Logo"
    Source="{Binding Options.Content.LogoSource}" />
```

也可支持字段或属性上的 Attribute：

```csharp
[FrontedV3PartVisual("Logo")]
public FrameworkElement LogoElement => _logo;
```

最终两种写法都解析成同一个 Part Visual。

---

## 9.3 Part Geometry

Part 的坐标相对于父控件。

```csharp
public sealed class FrontedV3PartGeometry
{
    public required IFrontedV3GeometryStorage Storage { get; init; }

    public FrontedV3PartCapabilities Capabilities { get; init; }
}
```

Capabilities：

```csharp
[Flags]
public enum FrontedV3PartCapabilities
{
    None = 0,
    Move = 1,
    Resize = 2,
    EditProperties = 4,
    Animate = 8
}
```

如果 Part 只声明 Width 和 Height：

```csharp
geometry:
    FrontedV3PartGeometry.Flat(
        widthStorage: "ImageWidth",
        heightStorage: "ImageHeight")
```

则只能缩放，不能移动。

---

# 10. BorderedImage 迁移

当前 `BorderedImageFrontedControlConfig` 使用：

```text
ImageWidth
ImageHeight
```

来调整内部 Image 的尺寸。

Designer 目前为它维护单独的 Resize Target 和专用 resize 方法。

迁移后：

```csharp
public static readonly FrontedV3PartDefinition
    ImagePart =
        FrontedV3Part.Register<
            BorderedImageControl>(
            id: "Image",
            geometry:
                FrontedV3PartGeometry.Flat(
                    widthStorage: "ImageWidth",
                    heightStorage: "ImageHeight"),
            capabilities:
                FrontedV3PartCapabilities.Resize);
```

JSON 仍然：

```json
{
  "ImageWidth": 100,
  "ImageHeight": 100
}
```

Designer 不再包含任何 `BorderedImage` 特判。

---

# 11. MapV2 固定 Part 迁移

当前 MapV2 使用：

```csharp
List<MapV2InternalPartLayoutConfig> InternalParts
```

每个项包含：

```text
Part
X
Y
Width
Height
```

注册为五个固定 Part：

```text
TeamName
MapCard
MapName
CampName
PickingBorder
```

例如：

```csharp
public static readonly FrontedV3PartDefinition
    TeamNamePart =
        FrontedV3Part.Register<MapV2Control>(
            id: "TeamName",
            geometry:
                FrontedV3PartGeometry.CollectionItem(
                    collectionStorage: "InternalParts",
                    keyStorage: "Part",
                    keyValue:
                        MapV2InternalStylePart.TeamName),
            capabilities:
                FrontedV3PartCapabilities.Move |
                FrontedV3PartCapabilities.Resize |
                FrontedV3PartCapabilities.EditProperties);
```

Part 自身的 Options：

```text
Options.Appearance.FontFamily
Options.Appearance.FontWeight
Options.Appearance.Color
Options.Appearance.FontSize
```

但 Storage 对应现有根级字段：

```text
TeamNameFontFamily
TeamNameFontWeight
TeamNameColor
TeamNameFontSize
```

MapName Part 使用相同的 Options Path，但映射：

```text
MapNameFontFamily
MapNameFontWeight
MapNameColor
MapNameFontSize
```

这使不同 Part 在 Designer 和 XAML 中拥有统一语义，同时完全保持现有 JSON。

当前 MapV2 工厂仍需手工将 Config 属性应用到 Presenter，并单独应用 `InternalParts`。

这部分应由统一 Part 和 Property Binding Runtime 接管。

---

# 12. 模板子控件集合

固定 Part 无法表达 `GlobalScoreRow.Cells`，因此需要 Part Collection。

```csharp
public static readonly FrontedV3PartCollection<
    GlobalScoreCellConfig>
    Cells =
        FrontedV3Parts.RegisterCollection<
            GlobalScoreRowControl,
            GlobalScoreCellConfig>(
            storage: "Cells",
            key: item => item.Id,
            geometry:
                FrontedV3ItemGeometry.Create(
                    x: item => item.X,
                    y: item => item.Y,
                    width: item => item.Width,
                    height: item => item.Height),
            policy:
                FrontedV3PartCollectionPolicy.FixedTemplate,
            capabilities:
                FrontedV3PartCapabilities.Move |
                FrontedV3PartCapabilities.Resize |
                FrontedV3PartCapabilities.EditProperties);
```

Collection Policy：

```csharp
public enum FrontedV3PartCollectionPolicy
{
    FixedTemplate,
    Dynamic,
    ReadOnly
}
```

## FixedTemplate

适用于 ScoreGlobal：

* 子项由模板规则保证完整；
* 可以移动；
* 可以缩放；
* 可以编辑；
* 不能随意删除；
* 不能额外新增；
* 可以根据 BO3/BO5 补齐缺失项。

## Dynamic

适用于插件卡片列表：

* 可添加；
* 可删除；
* 可复制；
* 可排序；
* 可独立编辑。

## ReadOnly

运行时生成，只允许查看或选择。

现有 `GlobalScoreRowControlConfig.Cells` 已经具备稳定 ID、相对坐标、尺寸、可见性和独立样式覆盖，适合直接接入该模型。

---

# 13. 父样式继承

子项属性需要支持父级继承。

```csharp
public enum FrontedV3PropertyInheritance
{
    None,

    ParentFallback,

    CopyFromParentOnCreate,

    LockedToParent
}
```

## ParentFallback

```text
子项有值 → 使用子项
子项为空 → 读取父级相同 Options Path
```

适用于 Score Cell：

```text
Options.Appearance.FontFamily
Options.Appearance.FontWeight
Options.Appearance.Color
Options.Appearance.FontSize
Options.Appearance.ShowCampIcon
```

现有 `GlobalScoreCellConfig` 正是通过 nullable 字段表达父级 fallback。

## CopyFromParentOnCreate

创建子项时复制父级值，以后允许单独修改。

## LockedToParent

子项始终使用父级值，Designer 不允许创建覆盖。

---

# 14. 父样式批量操作

当 Part Collection 中存在继承属性时，Designer 自动提供：

```text
将父控件外观应用到全部子项
清除全部子项外观覆盖
```

框架按相同 `OptionsPath` 自动匹配：

```text
Parent Options.Appearance.FontFamily
    ↓
Child Options.Appearance.FontFamily
```

不允许再在 Designer ViewModel 中硬编码：

```csharp
cell.FontFamily = row.FontFamily;
cell.FontWeight = row.FontWeight;
cell.Color = row.Color;
```

当前 ScoreGlobal 的父样式应用和清空覆盖逻辑就是逐字段硬编码。

---

# 15. 同类型控件样式传播

该能力用于 MapV2 一类重复控件。

建议命名：

```text
Peer Style Transfer
同类型样式传播
```

而不是 `IsMultiable`。

## 15.1 属性级传播

```csharp
transfer:
    FrontedV3PropertyTransfer.SameControlType
```

```csharp
public enum FrontedV3PropertyTransfer
{
    None,

    SameControlType
}
```

只传播给 Canonical ControlType 相同的控件：

```text
plugin:a/TeamCard
```

不能传播给：

```text
plugin:b/TeamCard
```

---

## 15.2 Profile 级传播

```csharp
public static readonly FrontedV3StyleTransferProfile
    StyleTransferProfile =
        FrontedV3StyleTransfer.Register<
            MapV2Control>(
            scope:
                FrontedV3StyleTransferScope.SameControlType,
            components:
                FrontedV3StyleComponent.AppearanceProperties |
                FrontedV3StyleComponent.RootSize |
                FrontedV3StyleComponent.PartLayout |
                FrontedV3StyleComponent.Behaviors);
```

Components：

```csharp
[Flags]
public enum FrontedV3StyleComponent
{
    None = 0,

    AppearanceProperties = 1,

    RootSize = 2,

    PartLayout = 4,

    Behaviors = 8,

    Effects = 16
}
```

默认只传播：

```text
AppearanceProperties
```

必须显式开启：

```text
RootSize
PartLayout
Behaviors
Effects
```

永不默认传播：

```text
Left
Top
ZIndex
Control Name
MapKey
TeamType
BindingPath
数据身份字段
```

当前 MapV2 的“应用到全部”会手工复制几十个属性、尺寸、InternalParts 和 Behavior。

重构后必须由 `FrontedV3StyleTransferService` 根据 Schema 完成。

---

# 16. 统一 Selection 模型

当前 Designer 分别维护：

* `SelectedDesignItem`；

* `SelectedGlobalScoreCell`；

* `SelectedMapV2InternalStylePart`；

* `BorderedImageResizeTarget`。

统一为：

```csharp
public sealed class FrontedV3DesignSelection
{
    public required FrontedControlDesignItem RootControl
    {
        get;
        init;
    }

    public FrontedV3DesignSubTarget? SubTarget
    {
        get;
        init;
    }
}
```

```csharp
public abstract record FrontedV3DesignSubTarget;
```

```csharp
public sealed record FrontedV3FixedPartTarget(
    FrontedV3PartDefinition Part)
    : FrontedV3DesignSubTarget;
```

```csharp
public sealed record FrontedV3CollectionItemTarget(
    FrontedV3PartCollectionDefinition Collection,
    string ItemKey)
    : FrontedV3DesignSubTarget;
```

示例：

```text
BorderedImage.Image
    → FixedPartTarget

MapV2.TeamName
    → FixedPartTarget

GlobalScore.Cells["Game1FirstHalf"]
    → CollectionItemTarget
```

---

# 17. 统一 Geometry Target

```csharp
public interface IFrontedV3GeometryTarget
{
    FrontedV3Rect GetBounds();

    void SetBounds(FrontedV3Rect bounds);

    FrontedV3GeometryCapabilities Capabilities
    {
        get;
    }

    FrontedV3Rect GetConstraintBounds();
}
```

实现：

```text
RootControlGeometryTarget
FixedPartGeometryTarget
CollectionItemGeometryTarget
```

Designer 的 Move、Resize、Snap、Clamp 和 Undo 只针对接口工作。

当前代码分别对：

* 根控件；
* BorderedImage 内部图片；
* GlobalScore Cell；
* MapV2 Internal Part；

实现不同的移动和缩放方法。

重构后这些逻辑只能保留一套。

---

# 18. PropertyGrid 重构

PropertyGrid 根据 Selection 构建不同 Schema：

```text
Root Selection
    → Root Property Schema

Fixed Part Selection
    → Part Property Schema

Collection Item Selection
    → Item Property Schema
```

不再：

* 反射整个 Config；
* 特判 BorderedImage；
* 特判 MapV2；
* 特判 GlobalScore；
* 读取插件手写 descriptor。

当前 PropertyGrid 会先判断插件 descriptor，否则反射 Config 的所有 public property。

新的 PropertyGrid 行由 `FrontedV3PropertyDefinition` 构建。

PropertyGrid 的每次编辑直接调用：

```text
PropertyDefinition.Storage.SetValue(...)
```

不再使用：

```csharp
config.GetType().GetProperty(propertyName)
```

---

# 19. 统一注册模型

```csharp
internal sealed record FrontedV3ControlRegistration
{
    public required string CanonicalControlType
    {
        get;
        init;
    }

    public required string LocalControlId
    {
        get;
        init;
    }

    public string? PackageId
    {
        get;
        init;
    }

    public required bool IsBuiltIn
    {
        get;
        init;
    }

    public required Type ControlType
    {
        get;
        init;
    }

    public required FrontedV3ControlAttribute Metadata
    {
        get;
        init;
    }

    public required IReadOnlyList<
        FrontedV3PropertyDefinition>
        RootProperties
    {
        get;
        init;
    }

    public required IReadOnlyList<
        FrontedV3PartDefinition>
        FixedParts
    {
        get;
        init;
    }

    public required IReadOnlyList<
        FrontedV3PartCollectionDefinition>
        PartCollections
    {
        get;
        init;
    }

    public FrontedV3StyleTransferProfile?
        StyleTransferProfile
    {
        get;
        init;
    }
}
```

Registry 只维护：

```text
CanonicalControlType
    → FrontedV3ControlRegistration
```

内置和插件没有两套注册架构。

当前 Registry 仍然先收集内置 `IFrontedControl`，再运行插件 Contributor，并把插件 descriptor 转换成 Adapter。

本次重构最终必须删掉这条双轨链路。

---

# 20. 默认配置

插件控件仍使用：

```csharp
PluginFrontedControlConfig
```

创建时：

1. 创建新的 Config；
2. 设置 Canonical ControlType；
3. 对所有 Root Property 写入默认值；
4. 初始化固定 Part geometry；
5. 初始化模板 Part Collection；
6. 由 Designer Runtime 写入根 Width/Height、ZIndex、BehaviorGuid 和放置位置。

控件 Attribute 可以提供默认根尺寸：

```csharp
[FrontedV3Control(
    "TeamCard",
    "Team Card",
    DefaultWidth = 260,
    DefaultHeight = 96)]
```

默认尺寸属于宿主元数据，不属于 Options。

---

# 21. 缺失插件处理

插件未安装时：

* JSON 字段继续由 ExtensionData 保留；
* Designer 显示缺失插件占位；
* 根控件仍可移动、缩放、删除；
* 不允许编辑插件自定义 Options；
* 不解析 Part；
* 不执行 XAML；
* 安装插件后重新启动，Schema 自动恢复。

当前 Renderer 已经支持在 Designer 中显示 Missing Plugin placeholder，并在前台运行时跳过缺失插件控件。

这一行为必须保留。

---

# 22. Visual Studio IntelliSense

运行时第一版可以通过动态 Options View 工作。

VS IntelliSense 作为可选增强，使用 Incremental Source Generator。

生成器读取：

```csharp
FrontedV3Property.Register<
    TeamCardControl,
    string>(
    "TextColor",
    "Appearance.TextColor",
    ...)
```

生成：

```csharp
public sealed class TeamCardOptions
{
    public TeamCardAppearanceOptions Appearance
    {
        get;
    } = new();

    public TeamCardContentOptions Content
    {
        get;
    } = new();
}
```

以及：

```csharp
public sealed class TeamCardDesignContext
{
    public TeamCardOptions Options
    {
        get;
    } = new();
}
```

插件 XAML：

```xml
d:DataContext="{d:DesignInstance
    Type=local:TeamCardDesignContext}"
```

生成器只服务 IDE：

* 不影响 JSON；
* 不参与运行时；
* 不允许成为加载插件的必要条件；
* 生成失败时控件运行时仍可工作。

---

# 23. ExamplePlugin 示例

示例插件至少增加两个示例。

## 23.1 XAML TeamCard

展示：

* Attribute 元数据；
* code-behind 属性注册；
* `Options.Appearance.*`；
* `Options.Content.*`；
* 一个可移动、可缩放的 Logo Part；
* 一个可绑定 TeamName 属性；
* 一个可传播的 Appearance 属性；
* XAML UserControl。

## 23.2 纯 C# StatusBadge

展示：

* 纯 C# VisualTree；
* 相同的 Attribute；
* 相同的 Property 注册 API；
* 相同的 Options Binding；
* 相同的 Designer PropertyGrid；
* 不需要 XAML。

ExamplePlugin 初始化最终：

```csharp
services.AddFrontedV3Control<TeamCardControl>();
services.AddFrontedV3Control<StatusBadgeControl>();
```

---

# 24. 内置控件迁移顺序

## 第一批：普通控件

选择：

```text
Text
Rectangle
Image
```

验证：

* Attribute；
* Root Properties；
* Options Binding；
* Host 根布局；
* JSON 不变。

## 第二批：BorderedImage

验证：

* 固定 Part；
* 内部 Resize；
* 旧 JSON 兼容。

## 第三批：MapV2Display

验证：

* 多固定 Part；
* Collection-backed geometry；
* Part PropertyGrid；
* Peer Style Transfer；
* Behavior 传播。

## 第四批：GlobalScoreRow

验证：

* PartCollection；
* FixedTemplate；
* ParentFallback；
* 父样式应用；
* 清除子项覆盖。

完成后再迁移其余内置控件。

---

# 25. 必须删除的旧架构

最终删除：

```text
IFrontedControlPluginContributor
IFrontedControlPluginRegistry
FrontedPluginControlDescriptor<TConfig>
IFrontedPluginControlDescriptor
FrontedControlPluginRegistry
FrontedPluginControlAdapter<TConfig>
AddFrontedPluginControlContributor<T>()
TeamCardFrontedControlContributor
插件专用 Config 类要求
插件 CreateControl delegate
插件 CreateDefaultConfig delegate
插件 Properties descriptor list
```

Designer 删除：

```text
IsBorderedImageSelected
BorderedImageResizeTarget
SelectedMapV2InternalStylePart
SelectedGlobalScoreCell
HasGlobalScoreCellEditor
HasSelectedMapV2InternalStylePart
ResizeSelectedBorderedImageInnerImage
MoveSelectedMapV2InternalPart
ResizeSelectedMapV2InternalPart
MoveSelectedGlobalScoreCell
ResizeSelectedGlobalScoreCell
ApplyParentStyleToGlobalScoreCells
ClearGlobalScoreCellStyleOverrides
ApplyMapV2DisplayStyleToAll
CopyMapV2DisplayStyle
```

允许保留业务模板初始化 Helper，例如：

```text
GlobalScoreRowCellLayoutHelper
MapV2InternalPartLayoutHelper
```

但它们只能负责：

* 初始化默认项；
* 补齐缺失模板；
* 提供业务默认布局。

不能继续负责 Designer 的通用选择、拖动、缩放或样式传播。

---

# 26. 重构阶段

## Phase 1：统一注册与属性 Schema

实现：

```text
FrontedV3ControlAttribute
FrontedV3ControlBase
FrontedV3Property<T>
FrontedV3PropertyDefinition
FrontedV3PropertyMetadata
StorageAccessor
FrontedV3OptionsView
FrontedV3ControlRegistration
AddFrontedV3Control<T>()
```

要求：

* 插件 PackageId 自动注入；
* JSON 不变；
* XAML 和纯 C# 都能创建；
* ExamplePlugin 先完成普通属性 POC。

---

## Phase 2：Host 接管根布局

实现：

```text
FrontedV3ControlHost
RootControlGeometryTarget
Host error boundary
Host visibility/effect/behavior handling
```

要求：

* Control 不再设置 Canvas 坐标；
* Renderer 不再要求 Control 返回已布局元素；
* Width/Height 完全由 Host 管理；
* 老布局渲染结果不变。

---

## Phase 3：固定 Part

实现：

```text
FrontedV3PartDefinition
FrontedV3Part.Register<T>()
FrontedV3.PartId AttachedProperty
FrontedV3PartVisualAttribute
FixedPartGeometryTarget
Part Property Context
```

迁移：

```text
BorderedImage
MapV2Display
```

---

## Phase 4：PartCollection

实现：

```text
FrontedV3PartCollectionDefinition
FrontedV3Parts.RegisterCollection
CollectionItemGeometryTarget
FixedTemplate / Dynamic / ReadOnly Policy
Collection Item Property Context
```

迁移：

```text
GlobalScoreRow.Cells
```

---

## Phase 5：统一样式系统

实现：

```text
FrontedV3PropertySemantic
FrontedV3PropertyInheritance
FrontedV3PropertyTransfer
FrontedV3StyleTransferProfile
FrontedV3StyleTransferService
Parent Style Apply
Clear Child Overrides
Peer Style Transfer
```

替换所有具体控件的手写样式复制方法。

---

## Phase 6：Designer 去特化

重构：

```text
FrontedV3DesignSelection
FrontedV3DesignSubTarget
IFrontedV3GeometryTarget
通用 Move
通用 Resize
通用 PropertyGrid
通用 Snap
通用 Clamp
通用 Undo
```

最终 Designer 不得引用：

```text
BorderedImageFrontedControlConfig
MapV2DisplayControlConfig
GlobalScoreRowControlConfig
```

---

## Phase 7：Source Generator 与文档

实现可选：

```text
Options 强类型生成
DesignContext 生成
XAML IntelliSense
诊断信息
```

增加：

* 插件开发文档；
* XAML 示例；
* 纯 C# 示例；
* Part 示例；
* PartCollection 示例；
* 样式传播示例。

---

# 27. 测试要求

## 注册

* 缺失 Attribute 时失败；
* 重复 Canonical ID 时失败；
* PackageId 自动命名空间正确；
* 两个插件可以注册相同 Local ID；
* IsBuiltIn 默认 false；
* 插件显式 IsBuiltIn true 时进入全局命名空间；
* 非法 ID 拒绝。

## 属性

* OptionsPath 唯一；
* Storage 不得映射保留布局字段；
* 默认值正确；
* Color、Enum、Number、Resource、Binding 编辑器正确；
* Options 修改后立即写入 Config；
* 保存后 JSON 结构不变；
* 重新加载后 Options 值一致。

## Host

* 根布局由 Host 应用；
* Control 内无需设置 Canvas；
* Visibility、Blur、Behavior 正常；
* 插件构造失败不崩溃宿主；
* XAML 加载失败显示错误占位。

## Part

* Part Visual 正确发现；
* 缺失 Part Visual 有诊断但不崩溃；
* Move/Resize 正确写入 Storage；
* 父边界 Clamp 正确；
* 只声明 Resize 的 Part 不可移动。

## PartCollection

* FixedTemplate 不可任意增删；
* Dynamic 可添加删除复制；
* ItemKey 唯一；
* Item Selection 稳定；
* JSON round-trip 不变。

## 样式

* ParentFallback 正确；
* LockedToParent 不允许覆盖；
* Apply Parent Style 正确；
* Clear Overrides 正确；
* Peer Style Transfer 只匹配相同 Canonical ControlType；
* 默认不复制数据属性；
* RootSize、PartLayout、Behavior 仅在 Profile 开启时传播。

## 缺失插件

* ExtensionData 不丢失；
* Designer 占位正常；
* 安装插件后重新 materialize 正确；
* 未安装插件时不能错误写回默认值覆盖原数据。

---

# 28. 最终插件开发体验

```csharp
[FrontedV3Control(
    "TeamCard",
    "Team Card",
    DefaultWidth = 260,
    DefaultHeight = 96)]
public partial class TeamCardControl
    : FrontedV3ControlBase
{
    public static readonly FrontedV3Property<string>
        TextColorProperty =
            FrontedV3Property.Register<
                TeamCardControl,
                string>(
                storage: "TextColor",
                optionsPath:
                    "Appearance.TextColor",
                defaultValue:
                    "#FFFFFFFF",
                editor:
                    FrontedPropertyEditorKind.Color,
                semantic:
                    FrontedV3PropertySemantic.Appearance,
                transfer:
                    FrontedV3PropertyTransfer.SameControlType);

    public static readonly FrontedV3Property<string>
        TeamNameProperty =
            FrontedV3Property.Register<
                TeamCardControl,
                string>(
                storage: "TeamName",
                optionsPath:
                    "Content.TeamName",
                defaultValue:
                    "Team",
                bindingTarget:
                    FrontedBindingTargetKind.Text);

    public static readonly FrontedV3PartDefinition
        LogoPart =
            FrontedV3Part.Register<
                TeamCardControl>(
                id: "Logo",
                geometry:
                    FrontedV3PartGeometry.Flat(
                        xStorage: "LogoX",
                        yStorage: "LogoY",
                        widthStorage:
                            "LogoWidth",
                        heightStorage:
                            "LogoHeight"),
                capabilities:
                    FrontedV3PartCapabilities.Move |
                    FrontedV3PartCapabilities.Resize);

    public TeamCardControl()
    {
        InitializeComponent();
    }
}
```

```xml
<fronted:FrontedV3ControlBase
    x:Class="ExamplePlugin.TeamCardControl">

    <Grid>
        <Image
            fronted:FrontedV3.PartId="Logo"
            Source="{Binding Options.Content.LogoSource}" />

        <TextBlock
            Foreground="{Binding Options.Appearance.TextColor}"
            Text="{Binding Options.Content.TeamName}" />
    </Grid>
</fronted:FrontedV3ControlBase>
```

```csharp
services.AddFrontedV3Control<TeamCardControl>();
```

保存后的 JSON 仍然：

```json
{
  "ControlType": "plugin:plfjy.ExamplePlugin/TeamCard",
  "Left": 100,
  "Top": 80,
  "Width": 260,
  "Height": 96,
  "TextColor": "#FFFFFFFF",
  "TeamName": "Team",
  "LogoX": 12,
  "LogoY": 16,
  "LogoWidth": 64,
  "LogoHeight": 64
}
```

最终职责清晰分离：

```text
Attribute
    → 控件身份与目录元数据

静态 FrontedV3Property
    → 控件公开给 Designer 的属性

Options
    → XAML 和 IDE 访问视图

StorageAccessor
    → 现有 JSON 映射

FrontedV3ControlHost
    → 根控件几何、效果和行为

Part
    → 固定内部可编辑区域

PartCollection
    → 模板化或动态子控件

Inheritance
    → 父子属性继承

StyleTransfer
    → 父到子与同类型批量传播

UserControl / C# VisualTree
    → 只负责最终视觉实现
```
