# Designer V3 Control 重构——并行执行总规则

你正在与另一个 GLM 任务共享同一个工作空间。

另一个任务正在修复前台 Window Registration、LayoutService、`.bpui` round-trip、FrontManage 和相关文档。你负责的是 **Designer V3 Control 完整重构**。

## 并行编辑规则

1. 每次修改文件前重新读取当前磁盘内容，不得依赖几分钟前读取的旧版本。
2. 不得执行：

   * `git reset`
   * `git restore`
   * `git checkout`
   * `git stash`
   * `git clean`
   * 回滚或覆盖其他任务的修改。
3. 不进行全仓格式化，不整理本阶段无关的 using、命名或注释。
4. 以下文件可能与另一任务冲突，除非当前 Phase 明确要求，否则暂时不要修改：

   * `neo-bpsys-wpf/Services/PluginService.cs`
   * `neo-bpsys-wpf/App.Services.xaml.cs`
   * `FrontedLayoutPackageImporter.cs`
   * `FrontedLayoutPackageExporter.cs`
   * `AGENTS.md`
   * 前台窗口系统相关文档
5. 出现编译错误时：

   * 先等待约 3～5 秒；
   * 重新读取报错文件；
   * 重新执行一次相同 Build；
   * 若错误明显来自另一任务正在编辑的类型或方法，最多重试三次；
   * 只有连续重试后仍然存在，才将其视为真实错误。
6. 不得为了消除瞬时编译错误新增：

   * shim；
   * adapter；
   * duplicate interface；
   * duplicate model；
   * fallback implementation；
   * 临时兼容构造函数。
7. 如果共享文件发生真实冲突，保留另一任务的新语义，在最新代码基础上做最小合并。
8. 每个 Phase 完成后必须：

   * Build；
   * 运行本阶段相关测试；
   * 搜索重复实现和旧类型引用；
   * 汇报改动文件、测试结果及未完成内容。
9. 不允许提前执行后续 Phase，也不允许一次性实现全部方案。
10. 不允许修改现有 `.bpui` 和布局 JSON 契约。

---

# 全局不可破坏契约

## JSON

`Options` 只是运行时和 Designer 属性投影，不进入 JSON。

必须继续保存：

```json
{
  "ControlType": "plugin:plfjy.ExamplePlugin/TeamCard",
  "Left": 100,
  "Top": 80,
  "Width": 260,
  "Height": 96,
  "TextColor": "#FFFFFFFF",
  "TeamName": "Team",
  "LogoWidth": 64,
  "LogoHeight": 64
}
```

禁止保存为嵌套的：

```json
{
  "Options": {
    "Appearance": {}
  }
}
```

## 单一事实来源

`Options.Appearance.TextColor` 必须直接投影到当前 Config 的根级 `TextColor`。

禁止 Options 保存一份独立值。

## Runtime 边界

根控件以下属性由 Host 管理：

```text
Left
Top
Width
Height
ZIndex
Visibility
GaussianBlur
BehaviorGuid
Canvas placement
Designer selection
Move/Resize handles
```

Control 本身只负责矩形区域内的视觉内容。

## 缺失插件

插件未安装时：

* `ExtensionData` 原样保留；
* Designer 显示 placeholder；
* 根控件仍能移动、缩放和删除；
* 不写入插件属性默认值；
* 不加载插件 XAML；
* 安装插件并重启后恢复 Schema。

---

# Phase 0：现状审计与落点设计

## 目标

不改生产行为，先完整分析当前 Control 架构，确定每个新类型应放置的位置及旧链路的迁移关系。

## 必须阅读

完整阅读并记录调用链：

```text
IFrontedControl
现有 FrontedControl Registry
IFrontedControlPluginContributor
IFrontedControlPluginRegistry
FrontedPluginControlDescriptor
FrontedPluginControlAdapter
PluginFrontedControlConfig
FrontedControlConfigBase
FrontedRenderer
Designer PropertyGrid
Designer selection / move / resize
BorderedImage
MapV2Display
GlobalScoreRow
ExamplePlugin control contributor
```

不得只通过类名推测实现。

## 输出

生成一份仓库内临时审计文档：

```text
docs/internal/designer-v3-control-refactor-audit.md
```

内容包括：

1. 当前注册链路；
2. 当前创建链路；
3. Config 与 JSON 链路；
4. PropertyGrid 链路；
5. 根布局设置位置；
6. BorderedImage 特判；
7. MapV2 特判；
8. GlobalScore 特判；
9. 缺失插件处理；
10. 每个拟新增类型的项目和目录；
11. 每个旧类型计划在哪个 Phase 删除。

## 限制

