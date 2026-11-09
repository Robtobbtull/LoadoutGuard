# Starts LoadoutGuard without an exe file: PowerShell compiles the source in src\ in memory and runs it.
# Smart App Control blocks unsigned exe files, but not this. install.ps1 sets it up; see the README.
#   powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File LoadoutGuard.ps1
try
{
    $ErrorActionPreference = 'Stop'
    # how to start LoadoutGuard again, for Start with Windows
    $env:LOADOUTGUARD_COMMAND = 'powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File "' + $PSCommandPath + '"'
    $sources = (Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter *.cs).FullName
    $types = Add-Type -Path $sources -ReferencedAssemblies System.Windows.Forms, System.Drawing -IgnoreWarnings -PassThru -WarningAction SilentlyContinue
    $program = $types | Where-Object Name -eq 'Program'
    $program.GetMethod('Main', [Reflection.BindingFlags]'Static, NonPublic').Invoke($null, @(, [string[]]$args)) | Out-Null
}
catch
{
    # the window is hidden, so show what went wrong
    Add-Type -AssemblyName System.Windows.Forms
    [Windows.Forms.MessageBox]::Show("LoadoutGuard could not start:`n`n$_", 'LoadoutGuard') | Out-Null
}
