# 测试与调试

## 测试现状

`neo-bpsys-wpf.Tests` 使用 xUnit v3、Moq 和 `Microsoft.NET.Test.Sdk`，引用主 WPF 项目。当前 `SmartBpServiceTest` 中的大部分断言和样例代码被注释，`[Fact]` 方法基本是空执行，更接近手工调试记录，而不是稳定自动化测试覆盖。

可运行测试命令：

```powershell
dotnet test .\neo-bpsys-wpf.Tests\neo-bpsys-wpf.Tests.csproj
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
4. 捕获画面比例是否和 `GameDataRegions.json` 匹配。
5. OCR 原始文本日志是否合理。
6. 求生者角色名匹配是 exact 还是 fuzzy，是否低于阈值。

区域配置路径：

```text
%APPDATA%\neo-bpsys-wpf\SmartBp\GameDataRegions.json
```

OCR 模型路径：

```text
Documents\neo-bpsys-wpf\OCRModels
```

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
2. 布局异常时检查 `%APPDATA%\neo-bpsys-wpf\*Config-*.json`。
3. 恢复默认布局会从内置 `Resources/FrontedDefaultPositions` 或插件 `FrontedDefaultPositions` 读取。
4. 插件注入控件不显示时检查目标窗口 GUID、Canvas 名称、控件 `Name`、默认位置和插件是否已重启加载。

## 提交前检查

文档改动至少检查：

```powershell
git diff --check
git diff --stat
```

代码改动按风险增加验证：纯函数跑单测；WPF/DI/项目文件跑 build；插件打包改动跑 `dotnet publish -p:CreateZip=true` 验证。
