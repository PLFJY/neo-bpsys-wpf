# neo-bpsys-wpf Plugin SDK

本项目用于开发 `neo-bpsys-wpf` 插件。v3 起 PluginSdk 不再作为 NuGet 包发布；插件作者应 clone `neo-bpsys-wpf` 仓库，并在插件项目中通过 `ProjectReference` 引用本项目源码。3.0 版本起，插件 API 版本为 `3.0.0.0`，前台窗口注册使用强类型 registration 模型，前台控件注册使用统一 V3 Control API（`FrontedV3ControlBase` + `[FrontedV3Control]` + `AddFrontedV3Control<T>()`）；旧 contributor/descriptor 注入 API 与旧控件架构（`IFrontedControl`、`IFrontedControlPluginContributor`、`FrontedPluginControlDescriptor`、`AddFrontedPluginControlContributor<T>()` 等）已移除，旧插件需要迁移到新 API。

完整参考示例请查看 `neo-bpsys-wpf.ExamplePlugin` 项目（插件 ID `plfjy.ExamplePlugin`），它是一个综合示例，演示了所有当前插件能力。

## manifest.yml

```yaml
id: plfjy.ExamplePlugin
name: ExamplePlugin
description: Example plugin.
entranceAssembly: neo-bpsys-wpf.ExamplePlugin.dll
url: https://github.com/PLFJY/neo-bpsys-wpf
version: 1.0.0.0
apiVersion: 3.0.0.0
author: Zero PLFJY
icon: icon.png
```

`apiVersion` 是宿主插件 API 兼容性版本，不等同于 PluginSdk 源码所在仓库提交。

## SDK 引用方式

插件项目应包含 SDK 项目引用，并显式导入打包 target：

```xml
<ItemGroup>
  <ProjectReference Include="..\neo-bpsys-wpf.PluginSdk\neo-bpsys-wpf.PluginSdk.csproj" Private="false" />
</ItemGroup>

<Import Project="..\neo-bpsys-wpf.PluginSdk\neo-bpsys-wpf.PluginSdk.targets" />
```

`ProjectReference` 提供编译期 API，`Import` 提供 `CreateZip` target。独立插件仓库可以把 `neo-bpsys-wpf` 作为 submodule、sparse checkout 或固定提交的源码目录引入。

## 后台页面

```csharp
services.AddBackendPage<MainPage, MainPageViewModel>();
```

插件安装、更新、启用状态在 Host build 前处理，新增页面、服务、前台窗口通常需要重启后生效。

## 插件服务/配置

```csharp
public class ExamplePlugin : PluginBase
{
    public PluginSettings Settings { get; set; } = new();

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        services.AddSingleton<IExampleService, ExampleService>();

        Settings = ConfigureFileHelper.LoadConfig<PluginSettings>(
            Path.Combine(PluginConfigFolder, "Settings.json"));
    }
}
```

## Designer v3 插件控件

插件前台控件通过统一 V3 Control API 注册到 Designer v3 控件 registry，不再注入到内置窗口的 WPF Canvas。控件必须继承 `FrontedV3ControlBase` 并标注 `[FrontedV3Control]` 特性。

### 注册控件

```csharp
public sealed class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        services.AddFrontedV3Control<TeamCardControl>();
    }
}
```

`AddFrontedV3Control<TControl>()` 只接受控件类型一个参数。`PackageId` 由宿主在插件初始化作用域内自动注入，控件作者不得传入 `CanonicalControlType`、`Config factory`、`CreateControl delegate` 或 `Property descriptor list`。

