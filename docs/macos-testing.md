# macOS 实测

0.3.0 的 macOS 13+ 应用包包含 Apple Silicon 和 Intel 架构。macOS 构建、Swift 测试、签名校验和四种界面原生渲染已经在 [GitHub Actions](https://github.com/MogooStudio/mogoo-codex-status/actions/runs/38205272328) 通过；以下行为需要在安装了 Codex 的真实 Mac 上确认。

## 安装

可直接在 Mac 的 Codex 聊天中说：

```text
请安装 https://github.com/MogooStudio/mogoo-codex-status 的 0.3.0 或更新版本，按仓库说明完成安装并检查 Hook。
```

或者在终端执行（需要 Codex CLI、Git、Node.js 22.13+）：

```sh
codex plugin marketplace add MogooStudio/mogoo-codex-status --json
codex plugin add mogoo-codex-status@mogoo-codex-status --json
```

已经安装时，先运行 `codex plugin marketplace upgrade mogoo-codex-status --json`，再执行第二条安装命令。

在 Codex 审核正式插件的 SessionStart 和 UserPromptSubmit Hook，然后正常发一条消息。首次运行按 macOS 系统提示为 Codex Status 授予辅助功能权限，授权后无需每次操作。应用使用本地签名，尚未公证；如出现首次运行批准提示，按系统单独授权入口处理。

## 确认行为

1. 普通消息自动显示状态条；重复发消息不会生成多个实例。
2. 移动、缩放 Codex 窗口时状态条跟随；切换到其他应用或最小化 Codex 时隐藏。
3. 按住文字或空白处拖动时稳定，松手后吸附到 Codex 顶部；左侧图标切换精简模式，右侧按钮切换深浅色。
4. 切换聊天后上下文对应当前聊天，格式为 `上下文 65% / 256k`。尚未上报用量或标题无法唯一对应时显示未知；限额与重置对应账号实际周期。
5. 退出 Codex 后状态条一并退出；重新打开 Codex 并发消息后自动恢复。

出现问题时，向 Codex 说明现象并要求“诊断 mogoo-codex-status”。本地诊断文件在 `~/Library/Application Support/mogoo-codex-status/`，重点查看 `hook-status.json`、`runtime.json`、`selection.json`。不要发送认证文件或聊天记录正文。
