using System;
using System.Globalization;

namespace IndependentStudyBrowser;

// Deliberately supports ordinary HTTPS browsing, not vendor or exam-client protocols.
internal static class StudyNavigationPolicy
{
    // HTTPS login redirects can carry long state and authorization parameters.
    // Keep an explicit upper bound without arbitrarily rejecting valid 2K+ URLs.
    internal const int MaximumNavigationUrlLength = 16384;

    public static bool TryNormalize(string? text, out Uri? target)
    {
        target = null;
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaximumNavigationUrlLength)
            return false;
        // Uri.TryCreate may normalize raw backslashes or invisible Unicode
        // formatting/control characters in surprising ways. Reject before parsing.
        foreach (char c in text)
            if (c == '\\' || char.IsControl(c) ||
                char.GetUnicodeCategory(c) == UnicodeCategory.Format) return false;
        if (!Uri.TryCreate(text.Trim(), UriKind.Absolute, out Uri? uri))
            return false;
        if (uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(uri.IdnHost) ||
            !string.IsNullOrEmpty(uri.UserInfo) || uri.IsFile || uri.HostNameType == UriHostNameType.Unknown)
            return false;
        target = uri;
        return true;
    }

    public static bool IsAllowed(string? uri) => TryNormalize(uri, out _);

    public static int SelfTest()
    {
        string[] allowed = {
            "https://example.org/",
            "https://itcoder.hutech.edu.vn/",
            "https://example.org/study?topic=c%23",
            "https://example.org:8443/study",
            "https://example.org/a%20b?q=encoded%2Fvalue#notes",
            "https://example.org/%5Cencoded-backslash",
            "  https://example.org/  ",
            "https://example.org/redirect?state=" + new string('a', 8000)
        };
        string[] denied = {
            "", "  ", "http://example.org", "file:///C:/Users/Admin/Desktop/a.txt",
            "javascript:alert(1)", "data:text/html,hello", "hutech-browser://start",
            "https://user:password@example.org", "https://user@example.org/",
            "https://", "127.0.0.1", "https://exa\tmple.org/",
            "https://example.org/pa\rth", "https://example.org/pa\nth",
            "https://example.org/\0tail", "https://example.org/\u007Ftail",
            "https://example.org/\\hidden", "https://example.org\\@other.example/",
            "https://example.org/\u202Ereversed", "https://example.org/\u200Bhidden",
            "https://example.org/" + new string('x', MaximumNavigationUrlLength)
        };
        foreach (string value in allowed)
            if (!IsAllowed(value)) return 1;
        foreach (string value in denied)
            if (IsAllowed(value)) return 2;
        return 0;
    }

    // Deterministic negative/positive corpus, run by the compiled EXE.
    // No network requests: validates the exact policy used by WebView2 events.
    internal static int CorpusSelfTest()
    {
        const string prefix = "https://example.org/";
        for (int code = 0; code <= 159; code++)
        {
            if (code > 31 && code < 127) continue;
            string c = ((char)code).ToString();
            if (IsAllowed("https://exa" + c + "mple.org/") ||
                IsAllowed(prefix + "a" + c + "b")) return 1;
        }

        char[] hidden = { '\u200B', '\u200E', '\u202E', '\u2066', '\uFEFF' };
        foreach (char c in hidden)
            if (IsAllowed(prefix + c + "study")) return 2;

        if (IsAllowed(prefix + "\\hidden") ||
            IsAllowed("https://example.org\\@other.example/")) return 3;

        if (!IsAllowed(prefix + new string('a', MaximumNavigationUrlLength - prefix.Length)) ||
            IsAllowed(prefix + new string('a', MaximumNavigationUrlLength + 1 - prefix.Length)))
            return 4;
        // An HTTPS sign-in redirect with long but valid state must not fail
        // just because it exceeds the legacy 2,048-character address limit.
        if (!IsAllowed(prefix + "callback?state=" + new string('a', 8192)))
            return 7;

        if (!IsAllowed(prefix + "%5Cencoded") ||
            !IsAllowed("HTTPS://example.org/path?topic=c%23")) return 5;

        if (IsAllowed("https://user@example.org/") ||
            IsAllowed("https://user:pass@example.org/") ||
            IsAllowed("file:///C:/private.txt")) return 6;

        return 0;
    }
}
