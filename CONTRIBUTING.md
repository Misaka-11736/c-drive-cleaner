# Contributing

Thanks for taking a look. This is a small, deliberately simple project — the bar for a PR
is "does it make the tool safer or clearer?", not "does it add features".

## Build

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

Requires Windows with .NET Framework 4.8 (the in-box `csc.exe`). No SDK, no NuGet, no
Visual Studio.

To build everything that goes into a release:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1 -Version 2.0.0
```

This writes the app, installer, portable ZIP and `SHA256SUMS.txt` into `dist/`.
When bumping the version, update **both** `-Version` and the `Version` constant in
`setup/Installer.cs` (it is shown in the installer window and in *Apps & features*).

## Test

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\test.ps1
```

The test compiles the real `Cleaner` against a temporary sandbox and checks deletion,
path validation and dry-run behaviour. CI runs it on every push.

## Language and style

- The compiler is C# 5 (`csc.exe` from .NET Framework), so:
  - no string interpolation, no `?.`, no `nameof`, no expression-bodied members;
  - use `string.Format` or concatenation;
  - keep `var`/lambdas where they help readability.
- UI strings are Simplified Chinese; code comments are Chinese or English, either is fine.
- No external dependencies. Everything must build with the framework compiler.
- 4 spaces, Allman braces, one type per file where practical.
- Custom-drawn UI: honour `Theme.S()` for pixel sizes so 125%/150% scaling keeps working.

## Safety rules (non-negotiable)

1. **Never weaken `Util.IsSafeDeletePath`.** Additions must be restrictions, not exceptions.
2. **New deletion targets** must be caches, logs, temp files or leftovers that rebuild
   automatically. Add a note explaining what regenerates them, and a risk level.
3. **Personal data stays report-only.** No switch, hidden flag or config that turns
   Documents/Downloads/chat data/game libraries into deletable items.
4. **No silent deletion.** The `DELETE` confirmation and the dry-run path must both keep
   working; do not add a "skip confirmation" option.
5. **No network, no telemetry, no auto-update.**
6. If you touch the deletion engine, extend `tools/CleanTest.cs` with a case that proves
   the new behaviour (including the failure path).

## Pull requests

- One logical change per PR; describe *why*, not just *what*.
- Include the test output if you changed `Cleaner` or `Scanner`.
- Screenshots are welcome for UI changes; please use fake paths (`C:\Users\Demo\…`) so no
  real user names end up in the repository.
- By submitting a PR you agree to license your contribution under the MIT license.

## Reporting bugs

Use the bug report template and include:

- Windows version and build;
- whether the app was running elevated;
- the relevant part of `Documents\C盘清理助手\C-Clean-Log-*.log`;
- what you expected vs. what happened.

For security issues see [SECURITY.md](SECURITY.md) — please do not use public issues.
