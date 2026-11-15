using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace IndependentStudyBrowser;

/// <summary>
/// Bounded offline WebView2 diagnostic. Uses a new temporary profile, about:blank only,
/// and optional machine-readable phase events. No website navigation or user-session reuse.
/// </summary>
internal sealed class OfflineWebViewSmokeWindow : Form
{
    private readonly WebView2 _view = new() { Dock = DockStyle.Fill };
    private readonly System.Windows.Forms.Timer _watchdog = new() { Interval = 15000 };
    private readonly string? _tracePath;
    private StreamWriter? _trace;
    private string? _scratchProfile;
    private bool _completed;

    internal OfflineWebViewSmokeWindow(string? tracePath = null)
    {
        _tracePath = tracePath;
        Text = "Offline WebView2 smoke test";
        Width = 640;
        Height = 440;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-20000, -20000);
        Controls.Add(_view);

        if (_tracePath is not null)
        {
            // Never truncate an existing report, and fail closed on invalid destinations.
            var target = Path.GetFullPath(_tracePath);
            _trace = new StreamWriter(new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
        }
        Trace("constructed");

        _watchdog.Tick += (_, _) => {
            Trace("watchdog-timeout");
            Complete(31);
        };
        Shown += async (_, _) =>
        {
            Trace("shown");
            try
            {
                _scratchProfile = Path.Combine(
                    Path.GetTempPath(), "StudyBrowserSmoke-" + Guid.NewGuid().ToString("N"));
                StudyTemporaryProfileInventory.MarkNewTemporaryProfile(_scratchProfile);
                Trace("environment-start");
                var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: _scratchProfile);
                if (_completed || IsDisposed) return;
                Trace("environment-ready");
                Trace("controller-start");
                await _view.EnsureCoreWebView2Async(environment);
                if (_completed || IsDisposed) return; // watchdog may have closed the form while awaiting
                Trace("controller-ready");
                var core = _view.CoreWebView2;
                core.Settings.IsScriptEnabled = false;
                core.Settings.IsWebMessageEnabled = false;
                core.Settings.AreHostObjectsAllowed = false;
                core.NavigationStarting += (_, nav) =>
                {
                    if (!string.Equals(nav.Uri, "about:blank", StringComparison.OrdinalIgnoreCase))
                        nav.Cancel = true;
                };
                core.NewWindowRequested += (_, request) => request.Handled = true;
                core.NavigationCompleted += (_, nav) => {
                    if (_completed || IsDisposed) return;
                    Trace(nav.IsSuccess ? "navigation-success" : "navigation-failed");
                    Complete(nav.IsSuccess ? 0 : 32);
                };
                Trace("navigation-start");
                core.Navigate("about:blank");
            }
            catch (Exception)
            {
                // Disposing an unfinished WebView2 after a watchdog timeout can throw.
                // Keep the original timeout verdict rather than reporting a second failure.
                if (!_completed)
                {
                    Trace("initialization-failed");
                    Complete(33);
                }
            }
            finally
            {
                // The environment creation task may finish after the form was
                // closed. Make a second bounded attempt on that exact temp
                // profile only. Process exit can still preempt this continuation.
                if ((_completed || IsDisposed) && _scratchProfile is not null)
                    StudyTemporaryProfileCleanup.TryDeleteOwnedTemporaryProfile(_scratchProfile);
            }
        };
        _watchdog.Start();
    }

    private void Trace(string phase)
    {
        if (_trace is null || (_completed && phase != "closed" && phase != "form-closed")) return;
        // Restrict output to an approved fixed set of event names; no paths,
        // URLs, error messages, process data, tokens or original application content.
        var evt = new { phase, utc = DateTimeOffset.UtcNow.ToString("O") };
        _trace.WriteLine(JsonSerializer.Serialize(evt));
        _trace.Flush();
    }

    private void Complete(int exitCode)
    {
        if (_completed) return;
        _completed = true;
        _watchdog.Stop();
        Environment.ExitCode = exitCode;
        Trace("closed");
        Close();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _watchdog.Stop();
        _watchdog.Dispose();
        _view.Dispose();
        Trace("form-closed");
        _trace?.Dispose();
        base.OnFormClosed(e);
        if (_scratchProfile is not null)
            StudyTemporaryProfileCleanup.TryDeleteOwnedTemporaryProfile(_scratchProfile);
    }
}
