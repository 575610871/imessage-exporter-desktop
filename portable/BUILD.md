# Independent build / 对应源码构建

Prerequisites: Windows x64, .NET 10 SDK, Rust stable (edition 2024), MSVC C++ build tools, Git, PowerShell.

The only desktop projects are `portable/Suoh.App`, `portable/Suoh.Model` and `portable/Suoh.Tests`.
They do not reference or compile the previous desktop application's source. The nearest `portable/Directory.Build.props` is independent.

From a repository checkout:

```powershell
dotnet run --project portable/Suoh.Tests -c Release
powershell -NoProfile -STA -File portable/scripts/Icon.ps1
dotnet publish portable/Suoh.App -c Release -r win-x64 --self-contained true -o build/app -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
git clone https://github.com/ReagentX/imessage-exporter.git engine-source
git -C engine-source checkout 4d90fc8d20a745c0a8acc1e01c0631c4bab89cb4
git -C engine-source apply ../portable/patches/suoh-password.patch
$env:CARGO_TARGET_X86_64_PC_WINDOWS_MSVC_RUSTFLAGS = '-C target-feature=+crt-static'
cargo build --locked --target x86_64-pc-windows-msvc --release --manifest-path engine-source/Cargo.toml --package imessage-exporter
New-Item -ItemType Directory -Force build/app/engine
Copy-Item engine-source/target/x86_64-pc-windows-msvc/release/imessage-exporter.exe build/app/engine/
```

For the corresponding-source ZIP, `desktop/portable` is the new desktop source and `engine-original` is the exact unmodified upstream source. Apply `desktop/portable/patches/suoh-password.patch` once to `engine-original` before compiling. No previous wrapper code is needed. The archive includes the GPL license.

For GUI checks run the published executable with `--ui-test <absolute-output-folder>` using `Start-Process -Wait -PassThru` and inspect its exit code. This mode skips personal preferences and backup scanning, creates synthetic UI screenshots, and does not launch an export.

CI workflow `.github/workflows/suoh-portable.yml` builds, tests and packages this independent application. Release was disabled for the initial visual review. Publishing requires every validation step to pass and never overwrites an existing version.

Windows upstream test notes: checkout with `core.autocrlf=false` to preserve Askama template line endings; the upstream time snapshots expect Pacific time. CI sets that timezone only on its disposable runner. One upstream assertion, `app::runtime::directory_tests::can_get_path_copied`, hardcodes `attachments/d.jpg` where Windows produces `attachments\\d.jpg`; only that assertion is excluded on Windows. Other tests still fail the build if they fail. No production exporter behavior is changed for these test-environment adjustments.

`scripts/verify_engine.py` checks actual PE imports for missing C/C++ runtimes and runs HTML/TXT exports using the upstream public SQLite fixture (macOS database path mode, not a real iPhone backup). This smoke test does not substitute for testing a user's encrypted iOS backup.