控件类型必须标注 `[FrontedV3Control("ControlId")]` 并继承 `FrontedV3ControlBase`。`ControlId` 只接受安全的局部标识：非空、非纯空白，且不含 `/`、`\`、`:`，也不允许直接传入完整的 canonical ID（`plugin:package/control` 形式）。不同插件可以复用相同的 `ControlId`。

Canonical Control Type 由 `ControlId` 和来源自动推导：

```text
内置控件:   直接使用 ControlId（例如 Text）
插件控件:   plugin:{PackageId}/{ControlId}（例如 plugin:plfjy.ExamplePlugin/TeamCard）
```

`IsBuiltIn` 是 attribute named argument，仅供宿主注册代码设置为 `true`；插件在插件作用域内设置该值为 `true` 会被拒绝。

### 创建控件

控件类继承 `FrontedV3ControlBase`（定义在 Core 程序集，命名空间 `neo_bpsys_wpf.Core.Abstractions.Services`）。该基类继承自 `UserControl`，支持 XAML 声明式视觉树。宿主在创建后通过 `InitializeFrontedV3` 注入运行时上下文，控件通过 `Context` 访问服务、共享数据、资源解析器与当前配置。

控件 **不** 管理自身的 Canvas 坐标（`Left`/`Top`/`Width`/`Height`/`ZIndex`/`Visibility`/`GaussianBlur`），这些由 `FrontedV3ControlHost` 统一负责。控件只负责矩形区域内的视觉内容。

```csharp
[FrontedV3Control("TeamCard")]
public partial class TeamCardControl : FrontedV3ControlBase
{
    public TeamCardControl()
    {
        InitializeComponent();
    }
}
```

基类 `FrontedV3ControlBase.InitializeFrontedV3` 已统一将 `DataContext` 设置为完整 `FrontedV3ControlContext`，派生控件无需自行设置 `DataContext`。XAML 通过 `Options.*` 根命名空间访问 V3 属性（例如 `{Binding Options.Appearance.TextColor}`）。

### 声明属性

属性通过控件类上的 `public static readonly FrontedV3Property<T>` 字段声明，框架在注册时通过反射发现并转换为属性定义。

```csharp
public static readonly FrontedV3Property<string> TextColorProperty =
    new("Appearance.TextColor", FrontedV3Storage.ExtensionData("TextColor"));

public static readonly FrontedV3Property<string> TeamNameProperty =
    new("Content.TeamName", FrontedV3Storage.ExtensionData("TeamName"));
```

`OptionsPath`（如 `Appearance.TextColor`）只是 Designer 属性网格与 StyleTransfer 的逻辑路径，**不进入 JSON**；实际读写位置由 `Storage` 访问器决定。`FrontedV3Storage` 提供三种存储访问器：

| 存储访问器 | 用途 | JSON 位置 |
| --- | --- | --- |
| `FrontedV3Storage.ExtensionData("key")` | 插件控件默认存储，读写 `PluginFrontedControlConfig.ExtensionData` 字典 | 序列化后平铺到 JSON 根级字段 |
| `FrontedV3Storage.ClrProperty("PropertyName")` | 内置控件迁移到 v3 API 后使用，反射读写 Config 的 CLR 属性 | 由 Config 类的 JSON 序列化决定 |
| `FrontedV3Storage.CollectionItemProperty(...)` | 读写 PartCollection 集合项的 CLR 属性（如 Cell 的 `X`/`Y`） | 由集合项的 JSON 序列化决定 |

存储访问器不得覆盖根级保留字段（`Left`/`Top`/`Width`/`Height`/`ZIndex`/`Visibility`/`BehaviorGuid`/`GaussianBlur`/`ControlType`），该校验在注册时完成。

### Options 动态代理视图

`Options` 是由属性 Schema 构建的动态代理视图，**不进入 JSON**，**不缓存独立值**。基类已将 `DataContext` 设置为完整 `FrontedV3ControlContext`，XAML 通过 `Options.*` 路径访问属性（例如 `{Binding Options.Appearance.TextColor}`），`Options` 通过 `ICustomTypeDescriptor` 发现动态属性，最终委托到对应存储访问器，直接读写当前 Config 的根级字段。

### 固定 Part（内部区域）

固定 Part 系统管理控件内部固定区域（如 BorderedImage 的内层 Image）。通过控件类上的 `public static readonly FrontedV3Part` 字段声明：

```csharp
public static readonly FrontedV3Part LogoPart =
    FrontedV3Part.Register<TeamCardControl>("Logo")
        .WithSize(
            FrontedV3Storage.ExtensionData("LogoWidth"),
            FrontedV3Storage.ExtensionData("LogoHeight"))
        .WithCapabilities(FrontedV3PartCapabilities.Resize);
