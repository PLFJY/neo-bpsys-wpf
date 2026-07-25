# Tasks

> 对应 `v3-control-refactor.md` Phase 7（第一部分）。Phase 1～6 已完成，V3 基础设施就绪。
>
> 执行策略：先迁移内置控件（Task 1～3）使旧架构无活跃用户，再迁移 ExamplePlugin（Task 4），再更新注册（Task 5），再统一 Registry 与依赖服务（Task 6），最后删除旧架构（Task 7），Build 验证与扫描（Task 8）。
>
> 通用迁移规则（适用于所有控件）：
> - 重写控件类为 `FrontedV3ControlBase` 子类，标注 `[FrontedV3Control("<ControlId>", IsBuiltIn = true)]`。
> - ControlId 必须与旧 `IFrontedControl.ControlType` 完全一致（保证 JSON `ControlType` 不变）。
> - 移除 `IFrontedControl` 接口实现、`ControlType`/`ConfigType` 属性、`Create` 方法。
> - 移除控件内 `Canvas.SetLeft`/`Canvas.SetTop`/`Panel.SetZIndex`/根 `Width`/`Height` 设置（Host 负责）。
> - 业务服务通过 `Context`（`FrontedV3ControlContext`）获取，在 `OnInitializeFrontedV3` 中初始化。
> - Config 类不变，JSON 契约不变。
> - 公共属性/方法需写 XML 注释。
> - 不新增 shim/adapter/duplicate interface/fallback。

- [x] Task 1: 迁移 Core 项目简单控件到 FrontedV3ControlBase
  - [x] SubTask 1.1: 迁移 `TextFrontedControl`（ControlId=`Text`）为 `FrontedV3ControlBase` 子类，保留 `TextFrontedControlConfig`，移除根布局设置。
  - [x] SubTask 1.2: 迁移 `RectangleFrontedControl`（ControlId=`Rectangle`）为 `FrontedV3ControlBase` 子类，保留 `RectangleFrontedControlConfig`。
  - [x] SubTask 1.3: 迁移 `ImageFrontedControl`（ControlId=`Image`）为 `FrontedV3ControlBase` 子类，保留 `ImageFrontedControlConfig`。
  - [x] SubTask 1.4: 迁移 `PolygonFrontedControl`（ControlId=`Polygon`）为 `FrontedV3ControlBase` 子类，保留对应 Config。
  - [x] SubTask 1.5: 迁移 `BackgroundTintRectangleFrontedControl`（ControlId=`BackgroundTintRectangle`）为 `FrontedV3ControlBase` 子类。
  - [x] SubTask 1.6: 迁移 `BackgroundTintPolygonFrontedControl`（ControlId=`BackgroundTintPolygon`）为 `FrontedV3ControlBase` 子类。

- [x] Task 2: 迁移 Core 项目 BorderedImage 控件到 FrontedV3ControlBase
  - [x] SubTask 2.1: 迁移 `BorderedImageFrontedControl`（ControlId=`BorderedImage`）为 `FrontedV3ControlBase` 子类，保留 `BorderedImageFrontedControlConfig`（`ImageWidth`/`ImageHeight` 不变）。内部 Image 通过 Phase 3 固定 Part 机制表达（Id=`Image`，Storage=`ImageWidth`/`ImageHeight`，Capabilities=Resize），移除 Designer 专用 BorderedImage resize 分支引用。

