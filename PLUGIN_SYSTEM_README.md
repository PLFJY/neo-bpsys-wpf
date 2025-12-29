# 插件系统 (Plugin System)

## 概述

neo-bpsys-wpf 现在支持完整的插件系统！您可以通过插件扩展应用程序的功能，添加自定义页面、控件和服务。

## 主要特性

- ✅ **动态加载/卸载插件** - 无需重启应用即可加载和卸载插件
- ✅ **UI组件扩展** - 添加自定义页面和控件到主应用程序
- ✅ **依赖注入集成** - 完全支持 .NET 依赖注入
- ✅ **插件隔离** - 使用 AssemblyLoadContext 实现插件隔离
- ✅ **事件通信** - 内置事件总线用于插件间通信
- ✅ **生命周期管理** - 完整的插件生命周期管理（初始化、启动、停止、卸载）
- ✅ **依赖管理** - 支持插件间依赖关系

## 快速开始

### 使用插件

1. 启动应用程序
2. 导航到"插件管理"页面
3. 点击"刷新插件列表"扫描可用插件
4. 选择要使用的插件，点击"加载"
5. 加载完成后，点击"启动"激活插件

### 开发插件

详细的插件开发指南请参考：[插件开发指南](PLUGIN_DEVELOPMENT_GUIDE.md)

#### 快速示例

1. 创建类库项目，引用 `neo-bpsys-wpf.Core`
2. 实现插件类：

```csharp
using neo_bpsys_wpf.Core.Abstractions.Plugins;

public class MyPlugin : UIPluginBase
{
    public override string Id => "com.example.myplugin";
    public override string Name => "我的插件";
    public override string Description => "插件描述";
    public override Version Version => new Version(1, 0, 0);
    public override string Author => "作者";
    
    protected override IEnumerable<PluginPageDescriptor> OnGetPages()
    {
        return new[]
        {
            new PluginPageDescriptor
            {
                PageType = typeof(MyPage),
                Title = "我的页面",
                Route = "mypage"
            }
        };
    }
}
```

3. 创建 `plugin.json` 清单文件
4. 编译并部署到插件目录

## 插件目录结构

```
%AppData%/neo-bpsys-wpf/Plugins/
    ├── MyPlugin/
    │   ├── plugin.json
    │   ├── MyPlugin.dll
    │   └── (依赖DLL)
    └── AnotherPlugin/
        ├── plugin.json
        └── AnotherPlugin.dll
```

## 示例插件

项目包含一个完整的示例插件 `SamplePlugin`，演示了：

- 基本插件结构
- UI页面添加
- 自定义控件创建
- 服务注入
- 插件配置

## 架构

插件系统采用 .NET 标准设计模式：

```
┌─────────────────────────────────────┐
│      主应用程序 (Host)              │
│  ┌──────────────────────────────┐  │
│  │   PluginService              │  │
│  │   - 插件发现                 │  │
│  │   - 生命周期管理             │  │
│  │   - 依赖注入集成             │  │
│  └──────────────────────────────┘  │
│           ↓                         │
│  ┌──────────────────────────────┐  │
│  │   AssemblyLoadContext        │  │
│  │   (插件隔离)                 │  │
│  └──────────────────────────────┘  │
└─────────────────────────────────────┘
            ↓
┌─────────────────────────────────────┐
│         插件 (Plugins)              │
│  ┌──────────────────────────────┐  │
│  │   IPlugin / IUIPlugin        │  │
│  │   - 实现插件接口             │  │
│  │   - 注册UI组件               │  │
│  │   - 配置服务                 │  │
│  └──────────────────────────────┘  │
└─────────────────────────────────────┘
```

## 核心组件

### 接口和抽象类

- `IPlugin` - 基础插件接口
- `IUIPlugin` - UI插件接口
- `IPluginContext` - 插件上下文，提供宿主服务访问
- `IPluginService` - 插件服务，管理插件生命周期
- `PluginBase` - 插件基类
- `UIPluginBase` - UI插件基类

### 描述符

- `PluginPageDescriptor` - 页面描述符
- `PluginControlDescriptor` - 控件描述符
- `PluginMetadata` - 插件元数据

### 服务

- `PluginService` - 插件服务实现
- `PluginContext` - 插件上下文实现
- `PluginAssemblyLoadContext` - 程序集加载上下文

## 最佳实践

1. **使用语义化版本** - 遵循 SemVer 规范
2. **最小化依赖** - 尽量使用宿主提供的服务
3. **异步操作** - 使用异步方法处理耗时任务
4. **错误处理** - 妥善处理异常，避免影响主应用
5. **资源清理** - 在 DisposeAsync 中正确清理资源
6. **文档完善** - 为插件编写清晰的文档

## 安全考虑

- 插件运行在应用程序进程中，具有相同的权限
- 仅加载受信任的插件
- 定期审查已安装的插件
- 插件隔离提供基本的程序集隔离，但不是安全边界

## 性能建议

- 避免在初始化时执行耗时操作
- 使用延迟加载技术
- 合理使用缓存
- 监控插件资源使用

## 故障排除

### 插件加载失败

1. 检查日志文件：`%AppData%/neo-bpsys-wpf/Logs/`
2. 验证 `plugin.json` 格式
3. 确认依赖项完整
4. 检查 .NET 版本兼容性

### UI组件不显示

1. 确认插件状态为"运行中"
2. 检查页面描述符配置
3. 查看导航设置

## 贡献

欢迎提交插件或改进插件系统！

## 支持

- 提交 Issue 报告问题
- 查看[插件开发指南](PLUGIN_DEVELOPMENT_GUIDE.md)
- 参考示例插件 `SamplePlugin`

## 许可证

遵循主项目许可证。