```

XAML 中通过 `fronted:FrontedV3.PartId="Logo"` 附加属性标记 Part Visual，与 C# 特性 `[FrontedV3PartVisual("Logo")]` 等价。Part 只管理控件内部固定区域的几何，不管理根布局。

框架在 `FrontedV3ControlHost.TryInitialize` 中统一调用 `FrontedV3PartVisualRuntimeBinder`，自动完成以下工作：

- 通过 `FrontedV3PartVisualResolver` 发现控件视觉树中标注的 Part Visual（XAML 附加属性或 C# 特性）。
- 根据 Part 的 `WidthStorage`/`HeightStorage`/`XStorage`/`YStorage` 读取 Config 中的几何值，应用到对应 `FrameworkElement`。
- 输出 Missing/Duplicate Visual 诊断，不阻止控件初始化。

派生控件**无需**在 `OnInitializeFrontedV3` 中手写几何读取代码。声明完 `FrontedV3Part` 与 XAML `fronted:FrontedV3.PartId` 后，运行时几何绑定由框架接管。

### PartCollection（模板或动态子项）

PartCollection 系统管理模板或动态集合（如 GlobalScoreRow 的 Cells）。通过控件类上的 `public static readonly FrontedV3Parts` 字段声明：

```csharp
public static readonly FrontedV3Parts CellsCollection =
    FrontedV3Parts.RegisterCollection<GlobalScoreRowControl>("Cells")
        .WithStrategy(FrontedV3PartCollectionStrategy.FixedTemplate)
        .WithItemCapabilities(FrontedV3PartCapabilities.MoveAndResize)
        .WithCollectionGetter(c => ((GlobalScoreRowControlConfig)c).Cells)
        .WithItemKeySelector(item => ((GlobalScoreCellConfig)item).Id)
        .WithEnsureTemplateItems(c => EnsureCells((GlobalScoreRowControlConfig)c));
