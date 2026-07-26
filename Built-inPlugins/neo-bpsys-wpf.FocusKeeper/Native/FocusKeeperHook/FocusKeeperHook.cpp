// FocusKeeperHook.cpp
//
// Focus-loss suppression DLL for the neo-bpsys-wpf FocusKeeper plugin.
//
// 目标游戏：第五人格（网易自研 NeoX 引擎）。
//
// 注入方式：
//   宿主进程（C#）调用 SetWindowsHookEx(WH_CBT)，Windows 自动将本 DLL
//   加载进目标游戏进程。
//
// 方案（完整移植自 Windhawk "Ignore Focus Loss" mod）：
//   1. Subclass 所有相关窗口，拦截 WM_KILLFOCUS / WM_ACTIVATE(WA_INACTIVE) /
//      WM_ACTIVATEAPP(FALSE) 消息，使游戏认为始终拥有焦点。
//   2. IAT hook GetForegroundWindow / GetActiveWindow / GetFocus，让轮询型
//      引擎在主动查询焦点状态时也得到"我是前台"的回答。
//   3. 跟踪"主窗口"（第一个获得焦点的窗口），所有焦点查询都返回它。
//   4. Hook SetParent 处理窗口在 message-only ↔ 普通窗口之间的切换。
//   5. Hook DestroyWindow 在主窗口销毁时重置状态。
//
// 关于 NeoX 引擎：
//   NeoX 的焦点检测机制未公开。本实现同时覆盖「消息驱动」与「轮询驱动」
//   两种模式，与 Windhawk mod 的行为一致。

#include <windows.h>
#include <commctrl.h>
#include <tlhelp32.h>

// ===================== 跨进程共享状态 =====================
// 宿主进程与目标进程共享 g_enabled：宿主通过 FocusKeeper_SetEnabled 切换，
// 目标进程在 subclass proc / API hook 中读取。
// 共享段变量必须初始化（否则落入 BSS，不跨进程共享）。
#pragma data_seg(".FK_SHARED")
volatile LONG g_enabled = 0;   // 0 = disabled, nonzero = enabled
#pragma data_seg()
#pragma comment(linker, "/SECTION:.FK_SHARED,RWS")

// ===================== 进程内状态 =====================
static HMODULE g_hModule = NULL;
static UINT g_cleanupMsg = 0;                   // RegisterWindowMessage 注册的清理消息
static volatile LONG g_subclassesInstalled = 0; // 懒初始化标志
static bool g_finalizing = false;               // 防止 Finalize 与 DLL_PROCESS_DETACH 重复清理

// 「主窗口」= 第一个获得焦点/激活/前台的窗口。
// 一旦设定，它将作为主窗口直到被销毁。
static HWND g_mainWindow = NULL;
static DWORD g_mainThreadId = 0;

// ===================== 原始函数指针（IAT hook 保存） =====================
typedef HWND (WINAPI *GetForegroundWindow_t)();
typedef HWND (WINAPI *GetActiveWindow_t)();
typedef HWND (WINAPI *GetFocus_t)();
typedef HWND (WINAPI *SetParent_t)(HWND hWndChild, HWND hWndNewParent);
typedef BOOL (WINAPI *DestroyWindow_t)(HWND hWnd);

static GetForegroundWindow_t g_origGetForegroundWindow = nullptr;
static GetActiveWindow_t     g_origGetActiveWindow = nullptr;
static GetFocus_t            g_origGetFocus = nullptr;
static SetParent_t           g_origSetParent = nullptr;
static DestroyWindow_t       g_origDestroyWindow = nullptr;

// ===================== 前向声明 =====================
static LRESULT CALLBACK FocusKeeperSubclassProc(
    HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam,
    UINT_PTR uIdSubclass, DWORD_PTR dwRefData);
static void InstallAllIatHooks();
static void RestoreAllIatHooks();
static void RemoveAllSubclassesInCurrentProcess();
static BOOL CALLBACK UnsubclassAllWindowsProc(HWND hwnd, LPARAM lParam);
static HWND WINAPI HookedGetForegroundWindow();
static HWND WINAPI HookedGetActiveWindow();
static HWND WINAPI HookedGetFocus();
static HWND WINAPI HookedSetParent(HWND hWndChild, HWND hWndNewParent);
static BOOL WINAPI HookedDestroyWindow(HWND hWnd);
extern "C" __declspec(dllexport) void WINAPI FocusKeeper_Finalize(void);

