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

## 导航宿主与创建

MainWindow 的 RootNavigation 使用项目自定义 [ModernNavigationView](modern-navigation-view.md)，内部由 [ModernFrame](modern-frame.md) 承载 Page；WPF-UI INavigationService、NavigationViewItem 和图标类型仍为兼容 API，不等于运行时仍使用原 NavigationView 控件。MainWindow 构造时调用 navigationService.SetNavigationControl(RootNavigation)，菜单由 DI 注册表提供。

不要导航时手动 new Page，页面与 VM 已由 DI 以 singleton 注册。ApplicationHostService 根据 IsClassicMode 选择 ClassicBackendWindow 或 Modern MainWindow；Modern 显示后导航 Home。源码里的 Pick/Ban 预加载仅为注释，不会执行。Classic 页管理弹窗与共享状态边界见[classic-mode.md](classic-mode.md)。

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
