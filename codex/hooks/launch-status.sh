#!/bin/sh
# Finder-launched apps may not inherit the user's shell PATH.
task_node=$(command -v node 2>/dev/null || true)
task_settings="$HOME/Library/Application Support/mogoo-codex-status/config.json"
if [ -f "$task_settings" ]; then
  task_saved=$(/usr/bin/plutil -extract nodePath raw -o - "$task_settings" 2>/dev/null || true)
  if [ -x "$task_saved" ]; then task_node=$task_saved; fi
fi
if [ -z "$task_node" ]; then
  for task_candidate in /opt/homebrew/bin/node /usr/local/bin/node "$HOME/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node" "$HOME"/.nvm/versions/node/*/bin/node; do
    if [ -x "$task_candidate" ]; then task_node=$task_candidate; break; fi
  done
fi
task_root=${PLUGIN_ROOT:-$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)}
if [ -n "$task_node" ]; then
  exec "$task_node" "$task_root/hooks/launch-status.mjs"
fi
# Never block a chat or add model context when a dependency is missing.
task_data="$HOME/Library/Application Support/mogoo-codex-status"
/bin/mkdir -p "$task_data"
printf '%s\n' '{"origin":"codex-hook","result":"node-missing","requirement":"Node.js 22.13+"}' > "$task_data/hook-status.json"
exit 0
