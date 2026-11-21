#Requires -Version 7.0
<#
.SYNOPSIS
    Builds the Windows and Linux release packages of Videofy.

.DESCRIPTION
    Reads the version from Utils/AppConstants.cs, publishes self-contained single-file builds
    for win-x64 and linux-x64 into clean folders, and packs them the same way as earlier releases:
      artifacts/v<version>/Videofy-v<version>-Win.zip       (Videofy.exe only)
      artifacts/v<version>/Videofy-v<version>-linux.tar.gz  (Videofy only, mode 755)
    It also writes SHA256SUMS.txt next to the packages.

    The Linux archive is written with System.Formats.Tar, so the executable bit is set inside
    the archive even when packing on Windows. No WSL needed.

.EXAMPLE
    pwsh ./release.ps1
#>
[CmdletBinding()]
param(
    # Build even when the working tree has uncommitted changes.
    [switch]$AllowDirty
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root    = $PSScriptRoot
$project = Join-Path $root 'Video Size Optimizer/Video Size Optimizer.csproj'
$consts  = Join-Path $root 'Video Size Optimizer/Utils/AppConstants.cs'

# Version from AppConstants.AppVersion, e.g. "v1.4.4".
$match = Select-String -Path $consts -Pattern 'AppVersion\s*=\s*"(v[^"]+)"'
if (-not $match) { throw "AppVersion not found in $consts" }
$version = $match.Matches[0].Groups[1].Value
Write-Host "Version: $version" -ForegroundColor Cyan

# Uncommitted changes would end up in the build but not in git.
$dirty = git -C $root status --porcelain
if ($dirty -and -not $AllowDirty) {
    throw "Working tree has uncommitted changes. Commit them first, or run with -AllowDirty."
}

$outDir  = Join-Path $root "artifacts/$version"
$tempDir = Join-Path $outDir 'publish'
if (Test-Path $outDir) { Remove-Item -Recurse -Force $outDir }
New-Item -ItemType Directory -Force $tempDir | Out-Null

function Publish-Build([string]$rid) {
    $dir = Join-Path $tempDir $rid
    Write-Host "Publishing $rid ..." -ForegroundColor Cyan
    dotnet publish $project -r $rid -c Release --self-contained true -o $dir `
        /p:PublishSingleFile=true /p:PublishTrimmed=false `
        /p:IncludeNativeLibrariesForSelfExtract=true /p:DebugType=none --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $rid (exit code $LASTEXITCODE)" }
    return $dir
}

$winDir   = Publish-Build 'win-x64'
$linuxDir = Publish-Build 'linux-x64'

$winExe   = Join-Path $winDir 'Videofy.exe'
$linuxExe = Join-Path $linuxDir 'Videofy'
foreach ($f in $winExe, $linuxExe) { if (-not (Test-Path $f)) { throw "Missing build output: $f" } }

# Windows: zip with only Videofy.exe.
$zipPath = Join-Path $outDir "Videofy-$version-Win.zip"
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
        $zip, $winExe, 'Videofy.exe', [System.IO.Compression.CompressionLevel]::Optimal)
} finally { $zip.Dispose() }

# Linux: tar.gz with only Videofy, executable (rwxr-xr-x), owner root.
$tarPath = Join-Path $outDir "Videofy-$version-linux.tar.gz"
$fileStream = [System.IO.File]::Create($tarPath)
try {
    $gzip = [System.IO.Compression.GZipStream]::new($fileStream, [System.IO.Compression.CompressionLevel]::Optimal)
    try {
        $tar = [System.Formats.Tar.TarWriter]::new($gzip, [System.Formats.Tar.TarEntryFormat]::Ustar, $false)
        try {
            $entry = [System.Formats.Tar.UstarTarEntry]::new([System.Formats.Tar.TarEntryType]::RegularFile, 'Videofy')
            $entry.Mode = [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute, GroupRead, GroupExecute, OtherRead, OtherExecute'
            $entry.ModificationTime = (Get-Item $linuxExe).LastWriteTimeUtc
            $entry.Uid = 0; $entry.Gid = 0; $entry.UserName = 'root'; $entry.GroupName = 'root'
            $source = [System.IO.File]::OpenRead($linuxExe)
            try { $entry.DataStream = $source; $tar.WriteEntry($entry) } finally { $source.Dispose() }
        } finally { $tar.Dispose() }
    } finally { $gzip.Dispose() }
} finally { $fileStream.Dispose() }

Remove-Item -Recurse -Force $tempDir

# Checksums, in the format "sha256sum -c" reads.
$sums = Get-ChildItem $outDir -File | Where-Object Name -ne 'SHA256SUMS.txt' | Sort-Object Name | ForEach-Object {
    "{0}  {1}" -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name
}
$sums | Set-Content -Path (Join-Path $outDir 'SHA256SUMS.txt') -Encoding utf8NoBOM

Write-Host ""
Write-Host "Done: $outDir" -ForegroundColor Green
Get-ChildItem $outDir -File | ForEach-Object { "{0,-34} {1,8:N1} MB" -f $_.Name, ($_.Length / 1MB) }
Write-Host ""
$sums
