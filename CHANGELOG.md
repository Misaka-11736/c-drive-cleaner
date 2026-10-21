# Changelog

## [2.2.3] - 2026-10-21

### Added

- Backup drives: only the recycle bin is deletable (typed `DELETE F`); all other items stay
  read-only.
- Settings now shows the installed disk pack version and a **打开下载页** button.

### Fixed

- Duplicate scan raises the candidate cap to 8000 and reports when the cap is hit instead of
  silently truncating.

### Build

- CI checks that PowerShell scripts containing non-ASCII text keep their UTF-8 BOM.

## [2.2.2] - 2026-10-21

### Fixed

- Disk pack cleaner now rejects report-only or non-deletable items at the API level, even if a
  caller passes them by mistake.
- The path guard under `Program Files` now only allows known cache folders
  (`downloading`, `shadercache`, `temp`), so a crafted path cannot target an installed app.
- `SHA256SUMS.txt` now covers only the files actually attached to the release.

### Added

- Main app warns when the installed disk pack version differs from the app version.
- Disk pack warns when Steam is running before cleaning Steam caches.

## [2.2.1] - 2026-10-21

### Fixed

- Disk pack: known cache paths under `Program Files` were rejected by the path guard, so
  Steam's unfinished downloads (`...\Steam\steamapps\downloading`) could be detected but not
  cleaned. The guard now protects the system directories themselves (and one level below),
  while allowing specific cache paths inside them.

## [2.2.0] - 2026-10-21

### Added

- **Optional disk pack** (`c-drive-cleaner-<version>-disks.zip`), a separate small EXE that
  scans other disks: internal data partitions (D:/E:), USB hard drives, USB sticks and SD
  cards. It is not bundled with the main installer and never auto-downloads.
  - **Quick scan**: directory sizes, per-volume recycle bin, Steam shader cache,
    Steam unfinished downloads, download folders.
  - **Deep scan**: additionally finds the largest files, duplicate files (report only) and
    files larger than 1 GB not modified for 2 years.
  - **Only safe items are deletable**: per-volume recycle bin, Steam `shadercache`,
    `downloading` and `temp`. Games, downloads, media and documents are report-only.
  - **Backup drives are detected and locked read-only** (`WindowsImageBackup`, `FileHistory`,
    volume labels containing Backup/Passport/备份).
  - Network drives are not listed; the module makes no network requests.
  - Deleting requires typing `DELETE <letter>` (for example `DELETE D`).
  - Every deletion is logged to `Documents\C盘清理助手\Disk-Clean-Log-*.log`.
- Main app: sidebar entry **磁盘扩展包** which launches the module if present, or explains
  where to download it.

## [2.1.1] - 2026-10-20

### Fixed

- Update check now falls back to following
  `https://github.com/Misaka-11736/c-drive-cleaner/releases/latest` when
  `api.github.com` is blocked (corporate proxies, some networks). The final redirect URL
  carries `/tag/vX.Y.Z`, which is enough to detect and link the new version.

## [2.1.0] - 2026-10-20

### Added

- **Update check.** At most once every 24 hours the app asks the GitHub Releases API for the
  latest stable version and shows a dismissible banner:
  `发现新版本 x.y.z（当前 a.b.c）· 查看 · 忽略此版本`.
  - *查看* opens the release page in the browser — the app **never downloads or executes**
    an update by itself.
  - *忽略此版本* remembers the version and stops nagging for it.
- **Settings dialog** (sidebar → 设置): toggle the automatic check, run a manual
  *立即检查更新*, see the current version.
- `--no-update-check` command-line switch.
- Version number is now derived from `version.txt` / the git tag at build time
  (`src/generated/VersionInfo.g.cs`); the two hard-coded version constants are gone.

### Changed

- Release publishing is fully automated: pushing a `v*` tag makes GitHub Actions build the
  app, installer and portable ZIP, run the safety tests and publish a GitHub Release.

### Notes

- This is the first network behaviour in the project: one HTTPS `GET` per day to
  `api.github.com`, no telemetry, fully optional.

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