* 本 Phase 不删除旧 API；
* 不修改 JSON；
* 不增加 facade；
* 不修改 Window 重构相关代码；
* 不开始大规模编码。

## 完成条件

必须明确证明新方案可以映射到现有模型，而不是重新创建第二套孤立 Runtime。

---

# Phase 1：统一 Control Registration 与 Property Schema

## 目标

建立新 Control API 的最小闭环，但暂时保留旧 Control 注册链用于现有内置控件。

实现：

```text
FrontedV3ControlAttribute
FrontedV3ControlBase
FrontedV3ControlRegistration
FrontedV3Property<T>
FrontedV3PropertyDefinition
FrontedV3PropertyMetadata
IFrontedV3StorageAccessor
FrontedV3Storage
FrontedV3OptionsView
FrontedV3ControlContext
AddFrontedV3Control<T>()
统一 Registry 的新 registration 入口
```

## Registration 规则

插件 API：

```csharp
services.AddFrontedV3Control<TeamCardControl>();
```

插件作者不得传入：

```text
PackageId
CanonicalControlType
Config factory
CreateControl delegate
Property descriptor list
```

PackageId 从现有插件注册上下文自动获得。

身份：

```text
内置：Text
插件：plugin:{PackageId}/TeamCard
```

Control ID 允许安全的普通字符串，不强制 GUID。

必须验证：

* 非空、非纯空白；
* 不允许 `/`、`\`、`:`；
* 不允许直接传入完整 canonical ID；
* 同一 canonical type 不重复；
* 不同插件可以使用相同 local ID。

插件不能通过设置 `IsBuiltIn=true` 逃离自己的命名空间。只有宿主注册代码才能注册 built-in Control。

## Property 规则

属性必须同时具备：

```text
OptionsPath
StorageAccessor
Metadata
```

OptionsPath 不能直接当 JSON path 使用。

禁止注册：

```text
Options.Layout
Options.Geometry
Options.Position
```

禁止 Storage 覆盖：

```text
Left
Top
Width
Height
ZIndex
Visibility
BehaviorGuid
GaussianBlur
ControlType
```

## Storage

先实现：

1. `PluginFrontedControlConfig.ExtensionData` storage；
2. 内置 CLR property storage。

Collection item storage 留到 Phase 4。

必须正确处理：

* string；
* bool；
* int/double；
* enum；
* nullable；
* Color 字符串；
* JsonElement 到目标类型的转换。

## Options View

Options 必须动态代理当前 Config。

要求：

```text
读取 Options
→ Storage.GetValue(config)

修改 Options
→ Storage.SetValue(config)
→ PropertyChanged
→ 当前视觉 Binding 更新
```

不得缓存独立 property value。

## ExamplePlugin POC

新增一个简单的 XAML `TeamCardControl`：

* `Appearance.TextColor`；
* `Content.TeamName`；
* 根级 ExtensionData；
* XAML Binding；
* `AddFrontedV3Control<T>()` 注册。

本 Phase 暂不实现 Part。

## 测试

至少覆盖：

```text
MissingAttributeFails
DuplicateCanonicalTypeFails
PackageIdNamespacesPluginControl
SameLocalIdAcrossPluginsSucceeds
UnsafeIdFails
OptionsPathMustBeUnique
ReservedStorageFieldFails
OptionsReadUsesCurrentConfig
OptionsWriteUpdatesConfigImmediately
SerializedJsonHasNoOptionsObject
ExtensionDataFieldsRoundTrip
```

## 禁止

* 不删除旧 contributor；
* 不迁移所有内置控件；
* 不修改 Renderer 根布局；
* 不实现 Part；
* 不修改 `.bpui` Importer/Exporter；
* 不把新 Registry 做成旧 Registry 外面的 facade。

---

# Phase 2：FrontedV3ControlHost 接管根控件布局

## 目标

引入：

```text
FrontedV3ControlHost
RootControlGeometryTarget
Control error boundary
```

结构统一为：

```text
Canvas
└── FrontedV3ControlHost
    └── FrontedV3ControlBase
