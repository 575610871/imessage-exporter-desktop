# Changelog

## 0.2.3 — Upstream 4.3.0 follow-up source update

- Keep the 0.2.2 interface, product names, Suoh palette, centering, DPI handling and export settings unchanged.
- Advance the engine from the upstream 4.3.0 tag (`4d90fc8`) to the user-provided develop snapshot `0d444ab` (2026-09-18), six commits later. This includes null NSKeyedArchiver payload handling, URL text fallback fixes and pinned dependency updates.
- Verify the uploaded source's full Git tree and the uploaded Windows EXE's SHA256 against upstream. The EXE is the official 4.3.0 asset; the source snapshot contains later fixes. The bundled engine is rebuilt from that snapshot with the existing password bridge.
- Record provenance in `engine-source.json` and `BUILD-INFO.txt`; stamp the engine as `4.3.0+suoh.0d444ab` instead of leaving upstream's `0.0.0` development placeholders. Preserve third-party dependency versions/checksums and use Cargo's locked build.
- Test both upstream workspace packages, including the new null-payload regressions; verify engine version, GUI CLI options, basic and option-enabled HTML/TXT exports, diagnostics, native runtime dependencies and the extracted portable package.

## 0.2.2 — Suoh Edition product identity

- Name the application, Windows executable, file properties and portable root folder `iMessage Exporter Desktop Suoh Edition` (with `.exe` for the executable).
- Rename the desktop namespace to `IMessageExporterDesktopSuohEdition`, and the model/test namespace roots to `.Model` / `.Tests`; synchronize XAML classes and all references.
- Centralize dialog names and displayed version information; preserve the Suoh palette and responsive controls.
- Use `imessage-exporter-desktop-suoh-edition` for release archive prefixes.
- Add actual executable/assembly/namespace identity checks, then extract and launch the final package from a path containing spaces, with a different working directory; repeat fixture exports with its bundled engine.
- Original engine source, password bridge, export behavior and DPI/centering logic are unchanged.

## 0.2.1 — Executable naming

- Rename the portable GUI executable to `iMessage Exporter Desktop.exe` through its build output name.
- Update the quoted Windows startup-test path, user-facing messages and documentation.
- Keep the independent implementation, upstream engine name, Suoh theme and DPI behavior unchanged.

## 0.2.0 — Suoh independent rewrite

- New standalone projects under `portable/`; no references to the 0.1.x wrapper.
- Build the Rust engine directly from pinned ReagentX source with a small GUI password bridge.
- New Suoh (#992C49) WPF interface, responsive settings, pinned job controls and log view.
- Place the native window inside the cursor monitor's work area, then center; re-check after WPF processes DPI/display changes.
- Self-contained .NET 10 Windows x64 portable ZIP only, with matching source.
- New model tests and real Windows native-window/WPF rendering checks, including honest limits on simulated DPI coverage.

## 0.1.1

- Declare Per-Monitor V2 DPI awareness with compatibility fallback.
- Fit the initial window to the active monitor's available work area.
- Recalculate usable dimensions when moving between monitors with different DPI.
- Switch export options from two columns to one column on narrow windows.
- Add layout sizing tests for normal, high-scale and very small work areas.

## 0.1.0

- Add a Chinese-first Windows desktop interface.
- Detect Apple Devices and iTunes backup locations automatically.
- Map export, filter, attachment and diagnostic options to the upstream engine.
- Keep encrypted-backup passwords out of command-line arguments and settings.
- Add cancellation, logs, validation and one-click output-folder access.
- Add a per-user Inno Setup installer and a portable ZIP build.
