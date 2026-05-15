param(
    [string] $ServiceName = 'MwsManifestadorAgent',
    [switch] $RemoveCredentials
)

$ErrorActionPreference = 'Stop'

Write-Host 'MWS Agent technical support uninstaller. Run from an elevated PowerShell session.'

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Run this script from an elevated PowerShell session.'
    }
}

Assert-Administrator

if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
}

if ($RemoveCredentials) {
    $credentialPath = Join-Path -Path $env:ProgramData -ChildPath 'MWS Manifestador Agent\agent-credentials.dpapi'
    if (Test-Path -LiteralPath $credentialPath) {
        Remove-Item -LiteralPath $credentialPath -Force
    }
}
