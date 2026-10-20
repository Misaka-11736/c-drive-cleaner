# Changelog

## [2.0.1] - 2026-10-20

### Changed

- **DISM component cleanup (COMP-01) is now unchecked by default.** On a 25 GB component
  store it can run for 14+ minutes while freeing only 1–4 GB. It is still available as a
  manual option.
- Cleanup now runs the **largest caches first** and defers DISM / restore points to the end,
  so free space grows immediately instead of appearing stuck on WinSxS.
- The status bar shows **elapsed time** during scan and cleanup.
- The log records **per-item duration**, total duration, and the DISM exit code with its
  runtime.
- DISM is **aborted after 30 minutes** and the reason is written to the log.

## [2.0.0] - 2026-10-20

First public release. A complete rewrite of a private PowerShell prototype as a native
WinForms application.

### Added

- Portable single-file GUI (`C盘清理助手.exe`, ~130 KB) built with the in-box C# compiler.
- Per-user installer (`c-drive-cleaner-2.0.0-setup.exe`): installs to
  `%LOCALAPPDATA%\Programs\C盘清理助手`, adds Start Menu and optional desktop shortcuts, and
  registers a standard uninstaller in *Apps & features*. Installing does not require admin.
- Portable ZIP and `SHA256SUMS.txt` for checksum verification.
- Full C: drive scan: per-folder aggregation (depth 1–4), largest-file ranking, hard-link
  aware totals, denied-path reporting.
- 76 cleanup targets across six categories, with risk levels, recommendations and notes.
- Selection model with per-category filtering; selection persists across filters.
- Whitelist-validated deletion with a mandatory `DELETE` confirmation and a dry-run mode.
- DISM component cleanup, restore-point pruning (keeps the newest) and Recycle Bin emptying.
- HTML report and JSON plan export; plans can be imported (including v1 plans).
- Complete audit log per cleanup run.
- Embedded application icon and DPI-aware layout.
- Sandboxed deletion/safety test (`tools/test.ps1`) and a Windows CI workflow.

### Security

- Personal data (chat apps, Downloads, Desktop, Documents, videos, game libraries, Docker
  images) is report-only and cannot be selected for deletion.
- Reparse points are never traversed.
- A single unsafe path aborts the whole cleanup before anything is deleted.

### Notes

- The program is unsigned; SmartScreen may warn on first launch.
- No quarantine: deletion is permanent.
