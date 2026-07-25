# neo-bpsys-wpf 前台窗口注册系统第二轮严格验收

## 一、验收结论

* 旧 `IFrontedWindowDescriptor`、`FrontedPluginWindowDescriptor`、`FrontedBuiltInWindowDescriptor`、`IFrontedWindowPluginContributor`、旧 window contributor extension 及示例 contributor 已从源码中删除；
* 前台窗口主模型已经收敛为 `FrontedXamlWindowRegistration` 与 `FrontedV3LayoutWindowRegistration`；
* 插件初始化使用 `AsyncLocal` registration scope 注入 `PackageId`；
* 内置 v3 窗口已在 App DI 中显式调用 `AddFrontedV3LayoutWindow()`；
* v3 加载顺序基本符合约定：

  * 内置：活动包 → built-in resource → 内存空模板；
  * 插件：活动包 → 内存空模板；
* 插件安装目录不再作为 v3 默认布局来源；
* 空模板不会自动落盘。

但是以下问题仍与既定计划直接冲突，其中 **P0 必须全部修复**。在 P0 完成前，不接受本轮架构验收。

---

# P0-1：Canonical ID 的大小写规则与 Windows 文件路径不一致

## 当前代码

Registry 当前使用：

```csharp
new Dictionary<string, FrontedWindowRegistration>(
    StringComparer.Ordinal);
```

并且空 ID 不是 fail-fast，而是记录 warning 后静默跳过。

WindowService 的实例和状态缓存则是没有指定 comparer 的普通字典：

```csharp
public Dictionary<string, Window> FrontedWindows { get; } = [];
public Dictionary<string, bool> FrontedWindowStates { get; } = [];
```

## 为什么这是实际 Bug

Windows 文件系统默认大小写不敏感：

```text
plugin:test/Overlay
plugin:test/overlay
```

Registry 当前认为是两个窗口，但二者映射到的布局文件路径在 Windows 上会发生碰撞。

而且一旦 Registry 改成忽略大小写，WindowService 当前仍会产生另一类 Bug：

1. 调用 `ShowWindow("bpwindow")`；
2. Registry 找到正式注册 `"BpWindow"`；
3. Window 存入 `FrontedWindows["BpWindow"]`；
4. `ShowWindowAsync` 后续继续使用调用者传入的 `"bpwindow"` 更新状态；
5. Window 与 state 使用两套 key；
6. `HideWindow("bpwindow")` 可能无法找到真正窗口。

当前 `EnsureWindowCreated` 虽然用 `registration.Id` 注册实例，但 `ShowWindowAsync`、`HideWindow` 和事件发布仍继续使用输入参数。

## 必须修改

Registry：

```csharp
_byCanonicalId =
    new Dictionary<string, FrontedWindowRegistration>(
        StringComparer.OrdinalIgnoreCase);
```

WindowService：

```csharp
private readonly Dictionary<string, Window> _frontedWindows =
    new(StringComparer.OrdinalIgnoreCase);

private readonly Dictionary<string, bool> _frontedWindowStates =
    new(StringComparer.OrdinalIgnoreCase);
```

所有接受字符串 ID 的公开入口必须统一：

```csharp
if (!_windowRegistry.TryGet(requestedId, out var registration))
{
    ...
}

var canonicalId = registration.Id;
```

从解析成功以后，整条调用链只允许使用：

```csharp
registration.Id
```

不得继续使用调用者传入的原始字符串。

Registry 遇到空白 ID 必须抛出异常，不能 warning 后静默丢弃。

## 必须增加测试

```text
Registry_RejectsCanonicalIdsDifferingOnlyByCase
Registry_TryGet_IsOrdinalIgnoreCase
ShowWindow_CaseVariantUsesRegisteredCanonicalId
HideWindow_CaseVariantFindsSameInstance
RuntimeCaches_UseCanonicalComparer
Registry_EmptyIdFailsFast
```

---

# P0-2：`.bpui` 字段没有改，但语义 round-trip 仍然被破坏

