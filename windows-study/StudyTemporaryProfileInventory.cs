using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace IndependentStudyBrowser;

/// <summary>
/// Read-only recovery inventory for v41+ profiles with an explicit creation
/// marker. No folders are deleted, and report data never includes paths,
/// process IDs, browser storage, URL data or Windows account information.
/// </summary>
internal static class StudyTemporaryProfileInventory
{
    private const string Marker = ".independent-study-browser-owner-v1";
    private const string Header = "IndependentStudyBrowserOwnerV1";
    private static readonly TimeSpan MinimumStaleAge = TimeSpan.FromHours(24);
    private const int MaximumAuditCandidates = 512;

    internal static void MarkNewTemporaryProfile(string path)
    {
        if (!StudyTemporaryProfileCleanup.IsOwnedTemporaryPath(path))
            throw new ArgumentException("Invalid temporary profile scope.");
        if (Directory.Exists(path))
            throw new IOException("Temporary profile path already exists.");

        Directory.CreateDirectory(path);
        string content = string.Join("|", Header, Path.GetFileName(path),
            Environment.ProcessId,
            Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks,
            DateTime.UtcNow.Ticks);
        using var stream = new FileStream(Path.Combine(path, Marker),
            FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream);
        writer.Write(content);
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }

    internal enum CandidateState
    {
        Unmarked,
        Invalid,
        Recent,
        Active,
        Uncertain,
        Stale
    }

    internal static CandidateState InspectCandidate(string path, DateTime utcNow)
    {
        if (!StudyTemporaryProfileCleanup.IsOwnedTemporaryPath(path))
            return CandidateState.Invalid;
        try
        {
            var dir = new DirectoryInfo(path);
            if (!dir.Exists) return CandidateState.Invalid;
            if ((dir.Attributes & FileAttributes.ReparsePoint) != 0)
                return CandidateState.Invalid;

            string marker = Path.Combine(path, Marker);
            if (!File.Exists(marker)) return CandidateState.Unmarked;
            var file = new FileInfo(marker);
            if ((file.Attributes & FileAttributes.ReparsePoint) != 0 ||
                file.Length <= 0 || file.Length > 256)
                return CandidateState.Invalid;

            string[] parts = File.ReadAllText(marker).Split('|');
            if (parts.Length != 5 || parts[0] != Header ||
                parts[1] != Path.GetFileName(path) ||
                !int.TryParse(parts[2], out int pid) || pid <= 0 ||
                !long.TryParse(parts[3], out long startedTicks) ||
                !long.TryParse(parts[4], out long createdTicks))
                return CandidateState.Invalid;
            var createdUtc = new DateTime(createdTicks, DateTimeKind.Utc);
            var elapsed = utcNow.ToUniversalTime() - createdUtc;
            if (elapsed < MinimumStaleAge || elapsed > TimeSpan.FromDays(365))
                return CandidateState.Recent;

            // Fail closed if process state cannot be established. PID reuse is
            // distinguished with process start time; no processes are killed.
            try
            {
                using var process = Process.GetProcessById(pid);
                long observedStart = process.StartTime.ToUniversalTime().Ticks;
                return observedStart == startedTicks
                    ? CandidateState.Active : CandidateState.Stale;
            }
            catch (ArgumentException)
            {
                return CandidateState.Stale; // PID does not exist.
            }
            catch (Exception)
            {
                return CandidateState.Uncertain;
            }
        }
        catch (Exception)
        {
            return CandidateState.Invalid;
        }
    }

    private static IEnumerable<string> EnumerateCandidates()
    {
        string tempRoot = Path.GetFullPath(Path.GetTempPath());
        // Enumerate ONLY immediate matching children; never descend into
        // another user's data or scan all directories recursively.
        foreach (string prefix in new[] { "IndependentStudyBrowser-", "StudyBrowserSmoke-" })
            foreach (string path in Directory.EnumerateDirectories(
                tempRoot, prefix + "*", SearchOption.TopDirectoryOnly))
                yield return path;
    }

