using Microsoft.Web.WebView2.Core;

namespace IndependentStudyBrowser;

/// <summary>
/// WebView2 reports failures from multiple browser subprocesses. Only known
/// nonfatal child-process failures should leave study navigation enabled.
/// Browser/renderer failures and unknown kinds fail closed.
/// </summary>
internal static class StudyWebView2FailurePolicy
{
    internal static bool ShouldDisableNavigation(CoreWebView2ProcessFailedKind kind) =>
        kind switch
        {
            CoreWebView2ProcessFailedKind.GpuProcessExited => false,
            CoreWebView2ProcessFailedKind.UtilityProcessExited => false,
            CoreWebView2ProcessFailedKind.SandboxHelperProcessExited => false,
            CoreWebView2ProcessFailedKind.PpapiPluginProcessExited => false,
            CoreWebView2ProcessFailedKind.PpapiBrokerProcessExited => false,
            CoreWebView2ProcessFailedKind.FrameRenderProcessExited => false,
            CoreWebView2ProcessFailedKind.BrowserProcessExited => true,
            CoreWebView2ProcessFailedKind.RenderProcessExited => true,
            CoreWebView2ProcessFailedKind.RenderProcessUnresponsive => true,
            _ => true
        };

    internal static int SelfTest()
    {
        CoreWebView2ProcessFailedKind[] nonfatal =
        {
            CoreWebView2ProcessFailedKind.GpuProcessExited,
            CoreWebView2ProcessFailedKind.UtilityProcessExited,
            CoreWebView2ProcessFailedKind.SandboxHelperProcessExited,
            CoreWebView2ProcessFailedKind.PpapiPluginProcessExited,
            CoreWebView2ProcessFailedKind.PpapiBrokerProcessExited,
            CoreWebView2ProcessFailedKind.FrameRenderProcessExited
        };
        foreach (var kind in nonfatal)
            if (ShouldDisableNavigation(kind)) return 1;

        CoreWebView2ProcessFailedKind[] disruptive =
        {
            CoreWebView2ProcessFailedKind.BrowserProcessExited,
            CoreWebView2ProcessFailedKind.RenderProcessExited,
            CoreWebView2ProcessFailedKind.RenderProcessUnresponsive,
            CoreWebView2ProcessFailedKind.UnknownProcessExited
        };
        foreach (var kind in disruptive)
            if (!ShouldDisableNavigation(kind)) return 2;

        if (!ShouldDisableNavigation((CoreWebView2ProcessFailedKind)999)) return 3;

        var lifecycle = new StudyBrowserLifecycle();
        if (!lifecycle.MarkReady()) return 4;
        if (ShouldDisableNavigation(CoreWebView2ProcessFailedKind.GpuProcessExited) ||
            !lifecycle.CanNavigate) return 5;
        if (!ShouldDisableNavigation(CoreWebView2ProcessFailedKind.RenderProcessExited) ||
            !lifecycle.MarkRendererFailed() || lifecycle.CanNavigate) return 6;
        return 0;
    }
}
