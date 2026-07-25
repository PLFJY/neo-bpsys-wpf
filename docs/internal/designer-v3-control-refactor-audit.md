# Designer V3 Control 重构——Phase 0 现状审计与落点设计

> 本文档为 `v3-control-refactor.md` Phase 0 的产物。目的是在不修改生产行为的前提下，完整记录当前 Control 架构的实际调用链，确定每个新类型的落点，并证明新方案映射到现有模型而非创建第二套孤立 Runtime。
>
> 所有结论均基于实际代码阅读，非类名推测。引用格式：`文件路径` + 关键类/方法名。

---

## 1. 当前注册链路

### 1.1 内置控件注册

内置控件通过 DI 注册为 `IFrontedControl` 单例，再由 `FrontedControlRegistry` 聚合。

- `neo-bpsys-wpf/App.Services.xaml.cs`（行 167–181）逐个注册内置控件工厂：
  - `services.AddSingleton<IFrontedControl, TextFrontedControl>()`
  - `services.AddSingleton<IFrontedControl, LocalizedTextFrontedControl>()`
  - `services.AddSingleton<IFrontedControl, ImageFrontedControl>()`
  - `services.AddSingleton<IFrontedControl, BorderedImageFrontedControl>()`
  - `services.AddSingleton<IFrontedControl, RectangleFrontedControl>()`
  - `services.AddSingleton<IFrontedControl, PolygonFrontedControl>()`
  - `services.AddSingleton<IFrontedControl, BackgroundTintRectangleFrontedControl>()`
  - `services.AddSingleton<IFrontedControl, BackgroundTintPolygonFrontedControl>()`
  - `services.AddSingleton<IFrontedControl, GlobalScoreRowFrontedControl>()`
  - `services.AddSingleton<IFrontedControl, TalentTraitDisplayFrontedControl>()`
  - `services.AddSingleton<IFrontedControl, GameProgressTextFrontedControl>()`
  - `services.AddSingleton<IFrontedControl, MapNameTextFrontedControl>()`
  - `services.AddSingleton<IFrontedControl, MapV2DisplayFrontedControl>()`
  - `services.AddSingleton<IFrontedControlRegistry, FrontedControlRegistry>()`

- `neo-bpsys-wpf.Core/Abstractions/Services/IFrontedControl.cs`：`IFrontedControl` 接口暴露 `ControlType`、`ConfigType`、`Create(name, config, context)`。内置控件直接实现该接口，`ControlType` 为裸名（如 `"Text"`、`"BorderedImage"`、`"MapV2Display"`、`"GlobalScoreRow"`）。

### 1.2 插件控件注册

插件控件通过 contributor + descriptor + adapter 三段式注册。

- `neo-bpsys-wpf.Core/Extensions/Registry/FrontedPluginControlRegistryExtensions.cs`：`AddFrontedPluginControlContributor<TContributor>()` 将 contributor 注册为 `Singleton<IFrontedControlPluginContributor, TContributor>`。

- `neo-bpsys-wpf.Core/Abstractions/Services/IFrontedControlPluginContributor.cs`：`IFrontedControlPluginContributor.RegisterFrontedControls(IFrontedControlPluginRegistry registry)`。

- `neo-bpsys-wpf.Core/Abstractions/Services/IFrontedControlPluginRegistry.cs`：`IFrontedControlPluginRegistry.Register<TConfig>(FrontedPluginControlDescriptor<TConfig> descriptor)`。

- `neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedControlPluginRegistry.cs`：`FrontedControlPluginRegistry(IReadOnlySet<string> builtInControlTypes)` 实现 `IFrontedControlPluginRegistry`。
  - `Register<TConfig>` 校验：`FrontedPluginControlType.IsValidPart(PackageId)`、`IsValidPart(ControlTypeName)`、`Parse(FullControlType)`、`ConfigType` 必须可赋值给 `TConfig` 与 `FrontedControlConfigBase`、`CreateControl` 非空。
  - 拒绝覆盖内置控件：`builtInControlTypes.Contains(FullControlType)` 抛异常。
  - 按 `FullControlType` 去重。

- `neo-bpsys-wpf.Core/Models/FrontedLayout/FrontedPluginControlDescriptor.cs`：`FrontedPluginControlDescriptor<TConfig>` 暴露过多内部细节：`PackageId`、`ControlTypeName`、`FullControlType`、`ConfigType`、`CreateControl` 工厂委托、`CreateDefaultConfig` 工厂委托、`Properties` 描述符列表、`Validate`、本地化键、`Icon`、`MinHostVersion`、`ConfigSchemaVersion`。`FullControlType` 由 `FrontedPluginControlType(PackageId, ControlTypeName).ToString()` 生成。

- `neo-bpsys-wpf.Core/Models/FrontedLayout/IFrontedPluginControlDescriptor.cs`：非泛型元数据视图，供 Registry/PropertyGrid 消费。

