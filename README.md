# neo-bpsys-wpf

powered by <img src="neo-bpsys-wpf/Assets/logo_net.jpg" width="30px" height="25px"> 9.0.4 & <img src="neo-bpsys-wpf/Assets/wpfui.png" width="30px" height="25px"> 4.0.3

[项目官网](https://bpsys.plfjy.top/) | [备用官网](https://plfjy.github.io/neo-bpsys-website/) | [项目仓库](https://github.com/PLFJY/neo-bpsys-wpf) | [开发文档](https://docs.bpsys.plfjy.top) | [QQ交流群](https://qm.qq.com/q/uqoK5tMtJQ)

## 软件简介

neo-bpsys-wpf是一个基于.NET WPF开发的第五人格直播BP软件，其前身为[bp-sys-wpf](https://github.com/PLFJY/bp-sys-wpf)与[idv-bp-asg-e](https://github.com/PLFJY/idv-bp-asg-e)(均已不再维护）。它由[零风PLFJY](https://plfjy.top/)制作并于2025年6月14日发布v1.0.0.0正式版。其包含地图BP、角色BP与赛后数据显示等在第五人格赛事中能用到的功能。



本项目的前身 [bp-sys-wpf](https://github.com/plfjy/bp-sys-wpf) 曾帮助了40余个民间赛事，希望它能够在未来帮助到你。

## 构建

Release有提前编译好的安装包可以直接下载并安装

手动构建前需要安装好[.NET 9 SDK](https://dotnet.microsoft.com/zh-cn/download/dotnet/9.0)。

```cmd
git clone https://github.com/PLFJY/neo-bpsys-wpf.git
cd neo-bpsys-wpf
mkdir build
dotnet publish ".\neo-bpsys-wpf\neo-bpsys-wpf.csproj" -c Release -o ".\build\neo-bpsys-wpf"
:: Pack installer
".\InstallerGenerate\Inno Setup 6\ISCC.exe" ".\InstallerGenerate\build_Installer.iss"
```

备注：此处的Inno Setup是从inno官网下载的官方版本并在文件内附加了中文语言包，后续可能被会分离到另一个仓库内

同时，也可以使用脚本自动构建

```powershell
.\build.bat
```

## 使用教程

请前往 [使用文档](https://docs.bpsys.plfjy.top)
