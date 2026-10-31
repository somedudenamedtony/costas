# Costas - a station-centric FT8/FT4 client.
# Copyright (C) 2026 Costas contributors. GPLv3; see LICENSE.
#
# Code-signs Windows binaries with Microsoft's Artifact Signing (formerly Trusted Signing), so SmartScreen shows the
# publisher instead of "Unknown publisher". build-installer.ps1 calls it for the staged app, and Inno Setup calls it
# for the installer and uninstaller. Files that already carry a valid signature (the .NET runtime, Microsoft's DLLs)
# are left alone.
#
# Needs the ArtifactSigning PowerShell module and these environment variables (CI sets them on release runs only):
#   ARTIFACT_SIGNING_ENDPOINT   e.g. https://wus2.codesigning.azure.net/
#   ARTIFACT_SIGNING_ACCOUNT    the Artifact Signing account name
#   ARTIFACT_SIGNING_PROFILE    the certificate profile name
#   AZURE_TENANT_ID, AZURE_CLIENT_ID, AZURE_CLIENT_SECRET   an app registration with the
#                               "Artifact Signing Certificate Profile Signer" role (read by DefaultAzureCredential)
param([Parameter(Mandatory, ValueFromRemainingArguments)][string[]]$Path)
$ErrorActionPreference = 'Stop'

foreach ($name in 'ARTIFACT_SIGNING_ENDPOINT', 'ARTIFACT_SIGNING_ACCOUNT', 'ARTIFACT_SIGNING_PROFILE') {
    if (-not [Environment]::GetEnvironmentVariable($name)) { throw "$name is not set; signing is not configured" }
}

$files = @($Path | ForEach-Object { (Resolve-Path $_).Path } |
    Where-Object { (Get-AuthenticodeSignature $_).Status -ne 'Valid' })
if ($files.Count -eq 0) { Write-Host 'Nothing to sign: every file is already signed'; return }
Write-Host "Signing $($files.Count) file(s)"

Import-Module ArtifactSigning
# The module takes one comma-separated string of paths and signs them in batches.
$params = @{
    Endpoint = $env:ARTIFACT_SIGNING_ENDPOINT
    CodeSigningAccountName = $env:ARTIFACT_SIGNING_ACCOUNT
    CertificateProfileName = $env:ARTIFACT_SIGNING_PROFILE
    Files = $files -join ','
    FileDigest = 'SHA256'
    TimestampRfc3161 = 'http://timestamp.acs.microsoft.com'
    TimestampDigest = 'SHA256'
    Description = 'Costas'
    DescriptionUrl = 'https://github.com/somedudenamedtony/costas'
}
Invoke-ArtifactSigning @params

$bad = @($files | Where-Object { (Get-AuthenticodeSignature $_).Status -ne 'Valid' })
if ($bad.Count -gt 0) { throw "Not signed after signing: $($bad -join ', ')" }
