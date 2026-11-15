using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace IndependentStudyBrowser;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // No WebView2 or network is initialized in self-test mode.
        if (args.Length == 1 && args[0] == "--self-test")
        {
            int policy = StudyNavigationPolicy.SelfTest();
            if (policy != 0) return policy;
            int urlCorpus = StudyNavigationPolicy.CorpusSelfTest();
            if (urlCorpus != 0) return 20 + urlCorpus;
            int guard = UiSessionGuard.SelfTest();
            if (guard != 0) return guard;
            int lifecycle = StudyBrowserLifecycle.SelfTest();
            if (lifecycle != 0) return lifecycle;
            int profiles = StudyBrowserProfilePolicy.SelfTest();
            if (profiles != 0) return profiles;
            int launch = StudyBrowserLaunchOptions.SelfTest();
            if (launch != 0) return 90 + launch;
            int savedLease = StudySavedProfileLease.SelfTest();
            if (savedLease != 0) return 100 + savedLease;
            int webView2 = StudyWebView2FailurePolicy.SelfTest();
            if (webView2 != 0) return webView2;
            int certificates = StudyWebView2CertificatePolicy.SelfTest();
            if (certificates != 0) return 70 + certificates;
            int networkPolicy = StudyWebView2NetworkPolicy.SelfTest();
            if (networkPolicy != 0) return 80 + networkPolicy;
            int atomicReports = StudyAtomicReportWriter.SelfTest();
            if (atomicReports != 0) return atomicReports;
            int temp = StudyTemporaryProfileCleanup.SelfTest();
            return temp != 0 ? temp : StudyTemporaryProfileInventory.SelfTest();
        }
        if (args.Length == 1 && args[0] == "--ui-preflight")
            return UiSessionGuard.Preflight();
        if (args.Length == 2 && args[0] == "--diagnose")
            return StudyBrowserDiagnostics.Write(args[1]);
        if (args.Length == 2 && args[0] == "--verify")
            return StudyBrowserVerification.WriteReport(args[1]);
        if (args.Length == 2 && args[0] == "--audit-temp-profiles")
            return StudyTemporaryProfileInventory.WriteReport(args[1]);
        // Query runtime availability without opening a form, browser process or web page.
        if (args.Length == 1 && args[0] == "--runtime-check")
        {
            try
            {
                string? version = CoreWebView2Environment.GetAvailableBrowserVersionString();
                return string.IsNullOrWhiteSpace(version) ? 12 : 0;
            }
            catch (Exception)
            {
                return 13;
            }
        }
        if ((args.Length == 1 || args.Length == 2) && args[0] == "--offline-ui-smoke")
        {
            int preflight = UiSessionGuard.Preflight();
            if (preflight != 0) return preflight;
            ApplicationConfiguration.Initialize();
            Application.Run(new OfflineWebViewSmokeWindow(args.Length == 2 ? args[1] : null));
            return Environment.ExitCode;
        }
        if (!StudyBrowserLaunchOptions.TryParse(args, out var launchOptions))
            return 2;
        int sessionGate = UiSessionGuard.Preflight();
        if (sessionGate != 0) return sessionGate;
        ApplicationConfiguration.Initialize();
        Application.Run(new StudyBrowserWindow(
            launchOptions!.ProfileMode, launchOptions.HomeAddress));
        return Environment.ExitCode;
    }
}

internal sealed class StudyBrowserWindow : Form
{
    private readonly string _homeAddress;
    private readonly WebView2 _web = new() { Dock = DockStyle.Fill };
    private readonly TextBox _address = new() { Width = 580 };
    private readonly Label _status = new() { Text = "Preparing ordinary WebView2 browsing", Dock = DockStyle.Fill };
    private readonly Button _back = new() { Text = "Back", Width = 65 };
    private readonly Button _forward = new() { Text = "Forward", Width = 72 };
    private readonly Button _reload = new() { Text = "Reload", Width = 75 };
    private readonly Button _home = new() { Text = "Home", Width = 70 };
    private readonly Button _go = new() { Text = "Go", Width = 50 };
    private string? _profilePath;
    private readonly StudyProfileMode _profileMode;
    private FileStream? _savedProfileLease;
    private bool _initializationPending;
    private readonly StudyBrowserLifecycle _lifecycle = new();
    private readonly System.Windows.Forms.Timer _initializationWatchdog = new() { Interval = 20000 };
    private bool CanUpdateUi => !_lifecycle.IsClosed && !IsDisposed && !Disposing && !_web.IsDisposed;

