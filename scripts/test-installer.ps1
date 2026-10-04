# Costas - a station-centric FT8/FT4 client.
# Copyright (C) 2026 Costas contributors. GPLv3; see LICENSE.
#
# Exercises the built installer on Windows (CI): a fresh per-user install, an upgrade over an install that still has a
# pre-rename build's files and shortcuts, a start of the installed app, and an uninstall.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$setup = Get-ChildItem (Join-Path $root 'artifacts') -Filter 'Costas-Setup-*.exe' | Select-Object -First 1
if (-not $setup) { throw 'No installer in artifacts/' }
$dir = Join-Path $env:LOCALAPPDATA 'Programs\Costas'
$startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{ED0829A3-63BC-4E6C-99CB-5DFF4BB4A021}_is1'
$silent = '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CURRENTUSER', "/DIR=$dir"

function Assert-File($path, $present = $true) {
    if ((Test-Path $path) -ne $present) { throw "Expected $path to be $(if ($present) { 'present' } else { 'gone' })" }
}

Write-Host "Fresh install of $($setup.Name)"
Start-Process $setup.FullName -ArgumentList $silent -Wait
Assert-File (Join-Path $dir 'Costas.exe')
Assert-File (Join-Path $dir 'third_party\wsjtx\bin\jt9.exe')
Assert-File (Join-Path $dir 'third_party\hamlib\bin\rigctld.exe')
Assert-File (Join-Path $startMenu 'Costas.lnk')

Write-Host 'Upgrade over leftovers from a pre-rename build'
Set-Content (Join-Path $dir 'Ft8Client.App.exe') 'old'
Set-Content (Join-Path $dir 'Ft8Client.App.dll') 'old'
Set-Content (Join-Path $startMenu 'FT8 Client.lnk') 'old'
Start-Process $setup.FullName -ArgumentList $silent -Wait
Assert-File (Join-Path $dir 'Costas.exe')
Assert-File (Join-Path $dir 'Ft8Client.App.exe') $false
Assert-File (Join-Path $dir 'Ft8Client.App.dll') $false
Assert-File (Join-Path $startMenu 'FT8 Client.lnk') $false
$entry = Get-ItemProperty $uninstallKey
if ($entry.DisplayName -ne 'Costas') { throw "Uninstall entry is '$($entry.DisplayName)'" }
Write-Host "One Apps & features entry: $($entry.DisplayName) $($entry.DisplayVersion)"

Write-Host 'Start the installed app'
$appHome = Join-Path $env:RUNNER_TEMP 'costas-home'
Remove-Item -Recurse -Force $appHome -ErrorAction SilentlyContinue
$app = Start-Process (Join-Path $dir 'Costas.exe') -ArgumentList '--home', $appHome -PassThru
$deadline = (Get-Date).AddSeconds(60)
$started = $false
while ((Get-Date) -lt $deadline -and -not $app.HasExited) {
    $log = Get-ChildItem (Join-Path $appHome 'logs') -Filter 'app-*.log' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($log -and (Select-String -Path $log.FullName -Pattern 'Costas .* starting' -Quiet)) { $started = $true; break }
    Start-Sleep -Seconds 1
}
Start-Sleep -Seconds 10
$alive = -not $app.HasExited
if ($alive) { Stop-Process -Id $app.Id -Force }
if ($log) { Get-Content $log.FullName | Select-Object -First 40 | Out-Host }
if (-not $started) { throw 'The installed app did not log its start within 60 s' }
if (-not $alive) { throw "The installed app exited by itself (code $($app.ExitCode))" }
Write-Host 'The installed app started and kept running'

Write-Host 'Uninstall'
Start-Process (Join-Path $dir 'unins000.exe') -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -Wait
Start-Sleep -Seconds 3
Assert-File (Join-Path $dir 'Costas.exe') $false
Assert-File (Join-Path $startMenu 'Costas.lnk') $false
if (Test-Path $uninstallKey) { throw 'Uninstall entry still present' }
Write-Host 'Installer checks passed'