“`FormatVersion` 等字段没变化”只证明 schema 常量没改，不能证明 `.bpui` 契约没变化。

当前有两个独立的 round-trip 问题。

## 问题 A：Importer 会重写每一个 Layout JSON

Importer 当前：

```csharp
var config =
    JsonSerializer.Deserialize<FrontedWindowConfig>(...);

await File.WriteAllTextAsync(
    path,
    JsonSerializer.Serialize(config, options));
```

但 `FrontedWindowConfig`、`FrontedWindowSettings` 等根模型没有完整的 `JsonExtensionData` 保留机制。

因此普通 v3 导入会：

* 删除宿主暂时不认识的根字段；
* 删除未来版本扩展字段；
* 改写 JSON 格式和属性顺序；
* 可能删除插件或第三方工具放入的扩展信息；
* 违反仓库 `AGENTS.md` 中“不应在读取期重写持久化内容”的规则。

## 必须修改

Importer 可以反序列化用于：

* schema 校验；
* 控件依赖扫描；
* 资源安全检查；
* Missing Plugin 诊断。

但**普通导入不得覆盖原 Layout JSON**。

删除：

```csharp
File.WriteAllTextAsync(
    path,
    JsonSerializer.Serialize(config, ...));
```

只有显式 legacy migration 才允许生成新 JSON。

---

## 问题 B：Exporter 只导出当前 Registry 中存在的窗口

Exporter 的选择源是：

```csharp
_layoutCatalog.GetEntries()
```

而 `FrontedDesignerLayoutCatalog.GetEntries()` 只列出：

```csharp
_windowRegistry.GetV3LayoutWindows()
```

所以出现以下场景时：

1. 用户导入一个包含
   `plugin:missing.plugin/Overlay` 的 `.bpui`；
2. 当前未安装该插件；
3. Importer 将文件安装到包目录；
4. 用户重新导出该活动包；
5. 未安装插件窗口不在 Registry；
6. Exporter 不会枚举它；
7. 该 layout、behavior 和 manifest entry 从导出结果中消失。

同时，Exporter 会对所有当前已注册窗口调用 LayoutService。缺失文件会得到空模板，因此还可能把原包中不存在的窗口补成空布局。

当前实现本质是：

```text
根据当前运行环境中的 Registry 重建一个新包
```

而不是：

```text
保留当前活动包内容，再更新用户实际编辑过的内容
```

Exporter 还会把强类型 `FrontedWindowConfig` 重新序列化，因此已安装窗口中的未知 JSON 字段同样可能丢失。

## 必须修改

“导出全部”必须基于当前活动包的 package snapshot：

```text
活动包 manifest 中已有的 Layout entries
+
活动包磁盘上已有的合法 Layout 文件
+
当前 Registry 中实际保存到活动包的新 Layout
```

不得仅以 Registry 为全集。

要求：

1. 未注册或未安装插件的 layout 文件原样保留；
2. 对应 behavior 文件保留；
3. manifest 中 `Content.Layouts[].Window` 保留原 Canonical ID；
4. 路径继续保持：

   ```text
   FrontedLayouts/plugin/{PackageId}/{LocalId}.json
   ```
5. Registry 中存在但活动包内没有、且用户从未保存的窗口，不得在导出时自动生成空 JSON；
6. 已有 JSON 应优先以 `JsonNode` 操作必要资源路径，不要先强类型序列化后再 parse；
7. 依赖扫描可以读取强类型 projection，但不得以 projection 替换原始 JSON。

## 必须增加端到端测试

构造 `.bpui`，包含：

```text
Window = plugin:missing.plugin/Overlay
Path   = FrontedLayouts/plugin/missing.plugin/Overlay.json
```

JSON 加入未知字段：

```json
{
  "Version": 3,
  "VendorExtension": {
    "KeepMe": true
  }
}
```

并加入对应：

```text
FrontedBehaviors/plugin/missing.plugin/Overlay.behaviors.json
```

执行：

```text
Import → Activate → Export All
```

断言：

