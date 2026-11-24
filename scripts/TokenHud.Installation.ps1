$TokenHudOwnerMarker = 'TokenHUD private Windows installer v1'

function Get-TokenHudInstallRoot([string] $RequestedRoot, [switch] $NoShellIntegration) {
    if ([bool]$RequestedRoot -ne [bool]$NoShellIntegration) {
        throw 'Supply -InstallRoot and -NoShellIntegration together for an isolated install.'
    }

    if (-not $RequestedRoot) {
        $RequestedRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs\TokenHUD'
    }

    $root = [IO.Path]::GetFullPath(
        $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($RequestedRoot)
    ).TrimEnd('\')
    if ($root -notmatch '^[A-Za-z]:\\') { throw 'InstallRoot must be a local, non-volume-root directory.' }

    for ($cursor = $root; $cursor; $cursor = [IO.Path]::GetDirectoryName($cursor)) {
        if ((Test-Path -LiteralPath $cursor) -and
            ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "InstallRoot crosses a reparse point: $cursor"
        }
    }
    $root
}

function Enter-TokenHudInstallLock([string] $Root) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $name = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Root.ToUpperInvariant()))).Replace('-', '')
    }
    finally { $sha.Dispose() }

    $mutex = [Threading.Mutex]::new($false, "Local\TokenHUD-Install-$name")
    try {
        try { $acquired = $mutex.WaitOne(0) }
        catch [Threading.AbandonedMutexException] { $acquired = $true }
        if (-not $acquired) { throw 'Another TokenHUD install or uninstall is active for this root.' }
        $mutex
    }
    catch {
        $mutex.Dispose()
        throw
    }
}

function Resolve-TokenHudChild([string] $Root, [string] $Path) {
    $item = Get-Item -LiteralPath $Path -Force
    if (-not $item.PSIsContainer -or
        $item.FullName -ne $Path -or
        $item.Parent.FullName -ne $Root -or
        ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Expected a direct, ordinary directory inside '$Root': $Path"
    }
    $item.FullName
}

function Remove-TokenHudChild([string] $Root, [string] $Path) {
    $resolved = Resolve-TokenHudChild $Root $Path
    $pending = [Collections.Generic.Stack[string]]::new()
    $pending.Push($resolved)
    while ($pending.Count -gt 0) {
        foreach ($item in Get-ChildItem -LiteralPath ($pending.Pop()) -Force) {
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Refusing to remove a directory containing a reparse point: $($item.FullName)"
            }
            if ($item.PSIsContainer) { $pending.Push($item.FullName) }
        }
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}

function Assert-TokenHudOwned([string] $Root, [string] $AppDirectory) {
    $app = Resolve-TokenHudChild $Root $AppDirectory
    $marker = Join-Path $app '.tokenhud-install'
    if (-not (Test-Path -LiteralPath $marker -PathType Leaf) -or
        ((Get-Item -LiteralPath $marker -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -or
        (Get-Content -LiteralPath $marker -Raw).Trim() -ne $TokenHudOwnerMarker) {
        throw "Directory is not owned by the TokenHUD installer: $app"
    }
}

function Assert-TokenHudStopped([string] $Executable) {
    $running = Get-Process -Name TokenHUD -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -eq $Executable }
    if ($running) { throw 'Exit this TokenHUD installation from its tray menu and retry.' }
}

function Get-TokenHudShortcutPath {
    Join-Path ([Environment]::GetFolderPath('Programs')) 'Token HUD.lnk'
}

function Set-TokenHudShortcut([string] $Executable) {
    $path = Get-TokenHudShortcutPath
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($path)
    if ((Test-Path -LiteralPath $path) -and $shortcut.TargetPath -ne $Executable) {
        Write-Warning "Start menu shortcut belongs to another application: $path"
        return
    }
    $shortcut.TargetPath = $Executable
    $shortcut.WorkingDirectory = [IO.Path]::GetDirectoryName($Executable)
    $shortcut.Description = 'Token HUD'
    $shortcut.Save()
}

function Remove-TokenHudShellEntries([string] $Executable) {
    $path = Get-TokenHudShortcutPath
    if (Test-Path -LiteralPath $path -PathType Leaf) {
        $shell = New-Object -ComObject WScript.Shell
        $shortcut = $shell.CreateShortcut($path)
        if ($shortcut.TargetPath -eq $Executable) { Remove-Item -LiteralPath $path }
    }

    $runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    $run = Get-ItemProperty -LiteralPath $runKey -Name TokenHUD -ErrorAction SilentlyContinue
    if ($run.TokenHUD -eq ('"{0}" --startup' -f $Executable)) {
        Remove-ItemProperty -LiteralPath $runKey -Name TokenHUD
    }
}
