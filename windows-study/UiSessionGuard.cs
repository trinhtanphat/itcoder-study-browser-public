using System;
using System.Diagnostics;

namespace IndependentStudyBrowser;

/// <summary>
/// Guard against launching WinForms/WebView2 controls from noninteractive service
/// contexts. Preflight checks are non-invasive and never instantiate a browser.
/// They do not prove that a GUI will work in a permitted session.
/// </summary>
internal static class UiSessionGuard
{
    internal const int SessionZero = 40;
    internal const int NonInteractive = 41;
    internal const int Unknown = 42;

    internal static int Evaluate(int sessionId, bool userInteractive)
    {
        if (sessionId < 0) return Unknown;
        if (sessionId == 0) return SessionZero;
        if (!userInteractive) return NonInteractive;
        return 0;
    }

    internal static int Preflight()
    {
        try
        {
            using Process current = Process.GetCurrentProcess();
            return Evaluate(current.SessionId, Environment.UserInteractive);
        }
        catch (Exception)
        {
            return Unknown;
        }
    }

    internal static int SelfTest()
    {
        if (Evaluate(0, true) != SessionZero) return 1;
        if (Evaluate(0, false) != SessionZero) return 2;
        if (Evaluate(2, false) != NonInteractive) return 3;
        if (Evaluate(2, true) != 0) return 4;
        if (Evaluate(-1, true) != Unknown) return 5;
        return 0;
    }
}