```

三种预设策略：

| 策略 | 行为 | 典型场景 |
| --- | --- | --- |
| `FixedTemplate` | 根据业务模板补齐缺失项，拒绝任意增删；可移动、缩放、编辑 | GlobalScoreRow 的 Cells |
| `Dynamic` | 允许任意增删集合项 | 动态图层列表 |
| `ReadOnly` | 只读，不允许增删或几何操作 | 只读装饰集合 |

### StyleTransfer

`FrontedV3StyleTransferService` 提供父-子继承与同 peer 传播，替代旧链路中 MapV2/GlobalScore 的手写 StyleTransfer 特判。

关键约束：

- Peer 传播仅匹配完全相同的 `CanonicalControlType`。`plugin:a/TeamCard` 不能传播给 `plugin:b/TeamCard`。
- 默认仅传播 `Appearance` 语义的属性（颜色、字体、边框等）。
- `DataIdentity` 语义（MapKey、TeamType、BindingPath、ControlName 等）和根级保留字段（`Left`/`Top`/`ZIndex` 等）**永远不传播**。
- 属性语义通过 `FrontedV3PropertyMetadata Semantic` 声明，默认为 `Other`（不参与传播）。

### 缺失插件行为

当插件缺失时：

- `ExtensionData` 原样保留，不会丢失。
- Designer 显示 Missing Plugin placeholder，根控件仍可选择、移动、缩放和删除。
- 运行时默认跳过该控件并记录 warning。
- 导出会继续保留控件 JSON、`ExtensionData` 和依赖元数据。
- 不会写入任何默认值掩盖缺失。
- 安装插件并重启后，schema 恢复可用。

### Binding Browser

宿主 Binding Browser 由显式 root 和绑定 attribute 反射 catalog 驱动，不会扫描任意插件服务或调用运行时 getter。插件如果需要暴露自己的语义绑定源，应通过宿主提供的 binding root/contributor 扩展点注册稳定 root、虚拟节点或 semantic key；普通 DTO 属性应使用 `[FrontedBindingObject]`、`[FrontedBindable]`、`[FrontedBindingIgnore]` 和 `[FrontedBindingCollection]` 这类契约描述，而不是要求宿主手写每个属性节点。

## 插件前台窗口

v3 提供两类前台窗口，均通过强类型 registration 模型注册。窗口类型不在 `manifest.yml` 中指定。

### XAML Window

插件提供自己的 WPF `Window` 类型，宿主只负责创建、注册、显示和隐藏。它会出现在 FrontManage，不默认进入 Designer。

窗口类必须使用 `[FrontedWindowInfo]` 特性标注，并继承 `FrontedWindowBase`。`FrontedWindowInfo.Id` 是稳定的窗口 Local ID；推荐新插件使用 GUID 以降低与历史插件发生冲突的可能，但不强制 GUID，允许任何通过 Window Local ID 安全校验的稳定字符串（不含 `/`、`\`、`:`、控制字符或前后空白）。`IsBuiltIn` 是 attribute named argument，默认 `false`；内置窗口设 `IsBuiltIn = true`。

```csharp
[FrontedWindowInfo("3363BFE1-1393-4765-B926-001B6848FAF7", "Example XAML Window")]
public partial class ExampleXamlWindow : FrontedWindowBase
{
    public ExampleXamlWindow() => InitializeComponent();
}
```

注册：

```csharp
services.AddFrontedWindow<ExampleXamlWindow, ExampleXamlWindowViewModel>();
```

`AddFrontedWindow<TWindow, TViewModel>` 会读取 Attribute 上的 `Id` 作为 runtime ID（LocalId），注册 ViewModel 和 Window，并在创建时设置 DataContext。PackageId 仅表示来源，不参与 v3 layout / Designer。`FrontedWindowInfo` 旧的 canvas 构造函数仍保留但参数会被忽略，Canvas 注入能力不会恢复。

### v3 Layout Window

插件声明一个 layout window，宿主使用标准 v3 layout host 渲染。它可出现在 FrontManage，进入 Designer 编辑。

```csharp
services.AddFrontedV3LayoutWindow("ExampleLayoutOverlay");
```

`AddFrontedV3LayoutWindow(string windowId, bool isBuiltIn = false)` 只接受局部窗口标识 `windowId` 和 `isBuiltIn` 两个参数。`windowId` 只需插件内唯一；`isBuiltIn` 默认 `false`，内置窗口显式传 `true`。PackageId 不是 API 参数，由宿主在插件初始化作用域内通过 `FrontedPluginRegistrationContext` 自动注入。

局部 ID 验证规则：不允许包含 `/`、`\`、`:`、`.`、`..` 或非法文件名字符；不允许直接传入完整 `plugin:package/window` 形式。同名局部 ID 可以存在于不同插件（`plugin:a/Overlay` 与 `plugin:b/Overlay` 共存）。

### v3 Layout 加载

v3 Layout Window 不要求默认 JSON 存在。无默认 JSON 时使用 `FrontedV3LayoutWindowConfigFactory` 生成的内存空模板，可正常渲染并打开 Designer；Designer 首次保存时才创建 JSON 文件。

加载优先级：

```text
内置: 激活 package → 内置资源 → 空模板
插件: 激活 package → 空模板
```

宿主不从插件安装目录加载默认 v3 Layout。

## 标识模型

前台窗口使用 Canonical ID 作为运行时、Designer 和 `.bpui` 的统一身份：

```text
内置 v3 窗口:   直接使用 local ID（例如 BpWindow）
插件 v3 窗口:   plugin:{PackageId}/{LocalWindowId}（例如 plugin:plfjy.ExamplePlugin/ExampleLayoutOverlay）
内置 XAML 窗口: 直接使用 Attribute ID（例如 3363BFE1-1393-4765-B926-001B6848FAF7）
插件 XAML 窗口: plugin:{PackageId}/{AttributeId}
```

`FrontedWindowInfo.Id` 是稳定的窗口 Local ID。推荐新插件使用 GUID，以降低与历史插件发生冲突的可能；但为了兼容既有社区插件，不强制 GUID。允许任何通过 Window Local ID 安全校验的稳定字符串（不含 `/`、`\`、`:`、控制字符或前后空白）。插件 XAML 窗口的 Canonical ID 为 `plugin:{PackageId}/{AttributeId}`，宿主直接注册的 XAML 窗口为 `{AttributeId}`。XAML 窗口不参与 v3 layout、Designer 或 `.bpui` layout entry。PackageId 仅表示来源，不参与 v3 layout / Designer。

`.bpui` 契约保持不变：`FormatVersion=3`、`LayoutSchemaVersion=3`、所有 JSON 字段名不变，`Content.Layouts[].Window` 使用 Canonical ID。导入再导出不会重写 Canonical ID。

用户 layout 安全路径示例：

```text
FrontedLayouts/BpWindow.json
FrontedLayouts/plugin/plfjy.ExamplePlugin/ExampleLayoutOverlay.json
```

同一 Canonical ID 冲突时启动失败，不会静默跳过。

## 缺失插件行为

导入 `.bpui` 时，如果插件窗口 registration 缺失，导入仍成功，layout JSON、资源和依赖元数据会保留；运行时不创建该窗口，FrontManage 和 Designer 不显示它。插件安装并重启后，窗口会重新可用并使用已保留布局。

如果 layout 中存在缺失插件控件，导入仍成功。Designer 显示 Missing Plugin placeholder，可选择、移动、缩放和手动删除；运行时默认跳过该控件并记录 warning；导出会继续保留控件 JSON、`ExtensionData` 和依赖元数据。

插件市场安装/更新引导仍可用，但永不静默安装；导入不依赖安装完成。安装或更新插件后通常需要重启。

## Designer v3 自定义动画触发事件

插件可以注册自己的语义事件，让布局作者在 Designer v3 中把它们选作 OneShot 或 Loop 行为的动画触发器。职责边界是：**插件只描述并发布“发生了什么”，Behavior 配置决定“发生以后执行什么动画”**。

事件始终进入宿主现有链路：

```text
Plugin business logic
  -> IFrontedBehaviorEventPublisher<TPlugin>
  -> FrontedBehaviorEvent
  -> IFrontedEventBus
     -> WPF V3 Runtime
     -> WebRenderer
  -> TriggerEvaluator
  -> Behavior Graph
  -> Animation Runtime