// ===================== 主窗口跟踪 =====================
static void SetMainWindow(HWND hwnd) {
    if (g_mainWindow != NULL) return;  // 已有主窗口，不覆盖
    g_mainWindow = hwnd;
    g_mainThreadId = GetWindowThreadProcessId(hwnd, nullptr);
}

// ===================== IAT Hook 基础设施 =====================
// 在单个模块的导入表中查找并替换目标函数。
// 返回原始函数地址（替换前 IAT 中的值），未找到返回 nullptr。
static void* HookIatForModule(HMODULE hModule,
                              const char* importDll,
                              const char* funcName,
                              void* hookFunc) {
    if (!hModule) return nullptr;

    auto dosHeader = reinterpret_cast<IMAGE_DOS_HEADER*>(hModule);
    if (dosHeader->e_magic != IMAGE_DOS_SIGNATURE) return nullptr;

    auto ntHeaders = reinterpret_cast<IMAGE_NT_HEADERS*>(
        reinterpret_cast<BYTE*>(hModule) + dosHeader->e_lfanew);
    if (ntHeaders->Signature != IMAGE_NT_SIGNATURE) return nullptr;

    auto& importDir = ntHeaders->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
    if (importDir.VirtualAddress == 0) return nullptr;

    auto importDesc = reinterpret_cast<IMAGE_IMPORT_DESCRIPTOR*>(
        reinterpret_cast<BYTE*>(hModule) + importDir.VirtualAddress);

    for (; importDesc->Name != 0; importDesc++) {
        const char* dllName = reinterpret_cast<const char*>(
            reinterpret_cast<BYTE*>(hModule) + importDesc->Name);
        if (_stricmp(dllName, importDll) != 0) continue;

        // FirstThunk 指向 IAT（运行时已被填充为函数地址）
        auto thunk = reinterpret_cast<IMAGE_THUNK_DATA*>(
            reinterpret_cast<BYTE*>(hModule) + importDesc->FirstThunk);
        // OriginalFirstThunk 指向 INT（保留函数名信息）
        auto origThunk = reinterpret_cast<IMAGE_THUNK_DATA*>(
            reinterpret_cast<BYTE*>(hModule) + importDesc->OriginalFirstThunk);

        for (; thunk->u1.Function != 0; thunk++, origThunk++) {
            if (IMAGE_SNAP_BY_ORDINAL(origThunk->u1.Ordinal)) continue;  // 按序号导入，跳过

            auto importByName = reinterpret_cast<IMAGE_IMPORT_BY_NAME*>(
                reinterpret_cast<BYTE*>(hModule) + origThunk->u1.AddressOfData);
            if (strcmp(importByName->Name, funcName) != 0) continue;

            void* original = reinterpret_cast<void*>(thunk->u1.Function);
            DWORD oldProtect = 0;
            VirtualProtect(&thunk->u1.Function, sizeof(void*), PAGE_READWRITE, &oldProtect);
            thunk->u1.Function = reinterpret_cast<ULONG_PTR>(hookFunc);
            VirtualProtect(&thunk->u1.Function, sizeof(void*), oldProtect, &oldProtect);
            return original;
        }
    }
    return nullptr;
}

// 遍历当前进程的所有模块，替换每个模块 IAT 中匹配的函数。
// origFunc 只保存第一次找到的原始地址（所有模块导入的是同一个函数实现）。
static void HookIatForAllModules(const char* importDll,
                                 const char* funcName,
                                 void* hookFunc,
                                 void** origFunc) {
    HANDLE snapshot = CreateToolhelp32Snapshot(
        TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, GetCurrentProcessId());
    if (snapshot == INVALID_HANDLE_VALUE) return;

    MODULEENTRY32W me{};
    me.dwSize = sizeof(me);
    if (Module32FirstW(snapshot, &me)) {
        do {
            void* orig = HookIatForModule(me.hModule, importDll, funcName, hookFunc);
            if (orig && origFunc && !*origFunc) {
                *origFunc = orig;
            }
        } while (Module32NextW(snapshot, &me));
    }
    CloseHandle(snapshot);
}

