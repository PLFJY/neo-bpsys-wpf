# neo-bpsys-wpf

powered by <img src="https://raw.githubusercontent.com/PLFJY/neo-bpsys-wpf/refs/heads/main/neo-bpsys-wpf/Assets/logo_net.jpg" width="25px" height="25px"> 9.0.4 & <img src="https://raw.githubusercontent.com/PLFJY/neo-bpsys-wpf/refs/heads/main/neo-bpsys-wpf/Assets/wpfui.png" width="25px" height="25px"> 4.0.3

![GitHub License](https://img.shields.io/github/license/plfjy/neo-bpsys-wpf) ![GitHub Issues or Pull Requests](https://img.shields.io/github/issues/plfjy/neo-bpsys-wpf) ![GitHub Issues or Pull Requests](https://img.shields.io/github/issues-pr/plfjy/neo-bpsys-wpf) ![GitHub forks](https://img.shields.io/github/forks/plfjy/neo-bpsys-wpf?style=flat) ![GitHub Repo stars](https://img.shields.io/github/stars/plfjy/neo-bpsys-wpf?style=flat)

![GitHub Release](https://img.shields.io/github/v/release/plfjy/neo-bpsys-wpf) ![GitHub Downloads (specific asset, all releases)](https://img.shields.io/github/downloads/plfjy/neo-bpsys-wpf/neo-bpsys-wpf_Installer.exe)

[项目官网](https://bpsys.plfjy.top/) | [备用官网](https://plfjy.github.io/neo-bpsys-website/) | [项目仓库](https://github.com/PLFJY/neo-bpsys-wpf) | [使用文档](https://docs.bpsys.plfjy.top/docs/neo-bpsys-wpf%E4%BD%BF%E7%94%A8%E6%96%87%E6%A1%A3/%E5%89%8D%E8%A8%80) | [开发文档](https://docs.bpsys.plfjy.top/docs/%E5%BC%80%E5%8F%91%E6%96%87%E6%A1%A3) | [QQ交流群](https://qm.qq.com/q/uqoK5tMtJQ)

---

## :book: 项目简介

neo-bpsys-wpf 是一个基于 .NET WPF 开发的第五人格直播BP软件，其前身为[bp-sys-wpf](https://github.com/PLFJY/bp-sys-wpf)与[idv-bp-asg-e](https://github.com/PLFJY/idv-bp-asg-e)(均已分别因为屎山代码和技术栈老旧停止维护）。其包含地图BP、角色BP与赛后数据显示等在第五人格赛事中能用到的功能。

本项目的前身 [bp-sys-wpf](https://github.com/plfjy/bp-sys-wpf) 曾帮助了40余个民间赛事，希望它能够在未来帮助到你。

## :sparkles: 功能

- [x] 基础BP界面
- [x] 过场画面
- [x] 比分
  - [x] 游戏内比分
  - [x] 分数统计（用于场间）
- [x] 赛后数据
- [x] 地图BP
- [x] BP概览（用于场间）
- [x] 后台引导式BP导播
- [x] 队员管理
  - [x] 选手定妆照
  - [x] 选手数量动态调整
- [x] 前台UI自定义
  - [x] 控件位置自定义
  - [x] 文字颜色、字体、大小自定义
  - [x] UI背景图片自定义

## :white_check_mark: To Do

- [ ] OCR识别自动填充赛后数据
- [ ] OCR识别实现全自动BP
- [ ] 全局禁选根据对局进度联动

## :computer: 构建

Release有提前编译好的安装包可以直接下载并安装

手动构建前需要安装好[.NET 9 SDK](https://dotnet.microsoft.com/zh-cn/download/dotnet/9.0)。

```cmd
git clone https://github.com/PLFJY/neo-bpsys-wpf.git

cd neo-bpsys-wpf
mkdir build

dotnet publish ".\neo-bpsys-wpf\neo-bpsys-wpf.csproj" -c Release -o ".\build\neo-bpsys-wpf"
```
安装包构建
```cmd
".\InstallerGenerate\Inno Setup 6\ISCC.exe" ".\InstallerGenerate\build_Installer.iss"
```

备注：此处的Inno Setup是从inno官网下载的官方版本并在文件内附加了中文语言包，后续可能被会分离到另一个仓库内

同时，也可以使用脚本自动构建

```cmd
.\build.bat
```

---


## :star: Stargazers over time
[![Stargazers over time](https://starchart.cc/PLFJY/neo-bpsys-wpf.svg?variant=adaptive)](https://starchart.cc/PLFJY/neo-bpsys-wpf)