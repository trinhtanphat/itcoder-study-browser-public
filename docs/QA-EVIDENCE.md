# Reproducible QA evidence (15 November 2026)

## Scope
Independent Windows x64 ITCoder Study Browser, version 0.5.9, on WIN11-181.
Not a HUTECH official examination client. No modification to the installed
vendor browser, production deployments or the user's existing desktop Session 2.

## Verified offline build and package results

- .NET 9 build: PASS; independent `ITCoderStudyQA.exe` generated.
- Whole source analysis repository Node tests: 183 PASS, 0 FAIL, 10 SKIP.
- Portable ZIP on source QA worktree: 486 files individually checked SHA-256.
- Packaged EXE: 6/6 no-GUI probes PASS, including preflight refusal in Session 0.
- Compiled C# policy checks: 13/13 PASS.
- Original source QA ZIP SHA256: `31E548375F02360EBA761C5EC9E7DAE90D64C8D46DE908F9DACC15E1FBECA6BE`.
- Original source QA EXE SHA256: `271F403B7AACFB31287F84C9F366E899A96460094D93F2CB6C5A1D26C09C6DE3`.
- All results are offline tests; the original authenticated service was never used.

The public repository provides the same independently authored Windows source,
the build and ZIP verification scripts, and the core package regression test.
The public GitHub prerelease includes independent downloadable artifacts with
their SHA-256. The download can be checked with `Get-FileHash -Algorithm SHA256`.

## Explicitly not proven

- Opening and interacting with native WinForms/WebView2 GUI on a separate QA desktop
- Live navigation and account sign-in/SSO to the HUTECH website
- Vendor secure exam protocol, attestation, or examination approval

These require an authorized isolated interactive desktop and test account.
A headless/Session 0 executable test does not certify any GUI behavior.