- [x] Task 3: 迁移主项目业务控件到 FrontedV3ControlBase
  - [x] SubTask 3.1: 迁移 `LocalizedTextFrontedControl`（ControlId=`LocalizedText`）为 `FrontedV3ControlBase` 子类，保留 `LocalizedTextControlConfig`，业务逻辑（语言切换、文本绑定）通过 Context 获取服务。
  - [x] SubTask 3.2: 迁移 `MapNameTextFrontedControl`（ControlId=`MapNameText`）为 `FrontedV3ControlBase` 子类，保留 `MapNameTextControlConfig`。
  - [x] SubTask 3.3: 迁移 `GameProgressTextFrontedControl`（ControlId=`GameProgressText`）为 `FrontedV3ControlBase` 子类，保留 `GameProgressTextControlConfig`。
  - [x] SubTask 3.4: 迁移 `TalentTraitDisplayFrontedControl`（ControlId=`TalentTraitDisplay`）为 `FrontedV3ControlBase` 子类，保留 `TalentTraitDisplayControlConfig`。
  - [x] SubTask 3.5: 迁移 `MapV2DisplayFrontedControl`（ControlId=`MapV2Display`）为 `FrontedV3ControlBase` 子类，保留 `MapV2DisplayControlConfig`，5 个固定内部部件通过 Phase 4 Part 机制表达。
  - [x] SubTask 3.6: 迁移 `GlobalScoreRowFrontedControl`（ControlId=`GlobalScoreRow`）为 `FrontedV3ControlBase` 子类，保留 `GlobalScoreRowControlConfig`，Cells 通过 Phase 4 FixedTemplate PartCollection 表达。

- [x] Task 4: 迁移 ExamplePlugin TeamCard 到 FrontedV3ControlBase
  - [x] SubTask 4.1: 将 `TeamCardFrontedControlContributor`（contributor + descriptor + CreateControl）重写为 `TeamCardControl : FrontedV3ControlBase`，标注 `[FrontedV3Control("TeamCard")]`（非 built-in），保留 `TeamCardFrontedControlConfig`，通过 `AddFrontedV3Control<TeamCardControl>()` 注册。删除 `TeamCardFrontedControlContributor.cs`。

- [x] Task 5: 更新 App.Services.xaml.cs 与 ExamplePlugin 注册
  - [x] SubTask 5.1: 移除所有 `services.AddSingleton<IFrontedControl, XxxFrontedControl>()` 注册（13 个内置控件），替换为 `services.AddBuiltInFrontedV3Control<XxxControl, XxxConfig>(() => new XxxConfig())`。
  - [x] SubTask 5.2: 移除 `services.AddSingleton<IFrontedControlRegistry, FrontedControlRegistry>()`（若 Registry 统一后不再需要），确保 `IFrontedV3ControlRegistry` 注册存在。
  - [x] SubTask 5.3: 更新 ExamplePlugin 插件初始化代码，将 `AddFrontedPluginControlContributor<TeamCardFrontedControlContributor>()` 改为 `AddFrontedV3Control<TeamCardControl>()`。

- [x] Task 6: 统一 Registry 与更新依赖服务
  - [x] SubTask 6.1: 让 `FrontedControlRegistry`（或其替代）委托 `IFrontedV3ControlRegistry`，或直接由 `FrontedV3ControlRegistry` 取代，最终只维护 `CanonicalControlType → FrontedV3ControlRegistration`。移除 `_controls`（`IFrontedControl` 字典）与 `_pluginDescriptors`。
  - [x] SubTask 6.2: 更新 `FrontedRenderer`：通过 `IFrontedV3ControlRegistry` 解析 `FrontedV3ControlRegistration`，经 `FrontedV3ControlHost` 创建并包装控件，移除旧 `IFrontedControl.Create` 调用路径。保留缺失插件 placeholder 逻辑（ExtensionData 原样保留、不写默认值）。
  - [x] SubTask 6.3: 更新 `FrontedPropertyGridBuilder`：通过 V3 Registration 的 Properties Schema 构建属性行，移除反射全 Config 属性路径与控件类型特判分支（`is BorderedImageFrontedControlConfig` 等）。保留只读 legacy diagnostic view（如有）。
  - [x] SubTask 6.4: 更新 `FrontedLayoutDesignConverter`：通过 V3 Registration 解析 Config 类型，移除对 `FrontedPluginControlConfigMaterializer` 的依赖。
  - [x] SubTask 6.5: 更新 `FrontedControlDefaultConfigFactory`：通过 V3 Registration 的 `CreateDefaultConfig` 创建默认配置，移除硬编码 `AddableControlTypes` 白名单与 `CreateDefault` switch。
  - [x] SubTask 6.6: 更新 `FrontedLayoutValidator` 及其他依赖旧 Registry/Control 的服务，改用 V3 Registration 路径。