* `Window` 字符串完全一致；
* layout 路径完全一致；
* layout 文件仍存在；
* behavior 文件仍存在；
* `VendorExtension.KeepMe` 仍存在；
* 未安装插件不会阻止导入或导出；
* 未注册但不存在于原包的窗口不会被补成空模板。

---

# P0-3：`FrontedWindowType.ScoreWindow` 的组合语义已经失效

枚举注释明确规定：

```text
ScoreWindow 等同于同时操作
ScoreSurWindow
ScoreHunWindow
ScoreGlobalWindow
```

但当前：

```csharp
GetFrontedWindowCanonicalId(
    FrontedWindowType.ScoreWindow)
```

返回：

```text
00000000-0000-0000-0000-000000000000
```

WindowService 的 enum overload 只是将该值转发给字符串 overload，没有任何组合分派。

因此：

```csharp
ShowWindow(FrontedWindowType.ScoreWindow)
HideWindow(FrontedWindowType.ScoreWindow)
```

目前不会操作三个比分窗口，只会查找一个不存在的空 GUID。

## 必须修改

在 enum overload 中显式处理组合类型：

```csharp
if (windowType == FrontedWindowType.ScoreWindow)
{
    ShowWindow(FrontedWindowType.ScoreSurWindow);
    ShowWindow(FrontedWindowType.ScoreHunWindow);
    ShowWindow(FrontedWindowType.ScoreGlobalWindow);
    return;
}
```

Hide 同理。

不要让 `ScoreWindow` 进入普通 Canonical ID 解析。

旧 GUID 映射如果仍供 legacy converter 使用，应移动到明确的：

```text
LegacyFrontedWindowIdMap
```

不要继续留在新 runtime identity 主链。

## 必须增加测试

```text
ShowScoreWindow_ShowsAllThreeScoreWindows
HideScoreWindow_HidesAllThreeScoreWindows
ScoreWindow_IsNeverResolvedAsGuidEmptyRegistration
```

---

# P0-4：安全打开链路仍然存在 `async void`

当前：

```csharp
public async void AllWindowShow()
```

它逐个 await `ShowWindowAsync`。只要任意窗口在 `Show()`、设置或初始化阶段抛异常，异常就会从 `async void` 进入 WPF SynchronizationContext，存在直接导致应用异常退出的可能。

`ShowWindow(string)` 则使用：

```csharp
ShowWindowAsync(...).ContinueWith(...)
```

只记录 fault，但没有统一用户错误处理，也没有和 `AllWindowShow` 共用安全入口。

新增的 `SafeWindowOpenTest` 只测试了：

```text
EnsureWindowCreated()
```

没有测试真实的 `Window.Show()`、`AllWindowShow()` 或显示后初始化失败。

## 必须修改

```csharp
public void ShowWindow(string id)
{
    _ = ShowWindowSafelyAsync(id);
}

private async Task ShowWindowSafelyAsync(string id)
{
    try
    {
        await ShowWindowCoreAsync(id);
    }
    catch (Exception ex)
    {
        _logger.LogError(...);
        await MessageBoxHelper.ShowErrorAsync(...);
    }
}
```

显示全部：

```csharp
public void AllWindowShow()
{
    _ = ShowAllWindowsSafelyAsync();
}
```

内部 `Task` 方法完整捕获异常，不允许 `async void`。

单个窗口失败时不能阻止后续其他窗口打开。

## 必须增加测试

```text
ShowWindow_WhenWindowShowThrows_DoesNotEscapeException
AllWindowShow_OneWindowFails_ContinuesRemainingWindows
AllWindowShow_HasNoAsyncVoidImplementation
```

---

# P1-1：`FrontedLayoutService` 仍然是伪兼容构造函数集合

当前 `FrontedLayoutService` 仍有大量 public 构造函数：

* 默认构造；
* user store + logger；
* user store + registry + logger；
* user store + built-in root；
* user store + built-in root + registry；
* user store + package manager + registry；
* user store + root + package manager + registry；
* user store + package manager；
* user store + package manager + config factory。

