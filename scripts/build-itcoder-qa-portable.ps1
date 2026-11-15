param(
  [string]$RepoRoot = '',
  [string]$Dotnet = 'dotnet',
  [string]$WebView2Nupkg = '',
  [string]$OutputDir = ''
)
# Independent ITCoder Study QA: source-to-Windows-x64 portable EXE.
# Does not execute/patch the official HUTECH program or require GitHub Actions.
$ErrorActionPreference='Stop'
if(!$RepoRoot){ $RepoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path }
$RepoRoot=(Resolve-Path -LiteralPath $RepoRoot).Path
$project=Join-Path $RepoRoot 'windows-itcoder-qa\ITCoderStudyQA.csproj'
if(!(Test-Path -LiteralPath $project -PathType Leaf)){throw 'Independent Windows QA project missing'}
$dc=Get-Command $Dotnet -ErrorAction SilentlyContinue
if(!$dc){throw 'Full .NET 9 SDK is required; point -Dotnet to dotnet.exe'}
$dotnetExe=$dc.Source
$dotnetRoot=Split-Path -Parent $dotnetExe
if(!$OutputDir){
 $buildId=[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8)
 $OutputDir=Join-Path $RepoRoot ('dist\itcoder-study-qa-'+$buildId)
}
$OutputDir=[IO.Path]::GetFullPath($OutputDir)
if(Test-Path -LiteralPath $OutputDir){throw 'Refusing existing output'}
New-Item -ItemType Directory -Path $OutputDir -Force|Out-Null
$published=Join-Path $OutputDir 'published'
$nugetFeed=Join-Path $OutputDir 'offline-feed'
$env:DOTNET_ROOT=$dotnetRoot
$env:DOTNET_CLI_HOME=Join-Path $OutputDir 'dotnet-home'
$env:NUGET_PACKAGES=Join-Path $OutputDir 'nuget-packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_NOLOGO='1'
if($WebView2Nupkg){
 if(!(Test-Path -LiteralPath $WebView2Nupkg -PathType Leaf)){throw 'Offline WebView2 NuGet package missing'}
 if((Split-Path $WebView2Nupkg -Leaf) -ne 'microsoft.web.webview2.1.0.4258.31.nupkg'){throw 'Unexpected WebView2 package name'}
 New-Item -ItemType Directory -Path $nugetFeed -Force|Out-Null
 Copy-Item -LiteralPath $WebView2Nupkg -Destination $nugetFeed
 & $dotnetExe restore $project --source $nugetFeed -p:RuntimeIdentifier= -p:SelfContained=false -p:NuGetAudit=false --disable-parallel -v:q
} else {
 & $dotnetExe restore $project -p:RuntimeIdentifier= -p:SelfContained=false -p:NuGetAudit=false --disable-parallel -v:q
}
if($LASTEXITCODE -ne 0){throw 'Independent browser restore failed'}
& $dotnetExe publish $project -c Release --self-contained false --no-restore -p:RuntimeIdentifier= -p:AppHostDotNetSearch=AppRelative -p:AppHostRelativeDotNet=./runtime -p:PublishSingleFile=false -p:PublishTrimmed=false -o $published -v:q
if($LASTEXITCODE -ne 0){throw 'Independent browser publish failed'}
$runtime=Join-Path $published 'runtime'
New-Item -ItemType Directory -Path $runtime -Force|Out-Null
Copy-Item -LiteralPath $dotnetExe -Destination (Join-Path $runtime 'dotnet.exe')
foreach($section in @('host\fxr','shared\Microsoft.NETCore.App','shared\Microsoft.WindowsDesktop.App')){
 $from=Join-Path $dotnetRoot $section
 if(!(Test-Path -LiteralPath $from)){throw ('Missing .NET runtime: '+$section)}
 $to=Join-Path $runtime $section
 New-Item -ItemType Directory -Path (Split-Path $to -Parent) -Force|Out-Null
 Copy-Item -LiteralPath $from -Destination $to -Recurse -Force
}
$launcher='@echo off'+[Environment]::NewLine+
 'setlocal'+[Environment]::NewLine+
 'set "DOTNET_ROOT=%~dp0runtime"'+[Environment]::NewLine+
 'set "DOTNET_ROOT_X64=%~dp0runtime"'+[Environment]::NewLine+
 '"%~dp0ITCoderStudyQA.exe" %*'+[Environment]::NewLine+
 'endlocal'+[Environment]::NewLine
[IO.File]::WriteAllText((Join-Path $published 'Launch-ITCoderStudyQA.cmd'),$launcher,[Text.Encoding]::ASCII)
$exe=Join-Path $published 'ITCoderStudyQA.exe'
if(!(Test-Path -LiteralPath $exe)){throw 'Executable missing after publish'}
$stdout=Join-Path $OutputDir 'self-test.stdout.txt'
$stderr=Join-Path $OutputDir 'self-test.stderr.txt'
$p=Start-Process -FilePath $exe -ArgumentList '--self-test' -NoNewWindow -Wait -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
if($p.ExitCode -ne 0 -or (Get-Item $stderr).Length -ne 0){throw 'Packaged EXE policy self-test failed'}
$summary=[ordered]@{
 application='Independent ITCoder Study QA'
 officialExamClient=$false
 executable=$exe
 exeSha256=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
 exeBytes=(Get-Item -LiteralPath $exe).Length
 selfTest='PASS'
 bundledDotnetRuntime=$true
 systemWebView2RuntimeRequired=$true
 interactiveGuiVerified=$false
 }
$summary|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $OutputDir 'build-summary.json') -Encoding UTF8
$summary|ConvertTo-Json -Depth 4
