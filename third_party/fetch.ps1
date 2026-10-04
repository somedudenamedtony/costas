# Fetches jt9 (WSJT-X) and rigctld (Hamlib) for Windows into third_party/.
# Requires 7-Zip (7z.exe) on PATH to unpack the WSJT-X NSIS installer.
param(
    [string]$WsjtxVersion = '3.0.2',
    [string]$HamlibVersion = '4.6.5'
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$tmp = Join-Path $root '.download'
New-Item -ItemType Directory -Force -Path $tmp | Out-Null

$wsjtxExe = Join-Path $tmp "wsjtx-$WsjtxVersion-win64.exe"
if (-not (Test-Path $wsjtxExe)) {
    $url = "https://sourceforge.net/projects/wsjt/files/wsjtx-$WsjtxVersion/wsjtx-$WsjtxVersion-win64.exe/download"
    Write-Host "Downloading $url"
    Invoke-WebRequest -Uri $url -OutFile $wsjtxExe -UserAgent 'Wget'
}
$wsjtxDir = Join-Path $root 'wsjtx'
Remove-Item -Recurse -Force $wsjtxDir -ErrorAction SilentlyContinue
& 7z x $wsjtxExe "-o$wsjtxDir" -y | Out-Null
if ($LASTEXITCODE -ne 0) { throw '7z failed to unpack the WSJT-X installer' }
if (-not (Test-Path (Join-Path $wsjtxDir 'bin/jt9.exe'))) { throw 'jt9.exe not found after unpacking' }

$hamZip = Join-Path $tmp "hamlib-w64-$HamlibVersion.zip"
if (-not (Test-Path $hamZip)) {
    $url = "https://github.com/Hamlib/Hamlib/releases/download/$HamlibVersion/hamlib-w64-$HamlibVersion.zip"
    Write-Host "Downloading $url"
    Invoke-WebRequest -Uri $url -OutFile $hamZip
}
$hamDir = Join-Path $root 'hamlib'
Remove-Item -Recurse -Force $hamDir -ErrorAction SilentlyContinue
Expand-Archive -Path $hamZip -DestinationPath $tmp -Force
Move-Item (Join-Path $tmp "hamlib-w64-$HamlibVersion") $hamDir

Invoke-WebRequest -Uri 'https://www.country-files.com/cty/cty.dat' -OutFile (Join-Path $root 'cty.dat')
Write-Host "Done. jt9: $wsjtxDir\bin\jt9.exe  rigctld: $hamDir\bin\rigctld.exe"
