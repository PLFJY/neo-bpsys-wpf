# WPF-UI 坑点记录

线程和异步相关规则另见 [threading-dispatcher-and-async.md](threading-dispatcher-and-async.md)。资源、字体和本地化规则另见 [resources-localization-and-assets.md](resources-localization-and-assets.md)。

## 导航和 DI

后台页面、前台窗口、ViewModel 和多数服务都通过 DI 注册。不要绕开：

```csharp
services.AddBackendPage<MyPage, MyViewModel>();
services.AddFrontedWindow<MyWindow, MyWindowViewModel>();
```

手动 `new Page()` 或 `new Window()` 会丢失 DataContext、服务注入、注册表信息和 WPF-UI page provider 集成。插件也应使用这些扩展。

## 生命周期

当前注册模式中：

1. 后台页面是 singleton。
2. 后台页面 ViewModel 是 singleton。
3. 前台窗口是 singleton。
4. 前台窗口 ViewModel 是 singleton。
5. 大部分业务服务是 singleton。

这适合导播工具的长期状态，但也意味着页面构造函数、事件订阅和计时器要谨慎。不要在页面构造里做不可重复释放的重操作；如果订阅全局事件，要考虑是否会泄漏或重复处理。

因为页面/ViewModel 是 singleton，构造函数里的事件订阅通常只发生一次；但如果在命令、页面 Loaded、弹窗或临时对象中订阅事件，就要有解绑策略。否则长时间直播中会出现重复响应或对象无法释放。

## 本地化

项目使用 `WPFLocalizeExtension`：

```xaml
Text="{lex:Loc SomeKey}"
```

启动时设置 `LocalizeDictionary.Instance.Culture`，并将 `Application.Current.Resources["CurrentLanguage"]` 设为当前 `XmlLanguage`。

坑点：

1. 新增用户可见文本应优先添加到 `Locales/Lang.resx`、`Lang.en-us.resx`、`Lang.ja-jp.resx`。
2. 后台代码提示文本应通过 `I18nHelper.GetLocalizedString(...)`。
3. 硬编码中文只适合内部日志、临时调试或明确不本地化的标识。
4. 语言切换相关逻辑会监听 `Settings.Language` 和 `CultureInfo`，不要直接改 `CultureInfo` 私有 setter。
5. `I18nHelper` 找不到 key 时会返回 key 本身；如果界面显示类似 `SomeMissingKey`，优先检查 resx 是否缺项或默认字典配置是否正确。

## WPF-UI 图标

后台代码中创建 WPF-UI 图标的安全模式可参考主窗口：

```csharp
new SymbolIcon(SymbolRegular.Info24, 24D)
```

或：

```csharp
new SymbolIcon { Symbol = SymbolRegular.ArrowExit20 }
```

`BackendPageInfo` 的图标字段直接使用 `SymbolRegular`。创建或更新 `SymbolIcon` 属于 WPF UI 对象操作，应在 UI 线程执行。后台下载、OCR、插件市场回调更新 UI 时，参考 `PluginMarketService.RunOnUiThread` 或 `Application.Current.Dispatcher.Invoke`。

不要在后台线程中创建 `NavigationViewItem`、`SymbolIcon`、`Page` 或 `Window` 并交给 UI 绑定集合。

## InfoBar 和 Snackbar

主窗口构造时把控件交给服务：

```csharp
infoBarService.SetInfoBarControl(InfoBar);
snackbarService.SetSnackbarPresenter(SnbPre);
```

页面或服务应通过 `IInfoBarService` / `ISnackbarService` 访问，不要跨层直接找主窗口控件。错误、警告和下载失败等适合 InfoBar；更新后提示使用 Snackbar。

## ObservableCollection

绑定到 UI 的 `ObservableCollection` 必须在 UI 线程更新。插件市场下载队列已经用 Dispatcher 包装；新下载器回调、OCR 回调或捕获回调如果要改集合，应复用同类模式。直接从后台线程 Add/Remove 会导致 WPF 线程异常或间歇性 UI 崩溃。

## 资源字典和主题

启动时默认深色主题，主题变化会更新 `IconThemesDictionary.Theme`。如果新增图标资源或主题资源，需要确认它在浅色/深色下都可用。

`MainWindow.xaml` 大量依赖动态资源和 WPF-UI 样式。修改全局样式前先确认前台窗口是否也引用了同名资源，避免导播输出窗口被后台样式意外影响。

## 前台窗口透明和背景

部分前台窗口支持透明背景。透明时返回 `Transparent`，非透明时默认绿色 `#00FF00`。OBS 场景可能依赖绿幕色或透明窗口，改默认颜色和 `AllowsWindowTransparency` 行为时需要兼顾直播工作流。

`FrontedWindowBase` 会把内容自动包进 `Viewbox`，并设置 `Stretch.Fill`。前台窗口内部布局应以固定画布和绑定宽高为基础，不要假设窗口内容原样作为根元素存在。

## 插件 UI

插件注册的页面/窗口也进入同一 DI 和 WPF UI 环境。插件作者应：

1. 使用 `BackendPageInfo` / `FrontedWindowInfo`。
2. 避免和宿主或其他插件重复 ID。
3. 注入控件时给控件稳定 `Name` 和合理默认 `ElementInfo`。
4. 用户可见文本同样考虑本地化。

## NavigationView

WPF-UI 默认的 `NavigationView` 样式和行为有坑点。Page被加载后外面会自动包一层 `ScrollViewer`，不要重复包裹，否则就导致页面无法滚动
