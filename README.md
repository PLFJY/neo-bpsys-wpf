# neo-bpsys-wpf

powered by <img src="https://raw.githubusercontent.com/PLFJY/neo-bpsys-wpf/refs/heads/main/neo-bpsys-wpf/Assets/logo_net.jpg" width="25px" height="25px"> 9.0 & <img src="https://raw.githubusercontent.com/PLFJY/neo-bpsys-wpf/refs/heads/main/neo-bpsys-wpf/Assets/wpfui.png" width="25px" height="25px">

![GitHub License](https://img.shields.io/github/license/plfjy/neo-bpsys-wpf) ![GitHub Issues or Pull Requests](https://img.shields.io/github/issues/plfjy/neo-bpsys-wpf) ![GitHub Issues or Pull Requests](https://img.shields.io/github/issues-pr/plfjy/neo-bpsys-wpf) ![GitHub forks](https://img.shields.io/github/forks/plfjy/neo-bpsys-wpf?style=flat) ![GitHub Repo stars](https://img.shields.io/github/stars/plfjy/neo-bpsys-wpf?style=flat)

![GitHub Release](https://img.shields.io/github/v/release/plfjy/neo-bpsys-wpf) ![GitHub Downloads (specific asset, all releases)](https://img.shields.io/github/downloads/plfjy/neo-bpsys-wpf/neo-bpsys-wpf_Installer.exe)

[项目官网](https://bpsys.plfjy.top/) | [项目仓库](https://github.com/PLFJY/neo-bpsys-wpf) | [使用文档](https://docs.bpsys.plfjy.top/) | [QQ交流群](https://qm.qq.com/q/uqoK5tMtJQ)

---

## 📖 项目简介

neo-bpsys-wpf 是一个基于 .NET WPF 的**专为第五人格民间赛**开发的 BP 画面直播展示工具，旨在为民间赛事的直播带来更贴近官方职业赛事的直播效果。

本项目的前身 [bp-sys-wpf](https://github.com/plfjy/bp-sys-wpf) 曾帮助了40余个民间赛事，希望它能够在未来帮助到你。

## ✨ 功能

本项目现已涵盖了民间赛直播所需的全部功能，高级功能还在开发当中

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
- [x] 自定义插件及插件市场
- [x] OCR识别自动填充赛后数据
- [x] 全局禁选根据对局进度联动

## ✅ To Do

- [ ] OCR 识别实现全自动 BP 画面切换
- [ ] 应用自带的 3D 展示画面（已有社区插件支持，可前往插件市场下载）

## 📌 开始使用

前往 [Release](https://github.com/PLFJY/neo-bpsys-wpf/releases/latest) 下载最新的版本，或者前往官网下载：https://bpsys.plfjy.top/

更详细的教程请前往文档站查看：[https://docs.bpsys.plfjy.top/](https://docs.bpsys.plfjy.top/)

## 💻 构建

**Release有提前编译好的安装包可以直接下载并安装**

手动构建前需要安装好[.NET 9 SDK](https://dotnet.microsoft.com/zh-cn/download/dotnet/9.0)。

```cmd
git clone https://github.com/PLFJY/neo-bpsys-wpf.git

cd neo-bpsys-wpf
git submodule update --init --recursive
mkdir build

dotnet publish ".\neo-bpsys-wpf\neo-bpsys-wpf.csproj" -c Release -o ".\build\neo-bpsys-wpf"
```

安装包构建

```cmd
".\Installer\Inno Setup 6\ISCC.exe" ".\Installer\build_Installer.iss"
```

备注：此处的Inno Setup是从inno官网下载的官方版本并在文件内附加了中文语言包，后续可能被会分离到另一个仓库内

同时，也可以使用脚本自动构建

```cmd
.\build.bat
```

---


## ⭐ Stargazers over time

[![Stargazers over time](https://starchart.cc/PLFJY/neo-bpsys-wpf.svg?variant=adaptive)](https://starchart.cc/PLFJY/neo-bpsys-wpf)