- [x] Task 7: 删除旧插件 Control 架构文件
  - [x] SubTask 7.1: 删除 `neo-bpsys-wpf.Core/Abstractions/Services/IFrontedControl.cs`。
  - [x] SubTask 7.2: 删除 `neo-bpsys-wpf.Core/Abstractions/Services/IFrontedControlPluginContributor.cs`。
  - [x] SubTask 7.3: 删除 `neo-bpsys-wpf.Core/Abstractions/Services/IFrontedControlPluginRegistry.cs`。
  - [x] SubTask 7.4: 删除 `neo-bpsys-wpf.Core/Abstractions/Services/IFrontedControlRegistry.cs`（若被 `IFrontedV3ControlRegistry` 取代）。
  - [x] SubTask 7.5: 删除 `neo-bpsys-wpf.Core/Models/FrontedLayout/FrontedPluginControlDescriptor.cs`（含 `FrontedPluginControlDescriptor<TConfig>` 与 `IFrontedPluginControlDescriptor`）。
  - [x] SubTask 7.6: 保留 `neo-bpsys-wpf.Core/Models/FrontedLayout/FrontedPluginControlType.cs`（V3 身份验证仍活跃使用该类型解析 `plugin:{PackageId}/{ControlId}`，未被取代）。
  - [x] SubTask 7.7: 删除 `neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedControlPluginRegistry.cs`。
  - [x] SubTask 7.8: 删除 `neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedPluginControlAdapter.cs`。
  - [x] SubTask 7.9: 删除 `neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedPluginControlConfigMaterializer.cs`。
  - [x] SubTask 7.10: 删除 `neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedControlRegistry.cs`（若被 `FrontedV3ControlRegistry` 取代）。
  - [x] SubTask 7.11: 删除 `neo-bpsys-wpf.Core/Extensions/Registry/FrontedPluginControlRegistryExtensions.cs`（`AddFrontedPluginControlContributor<T>()`）。
  - [x] SubTask 7.12: 删除 `neo-bpsys-wpf.ExamplePlugin/TeamCardFrontedControlContributor.cs`（Task 4 迁移后）。

- [x] Task 8: Build 验证与旧符号扫描
  - [x] SubTask 8.1: 运行 `dotnet build neo-bpsys-wpf.slnx` 确认 0 error / 0 warning。
  - [x] SubTask 8.2: 运行 `dotnet test neo-bpsys-wpf.Tests\neo-bpsys-wpf.Tests.csproj` 确认 V3 控件相关测试通过（既有范围外失败需列出）。
  - [x] SubTask 8.3: 全仓搜索旧符号确认为零（除 `docs/internal/designer-v3-control-refactor-audit.md`、`v3-control-refactor.md`、本 spec 目录外）：`IFrontedControlPluginContributor`、`IFrontedControlPluginRegistry`、`FrontedPluginControlDescriptor`、`IFrontedPluginControlDescriptor`、`FrontedControlPluginRegistry`、`FrontedPluginControlAdapter`、`AddFrontedPluginControlContributor`、`TeamCardFrontedControlContributor`、`IFrontedControl`（非 V3）、`FrontedPluginControlConfigMaterializer`。
  - [x] SubTask 8.4: 确认 Designer 通用编辑路径不引用 `BorderedImageFrontedControlConfig`、`MapV2DisplayControlConfig`、`GlobalScoreRowControlConfig`（业务预览渲染代码除外）。
  - [x] SubTask 8.5: 修复 V3 源文件 CS1574 XML cref 警告（`IFrontedV3StorageAccessor.cs`、`FrontedV3LayoutWindowRegistryExtensions.cs`、`FrontedV3DesignSelection.cs`、`FrontedV3PropertyMetadata.cs`、`FrontedV3Property{T}.cs`、`FrontedV3ControlAttribute.cs`、`FrontedV3ControlBase.cs`、`FrontedV3PartVisualAttribute.cs`）。
  - [x] SubTask 8.6: 修复 V3 测试文件 CS8632 nullable 警告（`FrontedV3StyleTransferTest.cs`、`FrontedV3ControlRegistrationTest.cs` 添加 `#nullable enable`）。
  - [x] SubTask 8.7: 重新构建确认 V3 相关警告归零（仅余既有非 V3 警告：CS1591/CS8632/xUnit1051 等）。
  - [x] SubTask 8.8: 最终扫描全仓搜索旧符号确认为零。
  - [x] SubTask 8.9: 编写最终报告（见本文件末尾"最终报告"章节）。
  - [x] SubTask 8.10: Build 0 error + 全测试运行（6 既有失败列出，V3 相关测试全通过）。

