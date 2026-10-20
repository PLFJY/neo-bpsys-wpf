# WPF 与 WPF-UI 运行时陷阱

本文只保留跨模块的 WPF 故障模式。导航规格见[ModernNavigationView](backend/modern-navigation-view.md)与[ModernFrame](backend/modern-frame.md)，线程规则见[Dispatcher](architecture/threading-dispatcher-and-async.md)，本地化见[资源字典契约](resources/localization.md)，Designer 交互与释放见[编辑器](frontend/fronted-designer-editor.md)。

## Page 的合法父级

Page 只能由 Window、Frame 或正确实现的导航宿主承载，直接放进 ContentControl、Border、Grid、TabItem 或模块/插件嵌入 host 会抛出 `Page can have only Window or Frame as parent.`。嵌入片段用 UserControl/Control；ModernFrame 内部通过真实 Frame 承载 Page，不是任意 ContentPresenter 都支持 Page。

singleton Page 同时只能有一个宿主。Classic 管理弹窗关闭必须 detach 内容，重开时才能重新承载。ModernFrame 已提供滚动容器，Page 子内容不要重复包 ScrollViewer；有独立滚动语义时显式声明 Self ownership，不能按控件类型猜测。

## DI、事件与对象寿命

后台 Page/VM 多数是 singleton，不在导航时 new 一份。前台 XAML 经 DI 创建；v3 输出窗口由 WindowService 按需创建并可能因透明度重建，不能笼统假定所有前台 Window 都是永久 DI singleton。普通关闭通常 Hide，真正释放走 service close，见[窗口运行时](frontend/frontend-windows-and-layout.md)。

Loaded 可能发生多次，事件订阅、Timer 与附加行为必须幂等，Unloaded/Closed 按所属生命周期解除。singleton 服务事件会反向持有临时窗口；WPF 弱事件或显式解绑均需覆盖。异步回调结束后再次检查所属会话，关闭后不能写回或复活动画。

## 输入、绑定与视觉树

- 属性控件初始化和绑定同步也可能触发事件，不能把所有值变化都当用户提交；失败提交保留输入。Enter 处理读取 TextBox.Text，不能等待 LostFocus 才更新源。
- 窗口级快捷键须让 TextBox、ComboBox、DataGrid、ColorPicker 等处理自己的输入、复制和撤销，避免误删设计项或移动控件。
- UIElement 只能有一个父级。菜单图标复用应克隆或转换数据，不直接把同一 SymbolIcon 挂到多个按钮。
- BitmapImage 使用 OnLoad 读取文件并及时释放流，跨线程传递冻结 BitmapSource；避免缩略图锁住导入/删除文件。
- 重建生成视觉树时注销 namescope 名称，解除动画及旧 presenter 引用；隐藏元素不等于释放它们。
- ObservableCollection 的绑定更新回到 UI Dispatcher，不能从下载/OCR 回调直接 Add/Remove。

## 窗口与关闭

WPF 不允许在继续 Closing 的过程中重入 Show、ShowDialog、Close 或 EnsureHandle。若需异步未保存提示，先取消 Closing，再经 Dispatcher 提示，确认后用强制关闭标记再次 Close。父窗口关闭非模态详情窗时保护已关闭/正在关闭情形。

AllowsTransparency 必须在 source 初始化前设置；v3 配置变化由 WindowService 重建目标实例。不要恢复旧“透明/绿幕”设置类的默认值逻辑，也不要把 Canvas 与 Window 尺寸合为一份。工具窗口设 Owner、默认最大化或修改 Viewbox 结构都可能影响坐标和任务栏行为，需按模块实际宿主验证。

## 通知、主题与图标

InfoBar/Snackbar 经 IInfoBarService / ISnackbarService，不跨层查主窗口控件。SymbolRegular 是图标数据，SymbolIcon 是 WPF UI 对象，须在 UI 线程创建。可用 `new SymbolIcon(SymbolRegular.Info24, 24D)` 或对象初始化器。

全局动态资源影响后台和前台，改样式前检查共享 key 与明暗主题；不要用隐式 ComboBox style 覆盖 WPF-UI 默认样式链。语言切换走 Settings 与本地化服务，不直接改 CultureInfo 私有 setter，不重新引入 Lang.resx 单字典。
