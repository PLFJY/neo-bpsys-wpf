# neo-bpsys-wpf 插件开发指南

## 概述

neo-bpsys-wpf 插件系统是一个符合 .NET 哲学的现代化插件架构，支持：

- 🔌 **热插拔** - 动态加载/卸载插件
- 🎨 **UI 扩展** - 添加自定义页面、设置、前台窗口等
- 📡 **事件系统** - 订阅和发布应用程序事件
- 💾 **配置持久化** - 自动保存和加载插件配置
- 🔐 **隔离加载** - 使用 `AssemblyLoadContext` 实现程序集隔离
- 💉 **依赖注入** - 完全集成 Microsoft.Extensions.DependencyInjection

## 快速开始

### 1. 创建插件项目

创建一个新的类库项目，并引用 `neo-bpsys-wpf.PluginSDK`：

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net9.0-windows7.0</TargetFramework>
        <UseWpf>true</UseWpf>
        <EnableDynamicLoading>true</EnableDynamicLoading>
    </PropertyGroup>
    
    <ItemGroup>
        <PackageReference Include="neo-bpsys-wpf.PluginSDK" Version="1.0.0">
            <Private>false</Private>
            <ExcludeAssets>runtime</ExcludeAssets>
        </PackageReference>
    </ItemGroup>
</Project>
```

### 2. 实现插件主类

```csharp
using neo_bpsys_wpf.PluginSDK.Abstractions;

public class MyPlugin : PluginBase
{
    public override IPluginMetadata Metadata { get; } = new PluginMetadata
    {
        Id = "com.yourcompany.myplugin",
        Name = "我的插件",
        Version = new Version(1, 0, 0),
        Author = "Your Name",
        Description = "插件描述"
    };

    public override async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        await base.InitializeAsync(serviceProvider, cancellationToken);
        // 初始化代码
    }

    public override async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await base.StartAsync(cancellationToken);
        // 启动代码
    }
}
```

### 3. 部署插件

将编译后的 DLL 放到 `%AppData%/neo-bpsys-wpf/Plugins/{pluginId}/` 目录下（推荐用插件 `Metadata.Id` 作为文件夹名）。

## 核心概念

### 插件生命周期

```
NotLoaded → Loaded → Initialized → Running → Stopped
                                      ↓
                                   Disabled
```

1. **NotLoaded**: 插件尚未加载
2. **Loaded**: 插件已加载但未初始化
3. **Initialized**: 插件已初始化，服务已配置
4. **Running**: 插件正在运行
5. **Stopped**: 插件已停止
6. **Disabled**: 插件被禁用

### 插件上下文 (IPluginContext)

插件上下文提供了访问宿主应用程序功能的入口：

```csharp
public interface IPluginContext
{
    IPluginMetadata Metadata { get; }           // 插件元数据
    IHostApplicationService HostApplication { get; }  // 宿主服务
    IPluginManager PluginManager { get; }       // 插件管理器
    IPluginEventBus EventBus { get; }           // 事件总线
    IUIExtensionService UIExtensions { get; }   // UI扩展服务
    IPluginConfigurationService Configuration { get; } // 配置服务
    IPluginResourceService Resources { get; }   // 资源服务
    IPluginLogger Logger { get; }               // 日志服务
}
```

## UI 扩展点

### 导航页面扩展

添加新的导航页面到主窗口：

```csharp
public class MyPageExtension : NavigationPageExtensionBase
{
    public override string Id => "my-plugin-page";
    public override string Title => "我的页面";
    public override Type PageType => typeof(MyPage);
}

// 在插件初始化时注册
context.RegisterUIExtension(new MyPageExtension());
```

### 设置扩展

添加插件设置到设置页面：

```csharp
public class MySettingsExtension : SettingsExtensionBase
{
    public override string Id => "my-plugin-settings";
    public override string Title => "我的设置";
    
    public override FrameworkElement CreateElement()
    {
        // 返回设置UI
    }
    
    public override Task LoadSettingsAsync() { /* ... */ }
    public override Task SaveSettingsAsync() { /* ... */ }
}
```

### 前台窗口扩展

创建自定义前台窗口：

```csharp
public class MyFrontWindowExtension : FrontWindowExtensionBase
{
    public override string Id => "my-front-window";
    public override string Title => "我的窗口";
    public override double Width => 400;
    public override double Height => 300;
    
