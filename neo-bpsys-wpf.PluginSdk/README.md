# neo-bpsys-wpf Plugin SDK

本包用于开发 `neo-bpsys-wpf` 插件。3.0 版本起，插件 API 版本为 `3.0.0.0`，旧前台注入 API 已移除；旧插件需要迁移到 v3 descriptor / Designer v3 布局模型。

## manifest.yml

```yaml
id: top.plfjy.example
name: Example Plugin
description: Example plugin.
entranceAssembly: Example.Plugin.dll
url: https://example.com
version: 1.0.0.0
apiVersion: 3.0.0.0
author: Example
icon: icon.png
```

`apiVersion` 是宿主插件 API 兼容性版本，不等同于本 NuGet 包版本。

## 后台页面

```csharp
services.AddBackendPage<MainPage, MainPageViewModel>();
```

插件安装、更新、启用状态在 Host build 前处理，新增页面、服务、前台窗口通常需要重启后生效。

## Designer v3 插件控件

插件前台控件注册到 Designer v3 控件 registry，不再注入到内置窗口的 WPF Canvas。

```csharp
public sealed class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        services.AddFrontedPluginControlContributor<TeamCardContributor>();
    }
}
```

控件类型必须使用稳定命名：

```text
plugin:{PackageId}/{ControlTypeName}
```

控件配置继承 `FrontedControlConfigBase`，构造函数写入完整 `ControlType`。插件 descriptor 提供控件创建函数、默认配置和 PropertyGrid 元数据。`.bpui` 会保存控件 JSON 和插件依赖，不会包含插件 DLL。

## 插件前台窗口

v3 提供两类插件前台窗口，均通过 `IFrontedWindowPluginContributor` 暴露 descriptor。

### Plugin XAML Window

插件提供自己的 WPF `Window` 类型，宿主只负责创建、注册、显示和隐藏。它会出现在 FrontManage，不默认进入 Designer。

```csharp
public sealed class ExampleWindowContributor : IFrontedWindowPluginContributor
{
    public IEnumerable<FrontedPluginWindowDescriptor> GetFrontedWindows()
    {
        yield return new FrontedPluginWindowDescriptor
        {
            PackageId = "top.plfjy.example",
            WindowId = "3363BFE1-1393-4765-B926-001B6848FAF7",
            WindowTypeName = "ExampleXamlWindow",
            DisplayName = "Example XAML Window",
            Kind = FrontedWindowKind.PluginXaml,
            WindowType = typeof(ExampleWindow),
            ViewModelType = typeof(ExampleWindowViewModel)
        };
    }
}
```

### Plugin v3 Layout Window

插件声明一个 layout window，宿主使用标准 v3 layout host 渲染。它可出现在 FrontManage；有 `Customizable=true` Canvas 时会进入 Designer。

```csharp
yield return new FrontedPluginWindowDescriptor
{
    PackageId = "top.plfjy.example",
    WindowId = "B11F63A4-1765-4870-9E36-0AE654026421",
    WindowTypeName = "ExampleLayoutOverlay",
    DisplayName = "Example Layout Overlay",
    Kind = FrontedWindowKind.PluginLayout,
    Canvases =
    [
        new FrontedCanvasDescriptor
        {
            CanvasName = "BaseCanvas",
            DisplayName = "BaseCanvas",
            Customizable = true,
            DefaultWidth = 1440,
            DefaultHeight = 810
        }
    ]
};
```

默认布局文件放在插件安装目录：

```text
FrontedLayouts/{WindowTypeName}/{CanvasName}.json
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
FrontedLayouts/plugin/top.plfjy.example/ExampleLayoutOverlay/BaseCanvas.json
```

## 缺失插件行为

导入 `.bpui` 时，如果插件窗口 descriptor 缺失，导入仍成功，layout JSON、资源和依赖元数据会保留；运行时不创建该窗口，FrontManage 和 Designer 不显示它。插件安装并重启后，窗口会重新可用并使用已保留布局。

如果 layout 中存在缺失插件控件，导入仍成功。Designer 显示 Missing Plugin placeholder，可选择、移动、缩放和手动删除；运行时默认跳过该控件并记录 warning；导出会继续保留控件 JSON、`ExtensionData` 和依赖元数据。

插件市场安装/更新引导仍可用，但永不静默安装；导入不依赖安装完成。安装或更新插件后通常需要重启。

## 迁移说明

旧的“把 WPF 控件直接塞进现有前台窗口”能力已经移除，不提供 Obsolete shim。旧插件应迁移为：

1. Designer v3 插件控件，用于可编辑 overlay 元素。
2. Plugin XAML Window，用于插件完全控制 XAML/行为的前台窗口。
3. Plugin v3 Layout Window，用于宿主托管的可编辑 layout 窗口。