```

## Host 职责

Host 唯一负责：

```text
Canvas.Left
Canvas.Top
Width
Height
ZIndex
Visibility
GaussianBlur
BehaviorGuid marker
NameScope
Runtime generated marker
Designer selection
根 Move/Resize
错误占位
```

Control 不得设置自己的 Canvas 坐标。

## Renderer

修改 Renderer，使新 registration 创建出的 Control 总是包装在 Host 内。

不得同时保留：

```text
Renderer 设置一部分根属性
Host 再设置另一部分根属性
```

Host 完成后，新 Control 路径只能有一套根布局实现。

旧 Control 可以暂时通过明确的 legacy bridge 接入 Host，但 bridge 必须：

* internal；
* 单向；
* 有后续删除计划；
* 不暴露给插件 SDK；
* 不形成第二个正式 Runtime。

## 错误边界

以下错误不能导致整个前台窗口或 Designer 崩溃：

```text
Control constructor
InitializeFrontedV3
XAML InitializeComponent
Binding initialization
服务解析
```

失败时：

* Runtime 记录 warning 并跳过或显示安全占位；
* Designer 显示错误占位；
* 保留原 Config；
* 不写默认值覆盖 Config。

## 测试

```text
HostAppliesRootPosition
HostAppliesSizeAndZIndex
ControlDoesNotNeedCanvasCoordinates
HostAppliesVisibility
HostAppliesBlur
HostPreservesBehaviorGuid
ConstructorFailureDoesNotCrashWindow
InitializationFailureShowsDesignerPlaceholder
MissingPluginConfigIsNotModified
```

## 并行限制

本 Phase 不修改：

```text
PluginService.cs
App.Services.xaml.cs
.bpui Importer/Exporter
WindowService
FrontedLayoutService
```

如果另一任务正在修改 Renderer 附近文件，重新读取后做最小合并。

---

# Phase 3：固定 Part 与 BorderedImage 迁移

## 目标

实现统一固定内部部件：

```text
FrontedV3PartDefinition
FrontedV3Part.Register<T>()
FrontedV3PartGeometry
FrontedV3PartCapabilities
FrontedV3.PartId AttachedProperty
FrontedV3PartVisualAttribute
FixedPartGeometryTarget
Part property context
```

## Visual 发现

同时支持：

```xml
fronted:FrontedV3.PartId="Logo"
```

和：

```csharp
[FrontedV3PartVisual("Logo")]
public FrameworkElement LogoElement { get; }
```

解析后必须映射到同一个 Part。

缺失或重复 visual：

* 输出清晰诊断；
* 不崩溃 Designer；
* 不破坏 Config。

## Geometry

Geometry 全部通过 Storage 读写。

Capabilities 必须实际限制操作：

```text
Resize-only Part 不允许 Move
Move-only Part 不允许 Resize
```

坐标相对于父 Control。

## 首个迁移对象：BorderedImage

将内部 Image 注册为固定 Part：

```text
Id = Image
Width Storage = ImageWidth
Height Storage = ImageHeight
Capabilities = Resize
```

保持原 JSON：

```json
{
  "ImageWidth": 100,
  "ImageHeight": 100
}
```

删除 Designer 对 BorderedImage 内部图片的专用：

```text
BorderedImageResizeTarget
ResizeSelectedBorderedImageInnerImage
相关 selection flags
相关命令
```

不得保留新旧两套 resize。

## 测试

```text
PartVisualIsDiscovered
MissingPartVisualProducesDiagnostic
DuplicatePartVisualProducesDiagnostic
ResizeWritesExistingImageWidthHeightFields
ResizeOnlyPartCannotMove
PartBoundsClampToParent
BorderedImageJsonIsUnchanged
DesignerNoLongerUsesBorderedImageSpecialCase
```

---

# Phase 4：PartCollection 与 MapV2 / GlobalScore 迁移

## 目标

实现：

```text
FrontedV3PartCollectionDefinition
FrontedV3Parts.RegisterCollection
CollectionItemGeometryTarget
FixedTemplate
Dynamic
ReadOnly
Collection item property context
Collection item storage accessor
```

## MapV2

将以下固定内部区域注册为 Part：

```text
TeamName
MapCard
MapName
CampName
PickingBorder
```

继续使用现有：

```text
InternalParts
Part
X
Y
Width
Height
```

不得改变 JSON。

属性 OptionsPath 可以统一，但 Storage 必须映射到已有根级字段，例如：

```text
TeamName:
Options.Appearance.Color
→ TeamNameColor

