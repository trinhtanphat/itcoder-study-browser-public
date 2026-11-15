namespace IndependentStudyBrowser;

/// <summary>
/// Compile-time branding only. The independent QA variant is a normal WebView2
/// browsing tool. It never impersonates the official secure exam application.
/// </summary>
internal static class StudyBranding
{
#if ITCODER_QA
    internal const string WindowTitle =
        "ITCoder Study QA — rebuilt independent browser (NOT exam client)";
    internal const string Banner =
        "ITCODER STUDY QA | Independent WebView2 browser — NOT HUTECH exam/lockdown client";
    internal const string SavedSession =
        "ITCoder QA profile: persistent sign-in enabled for this separate browser";
    internal const string TemporarySession =
        "ITCoder QA profile: temporary session, browsing data discarded on exit";
#else
    internal const string WindowTitle =
        "Independent Study Browser — NOT HUTECH Exam Browser";
    internal const string Banner =
        "INDEPENDENT STUDY BROWSER — ordinary HTTPS only; NOT an exam/lockdown client";
    internal const string SavedSession =
        "Saved session enabled in a separate Study Browser profile";
    internal const string TemporarySession =
        "Temporary session: browsing data is not intentionally retained";
#endif
}
