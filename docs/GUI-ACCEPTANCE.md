# GUI acceptance: reproducible but **not yet executed**

This project separates build/offline verification from GUI and live website acceptance.

## Conditions for a legitimate interactive test

- Run in a fresh, authorized **interactive Windows desktop**, separate from user Session 2.
- Never open, modify or bypass the original vendor examination client.
- Use a dedicated test profile and only a site/account you are authorized to access.
- Start independent `ITCoderStudyQA.exe --url https://example.org/` or use a
  synthetic HTTPS QA page; confirm window title, toolbar Back/Forward/Reload,
  text entry, TLS error handling and app exit without touching other sessions.
- For signed-in website testing, log in manually with an authorized test account;
  do not collect/export login credentials, cookies or exam session tokens.
- Capture screenshots and sanitized logs only after the test; keep user identities
  and cookies out of all public artifacts.

## Public testing tools used as references

- Playwright's official WebView2 automation guide: https://playwright.dev/docs/webview2
- Microsoft's native Windows UI Automation guidance: https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-ui-automation
- Microsoft's winapp UI skill (Windows Sandbox recommendation):
  https://github.com/microsoft/winappCli/blob/main/plugins/winui/agent-plugin/skills/winui-ui-testing/SKILL.md

Playwright can attach to a test WebView2 instance via loopback CDP if the
test process explicitly enables remote debugging. Do not expose the debugging
port publicly or enable it for privileged/sensitive browsing sessions.

## Current blocker on WIN11-181

The only connected interactive desktop worker is Session 2 (user's active session).
The Windows Sandbox optional feature is disabled, and no separate interactive
QA session is available. The QA application must not be launched into Session 2.
GUI / login / official exam parity results remain **NOT VERIFIED**, not PASS.