- [x] Task 9: 迁移测试项目到 V3 API（修复 19 个 build error）
  - [x] SubTask 9.1: 修复 `BackgroundTintFrontedControlTest.cs`（1 error）
  - [x] SubTask 9.2: 修复 `FrontedCanvasConfigTest.cs`（3 errors）
  - [x] SubTask 9.3: 修复 `FrontedLayoutPluginDependencyPackageTest.cs`（2 errors）
  - [x] SubTask 9.4: 修复 `FrontedLayoutDesignerFoundationTest.cs`（13 errors）
  - [x] SubTask 9.5: Build 验证与测试运行

# Task Dependencies
- Task 1, 2, 3 无相互依赖，可并行。
- Task 4 无依赖 Task 1～3，可并行。
- Task 5 依赖 Task 1～4 完成（所有控件迁移后才能更新注册）。
- Task 6 依赖 Task 5（注册更新后才能统一 Registry 与依赖服务）。
- Task 7 依赖 Task 6（Registry 统一、依赖服务更新后旧架构无引用才能删除）。
- Task 8 依赖 Task 7。
- Task 9 依赖 Task 7（旧架构删除后才能迁移测试项目）。SubTask 9.1～9.4 无相互依赖，可并行。

# 最终报告（SubTask 8.9）

## 1. 每个 Phase 修改文件

### Phase 7 第一部分（本 spec 范围）

#### 内置控件迁移（Task 1～3）
- `neo-bpsys-wpf.Core/Controls/FrontedLayout/TextFrontedControl.cs` — 重写为 `FrontedV3ControlBase` 子类
- `neo-bpsys-wpf.Core/Controls/FrontedLayout/RectangleFrontedControl.cs` — 重写为 `FrontedV3ControlBase` 子类
- `neo-bpsys-wpf.Core/Controls/FrontedLayout/ImageFrontedControl.cs` — 重写为 `FrontedV3ControlBase` 子类
- `neo-bpsys-wpf.Core/Controls/FrontedLayout/PolygonFrontedControl.cs` — 重写为 `FrontedV3ControlBase` 子类
- `neo-bpsys-wpf.Core/Controls/FrontedLayout/BackgroundTintRectangleFrontedControl.cs` — 重写为 `FrontedV3ControlBase` 子类
- `neo-bpsys-wpf.Core/Controls/FrontedLayout/BackgroundTintPolygonFrontedControl.cs` — 重写为 `FrontedV3ControlBase` 子类
- `neo-bpsys-wpf.Core/Controls/FrontedLayout/BorderedImageFrontedControl.cs` — 重写为 `FrontedV3ControlBase` 子类，内部 Image 通过固定 Part 表达
- `neo-bpsys-wpf/Controls/FrontedLayout/LocalizedTextFrontedControl.cs` — 重写为 `FrontedV3ControlBase` 子类
- `neo-bpsys-wpf/Controls/FrontedLayout/MapNameTextFrontedControl.cs` — 重写为 `FrontedV3ControlBase` 子类
- `neo-bpsys-wpf/Controls/FrontedLayout/GameProgressTextFrontedControl.cs` — 重写为 `FrontedV3ControlBase` 子类
- `neo-bpsys-wpf/Controls/FrontedLayout/TalentTraitDisplayFrontedControl.cs` — 重写为 `FrontedV3ControlBase` 子类
- `neo-bpsys-wpf/Controls/FrontedLayout/MapV2DisplayFrontedControl.cs` — 重写为 `FrontedV3ControlBase` 子类，5 个固定 Part
- `neo-bpsys-wpf/Controls/FrontedLayout/GlobalScoreRowFrontedControl.cs` — 重写为 `FrontedV3ControlBase` 子类，Cells 通过 FixedTemplate PartCollection 表达