其中：

* `_userLayoutStore` 只赋值，从不读取；

* `windowRegistry` 参数全部未使用；

* 注释仍写“解析插件默认布局描述符”；

* `_configFactory` 只是为了测试注入又增加一层构造函数；

* `PluginDefault`、`MissingOrError` 枚举值已经没有运行时来源。

这说明测试仍在驱动生产 API 保留旧结构，而不是测试真实生产架构。

## 必须收敛为

生产构造函数只保留：

```csharp
public FrontedLayoutService(
    IFrontedLayoutPackageManager packageManager,
    ILogger<FrontedLayoutService> logger)
```

空模板工厂可以：

* 变为静态 factory；
* 或作为一个正常 DI dependency；
* 或使用 internal test constructor。

不要保留八九个 public 兼容构造函数。

删除：

* `_userLayoutStore` 字段；
* LayoutService 对 `IFrontedUserLayoutStore` 的依赖；
* 无用 `IFrontedWindowRegistry` 参数；
* `GetIsolatedPackageRoot` 测试便利路径；
* `FrontedLayoutSource.PluginDefault`；
* `FrontedLayoutSource.MissingOrError`。

同步修正接口：

当前实现缺失时总能返回内存空模板，但接口仍声明返回 nullable config。应统一为非空：

```csharp
Task<FrontedWindowConfig> LoadWindowConfigAsync(...)
```

反序列化得到 JSON `null` 时应视为损坏，而不是返回 `Source=User/BuiltIn` 且 `Config=null`。

---

# P1-2：Designer 仍保留硬编码 fallback 架构

`FrontedDesignerLayoutCatalog` 当前仍有：

```csharp
public FrontedDesignerLayoutCatalog()
```

当 Registry 为 null 时调用：

```csharp
GetFallbackEntries()
```

并硬编码八个内置窗口，还给 v3 entry 填入旧 GUID。

这违反：

```text
Designer 只从 IFrontedWindowRegistry.GetV3LayoutWindows() 获取窗口
```

生产 DI 虽然使用正确构造函数，但测试中至少有多处直接：

```csharp
new FrontedDesignerLayoutCatalog()
```

这些测试测试的是 fallback，不是正式架构。

## 必须修改

删除：

* parameterless constructor；
* nullable Registry；
* `GetFallbackEntries()`；
* hardcoded built-in list；
* fallback 中的 legacy GUID。

唯一构造函数：

```csharp
public FrontedDesignerLayoutCatalog(
    IFrontedWindowRegistry windowRegistry)
```

所有测试使用真实 registration 集合创建 Registry。

`FrontedDesignerLayoutCatalogEntry` 当前：

```text
WindowTypeName = registration.Id
WindowId       = registration.Id
```

是同一个值保存两次。新内部模型应只保留一个：

```text
CanonicalWindowId
```

`.bpui` 对外字段名 `Window` 保持不变即可，不需要把旧 `WindowTypeName` 继续传播到内部。

---

# P1-3：XAML singleton 不能进入透明度“关闭后重建”链路

`AddFrontedWindow` 注册的是 singleton Window。

但 `RestartWindowForTransparencyChangeAsync` 对任意 `FrontedWindowRegistration` 都会：

1. 从缓存移除；
2. `Close()` 旧窗口；
3. 再次从 DI 解析窗口；
4. 尝试 `Show()`。

对 XAML singleton 来说，DI 会返回已经关闭过的同一 Window。WPF Window 关闭后不能再次显示。

虽然 Designer 当前只对 v3 调这条链，但公共服务契约仍允许错误调用。

## 必须修改

入口直接限制：

```csharp
if (registration is not FrontedV3LayoutWindowRegistration)
{
    return false;
}
```

不要为此引入新的 Window factory abstraction；当前最简单正确的规则就是：

```text
静默重建只支持宿主创建的 v3 Window
```

增加 XAML rejection 测试。

---

# P1-4：FrontManage 没有实现约定的三类来源

