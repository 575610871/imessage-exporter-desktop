# Changelog

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