```

插件不得查找某个 Behavior，不得直接调用 `IFrontedAnimationRuntime`，也不要借用 `Guidance.*`、`SharedData.*`、`Selection.*` 等既有业务事件来模拟插件按钮或插件业务动作。这样会污染宿主事件语义，并可能误触发其他监听器。

### 注册事件

在 `PluginBase.Initialize(...)` 中调用 `AddFrontedBehaviorEvents<TPlugin>()`。插件作者只填写 local EventId，不能手写或重复传入自己的 PackageId：

```csharp
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Extensions.Registry;

public sealed class ExamplePlugin : PluginBase
{
    public override void Initialize(
        HostBuilderContext context,
        IServiceCollection services)
    {
        services.AddFrontedBehaviorEvents<ExamplePlugin>(events =>
        {
            events.Add("ShowCard", e => e
                .WithDisplayName("Show card")
                .WithDescription("Request the frontend to show a player card")
                .WithCategory("Example")
                .AddPayload<int>("PlayerIndex", "Player index")
                .AddPayload<string>("PlayerName", "Player name")
                .AddPayload<Camp>("Camp", "Camp"));

            events.Add("HideCard", e => e
                .WithDisplayName("Hide card")
                .WithCategory("Example"));
        });
    }
}
```

宿主在插件初始化作用域中自动取得 `manifest.yml` 的 PackageId，并生成 canonical EventType：

```text
local EventId: ShowCard
PackageId:     top.example.overlay
EventType:     plugin:top.example.overlay/ShowCard
```

local EventId 必须是非空的安全局部标识，不能包含 `plugin:`、`/`、`\`、`:` 或 `..`。不同插件可以使用同一个 local EventId；同一插件重复注册相同事件会在启动注册阶段明确失败。插件不能把事件声明为 built-in，也不能伪造其他插件的 namespace。

`WithDisplayName`、`WithDescription`、`WithCategory` 的直接文本是可靠 fallback。它们都允许额外提供宿主 localization key；当前版本不会跨程序集自动解析插件自己的 resx，因此插件不应只提供宿主不存在的 key 而省略直接文本。

### 发布事件

业务服务注入与插件入口类型绑定的发布器，并继续使用注册时的 local EventId：

```csharp
public sealed class ExampleController(
    IFrontedBehaviorEventPublisher<ExamplePlugin> publisher)
{
    public void Execute()
    {
        publisher.Publish(
            "ShowCard",
            new Dictionary<string, object?>
            {
                ["PlayerIndex"] = 2,
                ["PlayerName"] = "Alice",
                ["Camp"] = Camp.Sur
            });
    }
}
```

发布器拒绝未注册的 local EventId、未知或缺失 payload 字段、类型不匹配，以及带 `Event.` 前缀的 payload key。它自动写入 canonical EventType、`Source = "Plugin:<PackageId>"`、UTC 时间戳和可选 `WindowId` / `WindowType`，再发布到唯一的 `IFrontedEventBus`。调用方不需要也不能在每次发布时传 PackageId。

允许的 payload 类型为：

- `string`、`bool`
- `byte`、`short`、`int`、`long`
- `float`、`double`、`decimal`
- `Guid`
- `enum`
- `DateTime`、`DateTimeOffset`
- 上述值类型的 nullable 版本

复杂对象、集合和任意 DTO 不属于插件事件协议。枚举会规范化为稳定 enum name；`Guid` 和日期会规范化为 invariant 字符串，以便 WPF filter、Graph 与 WebRenderer IPC 使用同一语义。注册字段与发布字典中的 key 不带 `Event.` 前缀。

### Designer 使用

注册后，Designer 会显示：

```text
Trigger:
    Example / Show card

