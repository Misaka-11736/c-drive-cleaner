# Security Policy

## Supported versions

| Version | Supported |
|---------|-----------|
| 2.x     | ✅        |

## Reporting a vulnerability

Please **do not open a public issue** for security problems.

Use GitHub's private vulnerability reporting:
**Security → Report a vulnerability** on this repository.

Include the version, a description, reproduction steps and, if possible, a minimal plan
file or path that triggers the problem.

## What counts as a security issue

This program deletes files by design; "it deleted my cache" is not a vulnerability.
Things that *are* in scope:

- a path that can be deleted outside the documented target list;
- a way to bypass `IsSafeDeletePath` (traversal, wildcard, reparse-point, encoding tricks);
- deletion of personal-data directories, which are report-only by design;
- a TOCTOU issue where the path validated is not the path deleted;
- privilege escalation beyond the elevated instance the user approved;
- anything that writes outside `Documents\C盘清理助手\` unexpectedly.

## Verifying a build

The binary is not code-signed. To be sure it matches this source:

1. Build it yourself: `powershell -ExecutionPolicy Bypass -File .\build.ps1`
2. Or use the artifact produced by the `build` GitHub Actions workflow, which compiles the
   exact commit and runs `tools/test.ps1`.

The application makes no network requests at runtime.
