#!/bin/sh
set -eu
task_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
task_package="$task_root/native/macos"
task_output="$task_root/bin/macos"
task_app="$task_output/MogooCodexStatus.app"
task_version=$(node -p "JSON.parse(require('fs').readFileSync(process.argv[1],'utf8')).version" "$task_root/package.json")
swift test --package-path "$task_package"
swift build --package-path "$task_package" -c release --arch arm64
task_arm=$(swift build --package-path "$task_package" -c release --arch arm64 --show-bin-path)
swift build --package-path "$task_package" -c release --arch x86_64
task_intel=$(swift build --package-path "$task_package" -c release --arch x86_64 --show-bin-path)
mkdir -p "$task_app/Contents/MacOS" "$task_app/Contents/Resources"
lipo -create "$task_arm/MogooCodexStatus" "$task_intel/MogooCodexStatus" -output "$task_app/Contents/MacOS/MogooCodexStatus"
chmod 755 "$task_app/Contents/MacOS/MogooCodexStatus"
cat > "$task_app/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleIdentifier</key><string>cn.mogoo.codexstatus</string>
<key>CFBundleName</key><string>Codex Status</string>
<key>CFBundleDisplayName</key><string>Codex Status</string>
<key>CFBundleExecutable</key><string>MogooCodexStatus</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleShortVersionString</key><string>$task_version</string>
<key>CFBundleVersion</key><string>$task_version</string>
<key>LSMinimumSystemVersion</key><string>13.0</string>
<key>LSUIElement</key><true/>
<key>NSHighResolutionCapable</key><true/>
<key>NSQuitAlwaysKeepsWindows</key><false/>
</dict></plist>
EOF
codesign --force --options runtime --sign - "$task_app"
codesign --verify --deep --strict "$task_app"
file "$task_app/Contents/MacOS/MogooCodexStatus"
ditto -c -k --keepParent "$task_app" "$task_output/MogooCodexStatus.app.zip"
node "$task_root/scripts/render-macos.mjs" "$task_app/Contents/MacOS/MogooCodexStatus"
printf '%s\n' "$task_output/MogooCodexStatus.app.zip"
