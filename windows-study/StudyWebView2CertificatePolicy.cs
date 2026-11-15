using Microsoft.Web.WebView2.Core;

namespace IndependentStudyBrowser;

/// <summary>
/// Refuse all WebView2 server-certificate verification failures. This
/// independent study browser never offers a TLS warning bypass, certificate
/// allowlist, trust-on-first-use or per-site exception.
/// </summary>
internal static class StudyWebView2CertificatePolicy
{
    internal static CoreWebView2ServerCertificateErrorAction ActionFor(
        CoreWebView2WebErrorStatus errorStatus) =>
        CoreWebView2ServerCertificateErrorAction.Cancel;

    internal static int SelfTest()
    {
        CoreWebView2WebErrorStatus[] invalidCertificates =
        {
            CoreWebView2WebErrorStatus.CertificateCommonNameIsIncorrect,
            CoreWebView2WebErrorStatus.CertificateExpired,
            CoreWebView2WebErrorStatus.ClientCertificateContainsErrors,
            CoreWebView2WebErrorStatus.CertificateRevoked,
            CoreWebView2WebErrorStatus.CertificateIsInvalid
        };

        foreach (var error in invalidCertificates)
            if (ActionFor(error) != CoreWebView2ServerCertificateErrorAction.Cancel)
                return 1;

        // Reject new/unknown verification errors, rather than allowing them.
        if (ActionFor((CoreWebView2WebErrorStatus)int.MaxValue) !=
            CoreWebView2ServerCertificateErrorAction.Cancel)
            return 2;

        return 0;
    }
}
