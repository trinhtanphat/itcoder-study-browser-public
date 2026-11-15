# ITCoder Study QA v51 (Windows x64)

This is an **independently developed study/QA WebView2 browser**. It is NOT
the official HUTECH IT Browser, not a secure exam/lockdown client, and does not
imitate or forge the official application's authentication/attestation signals.

The app starts at the public `https://itcoder.hutech.edu.vn/` home page.
Any normal website login is performed by the user directly within WebView2;
credentials are never requested, logged or transferred by this project. The
ITCoder service may refuse features requiring its official secured browser.

The QA browser **has no virtual-machine/hypervisor detection or blocking UI by
design**. That is a property of this independent app; this project neither
patches, modifies, nor disables the official HUTECH executable or its checks.

For a separate, optional persistent WebView2 login profile, run:
```
ITCoderStudyQA.exe --remember-session
```

Run without arguments to use a temporary profile. The application must be
launched in an interactive Windows desktop session, not SYSTEM Session 0.
Compile from `ITCoderStudyQA.csproj`, which reuses the independently written
sources in `../windows-study/`. It does not bundle any decompiled vendor code.
The build's window title includes “NOT exam client” to avoid confusion.