// 在单个模块的 IAT 中查找当前指向 hookFunc 的条目，恢复为 origFunc。
static void UnhookIatForModule(HMODULE hModule,
                               const char* importDll,
                               const char* funcName,
                               void* hookFunc,
                               void* origFunc) {
    if (!hModule || !origFunc) return;

    auto dosHeader = reinterpret_cast<IMAGE_DOS_HEADER*>(hModule);
    if (dosHeader->e_magic != IMAGE_DOS_SIGNATURE) return;

    auto ntHeaders = reinterpret_cast<IMAGE_NT_HEADERS*>(
        reinterpret_cast<BYTE*>(hModule) + dosHeader->e_lfanew);
    if (ntHeaders->Signature != IMAGE_NT_SIGNATURE) return;

    auto& importDir = ntHeaders->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
    if (importDir.VirtualAddress == 0) return;

    auto importDesc = reinterpret_cast<IMAGE_IMPORT_DESCRIPTOR*>(
        reinterpret_cast<BYTE*>(hModule) + importDir.VirtualAddress);

    for (; importDesc->Name != 0; importDesc++) {
        const char* dllName = reinterpret_cast<const char*>(
            reinterpret_cast<BYTE*>(hModule) + importDesc->Name);
        if (_stricmp(dllName, importDll) != 0) continue;

        auto thunk = reinterpret_cast<IMAGE_THUNK_DATA*>(
            reinterpret_cast<BYTE*>(hModule) + importDesc->FirstThunk);
        auto origThunk = reinterpret_cast<IMAGE_THUNK_DATA*>(
            reinterpret_cast<BYTE*>(hModule) + importDesc->OriginalFirstThunk);

        for (; thunk->u1.Function != 0; thunk++, origThunk++) {
            if (IMAGE_SNAP_BY_ORDINAL(origThunk->u1.Ordinal)) continue;

            auto importByName = reinterpret_cast<IMAGE_IMPORT_BY_NAME*>(
                reinterpret_cast<BYTE*>(hModule) + origThunk->u1.AddressOfData);
            if (strcmp(importByName->Name, funcName) != 0) continue;

            // 仅当当前 IAT 条目指向我们的 hook 函数时才恢复
            if (reinterpret_cast<void*>(thunk->u1.Function) != hookFunc) continue;

            DWORD oldProtect = 0;
            VirtualProtect(&thunk->u1.Function, sizeof(void*), PAGE_READWRITE, &oldProtect);
            thunk->u1.Function = reinterpret_cast<ULONG_PTR>(origFunc);
            VirtualProtect(&thunk->u1.Function, sizeof(void*), oldProtect, &oldProtect);
        }
    }
}

// 遍历所有模块，恢复 IAT 中被 hook 的函数。
// 必须在 DLL 仍驻留目标进程时调用，否则 hook 函数地址无效。
static void RestoreAllIatHooks() {
    HANDLE snapshot = CreateToolhelp32Snapshot(
        TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, GetCurrentProcessId());
    if (snapshot == INVALID_HANDLE_VALUE) return;

    MODULEENTRY32W me{};
    me.dwSize = sizeof(me);
    if (Module32FirstW(snapshot, &me)) {
        do {
            UnhookIatForModule(me.hModule, "user32.dll", "GetForegroundWindow",
                               (void*)HookedGetForegroundWindow, (void*)g_origGetForegroundWindow);
            UnhookIatForModule(me.hModule, "user32.dll", "GetActiveWindow",
                               (void*)HookedGetActiveWindow, (void*)g_origGetActiveWindow);
            UnhookIatForModule(me.hModule, "user32.dll", "GetFocus",
                               (void*)HookedGetFocus, (void*)g_origGetFocus);
            UnhookIatForModule(me.hModule, "user32.dll", "SetParent",
                               (void*)HookedSetParent, (void*)g_origSetParent);
            UnhookIatForModule(me.hModule, "user32.dll", "DestroyWindow",
                               (void*)HookedDestroyWindow, (void*)g_origDestroyWindow);
        } while (Module32NextW(snapshot, &me));
    }
    CloseHandle(snapshot);
}

