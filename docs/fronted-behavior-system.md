# Fronted Behavior Graph System — Phase 0 源码勘察报告

> **状态**: Phase 0 完成，待 Phase 1 实施
> **功能主线**: 把 Designer v3 从静态前台布局编辑器升级成"事件驱动的控件动画/行为编排系统"
> **本报告仅做源码勘察，不包含实现代码**

## Phase 1 implemented

Phase 1 已完成行为系统的数据基础：

- `FrontedControlConfigBase` 增加 `BehaviorGuid`，作为行为系统内部控件标识；普通 PropertyGrid 不显示该字段。
- Add Control 创建内置控件和插件控件时都会生成新的 `BehaviorGuid`；复制/粘贴控件会重新生成，重命名控件不影响该值。
- 删除控件时会通过 `IFrontedBehaviorService.RemoveBehaviors(Guid)` 调用清理入口；当前实现为 no-op，占位给后续 behaviors 持久化使用。
- Core 新增 `Models/FrontedLayout/Behaviors/` 纯数据模型，覆盖行为文档、控件行为集合、触发器、过滤器、节点图、连接和循环策略。
- 已添加 BehaviorGuid JSON/PropertyGrid/复制/删除测试，以及行为模型默认值和 JSON roundtrip 测试。

## Phase 2 implemented

Phase 2 已完成 Designer 侧行为面板和触发器编辑能力：

- `IFrontedBehaviorService` 扩展为行为文档读写服务，`FrontedBehaviorService` 会按当前激活布局包读写 `behaviors/{WindowType}/{CanvasName}.behaviors.json`。行为数据仍独立于控件 config 和 `FrontedCanvasConfig`。
- Designer v3 右侧属性区新增可折叠的“动画 / 行为”面板。选中控件后可以添加 OneShot / Loop 行为，重命名、启用/禁用、复制和删除行为。
- 当旧布局控件的 `BehaviorGuid == Guid.Empty` 且用户第一次添加行为时，编辑器会按需生成新的 `BehaviorGuid` 并标记 layout dirty；仅切换选中控件不会生成 Guid。
- OneShot 行为可编辑 `Trigger`；Loop 行为可编辑 `StartTrigger`、`EndTrigger` 和 `LoopPolicy`。触发器编辑器使用事件 payload 参数、可读运算符和文本值组成规则；内部 `Source` 与兼容字段 `RightValueKind` 不在正常 UI 中显示。
- UI 提供动画编辑器入口；OneShot 显示单图占位，Loop 通过 Top LocalTabs 切换 `StartGraph` / `LoopGraph` / `StopGraph` 占位摘要，并明确提示节点图编辑器将在 Phase 3 提供。
- Designer VM 单独跟踪 `AreBehaviorsDirty`；保存操作会同时处理 layout dirty 和 behaviors dirty。删除控件时会删除该控件自身的 `ControlBehaviorSet`。
- `FrontedBehaviorEventCatalog` 从显式标注的 `ISharedDataService` 语义事件反射并缓存事件元数据与常用 payload 过滤字段。
- 已添加行为面板 ViewModel、行为文档持久化、事件目录和轻量 Designer 集成测试。

Phase 2 仍不实现：可视化节点图编辑器、真实事件总线、动画 runtime、WPF 动画执行、插件节点执行、Timeline 编辑器或前台窗口行为播放。这些仍属于 Phase 3+。

## Phase 2 UX / event catalog update

### Attribute-driven event catalog

行为编辑器不再维护硬编码事件列表。`ISharedDataService` 中只有显式标记
`FrontedBehaviorEventAttribute` 的事件会进入 `FrontedBehaviorEventCatalog`；未标记事件不会暴露。
`FrontedBehaviorEventPayloadAttribute` 描述可用于过滤器的 payload 路径、显示名、类型和未来 runtime
取值来源（服务属性、事件参数属性等）。目录通过反射构建一次并缓存，按分类、顺序和显示名稳定排序。

事件名、分类名和 payload 参数名都通过本地化 key 展示；行为 JSON 仍保存稳定的原始
`EventType` 与 `Event.*` 路径。新增共享数据事件时，应先判断它是否具有前台动画语义，再决定是否标注，
不要盲目把所有服务事件加入目录。

### Filter rule builder

