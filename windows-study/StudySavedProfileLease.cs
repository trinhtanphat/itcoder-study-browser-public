using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace IndependentStudyBrowser;

/// <summary>
/// A cross-process, OS-released lease for the Study Browser's explicit
/// --remember-session profile. The lock file is in the app's own Locks folder,
/// outside WebView2 cookie/storage directories. No existing saved profile
/// files are read, changed, enumerated, or removed.
/// </summary>
internal static class StudySavedProfileLease
{
    /// <summary>
    /// Existing profile path components must be genuine directories. Refuse
    /// junctions, symlinks and files before WebView2 accesses persistent data.
    /// Nonexistent leaf directories are permitted for first-time use.
    /// </summary>
    private static bool IsSafeDirectoryComponent(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            FileAttributes attributes = File.GetAttributes(path);
            return (attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) ==
                   FileAttributes.Directory;
        }
        catch (FileNotFoundException) { return true; }
        catch (DirectoryNotFoundException) { return true; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    internal static bool TryAcquire(string? savedProfile, out FileStream? lease)
    {
        lease = null;
        try
        {
            if (string.IsNullOrWhiteSpace(savedProfile) ||
                !Path.IsPathFullyQualified(savedProfile)) return false;
            string profile = Path.TrimEndingDirectorySeparator(Path.GetFullPath(savedProfile));
            string? profiles = Path.GetDirectoryName(profile);
            string? application = profiles is null ? null : Path.GetDirectoryName(profiles);
            if (!string.Equals(Path.GetFileName(profile), "saved", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetFileName(profiles), "Profiles", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetFileName(application), StudyBrowserProfilePolicy.AppDirectory, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(application))
                return false;

            // Guard ALL app-owned saved-profile ancestors before accessing
            // the lock. A redirected Profiles/saved path could otherwise cause
            // WebView2 to load unrelated data despite an exclusive lock.
            string locks = Path.Combine(application, "Locks");
            if (!IsSafeDirectoryComponent(application) ||
                !IsSafeDirectoryComponent(profiles) ||
                !IsSafeDirectoryComponent(profile) ||
                !IsSafeDirectoryComponent(locks))
                return false;
            Directory.CreateDirectory(locks);
            string lockFile = Path.Combine(locks, "saved-profile.lock");
            if (File.Exists(lockFile) &&
                (File.GetAttributes(lockFile) & FileAttributes.ReparsePoint) != 0)
                return false;

            // OpenOrCreate makes a crash-stale marker harmless: the exclusive
            // OS handle is released on process exit without deleting anything.
            lease = new FileStream(lockFile, FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        catch (ArgumentException) { return false; }
        catch (NotSupportedException) { return false; }
    }

    /// <summary>
    /// Probe the same lock from an entirely separate Windows process. The
    /// child receives ONLY the random test fixture path, not the user's saved
    /// profile; its script source/arguments are encoded to avoid quoting bugs.
    /// Exit 0 = opened exclusive, 75 = already locked, 76 = unexpected error.
    /// </summary>
    private static int ProbeInSeparateProcess(string lockFile)
    {
        string encodedPath = Convert.ToBase64String(Encoding.UTF8.GetBytes(lockFile));
        string script =
            "$p=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('" + encodedPath + "'));" +
            "try{$f=[IO.File]::Open($p,[IO.FileMode]::OpenOrCreate," +
            "[IO.FileAccess]::ReadWrite,[IO.FileShare]::None);" +
            "$f.Dispose();exit 0}catch [IO.IOException]{exit 75}catch{exit 76}";
        string encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        string powershell = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.Windows), "System32", "WindowsPowerShell",
            "v1.0", "powershell.exe");
        try
        {
            var start = new ProcessStartInfo(powershell)
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            start.ArgumentList.Add("-NoLogo");
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-NonInteractive");
            start.ArgumentList.Add("-EncodedCommand");
            start.ArgumentList.Add(encodedCommand);
            using Process? child = Process.Start(start);
            if (child is null) return 76;
            if (!child.WaitForExit(8000))
            {
                // Terminate only the disposable subprocess created by THIS test.
                try { child.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                child.WaitForExit(2000);
                return 76;
            }
            return child.ExitCode;
        }
        catch (Exception) { return 76; }
    }

    internal static int SelfTest()
    {
        string root = Path.Combine(Path.GetTempPath(),
            "StudySavedLeaseTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string saved = StudyBrowserProfilePolicy.SavedPathFor(root);
            if (!TryAcquire(saved, out FileStream? first) || first is null) return 1;
            try
            {
                if (TryAcquire(saved, out FileStream? simultaneous))
                {
                    simultaneous?.Dispose();
                    return 2;
                }
                if (Directory.Exists(saved)) return 3; // No WebView2 profile created.
                string lockFile = Path.Combine(root, StudyBrowserProfilePolicy.AppDirectory,
                    "Locks", "saved-profile.lock");
                if (ProbeInSeparateProcess(lockFile) != 75) return 9;
            }
            finally { first.Dispose(); }

            // The retained lock file is not a stale lock: only the live OS
            // handle matters, so a restart can acquire it immediately.
            if (!TryAcquire(saved, out FileStream? reopened) || reopened is null)
                return 4;
            reopened.Dispose();
            string releasedLockFile = Path.Combine(root, StudyBrowserProfilePolicy.AppDirectory,
                "Locks", "saved-profile.lock");
            if (!File.Exists(releasedLockFile)) return 5;
            // A new process must be able to reopen after normal release.
            if (ProbeInSeparateProcess(releasedLockFile) != 0) return 10;

            if (TryAcquire(root, out var wrong)) { wrong?.Dispose(); return 6; }
            if (TryAcquire(Path.Combine(root, "Profiles", "saved"), out wrong))
            { wrong?.Dispose(); return 7; }
            if (TryAcquire(null, out wrong)) { wrong?.Dispose(); return 8; }

            // A regular file substituted for a profile directory must never
            // be interpreted as a new profile directory or followed.
            string invalidRoot = Path.Combine(root, "blocked");
            string invalidApp = Path.Combine(invalidRoot, StudyBrowserProfilePolicy.AppDirectory);
            Directory.CreateDirectory(invalidApp);
            File.WriteAllText(Path.Combine(invalidApp, "Profiles"), "not a directory");
            string badSaved = StudyBrowserProfilePolicy.SavedPathFor(invalidRoot);
            if (TryAcquire(badSaved, out wrong))
            {
                wrong?.Dispose();
                return 11;
            }

            // A test-only symlink is validated when Windows allows its creation
            // without touching user profiles. Missing symlink privileges are not
            // mistaken for a real successful symlink-blocking probe.
            string linkRoot = Path.Combine(root, "link-probe");
            string linkApp = Path.Combine(linkRoot, StudyBrowserProfilePolicy.AppDirectory);
            Directory.CreateDirectory(linkApp);
            string redirected = Path.Combine(root, "isolated-target");
            Directory.CreateDirectory(redirected);
            string link = Path.Combine(linkApp, "Profiles");
            bool symlinkCreated = false;
            try
            {
                Directory.CreateSymbolicLink(link, redirected);
                symlinkCreated = true;
                string linkedProfile = StudyBrowserProfilePolicy.SavedPathFor(linkRoot);
                if (TryAcquire(linkedProfile, out wrong))
                {
                    wrong?.Dispose();
                    return 12;
                }
            }
            catch (UnauthorizedAccessException) { /* OS denied test symlink. */ }
            catch (IOException) { /* Symlink support unavailable. */ }
            catch (PlatformNotSupportedException) { /* Unsupported on OS. */ }
            finally
            {
                // Remove only the link created under our own random fixture.
                if (symlinkCreated) Directory.Delete(link);
            }
            return 0;
        }
        finally
        {
            // Only this test's new random root may be deleted. Never delete
            // the user's real saved WebView2 profile or existing lock file.
            Directory.Delete(root, recursive: true);
        }
    }
}