Filter:
    Event.PlayerIndex == 2
    Event.Camp == Sur
```

普通插件事件不区分“瞬发事件”“循环开始事件”或“循环停止事件”：每个已注册事件都可用于 OneShot `Trigger`、Loop `StartTrigger` 和 `StopTriggers`，由 Behavior 自己决定收到事件后执行哪种动画。Graph 中继续使用 `Event.*`；Loop 的阶段语义仍是 `StartEvent.*` 和 `StopEvent.*`。第一版普通插件事件不用于 Transition，因为 Transition 由 `IFrontedTransitionOrchestrator` 的 `ExitGraph -> commit -> EnterGraph` 独立链路驱动；未来的插件 Transition API 将作为单独能力设计。

### 包依赖与缺失恢复

导出 `.bpui` 时，宿主扫描 behavior 文档中的 `Trigger.EventType`、`StartTrigger.EventType`、`StopTriggers[].EventType` 和 `TransitionTrigger.EventType`。`plugin:<PackageId>/<EventId>` 引用会自动进入 manifest `PluginDependencies`：`Events` 保存 canonical EventType，`RequiredBy` 保存引用窗口，已安装插件版本沿用现有依赖合并规则写入 `MinVersion`。

缺少插件或某个 event registration 不会阻止导入，也不会删除、清空或替换 behavior。Designer 会保留原 EventType、Filters 和 Graph，并显示 Missing plugin event；插件重新安装或恢复注册、重启应用后，同一 canonical EventType 会自动重新被识别，不需要迁移 JSON。插件安装或更新通常需要重启，因为插件能力在 Host build 前注入 DI，不支持运行时 hot reload。

仓库中的 `neo-bpsys-wpf.ExamplePlugin` 提供了可运行演示：`CounterChanged` 发布 `CounterValue` / `Delta`，另有 `StartPulse` 和 `StopPulse` 两个无负载语义事件。三个事件都可自由用于 OneShot 或 Loop 的启动/停止触发器；名称只表达示例业务含义，不限制 Behavior 类型。启动 Debug 构建后，可在示例插件后台页点击三个带图标按钮观察发布状态，并在 Designer 中配置对应动画。

## 迁移说明

旧的前台窗口 contributor/descriptor 架构（包括 contributor 接口、window descriptor、window kind 枚举和 contributor 注册扩展）已整体移除，不提供 Obsolete shim，也不保留 adapter。旧的前台控件架构（`IFrontedControl`、`IFrontedControlPluginContributor`、`FrontedPluginControlDescriptor`、`AddFrontedPluginControlContributor<T>()` 等）也已整体移除，由统一 V3 Control API 替代。

迁移到新 API：

1. **Plugin XAML Window**：将原 contributor 中的窗口类型、ViewModel 类型、显示名和 GUID 整理到窗口类上的 `[FrontedWindowInfo("GUID", "DisplayName")]` 特性，并让窗口类继承 `FrontedWindowBase`，然后用 `services.AddFrontedWindow<TWindow, TViewModel>()` 注册。
2. **Plugin v3 Layout Window**：将原 descriptor 中的窗口短名作为局部 ID 传入 `services.AddFrontedV3LayoutWindow("WindowId")`。原 descriptor 上的默认布局根、空白布局开关、插件目录等字段不再支持；如需默认布局，改由用户首次在 Designer 中保存生成。
3. **Designer v3 插件控件**：将原 `IFrontedControlPluginContributor` + `FrontedPluginControlDescriptor` 实现改为继承 `FrontedV3ControlBase`、标注 `[FrontedV3Control("ControlId")]`、用 `public static readonly FrontedV3Property<T>` 声明属性，并通过 `services.AddFrontedV3Control<TControl>()` 注册。原 descriptor 上的 `CreateControl` delegate、`CreateDefaultConfig`、`Properties` 列表和 `Validate` 回调不再由插件提供，改由属性字段反射发现与框架默认实现。插件专属属性存储到 `ExtensionData`（序列化为 JSON 根级字段），不再需要手写 typed Config 类。

旧的"把 WPF 控件直接塞进现有前台窗口"能力已移除，不提供兼容路径。需要可编辑 overlay 元素时使用 Designer v3 插件控件；需要插件完全控制 XAML/行为时使用 XAML Window；需要宿主托管的可编辑 layout 窗口时使用 v3 Layout Window。

参照 `neo-bpsys-wpf.ExamplePlugin` 项目获取完整迁移示例。
