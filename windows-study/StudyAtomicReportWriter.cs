using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace IndependentStudyBrowser;

/// <summary>
/// Complete a JSON report on a private scratch file in the destination
/// directory and atomically rename it without overwrite. No browser profiles
/// are read or modified. Other users' preexisting files are never deleted.
/// </summary>
internal static class StudyAtomicReportWriter
{
    internal static void WriteNewJson<T>(string destination, T report)
    {
        // Serialize before creating any output file.
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(report,
            new JsonSerializerOptions { WriteIndented = true });
        string full = Path.GetFullPath(destination);
        string directory = Path.GetDirectoryName(full)
            ?? throw new ArgumentException("Report parent directory is missing.");
        if (File.Exists(full) || Directory.Exists(full))
            throw new IOException("Refusing to overwrite an existing report.");

        string scratch = Path.Combine(directory,
            "." + Path.GetFileName(full) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        bool scratchCreated = false;
        try
        {
            using (var stream = new FileStream(scratch, FileMode.CreateNew,
                FileAccess.Write, FileShare.None))
            {
                scratchCreated = true;
                stream.Write(payload);
                stream.Flush(flushToDisk: true);
            }
            // Both paths share a parent/volume. File.Move without overwrite
            // will fail if another process creates destination in the meantime.
            File.Move(scratch, full);
            scratchCreated = false;
        }
        finally
        {
            // Only our random, newly created temp file can be cleaned up.
            if (scratchCreated && File.Exists(scratch))
            {
                try { File.Delete(scratch); }
                catch (IOException) { /* Do not weaken file permissions. */ }
                catch (UnauthorizedAccessException) { /* Leave evidence untouched. */ }
            }
        }
    }

    /// <summary>
    /// Real file-system regression test under an isolated, random temp directory.
    /// It tests write, no-overwrite, two concurrent writers, and scratch cleanup.
    /// No profiles, website access, registry or user documents are touched.
    /// </summary>
    internal static int SelfTest()
    {
        string sandbox = Path.Combine(Path.GetTempPath(),
            "StudyBrowserAtomicTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sandbox);
        try
        {
            string first = Path.Combine(sandbox, "first.json");
            WriteNewJson(first, new { schemaVersion = 1, value = "original" });
            string original = File.ReadAllText(first);
            using (JsonDocument document = JsonDocument.Parse(original))
            {
                if (document.RootElement.GetProperty("value").GetString() != "original")
                    return 1;
            }

            // An already-existing file cannot be replaced, even if valid JSON.
            try { WriteNewJson(first, new { value = "replacement" }); return 2; }
            catch (IOException) { }
            if (File.ReadAllText(first) != original) return 3;

            // Simultaneous attempts on the same new path must have one winner.
            // The losing writer is rejected, not allowed to overwrite/append.
            string raced = Path.Combine(sandbox, "raced.json");
            int success = 0, rejected = 0, unexpected = 0;
            void TryRace(string label)
            {
                try
                {
                    WriteNewJson(raced, new { value = label });
                    Interlocked.Increment(ref success);
                }
                catch (IOException) { Interlocked.Increment(ref rejected); }
                catch (Exception) { Interlocked.Increment(ref unexpected); }
            }
            Parallel.Invoke(() => TryRace("first"), () => TryRace("second"));
            if (success != 1 || rejected != 1 || unexpected != 0) return 4;
            using (JsonDocument result = JsonDocument.Parse(File.ReadAllText(raced)))
            {
                string? value = result.RootElement.GetProperty("value").GetString();
                if (value != "first" && value != "second") return 5;
            }

            // Directory and missing-parent destinations must fail closed.
            try { WriteNewJson(sandbox, new { value = 1 }); return 6; }
            catch (IOException) { }
            try { WriteNewJson(Path.Combine(sandbox, "missing", "file.json"),
                new { value = 1 }); return 7; }
            catch (DirectoryNotFoundException) { }

            if (Directory.GetFiles(sandbox, "*.tmp").Length != 0 ||
                Directory.GetFiles(sandbox).Length != 2 ||
                Directory.Exists(Path.Combine(sandbox, "missing"))) return 8;
            return 0;
        }
        finally
        {
            // Only the directory this self-test created is eligible for cleanup.
            Directory.Delete(sandbox, recursive: true);
        }
    }
}