    public StudyBrowserWindow(StudyProfileMode profileMode, string homeAddress)
    {
        _profileMode = profileMode;
        // The argument is already validated by the shared HTTPS CLI policy.
        _homeAddress = homeAddress;
        Text = StudyBranding.WindowTitle;
        Width = 1240;
        Height = 820;
        MinimumSize = new Size(820, 560);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        _initializationWatchdog.Tick += (_, _) => {
            _initializationWatchdog.Stop();
            if (_lifecycle.MarkTimedOut() && CanUpdateUi) {
                UpdateNavigationButtons();
                _status.Text = "WebView2 initialization timed out. Close and reopen the study browser.";
            }
        };
        KeyDown += (_, e) => {
            if (!CanUpdateUi) return;
            if (e.Control && e.KeyCode == Keys.L) {
                _address.Focus();
                _address.SelectAll();
                e.SuppressKeyPress = true;
            } else if (e.KeyCode == Keys.F5 && _lifecycle.CanNavigate) {
                _web.Reload();
                e.SuppressKeyPress = true;
            }
        };

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

        var notice = new Label {
            Dock = DockStyle.Fill,
            Text = StudyBranding.Banner,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(28, 68, 116),
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = false, WrapContents = false };
        _address.Text = _homeAddress;
        _status.Text = _profileMode == StudyProfileMode.RememberSession
            ? StudyBranding.SavedSession
            : StudyBranding.TemporarySession;
        // No navigation commands are active until WebView2 controller is ready.
        _back.Enabled = false;
        _forward.Enabled = false;
        _reload.Enabled = false;
        _home.Enabled = false;
        _go.Enabled = false;
        toolbar.Controls.AddRange(new Control[] { _back, _forward, _reload, _home, _address, _go });

        layout.Controls.Add(notice, 0, 0);
        layout.Controls.Add(toolbar, 0, 1);
        layout.Controls.Add(_web, 0, 2);
        layout.Controls.Add(_status, 0, 3);
        Controls.Add(layout);

        toolbar.Resize += (_, _) => _address.Width = Math.Max(200, toolbar.ClientSize.Width - 380);
        _back.Click += (_, _) => { if (_lifecycle.CanNavigate && _web.CanGoBack) _web.GoBack(); };
        _forward.Click += (_, _) => { if (_lifecycle.CanNavigate && _web.CanGoForward) _web.GoForward(); };
        _reload.Click += (_, _) => { if (_lifecycle.CanNavigate) _web.Reload(); };
        _home.Click += (_, _) => Navigate(_homeAddress);
        _go.Click += (_, _) => Navigate(_address.Text);
        _address.KeyDown += (_, e) => {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Navigate(_address.Text); }
        };

