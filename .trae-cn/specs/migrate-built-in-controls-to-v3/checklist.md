# Checklist

> 验证 `migrate-built-in-controls-to-v3` spec 的完成情况。每项检查通过后勾选。

## 内置控件迁移验证

- [x] Core 简单控件（Text、Rectangle、Image、Polygon、BackgroundTintRectangle、BackgroundTintPolygon）均已重写为 `FrontedV3ControlBase` 子类，标注 `[FrontedV3Control("<ControlId>", IsBuiltIn = true)]`。
- [x] BorderedImage 已迁移为 `FrontedV3ControlBase` 子类，`ImageWidth`/`ImageHeight` JSON 字段不变。
- [x] 主项目业务控件（LocalizedText、MapNameText、GameProgressText、TalentTraitDisplay、MapV2Display、GlobalScoreRow）均已迁移为 `FrontedV3ControlBase` 子类。
- [x] 所有迁移后的控件不再实现 `IFrontedControl`，不再包含 `ControlType`/`ConfigType` 属性与 `Create` 方法。
- [x] 所有迁移后的控件不再调用 `Canvas.SetLeft`/`Canvas.SetTop`/`Panel.SetZIndex` 或在构造函数设置根 `Width`/`Height`（根布局由 `FrontedV3ControlHost` 负责）。
- [x] 业务服务（`ISharedDataService`、`ISettingsHostService` 等）通过 `FrontedV3ControlContext` 获取，在 `OnInitializeFrontedV3` 中初始化订阅。
- [x] 控件 Config 类未修改，JSON `ControlType` 值与迁移前一致（裸名或 `plugin:{PackageId}/{ControlId}`）。

## ExamplePlugin 迁移验证

- [x] `TeamCardFrontedControlContributor.cs` 已删除。
- [x] 新 `TeamCardControl : FrontedV3ControlBase` 标注 `[FrontedV3Control("TeamCard")]`（非 built-in），通过 `AddFrontedV3Control<TeamCardControl>()` 注册。
- [x] `TeamCardFrontedControlConfig` 字段不变，JSON 契约不变。

## 注册更新验证

- [x] `App.Services.xaml.cs` 中 13 个 `services.AddSingleton<IFrontedControl, XxxFrontedControl>()` 已全部移除，替换为 `services.AddBuiltInFrontedV3Control<XxxControl, XxxConfig>(...)`。
- [x] ExamplePlugin 插件初始化使用 `AddFrontedV3Control<TeamCardControl>()`，不再使用 `AddFrontedPluginControlContributor<T>()`。
- [x] `IFrontedV3ControlRegistry` 注册存在且可解析所有迁移后的控件。

## Registry 统一验证

- [x] Registry 最终只维护 `CanonicalControlType → FrontedV3ControlRegistration`，不再有 `IFrontedControl` 字典或 `_pluginDescriptors`。
- [x] `FrontedRenderer` 通过 `IFrontedV3ControlRegistry` 解析 Registration，经 `FrontedV3ControlHost` 创建并包装控件，不再调用 `IFrontedControl.Create`。
- [x] `FrontedPropertyGridBuilder` 通过 V3 Properties Schema 构建属性行，不再反射 Config 全部 public 属性，不再有 `is BorderedImageFrontedControlConfig`/`is MapV2DisplayControlConfig`/`is GlobalScoreRowControlConfig` 类型特判分支。
- [x] `FrontedLayoutDesignConverter` 不再依赖 `FrontedPluginControlConfigMaterializer`。
- [x] `FrontedControlDefaultConfigFactory` 不再使用硬编码 `AddableControlTypes` 白名单与 `CreateDefault` switch，改用 V3 Registration 的 `CreateDefaultConfig`。
- [x] 缺失插件 placeholder 逻辑保留：ExtensionData 原样保留、不写默认值、Designer 显示占位、根控件可移动/缩放/删除。

## 旧架构删除验证

- [x] `IFrontedControl.cs` 已删除。
- [x] `IFrontedControlPluginContributor.cs` 已删除。
- [x] `IFrontedControlPluginRegistry.cs` 已删除。
- [x] `IFrontedControlRegistry.cs` 已删除（若被 `IFrontedV3ControlRegistry` 取代）。
- [x] `FrontedPluginControlDescriptor.cs`（含泛型与非泛型）已删除。
- [x] `FrontedPluginControlType.cs` 保留（V3 身份验证仍活跃使用该类型解析 `plugin:{PackageId}/{ControlId}` canonical ID，未被取代）。
- [x] `FrontedControlPluginRegistry.cs` 已删除。
- [x] `FrontedPluginControlAdapter.cs` 已删除。
- [x] `FrontedPluginControlConfigMaterializer.cs` 已删除。
- [x] `FrontedControlRegistry.cs` 已删除（若被 `FrontedV3ControlRegistry` 取代）。
- [x] `FrontedPluginControlRegistryExtensions.cs`（`AddFrontedPluginControlContributor<T>()`）已删除。
- [x] `TeamCardFrontedControlContributor.cs` 已删除。
- [x] 未保留任何 Obsolete shim / adapter / facade / 旧注册路径 fallback。

## Build 与测试验证

