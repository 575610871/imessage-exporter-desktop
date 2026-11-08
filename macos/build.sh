#!/bin/bash
set -euo pipefail
source_dir="${1:?Pass the prepared engine-source directory}"
product='iMessage Exporter Desktop Suoh Edition'
version='0.3.1'
package='imessage-exporter-desktop-suoh-edition'
root="$PWD/build/mac-package/$product"
app="$root/$product.app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources/engine" "$app/Contents/Resources/licenses" build/mac-dist build/mac-binaries
sdk="$(xcrun --sdk macosx --show-sdk-path)"
export MACOSX_DEPLOYMENT_TARGET=13.0
for rust_target in aarch64-apple-darwin x86_64-apple-darwin; do
    cargo build --locked --release --manifest-path "$source_dir/Cargo.toml" --package imessage-exporter --target "$rust_target"
done
for arch in arm64 x86_64; do
    swiftc -parse-as-library -swift-version 5 -O -sdk "$sdk" -target "$arch-apple-macosx13.0" \
        macos/Sources/*.swift -framework SwiftUI -framework AppKit -o "build/mac-binaries/$arch"
done
lipo -create build/mac-binaries/arm64 build/mac-binaries/x86_64 -output "$app/Contents/MacOS/$product"
lipo -create "$source_dir/target/aarch64-apple-darwin/release/imessage-exporter" \
    "$source_dir/target/x86_64-apple-darwin/release/imessage-exporter" -output "$app/Contents/Resources/engine/imessage-exporter"
cp macos/Info.plist "$app/Contents/Info.plist"
cp portable/engine-source.json "$app/Contents/Resources/engine/engine-source.json"
cp LICENSE "$app/Contents/Resources/licenses/SUOH-GPL-3.0.txt"
cp "$source_dir/LICENSE" "$app/Contents/Resources/licenses/UPSTREAM-GPL-3.0.txt"
cp portable/THIRD-PARTY.md "$app/Contents/Resources/licenses/THIRD-PARTY.md"
cp macos/README.md "$root/README.md"
python3 - "$root/BUILD-INFO.txt" <<'PY'
import json, os, pathlib, sys
source = json.loads(pathlib.Path('portable/engine-source.json').read_text())
pathlib.Path(sys.argv[1]).write_text('iMessage Exporter Desktop Suoh Edition 0.3.1\n'
    + 'Desktop: ' + os.environ.get('GITHUB_SHA', 'local checkout') + '\n'
    + 'Engine: ' + source['engine_version'] + '\n'
    + 'Original source: ' + source['source_commit'] + '\n'
    + 'Source tree: ' + source['source_tree'] + '\n'
    + 'Universal macOS 13+: arm64 and x86_64; native SwiftUI; ad-hoc signed, not notarized.\n', encoding='utf-8')
PY
swift macos/Icon.swift build/Suoh.iconset
iconutil -c icns build/Suoh.iconset -o "$app/Contents/Resources/Suoh.icns"
codesign --force --sign - --timestamp=none "$app/Contents/Resources/engine/imessage-exporter"
codesign --force --sign - --timestamp=none "$app"
codesign --verify --strict --verbose=2 "$app"
ditto -c -k --keepParent "$root" "build/mac-dist/$package-$version-macos-universal.zip"
