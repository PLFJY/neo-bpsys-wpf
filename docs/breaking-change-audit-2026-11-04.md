# 代码审计中需破坏性变更的问题（2026-11-04）

本文件记录最近一轮 XCodeReviewer 审计中**已核实属实、但无法在不破坏现有公共契约的前提下直接修复**的问题。
按项目要求，本轮仅修复了非破坏性缺陷；下列问题需单独立项、评估迁移策略后再处理。

> 说明：仓库现有文档目录为 `docs/`（用户需求中的 `doc` 目录在仓库中对应此目录）。

## 1. 公共同步 API 内部阻塞等待异步任务

**问题描述**

以下公共 API 在同步方法内部使用 `GetAwaiter().GetResult()` 等待异步任务：

- `neo-bpsys-wpf/Services/PluginInstallService.cs`：`InstallFromArchive` 同步等待 `InstallFromArchiveAsync`；
- `neo-bpsys-wpf.SmartBp.Module/Services/SelectedOcrService.cs`：`GetProviderStatus` 同步等待 `_rapidAssets.GetStatusAsync()`；
- `neo-bpsys-wpf.SmartBp.Module/Services/RapidOcrNetProvider.cs`：`IsReady` 与 `EnsureInitializedUnsafe` 同步等待异步资产状态；
- `Built-inPlugins/neo-bpsys-wpf.FocusKeeper/FocusKeeperService.cs`：`EnumerateGameWindows` 在 UI 线程同步枚举窗口并读取进程信息。

**可能导致的问题**

- 在带同步上下文（WPF UI 线程）的线程上调用时可能死锁，导致界面无响应；
- 阻塞 UI 线程造成卡顿（窗口枚举、OCR 状态刷新）；
- 异步任务中的异常被包装为 `AggregateException`，调用方的异常处理语义与异步版本不一致。

**涉及代码**

- `IPluginInstallService.InstallFromArchive` / `PluginInstallService.InstallFromArchive`
- `IOCRService.GetProviderStatus` 及全部调用方
- `RapidOcrNetProvider.IsReady` / `EnsureInitializedUnsafe`
- `IFocusKeeperService.EnumerateGameWindows` 及页面 ViewModel

**破坏性变更内容**

将上述同步 API 改为异步（`Task<...>` / `ValueTask<...>`），或删除同步重载并更新所有调用方：

- `IPluginInstallService` 接口签名变更，主程序与插件、测试需同步修改；
- `IOCRService.GetProviderStatus` 变为 `GetProviderStatusAsync`，SmartBP 模块内多处调用方与测试需改造；
- `IFocusKeeperService.EnumerateGameWindows` 变为异步后，FocusKeeper 管理页初始化流程需要改为异步加载；
- 需补充取消令牌与 UI 线程调度约定，属于跨模块 API 迁移，不能在本轮保守修复中完成。

## 2. 公共构建上下文缺少 `required` 约束

**问题描述**

`neo-bpsys-wpf.Core/Abstractions/Services/FrontedControlBuildContext.cs` 中 `CanvasWidth` / `CanvasHeight` 为 `double` 且未标记 `required`，调用方遗漏赋值时会静默使用 0，导致控件布局不可见。

**可能导致的问题**

- 插件或内置控件构建时忘记设置画布尺寸，控件渲染异常且无编译期提示。

**涉及代码**

- `FrontedControlBuildContext.CanvasWidth` / `CanvasHeight`
- 所有构造 `FrontedControlBuildContext` 的控件宿主与插件实现

**破坏性变更内容**

为两个属性添加 `required` 修饰符属于**源级破坏性变更**：所有现有初始化器必须显式赋值，否则编译失败。
该类型是插件 SDK 公共契约的一部分，需要发布 SDK 迁移说明并同步更新内置控件、示例插件与第三方插件，不能直接在本轮修改。

## 3. 反射修改 `JsonSerializerOptions` 全局默认值

**问题描述**

`neo-bpsys-wpf.Core/Helpers/ConfigureFileHelper.cs` 的静态构造函数通过反射写入
`JsonSerializerOptions` 的私有静态字段 `s_defaultOptions`，以全局改变默认序列化行为。

**可能导致的问题**

- 依赖 .NET 运行时内部字段名，未来 .NET 版本升级可能失效或静默不生效；
- 全局修改影响进程内所有未显式传入选项的 `JsonSerializer` 调用，行为不透明；
- 反射写入失败时无诊断，配置序列化行为可能与预期不一致。

**涉及代码**

- `ConfigureFileHelper` 静态构造函数
- 所有未显式传入 `JsonSerializerOptions` 的序列化/反序列化调用（包括第三方插件代码）