- [x] `dotnet build neo-bpsys-wpf.slnx` 0 error / 0 warning（V3 相关警告归零；既有非 V3 警告 CS1591/CS8632/xUnit1051 等属范围外，不在本任务修复范围）。
- [x] V3 控件相关测试通过（既有范围外失败已列出）。
- [x] 全仓搜索旧符号为零（除 `docs/internal/designer-v3-control-refactor-audit.md`、`v3-control-refactor.md`、`docs/plugin-system.md`、`docs/fronted-designer-v3.md`、`neo-bpsys-wpf.PluginSdk/README.md`、本 spec 目录外）：`IFrontedControlPluginContributor`、`IFrontedControlPluginRegistry`、`FrontedPluginControlDescriptor`、`IFrontedPluginControlDescriptor`、`FrontedControlPluginRegistry`、`FrontedPluginControlAdapter`、`AddFrontedPluginControlContributor`、`TeamCardFrontedControlContributor`、`FrontedPluginControlConfigMaterializer`。
- [x] Designer 通用编辑路径不引用 `BorderedImageFrontedControlConfig`、`MapV2DisplayControlConfig`、`GlobalScoreRowControlConfig`（业务预览渲染代码除外）。

## 测试项目迁移验证（Task 9）

- [x] `BackgroundTintFrontedControlTest.cs` 不再引用 `IFrontedControl`；`RecordingControl` 已替换为 V3 测试控件（`FrontedV3ControlBase` 子类 + `[FrontedV3Control]`）或复用 `FrontedRendererBehaviorGuidTest.RecordingV3Control`。
- [x] `BackgroundTintFrontedControlTest.cs` 中 `FrontedControlRegistry` 引用已替换为 `FrontedV3ControlRegistry`；`RendererPassesEffectiveBo3BackgroundAndCanvasSizeToControl` 断言适配 V3 Host 路径。
- [x] `FrontedCanvasConfigTest.cs` 中 `TestPluginControlContributor`（`IFrontedControlPluginContributor`）及其相关测试方法已删除。
- [x] `FrontedCanvasConfigTest.cs` 中 `FakeFrontedControl`（`IFrontedControl`）已删除或迁移为 V3 测试控件；残留的 `FrontedControlRegistry` 引用已替换为 `FrontedV3ControlRegistry`。
- [x] `FrontedLayoutPluginDependencyPackageTest.cs` 中 `CreateRegistryWithExamplePlugin()` 与 `CreateTextOnlyRegistry()` 返回 `FrontedV3ControlRegistry`，不再使用 `FrontedControlRegistry`/`TeamCardFrontedControlContributor`。
- [x] `FrontedLayoutDesignerFoundationTest.cs` 中 `PluginFrontedControlRegistryForTests`（依赖 `FrontedPluginControlDescriptor<>`/`IFrontedPluginControlDescriptor`）及其相关测试方法已删除。
- [x] `FrontedLayoutDesignerFoundationTest.cs` 中 `KnownFrontedControlRegistry`（`IFrontedControlRegistry`）已迁移为 V3 等价（`FrontedV3ControlRegistry` 或工厂方法）；`KnownFrontedControl`（`IFrontedControl`）已删除。
- [x] `dotnet build neo-bpsys-wpf.Tests\neo-bpsys-wpf.Tests.csproj --no-dependencies` 0 error。
- [x] 被修改测试文件中的迁移后测试通过（既有范围外失败已列出）。
- [x] 测试项目中全仓搜索旧符号为零：`IFrontedControl`（非 V3）、`FrontedControlRegistry`、`IFrontedControlRegistry`、`IFrontedControlPluginContributor`、`IFrontedControlPluginRegistry`、`FrontedPluginControlDescriptor`、`IFrontedPluginControlDescriptor`。
- [x] 未新增 shim / adapter / duplicate interface / fallback 来掩盖新旧语义冲突。
- [x] 未执行 `git reset`/`git restore`/`git checkout`/`git stash`/`git clean` 等有副作用 Git 命令。

## 契约保护验证

- [x] 内置控件 JSON 序列化输出与迁移前一致：根级平铺字段，无 `Options` 嵌套对象。
- [x] `PluginFrontedControlConfig.ExtensionData` round-trip 保留未知字段。
- [x] 未执行 `git reset`/`git restore`/`git checkout`/`git stash`/`git clean` 等有副作用 Git 命令。
- [x] 未新增 shim / adapter / duplicate interface / duplicate model / fallback / 临时兼容构造函数。
- [x] 未修改 `.bpui` 和布局 JSON 契约。

## V3 警告修复验证（Task 8.5～8.7）

- [x] V3 源文件 CS1574 XML cref 警告全部修复（`IFrontedV3StorageAccessor.cs`、`FrontedV3LayoutWindowRegistryExtensions.cs`、`FrontedV3DesignSelection.cs`、`FrontedV3PropertyMetadata.cs`、`FrontedV3Property{T}.cs`、`FrontedV3ControlAttribute.cs`、`FrontedV3ControlBase.cs`、`FrontedV3PartVisualAttribute.cs`）。
- [x] V3 测试文件 CS8632 nullable 警告全部修复（`FrontedV3StyleTransferTest.cs`、`FrontedV3ControlRegistrationTest.cs` 添加 `#nullable enable`）。
- [x] 重新构建确认 V3 相关警告归零（仅余既有非 V3 警告）。