        Shown += async (_, _) => {
            _initializationPending = true;
            _initializationWatchdog.Start();
            try { await InitializeWebView(); }
            finally {
                _initializationPending = false;
                // Retain the cross-process profile lease while a WebView2
                // initialization await could still be creating user data.
                if (_lifecycle.IsClosed) ReleaseSavedProfileLease();
            }
        };
    }

    private void Navigate(string? address)
    {
        if (!CanUpdateUi) return;
        if (!StudyNavigationPolicy.TryNormalize(address, out Uri? uri)) {
            _status.Text = "Blocked: only absolute HTTPS URLs without embedded credentials are permitted.";
            return;
        }
        if (!_lifecycle.CanNavigate || _web.CoreWebView2 is null) {
            _status.Text = "Browser is not ready yet.";
            return;
        }
        _web.CoreWebView2.Navigate(uri!.AbsoluteUri);
    }

    private async Task InitializeWebView()
    {
        try {
            // Both modes use dedicated WebView2 profiles, never the Edge or exam-browser profile.
            // The persistent profile is enabled only by the explicit --remember-session flag.
            _profilePath = StudyBrowserProfilePolicy.GetUserDataFolder(_profileMode);
            if (_profileMode == StudyProfileMode.RememberSession &&
                !StudySavedProfileLease.TryAcquire(_profilePath, out _savedProfileLease))
            {
                if (CanUpdateUi && _lifecycle.MarkInitializationFailed())
                {
                    _initializationWatchdog.Stop();
                    UpdateNavigationButtons();
                    _status.Text = "Saved Study Browser profile is busy or its lock is unavailable.";
                }
                return;
            }
            if (!_lifecycle.IsStarting || !CanUpdateUi) return;
            if (StudyBrowserProfilePolicy.DeleteOnExit(_profileMode))
                StudyTemporaryProfileInventory.MarkNewTemporaryProfile(_profilePath);
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: _profilePath);
            if (!_lifecycle.IsStarting || !CanUpdateUi) return;
            await _web.EnsureCoreWebView2Async(environment);
            if (!_lifecycle.IsStarting || !CanUpdateUi) return;
            CoreWebView2 core = _web.CoreWebView2;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.IsWebMessageEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.IsStatusBarEnabled = false;

            core.NavigationStarting += (_, e) => {
                if (!CanUpdateUi) { e.Cancel = true; return; }
                if (!_lifecycle.CanNavigate || !StudyNavigationPolicy.IsAllowed(e.Uri)) {
                    e.Cancel = true;
                    if (_lifecycle.CanNavigate)
                        _status.Text = "Blocked non-HTTPS navigation or custom protocol";
                } else {
                    _status.Text = "Loading HTTPS page...";
                }
            };
            // A secure top-level address is not enough: embedded frames can
            // navigate independently, including via redirects and scripts.
            core.FrameNavigationStarting += (_, e) => {
                if (!CanUpdateUi ||
                    !StudyWebView2NetworkPolicy.IsAllowedFrameNavigation(e.Uri))
                    e.Cancel = true;
            };
            // Reject non-HTTPS network resources from embedded content, too.
            // Do not intercept internal about:blank/srcdoc documents.
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += (_, e) => {
                if (!CanUpdateUi ||
                    !StudyWebView2NetworkPolicy.IsAllowedResourceRequest(e.Request.Uri))
                    e.Response = core.Environment.CreateWebResourceResponse(
                        Stream.Null, 403, "Blocked", "Content-Type: text/plain");
            };
            core.SourceChanged += (_, _) => {
                if (CanUpdateUi && _lifecycle.CanNavigate && StudyNavigationPolicy.IsAllowed(core.Source))
                    _address.Text = core.Source;
            };
            core.HistoryChanged += (_, _) => UpdateNavigationButtons();
            core.NavigationCompleted += (_, e) => {
                if (!CanUpdateUi || !_lifecycle.CanNavigate) return;
                _status.Text = e.IsSuccess ? "Ordinary HTTPS page loaded" :
                    "WebView2 could not complete HTTPS navigation";
                UpdateNavigationButtons();
            };
            core.NewWindowRequested += (_, e) => {
                e.Handled = true;
                if (CanUpdateUi && _lifecycle.CanNavigate && StudyNavigationPolicy.IsAllowed(e.Uri))
                    Navigate(e.Uri);
            };
            core.ProcessFailed += (_, failure) => {
                if (!CanUpdateUi) return;
                // A GPU/utility/sandbox/subframe process exit is often auto-recovered
                // by WebView2; do not destroy navigation for these known nonfatal kinds.
                if (StudyWebView2FailurePolicy.ShouldDisableNavigation(failure.ProcessFailedKind)) {
                    if (_lifecycle.MarkRendererFailed()) {
                        UpdateNavigationButtons();
                        _status.Text = "A critical WebView2 process failed. Close and reopen the study browser.";
                    }
                } else if (_lifecycle.CanNavigate) {
                    _status.Text = "A supporting WebView2 process exited; the browser may recover.";
                }
            };
            // HTTPS alone does not establish trust. On an invalid server TLS
            // certificate, cancel the request; never show a proceed-anyway path.
            core.ServerCertificateErrorDetected += (_, e) => {
                e.Action = StudyWebView2CertificatePolicy.ActionFor(e.ErrorStatus);
                if (CanUpdateUi && _lifecycle.CanNavigate)
                    _status.Text = "HTTPS certificate verification failed; request blocked.";
            };
            core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            core.DownloadStarting += (_, e) => e.Cancel = true;

            if (!_lifecycle.MarkReady()) return;
            _initializationWatchdog.Stop();
            UpdateNavigationButtons();
            Navigate(_homeAddress);
        }
        catch (Exception error) {
            if (CanUpdateUi && _lifecycle.MarkInitializationFailed()) {
                _initializationWatchdog.Stop();
                UpdateNavigationButtons();
                _status.Text = "Study browser unavailable: " + error.GetType().Name;
            }
        }
        finally {
            // If a WebView2 initialization await completes after the window was
            // closed, its environment may have recreated the ephemeral profile
            // after OnFormClosed tried deleting it. Reattempt the same strictly
            // scoped cleanup. This is best effort: process exit can preempt it.
            if (_lifecycle.IsClosed && _profilePath is not null &&
                StudyBrowserProfilePolicy.DeleteOnExit(_profileMode))
                StudyTemporaryProfileCleanup.TryDeleteOwnedTemporaryProfile(_profilePath);
        }
    }

    private void UpdateNavigationButtons()
    {
        // Browser callbacks can arrive as controls are being disposed. Avoid
        // touching WinForms controls after the window is closed or disposed.
        if (!CanUpdateUi) return;
        bool ready = _lifecycle.CanNavigate;
        _reload.Enabled = ready;
        _home.Enabled = ready;
        _go.Enabled = ready;
        _back.Enabled = ready && _web.CanGoBack;
        _forward.Enabled = ready && _web.CanGoForward;
    }

    private void ReleaseSavedProfileLease()
    {
        _savedProfileLease?.Dispose();
        _savedProfileLease = null;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _lifecycle.Close();
        _initializationWatchdog.Stop();
        _initializationWatchdog.Dispose();
        _web.Dispose();
        base.OnFormClosed(e);
        if (!_initializationPending) ReleaseSavedProfileLease();
        if (_profilePath is not null && StudyBrowserProfilePolicy.DeleteOnExit(_profileMode))
        {
            // Only this process's randomly named temporary profile is eligible.
            // Retry briefly while WebView2 releases files; never remove saved data.
            StudyTemporaryProfileCleanup.TryDeleteOwnedTemporaryProfile(_profilePath);
        }
    }
}
