[CmdletBinding()]
param([string] $OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\windows-x64'))

$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Build the Windows package on Windows.' }
$OutputDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
$project = Join-Path $PSScriptRoot '..\src\TokenHud.App\TokenHud.App.csproj'
$stage = Join-Path $OutputDirectory ('.build-' + [Guid]::NewGuid().ToString('N'))
$publish = Join-Path $stage 'publish'
$archiveName = 'TokenHUD-win-x64.zip'
New-Item -ItemType Directory -Path $stage -Force | Out-Null

try {
    dotnet publish $project --configuration Release --runtime win-x64 --self-contained true --output $publish -p:ContinuousIntegrationBuild=true
    if ($LASTEXITCODE -ne 0) { throw "TokenHUD publish failed with exit code $LASTEXITCODE." }
    if (-not (Test-Path -LiteralPath (Join-Path $publish 'TokenHUD.exe') -PathType Leaf)) {
        throw 'Publish did not produce TokenHUD.exe.'
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = Join-Path $stage $archiveName
    $files = [IO.Directory]::GetFiles($publish, '*', [IO.SearchOption]::AllDirectories)
    [Array]::Sort($files, [StringComparer]::Ordinal)
    $zip = [IO.Compression.ZipFile]::Open($archive, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in $files) {
            $entryName = $file.Substring($publish.Length + 1).Replace('\', '/')
            $entry = $zip.CreateEntry($entryName)
            $entry.LastWriteTime = [DateTimeOffset]::new(2000, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
            $source = [IO.File]::OpenRead($file)
            try {
                $target = $entry.Open()
                try { $source.CopyTo($target) }
                finally { $target.Dispose() }
            }
            finally { $source.Dispose() }
        }
    }
    finally { $zip.Dispose() }
    $checksum = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
    Set-Content -LiteralPath "$archive.sha256" -Value $checksum -Encoding Ascii

    foreach ($script in 'Install-TokenHud.ps1', 'Uninstall-TokenHud.ps1', 'TokenHud.Installation.ps1') {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $script) -Destination $OutputDirectory
    }
    Move-Item -LiteralPath $archive -Destination (Join-Path $OutputDirectory $archiveName) -Force
    Move-Item -LiteralPath "$archive.sha256" -Destination (Join-Path $OutputDirectory "$archiveName.sha256") -Force
    Write-Output "Private package: $(Join-Path $OutputDirectory $archiveName)"
    Write-Output "SHA-256: $checksum"
}
finally {
    $resolvedStage = (Resolve-Path -LiteralPath $stage).ProviderPath
    if ($resolvedStage -ne $stage -or [IO.Path]::GetDirectoryName($resolvedStage) -ne $OutputDirectory.TrimEnd('\')) {
        throw "Unexpected package staging path: $resolvedStage"
    }
    Remove-Item -LiteralPath $resolvedStage -Recurse -Force
}
