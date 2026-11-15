using System;
using System.IO;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace IndependentStudyBrowser;

/// <summary>
/// Offline, non-GUI status snapshot. A successful write confirms only that
/// diagnostics ran; it does not establish that an interactive browser works.
/// No usernames, paths, version strings, URLs or environment variables emitted.
/// </summary>
internal static class StudyBrowserDiagnostics
{
    internal static int Write(string destination)
    {
        try
        {
            int preflight = UiSessionGuard.Preflight();
            bool runtimeAvailable;
            try
            {
                runtimeAvailable = !string.IsNullOrWhiteSpace(
                    CoreWebView2Environment.GetAvailableBrowserVersionString());
            }
            catch (Exception)
            {
                runtimeAvailable = false;
            }

            var status = new
            {
                schemaVersion = 1,
                mode = "independent-study-browser",
                isOfficialExamClient = false,
                webView2RuntimePresent = runtimeAvailable,
                uiPreflightExitCode = preflight,
                uiSessionEligible = preflight == 0,
                browserControllerTested = false,
                guiNavigationVerified = false,
                classification = !runtimeAvailable
                    ? "runtime-unavailable"
                    : preflight == 0
                        ? "eligible-but-gui-unverified"
                        : preflight == UiSessionGuard.SessionZero
                            ? "session-zero-blocked"
                            : "noninteractive-or-unknown-blocked"
            };

            // Complete JSON is flushed to a private scratch file then moved
            // into place without overwriting any preexisting user report.
            StudyAtomicReportWriter.WriteNewJson(destination, status);
            return 0;
        }
        catch (Exception)
        {
            // Do not disclose user paths, security context or exception detail.
            return 50;
        }
    }
}
