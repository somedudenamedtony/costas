# Costas - a station-centric FT8/FT4 client.
# Copyright (C) 2026 Costas contributors. GPLv3; see LICENSE.
#
# Exercises the built installer on Windows (CI): a fresh per-user install, an upgrade over an install that still has a
# pre-rename build's files and shortcuts, the upgrade the app's Update button runs (which reopens the app), a start of the installed app, and an uninstall.
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
if (Get-Process -Name 'Costas' -ErrorAction SilentlyContinue) { throw 'A silent install started the app' }

Write-Host 'Update button: upgrade while the app is running, then reopen it'
# The same hidden cmd that UpdateService.InstallAndReopenCommand builds, started while Costas runs; the app closes
# itself right after, as it does when the operator presses Update.
$running = Start-Process (Join-Path $dir 'Costas.exe') -ArgumentList '--home', (Join-Path $env:RUNNER_TEMP 'costas-update-home') -PassThru
Start-Sleep -Seconds 5
$updateLog = Join-Path $env:RUNNER_TEMP 'costas-update.log'
$app = Join-Path $dir 'Costas.exe'
$script = "start `"`" /wait `"$($setup.FullName)`" /SILENT /SUPPRESSMSGBOXES /NORESTART /NOCANCEL `"/LOG=$updateLog`" & start `"`" `"$app`""
$psi = [System.Diagnostics.ProcessStartInfo]::new((Join-Path $env:SystemRoot 'System32\cmd.exe'), "/d /s /c `"$script`"")
$psi.UseShellExecute = $false
$psi.CreateNoWindow = $true
$psi.WorkingDirectory = $env:RUNNER_TEMP
# The app takes a few seconds to close after starting the installer (it stops jt9, rigctld and its services). A rigctld
# from the install folder that outlives the app by 8 s stands in for that; the installer must wait for it, not fail.
$helper = Start-Process (Join-Path $dir 'third_party\hamlib\bin\rigctld.exe') -ArgumentList '-m', '1', '-t', '4599' -PassThru -WindowStyle Hidden
$waiter = [System.Diagnostics.Process]::Start($psi)
Stop-Process -Id $running.Id
Start-Sleep -Seconds 8
if ($waiter.HasExited) { throw 'The installer finished while rigctld from the install folder was still running' }
Stop-Process -Id $helper.Id
if (-not $waiter.WaitForExit(300000)) { throw 'The update did not finish within 5 minutes' }
if (-not (Select-String -Path $updateLog -Pattern 'Installation process succeeded' -Quiet)) {
    Get-Content $updateLog -Tail 40 | Out-Host
    throw 'The update did not install'
}
if (-not (Select-String -Path $updateLog -Pattern 'Waiting for .* to exit before installing' -Quiet)) {
    Get-Content $updateLog -Tail 40 | Out-Host
    throw 'The installer did not wait for programs from the install folder to exit'
}
$deadline = (Get-Date).AddSeconds(30)
$reopened = $null
while ((Get-Date) -lt $deadline -and -not $reopened) {
    $reopened = Get-Process -Name 'Costas' -ErrorAction SilentlyContinue | Where-Object Id -ne $running.Id | Select-Object -First 1
    if (-not $reopened) { Start-Sleep -Seconds 1 }
}
if (-not $reopened) { throw 'Costas did not reopen after the update' }
if ($reopened.Path -ne $app) { throw "Reopened $($reopened.Path), not the installed app" }
Start-Sleep -Seconds 5
if ($reopened.HasExited) { throw "The reopened app exited by itself (code $($reopened.ExitCode))" }
Stop-Process -Id $reopened.Id -Force
$reopened.WaitForExit(10000) | Out-Null
Write-Host 'The update installed and Costas reopened'

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

Write-Host 'Velopack: a classic install moves over'
$vpSetup = Join-Path $root 'artifacts\velopack\CostasApp-win-Setup.exe'
Assert-File $vpSetup
Start-Process $setup.FullName -ArgumentList $silent -Wait
Assert-File (Join-Path $dir 'Costas.exe')
$vpDir = Join-Path $env:LOCALAPPDATA 'CostasApp'
$vpLog = Join-Path $env:RUNNER_TEMP 'costas-velopack.log'
$p = Start-Process $vpSetup -ArgumentList '--silent', '--log', $vpLog -PassThru -Wait
if ($p.ExitCode -ne 0) {
    if (Test-Path $vpLog) { Get-Content $vpLog -Tail 40 | Out-Host }
    throw "Velopack setup failed (code $($p.ExitCode))"
}
$vpApp = Join-Path $vpDir 'current\Costas.exe'
Assert-File $vpApp
Assert-File (Join-Path $vpDir 'current\third_party\wsjtx\bin\jt9.exe')
Assert-File (Join-Path $vpDir 'current\third_party\hamlib\bin\rigctld.exe')
# A silent Velopack install does not start the app; the update's cmd starts it, as here. The new Costas removes the
# classic install and puts the Start menu shortcut back.
Start-Process $vpApp | Out-Null
$deadline = (Get-Date).AddSeconds(120)
while ((Get-Date) -lt $deadline -and ((Test-Path $uninstallKey) -or (Test-Path (Join-Path $dir 'Costas.exe')))) { Start-Sleep -Seconds 2 }
$running = Get-Process -Name 'Costas' -ErrorAction SilentlyContinue
$appLog = Get-ChildItem (Join-Path $env:LOCALAPPDATA 'Costas\logs') -Filter 'app-*.log' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime | Select-Object -Last 1
if ($appLog) { Get-Content $appLog.FullName -Tail 30 | Out-Host }
if (-not ($running | Where-Object Path -eq $vpApp)) { throw 'Costas from the Velopack install is not running' }
if (Test-Path $uninstallKey) { throw 'The classic install was not removed' }
Assert-File (Join-Path $dir 'Costas.exe') $false
$deadline = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $deadline -and -not (Test-Path (Join-Path $startMenu 'Costas.lnk'))) { Start-Sleep -Seconds 1 }
Assert-File (Join-Path $startMenu 'Costas.lnk')
$running | Stop-Process -Force
Write-Host 'The classic install moved to Velopack'
