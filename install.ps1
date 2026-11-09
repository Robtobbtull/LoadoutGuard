# Installs LoadoutGuard without an exe file, for PCs where Smart App Control blocks LoadoutGuard.exe.
# Run in PowerShell:
#   irm https://raw.githubusercontent.com/Robtobbtull/LoadoutGuard/main/install.ps1 | iex
# It downloads the source of the latest release to %LOCALAPPDATA%\LoadoutGuard, adds LoadoutGuard to the
# Start menu and starts it. Each start compiles the source in memory (see LoadoutGuard.ps1).
# Run it again to update.
& {
    $ErrorActionPreference = 'Stop'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 # Windows PowerShell may default to TLS 1.0
    $repo = 'Robtobbtull/LoadoutGuard'
    $dir = Join-Path $env:LOCALAPPDATA 'LoadoutGuard'

    $tag = (Invoke-RestMethod "https://api.github.com/repos/$repo/releases/latest").tag_name
    Write-Host "Downloading LoadoutGuard $tag..."
    $temp = Join-Path $env:TEMP "LoadoutGuard-$tag"
    Remove-Item $temp, "$temp.zip" -Recurse -Force -ErrorAction SilentlyContinue
    Invoke-WebRequest "https://github.com/$repo/archive/refs/tags/$tag.zip" -OutFile "$temp.zip" -UseBasicParsing
    Expand-Archive "$temp.zip" $temp
    $source = (Get-ChildItem $temp -Directory)[0].FullName

    New-Item $dir -ItemType Directory -Force | Out-Null
    Remove-Item "$dir\src" -Recurse -Force -ErrorAction SilentlyContinue # a file dropped from a release must not stay behind
    Copy-Item "$source\src" $dir -Recurse
    Copy-Item "$source\LoadoutGuard.ps1" $dir -Force
    Remove-Item $temp, "$temp.zip" -Recurse -Force

    $powershell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $arguments = '-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File "' + (Join-Path $dir 'LoadoutGuard.ps1') + '"'
    $link = (New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path ([Environment]::GetFolderPath('Programs')) 'LoadoutGuard.lnk'))
    $link.TargetPath = $powershell
    $link.Arguments = $arguments
    $link.Description = 'Reminds you to save your ship insurance in Star Citizen'
    $link.Save()

    $running = $null
    if ([Threading.Mutex]::TryOpenExisting('LoadoutGuard.SingleInstance', [ref]$running))
    {
        $running.Dispose()
        Write-Host "LoadoutGuard $tag is installed. An older copy is running: exit it from its tray icon, then start LoadoutGuard from the Start menu."
    }
    else
    {
        Start-Process $powershell $arguments
        Write-Host "LoadoutGuard $tag is installed and starting. Next time, start it from the Start menu."
    }
}
