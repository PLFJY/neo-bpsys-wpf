# 异常退出与本地 Dump 收集

## 先收集日志

关闭应用后复制 `%APPDATA%\neo-bpsys-wpf\Log` 中的 `latest.txt`、对应时间的 `log-*.txt` 和 `run-state.json`。正常退出会归档 `latest.txt`；中断退出保留它，下次启动先归档上次日志，再创建本次日志。归档保留最近 10 次运行，因此请及时复制。

若主日志目录不可写或 Host 尚未建立，检查 `%TEMP%\neo-bpsys-wpf\Log\emergency-<PID>.txt`。紧急日志仅在主日志不可用时同步追加，无上传、无自动归档；收集后可手动删除。两个目录均不可写、磁盘已满或系统无法执行写入时不能保证留下日志。

`run-state.json` 保存 RunId、启动时间（含时区）、PID、版本、状态和更新时间。状态依次为 `Running`、`ShutdownRequested`、`ShutdownCompleted`。只有 Host 停止、释放和 WPF Exit 回调成功完成、且未见全局致命异常时才记录完成。状态用同目录临时文件原子替换。读取失败会记录状态未知，不猜测为崩溃。

下次启动发现未完成状态会记录 `Unclean shutdown detected` 和上次身份。它只能证明关闭流程没有完成，不能区分崩溃、强制结束、断电和系统重启。重启新进程等待旧进程释放诊断锁后才归档和写状态，避免新旧运行交叉覆盖；等待超过 45 秒则取消新进程启动并写紧急日志，保留旧进程证据。UAC 拒绝不代表退出，应用仍按原有调用方处理取消。

## 托管异常和日志级别

- WPF Dispatcher：同步 Critical，保存完整异常，不设置 `Handled`，不在异常回调中弹异步对话框或启动 Explorer。
- AppDomain：同步 Critical，记录 `IsTerminating`、异常对象类型和完整托管异常，不依赖 DI。
- TaskScheduler：Warning，记录完整 AggregateException，不调用 `SetObserved`；它不等于进程崩溃，且只有 GC 发现未观察异常时才触发，不保证即时触发。

日志的异常正文使用 `Exception.ToString()`，含类型、Message、StackTrace、InnerException，以及 AggregateException 的多个内部异常。每条日志含时间、PID 和当前托管线程 ID；运行诊断包含 RunId，Designer 节点包含 WindowInstance。尚未 throw 的异常可能没有 StackTrace。

Error/Critical 不受普通级别抑制。少量生命周期、Designer 初始化、运行状态和全局异常通过同一 FileLoggerProvider 的诊断入口始终保留，不复制日志管线。其他日志继续按现有 AppLogLevel / GetEffectiveLogLevel 设置过滤，不默认启用 Debug/Trace。插件在 Host.Build 前初始化时，已捕获的更新、清单解析及加载异常也通过同一诊断入口记录，避免 DI logger 尚不存在而丢失错误。WebRenderer stdout 普通流水转为 Debug，默认 Console 格式的警告、错误和缩进异常续行保留对应级别，stderr 为 Warning，意外退出记录 PID 和退出码；不会把 HTTP 请求流水默认灌入 Information 日志。

`OnExit` 不再依赖 `async void` 完成：在 UI 线程开始停止、不捕获 UI 同步上下文，有期限地等待 Host 停止（30 秒），随后释放 Host。停止失败/超时记录完整异常并继续原有异常传播，不标记完成。插件停止回调不得同步等待已进入关闭流程的 Dispatcher；不遵守取消的插件仍可能拖延退出。这里的完成标记表示应用关闭工作完成，不是操作系统已确认进程退出。

不记录配置、布局 JSON、BP 内容或凭据，不新增遥测。异常本身可能包含路径或第三方组件提供的内容，分享前仍应检查。

## 手动启用 Windows WER LocalDumps

适用于主程序 **`neo-bpsys-wpf.exe`**。用户主动启用，应用不会修改注册表。需使用 **64 位管理员 PowerShell**。配置依据：[Microsoft：Collecting User-Mode Dumps](https://learn.microsoft.com/en-us/windows/win32/wer/collecting-user-mode-dumps)。

1. 先退出应用。检查该 EXE 是否已有专属配置；如果存在，先导出备份，并记下备份位置：

   ```powershell
   $werKey = 'HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\neo-bpsys-wpf.exe'
   reg.exe query $werKey
   # 仅在 query 成功、该键原本存在时执行；不要覆盖以前的备份。
   reg.exe export $werKey "$env:USERPROFILE\Desktop\neo-bpsys-wpf-WER-before.reg"
   ```

2. 只配置主程序，不修改全局 LocalDumps。以下启用最多 3 份完整 Dump，保存在崩溃进程用户的 `%LOCALAPPDATA%\CrashDumps`：

   ```powershell
   $werKey = 'HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\neo-bpsys-wpf.exe'
   reg.exe add $werKey /v DumpFolder /t REG_EXPAND_SZ /d '%LOCALAPPDATA%\CrashDumps' /f
   reg.exe add $werKey /v DumpCount /t REG_DWORD /d 3 /f
   reg.exe add $werKey /v DumpType /t REG_DWORD /d 2 /f
   reg.exe query $werKey
   ```

   每条命令应显示成功。重新启动应用，等待问题复现；退出后在该用户目录寻找 `.dmp`，同时复制应用日志。若管理员使用了另一账户，查看实际运行应用的用户目录。

3. 诊断完成后关闭：删除这次 EXE 的专属键。如果原来已有配置，随后导入第一步的备份恢复原值。不要删除整个 `LocalDumps` 键。

   ```powershell
   $werKey = 'HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\neo-bpsys-wpf.exe'
   reg.exe delete $werKey /f
   # 仅当第一步确实备份了原配置时执行：
   reg.exe import "$env:USERPROFILE\Desktop\neo-bpsys-wpf-WER-before.reg"
   ```

   若机器另有全局 LocalDumps 策略，删除专属键会恢复继承该策略；由原配置的管理者处理。已生成的 Dump 不会自动删除。

**完整 Dump 可能达到数 GB，并含进程内存中的 Token、密码、私人文本和布局内容。不得公开上传或附到公开 Issue。仅在用户明确同意后通过私密渠道交给维护者；分析结束后删除。** 不要长期保留完整 Dump 配置。这里没有持续抓取、云端服务或默认自动收集。

## 没有 Dump 时

AccessViolation、StackOverflow、FailFast 等可能绕过托管事件；WER 是否生成 Dump 取决于实际终止路径和系统配置。外部强杀、断电、系统重启通常不会触发应用崩溃 Dump。自动崩溃调试器也可能阻止 LocalDumps 收集。不要把“没有日志/没有 Dump”当成排除原生故障的证据。

如果进程仍在但卡住，可在任务管理器“详细信息”中右击 `neo-bpsys-wpf.exe`，选择创建内存转储文件，并记下提示路径；这记录的是采集时状态。需要在退出时采集时可由维护者指导使用 [Microsoft Sysinternals ProcDump](https://learn.microsoft.com/en-us/sysinternals/downloads/procdump)。进程已经消失后无法补抓此前内存。

维护者同时索取准确的退出时间（含时区）、应用版本、Windows 版本、用户操作、RunId、日志和私密 Dump；不能仅凭最后一条 INFO 推断根因。