MapName:
Options.Appearance.Color
→ MapNameColor
```

删除 Designer 中 MapV2 专用：

```text
SelectedMapV2InternalStylePart
MoveSelectedMapV2InternalPart
ResizeSelectedMapV2InternalPart
相关 selection flags
相关 geometry 分支
```

## GlobalScoreRow

将 `Cells` 注册为 `FixedTemplate` PartCollection。

要求：

* 根据现有 BO3/BO5 模板补齐缺失 Cell；
* 不允许用户任意添加或删除固定模板 Cell；
* 可移动、缩放、编辑；
* ItemKey 唯一；
* 继续使用现有 Cell ID 和 JSON 字段；
* 业务模板 Helper 只负责初始化与补齐，不再负责 Designer 操作。

删除 Designer 中：

```text
SelectedGlobalScoreCell
HasGlobalScoreCellEditor
MoveSelectedGlobalScoreCell
ResizeSelectedGlobalScoreCell
专用 selection 分支
专用 geometry 分支
```

## 测试

```text
MapV2PartsUseExistingInternalPartsJson
MapV2PartMovementUsesGenericGeometryTarget
GlobalScoreCellsUseFixedTemplatePolicy
FixedTemplateRejectsAddAndDelete
MissingTemplateCellsAreRestored
CollectionItemKeyMustBeUnique
CollectionItemMoveResizeRoundTrips
DesignerHasNoMapV2OrGlobalScoreGeometrySpecialCases
```

---

# Phase 5：统一继承与 Style Transfer

## 目标

实现：

```text
FrontedV3PropertySemantic
FrontedV3PropertyInheritance
FrontedV3PropertyTransfer
FrontedV3StyleTransferProfile
FrontedV3StyleTransferService
FrontedV3StyleComponent
```

## Inheritance

支持：

```text
None
ParentFallback
CopyFromParentOnCreate
LockedToParent
```

`ParentFallback` 必须动态读取：

```text
子项 override
→ 没有则父 OptionsPath
```

不得在创建 View 时复制一份 fallback 值。

## Parent Style 操作

框架根据相同 OptionsPath 自动完成：

```text
Apply Parent Style
Clear Child Overrides
```

删除 GlobalScore 专用逐字段复制：

```text
ApplyParentStyleToGlobalScoreCells
ClearGlobalScoreCellStyleOverrides
```

## Peer Style Transfer

仅匹配完全相同：

```text
CanonicalControlType
```

所以：

```text
plugin:a/TeamCard
```

不能传播给：

```text
plugin:b/TeamCard
```

默认仅传播：

```text
AppearanceProperties
```

只有 profile 显式开启时才传播：

```text
RootSize
PartLayout
Behaviors
Effects
```

永远不传播：

```text
Left
Top
ZIndex
ControlName
MapKey
TeamType
BindingPath
数据身份字段
```

删除 MapV2 手写：

```text
ApplyMapV2DisplayStyleToAll
CopyMapV2DisplayStyle
几十个逐字段 copy
```

## 测试

```text
ParentFallbackUsesParentWhenOverrideMissing
ParentOverrideWins
LockedToParentRejectsOverride
ApplyParentStyleUsesMatchingOptionsPath
ClearOverridesRestoresFallback
PeerTransferRequiresExactCanonicalType
AppearanceTransfersByDefault
DataSemanticDoesNotTransfer
RootSizeTransfersOnlyWhenEnabled
PartLayoutTransfersOnlyWhenEnabled
BehaviorTransfersOnlyWhenEnabled
```

---

# Phase 6：Designer 完全去特化

## 目标

统一为：

```text
FrontedV3DesignSelection
FrontedV3DesignSubTarget
FrontedV3FixedPartTarget
FrontedV3CollectionItemTarget
IFrontedV3GeometryTarget
RootControlGeometryTarget
FixedPartGeometryTarget
CollectionItemGeometryTarget
通用 PropertyGrid
通用 Move/Resize/Snap/Clamp/Undo
```

## Selection

Designer 只维护：

```csharp
FrontedV3DesignSelection? SelectedTarget
```

不得继续并行维护：

```text
SelectedGlobalScoreCell
SelectedMapV2InternalStylePart
BorderedImageResizeTarget
其他控件专用 selection state
```

## Geometry

所有：

```text
Move
Resize
Snap
Clamp
Undo
```

只调用 `IFrontedV3GeometryTarget`。

不得通过：

```csharp
if (config is BorderedImage...)
if (config is MapV2...)
if (config is GlobalScore...)
```

选择几何实现。

## PropertyGrid

PropertyGrid 只能根据 Schema 构造：

```text
Root selection
Fixed Part selection
Collection Item selection
```

属性编辑直接调用：

```text
PropertyDefinition.Storage.SetValue()
```

删除：

```text
反射 Config 全部 public properties
插件手写 property descriptor
控件类型特判
propertyName 字符串反射写入
```

可以保留一个只读 legacy diagnostic view，但不能用于正式编辑路径。

## 测试

```text
RootSelectionBuildsRootSchema
FixedPartSelectionBuildsPartSchema
CollectionItemSelectionBuildsItemSchema
PropertyEditUsesStorageAccessor
MoveUsesGeometryTarget
ResizeUsesGeometryTarget
UndoWorksForAllGeometryTargets
DesignerDoesNotReferenceBorderedImageConfig
DesignerDoesNotReferenceMapV2DisplayConfig
DesignerDoesNotReferenceGlobalScoreRowConfig
```

完成后执行源码扫描，Designer 项目和 ViewModel 不得引用上述三个具体 Config 类型，业务预览渲染代码除外。

---

# Phase 7：删除旧 Control 架构、补齐示例与文档

## 前置条件

只有 Phase 1～6 全部通过后才允许执行。

开始前确认另一个 Window 重构任务已经完成或不再修改共享文档和 DI 文件。

## 删除

删除旧插件 Control 架构：

```text
IFrontedControlPluginContributor
IFrontedControlPluginRegistry
FrontedPluginControlDescriptor<TConfig>
IFrontedPluginControlDescriptor
FrontedControlPluginRegistry
FrontedPluginControlAdapter<TConfig>
AddFrontedPluginControlContributor<T>()
TeamCardFrontedControlContributor
插件专用 Config 强制要求
插件 CreateControl delegate
插件 CreateDefaultConfig delegate
插件 Properties descriptor list
```

不得保留：

```text
Obsolete shim
adapter
facade
旧注册路径 fallback
```

Registry 最终只能维护：

```text
CanonicalControlType
→ FrontedV3ControlRegistration
```

内置和插件不能保留两套正式注册链。

## 内置普通控件迁移

至少迁移并验证：

```text
Text
Rectangle
Image
BorderedImage
MapV2Display
GlobalScoreRow
```

其他内置控件如果未迁移，必须给出明确列表；不能通过旧正式架构长期共存。

## ExamplePlugin

最终提供：

### XAML TeamCard

展示：

* Attribute；
* Property；
* Options Binding；
* 固定 Logo Part；
* Appearance style transfer；
* TeamName；
* XAML。

### 纯 C# StatusBadge

展示：

* 纯 C# VisualTree；
* 相同 Attribute；
* 相同 Property API；
* 相同 Options；
* 相同 PropertyGrid；
* 无 XAML。

初始化只能是：

```csharp
services.AddFrontedV3Control<TeamCardControl>();
services.AddFrontedV3Control<StatusBadgeControl>();
```

## 文档

更新：

```text
PluginSdk README
Designer V3 文档
插件系统文档
AGENTS.md 中 Control 注册规则
ExamplePlugin README
JSON 契约文档
```

必须明确：

```text
Options 不进入 JSON
Control 不管理根布局
Part 管理固定内部区域
PartCollection 管理模板或动态子项
StyleTransfer 不默认传播数据身份
缺失插件数据不会丢失
```

不要覆盖另一任务对 Window system 和 `.bpui` 的最新文档修改。

## 最终扫描

全仓搜索以下旧符号，除历史迁移文档外必须为 0：

```text
IFrontedControlPluginContributor
IFrontedControlPluginRegistry
FrontedPluginControlDescriptor
IFrontedPluginControlDescriptor
FrontedControlPluginRegistry
FrontedPluginControlAdapter
AddFrontedPluginControlContributor
TeamCardFrontedControlContributor
```

Designer 通用编辑路径搜索以下具体类型，必须为 0：

```text
BorderedImageFrontedControlConfig
MapV2DisplayControlConfig
GlobalScoreRowControlConfig
```

## 最终报告

报告必须包含：

1. 每个 Phase 修改文件；
2. 新增和删除的公开 API；
3. 旧架构扫描结果；
4. JSON before/after 样例；
5. 缺失插件 round-trip 测试；
6. XAML TeamCard 测试；
7. C# StatusBadge 测试；
8. Host 异常隔离测试；
9. Part/PartCollection 测试；
10. StyleTransfer 测试；
11. Designer 去特化扫描；
12. Build 0 error / 0 warning；
13. 全测试结果；
14. 与本任务无关的既有失败列表；
15. 并行期间合并过的共享文件及处理方式。

---

# 可选 Phase 8：Source Generator

该阶段与运行时完全独立，只有前七个阶段稳定后才执行。

实现 Incremental Source Generator，为插件生成：

```text
TeamCardOptions
TeamCardAppearanceOptions
TeamCardContentOptions
TeamCardDesignContext
```

它只服务 Visual Studio XAML IntelliSense：

* 不参与运行时；
* 不参与插件加载；
* 不参与 JSON；
* 不生成第二套属性元数据；
* 生成失败不能影响插件运行；
* 所有生成内容必须来自现有 `FrontedV3Property` 定义。

若无法可靠从静态注册表达式提取信息，则本阶段暂停，不能为了 Generator 修改运行时 API 或引入重复 schema。
