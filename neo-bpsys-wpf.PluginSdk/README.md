# neo-bpsys-wpf.PluginSdk

[neo-bpsys-wpf](https://github.com/PLFJY/neo-bpsys-wpf/)的插件 SDK

# neo-bpsys-wpf 插件开发指南 -- AI生成，不保证的正确性，虽然我审过了一遍

欢迎使用 neo-bpsys-wpf 插件系统！本指南将帮助您快速开始开发自己的插件。

## 目录

- [快速开始](#快速开始)
- [插件结构](#插件结构)
- [插件清单文件](#插件清单文件)
- [插件入口类](#插件入口类)
- [插件能力](#插件能力)
  - [注册后台管理页面](#注册后台管理页面)
  - [注册前台展示窗口](#注册前台展示窗口)
  - [注入控件到现有窗口](#注入控件到现有窗口)
  - [注册自定义服务](#注册自定义服务)
  - [配置文件管理](#配置文件管理)
  - [访问共享数据](#访问共享数据)
- [开发环境设置](#开发环境设置)
- [打包与发布](#打包与发布)
- [示例插件](#示例插件)

---

## 快速开始

### 1. 创建新项目

创建一个新的 .NET WPF 类库项目，并引用 `neo-bpsys-wpf.PluginSdk`，接着进入插件项目的 `.csporj` 中在 sdk 包后面加上 `ExcludeAssets="runtime"` :

```xml
<ItemGroup>
  <PackageReference Include="neo-bpsys-wpf.PluginSdk" Version="0.1.5" ExcludeAssets="runtime"/>
</ItemGroup>
```

### 2. 创建插件清单文件

在项目根目录创建 `manifest.yml` 文件：

```yaml
id: your.unique.plugin.id
name: 你的插件名称
description: 插件功能描述
entranceAssembly: "YourPlugin.dll"
url: https://github.com/yourusername/yourplugin
version: 1.0.0.0
apiVersion: 2.0.0.0
author: 你的名字
icon: icon.png
```

### 3. 创建插件入口类

创建一个继承自 `PluginBase` 的类：

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using neo_bpsys_wpf.Core.Abstractions;

namespace YourPlugin;

public class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        // 在这里注册插件的各项功能
    }
}
```

---

## 插件结构

一个标准的插件项目结构如下：

```
YourPlugin/
├── manifest.yml          # 插件清单文件（必需）
├── icon.png             # 插件图标（可选）
├── Plugin.cs            # 插件入口类（必需）
├── Services/            # 自定义服务
├── ViewModels/          # 视图模型
├── Views/               # 视图（页面/窗口）
└── Models/              # 数据模型
```

---

## 插件清单文件

### manifest.yml 字段说明

| 字段 | 类型 | 必需 | 说明 |
|------|------|------|------|
| `id` | string | ✅ | 插件的唯一标识符，建议使用反向域名格式 |
| `name` | string | ✅ | 插件显示名称 |
| `description` | string | ✅ | 插件功能描述 |
| `entranceAssembly` | string | ✅ | 插件入口程序集文件名（含 .dll 后缀） |
| `url` | string | ❌ | 插件项目主页或仓库地址 |
| `version` | string | ✅ | 插件版本号（格式：major.minor.patch.build） |
| `apiVersion` | string | ✅ | 插件 API 版本。需为有效的 `Version` 字符串，且必须 `>= 2.0.0.0`，同时主版本不能高于宿主支持的 API 主版本（当前为 `2.x`） |
| `author` | string | ✅ | 插件作者名称 |
| `icon` | string | ❌ | 插件图标文件名（PNG 格式，推荐尺寸：256x256） |

### 示例

```yaml
id: plfjy.ExamplePlugin
name: ExamplePlugin
description: 示例插件。
entranceAssembly: "neo-bpsys-wpf.ExamplePlugin.dll"
url: https://github.com/PLFJY/neo-bpsys-wpf
version: 1.0.0.0
apiVersion: 2.0.0.0
author: 零风PLFJY
icon: icon.png
```

---

## 插件入口类

插件入口类必须继承 `PluginBase` 并实现 `Initialize` 方法。

### 基础结构

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using neo_bpsys_wpf.Core.Abstractions;

namespace YourPlugin;

public class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        // 插件初始化逻辑
    }
}
```

### 可用属性

- **`PluginConfigFolder`**: `string`  
  插件配置文件目录路径，插件的所有配置文件应保存在此目录中。

- **`Info`**: `PluginInfo`  
  当前插件的元数据信息（包含清单信息、状态等）。

---

## 插件能力

### 注册后台管理页面

后台管理页面显示在主应用的设置界面中，用于插件的配置和管理。

#### 1. 创建页面和 ViewModel

```csharp
// MainPage.xaml.cs
using System.Windows.Controls;
using neo_bpsys_wpf.Core.Attributes;
using Wpf.Ui.Controls;

namespace YourPlugin.Views;

[BackendPageInfo(
    "unique-page-id",           // 唯一 ID
    "后台页面名称",              // 显示名称
    SymbolRegular.Settings24,   // 图标
    BackendPageCategory.External // 分类
)]
public partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
    }
}
```

```csharp
// MainPageViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace YourPlugin.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    // ViewModel 逻辑
}
```

#### 2. 在插件入口注册

```csharp
using neo_bpsys_wpf.Core.Extensions.Registry;

public override void Initialize(HostBuilderContext context, IServiceCollection services)
{
    services.AddBackendPage<MainPage, MainPageViewModel>();
}
```

> [!TIP]
> `BackendPageCategory` 枚举值：
> - `External`: 外部插件（默认）
> - `General`: 常规设置
> - `Advanced`: 高级设置

---

### 注册前台展示窗口

前台展示窗口用于显示比赛数据、OBS 场景等前台内容。

#### 1. 创建窗口和 ViewModel

```csharp
// MainWindow.xaml.cs
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Controls;

namespace YourPlugin.Views;

[FrontedWindowInfo(
    "unique-window-id",         // 窗口唯一 ID
    "前台窗口名称",              // 显示名称
    new[] {                     // 画布定义（可选）
        "BaseCanvas",           // 基础画布（默认）
        "CustomCanvas|自定义画布" // 自定义画布（格式：画布名|显示名）
    }
)]
public partial class MainWindow : FrontedWindowBase
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
```

```xaml
<!-- MainWindow.xaml -->
<controls:FrontedWindowBase
    x:Class="YourPlugin.Views.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:controls="clr-namespace:neo_bpsys_wpf.Core.Controls;assembly=neo-bpsys-wpf.Core"
    Title="MainWindow"
    Width="800"
    Height="450">
    <Canvas Name="BaseCanvas" 
            Width="{Binding RelativeSource={RelativeSource AncestorType=Window}, Path=Width}"
            Height="{Binding RelativeSource={RelativeSource AncestorType=Window}, Path=Height}">
        <!-- 窗口内容 -->
    </Canvas>
</controls:FrontedWindowBase>
```

```csharp
// MainWindowViewModel.cs
using neo_bpsys_wpf.Core.Abstractions;

namespace YourPlugin.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    // ViewModel 逻辑
}
```

#### 2. 在插件入口注册

```csharp
using neo_bpsys_wpf.Core.Extensions.Registry;

public override void Initialize(HostBuilderContext context, IServiceCollection services)
{
    services.AddFrontedWindow<MainWindow, MainWindowViewModel>();
}
```

> [!NOTE]
> - 前台窗口必须继承 `FrontedWindowBase`
> - ViewModel 必须继承 `ViewModelBase`
> - 画布是可选的，默认包含 `BaseCanvas`

---

### 注入控件到现有窗口

插件可以将自定义控件注入到主应用的现有前台窗口中。

#### 1. 创建控件

```xaml
<!-- ExampleInjectedControl.xaml -->
<UserControl
    x:Class="YourPlugin.Views.ExampleInjectedControl"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Name="InjectedControl">
    <Grid>
        <TextBlock Name="MyText" 
                   Text="注入的控件" 
                   FontSize="50" />
    </Grid>
</UserControl>
```

```csharp
// ExampleInjectedControl.xaml.cs
using System.Windows.Controls;

namespace YourPlugin.Views;

public partial class ExampleInjectedControl : UserControl
{
    public ExampleInjectedControl()
    {
        InitializeComponent();
    }
}
```

#### 2. 在插件入口注入

```csharp
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;

public override void Initialize(HostBuilderContext context, IServiceCollection services)
{
    var control = new ExampleInjectedControl();
    
    FrontedWindowHelper.InjectControlToFrontedWindow(
        "unique-control-id",                    // 控件唯一 ID
        control,                                 // 控件实例
        FrontedWindowType.BpWindow,             // 目标窗口类型
        "BaseCanvas",                           // 目标画布名称
        new ElementInfo(379, 100, 522, 312)     // 默认位置和尺寸 (X, Y, Width, Height)
    );
}
```

#### 可用的窗口类型

```csharp
public enum FrontedWindowType
{
    BpWindow,              // BP 窗口
    CutSceneWindow,        // 过场窗口
    ScoreWindow,           // 分数窗口
    ScoreGlobalWindow,     // 全局分数窗口
    ScoreHunWindow,        // 监管者分数窗口
    ScoreSurWindow,        // 求生者分数窗口
    GameDataWindow,        // 比赛数据窗口
    WidgetsWindow          // 小部件窗口
}
```

> [!IMPORTANT]
> 注入的控件可以在主应用的前台窗口管理界面中手动调整位置和大小，设置会自动保存。

---

### 注册自定义服务

插件可以注册自己的服务到依赖注入容器中。

#### 1. 创建服务接口和实现

```csharp
// IExampleService.cs
namespace YourPlugin.Services;

public interface IExampleService
{
    void DoSomething();
}
```

```csharp
// ExampleService.cs
namespace YourPlugin.Services;

public class ExampleService : IExampleService
{
    public void DoSomething()
    {
        // 服务逻辑
    }
}
```

#### 2. 在插件入口注册

```csharp
public override void Initialize(HostBuilderContext context, IServiceCollection services)
{
    services.AddSingleton<IExampleService, ExampleService>();
}
```

#### 3. 在 ViewModel 中使用

```csharp
using Microsoft.Extensions.DependencyInjection;

public partial class MainPageViewModel : ObservableObject
{
    private readonly IExampleService _exampleService;
    
    public MainPageViewModel(IExampleService exampleService)
    {
        _exampleService = exampleService;
    }
}
```

---

### 配置文件管理

插件可以使用 `ConfigureFileHelper` 方便地管理配置文件。

#### 1. 创建配置类

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace YourPlugin.Models;

public partial class PluginSettings : ObservableObject
{
    [ObservableProperty]
    private string _settingValue = "默认值";
    
    [ObservableProperty]
    private int _counter = 0;
}
```

#### 2. 加载和保存配置

```csharp
using System.IO;
using neo_bpsys_wpf.Core.Helpers;

public class Plugin : PluginBase
{
    public PluginSettings Settings { get; set; } = new();
    
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        // 加载配置文件
        Settings = ConfigureFileHelper.LoadConfig<PluginSettings>(
            Path.Combine(PluginConfigFolder, "Settings.json")
        );
        
        // 监听属性变化并自动保存
        Settings.PropertyChanged += (sender, args) =>
        {
            ConfigureFileHelper.SaveConfig(
                Path.Combine(PluginConfigFolder, "Settings.json"), 
                Settings
            );
        };
    }
}
```

> [!NOTE]
> - 配置文件会自动保存为 JSON 格式
> - 推荐将配置文件保存在 `PluginConfigFolder` 目录中
> - 使用 `ObservableObject` 可以自动触发属性变化通知

---

### 访问共享数据

插件可以通过 `ISharedDataService` 访问和修改主应用的各种数据实例

#### 获取服务实例

```csharp
using neo_bpsys_wpf.Core.Abstractions.Services;

public partial class MainPageViewModel : ObservableObject
{
    private readonly ISharedDataService _sharedDataService;
    
    public MainPageViewModel(ISharedDataService sharedDataService)
    {
        _sharedDataService = sharedDataService;
    }
}
```

#### 可用的数据和方法

##### 队伍数据

```csharp
// 主队
Team mainTeam = _sharedDataService.MainTeam;
_sharedDataService.MainTeam = newMainTeam;

// 客队
Team awayTeam = _sharedDataService.AwayTeam;
_sharedDataService.AwayTeam = newAwayTeam;
```

##### 对局数据

```csharp
// 当前对局
Game currentGame = _sharedDataService.CurrentGame;
// 新建对局
_sharedDataService.NewGame();
```

##### 角色字典

```csharp
// 求生者角色字典
SortedDictionary<string, Character> surCharaDict = _sharedDataService.SurCharaDict;

// 监管者角色字典
SortedDictionary<string, Character> hunCharaDict = _sharedDataService.HunCharaDict;
```

##### Ban 位管理

```csharp
// 设置 Ban 位数量
_sharedDataService.SetBanCount(BanListName.CurrentSurBanned, 3);
_sharedDataService.SetBanCount(BanListName.CurrentHunBanned, 2);

// Ban 位可用状态列表
ObservableCollection<bool> canCurrentSurBannedList = _sharedDataService.CanCurrentSurBannedList;
ObservableCollection<bool> canCurrentHunBannedList = _sharedDataService.CanCurrentHunBannedList;
ObservableCollection<bool> canGlobalSurBannedList = _sharedDataService.CanGlobalSurBannedList;
ObservableCollection<bool> canGlobalHunBannedList = _sharedDataService.CanGlobalHunBannedList;
```

##### 倒计时控制

```csharp
// 开始倒计时（秒）
_sharedDataService.TimerStart(60);

// 停止倒计时
_sharedDataService.TimerStop();

// 获取剩余秒数
string remainingSeconds = _sharedDataService.RemainingSeconds;
```

##### 其他设置

```csharp
// 辅助特质可见性
bool isTraitVisible = _sharedDataService.IsTraitVisible;
_sharedDataService.IsTraitVisible = true;

// BO3 模式
bool isBo3Mode = _sharedDataService.IsBo3Mode;
_sharedDataService.IsBo3Mode = false;

// 地图 V2 呼吸灯
bool isMapV2Breathing = _sharedDataService.IsMapV2Breathing;
_sharedDataService.IsMapV2Breathing = true;

// 地图 V2 阵营可见性
bool isMapV2CampVisible = _sharedDataService.IsMapV2CampVisible;
_sharedDataService.IsMapV2CampVisible = false;
```

##### 事件订阅

```csharp
// 订阅数据变化事件
_sharedDataService.CurrentGameChanged += OnCurrentGameChanged;
_sharedDataService.BanCountChanged += OnBanCountChanged;
_sharedDataService.IsTraitVisibleChanged += OnIsTraitVisibleChanged;
_sharedDataService.IsBo3ModeChanged += OnIsBo3ModeChanged;
_sharedDataService.CountDownValueChanged += OnCountDownValueChanged;
_sharedDataService.TeamSwapped += OnTeamSwapped;
_sharedDataService.IsMapV2BreathingChanged += OnIsMapV2BreathingChanged;
_sharedDataService.IsMapV2CampVisibleChanged += OnIsMapV2CampVisibleChanged;
_sharedDataService.PickedMapChanged += OnPickedMapChanged;
_sharedDataService.MapV2BannedChanged += OnMapV2BannedChanged;

private void OnCurrentGameChanged(object? sender, EventArgs e)
{
    // 处理对局变化
}
```

> [!TIP]
> 由于主应用使用数据绑定，当您修改 `ISharedDataService` 中的数据时，前台界面会自动同步更新！你无需担心数据变更的后续操作

---

## 开发环境设置

### 1. 引用 PluginSdk

在项目文件 (.csproj) 中添加：

```xml
<ItemGroup>
  <PackageReference Include="neo-bpsys-wpf.PluginSdk" Version="2.0.0" />
</ItemGroup>
```

### 2. 配置输出

确保 `manifest.yml` 和图标文件被复制到输出目录：

```xml
<ItemGroup>
  <None Update="manifest.yml">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
  <None Update="icon.png">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

### 3. 调试插件

- 在调试模式下构建插件
- 将输出目录中的所有文件复制到主应用的 `Plugins/YourPluginId` 文件夹
- 启动主应用进行测试

---

## 打包与发布

### 使用 PluginPack 自动打包

在项目目录运行：

```bash
dotnet publish -p:PluginPack=true
```

这会自动创建一个包含所有必需文件的插件包，可以直接在插件页面导入它，也有可能不会创建，我也不知道为什么，可以自行去 publish 的目录拿产物打包成 zip

### 手动打包

1. 构建项目（Release 配置）
2. 从输出目录收集以下文件：
   - 插件 DLL 文件
   - `manifest.yml`
   - `icon.png`（如果有）
   - 所有依赖的 DLL（不包括 PluginSdk 和主应用已有的依赖）
3. 将这些文件放入一个文件夹，文件夹名为插件 ID
4. 压缩成 ZIP 文件进行分发

### 安装插件

直接在插件页面从文件包导入插件即可

---

## 示例插件

### ExamplePlugin - 完整功能示例

位置: `neo-bpsys-wpf.ExamplePlugin`

展示了插件的所有能力：
- ✅ 后台管理页面
- ✅ 前台展示窗口
- ✅ 控件注入
- ✅ 自定义服务
- ✅ 配置文件管理

```csharp
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Extensions.Registry;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.ExamplePlugin.Models;
using neo_bpsys_wpf.ExamplePlugin.Services;
using neo_bpsys_wpf.ExamplePlugin.Views;

namespace neo_bpsys_wpf.ExamplePlugin;

public class ExamplePlugin : PluginBase
{
    public PluginSettings Settings { get; set; } = new();

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        // 注册后台页面
        services.AddBackendPage<MainPage, ViewModels.MainPageViewModel>();
            
        // 注册前台窗口
        services.AddFrontedWindow<MainWindow, ViewModels.MainWindowViewModel>();
            
        // 注册服务
        services.AddSingleton<IExampleService, ExampleService>();
            
        // 注册注入控件
        ExampleInjectedControl injectedControl = new();
        FrontedWindowHelper.InjectControlToFrontedWindow(
            "D9AFD731-DB3C-408B-8368-D70E688CE7CB",
            injectedControl, 
            FrontedWindowType.BpWindow, 
            "BaseCanvas",
            new ElementInfo(379, 100, 522, 312)
        );

        // 加载配置文件
        Settings = ConfigureFileHelper.LoadConfig<PluginSettings>(
            Path.Combine(PluginConfigFolder, "Settings.json")
        );
        
        // 监听属性变化
        Settings.PropertyChanged += (sender, args) =>
        {
            ConfigureFileHelper.SaveConfig(
                Path.Combine(PluginConfigFolder, "Settings.json"), 
                Settings
            );
        };
    }
}
```

### TeamJsonMaker - 简单插件示例

位置: `neo-bpsys-wpf.TeamJsonMaker`

一个简单的插件，仅包含一个后台页面：

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Extensions.Registry;

namespace neo_bpsys_wpf.TeamJsonMaker;

public class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        services.AddBackendPage<TeamJsonMaker, TeamJsonMakerViewModel>();
    }
}
```

---

## 常见问题

### Q: 插件 API 版本要求是什么？
A: `apiVersion` 必须满足以下条件：
- 能被解析为 `Version`（例如 `2.0.0.0`）
- 不能低于 `2.0.0.0`
- 主版本不能高于宿主支持的 API 主版本（当前为 `2.x`）

### Q: 如何在插件之间共享数据？
A: 推荐使用主应用的 `ISharedDataService` 或创建自己的服务并注册为单例。

### Q: 插件可以访问主应用的哪些资源？
A: 插件可以访问：
- `ISharedDataService` 中的所有共享数据
- 主应用注册的所有服务
- 前台窗口的画布用于控件注入

### Q: 如何调试插件？
A: 建议将插件构建输出复制到主应用的 Plugins 文件夹，然后使用"附加到进程"调试主应用。

### Q: 插件可以使用第三方 NuGet 包吗？
A: 可以，但请确保在打包时包含所有依赖的 DLL 文件。

---

## 技术支持

如有问题或建议，请访问：
- GitHub 仓库: https://github.com/PLFJY/neo-bpsys-wpf
- 提交 Issue: https://github.com/PLFJY/neo-bpsys-wpf/issues

---

**祝您开发愉快！** 🎉
