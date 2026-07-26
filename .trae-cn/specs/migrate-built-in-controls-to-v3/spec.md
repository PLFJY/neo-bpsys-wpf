# 迁移内置控件到 V3 并删除旧 Control 架构 Spec

> 本 spec 对应仓库根目录 `v3-control-refactor.md` 的 **Phase 7（第一部分）**：删除旧插件 Control 架构并迁移内置控件。Phase 1～6 已完成，V3 基础设施（`FrontedV3ControlBase`、`FrontedV3ControlAttribute`、`AddBuiltInFrontedV3Control<TControl,TConfig>`、`FrontedV3ControlHost`、`IFrontedV3ControlRegistry`、`FrontedV3Property<T>`、Part/PartCollection、StyleTransfer、Designer 去特化）均已就绪。

## Why

当前内置控件（Text、Rectangle、Image、BorderedImage、Polygon、BackgroundTintRectangle、BackgroundTintPolygon、LocalizedText、MapV2Display、GlobalScoreRow、TalentTraitDisplay、GameProgressText、MapNameText）仍通过旧 `IFrontedControl` 工厂接口注册与创建，插件控件仍通过 `IFrontedControlPluginContributor` → `FrontedPluginControlDescriptor` → `FrontedPluginControlAdapter` 三段式链路接入。这与 Phase 1～6 已建立的 `CanonicalControlType → FrontedV3ControlRegistration → FrontedV3ControlHost → FrontedV3ControlBase` 单一正式运行时并存，形成两套注册/创建链路。Phase 7 需要把所有内置控件迁移到 V3 基类，删除旧架构，让 Registry 只维护 V3 Registration。

## What Changes

### 内置控件迁移（Core 项目简单控件）
- 将 `TextFrontedControl`、`RectangleFrontedControl`、`ImageFrontedControl`、`PolygonFrontedControl`、`BackgroundTintRectangleFrontedControl`、`BackgroundTintPolygonFrontedControl` 从 `IFrontedControl` 工厂重写为 `FrontedV3ControlBase` 子类，标注 `[FrontedV3Control("<ControlId>", IsBuiltIn = true)]`。
- 移除控件内 `Canvas.SetLeft/SetTop/Panel.SetZIndex/Width/Height` 根布局设置（由 `FrontedV3ControlHost` 统一负责）。
- 保留 Config 类与 JSON 契约不变。

### 内置控件迁移（Core 项目 BorderedImage）
- 将 `BorderedImageFrontedControl` 迁移到 `FrontedV3ControlBase`，内部 Image 通过 Phase 3 已建立的固定 Part 注册（Id=`Image`，Storage=`ImageWidth`/`ImageHeight`，Capabilities=Resize）。

### 内置控件迁移（主项目业务控件）
- 将 `LocalizedTextFrontedControl`、`MapV2DisplayFrontedControl`、`GlobalScoreRowFrontedControl`、`TalentTraitDisplayFrontedControl`、`GameProgressTextFrontedControl`、`MapNameTextFrontedControl` 迁移到 `FrontedV3ControlBase`。
- 业务逻辑（事件订阅、共享数据访问、资源解析）通过 `FrontedV3ControlContext` 获取服务，移除根布局设置。
- MapV2Display 的 5 个固定内部部件、GlobalScoreRow 的 Cells 通过 Phase 4 已建立的 Part/PartCollection 机制表达。

### ExamplePlugin 迁移（最小，为删除插件架构做准备）
- 将 `TeamCardFrontedControlContributor`（contributor + descriptor + CreateControl）迁移为 `TeamCardControl : FrontedV3ControlBase`，通过 `AddFrontedV3Control<TeamCardControl>()` 注册。
- StatusBadge 纯 C# 示例不在本部分范围。

### 注册更新
- `App.Services.xaml.cs`：移除所有 `services.AddSingleton<IFrontedControl, XxxFrontedControl>()` 注册，替换为 `services.AddBuiltInFrontedV3Control<XxxControl, XxxConfig>(...)`。
- ExamplePlugin 注册改为 `services.AddFrontedV3Control<TeamCardControl>()`。

