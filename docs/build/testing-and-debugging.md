# 测试与调试

## 测试现状

`neo-bpsys-wpf.Tests` 使用 xUnit v3、Moq 和 `Microsoft.NET.Test.Sdk`，引用主 WPF 项目。现有测试覆盖 SmartBP 识别规则与状态协调、比分、v3 布局、BPUI 包安全及插件契约。测试维护边界见 [testing-guidelines.md](testing-guidelines.md)。

当前 .NET 10 SDK 可直接构建后使用 xUnit runner：

```powershell
dotnet build .\neo-bpsys-wpf.Tests\neo-bpsys-wpf.Tests.csproj -p:WebRendererSkipWebBuild=true
dotnet .\neo-bpsys-wpf.Tests\bin\Debug\net10.0-windows10.0.20348\neo-bpsys-wpf.Tests.dll
```

文档变更通常不需要 full build。改服务、注册、项目文件或资源复制规则时应至少运行相关测试或 `dotnet build`。

## 可以优先补的测试

| 区域 | 可测内容 |
| --- | --- |
| SmartBP 文本处理 | `CleanDigitsOnly`、名称规范化、5 数字解析 |
| SmartBP 区域配置 | 默认配置生成、导入导出、校验失败 |
| 插件 API 兼容性 | 版本格式、过低、过高、兼容 |
| 插件市场 | SHA-256 规范化/比较、README 相对链接重写 |
| 设置 | 配置读写、字体/字重 converter、路径替换 |

涉及真实 WPF 窗口、OCR 推理和 OBS 捕获的测试成本高，适合拆出纯函数或服务边界后再做。

## 日志

日志路径：

```text
%APPDATA%\neo-bpsys-wpf\Log
```

用户反馈问题时优先收集：

1. 最新日志文件。
2. 应用版本和构建类型。
3. `Config.json`，注意可能包含本机路径。
4. 是否安装插件及插件列表。
5. 操作步骤和截图/录屏。

## SmartBP/OCR 调试

检查顺序：

1. OCR 模型是否已下载且已切换。
2. `Settings.OcrModelKey` 是否指向已安装模型。
3. 窗口捕获是否正在运行。
4. 赛后调试表格中的 OCR 文本、坐标聚类、列归属和最终行映射是否合理。
5. 求生者角色名匹配是 exact 还是 fuzzy，是否低于阈值。

OCR 模型路径：

```text
{SmartBpModuleRoot}\OCRModels
```

旧版本模型如果仍在 `Documents\neo-bpsys-wpf\OCRModels`，会在 SmartBP 模块首次安装或首次成功加载后迁移。Debug 构建会尝试直接加载模块项目输出目录：

```text
neo-bpsys-wpf.SmartBp.Module\bin\Debug\net10.0-windows10.0.20348
```

Debug 加载不进行远端 manifest 检查，也不强制比较模块版本。

### 可选原生 OCR Probe

`SmartBpModuleNativeDependencyTest.RapidOcrNet_IsolatedProbeInitializesAndDetects` 属于 `NativeIntegration`。普通测试显示 Skip；显式启用后，依赖、模型或样例缺失会失败，不再空执行通过：

```powershell
$env:RUN_SMARTBP_NATIVE_TESTS = '1'
$env:SMARTBP_NATIVE_MODULE_ROOT = 'C:\path\to\SmartBpModule'
dotnet build .\neo-bpsys-wpf.Tests\neo-bpsys-wpf.Tests.csproj -p:WebRendererSkipWebBuild=true
dotnet .\neo-bpsys-wpf.Tests\bin\Debug\net10.0-windows10.0.20348\neo-bpsys-wpf.Tests.dll -trait 'Category=NativeIntegration'
```

模块目录必须包含 RapidOCR 的 `ppocr-v5-zh-mobile` ONNX 模型、字典和内置测试帧，机器需要 .NET 10 x64 运行时及 SDK。Probe 使用独立 x64 子进程，覆盖原生目录注册、OCR 初始化和推理；它不等于完整执行生产模块加载器。当前 .NET 10 SDK 若通过 `dotnet test` 遇到 MTP/VSTest 入口错误，可直接使用上述 xUnit runner。执行后清除上述环境变量，恢复普通测试的默认 Skip。

## 插件加载调试

插件加载失败时看：

1. 插件目录是否在用户插件或内置插件路径下。
2. 是否存在 `manifest.yml`。
3. `entranceAssembly` 是否存在且可加载。
4. 是否有直接继承 `PluginBase` 的导出类型。
5. `apiVersion` 是否满足宿主检查。
6. 插件 ID 是否重复。
7. 插件是否被禁用或标记卸载。
8. 依赖 DLL 是否随插件包提供，或是否属于宿主已有依赖。

安装/更新插件后需要重启。`.new` 目录中的更新只有下次启动时才会覆盖到正式目录。

## 前台窗口调试

1. OBS 捕获前先确认窗口已通过后台显示。
2. 布局异常时先检查活动布局包；用户包位于 `%APPDATA%\neo-bpsys-wpf\FrontedLayoutPackages\{PackageId}\FrontedLayouts`，内置包位于程序 `Resources/FrontedLayouts`。旧 `*Config-*.json` 仅作为启动迁移输入。
3. Designer 恢复为内置布局通过包管理器读取 `builtin` 对应窗口布局与行为；不存在对应内置布局时报告缺失。
4. 插件 v3 控件不显示时检查 `plugin:{PackageId}/{ControlTypeName}`、插件是否已加载、layout 是否包含该控件，以及安装后是否已重启。

## 提交前检查

文档改动至少检查：

```powershell
git diff --check
git diff --stat
```

代码改动按风险增加验证：纯函数跑单测；WPF/DI/项目文件跑 build；插件打包改动跑 `dotnet publish -p:CreateZip=true` 验证。
