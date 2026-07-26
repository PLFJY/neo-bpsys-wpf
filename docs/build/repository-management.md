# 仓库分支管理流程规范

## 1. 分支结构说明

- **main 分支**：主分支，存放稳定发布版本，仅通过 PR 合并更新。
- **dev 分支**：开发主分支，存放最新开发代码，基于 main 分支创建。
- **其它分支**：其他功能性分支，基于 dev 分支创建，开发完成后合并至 dev。

## 2. 主版本更新流程（dev 合并至 main）

### 2.1. 提交 PR 合并 dev 到 main

- 在 GitHub 上创建从 dev 到 main 的合并请求（PR）。
- 确保 PR 通过代码审查后，点击 Merge pull request 合并。
- 合并方式选择 Merge。
- 由 Action 自动更新 dev 到最新的 main 上。

## 3. 小 bug 修复流程（直接在 dev 上修改）

### 3.1. 更新本地 dev 分支

```
git checkout dev
git pull --rebase  # 拉取远程更新并变基，保持本地分支整洁
```

### 3.2. 修复 bug 并提交

- 修改代码
- 提交

```
git add .
git commit -m "fix: 修复具体 bug 描述"
```

### 3.3. 推送到远程 dev 分支

```
git push origin dev
```

## 4. 新功能开发流程（基于新的功能分支开发）

### 4.1. 拉取最新 dev 分支

```
git checkout dev
git pull --rebase
```

### 4.2. 创建 feature 分支

```
git checkout -b dev-feat/功能名称
```

### 4.3. 开发功能并提交

开发过程中可多次提交，提交信息需清晰描述改动：

```
git add .
git commit -m "feat: 功能模块-具体功能描述"
```

### 4.4. 同步最新 dev 分支

```
git checkout dev
git pull --rebase
git checkout dev-feat/功能名称
git rebase dev  # 将 feature 分支变基到最新 dev，解决冲突
# 或者
git merge dev  # 将 dev 分支与 feature 分支合并，解决冲突
```

### 4.5. 压缩提交并合并到 dev

压缩 feature 分支的所有提交为一个 commit：

```
git checkout dev
git merge --squash dev-feat/功能名称
git commit -m "feat (作用域): 完整功能描述"  # 编写统一的提交信息
```

### 4.6. 推送到远程 dev 分支并清理

```
git push origin dev
git branch -d dev-feat/功能名称  # 删除本地 feature 分支
git push origin --delete dev-feat/功能名称  # 删除远程 feature 分支
```

## 5. 注意事项

### 5.1. 提交规范

- 使用语义化提交格式（如 `feat:`、`fix:`、`docs:` 等前缀）。
- 最好在 feat、fix、docs 的后面添加作用域（可选）。
- 提交信息需清晰描述改动目的，避免模糊表述。
- 如果自己拿不准可以让 AI 来（比如通义灵码插件）。

详细类型列表见 [commit-convention.md](commit-convention.md)。

### 5.2. 分支命名规范

- 采用行为语义化命名，格式为：`dev-行为/行为名称`。
- 行为列表详见 [Commit 提交规范](commit-convention.md)。
- 例如：`dev-feat/extensions`、`dev-refactor/2.0`。

### 5.3. 分支维护

- 废弃的 feature 分支需及时删除，保持仓库整洁。
