[CmdletBinding()]
param(
    [string] $ArchivePath = (Join-Path $PSScriptRoot 'TokenHUD-win-x64.zip'),
    [string] $ChecksumPath,
    [string] $InstallRoot,
    [switch] $NoShellIntegration
)

$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Install TokenHUD on Windows.' }
. (Join-Path $PSScriptRoot 'TokenHud.Installation.ps1')

function Expand-TokenHudArchive([string] $Archive, [string] $Destination) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($Archive)
    try {
        $prefix = $Destination + '\'
        foreach ($entry in $zip.Entries) {
            $relative = $entry.FullName.Replace('/', '\')
            $parts = $relative.TrimEnd('\').Split('\')
            if ($parts.Where({ $_ -notmatch '^[A-Za-z0-9_][A-Za-z0-9._-]*$' -or $_.EndsWith('.') -or
                $_ -match '^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)' }).Count -gt 0) {
                throw "Invalid path in TokenHUD package: $($entry.FullName)"
            }
            $target = [IO.Path]::GetFullPath((Join-Path $Destination $relative))
            if (-not $target.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Package entry escapes staging directory: $($entry.FullName)"
            }
            if ($relative.EndsWith('\')) {
                [IO.Directory]::CreateDirectory($target) | Out-Null
                continue
            }
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $false)
        }
    }
    finally { $zip.Dispose() }
}

function Assert-TokenHudPayload([string] $Directory) {
    foreach ($file in 'TokenHUD.exe', 'TokenHUD.dll', 'TokenHUD.runtimeconfig.json', 'TokenHud.Core.dll', 'coreclr.dll', 'PresentationFramework.dll', 'hostfxr.dll') {
        if (-not (Test-Path -LiteralPath (Join-Path $Directory $file) -PathType Leaf)) {
            throw "Required Windows payload file is missing: $file"
        }
    }

    $reader = [IO.BinaryReader]::new([IO.File]::OpenRead((Join-Path $Directory 'TokenHUD.exe')))
    try {
        if ($reader.BaseStream.Length -lt 64 -or $reader.ReadUInt16() -ne 0x5A4D) {
            throw 'Packaged TokenHUD.exe is not a Windows executable.'
        }
        $reader.BaseStream.Position = 60
        $offset = $reader.ReadInt32()
        if ($offset -lt 64 -or $offset -gt ($reader.BaseStream.Length - 6)) {
            throw 'Packaged TokenHUD.exe has an invalid PE header.'
        }
        $reader.BaseStream.Position = $offset
        if ($reader.ReadUInt32() -ne 0x4550 -or $reader.ReadUInt16() -ne 0x8664) {
            throw 'Packaged TokenHUD.exe is not built for Windows x64.'
        }
    }
    finally { $reader.Dispose() }
}

$root = Get-TokenHudInstallRoot $InstallRoot -NoShellIntegration:$NoShellIntegration
$archive = (Resolve-Path -LiteralPath $ArchivePath).ProviderPath
if (-not $ChecksumPath) { $ChecksumPath = "$archive.sha256" }
$expected = (Get-Content -LiteralPath $ChecksumPath -Raw).Trim()
if ($expected -notmatch '^[a-f0-9]{64}$') { throw 'The checksum must contain one SHA-256 digest.' }

$lock = Enter-TokenHudInstallLock $root
$stage = $null
$backup = $null
try {
    [IO.Directory]::CreateDirectory($root) | Out-Null
    $root = Get-TokenHudInstallRoot $InstallRoot -NoShellIntegration:$NoShellIntegration
    $app = Join-Path $root 'app'
    $exe = Join-Path $app 'TokenHUD.exe'
    if (Test-Path -LiteralPath $app) {
        Assert-TokenHudOwned $root $app
        Assert-TokenHudStopped $exe
    }

    $stage = Join-Path $root ('.stage-' + [Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($stage) | Out-Null
    $archiveCopy = Join-Path $stage 'package.zip'
    Copy-Item -LiteralPath $archive -Destination $archiveCopy
    if ((Get-FileHash -LiteralPath $archiveCopy -Algorithm SHA256).Hash -ne $expected) {
        throw 'Package checksum mismatch. Obtain the ZIP and checksum from the same trusted build.'
    }

    $payload = Join-Path $stage 'payload'
    [IO.Directory]::CreateDirectory($payload) | Out-Null
    Expand-TokenHudArchive $archiveCopy $payload
    Assert-TokenHudPayload $payload
    Set-Content -LiteralPath (Join-Path $payload '.tokenhud-install') -Value $TokenHudOwnerMarker -Encoding Ascii

    if (Test-Path -LiteralPath $app) {
        Assert-TokenHudOwned $root $app
        Assert-TokenHudStopped $exe
        $backup = Join-Path $root ('.backup-' + [Guid]::NewGuid().ToString('N'))
        [IO.Directory]::Move((Resolve-TokenHudChild $root $app), $backup)
    }

    try {
        $safeStage = Resolve-TokenHudChild $root $stage
        [IO.Directory]::Move((Resolve-TokenHudChild $safeStage $payload), $app)
    }
    catch {
        if ($backup -and (Test-Path -LiteralPath $backup)) {
            [IO.Directory]::Move((Resolve-TokenHudChild $root $backup), $app)
        }
        throw
    }

    if ($backup) { Remove-TokenHudChild $root $backup }
    if (-not $NoShellIntegration) {
        try { Set-TokenHudShortcut $exe }
        catch { Write-Warning "TokenHUD was installed, but the Start menu shortcut could not be created: $_" }
    }
    Write-Output "Installed: $exe"
}
finally {
    try {
        if ($stage -and (Test-Path -LiteralPath $stage)) { Remove-TokenHudChild $root $stage }
    }
    finally {
        $lock.ReleaseMutex()
        $lock.Dispose()
    }
}