过滤器 UI 使用面向用户的规则行：`当 [参数] [运算符] [文本值]`。左侧参数来自当前事件的 payload
下拉框，运算符显示为 `=`、`>`、`<`、`≥`、`≤`、`包含`、`不包含` 等可读符号/文本，右侧始终是普通文本。
`Source` 和兼容旧 Phase 2 JSON 的 `RightValueKind` 不在正常 UI 中显示。

未来 runtime 会将左值通过 `ToString()` 转为文本。等于和包含比较忽略大小写；大小比较在两侧都能按
Invariant Culture 解析为 decimal 时使用数值比较，否则回退为 ordinal 文本比较。一个 Trigger 的所有
过滤条件必须全部通过；任意条件失败都跳过动画。切换事件时不会静默删除旧过滤条件，找不到的路径会作为
“未知参数”保留并显示。

### Messenger policy

行为编辑器目录不直接暴露 Messenger message。行为系统消费具有前台语义的 `FrontedBehaviorEvent`；
未来可以通过 adapter 将 Messenger message 包装为语义事件。这样可以把 UI/MVVM 基础设施消息与前台行为
语义分开。未来 adapter 可以在 message 类型或 adapter 方法上复用相同的事件元数据属性，但本阶段只处理
`ISharedDataService` 的反射目录，不实现 Messenger adapter。

### Animation editor placeholder and scrolling

行为卡片提供“打开动画编辑器”入口。OneShot 显示单个图占位；Loop 使用 Top NavigationView /
`LocalTabs` 在开始动画、循环动画、结束动画之间切换，并显示当前节点/连线数量。真实节点图编辑和动画
runtime 仍属于 Phase 3。

BehaviorPanel 必须让 Designer 右侧的外层 ScrollViewer 负责滚动。行为列表和过滤器列表使用
`ItemsControl` + `Expander`/卡片，不在面板内部使用 `ListBox`、`ListView` 或额外 ScrollViewer，避免嵌套滚动。

---

## 目录索引

