# 插件系统开发指南 (Plugin System Development Guide)

本文档介绍如何为 neo-bpsys-wpf 开发插件。

## 目录
- [概述](#概述)
- [插件架构](#插件架构)
- [创建插件](#创建插件)
- [插件清单](#插件清单)
- [UI扩展](#ui扩展)
- [服务注入](#服务注入)
- [事件通信](#事件通信)
- [部署插件](#部署插件)

## 概述

neo-bpsys-wpf 插件系统采用 .NET 标准插件架构设计，支持：

- ✅ 动态加载和卸载插件
- ✅ 插件隔离（使用 AssemblyLoadContext）
- ✅ UI组件扩展（自定义页面和控件）
- ✅ 依赖注入集成
- ✅ 事件总线通信
- ✅ 插件生命周期管理
- ✅ 插件依赖管理

## 插件架构

### 核心接口

#### IPlugin
所有插件必须实现的基础接口：

```csharp
public interface IPlugin
{
    string Id { get; }           // 唯一标识符
    string Name { get; }         // 插件名称
    string Description { get; }  // 插件描述
    Version Version { get; }     // 插件版本
    string Author { get; }       // 插件作者

    Task InitializeAsync(IPluginContext context);
    Task StartAsync();
    Task StopAsync();
    Task DisposeAsync();
}
```

#### IUIPlugin
提供UI扩展的插件接口：

```csharp
public interface IUIPlugin : IPlugin
{
    void ConfigureServices(IServiceCollection services);
    IEnumerable<PluginPageDescriptor> GetPages();
    IEnumerable<PluginControlDescriptor> GetControls();
}
```

#### IPluginContext
插件上下文，提供访问宿主应用功能的能力：

```csharp
public interface IPluginContext
{
    IServiceProvider Services { get; }
    ILoggerFactory LoggerFactory { get; }
    string PluginDirectory { get; }
    string AppDataDirectory { get; }

    void PublishEvent<TEvent>(TEvent eventData);
    void SubscribeEvent<TEvent>(Action<TEvent> handler);
}
```

### 插件状态

插件在其生命周期中会经历以下状态：

- `NotLoaded` - 未加载
- `Loaded` - 已加载但未启动
- `Initializing` - 初始化中
- `Running` - 运行中
- `Stopped` - 已停止
- `Error` - 错误状态
- `Unloaded` - 已卸载

## 创建插件

### 1. 创建项目

创建一个 .NET 9.0 类库项目：

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net9.0-windows7.0</TargetFramework>
        <UseWpf>true</UseWpf>
    </PropertyGroup>

    <ItemGroup>
        <ProjectReference Include="..\neo-bpsys-wpf.Core\neo-bpsys-wpf.Core.csproj" />
    </ItemGroup>
</Project>
```

### 2. 实现插件类

#### 简单插件示例

```csharp
using neo_bpsys_wpf.Core.Abstractions.Plugins;

public class MyPlugin : PluginBase
{
    public override string Id => "com.mycompany.myplugin";
    public override string Name => "我的插件";
    public override string Description => "插件描述";
    public override Version Version => new Version(1, 0, 0);
    public override string Author => "作者名称";

    protected override Task OnInitializeAsync(IPluginContext context)
    {
        var logger = context.LoggerFactory.CreateLogger<MyPlugin>();
        logger.LogInformation("插件初始化");
        return Task.CompletedTask;
    }

    protected override Task OnStartAsync()
    {
        // 插件启动逻辑
        return Task.CompletedTask;
    }

    protected override Task OnStopAsync()
    {
        // 插件停止逻辑
        return Task.CompletedTask;
    }
}
```

#### UI插件示例

```csharp
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Abstractions.Plugins;

public class MyUIPlugin : UIPluginBase
{
    public override string Id => "com.mycompany.myuiplugin";
    public override string Name => "我的UI插件";
    public override string Description => "提供自定义UI的插件";
    public override Version Version => new Version(1, 0, 0);
    public override string Author => "作者名称";

    protected override void OnConfigureServices(IServiceCollection services)
    {
        // 注册插件的服务
        services.AddSingleton<MyService>();
        services.AddSingleton<MyPageViewModel>();
    }

    protected override IEnumerable<PluginPageDescriptor> OnGetPages()
    {
        return new[]
        {
            new PluginPageDescriptor
            {
                PageType = typeof(MyPage),
                ViewModelType = typeof(MyPageViewModel),
                Title = "我的页面",
                Icon = "AppGeneric",
                Route = "mypage",
                ShowInNavigation = true,
                Priority = 100
            }
        };
    }

    protected override IEnumerable<PluginControlDescriptor> OnGetControls()
    {
        return new[]
        {
            new PluginControlDescriptor
            {
                ControlType = typeof(MyControl),
                Name = "我的控件",
                Description = "自定义控件",
                Category = "自定义"
            }
        };
    }
}
```

### 3. 创建UI页面和控件

#### 创建页面

```csharp
using System.Windows.Controls;

public class MyPage : Page
{
    public MyPage()
    {
        // XAML 或代码创建UI
        var stackPanel = new StackPanel();
        stackPanel.Children.Add(new TextBlock { Text = "我的插件页面" });
        Content = stackPanel;
    }
}

public class MyPageViewModel
{
    public string Title { get; } = "我的页面标题";
    // 其他属性和命令
}
```

#### 创建自定义控件

```csharp
using System.Windows.Controls;

public class MyControl : Control
{
    static MyControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(MyControl),
            new FrameworkPropertyMetadata(typeof(MyControl)));
    }

    // 依赖属性
    public static readonly DependencyProperty MyPropertyProperty =
        DependencyProperty.Register(nameof(MyProperty), typeof(string),
            typeof(MyControl), new PropertyMetadata(string.Empty));

    public string MyProperty
    {
        get => (string)GetValue(MyPropertyProperty);
        set => SetValue(MyPropertyProperty, value);
    }
}
```

## 插件清单

每个插件目录必须包含一个 `plugin.json` 清单文件：

```json
{
  "Id": "com.mycompany.myplugin",
  "Name": "我的插件",
  "Description": "插件描述",
  "Version": "1.0.0",
  "Author": "作者名称",
  "AssemblyFile": "MyPlugin.dll",
  "TypeFullName": "MyNamespace.MyPlugin",
  "Dependencies": ["com.other.plugin"],
  "MinAppVersion": "1.0.0",
  "Tags": ["tag1", "tag2"]
}
```

### 清单字段说明

- `Id`: 插件唯一标识符（必需，建议使用反向域名格式）
- `Name`: 插件显示名称（必需）
- `Description`: 插件描述（可选）
- `Version`: 插件版本（必需，格式: major.minor.patch）
- `Author`: 插件作者（可选）
- `AssemblyFile`: 插件程序集文件名（必需）
- `TypeFullName`: 插件类的完全限定名称（必需）
- `Dependencies`: 依赖的其他插件ID列表（可选）
- `MinAppVersion`: 最低应用版本要求（可选）
- `Tags`: 插件标签（可选）

## UI扩展

### 页面扩展

插件可以向应用程序添加新页面：

```csharp
protected override IEnumerable<PluginPageDescriptor> OnGetPages()
{
    return new[]
    {
        new PluginPageDescriptor
        {
            PageType = typeof(MyPage),         // 页面类型
            ViewModelType = typeof(MyViewModel), // 视图模型类型（可选）
            Title = "我的页面",                  // 显示标题
            Icon = "AppGeneric",                // 图标（WPF-UI SymbolRegular）
            Route = "mypage",                   // 路由路径
            ShowInNavigation = true,            // 是否在导航菜单显示
            Priority = 100                      // 排序优先级（数字越小越靠前）
        }
    };
}
```

### 控件扩展

插件可以提供自定义控件：

```csharp
protected override IEnumerable<PluginControlDescriptor> OnGetControls()
{
    return new[]
    {
        new PluginControlDescriptor
        {
            ControlType = typeof(MyControl),  // 控件类型
            Name = "我的控件",                 // 控件名称
            Description = "控件描述",          // 控件描述
            Category = "自定义"                // 控件分类
        }
    };
}
```

## 服务注入

插件可以注册自己的服务到DI容器：

```csharp
protected override void OnConfigureServices(IServiceCollection services)
{
    // 单例服务
    services.AddSingleton<IMyService, MyService>();
    
    // 作用域服务
    services.AddScoped<IMyRepository, MyRepository>();
    
    // 瞬态服务
    services.AddTransient<IMyHelper, MyHelper>();
    
    // 注册视图模型
    services.AddSingleton<MyViewModel>();
}
```

### 使用宿主服务

插件可以通过上下文访问宿主提供的服务：

```csharp
protected override Task OnInitializeAsync(IPluginContext context)
{
    // 获取日志服务
    var logger = context.LoggerFactory.CreateLogger<MyPlugin>();
    
    // 获取其他宿主服务
    var messageBox = context.Services.GetService<IMessageBoxService>();
    
    return Task.CompletedTask;
}
```

## 事件通信

插件系统提供事件总线用于插件间和插件与宿主间的通信。

### 发布事件

```csharp
// 定义事件
public class MyCustomEvent
{
    public string Message { get; set; }
    public DateTime Timestamp { get; set; }
}

// 发布事件
Context?.PublishEvent(new MyCustomEvent
{
    Message = "Hello from plugin!",
    Timestamp = DateTime.Now
});
```

### 订阅事件

```csharp
protected override Task OnInitializeAsync(IPluginContext context)
{
    // 订阅事件
    context.SubscribeEvent<MyCustomEvent>(OnMyCustomEvent);
    
    return Task.CompletedTask;
}

private void OnMyCustomEvent(MyCustomEvent evt)
{
    // 处理事件
    Console.WriteLine($"Received event: {evt.Message}");
}
```

## 部署插件

### 1. 构建插件

```bash
dotnet build -c Release
```

### 2. 准备插件目录

在应用程序的插件目录下创建插件文件夹：

```
%AppData%/neo-bpsys-wpf/Plugins/MyPlugin/
    ├── MyPlugin.dll
    ├── plugin.json
    └── (其他依赖DLL)
```

### 3. 创建插件清单

确保 `plugin.json` 正确配置。

### 4. 加载插件

启动应用程序后：
1. 打开"插件管理"页面
2. 点击"刷新插件列表"
3. 找到你的插件并点击"加载"
4. 点击"启动"以启动插件

## 最佳实践

### 1. 错误处理

```csharp
protected override async Task OnStartAsync()
{
    try
    {
        // 启动逻辑
    }
    catch (Exception ex)
    {
        var logger = Context?.LoggerFactory.CreateLogger<MyPlugin>();
        logger?.LogError(ex, "Failed to start plugin");
        throw;
    }
}
```

### 2. 资源清理

```csharp
protected override Task OnDisposeAsync()
{
    // 清理资源
    _subscription?.Dispose();
    _timer?.Dispose();
    
    return Task.CompletedTask;
}
```

### 3. 版本管理

使用语义化版本（Semantic Versioning）：
- MAJOR：不兼容的API更改
- MINOR：向后兼容的功能添加
- PATCH：向后兼容的错误修复

### 4. 依赖管理

- 尽量减少外部依赖
- 使用宿主提供的共享库
- 明确声明依赖版本

### 5. 性能考虑

- 避免在初始化时执行耗时操作
- 使用异步方法处理长时间运行的任务
- 合理使用缓存

## 示例插件

完整的示例插件源代码请参考 `SamplePlugin` 项目。

## 故障排除

### 插件无法加载

1. 检查 `plugin.json` 格式是否正确
2. 确认 `AssemblyFile` 和 `TypeFullName` 是否正确
3. 查看应用日志获取详细错误信息

### 插件依赖问题

1. 确保所有依赖的DLL都在插件目录中
2. 检查依赖插件是否已加载
3. 验证 .NET 版本兼容性

### UI不显示

1. 确认插件已启动（状态为"运行中"）
2. 检查页面描述符配置
3. 验证 `ShowInNavigation` 设置

## 支持与反馈

如有问题或建议，请提交 Issue 或 Pull Request。

## 许可证

遵循主项目许可证。
