---
name: status-panel
description: 安装、启停、更新或诊断 mogoo-codex-status 常驻状态条。只在用户要求管理状态条时使用；日常显示由后台组件自动完成。
---

# 管理常驻状态条

从本技能所在位置向上三级定位安装插件根目录，确认存在 `scripts/control.mjs`、`server/` 与本机对应的原生运行包：Windows 为 `bin/MogooCodexStatus.exe`，macOS 为 `bin/macos/MogooCodexStatus.app.zip`。使用安装副本，不调用开发仓库或在插件缓存中构建。

新安装与更新采用 GitHub 自定义市场，与 mogoo-codex-git 一致：`codex plugin marketplace add MogooStudio/mogoo-codex-status --json`，再执行 `codex plugin add mogoo-codex-status@mogoo-codex-status --json`。同名市场指向本地目录时，先只移除该市场注册，再添加 GitHub 来源。更新使用 `codex plugin marketplace upgrade mogoo-codex-status --json` 和重新安装命令。完整步骤见插件根的 `README.md`，无需让用户手动克隆或构建。

本版支持 Windows 和 macOS，需 Node.js 22.13+。macOS 还需首次辅助功能授权；使用系统提示，不代替用户更改授权或关闭安全设置。日常由插件内置的 SessionStart（startup/resume）和 UserPromptSubmit Hook 自动拉起；两条 Hook 须经用户一次性审核信任。普通消息会复用已运行的状态条，不向聊天注入输出。以实际 Node 可执行文件运行 `<插件根>/scripts/control.mjs`：

- 用户要求安装或完成插件设置：执行 `install`，复制运行文件到 LocalAppData、启用聊天事件自动启动并立即启动。只准备运行文件、不立即显示时执行 `prepare`。用户信任 Hook 后，首次普通消息也能自动准备组件。
- 用户要求暂停或停止：执行 `stop`，停止当前状态条并将 `autoLaunchEnabled` 设为 false，后续消息不会重新启动。要求恢复：执行 `start`。
- 用户要求移除后台组件：执行 `uninstall`，停止状态条并关闭聊天事件自动启动，保留设置。随后按用户要求卸载 Codex 中的插件。
- 用户要求诊断：执行 `inspect` 检查当前窗口标题、Codex 模式和前台状态；执行 `status` 检查数据状态。诊断数据保留本地，不读取或输出认证文件及会话消息正文。
- 用户要求更新后台组件：在新的插件安装副本执行 `install`，替换稳定运行目录。

确认正式插件的两条 Hook 已被发现、启用并信任；新定义未信任时明确说明平台需要一次性审核，不能绕过或写入信任状态。确认 LocalAppData 下的 `runtime.json`、`state.json` 更新，明确显示数据是最新查询或缓存。`hook-status.json` 仅保留最近一次触发元数据。聊天重名、标题无法对应、未上报 Token 时保留未知状态，不选择最近运行的聊天替代。上下文是最近上报的近似占用，不能承诺与 Codex 原生显示逐字一致。

运行中的指标由后台组件持续更新，Hook 仅负责启动。不要要求用户每次调用技能、输入斜杠命令或打开网页。不要自动操作 Codex 的聊天、发送消息或修改原生安装文件。不得注册登录或开机启动、启动文件夹快捷方式、计划任务、LaunchAgent、登录项、服务或外部常驻启动监控器。自动启动实例在所属 Codex 桌面进程退出时关闭。