### Registry 统一
- `FrontedControlRegistry` / `IFrontedControlRegistry` 委托给 `IFrontedV3ControlRegistry`，或直接由 `FrontedV3ControlRegistry` 取代，最终只维护 `CanonicalControlType → FrontedV3ControlRegistration`。
- 更新 `FrontedRenderer`、`FrontedPropertyGridBuilder`、`FrontedLayoutDesignConverter`、`FrontedControlDefaultConfigFactory`、`FrontedLayoutValidator` 等依赖旧 Registry 的服务，改用 V3 Registration 路径。

### 删除旧架构
- 删除：`IFrontedControl`、`IFrontedControlPluginContributor`、`IFrontedControlPluginRegistry`、`IFrontedControlRegistry`（如被取代）、`FrontedPluginControlDescriptor<TConfig>`、`IFrontedPluginControlDescriptor`、`FrontedControlPluginRegistry`、`FrontedPluginControlAdapter<TConfig>`、`FrontedPluginControlConfigMaterializer`、`AddFrontedPluginControlContributor<T>()`、`TeamCardFrontedControlContributor`、插件专用 Config 强制要求、插件 CreateControl/CreateDefaultConfig/Properties descriptor list。**BREAKING**（插件 API 变更，但 Phase 1 已提供 `AddFrontedV3Control<T>()` 替代）。
- 不得保留 Obsolete shim / adapter / facade / 旧注册路径 fallback。

## Impact
- 受影响 spec：Designer V3 Control 重构 Phase 7（`v3-control-refactor.md`）。
- 受影响代码：
  - `neo-bpsys-wpf.Core/Abstractions/Services/`（删除旧接口）
  - `neo-bpsys-wpf.Core/Services/FrontedLayout/`（删除旧工厂、Registry、Adapter、Materializer；统一 Registry）
  - `neo-bpsys-wpf.Core/Models/FrontedLayout/`（删除 FrontedPluginControlDescriptor 等）
  - `neo-bpsys-wpf.Core/Extensions/Registry/`（删除 FrontedPluginControlRegistryExtensions）
  - `neo-bpsys-wpf/Controls/FrontedLayout/`（重写主项目控件）
  - `neo-bpsys-wpf/App.Services.xaml.cs`（更新注册）
  - `neo-bpsys-wpf.ExamplePlugin/`（迁移 TeamCard）
  - 依赖旧 Registry/Control 的服务（Renderer、PropertyGrid、DesignConverter、DefaultConfigFactory、LayoutValidator 等）
- JSON 契约不变：`ControlType`、根级字段、`ExtensionData` 全部保持。

## ADDED Requirements

### Requirement: 内置控件统一继承 FrontedV3ControlBase
所有内置前台控件 SHALL 继承 `FrontedV3ControlBase`，标注 `[FrontedV3Control("<ControlId>", IsBuiltIn = true)]`，通过 `AddBuiltInFrontedV3Control<TControl, TConfig>()` 注册。控件 SHALL NOT 实现 `IFrontedControl`，SHALL NOT 设置自身 Canvas 坐标（Left/Top/ZIndex/Width/Height）。

#### Scenario: 内置控件注册后可被 V3 Registry 解析
- **WHEN** 宿主调用 `AddBuiltInFrontedV3Control<TextControl, TextFrontedControlConfig>(...)`
- **THEN** `IFrontedV3ControlRegistry` 按 CanonicalControlType `"Text"` 返回 `FrontedV3ControlRegistration`，`ControlType` 为 `FrontedV3ControlBase` 子类。

#### Scenario: 控件不再设置根布局
- **WHEN** V3 控件被创建并包装进 `FrontedV3ControlHost`
- **THEN** Canvas.Left/Top/ZIndex/Width/Height 全部由 Host 设置，控件自身代码不调用 `Canvas.SetLeft`/`Canvas.SetTop`/`Panel.SetZIndex`。

### Requirement: 旧插件 Control 架构完全删除
删除后全仓搜索旧符号（除历史迁移文档外）必须为 0。

#### Scenario: 旧符号扫描为零
- **WHEN** 全仓搜索 `IFrontedControlPluginContributor`、`IFrontedControlPluginRegistry`、`FrontedPluginControlDescriptor`、`IFrontedPluginControlDescriptor`、`FrontedControlPluginRegistry`、`FrontedPluginControlAdapter`、`AddFrontedPluginControlContributor`、`TeamCardFrontedControlContributor`
- **THEN** 除 `docs/internal/designer-v3-control-refactor-audit.md` 等历史迁移文档外，无任何代码引用。

