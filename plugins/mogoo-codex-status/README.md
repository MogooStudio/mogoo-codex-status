# mogoo-codex-status · Codex 插件

作者：**mogoo** · 网站：[mogoo-codex-status](https://github.com/MogooStudio/mogoo-codex-status)

在普通聊天消息提交时自动显示上下文占用、账号限额剩余比例和重置倒计时。上下文显示为 `上下文 65% / 256k`，支持 Codex 深色／浅色、精简模式、拖动与顶部吸附。

## 安装

需要支持 `codex plugin` 命令的 Codex、Git、Windows 10/11 x64、.NET Framework 4.8 和 Node.js 22.13+。插件内置已构建的状态条和后台服务，不需要手动克隆仓库、pnpm 或编译。

直接从 GitHub 安装：

```powershell
codex plugin marketplace add MogooStudio/mogoo-codex-status --json
codex plugin add mogoo-codex-status@mogoo-codex-status --json
codex plugin list --marketplace mogoo-codex-status --json
```

Agent 收到安装请求时，先核对 `codex plugin --help`、实际 Node 可执行文件版本和 `git --version`。检查已有市场来源；若同名市场指向开发目录或旧的本地发布目录，按下方迁移说明切换到 GitHub。安装后检查完整插件已启用、两条 Hook 已发现。不要将 `codex/skills/` 单独当作完整插件安装，也不要要求用户构建源码。

Codex 会审核插件的 Hook，安装不等于信任。仅在平台显示尚未信任时，请用户审核正式插件的 SessionStart 和 UserPromptSubmit；已信任时继续使用，不重复要求审核。不得绕过或自行写入 Hook 信任状态。审核规则见 [OpenAI 官方 Hook 文档](https://learn.chatgpt.com/docs/hooks)。

也可在包含 `.agents/plugins/marketplace.json` 的已构建发布目录执行：

```powershell
codex plugin marketplace add . --json
codex plugin add mogoo-codex-status@mogoo-codex-status --json
```

这是自定义插件市场安装，不会自动发布到公共插件目录。Codex 使用安装缓存中的完整副本。GitHub 安装从仓库获取更新，移动开发源码不影响已安装副本运行；本地市场安装应保留注册目录作为更新来源。

## 使用

信任 Hook 后，正常发消息即可自动启动；运行中复用单实例。首次启动会把内置运行文件准备到 `%LOCALAPPDATA%\mogoo-codex-status\app`，皮肤、模式、位置与统计保存在上层目录，无需每天调用技能或打开页面。

SessionStart 对开始或恢复聊天提供补充触发，不保证仅打开首页立即显示。启动器不记录消息正文、不读取 Hook 提供的聊天记录路径、不向聊天添加内容；数据查询的 App Server 禁用 Hook，避免递归启动。

自动启动实例跟随所属 Codex 桌面进程退出。插件不注册 Windows 登录或开机启动、启动文件夹、计划任务、服务或外部常驻启动监控器。

需要暂停或恢复时，向 Agent 说明“暂停 mogoo-codex-status”或“恢复 mogoo-codex-status”。管理入口使用安装副本的 `scripts/control.mjs`：`stop` 暂停当前和后续自动启动，`start` 恢复，`prepare` 仅更新运行文件，`install` 准备并立即显示。皮肤、精简模式和位置会保留。

## 更新

```powershell
codex plugin marketplace upgrade mogoo-codex-status --json
codex plugin add mogoo-codex-status@mogoo-codex-status --json
```

新版 Hook 若显示待审核，信任当前定义后继续正常聊天。正在运行的实例会复用；关闭旧实例后，下一条普通消息会准备新版组件并启动。需要立即更新时，可让 Agent 使用新安装副本的管理入口完成。

## 从本地市场迁移

检查 `codex plugin marketplace list --json`；仅当同名市场仍指向旧本地目录时执行：

```powershell
codex plugin marketplace remove mogoo-codex-status --json
codex plugin marketplace add MogooStudio/mogoo-codex-status --json
codex plugin add mogoo-codex-status@mogoo-codex-status --json
```

仅切换此插件的市场来源，原有皮肤、模式、位置和本地统计保持。迁移后检查市场为 GitHub Git 来源、插件启用、正式 Hook 已加载；不得重新设置 Windows 自启动。

## 卸载

先在状态条右键菜单选择“退出状态条”，再运行：

```powershell
codex plugin remove mogoo-codex-status@mogoo-codex-status --json
codex plugin marketplace remove mogoo-codex-status --json
```

也可让 Agent 卸载：先使用安装副本的 `scripts/control.mjs uninstall` 停止组件并关闭聊天触发，再移除插件和市场。卸载保留用户设置；插件卸载后不再接收启动 Hook。
