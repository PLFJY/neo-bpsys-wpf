# 后台页面与导航

## 后台页面注册

后台页面是 WPF `Page`，通过 `BackendPageInfo` 标注：

```csharp
[BackendPageInfo(
    "page-id",
    "LocalizationOrDisplayName",
    SymbolRegular.Settings24,
    BackendPageCategory.External)]
public partial class MyPage : Page
{
}
```

注册使用：

```csharp
services.AddBackendPage<MyPage, MyPageViewModel>();
```

`AddBackendPage` 会读取特性、检查 ID 重复、写入 `BackendPagesRegistryService.Registered`，并以 singleton 注册页面和 ViewModel。

## 分类

`BackendPageCategory` 用于区分页面出现在内部导航还是外部/设置区域。内置页面在 `App.Services.xaml.cs` 中按注释分为：

| 分类注释 | 页面 |
| --- | --- |
| Internal | Home、队伍、地图 BP、Ban、Pick、天赋、比分、赛后数据 |
| External | 设置、前台管理、插件、SmartBP |

插件页面通常用 `External`，除非明确要进入某个宿主内部区域。

## WPF-UI 导航假设

主窗口构造时：

```csharp
navigationService.SetNavigationControl(RootNavigation);
```

并在 XAML 中绑定 `MenuItems` / `FooterMenuItems`。页面实例来自 WPF-UI 的 page provider 和 DI。不要在导航时手动 `new Page()`；那会绕开 DataContext、单例状态和服务注入。

`ApplicationHostService` 启动后会预加载部分页面，再回到 Home。这个行为意味着页面构造不应做耗时或不可重复的副作用。

## 图标

`BackendPageInfo.Icon` 类型是 WPF-UI 的 `SymbolRegular`，后台代码中需要创建图标对象时，当前安全模式可参考 `MainWindow.xaml.cs`：

```csharp
new SymbolIcon(SymbolRegular.Info24, 24D)
```

或：

```csharp
new SymbolIcon { Symbol = SymbolRegular.ArrowExit20 }
```

这些是 WPF UI 对象，必须在 UI 线程创建或访问。后台任务需要更新 UI 时应回到 `Application.Current.Dispatcher`。

## 常见坑

1. 页面和 ViewModel 当前默认是 singleton。把它们改成 transient 可能导致导航状态、事件订阅和绑定行为变化。
2. 页面显示名多数经 `lex:Loc` 本地化。新增用户可见文本时不要直接硬编码中文或英文。
3. 后台页面注册必须在 Host build 前完成；插件页面只能在插件 `Initialize` 中注册。
4. 页面 ID 和前台窗口 ID 都要全局唯一，插件也共享同一个注册表。
