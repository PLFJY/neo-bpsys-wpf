# neo-bpsys-wpf Plugin SDK

本项目用于开发 `neo-bpsys-wpf` 插件。v3 起 PluginSdk 不再作为 NuGet 包发布；插件作者应 clone `neo-bpsys-wpf` 仓库，并在插件项目中通过 `ProjectReference` 引用本项目源码。3.0 版本起，插件 API 版本为 `3.0.0.0`，前台窗口注册使用强类型 registration 模型；旧 contributor/descriptor 注入 API 已移除，旧插件需要迁移到新 API。

完整参考示例请查看 `neo-bpsys-wpf.ExamplePlugin` 项目（插件 ID `plfjy.ExamplePlugin`），它是一个综合示例，演示了所有当前插件能力。

## manifest.yml

```yaml
id: plfjy.ExamplePlugin
name: ExamplePlugin
description: Example plugin.
entranceAssembly: neo-bpsys-wpf.ExamplePlugin.dll
url: https://github.com/PLFJY/neo-bpsys-wpf
version: 1.0.0.0
apiVersion: 3.0.0.0
author: Zero PLFJY
icon: icon.png
```

`apiVersion` 是宿主插件 API 兼容性版本，不等同于 PluginSdk 源码所在仓库提交。

## SDK 引用方式

插件项目应包含 SDK 项目引用，并显式导入打包 target：

```xml
<ItemGroup>
  <ProjectReference Include="..\neo-bpsys-wpf.PluginSdk\neo-bpsys-wpf.PluginSdk.csproj" Private="false" />
</ItemGroup>

<Import Project="..\neo-bpsys-wpf.PluginSdk\neo-bpsys-wpf.PluginSdk.targets" />
```

`ProjectReference` 提供编译期 API，`Import` 提供 `CreateZip` target。独立插件仓库可以把 `neo-bpsys-wpf` 作为 submodule、sparse checkout 或固定提交的源码目录引入。

## 后台页面

```csharp
services.AddBackendPage<MainPage, MainPageViewModel>();
```

插件安装、更新、启用状态在 Host build 前处理，新增页面、服务、前台窗口通常需要重启后生效。

## 插件服务/配置

```csharp
public class ExamplePlugin : PluginBase
{
    public PluginSettings Settings { get; set; } = new();

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        services.AddSingleton<IExampleService, ExampleService>();

        Settings = ConfigureFileHelper.LoadConfig<PluginSettings>(
            Path.Combine(PluginConfigFolder, "Settings.json"));
    }
}
```

## Designer v3 插件控件

插件前台控件注册到 Designer v3 控件 registry，不再注入到内置窗口的 WPF Canvas。

```csharp
public sealed class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        services.AddFrontedPluginControlContributor<TeamCardFrontedControlContributor>();
    }
}
```

控件类型必须使用稳定命名：

```text
plugin:{PackageId}/{ControlTypeName}
```

示例：`plugin:plfjy.ExamplePlugin/TeamCard`

控件配置继承 `FrontedControlConfigBase`，构造函数写入完整 `ControlType`。插件 descriptor 提供控件创建函数、默认配置和 PropertyGrid 元数据。`.bpui` 会保存控件 JSON 和插件依赖，不会包含插件 DLL。

需要让控件属性使用 Binding Browser 时，在 `FrontedPluginPropertyDescriptor.BindingTargetKind` 中声明期望类型。宿主 Binding Browser 由显式 root 和绑定 attribute 反射 catalog 驱动，不会扫描任意插件服务或调用运行时 getter。插件如果需要暴露自己的语义绑定源，应通过宿主提供的 binding root/contributor 扩展点注册稳定 root、虚拟节点或 semantic key；普通 DTO 属性应使用 `[FrontedBindingObject]`、`[FrontedBindable]`、`[FrontedBindingIgnore]` 和 `[FrontedBindingCollection]` 这类契约描述，而不是要求宿主手写每个属性节点。

## 插件前台窗口

v3 提供两类前台窗口，均通过强类型 registration 模型注册。窗口类型不在 `manifest.yml` 中指定。

### XAML Window

插件提供自己的 WPF `Window` 类型，宿主只负责创建、注册、显示和隐藏。它会出现在 FrontManage，不默认进入 Designer。

窗口类必须使用 `[FrontedWindowInfo("GUID", "DisplayName")]` 特性标注，并继承 `FrontedWindowBase`。`IsBuiltIn` 是 attribute named argument，默认 `false`；内置窗口设 `IsBuiltIn = true`。

```csharp
[FrontedWindowInfo("3363BFE1-1393-4765-B926-001B6848FAF7", "Example XAML Window")]
public partial class ExampleXamlWindow : FrontedWindowBase
{
    public ExampleXamlWindow() => InitializeComponent();
}
```

注册：

```csharp
services.AddFrontedWindow<ExampleXamlWindow, ExampleXamlWindowViewModel>();
```

