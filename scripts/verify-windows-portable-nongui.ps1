param(
  [Parameter(Mandatory=$true)][string]$ArchivePath,
  [Parameter(Mandatory=$true)][string]$ExtractedRoot,
  [Parameter(Mandatory=$true)][string]$ReportDirectory,
  [Parameter(Mandatory=$true)][string]$ExpectedArchiveSha256
)
# Independent, bounded, non-GUI QA of an already extracted Windows portable ZIP.
# SAFETY: only execute in Session 0 and only use offline/no-GUI CLI modes.
# Never open the official/vendor browser, a live website, or an existing user profile.
$ErrorActionPreference='Stop'
$zip=(Resolve-Path -LiteralPath $ArchivePath -ErrorAction Stop).Path
$root=(Resolve-Path -LiteralPath $ExtractedRoot -ErrorAction Stop).Path
if(Test-Path -LiteralPath $ReportDirectory){throw 'Refuse to overwrite existing QA report directory'}
$archiveDigest=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
if($archiveDigest -ne $ExpectedArchiveSha256.ToUpperInvariant()){throw 'Unexpected release archive SHA-256'}
$manifestPath=Join-Path $root 'CHECKSUMS.json'
if(!(Test-Path -LiteralPath $manifestPath -PathType Leaf)){throw 'Missing release checksum manifest'}
$manifest=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json
if($manifest.schemaVersion -ne 1 -or !$manifest.independentNotOfficialExamClient){throw 'Unrecognized or official/vendor release manifest'}
$items=@($manifest.files)
if($items.Count -lt 1){throw 'Empty release manifest'}
$seen=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$verified=0
$rootPrefix=$root.TrimEnd('\')+'\'
foreach($item in $items) {
  $relative=[string]$item.path
  if(!$relative -or $relative -match '(?:^/|^[A-Za-z]:|(^|/)\.\.(/|$)|\\)' -or $relative -eq 'CHECKSUMS.json') {
    throw ('Unsafe checksum entry: '+$relative)
  }
  if(!$seen.Add($relative)){throw ('Duplicate checksum entry: '+$relative)}
  $full=[IO.Path]::GetFullPath((Join-Path $root $relative))
  if(!$full.StartsWith($rootPrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Checksum path escaped extraction root'}
  if(!(Test-Path -LiteralPath $full -PathType Leaf)){throw ('Missing release file: '+$relative)}
  $info=Get-Item -LiteralPath $full -Force
  if(($info.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Release file is a reparse point'}
  if($info.Length -ne [long]$item.bytes){throw ('Size mismatch: '+$relative)}
  if((Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash -ne ([string]$item.sha256).ToUpperInvariant()) {
    throw ('SHA-256 mismatch: '+$relative)
  }
  $verified++
}
$actual=@(Get-ChildItem -LiteralPath $root -File -Recurse -Force)
if($actual.Count -ne $items.Count+1){throw ('Unexpected extracted file count: '+$actual.Count)}
foreach($file in $actual){
  if(!$file.FullName.StartsWith($rootPrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected file outside root'}
  $rel=$file.FullName.Substring($rootPrefix.Length).Replace('\','/')
  if($rel -ne 'CHECKSUMS.json' -and !$seen.Contains($rel)){throw ('Unmanifested release file: '+$rel)}
}
$exe=Join-Path $root 'ITCoderStudyQA.exe'
if(!(Test-Path -LiteralPath $exe -PathType Leaf)){throw 'Missing Windows QA executable'}
$preflightExit=40 # required: non-interactive process in Windows Session 0.
function Invoke-QA([string[]]$Arguments) {
  $p=Start-Process -FilePath $exe -ArgumentList $Arguments -PassThru -WindowStyle Hidden
  try{
    if(!$p.WaitForExit(20000)){
      try{Stop-Process -Id $p.Id -Force -ErrorAction Stop}catch{}
      throw ('Timed out executing independent CLI check '+$Arguments[0])
    }
    $p.Refresh()
    return [int]$p.ExitCode
  }finally{$p.Dispose()}
}
# HARD SAFETY GATE: refuse to open the offline smoke form in any
# interactive session, even if it might be a "QA" desktop.
$preflight=Invoke-QA @('--ui-preflight')
if($preflight -ne $preflightExit){throw ('Refusing GUI-capable session: preflight='+$preflight)}
if($verified -ne $items.Count){throw 'Incomplete checksum verification'}
New-Item -ItemType Directory -Path $ReportDirectory -ErrorAction Stop|Out-Null
$policyPath=Join-Path $ReportDirectory 'compiled-policies.json'
$diagnosticsPath=Join-Path $ReportDirectory 'webview2-diagnostics.json'
$checks=@(
  @{name='self-test';arguments=@('--self-test');expected=0},
  @{name='runtime-check';arguments=@('--runtime-check');expected=0},
  @{name='compiled-policies';arguments=@('--verify',$policyPath);expected=0},
  @{name='diagnostics';arguments=@('--diagnose',$diagnosticsPath);expected=0},
  @{name='offline-smoke-guard';arguments=@('--offline-ui-smoke');expected=40}
)
$results=@(@{name='ui-session0-preflight';expected=40;actual=$preflight;passed=$true})
foreach($c in $checks) {
  $got=Invoke-QA $c.arguments
  $pass=$got -eq $c.expected
  $results+=@{name=$c.name;expected=$c.expected;actual=$got;passed=$pass}
  if(!$pass){throw ('Non-GUI release gate failed: '+$c.name+' exit='+$got)}
}
$policy=Get-Content -LiteralPath $policyPath -Raw | ConvertFrom-Json
$diag=Get-Content -LiteralPath $diagnosticsPath -Raw | ConvertFrom-Json
if(!$policy.allPassed -or @($policy.checks).Count -ne 13 -or @($policy.checks | Where-Object {-not $_.passed}).Count){
  throw 'Compiled policies did not pass 13 of 13'
}
if($policy.guiVerified -or $policy.websiteAccessed -or $policy.examBrowserTested){throw 'Policy report incorrectly claims live behavior'}
if(!$diag.webView2RuntimePresent -or $diag.uiSessionEligible -or $diag.uiPreflightExitCode -ne 40 -or $diag.classification -ne 'session-zero-blocked'){
  throw 'Invalid Session 0/WebView2 diagnostics'
}
$report=[ordered]@{
  schemaVersion=1
  subject='Independent ITCoder Windows portable ZIP, NOT HUTECH official examination client'
  archiveName=[IO.Path]::GetFileName($zip)
  archiveSha256=$archiveDigest
  exeSha256=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
  archiveFilesVerified=$verified
  extractedFiles=$actual.Count
  compiledPoliciesPassed=13
  failedChecks=0
  webView2RuntimePresent=$true
  uiPreflightExitCode=40
  offlineSmokeBlockedInSession0=$true
  guiVerified=$false
  liveWebsiteOrLoginTested=$false
  productionChanged=$false
  session2Changed=$false
  githubActionsUsed=$false
  checkResults=$results
}
$dest=Join-Path $ReportDirectory 'windows-portable-nongui-qa.json'
[IO.File]::WriteAllText($dest,($report|ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))
Write-Output ($report|ConvertTo-Json -Depth 6)
