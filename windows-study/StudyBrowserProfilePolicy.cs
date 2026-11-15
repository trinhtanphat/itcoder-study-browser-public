using System;
using System.IO;

namespace IndependentStudyBrowser;

/// <summary>
/// Explicit opt-in retention for ordinary website cookies and WebView2 storage.
/// It is always independent of Microsoft Edge and the official exam browser.
/// </summary>
internal enum StudyProfileMode
{
    Temporary,
    RememberSession
}

internal static class StudyBrowserProfilePolicy
{
#if ITCODER_QA
    internal const string AppDirectory = "ITCoderStudyQA";
#else
    internal const string AppDirectory = "IndependentStudyBrowser";
#endif
    private const string ProfilesDirectory = "Profiles";
    private const string SavedDirectory = "saved";

    internal static string SavedPathFor(string localApplicationData)
    {
        if (string.IsNullOrWhiteSpace(localApplicationData) ||
            !Path.IsPathFullyQualified(localApplicationData))
            throw new ArgumentException("A fully qualified application-data root is required.");
        return Path.Combine(Path.GetFullPath(localApplicationData),
            AppDirectory, ProfilesDirectory, SavedDirectory);
    }

    internal static string GetUserDataFolder(StudyProfileMode mode)
    {
        if (mode == StudyProfileMode.Temporary)
            return Path.Combine(Path.GetTempPath(),
                "IndependentStudyBrowser-" + Guid.NewGuid().ToString("N"));

        if (mode != StudyProfileMode.RememberSession)
            throw new ArgumentOutOfRangeException(nameof(mode));

        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return SavedPathFor(root);
    }

    internal static bool DeleteOnExit(StudyProfileMode mode) =>
        mode == StudyProfileMode.Temporary;

    internal static int SelfTest()
    {
        var sourceRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "study-profile-fixture"));
        var savedA = SavedPathFor(sourceRoot);
        var savedB = SavedPathFor(sourceRoot);
        if (savedA != savedB ||
            savedA != Path.Combine(sourceRoot, AppDirectory, "Profiles", "saved"))
            return 1;

        if (!DeleteOnExit(StudyProfileMode.Temporary) ||
            DeleteOnExit(StudyProfileMode.RememberSession)) return 2;

        var first = GetUserDataFolder(StudyProfileMode.Temporary);
        var second = GetUserDataFolder(StudyProfileMode.Temporary);
        if (first == second || !Path.IsPathFullyQualified(first) ||
            !Path.IsPathFullyQualified(second)) return 3;

        try { SavedPathFor("relative-root"); return 4; }
        catch (ArgumentException) { /* Fail closed for unsafe input. */ }

        try { GetUserDataFolder((StudyProfileMode)99); return 5; }
        catch (ArgumentOutOfRangeException) { /* Fail closed for invalid mode. */ }

        return 0;
    }
}
