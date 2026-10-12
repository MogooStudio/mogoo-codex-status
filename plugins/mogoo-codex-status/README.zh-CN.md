# mogoo-codex-status

[English](README.md) | **简体中文**

作者：**mogoo** · 网站：[mogoo-codex-status](https://github.com/MogooStudio/mogoo-codex-status)

![状态条预览](docs/images/status-strip.png)

Codex 状态条。一次性信任插件 Hook 后，普通消息自动启动用量显示，无需斜杠命令或手动打开界面。启动只由 Codex 聊天事件触发，与 Windows 登录、开机启动无关。

## 功能

- 在 Codex 窗口标题栏附近显示紧凑状态条，跟随移动和缩放；Codex 最小化或切到其他应用时隐藏，不抢输入焦点。
- 从辅助功能文档标题识别当前显示的聊天，用本地标题历史处理自动命名不同步。后台聊天更新不会切换上下文；重名或无法匹配时显示等待状态。
- 上下文按 `上下文 65% / 256k` 仅显示文字：百分比为最近上报的近似已用比例，取整数；容量按实际上下文窗口换算为 k（1k = 1000 Token），最多一位小数。悬停查看精确比例、完整 Token 用量和上报时间。累计消耗不作为上下文占用。
- 每 60 秒通过官方 App Server 读取账号限额，用“限额剩余”统一标注真实周期的剩余百分比，支持多个限额桶。接口失败时使用本地快照，过期显示“缓存”。
- 重置显示所属周期与分钟级倒计时；悬停查看所有窗口的完整北京时间。到零后显示“待确认重置”，不推算额度恢复。
- 拖动时自由移动，松手后自动吸附到 Codex 窗口顶部并保存横向位置；拖出左右边界会收回。双击请求提前刷新；右键退出。不需要日常操作。
- 仅保留 Codex 深色（默认）与 Codex 浅色两套皮肤，参考 [oil-git](https://oil-oil.github.io/oil-git/) 产品演示的色调；字体、14px 圆角和控件样式统一。调色盘按钮单击切换，右键直接选择，重启后保留选择。双限额最多 560px，单限额最多 480px，信息组间距均分；高度 32px、字号 13px。
- 点击左侧状态图标直接收起／展开。精简模式只显示图标与 `Codex Status`，尺寸 140×28px；悬停文字查看上下文、限额与重置详情，按住文字或空白处拖动。模式与皮肤独立保存，切换保持左侧位置，展开超出窗口时收回边界；右键仍可换肤或备用切换模式。
- 只读本机统计，后台运行不发聊天消息、不调用生成模型、不修改 Codex 安装文件。

## 环境

Windows 10/11 x64、.NET Framework 4.8 和 Node.js 22.13+。开发构建使用 Windows 自带 .NET Framework 编译器，不依赖 dotnet SDK 或前端依赖。聊天事件启动方案已在 Codex Desktop 26.1007.2314.0、Codex CLI 0.162.0-alpha.17.2 验证。

## 安装

把这句话交给支持安装 Codex 插件的 Agent：

```text
请帮我安装这个 Codex 插件：https://github.com/MogooStudio/mogoo-codex-status
```

或者在终端运行：

```powershell
codex plugin marketplace add MogooStudio/mogoo-codex-status --json
codex plugin add mogoo-codex-status@mogoo-codex-status --json
```

需要支持 `codex plugin` 命令的 Codex、Git、Windows 10/11 x64、.NET Framework 4.8 和 Node.js 22.13+。插件内置构建好的原生状态条和后台服务，安装时无需手动克隆、pnpm 或构建。更新、从本地市场迁移与卸载见[插件说明](README.md)。

## 使用

安装后，在 Hook 审核入口一次性信任正式插件的 SessionStart 和 UserPromptSubmit 两条 Hook，然后正常聊天即可。首次普通消息会自动准备运行文件并启动状态条，运行中直接复用单实例。审核规则见 [OpenAI 官方 Hook 文档](https://learn.chatgpt.com/docs/hooks)。

SessionStart 对开始或恢复会话提供补充触发；保证的日常入口是普通消息，不承诺仅打开首页就立即显示。启动器不记录消息正文，不读取 Hook 传入的聊天记录路径，也不向聊天添加内容。账号限额查询使用禁用 Hook 的 App Server，避免递归启动。

运行文件保存到 `%LOCALAPPDATA%\mogoo-codex-status\app`，设置和统计保存在上层目录。自动启动实例绑定发起启动的 Codex 桌面进程，该进程退出时状态条和它的统计后台一起退出。插件不会注册 Windows Run 项、启动文件夹、计划任务或服务，也没有登录后运行的监控器。插件更新后可运行 `prepare` 更新稳定运行目录，或让下一次聊天触发自动更新。

需要暂停、恢复或卸载时，在聊天中要求管理 `mogoo-codex-status` 即可；这些操作会保留皮肤、模式和位置设置。右键“退出状态条”只结束当前实例，后续消息可再次拉起。

## 开发与验证

```powershell
pnpm build
pnpm test
pnpm test:drag
pnpm test:skins
pnpm inspect
pnpm marketplace:sync
pnpm marketplace:check
```

修改源码或插件说明后，先运行 `pnpm build`，再运行 `pnpm marketplace:sync`，将生成的 `plugins/mogoo-codex-status/` 与 `.agents/plugins/marketplace.json` 一起提交。分发文件由源码生成，不要直接编辑；GitHub 安装使用这些内置运行文件。

拖动回归检查覆盖按住不动、移动后停住、松手和捕获丢失、多 DPI 下的位置保存与顶部吸附、左右边界收回，以及按住时暂停自动定位、松手后恢复。检查使用隐藏的测试窗口，不移动用户鼠标。

皮肤检查覆盖两个可选主题、旧设置兼容、实际按钮及菜单选择、精简／完整模式切换、重启读回、保存失败恢复、按住期间阻止模式变更、保留吸附设置、按钮与拖动隔离、文字对比度，以及两套皮肤在四种宽度和四种数据状态下的布局。原生预览见 [皮肤对比](docs/skin-preview.html)。

## 数据与限制

- 状态文件只包含聊天标题、会话 ID、Token 数字和限额统计；不会复制聊天正文或认证信息。不对外提供 HTTP 服务。
- 本地 `state_*.sqlite`、`session_index.jsonl` 和聊天记录格式不是稳定公共接口，升级可能需要适配。SQLite 始终以只读方式打开。
- 上下文来自最近一次 `token_count` 上报，不是每个生成字符的实时值，也不保证与 Codex 原生保留空间计算完全一致。缺失数据不会当成 0%。
- 可见标题重名时保留未知状态，不根据最近更新时间猜聊天。多个窗口时优先前台窗口；无法确定窗口时隐藏。
- 窄窗口中可能收起重置文字，悬停查看完整信息。页面显示完整窗口数据时最多同时展示两个限额窗口；更多窗口在完整状态数据中保留。
- 本版是 Windows 悬浮条，未向 Codex 原生界面注入元素。支持中文和英文的 Codex 模式标识；其他语言需适配。

[MIT](LICENSE)