- `neo-bpsys-wpf.Core/Models/FrontedLayout/FrontedPluginControlType.cs`：canonical type 格式 `plugin:{PackageId}/{ControlTypeName}`，`Prefix = "plugin:"`。`IsValidPart` 拒绝 `/`、`\`、`:`、`..`、空白。`Parse`/`TryParse` 解析双段式身份。

### 1.3 Registry 聚合

- `neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedControlRegistry.cs`：`FrontedControlRegistry(IEnumerable<IFrontedControl>, IEnumerable<IFrontedControlPluginContributor>, ILogger?)` 构造函数：
  1. 将内置 `IFrontedControl` 按 `ControlType` 装入 `_controls` 字典（`OrdinalIgnoreCase`），重复抛 `FrontedLayoutConfigException`。
  2. 收集内置 `ControlType` 集合 `builtInControlTypes`。
  3. 创建 `FrontedControlPluginRegistry(builtInControlTypes)`，遍历 `pluginContributors` 调用 `RegisterFrontedControls(pluginRegistry)`。
  4. 将每个 `IFrontedPluginControlDescriptor` 通过 `CreatePluginAdapter`（反射构造 `FrontedPluginControlAdapter<TConfig>`）适配为 `IFrontedControl`，装入 `_controls`，同时存入 `_pluginDescriptors`。
  5. 暴露 `GetControl(controlType)`、`GetControls()`、`IsPluginControlRegistered`、`GetPluginDescriptor`、`GetPluginDescriptors`。

**注册链路结论**：内置与插件走两套正式注册路径——内置 `IFrontedControl` 直注册，插件经 contributor → descriptor → adapter 转 `IFrontedControl`。canonical type 格式统一由 `FrontedPluginControlType` 把控。

---

## 2. 当前创建链路

### 2.1 Renderer 入口

- `neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedRenderer.cs`：`FrontedRenderer(IServiceProvider, ISharedDataService, IFrontedResourceResolver, IFrontedControlRegistry, ILogger)` 实现 `IFrontedRenderer`。
  - `RenderToCanvas(Canvas, FrontedCanvasConfig, FrontedRenderContext)`：
    1. `ClearGeneratedControls(canvas)` 清理上次生成的控件（通过 `FrontedRendererProperties.GetIsGeneratedControl` 标记识别）。
    2. `FrontedCanvasRuntimeStateResolver.Resolve` 解析运行时状态（CanvasWidth/Height、BackgroundImage、Controls）。
    3. 设置 `canvas.Width/Height/Background`。
    4. 构造 `FrontedControlBuildContext`（Services、SharedDataService、ResourceResolver、WindowId、CanvasName、CanvasBackgroundImage、CanvasWidth/Height、IsDesignerPreview、Logger）。
    5. 按 `ZIndex` 排序遍历 `runtimeState.Controls`：
       - `controlRegistry.GetControl(controlConfig.ControlType)` 取工厂。
       - 工厂为 null 且为插件类型 → 走缺失插件分支（见第 9 章）。
       - 工厂为 null 且非插件类型 → 抛 `FrontedLayoutConfigException`。
       - 否则 `factory.Create(name, controlConfig, buildContext)` 得 `FrameworkElement`。
       - Renderer 后处理：`element.Visibility = MapVisibility(config.Visibility)`、`SetIsGeneratedControl(true)`、`SetBehaviorGuid(config.BehaviorGuid)`、`RegisterGeneratedName`、`FrontedEffectHostFactory.Wrap(element)`、`ApplyStaticGaussianBlur(host, IsGaussianBlurEnabled, GaussianBlurRadius)`、`canvas.Children.Add(host)`。

### 2.2 内置控件创建

内置 `IFrontedControl.Create` 实现直接 new 出 `FrameworkElement`，并在构造函数内自行设置根布局。典型证据：
- `neo-bpsys-wpf/Controls/FrontedLayout/GlobalScoreRowFrontedControl.cs`（`GlobalScoreRowElement` 构造函数，行 56–68）：`Canvas.SetLeft(this, config.Left)`、`Canvas.SetTop(this, config.Top)`、`Panel.SetZIndex(this, config.ZIndex)`、`Width = config.Width`、`Height = config.Height`。
- `neo-bpsys-wpf/Controls/FrontedLayout/CutSceneFrontedControlHelper.cs`（行 17–19）：同样模式 `Canvas.SetLeft/SetTop/SetZIndex`。
- `neo-bpsys-wpf/Controls/FrontedLayout/MapNameTextFrontedControl.cs`（行 84–86）、`GameProgressTextFrontedControl.cs`（行 80–82）：从 outer 元素复制 Canvas 坐标。

### 2.3 插件控件创建

- `neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedPluginControlAdapter.cs`：`FrontedPluginControlAdapter<TConfig>.Create`：
  1. `ConvertConfig`：若 config 已是 `TConfig` 直接用；若为 `PluginFrontedControlConfig`，调用 `FrontedPluginControlConfigMaterializer.Materialize` 转换；否则抛异常。
  2. 调用 `descriptor.CreateControl(name, typedConfig, context)`。
- `neo-bpsys-wpf.ExamplePlugin/TeamCardFrontedControlContributor.cs`：`CreateControl` 内 new `Border`，通过 `ApplyCanvasLayout`（行 146–161）自行设置 `Canvas.SetLeft/SetTop/SetZIndex` + `Width/Height`，再构建 Grid + Image + TextBlock。

**创建链路结论**：当前根布局由控件自身负责（Left/Top/ZIndex/Width/Height），Renderer 只补 Visibility/BehaviorGuid/IsGeneratedControl 标记/NameScope/GaussianBlur/EffectHost 包装。不存在统一 Host。

---

## 3. Config 与 JSON 链路

### 3.1 基类字段

- `neo-bpsys-wpf.Core/Models/FrontedLayout/FrontedControlConfigBase.cs`：根级字段全部平铺序列化到 JSON 根：
  - `BehaviorGuid`（`JsonIgnoreCondition.WhenWritingDefault`）
  - `ControlType`、`Left`、`Top`、`Width`（nullable）、`Height`（nullable）、`ZIndex`
  - `Visibility`（`FrontedControlVisibility` 枚举，默认 Visible）
  - `BindingPath`（nullable）
  - `IsGaussianBlurEnabled`、`GaussianBlurRadius`

### 3.2 插件 Config 与 ExtensionData

- `neo-bpsys-wpf.Core/Models/FrontedLayout/PluginFrontedControlConfig.cs`：继承基类 + `[JsonExtensionData] Dictionary<string, JsonElement> ExtensionData`。插件未安装时，JSON 反序列化为 `PluginFrontedControlConfig`，未知字段进入 `ExtensionData`。`PackageId`/`ControlTypeName` 为 `[JsonIgnore]` 计算属性（从 `ControlType` 解析）。

### 3.3 类型化转换（Materializer）

- `neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedPluginControlConfigMaterializer.cs`：`Materialize(controlName, PluginFrontedControlConfig, IFrontedPluginControlDescriptor)`：
  1. `JsonSerializer.Serialize(config, config.GetType())` 序列化回 JSON。
  2. `JsonSerializer.Deserialize(json, descriptor.ConfigType)` 反序列化为类型化配置。
  3. `converted.ControlType = descriptor.FullControlType` 修正身份。
  4. 失败抛 `FrontedLayoutConfigException`。
  - 另一重载 `Materialize(controlName, FrontedControlConfigBase, IFrontedControlRegistry?)`：非 `PluginFrontedControlConfig` 或无 descriptor 时原样返回。

### 3.4 设计文档转换

- `neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedLayoutDesignConverter.cs`：
  - `FromConfig`：`FrontedPluginControlConfigMaterializer.Materialize` 将运行时 Config 转类型化，包装为 `FrontedControlDesignItem`。
  - `ToConfig`：将 `document.Controls` 的 `Name → Config` 写回 `FrontedCanvasConfig.Controls`，处理 Bo3/Bo5 状态分桶与 `RequiredPlugins` 同步。

### 3.5 具体控件 Config 示例

- `BorderedImageFrontedControlConfig`（构造函数设 `ControlType = "BorderedImage"`）+ `ImageWidth`/`ImageHeight`（内层 Image 尺寸）。
- `MapV2DisplayControlConfig`（`ControlType = "MapV2Display"`）+ `MapKey`、`MapNameColor`/`TeamNameColor`/`CampNameColor`、`MapBorderNormalColor`/`MapBorderBannedColor`、`InternalParts: List<MapV2InternalPartLayoutConfig>`（`Part`/`X`/`Y`/`Width`/`Height`）。
- `GlobalScoreRowControlConfig`（`ControlType = "GlobalScoreRow"`，实现 `IFrontedTextStyleConfig`）+ `TeamType`、`MajorGameGap`/`HalfGameGap`、`Cells: List<GlobalScoreCellConfig>`（`Id`/`GameNumber`/`GameKind`/`HalfKind`/`X`/`Y`/`Width`/`Height`/`Visibility` + 可空字体/颜色 override）、`FontFamily`/`FontWeight`/`Color`/`FontSize`/`ShowCampIcon`/`CampIconColor`。

### 3.6 默认 Config 工厂

- `neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedControlDefaultConfigFactory.cs`：
  - `AddableControlTypes` 硬编码内置白名单（Text/LocalizedText/Image/BorderedImage/Rectangle/Polygon/BackgroundTintRectangle/BackgroundTintPolygon/MapNameText/GameProgressText/TalentTraitDisplay/GlobalScoreRow/MapV2Display）。
  - `Create(controlType, document, centerX, centerY)`：内置走 `CreateDefault` switch（每个类型手动 new 配置并设默认值），插件走 `TryCreatePluginDefault`（反射调用 `descriptor.CreateDefaultConfig` 委托或无参构造）。统一赋 `BehaviorGuid`、`ZIndex`、`ApplyPlacement`（Left/Top 居中）。

**Config/JSON 链路结论**：JSON 全部平铺根级字段，无 `Options` 嵌套对象。`ExtensionData` 保证缺失插件时数据不丢。Materializer 通过 JSON round-trip 完成通用→类型化转换。

---

## 4. PropertyGrid 链路

- `neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedPropertyGridBuilder.cs`：`Build(FrontedCanvasDesignDocument, FrontedControlDesignItem, FrontedLayoutValidator, FrontedLayoutReferenceScanner)` 返回 `ObservableCollection<FrontedPropertyEditorItem>`。
  - `AddIdentityRows`：`Name`（可编辑，`RequiresExplicitCommit`）+ `ControlType`（只读，本地化显示名）。
  - `AddConfigRows` 三分支：
    1. `config is PluginFrontedControlConfig missingPlugin && pluginDescriptor is null` → `AddMissingPluginRows`（Layout 字段 + 只读 PackageId/ControlTypeName/ExtensionData keys + 安装引导 + GaussianBlur）。
    2. `pluginDescriptor.Properties?.Count > 0` → `AddPluginMetadataRows`：先加 Layout 字段（Left/Top/Width/Height/ZIndex/BindingPath），再按 `descriptor.Properties`（`FrontedPluginPropertyDescriptor`）元数据驱动加行，最后补 GaussianBlur。
    3. 否则 → 反射 `config.GetType().GetProperties(BindingFlags.Instance|Public)`，`IsSupportedProperty` 过滤，按 `ResolveGroupName`/`GetPropertyOrder` 排序，逐属性加行。

### 4.1 编辑器类型与分组解析

- `ResolveEditorKind`：按属性类型 + 名称推断 `FrontedPropertyEditorKind`（Color/FontFamily/Enum/Boolean/Number/Text/TextBinding/ReadOnly/ToggleSwitch）。
- `ResolveGroupName`：**大量 `is BorderedImageFrontedControlConfig` / `is MapV2DisplayControlConfig` / `is ShapeFrontedControlConfigBase` / `is ImageFrontedControlConfig` / `is TextFrontedControlConfig` 类型特判**决定分组（Layout/Binding/Resource/Image/Border/Overlay/Appearance/Content/ControlSpecific）。
- `IsVisibleProperty`：`is BorderedImageFrontedControlConfig` / `is ImageFrontedControlConfig` 分支隐藏特定属性。
- `ResolveBindingTargetKind`：`config is TextFrontedControlConfig`/`LocalizedTextControlConfig`/`ImageFrontedControlConfig`/`GameProgressTextControlConfig`/`MapNameTextControlConfig`/`ShapeFrontedControlConfigBase`/`BackgroundTintFrontedControlConfigBase` switch 决定绑定目标类型。

### 4.2 属性读写

- 读：`property.GetValue(selectedItem.Config)`。
- 写：ViewModel 侧按 `FrontedPropertyEditorItem.PropertyName` 字符串反射写回 Config（在 `FrontedDesignerWindowViewModel` 中）。
- 插件元数据：`neo-bpsys-wpf.Core/Models/FrontedLayout/FrontedPluginPropertyDescriptor.cs`（`PropertyName`/`DisplayNameKey`/`DescriptionKey`/`GroupName`/`EditorKind`/`Options`/`BindingTargetKind`/`IsVisible`/`IsReadOnly`）。

### 4.3 行模型

- `neo-bpsys-wpf.Core/Models/FrontedLayout/Designer/FrontedPropertyEditorItem.cs`：`ObservableObject`，含 `DisplayName`/`PropertyName`/`PropertyType`/`EditorKind`/`Value`/`EditText`/`ColorValue`/`IsReadOnly`/`Options`/`GroupName`/`CanBrowseBinding`/`CanBrowseResource`/`BindingTargetKind` 等。

**PropertyGrid 链路结论**：当前 PropertyGrid 混合三种构造方式（缺失插件/插件元数据/反射），分组与可见性逻辑深陷控件类型特判。属性写入依赖 propertyName 字符串反射，无 Schema 约束。

---

## 5. 根布局设置位置

当前根布局职责**分散在三处**，无单一 Owner：

| 根属性 | 设置位置 | 代码证据 |
| --- | --- | --- |
| `Left`/`Top`/`ZIndex` | 控件自身构造函数 | `GlobalScoreRowFrontedControl.cs:56-58`、`CutSceneFrontedControlHelper.cs:17-19`、`TeamCardFrontedControlContributor.cs:147-150` |
| `Width`/`Height` | 控件自身构造函数 | `GlobalScoreRowFrontedControl.cs:60-68`、`TeamCardFrontedControlContributor.cs:152-160` |
| `Visibility` | Renderer | `FrontedRenderer.cs:112` `element.Visibility = MapVisibility(...)` |
| `BehaviorGuid` | Renderer（附加属性） | `FrontedRenderer.cs:114` `FrontedRendererProperties.SetBehaviorGuid` |
| `IsGeneratedControl` 标记 | Renderer | `FrontedRenderer.cs:113` |
| NameScope/Name 注册 | Renderer | `FrontedRenderer.cs:115` `RegisterGeneratedName`、`GetNameScopeOwner`、`EnsureNameScope` |
| `GaussianBlur` | Renderer（经 EffectHost） | `FrontedRenderer.cs:116-117` `FrontedEffectHostFactory.Wrap` + `ApplyStaticGaussianBlur` |
| Designer Move/Resize 写入 | ViewModel 直接写 Config | `FrontedDesignerWindowViewModel.cs:2897-2898`（Left/Top）、`3163-3166`（Left/Top/Width/Height） |
| Canvas placement | Renderer `canvas.Children.Add` | `FrontedRenderer.cs:118` |

**根布局结论**：Renderer 与控件各自承担一部分根属性，Designer 又绕开两者直接改 Config。这正是 Phase 2 `FrontedV3ControlHost` 要统一的目标——Host 唯一负责所有根布局，Control 不得设置自己的 Canvas 坐标。

---

## 6. BorderedImage 特判

### 6.1 Config

- `neo-bpsys-wpf.Core/Models/FrontedLayout/BorderedImageFrontedControlConfig.cs`：继承 `ImageFrontedControlConfig`，新增 `ImageWidth`/`ImageHeight`（内层 Image 显式尺寸，nullable）。`ControlType = "BorderedImage"`。

### 6.2 Designer 专用状态

- `neo-bpsys-wpf/ViewModels/Windows/FrontedDesignerWindowViewModel.cs`：
  - `BorderedImageResizeTarget` 枚举字段（`_borderedImageResizeTarget`，默认 `Border`，行 563）。
  - `IsBorderedImageSelected`（行 443）：`SelectedDesignItem?.Config is BorderedImageFrontedControlConfig`。
  - `IsBorderedImageBorderResizeTarget`/`IsBorderedImageImageResizeTarget`（行 580/592）：切换 resize 目标。

### 6.3 Resize 特判

- `FrontedDesignerWindowViewModel.cs` resize 分发（行 3133–3147）：`SelectedDesignItem.Config is BorderedImageFrontedControlConfig imageConfig && BorderedImageResizeTarget == FrontedDesignerResizeTarget.Image` → 调用 `ResizeSelectedBorderedImageInnerImage`（行 3466）。
- `ResizeSelectedBorderedImageInnerImage`：按 handle 计算 widthDelta/heightDelta，snap 后写 `config.ImageWidth`/`config.ImageHeight`（行 3507–3508），不动根 Width/Height。

### 6.4 PropertyGrid 特判

- `FrontedPropertyGridBuilder.ResolveGroupName`（行 761–802）：`config is BorderedImageFrontedControlConfig` 分支决定 Overlay/Border/Image 分组。
- `FrontedPropertyGridBuilder.IsVisibleProperty`（行 954–959）：`config is BorderedImageFrontedControlConfig` 分支隐藏 `PickingBorder`/`BanLockAvailable`/`BanLockImagePath`。

**BorderedImage 特判结论**：内层 Image 尺寸编辑完全独立于根 resize，是 Phase 3 首个迁移对象——将内部 Image 注册为 Id=`Image`、Width Storage=`ImageWidth`、Height Storage=`ImageHeight`、Capabilities=Resize 的固定 Part。

---

## 7. MapV2 特判

### 7.1 Config

- `neo-bpsys-wpf.Core/Models/FrontedLayout/MapV2DisplayControlConfig.cs`：
  - `MapKey`、`MapNameFontFamily`/`MapNameFontWeight`/`MapNameColor`/`MapNameFontSize`、`TeamNameFontFamily`/`TeamNameFontWeight`/`TeamNameColor`/`TeamNameFontSize`、`CampNameFontFamily`/`CampNameFontWeight`/`CampNameColor`/`CampNameFontSize`、`MapBorderNormalColor`/`MapBorderBannedColor`、`PickingBorderImagePath`/`PickingBorderFillColor`。
  - `InternalParts: List<MapV2InternalPartLayoutConfig>`（`Part: MapV2InternalStylePart` + `X`/`Y`/`Width`/`Height`）。
  - `MapV2InternalStylePart` 枚举：`TeamName`/`MapCard`/`MapName`/`CampName`/`PickingBorder`。

### 7.2 Designer 专用状态

- `FrontedDesignerWindowViewModel.cs`：
  - `MapV2InternalStylePartOptions`（行 312–318）：5 个部件的本地化选项集合。
  - `_selectedMapV2InternalStylePart`（行 472）、`SelectedMapV2InternalPartLayout`（行 455，按选中部件从 `MapV2InternalPartLayoutHelper.EnsureParts(config)` 取布局）。
  - `IsMapV2DisplaySelected`（行 445）、`HasSelectedMapV2InternalStylePart`（行 450）、`_isMapV2InternalStyleEditorVisible`（行 475）。

### 7.3 Move/Resize 特判

- `MoveSelectedDesignItem`（行 2846–2850）：`HasSelectedMapV2InternalStylePart` → `MoveSelectedMapV2InternalPart`（行 3267）：写 `part.X`/`part.Y`，按父控件宽高 clamp。
- Resize 分发（行 3084–3096）：`HasSelectedMapV2InternalStylePart` → `ResizeSelectedMapV2InternalPart`（行 3365）：写 `part.X/Y/Width/Height`，调 `ClampSelectedMapV2InternalPart`（行 3415）。

### 7.4 Style Transfer 特判

- `ApplyMapV2DisplayStyleToAll`（行 1910）：筛选所有 `MapV2DisplayControlConfig`，逐个 `CopyMapV2DisplayStyle`（行 1941，手写逐字段复制 + `MapV2InternalPartLayoutHelper.EnsureParts` + 深拷贝 InternalParts）。
- `ApplyMapV2DisplayBehaviorSetToTargets`（行 1974）+ `RegenerateMapV2BehaviorGraphIds`（行 2110）+ `RewriteMapV2BehaviorTargetsAndFilters`（行 2034）：行为图重写。

### 7.5 PropertyGrid 特判

- `FrontedPropertyGridBuilder.ResolveGroupName`（行 859–864）：`config is MapV2DisplayControlConfig` 分支将 `MapBorderNormalColor`/`MapBorderBannedColor` 归 Border 组。

### 7.6 辅助

- `MapV2InternalPartLayoutHelper.EnsureParts`：保证 5 个固定部件存在。

**MapV2 特判结论**：5 个固定内部部件独立 Move/Resize + 手写 Style Transfer。Phase 4 将 `TeamName`/`MapCard`/`MapName`/`CampName`/`PickingBorder` 注册为固定 Part，Storage 映射到已有根级字段（`TeamNameColor`/`MapNameColor` 等），JSON 不变；Phase 5 删除 `ApplyMapV2DisplayStyleToAll`/`CopyMapV2DisplayStyle`。

---

## 8. GlobalScore 特判

### 8.1 Config

- `neo-bpsys-wpf.Core/Models/FrontedLayout/GlobalScoreRowControlConfig.cs`：实现 `IFrontedTextStyleConfig`。`TeamType`、`MajorGameGap`/`HalfGameGap`、`Cells: List<GlobalScoreCellConfig>`、`FontFamily`/`FontWeight`/`Color`/`FontSize`/`ShowCampIcon`/`CampIconColor`。
- `GlobalScoreCellConfig`：`Id`/`GameNumber`/`GameKind`/`HalfKind`/`X`/`Y`/`Width`/`Height`/`Visibility` + 可空 `FontFamily`/`FontWeight`/`Color`/`FontSize`/`ShowCampIcon`/`CampIconColor`（为空时继承父行）。

### 8.2 Designer 专用状态

- `FrontedDesignerWindowViewModel.cs`：
  - `GlobalScoreCellEditorItems`（行 307）、`_selectedGlobalScoreCellParentName`/`_selectedGlobalScoreCellId`（行 514–515）、`SelectedGlobalScoreCell`（行 541）、`HasSelectedGlobalScoreCell`（行 559）、`HasGlobalScoreCellEditor`（行 561）。
  - `OnGlobalScoreCellSelectionChanged`（行 1079）维护选择一致性。

### 8.3 Move/Resize 特判

- `MoveSelectedDesignItem`（行 2840–2844）：`HasSelectedGlobalScoreCell` → `MoveSelectedGlobalScoreCell`（行 3235）：写 `cell.X`/`cell.Y`，按 row 宽高 clamp，回设 `SelectedDesignItem = parentItem`。
- Resize 分发（行 3070–3082）：`HasSelectedGlobalScoreCell` → `ResizeSelectedGlobalScoreCell`（行 3289）：写 `cell.X/Y/Width/Height`，调 `ClampSelectedGlobalScoreCell`（行 3433）。

### 8.4 模板与编排

- `FillMissingGlobalScoreCells`（行 1787）→ `GlobalScoreRowCellLayoutHelper.EnsureCompleteCells`。
- `AutoArrangeGlobalScoreCellsBySpacing`（行 1800）→ `AutoArrangeBySpacing`。
- `ApplyBo3GlobalScoreVisibilityTemplate`（行 1815）/`ApplyBo5GlobalScoreVisibilityTemplate`（行 1828）。
- 默认模板：`FrontedControlDefaultConfigFactory.cs:314` `GlobalScoreRowCellLayoutHelper.CreateCompleteCellTemplate()`。

### 8.5 Style Transfer 特判

- `ApplyParentStyleToGlobalScoreCells`（行 1841）：先 `EnsureCompleteCells`，再逐字段把父行样式应用到 cell（手写）。
- `ClearGlobalScoreCellStyleOverrides`（行 1863）：逐字段清空 cell override（恢复继承）。

### 8.6 运行时渲染

- `neo-bpsys-wpf/Controls/FrontedLayout/GlobalScoreRowFrontedControl.cs`：`GlobalScoreRowElement` 订阅 `ISharedDataService.CurrentGameChanged` + `MatchScoreState.PropertyChanged`，`RenderCells` 生成 `GlobalScorePresenter`（按 cell 配置 + `GlobalScoreRowDisplay.Create`）。`CreateDefaultCells`（obsolete）在 `Cells` 为空时回退。

**GlobalScore 特判结论**：`Cells` 是 FixedTemplate PartCollection 的原型——根据 BO3/BO5 模板补齐，不允许任意增删，可独立 Move/Resize/编辑。Phase 4 迁移；Phase 5 删除 `ApplyParentStyleToGlobalScoreCells`/`ClearGlobalScoreCellStyleOverrides`，改用统一 ParentFallback 继承。

---

## 9. 缺失插件处理

### 9.1 运行时

- `FrontedRenderer.cs`（行 80–109）：`factory is null && FrontedPluginControlType.IsPluginControlType(controlConfig.ControlType)`：
  - `context.RenderMissingPluginPlaceholders`（Designer 预览）：`CreateMissingPluginPlaceholder`（行 146）new `Border`（OrangeRed 边框、"Missing Plugin" 文本 + PackageId/ControlTypeName/ControlType），设 `Canvas.SetLeft/SetTop` + `Panel.SetZIndex`（从 config 读取），`Width`/`Height` 回退 `FrontedDesignerGeometryHelper.MinHitWidth/MinHitHeight`。然后走正常后处理（Visibility、IsGeneratedControl、BehaviorGuid、RegisterGeneratedName、EffectHost、GaussianBlur、Add to canvas）。
  - 否则（live 前台窗口）：`logger.LogWarning` + `continue` 跳过。
  - **关键**：Config 不被修改，保持 `PluginFrontedControlConfig` + `ExtensionData` 原样，不写默认值。

### 9.2 Materializer

- `FrontedPluginControlConfigMaterializer.Materialize`：`config is not PluginFrontedControlConfig || controlRegistry?.GetPluginDescriptor(...) is null` → 原样返回 config。保证缺失插件时不强制类型化。

### 9.3 Designer

- `FrontedLayoutDesignConverter.FromConfig`：调用 Materialize，缺失插件时 Config 保持 `PluginFrontedControlConfig`，`FrontedControlDesignItem.Config` 指向它。
- `FrontedPropertyGridBuilder.AddMissingPluginRows`（行 358）：显示可编辑 Layout 字段（Left/Top/Width/Height/ZIndex/BindingPath）+ 只读 PackageId/ControlTypeName/ExtensionData keys + 安装引导文案 + GaussianBlur。控件可在 Designer 中选择、移动、缩放、删除。

### 9.4 持久化

- `PluginFrontedControlConfig.ExtensionData`（`[JsonExtensionData]`）原样保留未知字段。
- `.bpui` Importer/Exporter 保留控件 JSON、`ExtensionData`、依赖元数据（见 `neo-bpsys-wpf.PluginSdk/README.md`「缺失插件行为」）。

**缺失插件结论**：当前链路已满足「ExtensionData 原样保留 + 不写默认值 + Designer placeholder + 根控件可操作」。新方案的 `IFrontedV3StorageAccessor` 对 `ExtensionData` 的读写必须延续此契约。

---

## 10. 每个拟新增类型的项目和目录

> 落点原则：Core 项目放接口/模型/服务（无 WPF 依赖），主项目放 WPF 控件/附加属性/VisualTree，PluginSdk 暴露插件 API，Extensions 放注册扩展。新建 `V3` 子命名空间以隔离迁移期。

### Phase 1：Registration + Property Schema

| 新类型 | 项目 | 目录 |
| --- | --- | --- |
| `FrontedV3ControlAttribute` | `neo-bpsys-wpf.PluginSdk` | 根（插件 API 入口） |
| `FrontedV3ControlBase` | `neo-bpsys-wpf.PluginSdk` | 根（插件基类，抽象 `FrameworkElement`） |
| `FrontedV3ControlContext` | `neo-bpsys-wpf.Core` | `Abstractions/Services/`（替代 `FrontedControlBuildContext`） |
| `FrontedV3ControlRegistration` | `neo-bpsys-wpf.Core` | `Models/FrontedLayout/V3/` |
| `FrontedV3Property<T>` / `FrontedV3PropertyDefinition` / `FrontedV3PropertyMetadata` | `neo-bpsys-wpf.Core` | `Models/FrontedLayout/V3/Properties/` |
| `IFrontedV3StorageAccessor` | `neo-bpsys-wpf.Core` | `Abstractions/Services/` |
| `FrontedV3Storage`（静态工厂） | `neo-bpsys-wpf.Core` | `Services/FrontedLayout/V3/` |
| `FrontedV3OptionsView` | `neo-bpsys-wpf.Core` | `Models/FrontedLayout/V3/Options/`（动态代理） |
| 统一 Registry 新入口 | `neo-bpsys-wpf.Core` | `Services/FrontedLayout/V3/FrontedV3ControlRegistry.cs`（与现有 `FrontedControlRegistry` 并存） |
| `AddFrontedV3Control<T>()` | `neo-bpsys-wpf.Core` | `Extensions/Registry/FrontedV3ControlRegistryExtensions.cs` |

### Phase 2：Host

| 新类型 | 项目 | 目录 |
| --- | --- | --- |
| `FrontedV3ControlHost`（WPF ContentControl/Decorator） | `neo-bpsys-wpf` | `Controls/FrontedLayout/V3/` |
| `RootControlGeometryTarget` | `neo-bpsys-wpf.Core` | `Services/FrontedLayout/V3/Geometry/` |
| Control error boundary | `neo-bpsys-wpf` | `Controls/FrontedLayout/V3/`（Host 内部） |

### Phase 3：固定 Part

| 新类型 | 项目 | 目录 |
| --- | --- | --- |
| `FrontedV3PartDefinition` / `FrontedV3PartGeometry` / `FrontedV3PartCapabilities` | `neo-bpsys-wpf.Core` | `Models/FrontedLayout/V3/Parts/` |
| `FrontedV3Part.Register<T>()`（API） | `neo-bpsys-wpf.Core` | `Services/FrontedLayout/V3/Parts/` |
| `FrontedV3.PartId` AttachedProperty | `neo-bpsys-wpf` | `Controls/FrontedLayout/V3/`（WPF 附加属性） |
| `FrontedV3PartVisualAttribute` | `neo-bpsys-wpf.PluginSdk` | 根（插件标注） |
| `FixedPartGeometryTarget` | `neo-bpsys-wpf.Core` | `Services/FrontedLayout/V3/Geometry/` |
| Part property context | `neo-bpsys-wpf.Core` | `Models/FrontedLayout/V3/Parts/` |

### Phase 4：PartCollection

| 新类型 | 项目 | 目录 |
| --- | --- | --- |
| `FrontedV3PartCollectionDefinition` | `neo-bpsys-wpf.Core` | `Models/FrontedLayout/V3/Collections/` |
| `FrontedV3Parts.RegisterCollection` | `neo-bpsys-wpf.Core` | `Services/FrontedLayout/V3/Collections/` |
| `CollectionItemGeometryTarget` | `neo-bpsys-wpf.Core` | `Services/FrontedLayout/V3/Geometry/` |
| `FixedTemplate`/`Dynamic`/`ReadOnly` 策略 | `neo-bpsys-wpf.Core` | `Models/FrontedLayout/V3/Collections/` |
| Collection item property context / storage accessor | `neo-bpsys-wpf.Core` | `Models/FrontedLayout/V3/Collections/` + `Services/FrontedLayout/V3/Collections/` |

### Phase 5：StyleTransfer

| 新类型 | 项目 | 目录 |
| --- | --- | --- |
| `FrontedV3PropertySemantic` / `FrontedV3PropertyInheritance` / `FrontedV3PropertyTransfer` | `neo-bpsys-wpf.Core` | `Models/FrontedLayout/V3/StyleTransfer/` |
| `FrontedV3StyleTransferProfile` / `FrontedV3StyleComponent` | `neo-bpsys-wpf.Core` | `Models/FrontedLayout/V3/StyleTransfer/` |
| `FrontedV3StyleTransferService` | `neo-bpsys-wpf.Core` | `Services/FrontedLayout/V3/StyleTransfer/` |

### Phase 6：Designer 去特化

| 新类型 | 项目 | 目录 |
| --- | --- | --- |
| `FrontedV3DesignSelection` / `FrontedV3DesignSubTarget` | `neo-bpsys-wpf.Core` | `Models/FrontedLayout/Designer/V3/` |
| `FrontedV3FixedPartTarget` / `FrontedV3CollectionItemTarget` | `neo-bpsys-wpf.Core` | `Models/FrontedLayout/Designer/V3/` |
| `IFrontedV3GeometryTarget` | `neo-bpsys-wpf.Core` | `Abstractions/Services/` |
| 通用 PropertyGrid / Move/Resize/Snap/Clamp/Undo | `neo-bpsys-wpf.Core` + `neo-bpsys-wpf` | `Services/FrontedLayout/V3/Design/` + `ViewModels/...` |

---

## 11. 每个旧类型计划在哪个 Phase 删除

| 旧类型 / 特判 | 删除 Phase | 备注 |
| --- | --- | --- |
| `BorderedImageResizeTarget` 枚举 + `ResizeSelectedBorderedImageInnerImage` + `IsBorderedImageBorderResizeTarget`/`IsBorderedImageImageResizeTarget` + 相关 selection flags/命令 | **Phase 3** | 由固定 Part（`Image`/`ImageWidth`/`ImageHeight`/Resize）替代 |
| `SelectedMapV2InternalStylePart` + `MapV2InternalStylePartOptions` + `MoveSelectedMapV2InternalPart` + `ResizeSelectedMapV2InternalPart` + `ClampSelectedMapV2InternalPart` + `IsMapV2InternalStyleEditorVisible` + `SelectedMapV2InternalPartLayout` + 相关 selection/geometry 分支 | **Phase 4** | 由固定 Part + `FixedPartGeometryTarget` 替代 |
| `SelectedGlobalScoreCell` + `SelectedGlobalScoreCellParentName`/`SelectedGlobalScoreCellId` + `HasSelectedGlobalScoreCell` + `HasGlobalScoreCellEditor` + `GlobalScoreCellEditorItems` + `MoveSelectedGlobalScoreCell` + `ResizeSelectedGlobalScoreCell` + `ClampSelectedGlobalScoreCell` + 专用 selection/geometry 分支 | **Phase 4** | 由 `FixedTemplate` PartCollection + `CollectionItemGeometryTarget` 替代 |
| `ApplyMapV2DisplayStyleToAll` + `CopyMapV2DisplayStyle` + `ApplyMapV2DisplayBehaviorSetToTargets` + `RegenerateMapV2BehaviorGraphIds` + `RewriteMapV2BehaviorTargetsAndFilters` + 几十个逐字段 copy | **Phase 5** | 由 `FrontedV3StyleTransferService`（peer 传播，精确 canonical type 匹配）替代 |
| `ApplyParentStyleToGlobalScoreCells` + `ClearGlobalScoreCellStyleOverrides` | **Phase 5** | 由 `ParentFallback`/`LockedToParent` 继承 + Apply Parent Style/Clear Child Overrides 替代 |
| PropertyGrid 反射 `AddConfigRows` 路径 + `FrontedPropertyGridBuilder.ResolveGroupName`/`IsVisibleProperty`/`ResolveBindingTargetKind` 中所有 `is BorderedImageFrontedControlConfig`/`is MapV2DisplayControlConfig`/`is ShapeFrontedControlConfigBase`/`is ImageFrontedControlConfig` 类型特判 + propertyName 字符串反射写入 | **Phase 6** | 由 Schema 驱动 PropertyGrid + `PropertyDefinition.Storage.SetValue()` 替代；可保留只读 legacy diagnostic view |
| `IFrontedControlPluginContributor` + `IFrontedControlPluginRegistry` + `FrontedPluginControlDescriptor<TConfig>` + `IFrontedPluginControlDescriptor` + `FrontedControlPluginRegistry` + `FrontedPluginControlAdapter<TConfig>` + `AddFrontedPluginControlContributor<T>()` + `TeamCardFrontedControlContributor` + 插件专用 Config 强制要求 + 插件 `CreateControl`/`CreateDefaultConfig`/`Properties` descriptor list | **Phase 7** | 由 `FrontedV3ControlAttribute` + `FrontedV3ControlBase` + `AddFrontedV3Control<T>()` + `FrontedV3Property<T>` 全面替代，不保留 Obsolete shim/adapter/facade |
| `IFrontedControl`（内置工厂接口） + 内置 `XxxFrontedControl : IFrontedControl` 工厂类 | **Phase 7** | 内置控件迁移到 `FrontedV3ControlBase` 子类后删除工厂接口 |
| `FrontedControlDefaultConfigFactory` 的硬编码 `AddableControlTypes` 白名单 + `CreateDefault` switch | **Phase 7** | 由新 Registration 暴露的默认配置能力替代 |
| `FrontedPluginControlConfigMaterializer`（JSON round-trip 转换） | **Phase 7** | 新 Storage 直接读写 Config CLR 属性 / ExtensionData，无需 JSON round-trip |

**删除原则**：Phase 1–6 期间新旧链路并存（旧链路为正式注册路径，新链路为增量），Phase 7 在前置条件（Phase 1–6 全通过 + 另一 Window 重构任务完成）满足后一次性删除旧架构，Registry 最终只维护 `CanonicalControlType → FrontedV3ControlRegistration`。

---

## 映射证明：新方案映射到现有模型，而非第二套孤立 Runtime

本节证明每个新类型都是对现有职责的** relocated / unified**，而非引入并行的第二套事实来源。

### 证明 1：Registration 映射现有两段式身份

- 现有：内置 `IFrontedControl.ControlType`（裸名 `Text`）+ 插件 `FrontedPluginControlDescriptor.FullControlType`（`plugin:{PackageId}/{ControlTypeName}`），canonical 格式由 `FrontedPluginControlType` 统一。
- 新方案：`FrontedV3ControlRegistration` 同样以 `CanonicalControlType` 为 key，内置裸名、插件 `plugin:{PackageId}/{ControlTypeName}`。**身份格式与 `FrontedPluginControlType` 完全一致**，`.bpui` 与布局 JSON 的 `ControlType` 字段不变。
- 结论：新 Registry 是旧 `_controls` 字典 + `_pluginDescriptors` 字典的合并视图，不是第二套身份体系。

### 证明 2：Storage 映射现有 Config 字段，JSON 不变

- 现有：`FrontedControlConfigBase` 根级字段（`Left`/`Top`/`Width`/`Height`/`ZIndex`/`Visibility`/`BehaviorGuid`/`IsGaussianBlurEnabled`/`GaussianBlurRadius`/`ControlType`）+ 各子类字段（`BorderedImageFrontedControlConfig.ImageWidth/ImageHeight`、`MapV2DisplayControlConfig.TeamNameColor` 等、`GlobalScoreRowControlConfig.Cells`）全部平铺序列化到 JSON 根。
- 新方案：`IFrontedV3StorageAccessor` 的 `GetValue(config)`/`SetValue(config, value)` 直接读写这些**同一批 CLR 属性**；插件场景读写 `PluginFrontedControlConfig.ExtensionData` 的同一批 key。`OptionsPath`（如 `Appearance.TextColor`）只是 PropertyGrid 分组与 StyleTransfer 匹配的逻辑键，**不进入 JSON**。
- 结论：Options 是现有 Config 根级字段的动态投影，不是独立缓存。序列化输出与现有 JSON 字节级一致（`SerializedJsonHasNoOptionsObject`）。

### 证明 3：Host 映射现有散落的根布局职责

- 现有根布局分散三处：控件自身设 Left/Top/ZIndex/Width/Height；Renderer 设 Visibility/BehaviorGuid/IsGeneratedControl/NameScope/GaussianBlur/EffectHost；ViewModel 直接写 Config.Left/Top/Width/Height。
- 新方案：`FrontedV3ControlHost` 一次性接管**全部**根属性。这不是新增一层，而是把分散的设置点**合并到唯一 Owner**。Renderer 不再设任何根属性，控件不再设 Canvas 坐标，ViewModel 不再直接写根字段（改走 `RootControlGeometryTarget`）。
- 结论：Host 是现有职责的 relocation，运行时视觉树从 `Canvas → Element` 变为 `Canvas → Host → ControlBase`，但根属性的最终值与来源 Config 字段完全相同。

### 证明 4：Part 映射 BorderedImage + MapV2 的现有内部几何

- BorderedImage：现有 `ImageWidth`/`ImageHeight` + `ResizeSelectedBorderedImageInnerImage` 写这俩字段。
- MapV2：现有 `InternalParts: List<MapV2InternalPartLayoutConfig>`（`Part`/`X`/`Y`/`Width`/`Height`）+ `MoveSelectedMapV2InternalPart`/`ResizeSelectedMapV2InternalPart` 写 `part.X/Y/Width/Height`。
- 新方案：`FrontedV3Part` + `FixedPartGeometryTarget`。BorderedImage 的 Part Storage 指向 `ImageWidth`/`ImageHeight`；MapV2 的 5 个 Part Storage 指向 `InternalParts[i].X/Y/Width/Height`（或 `TeamNameColor` 等颜色字段）。**写入的是同一批 JSON 字段**。
- 结论：Part 是对现有「内层几何 + 专用 resize 方法」的统一抽象，不是新增几何数据。

### 证明 5：PartCollection 映射 GlobalScore Cells

- 现有：`GlobalScoreRowControlConfig.Cells: List<GlobalScoreCellConfig>`（`Id`/`X`/`Y`/`Width`/`Height` + 可空继承字段）+ `GlobalScoreRowCellLayoutHelper.EnsureCompleteCells`/`AutoArrangeBySpacing`/`ApplyBo3/Bo5VisibilityTemplate` + `MoveSelectedGlobalScoreCell`/`ResizeSelectedGlobalScoreCell`。
- 新方案：`FixedTemplate` PartCollection。模板补齐沿用 `GlobalScoreRowCellLayoutHelper`，ItemKey 沿用 `GlobalScoreCellConfig.Id`，几何 Storage 指向 `cell.X/Y/Width/Height`。**Cell JSON 字段与 ID 不变**。
- 结论：PartCollection 是对 `Cells` 列表 + 专用 cell 编辑方法的统一抽象，模板策略与现有 Helper 完全一致。

### 证明 6：StyleTransfer 映射现有手写复制

- 现有：`ApplyMapV2DisplayStyleToAll`/`CopyMapV2DisplayStyle`（MapV2 逐字段 copy + 行为图重写）+ `ApplyParentStyleToGlobalScoreCells`/`ClearGlobalScoreCellStyleOverrides`（GlobalScore 父子继承）。
- 新方案：`FrontedV3StyleTransferService`。Peer 传播按 `CanonicalControlType` 精确匹配（与现有「只传播给同类型 MapV2」语义一致）；Parent-Child 继承按相同 `OptionsPath` 匹配（与现有「父行 FontFamily → 子 cell FontFamily」语义一致）。`ParentFallback` 动态读取对应现有「cell 字段为空时继承父行」。
- 结论：StyleTransfer 是对现有手写复制逻辑的规则化抽象，传播范围与继承语义与现有行为一致。

### 证明 7：缺失插件映射现有 ExtensionData 契约

- 现有：`PluginFrontedControlConfig.ExtensionData`（`[JsonExtensionData]`）原样保留 + Renderer placeholder + Materializer 原样返回 + PropertyGrid `AddMissingPluginRows`。
- 新方案：`IFrontedV3StorageAccessor` 对 `ExtensionData` 的读写延续同一契约；Host 错误边界在 Control 构造失败时显示占位、保留原 Config、不写默认值。
- 结论：缺失插件行为完全继承现有链路，新 Storage 不破坏 `ExtensionData` round-trip。

### 证明 8：创建路径单一化

- 现有创建路径：`Renderer → controlRegistry.GetControl → factory.Create → 控件自设 Canvas 坐标 → Renderer 后处理`。
- 新方案创建路径：`Renderer → controlRegistry.GetRegistration → Host.Create → ControlBase.InitializeFrontedV3 → Host 应用根布局`。
- 迁移期（Phase 1–6）：旧 `IFrontedControl` 路径与新 `FrontedV3ControlRegistration` 路径并存，但新路径走 Host，旧路径保留 legacy bridge（internal/单向/有删除计划/不暴露 SDK）。Phase 7 删除旧路径后，**只剩一条创建路径**。
- 结论：不存在第二套正式 Runtime，迁移期 bridge 是临时过渡，最终统一到 Host → ControlBase。

### 综合结论

新方案的每个类型都**指向同一批 Config 字段、同一份 JSON、同一套 canonical 身份**：
- `FrontedV3ControlRegistration` → 现有 `IFrontedControl` + `FrontedPluginControlDescriptor` 的合并身份；
- `IFrontedV3StorageAccessor` → 现有 CLR 属性 + `ExtensionData` 的同一读写点；
- `FrontedV3ControlHost` → 现有散落在控件 + Renderer + ViewModel 的根布局职责的唯一 Owner；
- `FrontedV3Part` / `FrontedV3PartCollection` → 现有 BorderedImage/MapV2/GlobalScore 的内部几何与 Cells 列表；
- `FrontedV3StyleTransferService` → 现有 MapV2/GlobalScore 手写复制逻辑；
- `FrontedV3DesignSelection` / `IFrontedV3GeometryTarget` → 现有三套专用 selection/Move/Resize 方法。

新方案是**对现有职责的统一与 relocated**，而非并行 Runtime。JSON 契约、canonical type、Config 字段、ExtensionData 保留语义全部不变。迁移期新旧并存（旧链为正式路径，新链为增量），Phase 7 删除旧架构后只剩单一正式 Runtime：`CanonicalControlType → FrontedV3ControlRegistration → FrontedV3ControlHost → FrontedV3ControlBase`。
