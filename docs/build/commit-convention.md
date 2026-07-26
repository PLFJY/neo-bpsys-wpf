# Commit 提交规范

**推荐直接使用 IDE 内的 AI 自动生成 commit 记录**（VS Code、装了通义灵码的 Rider 都有，AI 写的足够清楚，无需按照以下规范）。

**如果要手写，请遵循以下规范：**

```
类型(作用域):

内容，如果是 bug fix，说明解决方案
```

类型和作用域可以有多个，逗号 + 空格隔开，可以只写对应功能 Page/Window 的名称或者对应模块的名称，如：`BanHunPage`、`DesignMode`。

若出现命名空间、接口上的更改需要在 Commit Message 的第一行添加 `BREAKING CHANGE`，一般只有新大版本研发才会需要到。

**小版本更新时如果需要更改名称或者删除某个 API，请使用 [Obsolete Attribute](https://learn.microsoft.com/dotnet/api/system.obsoleteattribute?view=net-9.0)，并保留旧的实现或者映射到新的实现。**

Example:

```
BREAKING CHANGE refactor(Timer, DesignMode):
计时器获取改为 Messenger 通知改变，IsDesignMode 变化由原来的事件通知改为 Messenger 通知
```

## 类型列表

| 类型 | 说明 |
| --- | --- |
| feat | 新功能 |
| fix | 修复 bug |
| refactor | 重构 |
| docs | 改文档，例如 README |
| style | 代码风格改变，不影响功能 |
| temp | 临时提交，用于合作开发同一分支 |
| chore | 杂项，例如 .gitignore、github workflow |
| build | 更改构建脚本、构建工具 |
| revert | 回滚 |
| merge | 合并 |
| improve | 提升 |
| i18n | 国际化 |
