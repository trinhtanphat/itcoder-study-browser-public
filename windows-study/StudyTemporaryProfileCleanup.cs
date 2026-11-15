using System;
using System.IO;
using System.Threading;

namespace IndependentStudyBrowser;

/// <summary>
/// Bounded cleanup of the exact random WebView2 profile created by this process.
/// Never enumerate unrelated temporary directories, follow a root junction, or
/// operate on the opt-in persistent profile under LocalApplicationData.
/// </summary>
internal static class StudyTemporaryProfileCleanup
{
    private static readonly string[] Prefixes =
    {
        "IndependentStudyBrowser-",
        "StudyBrowserSmoke-"
    };

    internal static bool IsOwnedTemporaryPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            string temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            string? parent = Path.GetDirectoryName(full);
            if (!string.Equals(parent, temp, StringComparison.OrdinalIgnoreCase))
                return false;

            string name = Path.GetFileName(full);
            foreach (string prefix in Prefixes)
            {
                if (!name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string suffix = name.Substring(prefix.Length);
                return suffix.Length == 32 &&
                    Guid.TryParseExact(suffix, "N", out _);
            }
        }
        catch (ArgumentException) { }
        catch (NotSupportedException) { }
        catch (PathTooLongException) { }
        return false;
    }

    internal static bool TryDeleteOwnedTemporaryProfile(string? path)
    {
        if (!IsOwnedTemporaryPath(path)) return false;
        string full = Path.GetFullPath(path!);
        // The browser subprocess can briefly keep profile files open while
        // WinForms is closing. Bounded short retries improve best-effort cleanup.
        for (int attempt = 0; attempt < 6; attempt++)
        {
            try
            {
                if (!Directory.Exists(full)) return true;
                if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
                    return false;
                Directory.Delete(full, recursive: true);
                return !Directory.Exists(full);
            }
            catch (DirectoryNotFoundException) { return true; }
            catch (UnauthorizedAccessException) { return false; }
            catch (IOException)
            {
                if (attempt == 5) return false;
                Thread.Sleep(40 * (1 << attempt));
            }
        }
        return false;
    }

    internal static int SelfTest()
    {
        foreach (string prefix in Prefixes)
        {
            string owned = Path.Combine(Path.GetTempPath(),
                prefix + Guid.NewGuid().ToString("N"));
            if (!IsOwnedTemporaryPath(owned)) return 1;
            if (Directory.Exists(owned)) return 2;

            Directory.CreateDirectory(owned);
            try
            {
                File.WriteAllText(Path.Combine(owned, "generated-test.txt"),
                    "Only an isolated self-test fixture.");
                if (!TryDeleteOwnedTemporaryProfile(owned)) return 3;
                if (Directory.Exists(owned)) return 4;
                if (!TryDeleteOwnedTemporaryProfile(owned)) return 5;
            }
            finally
            {
                // Clean ONLY the random directory that this test itself created.
                if (Directory.Exists(owned))
                    Directory.Delete(owned, recursive: true);
            }
        }

        // Exercise an actual locked file on Windows: WebView2 may retain
        // profile handles momentarily while its subprocess shuts down.
        string locked = Path.Combine(Path.GetTempPath(),
            "IndependentStudyBrowser-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(locked);
        try
        {
            string file = Path.Combine(locked, "held-open-test.bin");
            using var handle = new FileStream(file, FileMode.CreateNew,
                FileAccess.ReadWrite, FileShare.None);
            var release = new Thread(() =>
            {
                Thread.Sleep(170);
                handle.Dispose();
            }) { IsBackground = true };
            release.Start();
            bool cleaned = TryDeleteOwnedTemporaryProfile(locked);
            if (!release.Join(millisecondsTimeout: 3000)) return 9;
            if (!cleaned || Directory.Exists(locked)) return 10;
        }
        finally
        {
            // The fixture is ours; do not enumerate or touch unrelated folders.
            if (Directory.Exists(locked))
                TryDeleteOwnedTemporaryProfile(locked);
        }

        // Simulate environment creation completing after an earlier close
        // deleted (or never saw) the profile directory.
        string late = Path.Combine(Path.GetTempPath(),
            "IndependentStudyBrowser-" + Guid.NewGuid().ToString("N"));
        if (!TryDeleteOwnedTemporaryProfile(late)) return 11;
        Directory.CreateDirectory(late);
        try
        {
            File.WriteAllText(Path.Combine(late, "late-created.txt"), "test");
            if (!TryDeleteOwnedTemporaryProfile(late) ||
                Directory.Exists(late)) return 12;
        }
        finally
        {
            if (Directory.Exists(late))
                TryDeleteOwnedTemporaryProfile(late);
        }

        string root = Path.GetTempPath();
        if (IsOwnedTemporaryPath(root) ||
            IsOwnedTemporaryPath(Path.Combine(root, "IndependentStudyBrowser-saved")) ||
            IsOwnedTemporaryPath(Path.Combine(root, "StudyBrowserSmoke-NotAGuid")) ||
            IsOwnedTemporaryPath(Path.Combine(root, "IndependentStudyBrowser-" +
                Guid.NewGuid().ToString("N"), "..", "sibling")))
            return 6;

        string persistent = StudyBrowserProfilePolicy.SavedPathFor(
            Path.GetFullPath(Path.Combine(Path.GetTempPath(), "study-profile-fixture")));
        if (IsOwnedTemporaryPath(persistent) ||
            TryDeleteOwnedTemporaryProfile(persistent)) return 7;
        if (TryDeleteOwnedTemporaryProfile(null) ||
            TryDeleteOwnedTemporaryProfile(root)) return 8;

        return 0;
    }
}
