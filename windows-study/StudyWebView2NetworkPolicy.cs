using System;
using System.Globalization;

namespace IndependentStudyBrowser;

/// <summary>
/// Safeguards normal HTTPS browsing without breaking local content generated
/// by an HTTPS document (data: images/media and blob: object URLs).
/// Only HTTPS may make ordinary network requests. Browser-internal URLs are
/// not evidence of a new outbound network connection.
/// </summary>
internal static class StudyWebView2NetworkPolicy
{
    private const int MaximumResourceUrlLength = 65536;
    private const int MaximumDataUrlLength = 65536;

    internal static bool IsAllowedFrameNavigation(string? uri) =>
        uri is "about:blank" or "about:srcdoc" ||
        IsAllowedHttpsUrl(uri) || IsSecureOriginBlobUrl(uri);

    internal static bool IsAllowedResourceRequest(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) ||
            text.Length > MaximumResourceUrlLength ||
            !HasSafeCharacters(text)) return false;

        if (IsAllowedHttpsUrl(text) || IsSecureOriginBlobUrl(text)) return true;

        // A data: resource is inline bytes, not an HTTP request. Permit
        // generated images/fonts/media while continuing to block data: frames.
        // Script and HTML data: resources remain blocked explicitly.
        if (text.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return IsAllowedInlineResource(text);

        // WebView2 can expose internal empty documents through resource
        // callbacks; they are not insecure network requests.
        return text is "about:blank" or "about:srcdoc";
    }

    private static bool HasSafeCharacters(string text)
    {
        foreach (char c in text)
            if (char.IsControl(c) || c == '\\' ||
                char.GetUnicodeCategory(c) == UnicodeCategory.Format)
                return false;
        return true;
    }

    private static bool IsAllowedHttpsUrl(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) ||
            text.Length > MaximumResourceUrlLength || !HasSafeCharacters(text) ||
            !Uri.TryCreate(text, UriKind.Absolute, out Uri? url))
            return false;
        return url.Scheme == Uri.UriSchemeHttps &&
               !url.IsFile &&
               !string.IsNullOrWhiteSpace(url.IdnHost) &&
               string.IsNullOrEmpty(url.UserInfo) &&
               url.HostNameType != UriHostNameType.Unknown;
    }

    private static bool IsSecureOriginBlobUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !value.StartsWith("blob:", StringComparison.OrdinalIgnoreCase) ||
            value.Length > MaximumResourceUrlLength ||
            !HasSafeCharacters(value)) return false;

        string inner = value.Substring(5);
        if (!IsAllowedHttpsUrl(inner)) return false;

        // Restrict blob: inner URLs to a single HTTPS origin and an opaque
        // object identifier, not an unbounded arbitrary URL.
        if (!Uri.TryCreate(inner, UriKind.Absolute, out Uri? source)) return false;
        return source!.AbsolutePath.Length > 1 &&
               !source.AbsolutePath.Contains("//", StringComparison.Ordinal) &&
               string.IsNullOrEmpty(source.Query) &&
               string.IsNullOrEmpty(source.Fragment);
    }

    private static bool IsAllowedInlineResource(string value)
    {
        if (value.Length > MaximumDataUrlLength) return false;
        int comma = value.IndexOf(',');
        if (comma < 0 || comma > 140) return false;
        string metadata = value.Substring(5, comma - 5).ToLowerInvariant();
        string type = metadata.Split(';')[0];

        bool safeType = type.StartsWith("image/", StringComparison.Ordinal) ||
                        type.StartsWith("audio/", StringComparison.Ordinal) ||
                        type.StartsWith("video/", StringComparison.Ordinal) ||
                        type.StartsWith("font/", StringComparison.Ordinal) ||
                        type == "application/font-woff" ||
                        type == "application/font-woff2";
        if (!safeType || metadata.Contains("javascript") ||
            metadata.Contains("html") || metadata.Contains("svg"))
            return false;
        return true;
    }

    internal static int SelfTest()
    {
        string[] permittedFrames = {
            "about:blank", "about:srcdoc", "https://example.org/inner",
            "https://example.org/path?auth=public%20data",
            "blob:https://example.org/7ad3d6b8-3e2c-4f2e-9a63-9a5e903ba219"
        };
        foreach (string url in permittedFrames)
            if (!IsAllowedFrameNavigation(url)) return 1;

        string[] forbiddenFrames = {
            "", "about:config", "about:blank#unsafe",
            "http://example.org/", "file:///C:/private.txt",
            "javascript:alert(1)", "data:text/html,hi",
            "blob:http://example.org/1111", "blob:null/1111",
            "https://user:password@example.org/",
            "https://example.org/\tinner"
        };
        foreach (string url in forbiddenFrames)
            if (IsAllowedFrameNavigation(url)) return 2;

        string[] allowedResources = {
            "https://example.org/style.css",
            "https://example.org/api/v1?" + new string('q', 3000),
            "HTTPS://example.org/image.webp",
            "https://cdn.example.org/font.woff2",
            "https://example.org/a%5Cb",
            "about:blank", "about:srcdoc",
            "blob:https://example.org/7ad3d6b8-3e2c-4f2e-9a63-9a5e903ba219",
            "data:image/png;base64,iVBORw0KGgo=",
            "data:image/webp;base64,AA==",
            "data:font/woff2;base64,AA==",
            "data:audio/mpeg;base64,AA=="
        };
        foreach (string url in allowedResources)
            if (!IsAllowedResourceRequest(url)) return 3;

        string[] forbiddenResources = {
            "", "http://example.org/image.png",
            "ws://example.org/ws", "file:///C:/private.ini",
            "data:text/html,<script>alert(1)</script>",
            "data:application/javascript,alert(1)",
            "data:image/svg+xml,<svg></svg>",
            "blob:http://example.org/1111", "blob:null/1111",
            "javascript:alert(1)",
            "https://user@example.org/", "https://example.org/\u202Eodd",
            "https://example.org/" + new string('x', MaximumResourceUrlLength),
            "data:image/png;base64," + new string('a', MaximumDataUrlLength)
        };
        foreach (string url in forbiddenResources)
            if (IsAllowedResourceRequest(url)) return 4;

        // Uri's parser can have a lower length cap than the defensive ceiling.
        if (!IsAllowedResourceRequest("https://example.org/" +
            new string('a', 16000))) return 5;
        if (IsAllowedFrameNavigation(null) || IsAllowedResourceRequest(null))
            return 6;
        return 0;
    }
}