- [1. 关键源码地图](#1-关键源码地图)
- [2. 当前 Designer v3 数据流](#2-当前-designer-v3-数据流)
- [3. BehaviorGuid 接入点](#3-behaviorguid-接入点)
- [4. behaviors 文件接入点](#4-behaviors-文件接入点)
- [5. 行为列表 UI 接入点](#5-行为列表-ui-接入点)
- [6. 事件总线候选来源](#6-事件总线候选来源)
- [7. Phase 1 建议实施步骤](#7-phase-1-建议实施步骤)
- [8. Phase 1 建议测试清单](#8-phase-1-建议测试清单)
- [9. 不建议 Phase 1 做的事情](#9-不建议-phase-1-做的事情)
- [10. 开放问题](#10-开放问题)

---

## 1. 关键源码地图

| 文件路径 | 作用 | 和 Behavior 系统的关系 | 建议改动阶段 |
| --- | --- | --- | --- |
| `Core/Models/FrontedLayout/FrontedControlConfigBase.cs` | 所有控件配置基类 | **加 `BehaviorGuid` 的目标基类** | Phase 1 |
| `Core/Models/FrontedLayout/Designer/FrontedControlDesignItem.cs` | 设计时控件项包装 | 持有 Config 引用；编辑器选中状态在此 | Phase 1 |
| `Core/Models/FrontedLayout/Designer/FrontedCanvasDesignDocument.cs` | 单 Canvas 设计文档 | IsDirty、Controls 集合 | Phase 1 |
| `ViewModels/Windows/FrontedDesignerWindowViewModel.cs` | Designer 主 VM（4945 行） | **AddControl/Paste/Delete 入口都在这里** | Phase 1 |
| `Views/Windows/FrontedDesignerWindow.xaml` | Designer 窗口布局 | 右侧属性面板结构（Row 2 = PropertyGrid） | Phase 2 |
| `Views/Windows/FrontedDesignerWindow.xaml.cs` | Designer code-behind（3081 行） | 预览元素注册、对话框、交互辅助 | Phase 1-2 |
| `Core/Services/FrontedLayout/FrontedControlDefaultConfigFactory.cs` | 默认控件工厂 | 新建控件时确定 BehaviorGuid | Phase 1 |
| `Core/Services/FrontedLayout/FrontedControlNameGenerator.cs` | 控件名称生成器 | 不相关（Guid ≠ Name） | 无关 |
| `Core/Services/FrontedLayout/FrontedLayoutDesignConverter.cs` | Config ↔ DesignDocument 转换 | 转换时需保留 BehaviorGuid | Phase 1 |
| `Core/Models/FrontedLayout/Json/FrontedCanvasConfigJsonConverter.cs` | JSON 反/序列化转换器 | 读/写时需透传 BehaviorGuid | Phase 1 |
| `Core/Services/FrontedLayout/FrontedRenderer.cs` | 前台运行时渲染 | **AnimationTargetResolver 应接在这里** | Phase 3+ |
| `Core/Services/FrontedLayout/FrontedRendererProperties.cs` | 附加属性（IsGeneratedControl/RegisteredName） | RegisteredName → FrameworkElement 映射 | Phase 3+ |
| `Core/Abstractions/Services/IFrontedControl.cs` | 控件工厂接口 | Create() 返回 FrameworkElement | Phase 3+ |
| `Core/Services/FrontedLayout/FrontedControlRegistry.cs` | 控件注册表 | 运行时按 ControlType 查找工厂 | Phase 3+ |
| `Core/Services/FrontedLayout/FrontedPropertyGridBuilder.cs` | PropertyGrid 构造器 | **需跳过 BehaviorGuid**（不显示给用户） | Phase 1 |
| `Services/SharedDataService.cs` | 共享数据服务（事件核心） | 事件总线候选来源 | Phase 3+ |
| `Core/Abstractions/Services/ISharedDataService.cs` | 共享数据接口 | 事件声明（12 个事件） | Phase 3+ |
| `Services/CharacterSelectionService.cs` | 角色选择服务 | CharacterSelected/CharacterBanned 事件 | Phase 3+ |
| `Core/Services/FrontedLayout/FrontedLayoutPackageExporter.cs` | bpui 导出器 | behaviors 文件导出点 | Phase 3 |
| `Core/Services/FrontedLayout/FrontedLayoutPackageImporter.cs` | bpui 导入器 | behaviors 文件导入点 | Phase 3 |
| `Core/Services/FrontedLayout/FrontedLayoutPackageManager.cs` | 包管理器 | 删除/复制包的 behaviors 联动 | Phase 3 |
| `Core/Models/FrontedLayout/PackageModels/FrontedLayoutPackageManifest.cs` | manifest 模型 | 可能需要 HasBehaviors/RequiredNodePlugins 字段 | Phase 3 |
| `Tests/Models/FrontedLayoutDesignerFoundationTest.cs` | Designer 核心测试（5103 行） | 新增测试加在此处 | Phase 1 |
| `Tests/Models/FrontedCanvasConfigTest.cs` | Canvas JSON 往返测试（2728 行） | 新增 BehaviorGuid JSON 透传测试 | Phase 1 |
| `Tests/Services/FrontedLayoutPackageManagerTest.cs` | bpui 导入导出测试（1660 行） | 新增 behaviors 文件导入导出测试 | Phase 3 |

---

## 2. 当前 Designer v3 数据流

### 2.1 布局如何加载

1. `FrontedDesignerWindowViewModel.ReloadLayoutCoreAsync()` → `_layoutService.LoadCanvasConfigWithMetadataAsync(windowType, canvasName)`
2. `FrontedLayoutService` 从用户布局路径 → 内置默认路径读取 JSON
3. `FrontedCanvasConfigJsonConverter.Read()` 反序列化为 [`FrontedCanvasConfig`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf.Core/Models/FrontedLayout/FrontedCanvasConfig.cs)（`Controls` 是 `Dictionary<string, FrontedControlConfigBase>`）
4. [`FrontedLayoutDesignConverter.FromConfig()`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedLayoutDesignConverter.cs) 转换为 [`FrontedCanvasDesignDocument`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf.Core/Models/FrontedLayout/Designer/FrontedCanvasDesignDocument.cs)（`Controls` 是 `ObservableCollection<FrontedControlDesignItem>`）
5. 设置 `CurrentDocument.IsDirty = false`

### 2.2 控件如何进入文档

- **新建**: `AddControl` 命令 → [`FrontedControlDefaultConfigFactory.Create()`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedControlDefaultConfigFactory.cs) → 创建 config + 生成 Name → 包装为 `FrontedControlDesignItem` → 加入 `CurrentDocument.Controls` → `IsDirty = true`
- **粘贴**: `PasteControl` 命令 → `FrontedDesignerClipboardPayload.CreateConfig()` 深拷贝 config → Left/Top +10, ZIndex max+1 → 生成新 Name → 包装为 `FrontedControlDesignItem` → 加入集合
- **从 JSON 加载**: 由 `FrontedLayoutDesignConverter.FromConfig()` 批量创建

### 2.3 选中控件如何维护

- [`SelectedDesignItem`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf/ViewModels/Windows/FrontedDesignerWindowViewModel.cs#L329) 属性（`[ObservableProperty]`）
- `OnSelectedDesignItemChanged` 分部方法同步 `IsSelected` 状态、刷新 PropertyGrid、更新预览选中框
- 左侧 Layer Panel 的 `IsSelected` 绑定到 `DesignerLayerNode.IsSelected` → 关联 `ControlItem.IsSelected`

### 2.4 控件如何保存

1. `SaveCurrentLayoutAsync()` → `_designConverter.ToConfig(CurrentDocument)` → [`FrontedCanvasConfig`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf.Core/Models/FrontedLayout/FrontedCanvasConfig.cs)（`Controls` 变回 `Dictionary<string, FrontedControlConfigBase>`，key = Name）
2. 设置 `config.Version = 3` → `_layoutService.SaveCanvasConfigAsync()`
3. 通过 `FrontedCanvasConfigJsonConverter.Write()` 序列化为 JSON → 写入磁盘
4. `IsDirty = false`

### 2.5 控件如何导出到 bpui

1. `FrontedLayoutPackageExporter.ExportLayoutsAsync()` → 加载每个窗口/Canvas 配置
2. 写入 staging 目录 `layouts/{WindowType}/{CanvasName}.json`
3. 资源文件（图片）复制到包内 `resources/` 并重写 URI
4. 打包为 `.bpui` (zip)

---

## 3. BehaviorGuid 接入点

### 3.1 应该改哪个基类

**[`FrontedControlConfigBase`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf.Core/Models/FrontedLayout/FrontedControlConfigBase.cs)** 是唯一正确的位置。原因：

- 所有 13 个内置控件 + 插件控件都继承自此基类
- JSON 序列化器 `Write()` 使用 `JsonSerializer.Serialize(writer, control, control.GetType())`，基类属性自动写入所有子类
- JSON 反序列化器 `ReadControl()` 按 ControlType 分派后，基类属性由 `JsonSerializer.Deserialize(ref reader, ...)` 自动反填充
- PropertyGridBuilder 需要主动跳过该属性

```csharp
// 建议新增字段
/// <summary>
/// 行为系统内部使用的控件标识符。用户不可编辑。
/// PropertyGrid 不应显示此字段。重命名控件时不改变此值。
/// 复制控件时直接重新生成新 Guid。
/// </summary>
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
public Guid BehaviorGuid { get; set; }
```

**`JsonIgnoreCondition.WhenWritingDefault`** 保证默认值 `Guid.Empty` 不会被序列化到旧 JSON 中，实现向前兼容。

### 3.2 新建控件在哪里生成

在 `AddControl` 方法中创建 config 之后，应在 [`FrontedControlDefaultConfigFactory.Create()`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedControlDefaultConfigFactory.cs) 内部直接设置 `config.BehaviorGuid = Guid.NewGuid()`。这样所有通过工厂创建的控件默认就有 BehaviorGuid。

反序列化得到的 config 中 `BehaviorGuid == Guid.Empty` 是合法的（旧布局没有此字段），不应影响运行时渲染。

### 3.3 复制控件在哪里重新生成

在 [`PasteControl`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf/ViewModels/Windows/FrontedDesignerWindowViewModel.cs#L1193) 中，`copiedControl.CreateConfig()` 之后立即覆盖：

```csharp
var config = copiedControl.CreateConfig();
config.BehaviorGuid = Guid.NewGuid();  // 重新生成
```

**注意**: `CopyBo5ToBo3` 使用的 `CloneControls` 不应重置 Guid — 同一控件的不同 BO 状态应共享同一 BehaviorGuid。

### 3.4 删除控件在哪里清理 behavior

在 [`DeleteSelectedControl`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf/ViewModels/Windows/FrontedDesignerWindowViewModel.cs#L1251) 中，从 `CurrentDocument.Controls.Remove` 之后追加：

```csharp
if (selectedItem.Config.BehaviorGuid != Guid.Empty)
    _behaviorService.RemoveBehaviors(selectedItem.Config.BehaviorGuid);
```

Phase 1 中 `_behaviorService` 可以是空实现（NoopBehaviorService），清理入口只做预留调用。

### 3.5 重命名为什么不影响 BehaviorGuid

重命名只更改 `FrontedControlDesignItem.Name` 属性，本质上是 `FrontedCanvasConfig.Controls` 字典的 key 变更。BehaviorGuid 在 Config 内部，与 Name 完全独立。保存时 `ToConfig` 将 Name 写回字典 key，Config 序列化时 BehaviorGuid 自然跟随着控件属性 JSON 输出。

---

## 4. behaviors 文件接入点

### 4.1 建议文件路径

```
behaviors/{WindowType}/{CanvasName}.behaviors.json
```

镜像 `layouts/` 的目录结构，放在独立的 `behaviors/` 根目录下。在 bpui 包内的路径为 `behaviors/BpWindow/BaseCanvas.behaviors.json`。

### 4.2 保存位置

```
%APPDATA%/neo-bpsys-wpf/FrontedLayoutPackages/{PackageId}/behaviors/{WindowType}/{CanvasName}.behaviors.json
```

### 4.3 导入位置

在 [`FrontedLayoutPackageImporter.ImportAsync()`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedLayoutPackageImporter.cs) 中，解压 staging 目录后检查 `behaviors/` 目录是否存在，若存在则将整个目录复制到安装路径。

### 4.4 导出位置

在 [`FrontedLayoutPackageExporter.ExportLayoutsAsync()`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedLayoutPackageExporter.cs) 之后追加：读取当前包下的 behaviors 文件，若存在则复制到 staging 的 `behaviors/` 目录。

### 4.5 和 manifest 的关系

[`FrontedLayoutPackageManifest`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf.Core/Models/FrontedLayout/PackageModels/FrontedLayoutPackageManifest.cs) 建议增加两个可选字段：

```json
{
  "hasBehaviors": true,
  "requiredNodePlugins": ["pluginId1", "pluginId2"]
}
```

- `hasBehaviors`: 标记包是否包含行为数据
- `requiredNodePlugins`: 行为节点图依赖的插件节点包

---

## 5. 行为列表 UI 接入点

### 5.1 当前右侧面板结构

[`FrontedDesignerWindow.xaml`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf/Views/Windows/FrontedDesignerWindow.xaml) 右侧面板（Grid.Column="3"）当前结构：

| 行 | 内容 |
| --- | --- |
| Row 0 | 选中控件摘要信息（Name, Type, Geometry, RuntimeCritical, Validation） |
| Row 1 | Polygon 顶点编辑器（条件显示） |
| Row 2 | **Property Grid**（ItemsControl\<FrontedPropertyEditorItem\>） |
| Row 3 | Canvas Properties（Expander） |
| Row 4 | Window Options（Expander） |

没有 tab 机制，所有内容垂直排列在 ScrollViewer 中。

### 5.2 最适合新增的 View / ViewModel

在 Property Grid（Row 2）下方新增一个 **Expander**，名为"Behaviors / 动画/行为"：

```
Row 2:   Property Grid（现有）
Row 2.5: Behaviors Panel（新增 Expander，可折叠）
           ├── 行为列表（ListView/ItemsControl）
           │     ├── 行为类型图标（OneShot/Loop）
           │     ├── 行为名称/摘要
           │     ├── 触发事件名
           │     └── 删除按钮
           ├── "添加单次行为" 按钮
           ├── "添加循环行为" 按钮
           └── （选中行为时展开详细编辑区）
Row 3:   Canvas Properties（现有）
Row 4:   Window Options（现有）
```

**ViewModel**: 建议新建独立的 `BehaviorPanelViewModel`，在 [`FrontedDesignerWindowViewModel`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf/ViewModels/Windows/FrontedDesignerWindowViewModel.cs) 中持有其实例，避免 VM 进一步膨胀（已 4945 行）。

### 5.3 需要哪些 commands

| 阶段 | Command | 说明 |
| --- | --- | --- |
| Phase 1 | `AddOneShotCommand` | 为选中控件添加空 OneShot 行为 |
| Phase 1 | `AddLoopCommand` | 为选中控件添加空 Loop 行为 |
| Phase 1 | `DeleteBehaviorCommand` | 删除选中的行为 |
| Phase 1 | `SelectBehaviorCommand` | 选中某项行为以显示编辑区域 |
| Phase 2 | `EditTriggerCommand` | 打开事件选择器 |
| Phase 2 | `EditFilterCommand` | 打开事件过滤器编辑 |
| Phase 3+ | `OpenNodeGraphCommand` | 打开节点图编辑器 |

### 5.4 Dirty tracking 如何接

建议在 `FrontedDesignerWindowViewModel` 中新增独立属性 `AreBehaviorsDirty: bool`，与 `CurrentDocument.IsDirty` 分开跟踪。保存按钮同时检查两者。这样可以独立保存 layout vs behaviors，避免把 behavior 数据和 layout 数据耦合。

---

## 6. 事件总线候选来源

### 6.1 SharedDataService 当前事件（[ISharedDataService](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf.Core/Abstractions/Services/ISharedDataService.cs)）

| 事件名 | Payload 应包含 | 已定参数 |
| --- | --- | --- |
| `CurrentGameChanged` | Game 实例 | `EventHandler` |
| `PickedMapChanged` | 地图名 | `EventHandler` |
| `MapV2BannedChanged` | 地图名 + 禁用状态 | `EventHandler` |
| `IsBo3ModeChanged` | bool | `EventHandler` |
| `TeamSwapped` | 无额外数据 | `EventHandler` |
| `GlobalScoreTotalMarginChanged` | double | `EventHandler` |
| `IsTraitVisibleChanged` | bool | `EventHandler` |
| `CountDownValueChanged` | 剩余秒数字符串 | `EventHandler` |
| `BanCountChanged` | BanListName + Index | `BanCountChangedEventArgs` |

### 6.2 CharacterSelectionService 事件（[CharacterSelectionService](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf/Services/CharacterSelectionService.cs)）

| 事件名 | Payload | 已定参数 |
| --- | --- | --- |
| `CharacterSelected` | Camp + PlayerIndex | `CharacterSelectedEventArgs` |
| `CharacterBanned` | Camp + PlayerIndex | `CharacterBannedEventArgs` |

### 6.3 需要新增语义事件的事件

| 缺失事件 | 来源 | 说明 |
| --- | --- | --- |
| `FrontedWindowShown` / `FrontedWindowHidden` | `FrontedWindowService` | 前台窗口生命周期，当前没有事件 |
| `MatchScoreChanged` | `Game.MatchScore` | 比分变化，当前通过 `INotifyPropertyChanged` 级联传播 |
| 布局重新渲染完成 | `FrontedWindowService.ReloadFrontedLayoutsAsync()` | 渲染完成通知 |

**关于 IFrontedEventBus**: Phase 1 不需要真正的 EventBus 实现。Phase 3+ 时建议用适配器模式包装现有 `ISharedDataService` 和 `CharacterSelectionService` 的事件，统一转换为强类型 `FrontedEvent`。

---

## 7. Phase 1 建议实施步骤

### 步骤 1: `FrontedControlConfigBase` 加 `BehaviorGuid`

在基类中新增 `Guid BehaviorGuid { get; set; }`，注明 `JsonIgnoreCondition.WhenWritingDefault`。验证新建布局 JSON 序列化时 `Guid.Empty` 不出现。

### 步骤 2: 默认工厂中自动生成 BehaviorGuid

在 [`FrontedControlDefaultConfigFactory.Create()`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedControlDefaultConfigFactory.cs) 末尾设置 `config.BehaviorGuid = Guid.NewGuid()`。验证 `AddControl` 创建的控件 Guid ≠ Empty。

### 步骤 3: 复制控件时重新生成 BehaviorGuid

在 [`PasteControl()`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf/ViewModels/Windows/FrontedDesignerWindowViewModel.cs#L1193) 中 `CreateConfig()` 后追加 `config.BehaviorGuid = Guid.NewGuid()`。验证粘贴的控件与源控件 Guid 不同。

### 步骤 4: PropertyGrid 排除 BehaviorGuid

在 [`FrontedPropertyGridBuilder.AddConfigRows()`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf.Core/Services/FrontedLayout/FrontedPropertyGridBuilder.cs) 中跳过名为 `BehaviorGuid` 的属性。验证选中任何控件，右侧面板不出现 BehaviorGuid 行。

### 步骤 5: 删除控件时清理入口

在 [`DeleteSelectedControl`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf/ViewModels/Windows/FrontedDesignerWindowViewModel.cs#L1251) 中添加 `_behaviorService.RemoveBehaviors(guid)` 的预留调用。Phase 1 中 `_behaviorService` 可以是 NoopBehaviorService。

### 步骤 6: BehaviorGuid JSON 往返验证

在 [`FrontedCanvasConfigTest.cs`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/neo-bpsys-wpf.Tests/Models/FrontedCanvasConfigTest.cs) 中新增测试：序列化含 BehaviorGuid 的 config → 反序列化 → Guid 值一致；旧 JSON（无 Guid 字段）反序列化后 `BehaviorGuid == Guid.Empty`。

### 步骤 7: 核心模型类定义（纯数据类，不含序列化/持久化）

在 `Core/Models/FrontedLayout/Behaviors/` 目录下定义：

- `FrontedBehaviorDocument` — 行为文档根
- `ControlBehaviorSet` — 单控件的行为集合，以 BehaviorGuid 为 key
- `FrontedBehavior` — 单个行为（OneShot 或 Loop）
- `FrontedBehaviorKind` — 枚举：OneShot, Loop
- `TriggerDescriptor` — 触发事件描述（事件名 + 来源）
- `TriggerFilter` — 事件过滤器（条件表达式等占位）
- `FrontedNodeGraph` — 节点图容器
- `FrontedNode` — 单个节点
- `FrontedNodeConnection` — 节点连线
- `LoopPolicy` — 循环策略（次数/持续时间等）
- `ReentryPolicy` — 重入策略
- `FillBehavior` — 填充行为

**注意**: 只定义模型，不实现节点图执行、不实现持久化。

### 步骤 8: 模型层单元测试

- 模型默认值、属性设置、简单 JSON 序列化/反序列化
- `ControlBehaviorSet` 与 `BehaviorGuid` 的关联查找

### 步骤 9: 更新 Designer 设计文档

- 更新 [`docs/fronted-designer-v3.md`](file:///e:/_PersonalStuff/ASG/bpsys/neo-bpsys-wpf/docs/fronted-designer-v3.md) 增加 BehaviorGuid 和行为系统章节
- 本报告即 Phase 0 勘察记录，作为交接参考

---

## 8. Phase 1 建议测试清单

| # | 测试名称 | 目的 | 测试文件 |
| --- | --- | --- | --- |
| 1 | `BehaviorGuid_NewControlViaFactory_HasNonEmptyGuid` | 工厂创建一定有 Guid | `FrontedLayoutDesignerFoundationTest.cs` |
| 2 | `BehaviorGuid_CloneConfigViaClipboard_GuidIsNew` | 粘贴时 Guid 重新生成 | `FrontedLayoutDesignerFoundationTest.cs` |
| 3 | `BehaviorGuid_PropertyGridDoesNotShowGuidRow` | PropertyGrid 跳过 Guid | `FrontedLayoutDesignerFoundationTest.cs` |
| 4 | `BehaviorGuid_JsonRoundTrip_PreservesGuid` | JSON 往返保持 Guid | `FrontedCanvasConfigTest.cs` |
| 5 | `BehaviorGuid_OldJsonMissingGuid_DeserializesAsEmpty` | 旧 JSON 兼容 | `FrontedCanvasConfigTest.cs` |
| 6 | `BehaviorGuid_NewJsonWithEmptyGuid_NotSerialized` | 空 Guid 不写入 JSON | `FrontedCanvasConfigTest.cs` |
| 7 | `ControlBehaviorSet_LookupByGuid_ReturnsCorrectSet` | 查 BehaviorGuid 对应集合 | 新增 Behaviors/ 测试 |
| 8 | `FrontedBehavior_OneShotDefaults_AreSane` | OneShot 模型默认值 | 新增 Behaviors/ 测试 |
| 9 | `FrontedBehavior_LoopDefaults_AreSane` | Loop 模型默认值 | 新增 Behaviors/ 测试 |
| 10 | `FrontedNodeGraph_EmptyGraph_IsValid` | 节点图模型基础验证 | 新增 Behaviors/ 测试 |
| 11 | `DeleteControl_TriggersBehaviorCleanupCall` | 删除时清理入口被调用 | `FrontedLayoutDesignerFoundationTest.cs` |

---

## 9. 不建议 Phase 1 做的事情

| 事项 | 原因 |
| --- | --- |
| **节点图 UI 编辑器** | 需要完整 Graph Canvas + 节点拖放 + 连线，至少 Phase 2 |
| **真实动画 runtime** | 需要集成 WPF 动画引擎，涉及性能调试 |  |
| **真实事件总线** | 强行抽象 EventBus 可能过度设计；用现有事件机制即可 |  |
| **插件节点系统** | 节点图引擎未成型时抽象插件接口会反复修改 |  |
| **Timeline 编辑器** | Timeline 是 Loop 行为的可视化增强，非 MVP 必需品 |  |
| **复杂 debugger/可视化** | 运行时的调试工具代价高，初期用日志 + 诊断即可 |  |
| **behaviors 文件导入导出** | 依赖 Phase 1 的模型定义和 Phase 2 的 UI 编辑能力 |  |
| **behaviors 文件实际持久化** | Phase 1 只定义模型和 BehaviorGuid 基础设施，不写入磁盘 |  |
| **manifest 扩展** | 等 behaviors 文件导入导出再一起改 manifest 格式 |  |
| **旧系统兼容迁移** | Designer v3 没有旧兼容需求，不要设计迁移方案 |  |
| **把行为数据塞进控件 config** | 已定版，行为数据独立为 behaviors 文件 |  |
| **重构 VM 或已有服务** | 不要借行为系统之名重构现有代码 |  |

---

## 10. 开放问题

| # | 问题 | 建议 |
| --- | --- | --- |
| Q1 | `BehaviorGuid` 用 `Guid.NewGuid()` 还是 `Guid.CreateVersion7()`？ | .NET 9 支持 `Guid.CreateVersion7()`，推荐使用。有序 Guid 在调试和日志中更方便。 |
| Q2 | 序列化策略用 `[JsonInclude]` 还是 `[JsonIgnore(Condition)]`？ | 推荐 `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]`。 |
| Q3 | 内置默认布局 JSON 是否需要补充 BehaviorGuid？ | **不需要**。默认 JSON 反序列化后 Guid 为空，不影响运行时渲染。用户通过 Designer 添加时才生成。 |
| Q4 | BehaviorGuid 是否要暴露给插件？ | 建议插件开发者能读取但不能修改。在 `IFrontedControl.Create()` 的参数中传递 Guid。 |
| Q5 | `NoopBehaviorService` 是否在 Phase 1 实现？ | 建议实现 `IFrontedBehaviorService` + `NoopFrontedBehaviorService`，DI 注册为 Singleton。 |
| Q6 | BO3/BO5 状态的 behavior 数据是否需要独立？ | 建议和 layout 保持一致：如果 BO3 状态有独立控件副本，behavior 也应独立。 |
| Q7 | 是否需要 behaviors 文件脏追踪单独保存？ | 建议新增 `AreBehaviorsDirty` 属性，与 `IsDirty` 分开跟踪。 |
| Q8 | 测试是否应依赖具体文件系统？ | Phase 1 所有测试应是纯内存模型测试，不涉及文件 I/O。 |
| Q9 | `DesignerPreviewSharedDataService` 是否需要触发行为事件？ | 当前是隔离的 preview 数据源，不触发真实事件。Phase 1 不需要改。 |
| Q10 | 建议未来行为文件命名？ | `behaviors/{WindowType}/{CanvasName}.behaviors.json` |

---

## 附录：建议核心模型一览

```
FrontedBehaviorDocument
  └─ ControlBehaviorSet[]
       ├─ BehaviorGuid (→ FrontedControlConfigBase.BehaviorGuid)
       └─ FrontedBehavior[]
            ├─ BehaviorId: Guid
            ├─ Kind: FrontedBehaviorKind (OneShot | Loop)
            ├─ OneShot:
            │    ├─ Trigger: TriggerDescriptor
            │    └─ Graph: FrontedNodeGraph
            └─ Loop:
                 ├─ StartTrigger: TriggerDescriptor
                 ├─ StartFilter: TriggerFilter
                 ├─ StartGraph: FrontedNodeGraph
                 ├─ LoopGraph: FrontedNodeGraph
                 ├─ EndTrigger: TriggerDescriptor
                 ├─ EndFilter: TriggerFilter
                 ├─ EndGraph: FrontedNodeGraph
                 └─ LoopPolicy: LoopPolicy

TriggerDescriptor
  ├─ EventName: string
  ├─ Source: string (如 "SharedDataService", "CharacterSelectionService")
  └─ Filter: TriggerFilter?

FrontedNodeGraph
  ├─ Nodes: FrontedNode[]
  └─ Connections: FrontedNodeConnection[]

LoopPolicy
  ├─ LoopCount: int? (null = infinite)
  ├─ Duration: TimeSpan?
  ├─ ReentryPolicy: ReentryPolicy
  └─ FillBehavior: FillBehavior
```
