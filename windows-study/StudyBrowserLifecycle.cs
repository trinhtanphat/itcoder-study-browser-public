namespace IndependentStudyBrowser;

/// <summary>
/// UI-independent lifecycle reducer. Events cannot resurrect a failed, timed-out
/// or closed controller. Reopen the application to use a fresh profile.
/// </summary>
internal enum StudyBrowserPhase
{
    Starting,
    Ready,
    TimedOut,
    InitializationFailed,
    RendererFailed,
    Closed
}

internal sealed class StudyBrowserLifecycle
{
    internal StudyBrowserPhase Phase { get; private set; } = StudyBrowserPhase.Starting;
    internal bool CanNavigate => Phase == StudyBrowserPhase.Ready;
    internal bool IsStarting => Phase == StudyBrowserPhase.Starting;
    internal bool IsClosed => Phase == StudyBrowserPhase.Closed;

    internal bool MarkReady()
    {
        if (!IsStarting) return false;
        Phase = StudyBrowserPhase.Ready;
        return true;
    }

    internal bool MarkTimedOut()
    {
        if (!IsStarting) return false;
        Phase = StudyBrowserPhase.TimedOut;
        return true;
    }

    internal bool MarkInitializationFailed()
    {
        if (!IsStarting) return false;
        Phase = StudyBrowserPhase.InitializationFailed;
        return true;
    }

    internal bool MarkRendererFailed()
    {
        if (!CanNavigate) return false;
        Phase = StudyBrowserPhase.RendererFailed;
        return true;
    }

    internal void Close() => Phase = StudyBrowserPhase.Closed;

    internal static int SelfTest()
    {
        var success = new StudyBrowserLifecycle();
        if (success.CanNavigate || success.Phase != StudyBrowserPhase.Starting) return 1;
        if (!success.MarkReady() || !success.CanNavigate) return 2;
        if (success.MarkReady() || success.MarkTimedOut()) return 3;
        if (!success.MarkRendererFailed() || success.CanNavigate ||
            success.Phase != StudyBrowserPhase.RendererFailed) return 4;
        if (success.MarkReady() || success.MarkTimedOut() || success.MarkRendererFailed()) return 5;
        success.Close();
        if (!success.IsClosed || success.MarkReady() || success.MarkTimedOut() ||
            success.MarkRendererFailed()) return 6;

        var timeout = new StudyBrowserLifecycle();
        if (!timeout.MarkTimedOut() || timeout.MarkReady() ||
            timeout.CanNavigate || timeout.Phase != StudyBrowserPhase.TimedOut) return 7;
        timeout.Close();
        if (timeout.Phase != StudyBrowserPhase.Closed) return 8;

        var immediateClose = new StudyBrowserLifecycle();
        immediateClose.Close();
        if (immediateClose.MarkReady() || immediateClose.CanNavigate) return 9;
        var failure = new StudyBrowserLifecycle();
        if (!failure.MarkInitializationFailed() || failure.MarkReady() ||
            failure.MarkTimedOut() || failure.CanNavigate) return 10;
        failure.Close();
        if (!failure.IsClosed) return 11;
        return 0;
    }
}