`AddFrontedWindow<TWindow, TViewModel>` 会读取 Attribute 上的 GUID 作为 Canonical ID，注册 ViewModel 和 Window，并在创建时设置 DataContext。`FrontedWindowInfo` 旧的 canvas 构造函数仍保留但参数会被忽略，Canvas 注入能力不会恢复。

### v3 Layout Window

插件声明一个 layout window，宿主使用标准 v3 layout host 渲染。它可出现在 FrontManage，进入 Designer 编辑。

```csharp
services.AddFrontedV3LayoutWindow("ExampleLayoutOverlay");
```

`AddFrontedV3LayoutWindow(string windowId, bool isBuiltIn = false)` 只接受局部窗口标识 `windowId` 和 `isBuiltIn` 两个参数。`windowId` 只需插件内唯一；`isBuiltIn` 默认 `false`，内置窗口显式传 `true`。PackageId 不是 API 参数，由宿主在插件初始化作用域内通过 `FrontedPluginRegistrationContext` 自动注入。

局部 ID 验证规则：不允许包含 `/`、`\`、`:`、`.`、`..` 或非法文件名字符；不允许直接传入完整 `plugin:package/window` 形式。同名局部 ID 可以存在于不同插件（`plugin:a/Overlay` 与 `plugin:b/Overlay` 共存）。

### v3 Layout 加载

v3 Layout Window 不要求默认 JSON 存在。无默认 JSON 时使用 `FrontedV3LayoutWindowConfigFactory` 生成的内存空模板，可正常渲染并打开 Designer；Designer 首次保存时才创建 JSON 文件。

加载优先级：

```text
内置: 激活 package → 内置资源 → 空模板
插件: 激活 package → 空模板
```

宿主不从插件安装目录加载默认 v3 Layout。

## 标识模型

前台窗口使用 Canonical ID 作为运行时、Designer 和 `.bpui` 的统一身份：

```text
内置 v3 窗口: 直接使用 local ID（例如 BpWindow）
插件 v3 窗口: plugin:{PackageId}/{LocalWindowId}（例如 plugin:plfjy.ExamplePlugin/ExampleLayoutOverlay）
XAML 窗口:   使用 Attribute GUID（例如 3363BFE1-1393-4765-B926-001B6848FAF7）
```

`.bpui` 契约保持不变：`FormatVersion=3`、`LayoutSchemaVersion=3`、所有 JSON 字段名不变，`Content.Layouts[].Window` 使用 Canonical ID。导入再导出不会重写 Canonical ID。

用户 layout 安全路径示例：

```text
FrontedLayouts/BpWindow.json
FrontedLayouts/plugin/plfjy.ExamplePlugin/ExampleLayoutOverlay.json
```

同一 Canonical ID 冲突时启动失败，不会静默跳过。

## 缺失插件行为

导入 `.bpui` 时，如果插件窗口 registration 缺失，导入仍成功，layout JSON、资源和依赖元数据会保留；运行时不创建该窗口，FrontManage 和 Designer 不显示它。插件安装并重启后，窗口会重新可用并使用已保留布局。

如果 layout 中存在缺失插件控件，导入仍成功。Designer 显示 Missing Plugin placeholder，可选择、移动、缩放和手动删除；运行时默认跳过该控件并记录 warning；导出会继续保留控件 JSON、`ExtensionData` 和依赖元数据。

插件市场安装/更新引导仍可用，但永不静默安装；导入不依赖安装完成。安装或更新插件后通常需要重启。

## 迁移说明

旧的前台窗口 contributor/descriptor 架构（包括 contributor 接口、window descriptor、window kind 枚举和 contributor 注册扩展）已整体移除，不提供 Obsolete shim，也不保留 adapter。

迁移到新 API：

1. **Plugin XAML Window**：将原 contributor 中的窗口类型、ViewModel 类型、显示名和 GUID 整理到窗口类上的 `[FrontedWindowInfo("GUID", "DisplayName")]` 特性，并让窗口类继承 `FrontedWindowBase`，然后用 `services.AddFrontedWindow<TWindow, TViewModel>()` 注册。
2. **Plugin v3 Layout Window**：将原 descriptor 中的窗口短名作为局部 ID 传入 `services.AddFrontedV3LayoutWindow("WindowId")`。原 descriptor 上的默认布局根、空白布局开关、插件目录等字段不再支持；如需默认布局，改由用户首次在 Designer 中保存生成。
3. **Designer v3 插件控件**：沿用 `AddFrontedPluginControlContributor<T>()`，无变化。

旧的“把 WPF 控件直接塞进现有前台窗口”能力已移除，不提供兼容路径。需要可编辑 overlay 元素时使用 Designer v3 插件控件；需要插件完全控制 XAML/行为时使用 XAML Window；需要宿主托管的可编辑 layout 窗口时使用 v3 Layout Window。

参照 `neo-bpsys-wpf.ExamplePlugin` 项目获取完整迁移示例。
