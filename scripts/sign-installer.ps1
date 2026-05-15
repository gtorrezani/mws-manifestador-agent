param(
    [string] $InstallerPath = (Join-Path $PSScriptRoot '..\artifacts\installer\MWS-Manifestador-Agent-Setup.msi'),
    [string] $CertificateThumbprint,
    [string] $TimestampServer = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    throw 'CertificateThumbprint is required. Production releases must use a valid code-signing certificate.'
}

$resolvedInstaller = Resolve-Path $InstallerPath
$signtool = Get-Command signtool.exe -ErrorAction SilentlyContinue
if ($null -eq $signtool) {
    throw 'signtool.exe was not found. Install the Windows SDK before signing the installer.'
}

& $signtool.Source sign /fd SHA256 /tr $TimestampServer /td SHA256 /sha1 $CertificateThumbprint $resolvedInstaller