// 移除当前进程中所有由本 DLL 安装的 subclass。
// 通过 EnumWindows 枚举本进程的窗口，向每个窗口发送清理消息。
static void RemoveAllSubclassesInCurrentProcess() {
    DWORD currentPid = GetCurrentProcessId();
    EnumWindows(UnsubclassAllWindowsProc, (LPARAM)currentPid);
}

// ===================== API Hook 函数 =====================

static HWND WINAPI HookedGetForegroundWindow() {
    if (g_enabled && g_mainWindow != NULL) {
        return g_mainWindow;
    }
    if (g_enabled && g_mainWindow == NULL) {
        // 尚未设定主窗口：检查当前前台是否属于本进程
        HWND fg = g_origGetForegroundWindow ? g_origGetForegroundWindow() : GetForegroundWindow();
        if (fg) {
            DWORD pid = 0;
            GetWindowThreadProcessId(fg, &pid);
            if (pid == GetCurrentProcessId()) {
                SetMainWindow(fg);
            }
        }
        return fg;
    }
    return g_origGetForegroundWindow ? g_origGetForegroundWindow() : GetForegroundWindow();
}

static HWND WINAPI HookedGetActiveWindow() {
    if (g_enabled) {
        if (g_mainWindow == NULL) {
            // 尚未设定主窗口：用真实活动窗口设定
            HWND actual = g_origGetActiveWindow ? g_origGetActiveWindow() : GetActiveWindow();
            if (actual) SetMainWindow(actual);
            return actual;
        }
        // 已有主窗口：仅当当前线程拥有它时才返回（与 Windhawk mod 一致）
        if (g_mainThreadId == GetCurrentThreadId()) return g_mainWindow;
        return NULL;
    }
    return g_origGetActiveWindow ? g_origGetActiveWindow() : GetActiveWindow();
}

static HWND WINAPI HookedGetFocus() {
    if (g_enabled) {
        if (g_mainWindow == NULL) {
            HWND actual = g_origGetFocus ? g_origGetFocus() : GetFocus();
            if (actual) SetMainWindow(actual);
            return actual;
        }
        if (g_mainThreadId == GetCurrentThreadId()) return g_mainWindow;
        return NULL;
    }
    return g_origGetFocus ? g_origGetFocus() : GetFocus();
}

// 处理窗口在 message-only ↔ 普通窗口之间切换
static HWND WINAPI HookedSetParent(HWND hWndChild, HWND hWndNewParent) {
    bool becomingMessageOnly = (hWndNewParent == HWND_MESSAGE);
    HWND oldParent = g_origSetParent ? g_origSetParent(hWndChild, hWndNewParent)
                                     : SetParent(hWndChild, hWndNewParent);
    bool wasMessageOnly = (oldParent == HWND_MESSAGE);

    if (becomingMessageOnly && !wasMessageOnly) {
        // 从普通窗口变为 message-only：移除 subclass
        SendMessageTimeoutW(hWndChild, g_cleanupMsg, 0, 0, SMTO_BLOCK, 1000, nullptr);
    } else if (wasMessageOnly && !becomingMessageOnly) {
        // 从 message-only 变为普通窗口：添加 subclass
        SetWindowSubclass(hWndChild, FocusKeeperSubclassProc, 0, 0);
    }
    return oldParent;
}

// 主窗口销毁时重置状态
static BOOL WINAPI HookedDestroyWindow(HWND hWnd) {
    if (g_mainWindow == hWnd) {
        g_mainWindow = NULL;
        g_mainThreadId = 0;
    }
    return g_origDestroyWindow ? g_origDestroyWindow(hWnd) : DestroyWindow(hWnd);
}

