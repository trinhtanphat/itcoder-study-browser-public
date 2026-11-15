# GUI acceptance for the public ITCoder Study Browser (15 November 2026)

The independent source, Windows EXE, package and downloaded GitHub asset passed
offline tests. **This is not evidence of interactive GUI or HUTECH login parity.**

The separate GUI harness at `scripts/test-interactive-gui.ps1` is a Windows UI
Automation probe. It only opens the independently authored and SHA-256-pinned
`ITCoderStudyQA.exe` with the synthetic URL `https://example.org/`.

## Running in an independent authorized QA desktop

Use a fresh Windows 11 VM or isolated QA user session. No HUTECH login, exam
client, device attestation or secured exam system is involved.

From a PowerShell prompt **already running inside the dedicated QA desktop**,
with its actual session ID determined from `[Diagnostics.Process]::GetCurrentProcess().SessionId`:

```powershell
& .\scripts\test-interactive-gui.ps1 `
  -ExecutablePath 'C:\qa\published\ITCoderStudyQA.exe' `
  -QaSessionId 1 `
  -ReportPath 'C:\qa\reports\gui-acceptance.json' `
  -ExpectedExeSha256 '3ED8F83BDECC2F4FB8CB7ACA1552BB48B7CD30605CDA47308151ED4A8FAF3062'
```

The output directory must already exist and the JSON report must not exist.
Use the SHA-256 of the build you are actually testing. This example is pinned
to the verified Windows public-source build from 15 November 2026.

The script refuses Session 0, noninteractive sessions, any mismatch between
the specified and actual session, and **Session 2**. In particular, it MUST NOT
be executed in WIN11-181's existing user Session 2.

It waits for a native window to appear; uses Windows UI Automation to inspect
the title, independent-not-exam banner, Back/Forward/Reload/Home/Go buttons and
HTTPS address field; and closes only its own child process. A sanitized JSON
report separates `nativeGuiVerified` and `httpsNavigationObserved`.
Website login and vendor exam parity remain false regardless of GUI result.

## Reference test tools

- Microsoft WinApp CLI's `winapp ui` automation and window inspection:
  https://github.com/microsoft/winappCli/blob/main/docs/ui-automation.md
- Official Playwright WebView2 CDP connection guide:
  https://playwright.dev/docs/webview2

CDP/debugging must only be enabled on synthetic sites in an isolated QA
desktop. Never expose a debug port publicly or use it for real accounts.

## Environment discovery and current execution boundary

- WIN11-181 has no verified dedicated interactive QA desktop besides a user
  Session 2; Windows Sandbox is disabled; do not disturb Session 2.
- Windows VM 100 on Proxmox 161 is running, but QEMU Guest Agent reports no
  logged-in interactive user and its RDP port was not reachable during audit.
  No VM configuration was modified.
- The test harness guard is executed and verified without opening any GUI.
  Until a dedicated interactive test session is available, the GUI test itself
  remains **BLOCKED_NOT_TESTED**. Do not fabricate screenshots or claim GUI PASS.
