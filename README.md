# mogoo-codex-status

**English** | [简体中文](README.zh-CN.md)

Author: **mogoo** · Website: [mogoo-codex-status](https://github.com/MogooStudio/mogoo-codex-status)

![Status strip preview](docs/images/status-strip.png)

A macOS and Windows status strip for Codex: context usage, quota percentages, and reset countdowns. After trusting the plugin's hooks once, ordinary chat messages start the strip automatically. No daily slash commands or panels, and no connection to OS login or boot startup.

- Follows the foreground Codex window and hides when minimized or another app is active. Does not take keyboard focus.
- Identifies the visible conversation from its accessibility document title and local title history. Does not follow the most recently active background conversation; ambiguous names remain unknown.
- Displays context as text only: `上下文 65% / 256k`, using the reported usage percentage and context capacity (1k = 1000 tokens). Quotas show remaining percentages, and the next reset identifies its owning period. Hover shows approximate usage details, full token counts, and all reset timestamps.
- Queries the official App Server for quotas every 60 seconds, with visibly marked cached snapshots as fallback. Window lengths come from actual data. Reset details use Asia/Shanghai (UTC+8); countdowns stop at minutes, and expired timestamps wait for confirmation.
- Drag freely; release to snap to the top of the Codex window and save the horizontal position, clamped within its sides. Double-click to refresh early, right-click to exit. None of these actions are needed during normal use.
- Two persistent skins: Codex dark (default) and Codex light, using the product-demo palettes from [oil-git](https://oil-oil.github.io/oil-git/). Both keep the same Codex shape, font, 14px radius, 32px height, and 13px text. Click the palette to toggle or select directly from the menu. Legacy light/dark preferences remain compatible. Width is capped at 560px for multiple quotas or 480px for one, with evenly shared gaps.
- Click the left status icon to collapse or expand directly. The 140×28px capsule shows only that icon and “Codex Status”; hover over the label for live metric details. Drag the text or empty space to dock. Mode persists independently of skin; switching keeps the left position unless expansion reaches the window edge. The menu remains available for skins and an alternative mode switch.
- Local statistics and read-only quota queries. The bundled hooks only launch the strip; they do not retain prompts, read the supplied transcript path, or add chat context. No model generation, chat messages, authentication-file reads, or modifications to Codex installation files. Quota App Server runs with hooks disabled.

## Install

Give this prompt to an agent that can install Codex plugins:

```text
Please install this Codex plugin: https://github.com/MogooStudio/mogoo-codex-status
```

Or run in your terminal:

```powershell
codex plugin marketplace add MogooStudio/mogoo-codex-status --json
codex plugin add mogoo-codex-status@mogoo-codex-status --json
```

Requires Codex with the `codex plugin` commands, Git, and Node.js 22.13+. Windows requires Windows 10/11 x64 and .NET Framework 4.8. macOS requires macOS 13+; the native application is universal for Apple Silicon and Intel. Both native applications and the shared service are bundled; installation needs no manual clone, pnpm, Xcode, or build. See the [plugin guide (Chinese)](codex/PLUGIN-README.md) for updates, migration, and removal.

## Usage

Review and trust the installed plugin's SessionStart and UserPromptSubmit hooks once. See the [official hook documentation](https://learn.chatgpt.com/docs/hooks). On macOS, approve the system Accessibility prompt once so the strip can read Codex's window position and document title. It does not read message text through Accessibility. The first ordinary message prepares the local runtime and launches the strip; subsequent messages reuse the running instance. SessionStart supplements this for startup/resume. Opening only the home screen is not guaranteed to launch it.

The automatic instance closes with its owning Codex desktop process. The plugin does not register Run values, Startup-folder shortcuts, scheduled tasks, LaunchAgents, login items, services, or external startup watchers. Data lives in `%LOCALAPPDATA%\mogoo-codex-status` on Windows or `~/Library/Application Support/mogoo-codex-status` on macOS. `prepare` updates the runtime without launching it; `install` prepares, enables chat-triggered launching, and opens it immediately.

Ask Codex to manage `mogoo-codex-status` when you need to pause, resume, or remove it. Settings are preserved. Exiting from the strip's menu only closes the current instance. `hook-status.json` keeps only the latest launch metadata.

## Development

```powershell
pnpm build
pnpm build:macos # Run on macOS with Xcode Command Line Tools
pnpm test
pnpm test:drag
pnpm test:skins
pnpm marketplace:sync
pnpm marketplace:check
```

Build uses the Windows .NET Framework compiler; no dotnet SDK or frontend dependency installation is needed. After changing source or plugin instructions, build and run `pnpm marketplace:sync`, then commit the generated `plugins/mogoo-codex-status/` and `.agents/plugins/marketplace.json` together. Verify them with `pnpm marketplace:check`; do not edit generated files directly.

Local session formats are version-dependent. Context is the last reported approximate occupancy, not a live per-character value. Duplicate titles remain unknown. Narrow windows may collapse reset text. This is an external overlay, not an injected native Codex status bar. macOS bundles use local ad-hoc signing and are not notarized; follow macOS's first-run security prompts if shown, without disabling system security. See the [Chinese guide](README.zh-CN.md) for details.

[MIT](LICENSE)