// ===================== Subclass Procedure =====================
static LRESULT CALLBACK FocusKeeperSubclassProc(
    HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam,
    UINT_PTR uIdSubclass, DWORD_PTR dwRefData)
{
    // 清理消息（宿主进程通过 SendMessageTimeout 发送）
    if (uMsg == g_cleanupMsg && wParam == 0) {
        RemoveWindowSubclass(hWnd, FocusKeeperSubclassProc, 0);
        return 0;
    }

    // 完整清理消息（宿主进程通过 SendMessageTimeout 发送，wParam == 1）
    // 触发目标进程内的 FocusKeeper_Finalize，移除所有 subclass 并恢复 IAT。
    // 仅在第一个窗口上执行一次（g_finalizing 防重复），其余窗口直接移除 subclass。
    if (uMsg == g_cleanupMsg && wParam == 1) {
        if (!g_finalizing) {
            g_finalizing = true;
            FocusKeeper_Finalize();
        } else {
            RemoveWindowSubclass(hWnd, FocusKeeperSubclassProc, 0);
        }
        return 0;
    }

    if (g_enabled) {
        switch (uMsg) {
            case WM_SETFOCUS:
                // 获得焦点时设定主窗口（若尚未设定）
                if (g_mainWindow == NULL) SetMainWindow(hWnd);
                break;
            case WM_KILLFOCUS:
                // 丢失键盘焦点：吞掉，让游戏认为仍有焦点
                return 0;
            case WM_ACTIVATE:
                // 同应用内激活状态变化：WA_INACTIVE 表示失去激活
                if (LOWORD(wParam) == WA_INACTIVE) return 0;
                break;
            case WM_ACTIVATEAPP:
                // 跨应用激活状态变化：FALSE 表示失去激活
                if (wParam == FALSE) return 0;
                break;
        }
    }

    return DefSubclassProc(hWnd, uMsg, wParam, lParam);
}

// ===================== 辅助函数 =====================
static bool IsRelevantWindow(HWND hwnd) {
    if (GetParent(hwnd) == HWND_MESSAGE) return false;  // 跳过 message-only
    DWORD pid = 0;
    GetWindowThreadProcessId(hwnd, &pid);
    return pid == GetCurrentProcessId();
}

// ===================== EnumWindows 回调 =====================
static BOOL CALLBACK SubclassExistingWindowsProc(HWND hwnd, LPARAM) {
    if (IsRelevantWindow(hwnd)) {
        SetWindowSubclass(hwnd, FocusKeeperSubclassProc, 0, 0);
    }
    return TRUE;
}

static BOOL CALLBACK UnsubclassAllWindowsProc(HWND hwnd, LPARAM lParam) {
    DWORD targetPid = (DWORD)lParam;
    DWORD pid = 0;
    GetWindowThreadProcessId(hwnd, &pid);
    if (pid == targetPid && GetParent(hwnd) != HWND_MESSAGE) {
        DWORD_PTR result = 0;
        // wParam=1 触发目标进程内的 FocusKeeper_Finalize（完整清理：subclass + IAT）
        SendMessageTimeoutW(hwnd, g_cleanupMsg, 1, 0, SMTO_BLOCK, 5000, &result);
    }
    return TRUE;
}

// ===================== WH_CBT Hook Procedure =====================
static LRESULT CALLBACK CbtHookProc(int nCode, WPARAM wParam, LPARAM lParam) {
    if (nCode >= 0) {
        // 懒初始化：第一次 hook 调用时 subclass 所有现有窗口 + 安装 IAT hook
        if (InterlockedCompareExchange(&g_subclassesInstalled, 1, 0) == 0) {
            EnumWindows(SubclassExistingWindowsProc, 0);
            InstallAllIatHooks();
        }
        // subclass 新创建的窗口
        if (nCode == HCBT_CREATEWND) {
            HWND hwnd = (HWND)wParam;
            if (IsRelevantWindow(hwnd)) {
                SetWindowSubclass(hwnd, FocusKeeperSubclassProc, 0, 0);
            }
        }
    }
    return CallNextHookEx(NULL, nCode, wParam, lParam);
}

