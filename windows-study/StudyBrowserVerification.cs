using System;
using System.IO;
using System.Text.Json;

namespace IndependentStudyBrowser;

/// <summary>
/// Independently executable, deterministic unit-policy checks. They do not
/// open a window, network connection, stored browser profile or exam software.
/// </summary>
internal static class StudyBrowserVerification
{
    private static readonly (string Name, Func<int> Run)[] Checks =
    {
        ("https-navigation-policy", StudyNavigationPolicy.SelfTest),
        ("https-url-boundary-corpus", StudyNavigationPolicy.CorpusSelfTest),
        ("windows-ui-session-guard", UiSessionGuard.SelfTest),
        ("browser-lifecycle", StudyBrowserLifecycle.SelfTest),
        ("isolated-profile-policy", StudyBrowserProfilePolicy.SelfTest),
        ("validated-https-launch-options", StudyBrowserLaunchOptions.SelfTest),
        ("exclusive-saved-profile-lease", StudySavedProfileLease.SelfTest),
        ("webview2-process-failure-policy", StudyWebView2FailurePolicy.SelfTest),
        ("webview2-certificate-errors-denied", StudyWebView2CertificatePolicy.SelfTest),
        ("webview2-frame-and-resource-https-policy", StudyWebView2NetworkPolicy.SelfTest),
        ("atomic-report-file-integrity", StudyAtomicReportWriter.SelfTest),
        ("temporary-profile-scope-and-cleanup", StudyTemporaryProfileCleanup.SelfTest),
        ("temporary-profile-ownership-inventory", StudyTemporaryProfileInventory.SelfTest)
    };

    internal static int WriteReport(string destination)
    {
        try
        {
            string fullPath = Path.GetFullPath(destination);
            // Execute and serialize all checks before creating an output file.
            // This avoids leaving a truncated report if a check or serializer throws.
            // Never read, replace or delete an existing user report/profile.
            var results = new object[Checks.Length];
            bool passed = true;
            for (int i = 0; i < Checks.Length; i++)
            {
                bool itemPassed;
                try { itemPassed = Checks[i].Run() == 0; }
                catch (Exception) { itemPassed = false; }
                passed &= itemPassed;
                results[i] = new { name = Checks[i].Name, passed = itemPassed };
            }
            var report = new
            {
                schemaVersion = 1,
                mode = "offline-compiled-policy-verification",
                allPassed = passed,
                guiVerified = false,
                websiteAccessed = false,
                examBrowserTested = false,
                checks = results
            };
            StudyAtomicReportWriter.WriteNewJson(fullPath, report);
            return passed ? 0 : 61;
        }
        catch (Exception)
        {
            // No paths, user profile data or exception information emitted.
            return 62;
        }
    }
}