    internal static object ReadOnlyReport(IEnumerable<string>? candidates = null)
    {
        int inspected = 0, unmarked = 0, invalid = 0, recent = 0;
        int active = 0, uncertain = 0, stale = 0;
        bool scanTruncated = false;
        // The injected sequence is for offline test fixtures only. CLI always
        // uses EnumerateCandidates() for the command account's own TEMP.
        foreach (string path in candidates ?? EnumerateCandidates())
        {
            if (inspected >= MaximumAuditCandidates)
            {
                scanTruncated = true;
                break;
            }
            inspected++;
            switch (InspectCandidate(path, DateTime.UtcNow))
            {
                case CandidateState.Unmarked: unmarked++; break;
                case CandidateState.Invalid: invalid++; break;
                case CandidateState.Recent: recent++; break;
                case CandidateState.Active: active++; break;
                case CandidateState.Uncertain: uncertain++; break;
                case CandidateState.Stale: stale++; break;
            }
        }
        return new
        {
            schemaVersion = 2,
            mode = "read-only-temporary-profile-inventory",
            candidateLimit = MaximumAuditCandidates,
            scanTruncated,
            scanComplete = !scanTruncated,
            inspectionCount = inspected,
            unmarkedCount = unmarked,
            invalidCount = invalid,
            recentCount = recent,
            activeCount = active,
            uncertainCount = uncertain,
            staleCandidateCount = stale,
            deletedCount = 0,
            browserGuiVerified = false
        };
    }

    internal static int WriteReport(string destination)
    {
        try
        {
            StudyAtomicReportWriter.WriteNewJson(destination, ReadOnlyReport());
            return 0;
        }
        catch (Exception) { return 72; }
    }

    internal static int SelfTest()
    {
        string active = Path.Combine(Path.GetTempPath(),
            "IndependentStudyBrowser-" + Guid.NewGuid().ToString("N"));
        string unmarked = Path.Combine(Path.GetTempPath(),
            "StudyBrowserSmoke-" + Guid.NewGuid().ToString("N"));
        try
        {
            MarkNewTemporaryProfile(active);
            if (InspectCandidate(active, DateTime.UtcNow) != CandidateState.Recent) return 1;
            if (InspectCandidate(active, DateTime.UtcNow.AddDays(2)) != CandidateState.Active) return 2;

            Directory.CreateDirectory(unmarked);
            if (InspectCandidate(unmarked, DateTime.UtcNow.AddDays(2)) != CandidateState.Unmarked)
                return 3;

            string ownedMarker = Path.Combine(active, Marker);
            string original = File.ReadAllText(ownedMarker);
            File.WriteAllText(ownedMarker, "not-a-valid-owner-marker");
            if (InspectCandidate(active, DateTime.UtcNow.AddDays(2)) != CandidateState.Invalid)
                return 4;
            File.WriteAllText(ownedMarker, original);

            // A marked fixture whose recorded owner PID no longer exists and
            // whose creation time is old must be reported, never auto-deleted.
            string[] fields = original.Split('|');
            fields[2] = int.MaxValue.ToString();
            fields[4] = DateTime.UtcNow.AddDays(-2).Ticks.ToString();
            File.WriteAllText(ownedMarker, string.Join("|", fields));
            if (InspectCandidate(active, DateTime.UtcNow) != CandidateState.Stale)
                return 7;
            if (!Directory.Exists(active)) return 8;

            if (InspectCandidate(StudyBrowserProfilePolicy.SavedPathFor(
                    Path.GetTempPath()), DateTime.UtcNow) != CandidateState.Invalid)
                return 5;
            if (!Directory.Exists(active) || !Directory.Exists(unmarked)) return 6;

            // Offline high-volume fixture: do not create any actual folders.
            // An inventory with >512 matching names must return partial=true,
            // not claim that all candidates were inspected.
            string fake = Path.Combine(Path.GetTempPath(),
                "IndependentStudyBrowser-" + Guid.NewGuid().ToString("N"));
            var largeReport = JsonSerializer.SerializeToElement(
                ReadOnlyReport(Enumerable.Repeat(fake, MaximumAuditCandidates + 1)));
            if (largeReport.GetProperty("inspectionCount").GetInt32() != MaximumAuditCandidates ||
                !largeReport.GetProperty("scanTruncated").GetBoolean() ||
                largeReport.GetProperty("scanComplete").GetBoolean() ||
                largeReport.GetProperty("deletedCount").GetInt32() != 0)
                return 9;
            var smallReport = JsonSerializer.SerializeToElement(
                ReadOnlyReport(Enumerable.Repeat(fake, 3)));
            if (smallReport.GetProperty("inspectionCount").GetInt32() != 3 ||
                smallReport.GetProperty("scanTruncated").GetBoolean() ||
                !smallReport.GetProperty("scanComplete").GetBoolean())
                return 10;
            return 0;
        }
        finally
        {
            // Cleanup ONLY randomly generated fixtures from this self-test.
            StudyTemporaryProfileCleanup.TryDeleteOwnedTemporaryProfile(active);
            StudyTemporaryProfileCleanup.TryDeleteOwnedTemporaryProfile(unmarked);
        }
    }
}
