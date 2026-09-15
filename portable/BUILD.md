# Independent build / 对应源码构建

Prerequisites: Windows x64, .NET 10 SDK, Rust stable (edition 2024), MSVC C++ build tools, Git, PowerShell.

The only desktop projects are `portable/Suoh.App`, `portable/Suoh.Model` and `portable/Suoh.Tests`.
They do not reference or compile the previous desktop application's source. The nearest `portable/Directory.Build.props` is independent.

The application sets `AssemblyName`, `Product` and `Title` to `iMessage Exporter Desktop Suoh Edition`, producing **iMessage Exporter Desktop Suoh Edition.exe**. Its `RootNamespace` and XAML/code-behind namespace are `IMessageExporterDesktopSuohEdition`; the model uses `IMessageExporterDesktopSuohEdition.Model`, and the tests use the `.Tests` root/assembly name. Existing source-project directory names are retained to keep build paths stable; they do not control namespaces. The original `engine/imessage-exporter.exe` name is unchanged.

The portable ZIP contains exactly one root folder named **iMessage Exporter Desktop Suoh Edition**, without a version suffix. Release archive filenames use the ASCII prefix `imessage-exporter-desktop-suoh-edition` plus version/platform. Quote executable paths and arguments containing spaces in PowerShell.

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

For GUI checks run the published executable with `--ui-test <absolute-output-folder>` using `Start-Process -Wait -PassThru` and inspect its exit code. This mode skips personal preferences and backup scanning, creates synthetic UI screenshots, and does not launch an export. It also checks the actual executable name, assembly/product metadata, namespaces and window title. CI extracts the final ZIP and repeats these checks with spaces in both the program path and report path, while using a different working directory; it then verifies the packaged engine using the public fixture.

CI workflow `.github/workflows/suoh-portable.yml` builds, tests and packages this independent application. Release was disabled for the initial visual review. Publishing requires every validation step to pass and never overwrites an existing version.

Windows upstream test notes: checkout with `core.autocrlf=false` to preserve Askama template line endings; the upstream time snapshots expect Pacific time. CI sets that timezone only on its disposable runner. An upstream assertion under `app::runtime::directory_tests::can_get_path_copied` hardcodes `attachments/d.jpg` where Windows produces `attachments\\d.jpg`. The `--skip` prefix matches two tests, so two tests are filtered out on Windows; all remaining tests must pass. No production exporter behavior is changed for these test-environment adjustments.

`scripts/verify_engine.py` checks actual PE imports for missing C/C++ runtimes and runs HTML/TXT exports using the upstream public SQLite fixture (macOS database path mode, not a real iPhone backup). This smoke test does not substitute for testing a user's encrypted iOS backup.