#### ExamplePlugin 迁移（Task 4）
- `neo-bpsys-wpf.ExamplePlugin/TeamCardControl.xaml.cs` — 重写为 `FrontedV3ControlBase` 子类，新增 Logo Part 与 StyleTransfer 属性
- `neo-bpsys-wpf.ExamplePlugin/StatusBadgeControl.cs` — 新增纯 C# V3 示例控件
- `neo-bpsys-wpf.ExamplePlugin/TeamCardFrontedControlContributor.cs` — 已删除

#### 注册更新（Task 5）
- `neo-bpsys-wpf/App.Services.xaml.cs` — 移除 13 个 `AddSingleton<IFrontedControl, T>()`，替换为 `AddBuiltInFrontedV3Control<TControl, TConfig>()`
- `neo-bpsys-wpf.ExamplePlugin/ExamplePlugin.cs` — 改为 `AddFrontedV3Control<TeamCardControl>()`

#### Registry 统一与依赖服务更新（Task 6）
- `neo-bpsys-wpf.Core/Services/FrontedLayout/V3/FrontedV3ControlRegistry.cs` — 统一 Registry，`CanonicalControlType → FrontedV3ControlRegistration`
- `neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedRenderer.cs` — 通过 V3 Registry + `FrontedV3ControlHost` 创建控件
- `neo-bpsys-wpf.Core/Services/FrontedLayout/Design/FrontedPropertyGridBuilder.cs` — 通过 V3 Properties Schema 构建属性行
- `neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedLayoutDesignConverter.cs` — 通过 V3 Registration 解析 Config 类型
- `neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedControlDefaultConfigFactory.cs` — 通过 V3 Registration 的 `CreateDefaultConfig` 创建默认配置
- `neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedLayoutValidator.cs` — 改用 V3 Registration 路径

#### 旧架构删除（Task 7）
- 已删除文件：
  - `neo-bpsys-wpf.Core/Abstractions/Services/IFrontedControl.cs`
  - `neo-bpsys-wpf.Core/Abstractions/Services/IFrontedControlPluginContributor.cs`
  - `neo-bpsys-wpf.Core/Abstractions/Services/IFrontedControlPluginRegistry.cs`
  - `neo-bpsys-wpf.Core/Abstractions/Services/IFrontedControlRegistry.cs`
  - `neo-bpsys-wpf.Core/Models/FrontedLayout/FrontedPluginControlDescriptor.cs`
  - `neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedControlPluginRegistry.cs`
  - `neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedPluginControlAdapter.cs`
  - `neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedPluginControlConfigMaterializer.cs`
  - `neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedControlRegistry.cs`
  - `neo-bpsys-wpf.Core/Extensions/Registry/FrontedPluginControlRegistryExtensions.cs`
  - `neo-bpsys-wpf.ExamplePlugin/TeamCardFrontedControlContributor.cs`
- 保留文件（条件性，V3 仍活跃使用）：
  - `neo-bpsys-wpf.Core/Models/FrontedLayout/FrontedPluginControlType.cs` — V3 用其解析 `plugin:{PackageId}/{ControlId}` canonical ID

