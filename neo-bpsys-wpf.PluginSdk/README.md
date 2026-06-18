# neo-bpsys-wpf Plugin SDK

本项目用于开发 `neo-bpsys-wpf` 插件。v3 起 PluginSdk 不再作为 NuGet 包发布；插件作者应 clone `neo-bpsys-wpf` 仓库，并在插件项目中通过 `ProjectReference` 引用本项目源码。3.0 版本起，插件 API 版本为 `3.0.0.0`，旧前台注入 API 已移除；旧插件需要迁移到 v3 descriptor / Designer v3 布局模型。

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

v3 提供两类插件前台窗口，均通过 `IFrontedWindowPluginContributor` 暴露 descriptor。

### Plugin XAML Window

插件提供自己的 WPF `Window` 类型，宿主只负责创建、注册、显示和隐藏。它会出现在 FrontManage，不默认进入 Designer。

```csharp
public sealed class ExampleFrontedWindowContributor : IFrontedWindowPluginContributor
{
    public IEnumerable<FrontedPluginWindowDescriptor> GetFrontedWindows()
    {
        yield return new FrontedPluginWindowDescriptor
        {
            PackageId = "plfjy.ExamplePlugin",
            WindowId = "3363BFE1-1393-4765-B926-001B6848FAF7",
            WindowTypeName = "ExampleXamlWindow",
            DisplayName = "Example XAML Window",
            Kind = FrontedWindowKind.PluginXaml,
            WindowType = typeof(ExampleXamlWindow),
            ViewModelType = typeof(ExampleXamlWindowViewModel)
        };
    }
}
```

### Plugin v3 Layout Window

插件声明一个 layout window，宿主使用标准 v3 layout host 渲染。它可出现在 FrontManage，进入 Designer 编辑。

```csharp
yield return new FrontedPluginWindowDescriptor
{
    PackageId = "plfjy.ExamplePlugin",
    WindowId = "B11F63A4-1765-4870-9E36-0AE654026421",
    WindowTypeName = "ExampleLayoutOverlay",
    DisplayName = "Example Layout Overlay",
    Kind = FrontedWindowKind.PluginLayout,
};
```

默认布局文件放在插件安装目录：

```text
FrontedLayouts/{WindowTypeName}.json
```

## 标识模型

`WindowId` 是运行时窗口身份，必须是稳定 GUID 字符串。`WindowTypeName` 是插件内短语义名称。`FullWindowType` 是布局和 `.bpui` 身份：

```text
内置: BpWindow
插件: plugin:{PackageId}/{WindowTypeName}
```

`FrontedWindowType` enum 只映射内置窗口；插件窗口不扩展该 enum。Designer 和 `.bpui` 应使用 `FullWindowType`。

用户 layout 安全路径示例：

```text
FrontedLayouts/BpWindow/BaseCanvas.json
FrontedLayouts/plugin/plfjy.ExamplePlugin/ExampleLayoutOverlay/BaseCanvas.json
```

## 缺失插件行为

导入 `.bpui` 时，如果插件窗口 descriptor 缺失，导入仍成功，layout JSON、资源和依赖元数据会保留；运行时不创建该窗口，FrontManage 和 Designer 不显示它。插件安装并重启后，窗口会重新可用并使用已保留布局。

如果 layout 中存在缺失插件控件，导入仍成功。Designer 显示 Missing Plugin placeholder，可选择、移动、缩放和手动删除；运行时默认跳过该控件并记录 warning；导出会继续保留控件 JSON、`ExtensionData` 和依赖元数据。

插件市场安装/更新引导仍可用，但永不静默安装；导入不依赖安装完成。安装或更新插件后通常需要重启。

## 迁移说明

旧的"把 WPF 控件直接塞进现有前台窗口"能力已经移除，不提供 Obsolete shim。旧插件应迁移为：

1. Designer v3 插件控件，用于可编辑 overlay 元素。
2. Plugin XAML Window，用于插件完全控制 XAML/行为的前台窗口。
3. Plugin v3 Layout Window，用于宿主托管的可编辑 layout 窗口。

参照 `neo-bpsys-wpf.ExamplePlugin` 项目获取完整迁移示例。
