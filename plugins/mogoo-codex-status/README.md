# mogoo-codex-status

**English** | [简体中文](README.zh-CN.md)

Author: **mogoo**

![Status strip preview](docs/images/status-strip.png)

A Windows status strip for Codex: context usage, quota percentages, and reset countdowns. After trusting the plugin's hooks once, ordinary chat messages start the strip automatically. No daily slash commands or panels, and no connection to Windows login or boot startup.

- Follows the foreground Codex window and hides when minimized or another app is active. Does not take keyboard focus.
- Identifies the visible conversation from its accessibility document title and local title history. Does not follow the most recently active background conversation; ambiguous names remain unknown.
- Displays context as text only: `上下文 65% / 256k`, using the reported usage percentage and context capacity (1k = 1000 tokens). Quotas show remaining percentages, and the next reset identifies its owning period. Hover shows approximate usage details, full token counts, and all reset timestamps.
- Queries the official App Server for quotas every 60 seconds, with visibly marked cached snapshots as fallback. Window lengths come from actual data. Reset details use Asia/Shanghai (UTC+8); countdowns stop at minutes, and expired timestamps wait for confirmation.
- Drag freely; release to snap to the top of the Codex window and save the horizontal position, clamped within its sides. Double-click to refresh early, right-click to exit. None of these actions are needed during normal use.
- Two persistent skins: Codex dark (default) and Codex light, using the product-demo palettes from [oil-git](https://oil-oil.github.io/oil-git/). Both keep the same Codex shape, font, 14px radius, 32px height, and 13px text. Click the palette to toggle or select directly from the menu. Legacy light/dark preferences remain compatible. Width is capped at 560px for multiple quotas or 480px for one, with evenly shared gaps.
- Click the left status icon to collapse or expand directly. The 140×28px capsule shows only that icon and “Codex Status”; hover over the label for live metric details. Drag the text or empty space to dock. Mode persists independently of skin; switching keeps the left position unless expansion reaches the window edge. The menu remains available for skins and an alternative mode switch.
- Local statistics and read-only quota queries. The bundled hooks only launch the strip; they do not retain prompts, read the supplied transcript path, or add chat context. No model generation, chat messages, authentication-file reads, or modifications to Codex installation files. Quota App Server runs with hooks disabled.

Requires Windows 10/11 x64, .NET Framework 4.8, and Node.js 22.13+.

```powershell
pnpm build
pnpm test
node scripts/control.mjs install
```

Install the plugin, then review and trust its SessionStart and UserPromptSubmit hooks once. See the [official hook documentation](https://learn.chatgpt.com/docs/hooks). The first ordinary message prepares `%LOCALAPPDATA%\mogoo-codex-status\app` and launches the strip; subsequent messages reuse the running instance. SessionStart supplements this for startup/resume. Opening only the home screen is not guaranteed to launch it.

The automatic instance closes with its owning Codex desktop process. The plugin does not register Run values, Startup-folder shortcuts, scheduled tasks, services, or external startup watchers. `prepare` updates the runtime without launching it; `install` prepares, enables chat-triggered launching, and opens it immediately.

```powershell
node scripts/control.mjs uninstall
```

Uninstall stops the strip and disables future chat-triggered launching; settings are preserved. `stop` also pauses automatic launching; `start` resumes it. Exiting from the strip's menu only closes the current instance. `inspect` reports visible conversation detection, while `status` reports local runtime state. `hook-status.json` keeps only the latest launch metadata.

Disabling or uninstalling the Codex plugin removes future triggers but may leave an existing instance running. Run the runtime uninstall command first when removing the plugin.

Build uses the Windows .NET Framework compiler; no dotnet SDK or frontend dependency installation needed. Generate bundled plugin files with `pnpm marketplace:sync`, then verify with `pnpm marketplace:check`.

Local session formats are version-dependent. Context is the last reported approximate occupancy, not a live per-character value. Duplicate titles remain unknown. Narrow windows may collapse reset text. This is an external overlay, not an injected native Codex status bar. See the [Chinese guide](README.zh-CN.md) for details.

[MIT](LICENSE)
