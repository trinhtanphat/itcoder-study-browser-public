# Independent ITCoder Study Browser for Windows (public QA source)

**Independently authored WebView2/WinForms HTTPS study browser**, buildable as a Windows x64 `.exe` with .NET 9.0. This project is **not** the official HUTECH IT Browser and is **not** certified for secure or protected examination workflows. It does not implement or impersonate vendor browser identity, exam lockdown, attestation, or authentication.

The app provides a normal browser window and optional separate per-user website session profile. User credentials, if entered on a website, are handled by that site in a normal WebView2 session and are not logged by these sources.

## Reproducible build on Windows 11

Requirements: .NET 9 SDK, the Microsoft.Web.WebView2 1.0.4258.31 NuGet package, installed WebView2 Runtime.

```powershell
# From repository root; use a new unique output path for each run
& .\scripts\build-itcoder-qa-portable.ps1 -RepoRoot (Get-Location).Path -Dotnet 'C:\path\to\dotnet.exe' -WebView2Nupkg 'C:\path\to\microsoft.web.webview2.1.0.4258.31.nupkg' -OutputDir 'C:\qa\independent-build-1'
& .\scripts\package-itcoder-qa-release.ps1 -PublishedDirectory 'C:\qa\independent-build-1\published' -OutputDirectory 'C:\qa\independent-release-1' -Version '0.5.9'
```

The release script builds a ZIP with a SHA-256 manifest for each packaged file, extracts the ZIP to a fresh QA directory, and verifies the packaged EXE in offline, non-GUI modes. It **refuses** to run that test gate in an interactive desktop session to avoid interfering with an active user desktop.

The executable's automated `--self-test`, `--runtime-check`, `--verify`, `--diagnose`, and `--ui-preflight` modes can be run without a HUTECH login.

## Public evidence and limitations

See [QA evidence](docs/QA-EVIDENCE.md) and [GUI acceptance instructions](docs/GUI-ACCEPTANCE.md). Release ZIP integrity and offline policy verification are not the same as live GUI and site login acceptance.

**No vendor binaries, installers, decompiled proprietary source, cookies, tokens, student data, or exam-protection bypasses are distributed.** The original analysis repository remains private. Public visibility does **not** grant a software reuse license automatically; vendor rights and trademarks remain with their respective owners.

## Independent second-machine QA

The v0.5.9 release ZIP was also validated inside a separate Windows 11 VM on Proxmox 161: **486/486 manifest files, 13/13 compiled policy checks and 6/6 offline executable probes PASS**. This is independent runtime evidence, not a GUI or secure exam acceptance test. See [the VM161 proof](docs/VM161-QA-20261115.md).
