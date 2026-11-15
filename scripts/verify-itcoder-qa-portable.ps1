param(
 [string]$ReleaseDir = '',
 [string]$OutputDir = ''
)
$ErrorActionPreference='Stop'
if(!$ReleaseDir){throw 'Supply -ReleaseDir pointing to published independent browser files'}
if(!$OutputDir){$OutputDir=Join-Path ([IO.Path]::GetTempPath()) ('ITCoderStudyQA-verify-'+[Guid]::NewGuid().ToString('N'))}
$exe=Join-Path $ReleaseDir 'ITCoderStudyQA.exe'
if(!(Test-Path -LiteralPath $exe)){throw 'Missing release exe'}
if(Test-Path -LiteralPath $OutputDir){throw 'Refuse existing QA reports'}
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$results=[ordered]@{}
foreach($flag in @('--self-test','--runtime-check','--ui-preflight')){
 $n=$flag.TrimStart('-')
 $stdout=Join-Path $OutputDir ($n+'.stdout.txt')
 $stderr=Join-Path $OutputDir ($n+'.stderr.txt')
 $p=Start-Process -FilePath $exe -ArgumentList $flag -NoNewWindow -Wait -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
 $results[$n]=[ordered]@{exit=$p.ExitCode;stderrBytes=(Get-Item $stderr).Length}
 if(($flag -ne '--ui-preflight' -and $p.ExitCode -ne 0) -or (($flag -eq '--ui-preflight') -and ($p.ExitCode -notin @(0,40,41))) -or (Get-Item $stderr).Length -gt 0){throw ('Failed release EXE '+$flag)}
}
foreach($mode in @('--diagnose','--verify','--audit-temp-profiles')){
 $n=$mode.TrimStart('-')
 $report=Join-Path $OutputDir ($n+'.json')
 $stdout=Join-Path $OutputDir ($n+'.stdout.txt')
 $stderr=Join-Path $OutputDir ($n+'.stderr.txt')
 $p=Start-Process -FilePath $exe -ArgumentList ($mode+' "'+$report+'"') -NoNewWindow -Wait -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
 $results[$n]=[ordered]@{exit=$p.ExitCode;stderrBytes=(Get-Item $stderr).Length;reportPresent=(Test-Path -LiteralPath $report)}
 if($p.ExitCode -ne 0 -or (Get-Item $stderr).Length -gt 0 -or !(Test-Path -LiteralPath $report)){throw ('Failed release EXE '+$mode)}
}
$diagnose=Get-Content -LiteralPath (Join-Path $OutputDir 'diagnose.json') -Raw | ConvertFrom-Json
$verify=Get-Content -LiteralPath (Join-Path $OutputDir 'verify.json') -Raw | ConvertFrom-Json
$audit=Get-Content -LiteralPath (Join-Path $OutputDir 'audit-temp-profiles.json') -Raw | ConvertFrom-Json
if($diagnose.isOfficialExamClient -ne $false -or $diagnose.browserControllerTested -ne $false -or $diagnose.guiNavigationVerified -ne $false){throw 'False GUI or exam report'}
if($verify.allPassed -ne $true -or $verify.guiVerified -ne $false -or @($verify.checks).Count -ne 13 -or @($verify.checks | Where-Object {!$_.passed}).Count -ne 0){throw 'Compiled checks failed'}
if($audit.deletedCount -ne 0 -or $audit.browserGuiVerified -ne $false){throw 'Profile audit is not read-only'}
$summary=[ordered]@{
 date=[DateTime]::UtcNow.ToString('yyyy-MM-dd')
 kind='Independent browser for ordinary HTTPS browsing, not official secure exam client'
 executable='ITCoderStudyQA.exe'
 executableSha256=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
 exeBytes=(Get-Item $exe).Length
 modes=$results
 compiledPolicyChecksPassed=13
 compiledPolicyChecksTotal=13
 webView2RuntimeDetected=[bool]$diagnose.webView2RuntimePresent
 uiSessionEligible=[bool]$diagnose.uiSessionEligible
 guiVerified=$false
 remoteWebsiteLoginVerified=$false
 officialExamBrowserEquivalent=$false
 temporaryProfileInventoryDeletedCount=$audit.deletedCount
}
$summary | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $OutputDir 'release-qa-summary.json') -Encoding UTF8
$summary|ConvertTo-Json -Depth 7