    public override FrameworkElement CreateWindowContent()
    {
        // 返回窗口内容
    }
}
```

### 扩展点位置

```csharp
public enum ExtensionPointLocation
{
    MainWindowToolbar,    // 主窗口工具栏
    MainWindowStatusBar,  // 主窗口状态栏
    NavigationMenu,       // 导航菜单
    SettingsPage,         // 设置页面
    FrontWindowArea,      // 前台窗口区域
    BpWindowArea,         // BP窗口区域
    ScoreWindowArea,      // 比分窗口区域
    ContextMenu,          // 上下文菜单
    Custom                // 自定义位置
}
```

## 事件系统

### 订阅事件

```csharp
// 订阅主题变更事件
var subscription = context.SubscribeEvent<ThemeChangedEvent>(e =>
{
    Console.WriteLine($"主题已变更为: {e.NewTheme}");
});

// 取消订阅
subscription.Dispose();
```

### 发布事件

```csharp
// 创建自定义事件
public class MyCustomEvent : PluginEventBase
{
    public required string Message { get; init; }
}

// 发布事件
context.PublishEvent(new MyCustomEvent { Message = "Hello" });
```

### 内置事件

| 事件 | 描述 |
|------|------|
| `ApplicationStartedEvent` | 应用程序启动完成 |
| `ApplicationShuttingDownEvent` | 应用程序正在关闭 |
| `ThemeChangedEvent` | 主题已变更 |
| `LanguageChangedEvent` | 语言已变更 |
| `NavigationEvent` | 页面导航 |
| `PluginLoadedEvent` | 插件已加载 |
| `PluginStartedEvent` | 插件已启动 |
| `PluginStoppedEvent` | 插件已停止 |
| `PluginErrorEvent` | 插件错误 |

## 配置管理

```csharp
// 读取配置
var greeting = context.Configuration.GetValue<string>(
    pluginId: "my-plugin",
    key: "greeting",
    defaultValue: "Hello"
);

// 保存配置
context.Configuration.SetValue("my-plugin", "greeting", "你好");
await context.Configuration.SaveAsync();
```

## 服务注册

在 `ConfigureServices` 方法中注册插件服务：

```csharp
public override void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<IMyService, MyService>();
    services.AddTransient<MyViewModel>();
}
```

## 宿主服务

通过 `IHostApplicationService` 访问宿主功能：

```csharp
// 显示通知
context.HostApplication.ShowNotification(
    "标题", 
    "消息内容",
    NotificationType.Success
);

// 显示消息框
var result = await context.HostApplication.ShowMessageBoxAsync(
    "确定要继续吗？",
    "确认",
    MessageBoxButtons.YesNo
);

// 导航到页面
context.HostApplication.Navigate(typeof(SomePage));

// 在UI线程执行
context.HostApplication.InvokeOnUIThread(() =>
{
    // UI操作
});
```

## 最佳实践

1. **唯一ID**: 使用反向域名格式作为插件ID，如 `com.yourcompany.pluginname`
2. **资源管理**: 在 `Dispose` 方法中释放所有资源
3. **异常处理**: 妥善处理异常，避免影响宿主应用
4. **异步操作**: 使用 `async/await` 进行耗时操作
5. **日志记录**: 使用 `IPluginLogger` 记录重要信息
6. **UI线程**: 所有UI操作必须在UI线程执行

## 目录结构

插件目录位置：`%AppData%\neo-bpsys-wpf\`

```
%AppData%\neo-bpsys-wpf\
├── Plugins/                  # 插件程序集目录
│   └── {pluginId}/
│       ├── MyPlugin.dll      # 插件主程序集
│       ├── MyPlugin.deps.json # 依赖信息
│       └── Resources/        # 插件资源目录
│           ├── images/
│           └── locales/
└── PluginData/               # 插件数据和配置目录
    ├── Config/               # 插件配置文件
    └── {PluginId}/           # 插件数据
        └── data.json
```

## 调试技巧

1. 在 Visual Studio 中，将插件项目的调试设置为启动主应用程序
2. 使用条件断点来调试特定场景
3. 利用 `IPluginLogger` 输出调试信息
4. 检查 `Plugins` 目录确保 DLL 正确部署

## 示例代码

完整的示例插件可以参考 `SamplePlugin` 项目。

## API 参考

详细的 API 文档请参考 `neo-bpsys-wpf.PluginSDK` 项目中的 XML 注释。