既定分组规则：

```text
IsBuiltIn == true       → BuiltIn
PackageId != null       → Plugin
其他非插件宿主注册       → External
```

当前 fallback 只有：

```csharp
registration.IsBuiltIn
    ? "BuiltIn"
    : "Plugin";
```

所以：

```csharp
AddFrontedV3LayoutWindow(
    "ExternalOverlay",
    isBuiltIn: false)
```

在非插件作用域中注册时，`PackageId == null`，却仍被显示为 Plugin。

另外 `KindDisplay` 混合了两种不同维度：

* built-in v3 显示 BuiltIn；
* plugin XAML 显示 XAML；
* plugin v3 显示 V3Layout。

来源和承载方式必须分开。

## 必须修改

来源分组：

```csharp
registration.IsBuiltIn
    ? BuiltIn
    : registration.PackageId is not null
        ? Plugin
        : External;
```

类型显示只根据：

```text
registration.Kind
```

输出：

```text
XAML
v3 Layout
```

同时删除完全重复的：

```text
WindowId
FullWindowType
```

只保留一个 Canonical ID 属性。

---

# P1-5：产品元数据又被硬编码回 Core

`FrontedWindowRegistration` 基类被加入了原计划之外的：

```text
GroupKey
DisplayOrder
I18nDisplayNames
```

随后又新增 `FrontedBuiltInWindowMetadata`，在 Core 内硬编码八个具体产品窗口、顺序和三语名称。该类注释甚至明确写着它替代旧 `GetBuiltInV3Windows()` 的硬编码清单。

这不是旧 descriptor 主架构复活，但仍属于把产品知识重新塞回 Core registration 层。

并且英文文本已有：

```text
Survivor Score in Gane Window
Hunter Score in Gane Window
```

拼写错误。

## 按“极致简洁”目标应修改

优先方案：

1. 删除 `FrontedBuiltInWindowMetadata`；
2. 从 registration 基类删除：

   * `GroupKey`
   * `DisplayOrder`
   * `I18nDisplayNames`
3. BuiltIn/Plugin/External 由 `IsBuiltIn + PackageId` 推导；
4. 顺序使用 DI registration order，或在 UI 按 `LocalId` 排序；
5. 内置显示名使用现有 resx：

   ```text
   Designer.Window.{LocalId}
   ```
6. 插件 XAML 使用 Attribute Name；
7. 插件 v3 默认使用 LocalId。

如果确实存在强业务顺序要求，也应在 App 层注册处声明，而不是在 Core static switch 中再次硬编码完整产品清单。

---

# P1-6：XAML 创建逻辑重复并掩盖错误 DI

`AddFrontedWindow` 已经：

* 注册 ViewModel singleton；
* 注册 Window singleton；
* 在 Window factory 中设置 DataContext。

但 WindowService 又：

* `GetService(windowType)`；
* 解析失败则 `ActivatorUtilities.CreateInstance`；
* 再次解析 ViewModel；
* 再次设置 DataContext。

这形成两套创建路径，并会掩盖 registration/DI 配置错误。

## 必须修改

XAML 创建只允许：

```csharp
_services.GetRequiredService(xaml.WindowType)
```

DataContext 只在 `AddFrontedWindow` 注册 factory 中设置一次。

完成后，如果 `ViewModelType` 没有其他实际消费者，则从 `FrontedXamlWindowRegistration` 删除它。

未知 registration 类型不应返回 null：

```csharp
_ => throw new InvalidOperationException(...)
```

系统已经明确只有两个 sealed 模型，不需要“第三种未知模式静默跳过”。

---

# P1-7：Local ID validator 允许纯空白字符串

当前 validator 使用：

```csharp
string.IsNullOrEmpty(localWindowId)
```

因此：

```text
"   "
```

会通过 Local ID validator。

之后 Registry 又通过 `IsNullOrWhiteSpace` 将其 warning 后跳过，造成：

```text
注册 API 调用成功
但窗口静默消失
```

## 必须修改

使用：