// ===================== IAT Hook 安装/卸载 =====================
static void InstallAllIatHooks() {
    HookIatForAllModules("user32.dll", "GetForegroundWindow",
                         (void*)HookedGetForegroundWindow,
                         (void**)&g_origGetForegroundWindow);
    HookIatForAllModules("user32.dll", "GetActiveWindow",
                         (void*)HookedGetActiveWindow,
                         (void**)&g_origGetActiveWindow);
    HookIatForAllModules("user32.dll", "GetFocus",
                         (void*)HookedGetFocus,
                         (void**)&g_origGetFocus);
    HookIatForAllModules("user32.dll", "SetParent",
                         (void*)HookedSetParent,
                         (void**)&g_origSetParent);
    HookIatForAllModules("user32.dll", "DestroyWindow",
                         (void*)HookedDestroyWindow,
                         (void**)&g_origDestroyWindow);
}

// ===================== 导出函数 =====================
extern "C" __declspec(dllexport)
HHOOK WINAPI FocusKeeper_InstallHook(DWORD targetThreadId) {
    return SetWindowsHookExW(WH_CBT, CbtHookProc, g_hModule, targetThreadId);
}

extern "C" __declspec(dllexport)
BOOL WINAPI FocusKeeper_UninstallHook(HHOOK hookHandle) {
    if (hookHandle == NULL) return FALSE;
    return UnhookWindowsHookEx(hookHandle);
}

extern "C" __declspec(dllexport)
void WINAPI FocusKeeper_CleanupSubclasses(DWORD targetProcessId) {
    // 先关闭开关（共享变量，目标进程立即可见）
    InterlockedExchange(&g_enabled, 0);
    // 向目标进程所有窗口发送完整清理消息（wParam=1 触发 FocusKeeper_Finalize）
    EnumWindows(UnsubclassAllWindowsProc, (LPARAM)targetProcessId);
}

extern "C" __declspec(dllexport)
void WINAPI FocusKeeper_SetEnabled(BOOL enabled) {
    InterlockedExchange(&g_enabled, enabled ? 1 : 0);
}

extern "C" __declspec(dllexport)
BOOL WINAPI FocusKeeper_IsEnabled(void) {
    return InterlockedCompareExchange(&g_enabled, 0, 0) != 0 ? TRUE : FALSE;
}

// 完整清理：在目标进程内移除所有 subclass 并恢复所有 IAT hook。
// 宿主进程在卸载钩子后、释放 DLL 前通过 SendMessageTimeout 触发目标进程调用此函数。
// 此函数运行在目标进程上下文中，确保清理是进程内的、安全的。
extern "C" __declspec(dllexport)
void WINAPI FocusKeeper_Finalize(void) {
    // 1) 先关闭开关，避免 subclass proc / hook 函数再产生副作用
    InterlockedExchange(&g_enabled, 0);

    // 2) 移除所有 subclass（必须在同进程内调用 RemoveWindowSubclass 才有效）
    RemoveAllSubclassesInCurrentProcess();

    // 3) 恢复所有 IAT hook（必须在 DLL 仍驻留时进行，否则 hook 函数地址无效）
    RestoreAllIatHooks();

    // 4) 重置主窗口跟踪状态
    g_mainWindow = NULL;
    g_mainThreadId = 0;

    // 5) 标记 subclass 未安装，允许下次注入时重新初始化
    InterlockedExchange(&g_subclassesInstalled, 0);
}

// ===================== DLL 入口 =====================
// DLL_PROCESS_DETACH 作为兜底清理路径：
// 当宿主进程异常退出、或 UnhookWindowsHookEx 触发 DLL 卸载时，
// 在目标进程内恢复 IAT 防止崩溃。
// 注意：DllMain 中不应调用 SendMessage 等可能阻塞的 API，
// 因此只做 IAT 恢复（进程内、不跨进程），subclass 移除由 Finalize 完成。

BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_ATTACH) {
        g_hModule = hModule;
        g_cleanupMsg = RegisterWindowMessageW(L"FocusKeeper_Cleanup_8A3F2B91");
        DisableThreadLibraryCalls(hModule);
    } else if (reason == DLL_PROCESS_DETACH) {
        // 兜底清理：若 Finalize 未被调用，在此恢复 IAT 防止崩溃。
        if (!g_finalizing) {
            g_finalizing = true;
            InterlockedExchange(&g_enabled, 0);
            RestoreAllIatHooks();
        }
    }
    return TRUE;
}
