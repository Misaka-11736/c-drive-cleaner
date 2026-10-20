# Changelog

## [2.0.0] - 2026-10-20

First public release. A complete rewrite of a private PowerShell prototype as a native
WinForms application.

### Added

- Portable single-file GUI (`C盘清理助手.exe`, ~130 KB) built with the in-box C# compiler.
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