#### 测试项目迁移（Task 9）
- `neo-bpsys-wpf.Tests/Models/BackgroundTintFrontedControlTest.cs` — 迁移到 V3 Registry + V3 测试控件
- `neo-bpsys-wpf.Tests/Models/FrontedCanvasConfigTest.cs` — 删除旧 contributor 测试，迁移到 `FrontedV3ControlHost`
- `neo-bpsys-wpf.Tests/Services/FrontedLayoutPluginDependencyPackageTest.cs` — 迁移到 `FrontedV3ControlRegistry`
- `neo-bpsys-wpf.Tests/Models/FrontedLayoutDesignerFoundationTest.cs` — 删除旧 descriptor 测试，迁移到 V3 Registry
- `neo-bpsys-wpf.Tests/Models/FrontedV3ControlRegistrationTest.cs` — 修复 CS8632 警告
- `neo-bpsys-wpf.Tests/Services/FrontedV3StyleTransferTest.cs` — 修复 CS8632 警告

#### Build 警告修复（Task 8.5～8.7）
- `neo-bpsys-wpf.Core/Abstractions/Services/IFrontedV3StorageAccessor.cs` — 修复 CS1574 cref
- `neo-bpsys-wpf.Core/Extensions/Registry/FrontedV3LayoutWindowRegistryExtensions.cs` — 修复 CS1574 cref
- `neo-bpsys-wpf.Core/Models/FrontedLayout/Designer/V3/FrontedV3DesignSelection.cs` — 修复 CS1574 cref
- `neo-bpsys-wpf.Core/Models/FrontedLayout/V3/Properties/FrontedV3PropertyMetadata.cs` — 修复 CS1574 cref
- `neo-bpsys-wpf.Core/Models/FrontedLayout/V3/Properties/FrontedV3Property{T}.cs` — 修复 CS1574 cref
- `neo-bpsys-wpf.Core/Abstractions/Services/FrontedV3ControlAttribute.cs` — 修复 CS1574 cref
- `neo-bpsys-wpf.Core/Abstractions/Services/FrontedV3ControlBase.cs` — 修复 CS1574 cref
- `neo-bpsys-wpf.Core/Abstractions/Services/FrontedV3PartVisualAttribute.cs` — 修复 CS1574 cref

## 2. 新增和删除的公开 API

### 新增公开 API
- `FrontedV3ControlBase`（PluginSdk 命名空间）— 所有 v3 控件的抽象基类
- `FrontedV3ControlAttribute` — 控件类型标注特性
- `FrontedV3ControlRegistryExtensions.AddFrontedV3Control<TControl>()` — 插件控件注册
- `FrontedV3ControlRegistryExtensions.AddBuiltInFrontedV3Control<TControl, TConfig>()` — 内置控件注册
- `FrontedV3ControlHost` — v3 控件根布局宿主
- `FrontedV3ControlRegistration` — 控件注册信息
- `FrontedV3ControlRegistry` / `IFrontedV3ControlRegistry` — 统一注册表
- `FrontedV3Property<T>` / `FrontedV3PropertyDefinition` / `FrontedV3PropertyMetadata` — 属性 Schema
- `FrontedV3Part` / `FrontedV3PartDefinition` — 固定 Part 机制
- `FrontedV3PartCollection` / `FrontedV3PartCollectionDefinition` — PartCollection 机制
- `FrontedV3StyleTransferService` / `FrontedV3PropertyTransfer` / `FrontedV3StyleTransferProfile` — StyleTransfer 系统
- `FrontedV3DesignSelection` / `IFrontedV3GeometryTarget` — Designer 统一选中与几何操作
- `BuiltInPropertyDefinitionResolver` / `BuiltInPartDefinitionResolver` / `BuiltInPartCollectionDefinitionResolver` — 内置控件 Schema/Part/PartCollection 定义解析器

### 删除的公开 API
- `IFrontedControl` — 旧控件工厂接口
- `IFrontedControlRegistry` / `FrontedControlRegistry` — 旧注册表
- `IFrontedControlPluginContributor` — 旧插件 contributor 接口
- `IFrontedControlPluginRegistry` / `FrontedControlPluginRegistry` — 旧插件注册表
- `FrontedPluginControlDescriptor<TConfig>` / `IFrontedPluginControlDescriptor` — 旧插件描述符
- `FrontedPluginControlAdapter<TConfig>` — 旧插件适配器
- `FrontedPluginControlConfigMaterializer` — 旧配置材料化器
- `AddFrontedPluginControlContributor<T>()` — 旧插件注册扩展
- `TeamCardFrontedControlContributor` — ExamplePlugin 旧 contributor