**破坏性变更内容**

移除反射 hack 后，必须逐处显式传入统一的 `JsonSerializerOptions` 实例；凡是依赖当前全局默认值（大小写不敏感、缩进、宽松转义）的调用点都会改变行为。
这是一次全局序列化契约迁移，需要遍历主程序、模块、内置插件与插件 SDK 示例，属于破坏性行为变更。

## 4. 接口中定义静态可变全局字段

**问题描述**

`neo-bpsys-wpf.Core/IAppHost.cs` 在接口 `IAppHost` 中定义 `public static IHost? Host;` 可变字段，
并在静态方法 `GetService<T>` 中直接读取。

**可能导致的问题**

- 任意代码可替换全局 Host，导致服务定位结果不可预测；
- 接口承载可变状态，破坏契约语义并增加测试隔离难度；
- 多宿主/测试并行场景下可能读到错误容器。

**涉及代码**

- `IAppHost.Host`
- `IAppHost.GetService<T>` / `TryGetService<T>` 及所有调用方（主程序、模块、插件）

**破坏性变更内容**

将 `Host` 移入独立静态类（如 `AppHost`）或改为只读属性/依赖注入访问，需要修改所有 `IAppHost.Host` 引用
（数量多且部分位于插件 SDK 公共示例中），属于公共 API 结构调整。

## 5. 基类构造函数调用抽象方法

**问题描述**

`neo-bpsys-wpf/ViewModels/CharaSelectViewModelBase.cs` 在构造函数中调用抽象方法
`SyncCharaFromSourceAsync()`，派生类实现可能访问尚未初始化的派生类字段。

**可能导致的问题**

- 派生类字段在基类构造期间仍为默认值，可能出现空引用或错误状态；
- 行为依赖派生类初始化顺序，难以测试与维护。

**涉及代码**

- `CharaSelectViewModelBase` 构造函数
- `SurPickViewModel`、`HunPickViewModel`、`BanSurPageViewModel`、`BanHunPageViewModel` 等派生类实现

**破坏性变更内容**

需要引入显式 `Initialize()`/工厂方法，并把初始化调用从构造函数移到派生类构造完成之后；
所有派生类及创建这些 ViewModel 的 DI 注册/页面生命周期都需要调整，属于公共 ViewModel 生命周期契约变更。

## 6. 视图模型事件订阅缺少统一释放契约

**问题描述**

多个长期存活的 ViewModel 在构造函数中订阅单例服务事件，但类未实现 `IDisposable`，宿主也未在页面/窗口销毁时调用释放
（如 `MainWindowViewModel`、`ScorePageViewModel`、`TeamInfoPageViewModel`、`MapBpPageViewModel`、`BanSurPageViewModel`、
`BanHunPageViewModel`、`HomePageViewModel`、`PickPageViewModel` 等，详见审计报告 high 级“事件订阅未取消导致内存泄漏”条目）。

**可能导致的问题**

- 若这些 ViewModel 被重复创建或替换，旧实例会被事件源长期持有，造成内存泄漏与重复回调；
- 页面重载、设计器重建等场景可能出现状态串扰。

**涉及代码**

- 上述 ViewModel 的构造函数与服务事件订阅点
- `AddBackendPage` / `AddFrontedWindow` 的 ViewModel 生命周期管理
- 页面/窗口关闭与导航生命周期处理

**破坏性变更内容**

正确修复需要：

1. 为相关 ViewModel 增加 `IDisposable`（或弱事件订阅）；
2. 宿主页面/窗口在销毁时统一调用 `Dispose`；
3. 明确 ViewModel 的注册生命周期（Singleton/Transient）与页面缓存策略。

这会改变页面/窗口生命周期与 DI 注册约定，并可能影响插件页面的既有行为，属于架构级契约变更，本轮仅记录。

## 其他已核实但不属于破坏性变更的项

- `CharacterList.json` / `CharacterList-ja-JP.json` 的 `BookOrderIndex` 成对重复：经核对为同一角色的多语言/多形态条目共用排序键，
  属于既有数据设计；如需改为唯一排序键需产品确认显示顺序，属于数据契约调整。
- `FrontedBehaviorAnimationHelpWindow` 按字面量 `\n` 拆分本地化文本：资源文件确实使用字面量 `\n` 表示换行，当前实现正确。
- `App.xaml` 已声明 `DispatcherUnhandledException="OnDispatcherUnhandledException"`，不存在“未订阅”问题。
- `RefreshAsync` 的 `_refreshStarted` 只置位不复位为设计行为：进程启动阶段只同步一次，按需刷新由 `RefreshOnDemandAsync` 负责。
