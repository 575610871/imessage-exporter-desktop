# Independent build / 对应源码构建

## Pinned engine

`portable/engine-source.json` is the single engine identity record. Version 0.3.0 uses the official **5.0.1** tag at `33c81a90beb151583983fb6ff000fc6f085ec5e8`, tree `38984b747d41ae8eadb10466e8c0d06917886245`.

Fetch with `python3 portable/scripts/fetch_engine.py engine-source`. The script verifies the exact commit and tree, applies `portable/patches/suoh-password.patch`, and runs `portable/scripts/prepare_engine.py`. The latter stamps only the two workspace manifests and matching Cargo.lock records as `5.0.1+suoh.33c81a9`; locked third-party entries remain unchanged. Cargo commands use `--locked`.

The GUI patch enables `SUOH_BACKUP_PASSWORD` when `SUOH_GUI` is set and rejects missing passwords without a hidden terminal prompt. The ordinary CLI behavior remains available. Passwords are never included in GUI process arguments.

## Windows

Prerequisites: Windows x64, .NET 10 SDK, Rust stable / MSVC C++ build tools, Git, PowerShell, Python 3.11+.

```powershell
python portable/scripts/fetch_engine.py engine-source
dotnet run --project portable/Suoh.Tests -c Release
powershell -NoProfile -STA -File portable/scripts/Icon.ps1
dotnet publish portable/Suoh.App -c Release -r win-x64 --self-contained true -o build/app -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
$env:CARGO_TARGET_X86_64_PC_WINDOWS_MSVC_RUSTFLAGS = '-C target-feature=+crt-static'
cargo build --locked --release --target x86_64-pc-windows-msvc --manifest-path engine-source/Cargo.toml --package imessage-exporter
New-Item -ItemType Directory -Force build/app/engine
Copy-Item engine-source/target/x86_64-pc-windows-msvc/release/imessage-exporter.exe build/app/engine/
```

Only `portable/Suoh.App`, `portable/Suoh.Model` and `portable/Suoh.Tests` are compiled. The Windows XAML and window-placement implementation are unchanged from 0.2.3; only build versions and the engine are updated. Product / assembly name: `iMessage Exporter Desktop Suoh Edition`; namespace: `IMessageExporterDesktopSuohEdition`.

CI tests 420 screen geometry combinations and runs the real WPF `--ui-test <absolute-output-directory>` mode. It extracts the ZIP into a folder containing spaces, uses a different working directory, checks the original engine lookup, then repeats public-fixture exports. The test mode avoids personal settings and backup discovery.

Windows Rust tests exclude exactly the two `app::runtime::directory_tests::relative_path_` tests whose assertions hardcode POSIX separators. Source code is not edited for them. All remaining workspace tests must pass. The macOS job runs the complete suite without exclusions. CI preserves LF source files and uses Pacific time for upstream snapshots on its disposable runners.

## macOS

Prerequisites: macOS with Xcode command-line tools / a macOS SDK, Rust stable and Python 3.11+. These are build requirements, not requirements for end users.

```bash
python3 portable/scripts/fetch_engine.py engine-source
rustup target add aarch64-apple-darwin x86_64-apple-darwin
TZ=America/Los_Angeles cargo test --locked --manifest-path engine-source/Cargo.toml --workspace
bash macos/build.sh engine-source
```

The build compiles native SwiftUI/AppKit sources for both `arm64-apple-macosx13.0` and `x86_64-apple-macosx13.0`, builds the original Rust engine for both targets, and combines each pair with `lipo`. It creates a standard `.app` with Info.plist, icon, bundled engine, licenses and identity record. It signs the nested engine and app locally (ad-hoc) and preserves executable permissions with `ditto`. It has no Developer ID certificate or notarization credentials.

The final extracted `.app` executable accepts `--model-test` (argument/date/path/symlink/settings validation, real process wrapper redaction, temp cleanup and cancellation using harmless local scripts) and `--ui-test <absolute-output-directory>` (real native-window placement, title/controls, backing scale, SwiftUI layouts, screenshots and off-screen repair). These modes never scan personal backups or write preferences. CI checks both Mach-O architectures and the local code signature; native execution uses the runner's architecture.

Mac preferences are opt-in in Application Support. The signed app bundle is never modified at runtime. Every export is a local subprocess with individual temporary storage, closed stdin, concurrent-safe bounded logging and cancellation of the owned engine / converter children. Temporary files are cleaned before completion is reported. Interrupted or force-killed apps may leave temporary files.

## Corresponding source archive

`desktop/portable` contains Windows source, engine identity and scripts; `desktop/macos` contains Mac source and packaging; `desktop/.github/workflows/suoh-portable.yml` is the full validation/release workflow. `engine-original` is the unmodified original upstream source. From this source archive, apply `desktop/portable/patches/suoh-password.patch` to `engine-original` once, then run `python3 desktop/portable/scripts/prepare_engine.py engine-original` before compiling. When reproducing CI, use the paths from the repository layout described above. GPL licenses accompany both source and binaries.

`scripts/verify_engine.py` checks engine version, CLI flags and values, Windows PE or Mac linked dependencies, HTML/TXT public-fixture exports, date/name/time switches and diagnostics. It runs on extracted final packages. The fixture uses macOS database mode, not a real encrypted iPhone backup; genuine backup content, media converters and live multi-display DPI changes need user-device verification.

The release job waits for both platforms to succeed, checks all archives, computes the combined SHA256SUMS.txt and publishes a new version without overwriting existing releases.
