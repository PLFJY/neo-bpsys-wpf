# 构建、发布与版本

应用更新检查、安装包下载和 SHA-256 校验见 [updater-and-downloads.md](updater-and-downloads.md)。

## .NET 要求

主应用目标框架：

```xml
<TargetFramework>net10.0-windows10.0.20348</TargetFramework>
```

构建前需要 .NET 10 SDK。安装包脚本中会检查并安装 .NET 10 Desktop Runtime，当前依赖脚本阈值是 10.0.9。SmartBP 模块打包工具同样运行在 .NET 10 下。

## 手动 publish

README 中的基础构建命令：

```powershell
dotnet publish ".\neo-bpsys-wpf\neo-bpsys-wpf.csproj" -c Release -o ".\build\neo-bpsys-wpf"
```

主项目 csproj 会在 Build/Publish 后构建并复制内置插件到输出目录的 `Plugins\...`。

## WebRenderer 前端构建

WebRenderer 插件的前端由 Host sidecar 的 MSBuild target 调用 `pnpm` 构建（`pnpm install --frozen-lockfile` + `pnpm run build`），构建机需要 pnpm + Node.js。

该步骤为非致命：若构建机未安装 pnpm/Node.js 或前端构建失败，MSBuild 不会中断主程序构建，而是发出警告并跳过该插件的打包——不复制到 `Plugins\`，Host sidecar 仍正常编译但不包含 `wwwroot`。主程序及其它内置插件照常构建。主程序运行时本就不依赖 WebRenderer sidecar / Node.js，因此构建期跳过该插件是安全的优雅降级。

## 构建脚本

根目录脚本：

| 脚本 | 配置 |
| --- | --- |
| `build.ps1` / `build.bat` | Release，默认配置 |
| `build.ps1 -Configuration Beta` / `build_beta.bat` | Beta |
| `build.ps1 -Configuration Preview` / `build_preview.bat` | Preview |
| `build_beta.ps1` / `build_preview.ps1` | 兼容 wrapper，只转调 `build.ps1 -Configuration ...` |

`build.ps1` 是唯一真实构建实现，`-Configuration` 可选 `Release`、`Beta`、`Preview`，默认 `Release`。三种配置都会执行完整产物流程：

1. 切到仓库根目录。
2. 创建 `build\neo-bpsys-wpf`。
3. 用 `git rev-parse --short=7 HEAD` 获取 `BuildMeta`。
4. 按 `win-x64`、`SelfContained=false` 执行主应用 `dotnet restore`。
5. 按同一 RID 和 self-contained 配置执行内置插件 `TeamJsonMaker` 的 `dotnet restore`。
6. 按同一 RID 和 self-contained 配置执行主应用 `dotnet publish`。
7. 检查 `neo-bpsys-wpf.exe` 是否存在。
8. 从 `neo-bpsys-wpf.exe` 的 `ProductVersion` 读取本次 tag。
9. 调用 `Installer\Inno Setup 6\ISCC.exe` 构建 lite 安装包。
10. 计算 `neo-bpsys-wpf_Installer.exe.sha256`。
11. 按 `win-x64`、`SelfContained=false` 执行 SmartBP 模块项目 `dotnet restore`。
12. 按同一 RID 和 self-contained 配置执行 SmartBP 模块项目 `dotnet publish` 到 `build\SmartBpModule`。
13. 用本次 tag 写入模块 staging 的 `component.json`。
14. 用仓库内官方 x64 7-Zip（`third_party/7zip/win-x64/7z.exe`）从同一 staging 目录生成 `SmartBpModule.7z` 和 `SmartBpModuleManifest.json`。
15. 调用 `Installer/build_Installer_full.iss` 构建 full 安装包。
16. 计算 `neo-bpsys-wpf_Installer_full.exe.sha256`。

所有配置预期产物：

```text
neo-bpsys-wpf_Installer.exe
neo-bpsys-wpf_Installer.exe.sha256
neo-bpsys-wpf_Installer_full.exe
neo-bpsys-wpf_Installer_full.exe.sha256
SmartBpModule.7z
SmartBpModuleManifest.json
```

`neo-bpsys-wpf_Installer.exe` 是 lite 默认安装包，也是旧 updater 固定目标。它不包含 SmartBP 的 OpenCvSharp、PaddleOCR、PaddleInference 和具体实现 DLL。`neo-bpsys-wpf_Installer_full.exe` 是首次安装便利包，包含 lite 应用和 SmartBP 模块 staging 文件，并在安装时写入 `%APPDATA%\neo-bpsys-wpf\SmartBpModuleState.json`。

`SmartBpModule.7z` 和 full 安装包必须来自同一个 `build\SmartBpModule` staging 目录，避免 release 模块包与 full installer 内置模块不一致。官方 SmartBP 模块 release artifact 是 `SmartBpModule.7z`；运行时仍兼容旧 `SmartBpModule.zip` 包。

SmartBP 模块通过在线下载、设置页导入或 SmartBP 页面手动导入 `.7z` / `.zip` 升级时，只替换模块程序文件；模块目录下的托管 OCR 资产根目录 `OCRModels/` 和 `tessdata/` 会原地保留。这里包含 PaddleOCR、RapidOCR 模型和 Tesseract traineddata。即使归档包误带同名资产目录，也不得覆盖用户已经下载好的 OCR 资产。

`SmartBpModuleManifest.json` 中的 `ModuleVersion` 和下载 URL 使用本次构建确定的 release tag，也就是主程序 `ProductVersion`。正式 GitHub Actions 发布时同样读取安装包 `ProductVersion` 并作为 `tag_name`，因此 manifest 内不再保留 `{tag}` 占位。full 安装器写入的 `SmartBpModuleState.ModuleVersion` 也使用同一个 `ProductVersion`。

SmartBP 模块打包使用仓库内官方 x64 7-Zip（`third_party/7zip/win-x64/7z.exe`），构建机器不需要安装 7-Zip。运行时解压由随应用发布的官方 7-Zip 完成（位于 `<AppBase>/Tools/7Zip/`），用户不需要单独安装 7-Zip。应用仅发布 `win-x64`。

插件包可以是 `.zip` 或 `.7z`，安装时同样由运行时归档服务探测并解压。`.bpui` / Designer v3 布局包导入导出行为不随 SmartBP/插件归档支持变化，仍按 BPUI 文档描述处理。

## Inno Setup

安装脚本：

```text
Installer/build_Installer.iss
```

lite 安装脚本从发布产物 exe 提取版本号，输出：

```text
build/neo-bpsys-wpf_Installer.exe
```

full 安装脚本：

```text
Installer/build_Installer_full.iss
```

输出：

```text
build/neo-bpsys-wpf_Installer_full.exe
```

安装包允许 x64 compatible 架构，lite 安装包复制 publish 目录全部内容和 LICENSE。full 安装包额外复制 SmartBP 模块 staging 目录，并提供模块安装路径页面。模块路径会阻止 Program Files、Program Files (x86)、Windows、System32、驱动器根目录等不适合写入或维护的位置。`InitializeSetup` 调用 `Dependency_AddDotNet100Desktop`，依赖脚本检查 `Microsoft.WindowsDesktop.App` 10.0.9 或更高修订。卸载时询问是否删除 `%APPDATA%\neo-bpsys-wpf`。

## 构建配置

主项目定义：

```xml
<Configurations>Debug;Release;Beta;Preview</Configurations>
```

| 配置 | 行为 |
| --- | --- |
| Release | 默认正式构建 |
| Beta | 定义 `BETA`，版本后缀 `-beta`，`IsFindPreRelease` 默认 true |
| Preview | 定义 `PREVIEW`，版本后缀 `-preview`，优化关闭、调试符号开启 |
| Debug | 开发调试 |

代码观察到的 caveat：`App.xaml.cs` 中更新检查条件写作 `#if !DEBUG && !Preview`，而 csproj 定义的是 `PREVIEW`。因此本文档不随口声称 Preview 构建一定跳过更新检查；本任务只记录 caveat，不修改代码。

## 版本概念

| 概念 | 位置 | 说明 |
| --- | --- | --- |
| 应用版本 | `neo-bpsys-wpf.csproj` 的 `VersionPrefix/VersionSuffix` | 应用发布版本 |
| BuildMeta | 构建脚本传入 `/p:BuildMeta=$GitHash` | 写入 `InformationalVersion` |
| 插件 API 版本 | 插件 `manifest.yml` 的 `apiVersion` | 宿主加载兼容性判断 |
| PluginSdk 源码引用版本 | 插件项目引用的 `neo-bpsys-wpf.PluginSdk` 仓库提交 | 编译和打包 SDK 来源 |
| 插件自身版本 | 插件 `manifest.yml` 的 `version` | 市场更新比较 |

主项目注释中给出应用版本迭代原则：首位用于大型重构或重大更改，第二位用于重大模块更新或第三位满十跟进，第三位用于新 Feature，构建元数据为 git 短 hash 或 local。

v3 起不再发布 PluginSdk NuGet 包，也不再通过 GitHub Actions 推送 PluginSdk nupkg。插件开发应 clone 本仓库并用 `ProjectReference` 引用 `neo-bpsys-wpf.PluginSdk`，必要时固定到具体 git 提交。
