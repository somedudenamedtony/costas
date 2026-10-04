# CI entry point for Windows: build (warnings are errors) and tests.
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
dotnet build -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet test -c Release --no-build
exit $LASTEXITCODE
