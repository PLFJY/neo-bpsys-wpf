# 构建、发布与版本

应用更新检查、安装包下载和 SHA-256 校验见 [updater-and-downloads.md](updater-and-downloads.md)。

## .NET 要求

主应用目标框架：

```xml
<TargetFramework>net9.0-windows10.0.20348</TargetFramework>
```

构建前需要 .NET 9 SDK。安装包脚本中会检查并安装 .NET 9 Desktop Runtime，当前依赖脚本阈值是 9.0.3。

## 手动 publish

README 中的基础构建命令：

```powershell
dotnet publish ".\neo-bpsys-wpf\neo-bpsys-wpf.csproj" -c Release -o ".\build\neo-bpsys-wpf"
```

主项目 csproj 会在 Build/Publish 后构建并复制内置插件到输出目录的 `Plugins\...`。

## 构建脚本

根目录脚本：

| 脚本 | 配置 |
| --- | --- |
| `build.ps1` / `build.bat` | Release |
| `build_beta.ps1` / `build_beta.bat` | Beta |
| `build_preview.ps1` / `build_preview.bat` | Preview |

PowerShell 脚本会：

1. 切到仓库根目录。
2. 创建 `build\neo-bpsys-wpf`。
3. 用 `git rev-parse --short=7 HEAD` 获取 `BuildMeta`。
4. 执行 `dotnet publish`。
5. 检查 `neo-bpsys-wpf.exe` 是否存在。
6. 调用 `Installer\Inno Setup 6\ISCC.exe` 打包。

三份 PowerShell 脚本的主要差异是传给 `dotnet publish` 的配置名：`Release`、`Beta`、`Preview`。其余路径、git hash 注入、产物检查和 Inno Setup 打包流程基本一致。

## Inno Setup

安装脚本：

```text
Installer/build_Installer.iss
```

它从发布产物 exe 提取版本号，输出：

```text
build/neo-bpsys-wpf_Installer.exe
```

安装包允许 x64 compatible 架构，复制 publish 目录全部内容和 LICENSE。`InitializeSetup` 调用 `Dependency_AddDotNet90Desktop`，依赖脚本检查 `Microsoft.WindowsDesktop.App` 9.0.3 或更高修订。卸载时询问是否删除 `%APPDATA%\neo-bpsys-wpf`。

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
| PluginSdk NuGet 包版本 | 插件项目引用的包版本 | 编译和打包 SDK 版本 |
| 插件自身版本 | 插件 `manifest.yml` 的 `version` | 市场更新比较 |

主项目注释中给出应用版本迭代原则：首位用于大型重构或重大更改，第二位用于重大模块更新或第三位满十跟进，第三位用于新 Feature，构建元数据为 git 短 hash 或 local。
