# Costas - a station-centric FT8/FT4 client.
# Copyright (C) 2026 Costas contributors. GPLv3; see LICENSE.
#
# Builds the Windows installer: publishes the app self-contained for win-x64, stages jt9 (WSJT-X) and rigctld (Hamlib)
# with their licences, smoke-tests both from the staged folder, then compiles installer/Costas.iss with Inno Setup.
# Then packs the same staged folder with Velopack (delta updates). Set VELOPACK_PREVIOUS=1 to download the latest
# published release first, so the pack includes a delta from it (release runs).
# Output: artifacts/Costas-Setup-<version>.exe and artifacts/velopack/ (Velopack installer, packages, feed)
param([string]$BuildNumber = '')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root

$version = (Select-Xml -Path (Join-Path $root 'Directory.Build.props') -XPath '//Version').Node.InnerText
# Release builds are <major>.<minor>.<CI run number>: three parts, as Velopack requires (SemVer). Builds before the move
# to Velopack were 0.1.0.<run>; 0.1.<run> still compares newer for them.
if ($BuildNumber) { $p = $version.Split('.'); $version = "$($p[0]).$($p[1]).$BuildNumber" }
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
if (-not (Test-Path (Join-Path $stage 'Costas.exe'))) { throw 'Costas.exe missing from the publish output' }

# jt9 and Hamlib in the layout the app searches (third_party/... beside the exe), with their licences.
$stp = Join-Path $stage 'third_party'
# Only jt9 and the DLLs it loads, not all of WSJT-X's bin folder (about 60 MB instead of 300 MB), plus
# wsjtx_app_version.exe, which Costas asks for jt9's version before it keeps jt9 running (shared memory). The list is
# the import closure of those two in WSJT-X 3.0.1; a DLL missing after a WSJT-X upgrade makes the staged checks fail.
$jt9Files = 'jt9.exe', 'wsjtx_app_version.exe', 'libdouble-conversion.dll', 'libfftw3f-3.dll', 'libfftw3f_threads-3.dll', 'libgcc_s_seh-1.dll',
    'libgfortran-5.dll', 'libgomp-1.dll', 'libicudt78.dll', 'libicuin78.dll', 'libicuuc78.dll', 'libpcre2-16-0.dll',
    'libquadmath-0.dll', 'libstdc++-6.dll', 'libwinpthread-1.dll', 'libzstd.dll', 'Qt5Core.dll', 'zlib1.dll'
New-Item -ItemType Directory -Force -Path (Join-Path $stp 'wsjtx/bin') | Out-Null
foreach ($f in $jt9Files) { Copy-Item (Join-Path $tp "wsjtx/bin/$f") (Join-Path $stp "wsjtx/bin/$f") }
Copy-Item -Recurse (Join-Path $tp 'hamlib/bin') (Join-Path $stp 'hamlib/bin')
foreach ($name in 'wsjtx', 'hamlib') {
    $dest = Join-Path $stp "licences/$name"
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Get-ChildItem (Join-Path $tp $name) -Recurse -File -Include 'COPYING*', 'LICENSE*', 'LICENCE*' |
        ForEach-Object { Copy-Item $_.FullName (Join-Path $dest $_.Name) -Force }
}
@"
Costas bundles these programs, unmodified, as separate executables:

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
& $iscc "/DAppVersion=$version" "/DStageDir=$stage" "/DOutputDir=$art" (Join-Path $root 'installer/Costas.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup failed' }
Get-ChildItem $art -Filter 'Costas-Setup-*.exe' | ForEach-Object { Write-Host "Installer: $($_.FullName) ($([math]::Round($_.Length / 1MB)) MB)" }

# Velopack: the installer for new installs and for classic installs moving over (CostasApp-win-Setup.exe), the full
# package and, when the previous release's package was downloaded, the delta from it. The pack id is CostasApp, not
# Costas: Velopack installs to %LocalAppData%\<id>, and %LocalAppData%\Costas is the data folder.
$vp = Join-Path $art 'velopack'
New-Item -ItemType Directory -Force -Path $vp | Out-Null
dotnet tool restore
if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed' }
if ($env:VELOPACK_PREVIOUS -eq '1') {
    dotnet vpk download github --repoUrl 'https://github.com/somedudenamedtony/costas' --outputDir $vp
    # The first Velopack release has nothing to download; it ships without a delta.
    if ($LASTEXITCODE -ne 0) { Write-Warning 'No earlier Velopack release to make a delta from' }
}
dotnet vpk pack --packId CostasApp --packVersion $version --packDir $stage --mainExe Costas.exe --packTitle Costas `
    --packAuthors 'Costas contributors' --icon (Join-Path $root 'src/Ft8Client.App/Assets/costas.ico') `
    --runtime win-x64 --shortcuts StartMenuRoot --noPortable --outputDir $vp
if ($LASTEXITCODE -ne 0) { throw 'Velopack pack failed' }
# Only this version's packages are published; an earlier one downloaded for the delta is already on its own release.
Get-ChildItem $vp -Filter '*.nupkg' | Where-Object { $_.Name -notlike "*-$version-*" } | Remove-Item
Get-ChildItem $vp | ForEach-Object { Write-Host "Velopack: $($_.Name) ($([math]::Round($_.Length / 1MB, 1)) MB)" }
