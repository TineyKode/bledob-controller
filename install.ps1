$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dest = Join-Path $env:LOCALAPPDATA "Programs\Bledob"

Get-Process -Name Bledob -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

dotnet publish (Join-Path $root "src\Bledob.App\Bledob.App.csproj") -c Release -o $dest
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$shell = New-Object -ComObject WScript.Shell
$exe = Join-Path $dest "Bledob.exe"
foreach ($link in @(
    (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\BLEDOB.lnk"),
    (Join-Path ([Environment]::GetFolderPath("Desktop")) "BLEDOB.lnk")
)) {
    $shortcut = $shell.CreateShortcut($link)
    $shortcut.TargetPath = $exe
    $shortcut.WorkingDirectory = $dest
    $shortcut.Description = "ELK-BLEDOB strip controller"
    $shortcut.Save()
}

$startup = $shell.CreateShortcut((Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Startup\BLEDOB.lnk"))
$startup.TargetPath = $exe
$startup.Arguments = "--tray"
$startup.WorkingDirectory = $dest
$startup.Description = "ELK-BLEDOB strip controller"
$startup.Save()
Start-Process $exe

Write-Output "Installed to $dest"