```csharp
string.IsNullOrWhiteSpace(localWindowId)
```

并在注册 API 入口直接抛出明确异常。

增加 whitespace 测试。

同时建议验证插件 manifest ID 是否能安全作为 canonical path segment；不能等到 LayoutService 访问路径时才报错。

---

# P2：收尾清理

以下不是 P0，但应在本轮一起完成，否则仍称不上“极致简洁”。

## 1. 路径反解析只有一个权威实现

`FrontedV3LayoutWindowPathHelper` 已有 canonical/path 规则，但 `FrontedLayoutPackageManager` 又手写了一套：

```text
ToCanonicalWindowIdFromLayoutRelativePath
```

应将 layout file path → canonical ID 放回 PathHelper，PackageManager 只调用 helper。

## 2. `FrontedWindowBase` 不应持有整个 registration

当前：

```csharp
InitializeV3LayoutHost(
    FrontedWindowRegistration registration,
    ...)
```

Renderer host 实际只需要：

```text
CanonicalWindowId
DisplayName
```

改成明确参数，避免 Registry/UI 元数据泄漏到渲染层。

## 3. WindowService 不应公开可变 Dictionary

接口应至少暴露：

```csharp
IReadOnlyDictionary<string, Window>
IReadOnlyDictionary<string, bool>
```

真实字典保持 private，防止 ViewModel 和测试破坏状态不变量。

## 4. 删除无用 dependency

`FrontedLayoutPackageExporter` 保存了：

```text
IFrontedWindowLayoutOptionsService
```

但当前实现没有使用。

`FrontedLayoutService._userLayoutStore` 同样完全未使用。

## 5. 删除测试专用生产 fallback

包括：

* `FrontedWindowRegistryService()` 空构造函数；
* `FrontedDesignerLayoutCatalog()` 空构造函数；
* LayoutService 的大量测试便利构造函数。

测试应适配生产架构，不应让生产代码适配测试。

## 6. 清理重复注册

`GameStopwatchEntry` 手工：

```csharp
services.AddSingleton<GameStopwatchWindowViewModel>();
```

随后 `AddFrontedWindow` 又注册一次相同 ViewModel。删除手工重复项。

---

# 文档必须同步修复

当前 `AGENTS.md` 仍写：

```text
built-in 前台窗口由 v3 descriptor
(GetBuiltInV3Windows()) 注册
```

但这套 API 已经删除。

以下文档之间也存在互相矛盾：

* 有的写插件窗口从插件安装目录加载默认 layout；
* 有的写不加载；
* 有的写 user package 缺失时回退 built-in；
* 有的写绝不回退；
* 有的仍描述 Registry descriptor、FullWindowType；
* PluginSdk README 又声明 XAML 使用原 GUID。

本轮必须统一到以下唯一规则：

```text
XAML:
Attribute GUID 是 runtime ID
PackageId 仅表示来源
不参与 v3 layout / Designer

v3 Built-in:
active package → built-in resource → empty template

v3 Plugin:
active package → empty template

Designer:
只读取 Registry 中的 v3 registrations

.bpui:
未知插件窗口和未知插件控件都可被保留
不因当前 Registry 缺失而删除

Canonical path:
BpWindow
    → FrontedLayouts/BpWindow.json

plugin:{PackageId}/{LocalId}
    → FrontedLayouts/plugin/{PackageId}/{LocalId}.json
```

重点更新：

```text
AGENTS.md
docs/frontend-windows-and-layout.md
docs/fronted-window-system-deep-dive.md
docs/fronted-layout-v3-window-centric.md
docs/fronted-designer-v3.md
docs/plugin-system.md
docs/bpui-package-v3.md
neo-bpsys-wpf.PluginSdk/README.md
```

注意：插件“控件 descriptor”是另一套仍然有效的 API，不要因为清理 window descriptor 文案而误删 plugin control descriptor。

---

# 必须补齐的最终测试矩阵

