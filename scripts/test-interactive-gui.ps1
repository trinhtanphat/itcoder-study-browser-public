param(
  [Parameter(Mandatory=$true)][string]$ExecutablePath,
  [Parameter(Mandatory=$true)][ValidateRange(1,65535)][int]$QaSessionId,
  [Parameter(Mandatory=$true)][string]$ReportPath,
  [string]$ExpectedExeSha256 = '3ED8F83BDECC2F4FB8CB7ACA1552BB48B7CD30605CDA47308151ED4A8FAF3062',
  [ValidateRange(5,120)][int]$StartupTimeoutSeconds = 35
)
# Independent, non-exam GUI acceptance on an isolated interactive QA desktop.
# Strictly refuses Session 0, the user's existing WIN11-181 Session 2,
# all mismatching/noninteractive sessions, and unknown vendor EXEs.
# Uses only https://example.org/ and never logs credentials or cookies.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$qaProcess = $null
$sessionId = [Diagnostics.Process]::GetCurrentProcess().SessionId
if ($sessionId -le 0 -or $sessionId -eq 2 -or $QaSessionId -eq 2 -or
    $sessionId -ne $QaSessionId -or ![Environment]::UserInteractive) {
  throw 'QA_SESSION_GUARD: requires a dedicated interactive Windows QA session other than Session 2'
}
if (Test-Path -LiteralPath $ReportPath) {
  throw 'QA_REPORT_GUARD: refusing to overwrite a previous report'
}
$exe = (Resolve-Path -LiteralPath $ExecutablePath -ErrorAction Stop).Path
if ((Split-Path -Leaf $exe) -cne 'ITCoderStudyQA.exe') {
  throw 'QA_BINARY_GUARD: only the independently authored study browser is permitted'
}
if ($ExpectedExeSha256 -notmatch '^[a-fA-F0-9]{64}$') {
  throw 'QA_BINARY_GUARD: expected SHA-256 is invalid'
}
$exeHash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
if ($exeHash -ne $ExpectedExeSha256.ToUpperInvariant()) {
  throw 'QA_BINARY_GUARD: executable does not match the pinned public QA build'
}
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$testUrl = 'https://example.org/'
$checks = New-Object 'System.Collections.Generic.List[object]'
function Add-Check([string]$name, [bool]$passed) {
  $checks.Add([ordered]@{ name = $name; passed = $passed })
}
try {
  $qaProcess = Start-Process -FilePath $exe -ArgumentList @('--url', $testUrl) -PassThru -WorkingDirectory (Split-Path -Parent $exe)
  if ($qaProcess.SessionId -ne $QaSessionId) {
    throw 'QA_PROCESS_SESSION_GUARD: test child escaped QA session'
  }
  $deadline = [DateTime]::UtcNow.AddSeconds($StartupTimeoutSeconds)
  $window = $null
  while ([DateTime]::UtcNow -lt $deadline) {
    Start-Sleep -Milliseconds 300
    $qaProcess.Refresh()
    if ($qaProcess.HasExited) { break }
    if ($qaProcess.MainWindowHandle -ne [IntPtr]::Zero) {
      try {
        $window = [System.Windows.Automation.AutomationElement]::FromHandle($qaProcess.MainWindowHandle)
        if ($null -ne $window) { break }
      } catch {
        $window = $null
      }
    }
  }
  Add-Check 'native-window-created' ($null -ne $window)
  $title = ''
  $buttons = @()
  $addressValue = ''
  $labels = @()
  if ($null -ne $window) {
    $title = [string]$window.Current.Name
    Add-Check 'independent-not-exam-branding' ($title -match '^ITCoder Study QA' -and $title -match 'NOT exam client')
    $buttonCondition = [System.Windows.Automation.PropertyCondition]::new(
      [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
      [System.Windows.Automation.ControlType]::Button)
    $elements = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $buttonCondition)
    foreach ($item in $elements) { $buttons += [string]$item.Current.Name }
    foreach ($name in @('Back','Forward','Reload','Home','Go')) {
      Add-Check ('toolbar-'+$name.ToLowerInvariant()) ($name -in $buttons)
    }
    $editCondition = [System.Windows.Automation.PropertyCondition]::new(
      [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
      [System.Windows.Automation.ControlType]::Edit)
    $editElements = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $editCondition)
    foreach ($item in $editElements) {
      try {
        $pattern = [System.Windows.Automation.ValuePattern]$item.GetCurrentPattern(
          [System.Windows.Automation.ValuePattern]::Pattern)
        if ($null -ne $pattern -and $pattern.Current.Value -eq $testUrl) {
          $addressValue = $pattern.Current.Value
          break
        }
      } catch { }
    }
    Add-Check 'validated-https-address' ($addressValue -eq $testUrl)
    $textCondition = [System.Windows.Automation.PropertyCondition]::new(
      [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
      [System.Windows.Automation.ControlType]::Text)
    $textElements = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $textCondition)
    foreach ($item in $textElements) { $labels += [string]$item.Current.Name }
    Add-Check 'independent-banner' (@($labels | Where-Object { $_ -match 'INDEPENDENT WEBVIEW2|Independent WebView2' }).Count -gt 0)
  }
  # WebView2 navigation is asynchronous; observe the explicit HTTPS
  # navigation result without trusting the initial UI label snapshot.
  $navigationObserved = $false
  if ($null -ne $window) {
    $navigationDeadline = [DateTime]::UtcNow.AddSeconds([Math]::Min(20, $StartupTimeoutSeconds))
    while ([DateTime]::UtcNow -lt $navigationDeadline) {
      try {
        $statusElements = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $textCondition)
        $statuses = @()
        foreach ($item in $statusElements) { $statuses += [string]$item.Current.Name }
        if (@($statuses | Where-Object { $_ -match '^Ordinary HTTPS page loaded$' }).Count -gt 0) {
          $navigationObserved = $true
          break
        }
        if (@($statuses | Where-Object { $_ -match '^WebView2 could not complete HTTPS navigation$' }).Count -gt 0) {
          break
        }
      } catch { }
      Start-Sleep -Milliseconds 350
    }
  }
  Add-Check 'https-navigation-completed' $navigationObserved
  $passed = @($checks | Where-Object { !$_.passed }).Count -eq 0 -and $checks.Count -eq 10
  $report = [ordered]@{
    schemaVersion = 1
    subject = 'Independent ITCoder Study QA Windows native GUI acceptance, not official HUTECH exam client'
    mode = 'isolated-interactive-qa'
    sessionId = $QaSessionId
    exeSha256 = $exeHash
    syntheticUrl = $testUrl
    checks = @($checks.ToArray())
    nativeGuiVerified = $passed
    httpsNavigationObserved = $navigationObserved
    websiteLoginTested = $false
    officialExamParityVerified = $false
    productionChanged = $false
    userSession2Touched = $false
  }
  $destination = [IO.Path]::GetFullPath($ReportPath)
  $parent = Split-Path -Parent $destination
  if (!(Test-Path -LiteralPath $parent)) { throw 'QA_REPORT_GUARD: destination folder must already exist' }
  [IO.File]::WriteAllText($destination, ($report | ConvertTo-Json -Depth 7), [Text.UTF8Encoding]::new($false))
  Write-Output ($report | ConvertTo-Json -Depth 7)
  if (!$passed) { throw 'QA_GUI_ACCEPTANCE_FAILED: see sanitized report' }
} finally {
  if ($null -ne $qaProcess) {
    try {
      if (!$qaProcess.HasExited) {
        [void]$qaProcess.CloseMainWindow()
        if (!$qaProcess.WaitForExit(5000)) {
          # Terminate only this QA-created process. Never terminate other windows.
          $qaProcess.Kill()
          [void]$qaProcess.WaitForExit(3000)
        }
      }
    } catch { }
    $qaProcess.Dispose()
  }
}