### Requirement: Registry 单一事实来源
Registry 最终只维护 `CanonicalControlType → FrontedV3ControlRegistration`，内置与插件不保留两套正式注册链。

#### Scenario: Registry 解析内置与插件控件
- **WHEN** 通过 CanonicalControlType 查询 Registry
- **THEN** 内置（如 `"Text"`）与插件（如 `"plugin:plfjy.ExamplePlugin/TeamCard"`）均返回 `FrontedV3ControlRegistration`，不存在 `IFrontedControl` 适配路径。

### Requirement: 测试项目不再引用已删除的旧 Control 架构
Task 7 删除旧架构后，`neo-bpsys-wpf.Tests` 项目 SHALL NOT 引用任何已删除类型（`IFrontedControl`、`IFrontedControlRegistry`、`IFrontedControlPluginContributor`、`IFrontedControlPluginRegistry`、`FrontedControlRegistry`、`IFrontedPluginControlDescriptor`、`FrontedPluginControlDescriptor<>`）。测试 REMOVED 功能的方法 SHALL 被删除；测试 CURRENT 行为的方法 SHALL 用 V3 等价物（`FrontedV3ControlRegistry`、`FrontedV3ControlRegistration`、V3 测试控件）替换旧 setup。

#### Scenario: 测试项目编译通过
- **WHEN** 运行 `dotnet build neo-bpsys-wpf.Tests\neo-bpsys-wpf.Tests.csproj --no-dependencies`
- **THEN** 0 error，不再有 CS0246 引用已删除旧类型的错误。

#### Scenario: 测试项目中无旧符号残留
- **WHEN** 在 `neo-bpsys-wpf.Tests` 目录搜索 `IFrontedControl`（非 V3）、`FrontedControlRegistry`、`IFrontedControlRegistry`、`IFrontedControlPluginContributor`、`IFrontedControlPluginRegistry`、`FrontedPluginControlDescriptor`、`IFrontedPluginControlDescriptor`
- **THEN** 结果为 0。

#### Scenario: 旧插件架构测试被删除
- **WHEN** 检查测试项目
- **THEN** 不存在验证 `IFrontedControlPluginContributor.RegisterFrontedControls`、`FrontedPluginControlDescriptor<>.CreateControl`、`IFrontedPluginControlDescriptor.Properties` 等旧插件 descriptor/contributor 链路的测试方法。

#### Scenario: 现有行为测试用 V3 setup
- **WHEN** 测试需要向 `FrontedRenderer` 或 `FrontedLayoutValidator` 提供控件 registry
- **THEN** 使用 `FrontedV3ControlRegistry`（构造自 `FrontedV3ControlRegistration` 集合），不再使用 `FrontedControlRegistry` 或 fake `IFrontedControl`。

## MODIFIED Requirements

### Requirement: 内置控件 JSON 契约
内置控件迁移到 V3 后，JSON 序列化输出 SHALL 与迁移前字节级一致：根级平铺字段，无 `Options` 嵌套对象，`ControlType` 为裸名（内置）或 `plugin:{PackageId}/{ControlId}`（插件）。

## REMOVED Requirements

### Requirement: 旧 IFrontedControl 工厂接口
**Reason**: 由 `FrontedV3ControlBase` + `AddBuiltInFrontedV3Control<TControl,TConfig>()` 全面替代，Phase 1～6 已建立单一 V3 运行时。
**Migration**: 内置控件重写为 `FrontedV3ControlBase` 子类；插件控件改用 `AddFrontedV3Control<T>()`。无 Obsolete shim/adapter/fallback。

### Requirement: 旧插件 Contributor/Descriptor/Adapter 三段式注册
**Reason**: 插件作者通过 `AddFrontedV3Control<T>()` 单步注册，不再需要 contributor + descriptor + adapter 链路。
**Migration**: ExamplePlugin 的 `TeamCardFrontedControlContributor` 重写为 `TeamCardControl : FrontedV3ControlBase`，通过 `AddFrontedV3Control<TeamCardControl>()` 注册。
