param(
  [Parameter(Mandatory=$true)][string]$PublishedDirectory,
  [Parameter(Mandatory=$true)][string]$OutputDirectory,
  [string]$Version = '0.5.9'
)
# Independent study browser only. No vendor installer, identity, or session data.
# Refuse overwrites and keep failed QA evidence for diagnosis.
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$') { throw 'Invalid semantic release version' }
$src = (Resolve-Path -LiteralPath $PublishedDirectory -ErrorAction Stop).Path.TrimEnd('\')
$dst = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\')
if (Test-Path -LiteralPath $dst) { throw 'Refusing existing release output directory' }
if ($dst.StartsWith(($src + '\'), [StringComparison]::OrdinalIgnoreCase) -or
    $src.StartsWith(($dst + '\'), [StringComparison]::OrdinalIgnoreCase) -or
    $dst -eq $src) { throw 'Source and output must be disjoint' }
foreach ($required in @('ITCoderStudyQA.exe', 'ITCoderStudyQA.dll', 'Launch-ITCoderStudyQA.cmd')) {
  if (!(Test-Path -LiteralPath (Join-Path $src $required) -PathType Leaf)) {
    throw ('Missing independent publish file: ' + $required)
  }
}
$all = @(Get-ChildItem -LiteralPath $src -Recurse -Force -ErrorAction Stop)
foreach ($entry in $all) {
  if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw ('Symlink/reparse point in published release: ' + $entry.Name)
  }
}
$files = @($all | Where-Object { !$_.PSIsContainer } | Sort-Object FullName)
if ($files.Count -lt 10) { throw 'Suspiciously small independent release' }
if (@($files | Where-Object { $_.Name -eq 'CHECKSUMS.json' }).Count) {
  throw 'Refusing pre-existing checksum manifest'
}
New-Item -ItemType Directory -Path $dst -ErrorAction Stop | Out-Null
$stage = Join-Path $dst 'staging'
New-Item -ItemType Directory -Path $stage -ErrorAction Stop | Out-Null
$prefix = $src + '\'
$items = New-Object 'System.Collections.Generic.List[object]'
$seen = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach ($file in $files) {
  if (!$file.FullName.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Source file escaped publish root'
  }
  $relative = $file.FullName.Substring($prefix.Length).Replace('\', '/')
  if (!$seen.Add($relative) -or $relative -eq 'CHECKSUMS.json' -or
      $relative -match '(^|/)\.\.(/|$)|^[A-Za-z]:|^/') {
    throw ('Unsafe or duplicate release path: ' + $relative)
  }
  $target = Join-Path $stage ($relative.Replace('/', '\'))
  $targetParent = Split-Path -Parent $target
  if (!(Test-Path -LiteralPath $targetParent)) {
    New-Item -ItemType Directory -Path $targetParent -Force | Out-Null
  }
  Copy-Item -LiteralPath $file.FullName -Destination $target -ErrorAction Stop
  $items.Add([ordered]@{
    path = $relative
    bytes = (Get-Item -LiteralPath $target).Length
    sha256 = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
  })
}
$manifest = [ordered]@{
  schemaVersion = 1
  independentNotOfficialExamClient = $true
  version = $Version
  files = @($items.ToArray())
}
[IO.File]::WriteAllText(
  (Join-Path $stage 'CHECKSUMS.json'),
  ($manifest | ConvertTo-Json -Depth 7),
  [Text.UTF8Encoding]::new($false)
)
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = Join-Path $dst ('ITCoderStudyQA-v' + $Version + '-Windows-x64-portable.zip')
[IO.Compression.ZipFile]::CreateFromDirectory(
  $stage, $archive, [IO.Compression.CompressionLevel]::Optimal, $false
)
$digest = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
$extracted = Join-Path $dst 'fresh-extraction'
[IO.Compression.ZipFile]::ExtractToDirectory($archive, $extracted)
$reports = Join-Path $dst 'qa-reports'
$gateArgs = @{
  ArchivePath = $archive
  ExtractedRoot = $extracted
  ReportDirectory = $reports
  ExpectedArchiveSha256 = $digest
}
& (Join-Path $PSScriptRoot 'verify-windows-portable-nongui.ps1') @gateArgs | Out-Null
if (!$?) { throw 'Fresh release non-GUI verification failed' }
$qa = Get-Content -LiteralPath (Join-Path $reports 'windows-portable-nongui-qa.json') -Raw | ConvertFrom-Json
if ($qa.failedChecks -ne 0 -or !$qa.offlineSmokeBlockedInSession0 -or $qa.guiVerified -or
    $qa.archiveFilesVerified -ne $files.Count -or $qa.archiveSha256 -ne $digest) {
  throw 'Release verification report disagrees with package'
}
$summary = [ordered]@{
  version = $Version
  source = 'Independent ITCoder Study Browser (not official HUTECH exam browser)'
  archive = $archive
  archiveSHA256 = $digest
  exeSHA256 = $qa.exeSha256
  filesVerified = $qa.archiveFilesVerified
  testsPassed = @($qa.checkResults | Where-Object { $_.passed }).Count
  compiledPoliciesPassed = $qa.compiledPoliciesPassed
  guiVerified = $false
  liveLoginVerified = $false
  productionChanged = $false
}
[IO.File]::WriteAllText(
  (Join-Path $dst 'release-summary.json'),
  ($summary | ConvertTo-Json -Depth 5),
  [Text.UTF8Encoding]::new($false)
)
$summary | ConvertTo-Json -Depth 5
