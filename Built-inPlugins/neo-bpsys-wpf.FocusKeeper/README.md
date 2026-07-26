# neo-bpsys-wpf.FocusKeeper

[neo-bpsys-wpf](https://github.com/PLFJY/neo-bpsys-wpf) 的内置插件 —— 通过注入 DLL 拦截窗口焦点丢失消息，使游戏（如第五人格）在失去前台焦点时继续运行而不暂停或静音。

## 简介

第五人格（网易自研 NeoX 引擎）在窗口失去前台焦点时会主动暂停渲染、音频与游戏逻辑，影响后台挂机、录像或直播导播场景。Focus Keeper 通过 `SetWindowsHookEx(WH_CBT)` 把 `FocusKeeperHook.dll` 注入到目标游戏进程，拦截焦点相关窗口消息并对轮询型 API 返回"仍拥有焦点"的应答，使游戏在后台时维持运行状态。

方案完整移植自 Windhawk 的 "Ignore Focus Loss" mod，覆盖消息驱动与轮询驱动两种焦点检测方式。

## 工作原理

### 注入

- 宿主进程（主程序）调用 `SetWindowsHookEx(WH_CBT, ..., targetThreadId)`，将 `FocusKeeperHook.dll` 加载到目标进程
- 目标进程首次收到窗口消息时，DLL 的 `DllMain(DLL_PROCESS_ATTACH)` 执行懒初始化：
  - 注册自定义窗口消息 `FocusKeeper_Cleanup_8A3F2B91`
  - `EnumWindows` 枚举目标进程的所有顶层与子窗口，调用 `SetWindowSubclass` 安装 subclass proc

### 消息拦截（消息驱动型引擎）

Subclass proc 拦截以下消息，在启用状态下吞掉"失去焦点"语义：

| 消息 | 处理 |
| --- | --- |
| `WM_KILLFOCUS` | 直接返回 0，不传递给原 proc |
| `WM_ACTIVATE` (`WA_INACTIVE`) | 直接返回 0 |
| `WM_ACTIVATEAPP` (`FALSE`) | 直接返回 0 |
| `WM_SETFOCUS` | 记录主窗口（首个获得焦点的窗口） |

### API Hook（轮询驱动型引擎）

部分引擎不依赖窗口消息，而是主动调用 Win32 API 查询焦点状态。对此使用 IAT hook 拦截以下函数，让查询返回"游戏仍拥有焦点"：

- `GetForegroundWindow` → 返回跟踪的主窗口
- `GetActiveWindow` → 返回跟踪的主窗口
- `GetFocus` → 返回跟踪的主窗口
- `SetParent` → 跟踪窗口在 message-only ↔ 普通窗口之间的切换
- `DestroyWindow` → 主窗口销毁时重置跟踪状态

### 清理时序

为保证主程序退出时不影响目标进程，清理分三层：

```
主程序退出
  ├─ ApplicationStopping 触发（FocusKeeperShutdownHostedService）
  │   └─ FocusKeeperService.Dispose() → Uninstall()
  │       ├─ FocusKeeper_CleanupSubclasses(pid)
  │       │   └─ EnumWindows + SendMessageTimeoutW(g_cleanupMsg, wParam=1)
  │       │       └─ 目标进程 subclass proc 收到消息
  │       │           └─ FocusKeeper_Finalize()
  │       │               ├─ g_enabled = 0
  │       │               ├─ RemoveAllSubclassesInCurrentProcess()
  │       │               ├─ RestoreAllIatHooks()
  │       │               └─ 重置主窗口跟踪状态
  │       └─ FocusKeeper_UninstallHook() → UnhookWindowsHookEx()
  │           └─ DLL 在目标进程中 DLL_PROCESS_DETACH（兜底恢复 IAT）
  └─ Host.Dispose()（DI 单例释放，重复 Dispose 安全）
```

### 兜底机制

- **`FocusKeeper_Finalize`**：导出函数，在目标进程内执行完整清理（移除 subclass + 恢复 IAT）。通过 `wParam=1` 的清理消息从宿主进程触发，运行在目标进程上下文。
- **`DLL_PROCESS_DETACH`**：当宿主进程异常退出或 `UnhookWindowsHookEx` 触发 DLL 卸载时，在 `DllMain` 中恢复 IAT，防止悬挂指针导致目标进程崩溃。`DllMain` 中不调用 `SendMessage` 等可能阻塞的 API，仅做进程内 IAT 恢复。

## 权限要求

跨进程 `SetWindowsHookEx` 注入要求**钩子进程与目标进程具有相同的完整性级别**。若主程序未提权而目标游戏以管理员权限运行，注入将因 `ERROR_ACCESS_DENIED (5)` 失败。

主程序通过 `IElevationService`（注册在 `neo-bpsys-wpf.Core.Abstractions.Services`）向插件开放权限检测能力：

- `IElevationService.IsCurrentProcessElevated`：当前主程序是否以管理员权限运行
- `IElevationService.RestartAsAdmin()`：以管理员权限重启主程序（释放单实例锁并触发 UAC 提权）

Focus Keeper 后台页在未提权时显示提示信息与"以管理员权限重启"按钮；UAC 拒绝时显示本地化错误。

## 反作弊警告

本插件通过 DLL 注入和 IAT hook 修改目标进程行为，可能被反作弊系统检测为外挂。**仅在单机、私人或允许此类工具的场景下使用，风险自负。** 不要在带有反作弊系统的官方对战服务器使用。目标游戏的服务条款可能禁止此类修改。

## 目录结构

```
neo-bpsys-wpf.FocusKeeper/
├── FocusKeeperEntry.cs                  # 插件入口，注册服务与后台页
├── FocusKeeperService.cs                # 注入与生命周期管理（IFocusKeeperService 实现）
├── FocusKeeperShutdownHostedService.cs  # 监听 ApplicationStopping，确保退出时清理
├── IFocusKeeperService.cs               # 服务接口
├── GameWindowInfo.cs                    # 窗口枚举数据模型
├── ViewModels/
│   └── FocusKeeperPageViewModel.cs      # 后台页 ViewModel
├── Views/
│   ├── FocusKeeperPage.xaml             # 后台页 UI
│   └── FocusKeeperPage.xaml.cs
├── Locales/                             # 本地化资源（中 / 英 / 日）
│   ├── FocusKeeper.resx
│   ├── FocusKeeper.en-us.resx
│   └── FocusKeeper.ja-jp.resx
├── Native/
│   └── FocusKeeperHook/
│       ├── FocusKeeperHook.cpp          # C++ 注入 DLL 实现
│       └── FocusKeeperHook.vcxproj
├── manifest.yml                         # 插件清单
└── neo-bpsys-wpf.FocusKeeper.csproj
```

## 构建

C++ DLL（`FocusKeeperHook.dll`）需要 Visual Studio 的 C++ 工作负载。`neo-bpsys-wpf.FocusKeeper.csproj` 中已配置双路径自动构建：

- **VS IDE / Developer Command Prompt**：直接调用 VS MSBuild
- **dotnet build / build.ps1**：通过 `vswhere` 定位 VS MSBuild

若未安装 C++ 工作负载，构建会以明确错误失败（fail-fast），不会静默跳过。

```powershell
# 构建插件
dotnet build .\Built-inPlugins\neo-bpsys-wpf.FocusKeeper\neo-bpsys-wpf.FocusKeeper.csproj -c Debug

# 完整构建（包含主项目与所有插件）
.\build.ps1
```

构建产物会输出到主程序的 `Plugins` 目录：托管 DLL、原生 `FocusKeeperHook.dll`、manifest、图标与本地化卫星程序集。

## 配置

无独立配置文件。所有操作通过后台页 UI 进行：

- **自动查找并注入**：按第五人格常见进程名 / 窗口标题匹配并注入
- **刷新列表**：重新枚举系统中的可见顶层窗口
- **卸载**：移除当前注入（不影响目标进程）
- **启用开关**：注入后可随时切换是否拦截焦点消息（无需重新注入）

## 已知限制

- 同一时间只能注入一个目标进程
- 目标进程切换需要先卸载再注入
- 目标游戏若使用反作弊保护（如 TP / EAC），注入可能失败或被检测
- DirectX 全屏独占模式下窗口消息行为可能与窗口模式不同