现有测试只覆盖精确重复 ID 和 `EnsureWindowCreated`，并不能证明完整计划成立。Registry 测试目前没有 case variant，SafeWindowOpen 也没有调用真实 Show 链路。

最终至少新增：

## Identity

```text
PluginXaml_UsesRawAttributeGuid
PluginXaml_PackageIdIsMetadataOnly
Xaml_RejectsNonGuidId
PluginV3_UsesNamespacedCanonicalId
BuiltInV3_UsesLocalId
DifferentPlugins_CanUseSameLocalId
CanonicalDuplicate_IsCaseInsensitive
WhitespaceLocalId_IsRejected
```

## Registry/runtime

```text
Registry_EmptyIdFailsFast
CaseVariantLookup_UsesSameRegistration
CaseVariantShowHide_UsesSameWindowAndState
ScoreWindow_ShowDispatchesThreeWindows
ScoreWindow_HideDispatchesThreeWindows
ShowWindow_ExceptionIsContained
AllWindowShow_ExceptionIsContainedAndContinues
XamlTransparencyRestart_IsRejected
```

## Designer

```text
DesignerCatalog_RequiresRegistry
DesignerCatalog_ContainsOnlyV3Registrations
DesignerCatalog_DoesNotContainXaml
DesignerCatalog_HasNoHardcodedFallback
PluginV3_UsesCanonicalId
```

## `.bpui`

```text
UnknownPluginWindow_ImportSucceeds
UnknownPluginWindow_ImportExportPreservesEntry
UnknownPluginWindow_ImportExportPreservesPath
UnknownPluginWindow_ImportExportPreservesBehavior
UnknownLayoutJsonField_IsPreserved
Import_DoesNotRewriteLayoutJson
Exporter_DoesNotSynthesizeUnsavedEmptyLayouts
ManifestWindowAndPathMismatch_IsRejected
```

## FrontManage

```text
BuiltInRegistration_GoesToBuiltInGroup
PluginRegistration_GoesToPluginGroup
HostNonBuiltInRegistration_GoesToExternalGroup
KindDisplay_IsIndependentFromSourceGroup
```

---

# 禁止的修复方式

本轮不要再通过以下方式让测试通过：

* 不新增 facade、adapter 或 Obsolete shim；
* 不保留旧构造函数只为了测试；
* 不增加第二套 Registry；
* 不增加 Designer fallback window list；
* 不把 XAML GUID 再映射成 v3 canonical ID；
* 不从插件安装目录恢复 v3 默认布局；
* 不在 Importer 中重写普通 v3 layout；
* 不通过忽略未知 `.bpui` entry 规避 round-trip；
* 不为 XAML Window 引入复杂 factory 生命周期；
* 不修改 `.bpui` schema、字段名、FormatVersion 或 LayoutSchemaVersion；
* 不处理本次范围外的 OCR、i18n audit、SmartBp native dependency、behavior runtime 和 Web Renderer 既有失败。

---

# 最终验收条件

完成修正后，应满足：

```text
1. XAML 与 v3 身份链完全分离；
2. XAML 插件继续使用历史 GUID 打开；
3. PackageId 对 XAML 只作为来源元数据；
4. 所有 runtime cache 始终使用 Registry 返回的 Canonical ID；
5. ID 比较和 Windows 路径语义一致；
6. Designer 没有硬编码 fallback；
7. LayoutService 没有无用 compatibility constructors；
8. ScoreWindow 组合行为恢复；
9. Window 打开链没有 async void；
10. 未安装插件窗口可在 .bpui import/export 中完整保留；
11. Importer 不重写普通 v3 layout；
12. 文档、PluginSdk 和 AGENTS.md 使用同一套规则。
```

修复完成后的汇报必须包含：

```text
- 修改文件列表；
- 每个 P0/P1 对应的具体修复；
- 删除的构造函数、fallback 和死字段；
- 新增测试名称；
- 全仓旧 window descriptor/contributor 关键词扫描；
- .bpui unknown-plugin round-trip 测试结果；
- Build 结果；
- 与本次无关的既有失败列表。
```
