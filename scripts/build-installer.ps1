# Ft8Client - a station-centric FT8/FT4 client.
# Copyright (C) 2026 Ft8Client contributors. GPLv3; see LICENSE.
#
# Builds the Windows installer: publishes the app self-contained for win-x64, stages jt9 (WSJT-X) and rigctld (Hamlib)
# with their licences, smoke-tests both from the staged folder, then compiles installer/Ft8Client.iss with Inno Setup.
# Output: artifacts/Ft8Client-Setup-<version>.exe
param([string]$BuildNumber = '')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root

$version = (Select-Xml -Path (Join-Path $root 'Directory.Build.props') -XPath '//Version').Node.InnerText
if ($BuildNumber) { $version = "$version.$BuildNumber" }
$art = Join-Path $root 'artifacts'
$stage = Join-Path $art 'stage'
Remove-Item -Recurse -Force $art -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $stage | Out-Null

# Third-party binaries.
$tp = Join-Path $root 'third_party'
if (-not (Test-Path (Join-Path $tp 'wsjtx/bin/jt9.exe')) -or -not (Test-Path (Join-Path $tp 'hamlib/bin/rigctld.exe'))) {
    & (Join-Path $tp 'fetch.ps1')
}

# The app, self-contained so no .NET install is needed.
dotnet publish src/Ft8Client.App -c Release -r win-x64 --self-contained -o $stage "-p:Version=$version"
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
Get-ChildItem $stage -Filter '*.xml' | Remove-Item
Copy-Item (Join-Path $root 'LICENSE') $stage

# jt9 and Hamlib in the layout the app searches (third_party/... beside the exe), with their licences.
$stp = Join-Path $stage 'third_party'
Copy-Item -Recurse (Join-Path $tp 'wsjtx/bin') (Join-Path $stp 'wsjtx/bin')
Copy-Item -Recurse (Join-Path $tp 'hamlib/bin') (Join-Path $stp 'hamlib/bin')
foreach ($name in 'wsjtx', 'hamlib') {
    $dest = Join-Path $stp "licences/$name"
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Get-ChildItem (Join-Path $tp $name) -Recurse -File -Include 'COPYING*', 'LICENSE*', 'LICENCE*' |
        ForEach-Object { Copy-Item $_.FullName (Join-Path $dest $_.Name) -Force }
}
@"
FT8 Client bundles these programs, unmodified, as separate executables:

jt9 (decoder) from WSJT-X - GNU GPL version 3. Source: https://sourceforge.net/projects/wsjt/files/
rigctld (radio control) from Hamlib - GNU LGPL 2.1 / GPL 2. Source: https://github.com/Hamlib/Hamlib/releases
cty.dat country data (inside the app) from https://www.country-files.com/

Their licence files, where the distributions include them, are in the licences folder.
"@ | Set-Content -Encoding utf8 (Join-Path $stp 'THIRD-PARTY.txt')

# Sample recordings for simulation mode (the default until a radio is set up).
$ss = Join-Path $stage 'samples'
New-Item -ItemType Directory -Force -Path (Join-Path $ss 'ft8'), (Join-Path $ss 'ft4') | Out-Null
Copy-Item (Join-Path $root 'samples/ft8/*.wav') (Join-Path $ss 'ft8')
Copy-Item (Join-Path $root 'samples/ft4/*.wav') (Join-Path $ss 'ft4')
Copy-Item (Join-Path $root 'samples/partners.txt'), (Join-Path $root 'samples/README.md') $ss

# Smoke tests against the staged copies: Hamlib answers, and jt9 decodes a busy sample through the app's decoder host.
$freq = & (Join-Path $stp 'hamlib/bin/rigctl.exe') -m 1 f
if ($LASTEXITCODE -ne 0 -or -not ($freq -match '^\d+$')) { throw "Staged rigctl did not answer: $freq" }
$jt9 = Join-Path $stp 'wsjtx/bin/jt9.exe'
$decodes = dotnet run --project tools/Ft8Client.DecodeCli -c Release -- --jt9 $jt9 samples/ft8/181201_180245.wav
if ($LASTEXITCODE -ne 0) { throw "DecodeCli failed with the staged jt9: $decodes" }
$count = @($decodes | Where-Object { $_ -match '~' }).Count
Write-Host "Staged jt9 decoded $count messages from samples/ft8/181201_180245.wav"
if ($count -lt 15) { throw "Staged jt9 decoded only $count messages; expected about 22." }

# Inno Setup.
$iscc = (Get-Command iscc.exe -ErrorAction SilentlyContinue).Source
if (-not $iscc) { $iscc = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe' }
if (-not (Test-Path $iscc)) {
    choco install innosetup -y --no-progress | Out-Host
    $iscc = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'
}
& $iscc "/DAppVersion=$version" "/DStageDir=$stage" "/DOutputDir=$art" (Join-Path $root 'installer/Ft8Client.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup failed' }
Get-ChildItem $art -Filter 'Ft8Client-Setup-*.exe' | ForEach-Object { Write-Host "Installer: $($_.FullName) ($([math]::Round($_.Length / 1MB)) MB)" }
