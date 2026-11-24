[CmdletBinding()]
param(
    [string] $InstallRoot,
    [switch] $NoShellIntegration
)

$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Uninstall TokenHUD on Windows.' }
. (Join-Path $PSScriptRoot 'TokenHud.Installation.ps1')

$root = Get-TokenHudInstallRoot $InstallRoot -NoShellIntegration:$NoShellIntegration
$lock = Enter-TokenHudInstallLock $root
try {
    $app = Join-Path $root 'app'
    $exe = Join-Path $app 'TokenHUD.exe'
    if (Test-Path -LiteralPath $app) {
        Assert-TokenHudOwned $root $app
        Assert-TokenHudStopped $exe
        Remove-TokenHudChild $root $app
    }

    if (-not $NoShellIntegration) { Remove-TokenHudShellEntries $exe }
    Write-Output 'TokenHUD was uninstalled. Settings and metrics history were preserved.'
}
finally {
    $lock.ReleaseMutex()
    $lock.Dispose()
}