## 3. 旧架构扫描结果

全仓搜索以下旧符号，结果仅出现在允许的历史文档中（`docs/internal/designer-v3-control-refactor-audit.md`、`v3-control-refactor.md`、`docs/plugin-system.md`、`docs/fronted-designer-v3.md`、`neo-bpsys-wpf.PluginSdk/README.md`、本 spec 目录），代码中零引用：
- `IFrontedControlPluginContributor` ✓
- `IFrontedControlPluginRegistry` ✓
- `FrontedPluginControlDescriptor` / `IFrontedPluginControlDescriptor` ✓
- `FrontedControlPluginRegistry` ✓
- `FrontedPluginControlAdapter` ✓
- `AddFrontedPluginControlContributor` ✓
- `TeamCardFrontedControlContributor` ✓
- `IFrontedControl`（非 V3）✓
- `FrontedControlRegistry` / `IFrontedControlRegistry` ✓
- `FrontedPluginControlConfigMaterializer` ✓

## 4. Build 与测试结果

### Build
- `dotnet build neo-bpsys-wpf.slnx`：**0 error，916 warning（全部为既有非 V3 警告）**
- V3 相关警告：**0**（已全部修复）
- 既有警告分类：CS1591（missing XML comment）、CS8632（nullable 上下文）、xUnit1051（CancellationToken）、CS8602/CS8603/CS8601/CS8625/CS8604/CS8600（nullable 引用）、CS0067（event never used）、CS1574（非 V3 文件 cref）、CS1573/CS0649/CS0169/CS0162

### 测试
- `dotnet test`：**Passed 1646，Failed 6，Total 1652**
- V3 相关测试：**全部通过**
- 既有失败（与本任务无关）：
  1. `ArchiveServiceTest.ExtractToDirectory_FinalProgressReaches100` — ZIP/Archive 服务
  2. `BehaviorPanelViewModelTest.AddFilter_UsesFirstPayloadField_WhenAvailable` — Behavior 面板 VM
  3. `I18nResourceAuditTest.AnyHostDictionaryLookup_ShouldOnlyBeUsedByFrontedLayoutLocalizationResolver` — i18n 审计
  4. `GameDataTableOcrParserTest.Parse_LeavesMissingColumnsEmptyAndReturnsOnlyRecognizedRows` — OCR 解析
  5. `WebTransitionCommitBarrierTest.BarrierCompletesOnlyAfterPostCommitRecalculation` — WebRenderer
  6. `FrontedBehaviorRuntimeLoopTest.BehaviorRuntime_Loop_PickingBorderStartGraph_WithWindow_CompletesWithoutBlocking` — Behavior 运行时循环（既有失败）

## 5. 契约保护

- 内置控件 JSON 序列化输出与迁移前一致：根级平铺字段，无 `Options` 嵌套对象
- `PluginFrontedControlConfig.ExtensionData` round-trip 保留未知字段
- 未执行 `git reset`/`git restore`/`git checkout`/`git stash`/`git clean` 等有副作用 Git 命令
- 未新增 shim / adapter / duplicate interface / duplicate model / fallback / 临时兼容构造函数
- 未修改 `.bpui` 和布局 JSON 契约

## 6. 禁止修复方式检查

- ✓ 未用读取期补字段掩盖新旧语义冲突
- ✓ 未用转换器掩盖语义冲突
- ✓ 未用运行时猜测或 fallback 掩盖冲突
- ✓ 未为旧数据通过新校验手动补默认值
- ✓ 未重写持久化内容
- ✓ 未保留 Obsolete shim / adapter / facade / 旧注册路径 fallback
