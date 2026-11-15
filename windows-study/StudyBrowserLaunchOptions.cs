using System;

namespace IndependentStudyBrowser;

/// <summary>
/// CLI parsing for a normal independent HTTPS browsing session. All URL
/// selection happens before GUI initialization and cannot import an existing
/// vendor/Edge profile or relax the address-validation policy.
/// </summary>
internal sealed record StudyBrowserLaunchOptions(
    StudyProfileMode ProfileMode, string HomeAddress)
{
    private const string DefaultHomeAddress = "https://itcoder.hutech.edu.vn/";

    internal static bool TryParse(string[]? args, out StudyBrowserLaunchOptions? options)
    {
        options = null;
        if (args is null || args.Length > 3) return false;
        bool remember = false;
        bool sawUrl = false;
        string home = DefaultHomeAddress;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--remember-session")
            {
                if (remember) return false;
                remember = true;
                continue;
            }

            if (args[i] == "--url")
            {
                if (sawUrl || i + 1 >= args.Length ||
                    !StudyNavigationPolicy.TryNormalize(args[++i], out Uri? uri))
                    return false;
                sawUrl = true;
                home = uri!.AbsoluteUri;
                continue;
            }

            // No forwarded WebView2/Chromium flags or unknown options.
            return false;
        }

        options = new StudyBrowserLaunchOptions(
            remember ? StudyProfileMode.RememberSession : StudyProfileMode.Temporary, home);
        return true;
    }

    internal static int SelfTest()
    {
        if (!TryParse(Array.Empty<string>(), out var defaults) ||
            defaults!.HomeAddress != DefaultHomeAddress ||
            defaults.ProfileMode != StudyProfileMode.Temporary) return 1;

        if (!TryParse(new[] { "--remember-session" }, out var saved) ||
            saved!.ProfileMode != StudyProfileMode.RememberSession ||
            saved.HomeAddress != DefaultHomeAddress) return 2;

        if (!TryParse(new[] { "--url", "https://example.org/qa" }, out var testSite) ||
            testSite!.ProfileMode != StudyProfileMode.Temporary ||
            testSite.HomeAddress != "https://example.org/qa") return 3;

        if (!TryParse(new[] { "--url", "https://example.org/", "--remember-session" }, out var first) ||
            first!.ProfileMode != StudyProfileMode.RememberSession ||
            first.HomeAddress != "https://example.org/") return 4;

        if (!TryParse(new[] { "--remember-session", "--url", "https://example.org/" }, out var last) ||
            last!.ProfileMode != StudyProfileMode.RememberSession ||
            last.HomeAddress != "https://example.org/") return 5;

        string[][] rejected =
        {
            new[] { "--url" },
            new[] { "--url", "" },
            new[] { "--url", "http://example.org/" },
            new[] { "--url", "file:///C:/private.txt" },
            new[] { "--url", "javascript:alert(1)" },
            new[] { "--url", "https://user:pass@example.org/" },
            new[] { "--url", "https://example.org/\\hidden" },
            new[] { "--url", "https://example.org/\u200Bzero" },
            new[] { "--url", "https://example.org/", "--url", "https://another.org/" },
            new[] { "--remember-session", "--remember-session" },
            new[] { "--remember-session", "--profile", "C:\\Users\\someone" },
            new[] { "--disable-web-security" },
            new[] { "--url", "--remember-session" },
            new[] { "--url", "https://example.org/", "https://extra.org/" },
            new[] { "--offline-ui-smoke", "output.json" },
            new[] { "--url", "https://example.org/", "--remember-session", "--unexpected" }
        };
        foreach (string[] argv in rejected)
            if (TryParse(argv, out _)) return 6;
        if (TryParse(null, out _)) return 7;

        string longHttps = "https://example.org/login?state=" + new string('s', 9000);
        if (!TryParse(new[] { "--url", longHttps }, out var longLaunch) ||
            longLaunch!.HomeAddress != longHttps) return 8;
        if (TryParse(new[] { "--url", "https://example.org/" +
            new string('s', StudyNavigationPolicy.MaximumNavigationUrlLength) }, out _))
            return 9;
        return 0;
    }
}
