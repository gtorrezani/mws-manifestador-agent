param(
    [Parameter(Mandatory = $true)]
    [string] $PackageDirectory,
    [string] $ServiceName = 'MwsManifestadorAgent',
    [string] $InstallDirectory = ''
)

$ErrorActionPreference = 'Stop'

Write-Host 'MWS Agent technical support updater. Use signed MSI/EXE update flow for end users.'

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Run this script from an elevated PowerShell session.'
    }
}

Assert-Administrator

if ([string]::IsNullOrWhiteSpace($InstallDirectory)) {
    $InstallDirectory = Join-Path -Path $env:ProgramFiles -ChildPath 'MWS Manifestador Agent'
}

$exePath = Join-Path -Path $PackageDirectory -ChildPath 'Mws.Manifestador.Agent.Worker.exe'
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Worker executable not found at $exePath"
}

if (-not (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue)) {
    throw "Service $ServiceName is not installed."
}

Stop-Service -Name $ServiceName -Force
New-Item -ItemType Directory -Path $InstallDirectory -Force | Out-Null
Copy-Item -Path (Join-Path -Path $PackageDirectory -ChildPath '*') -Destination $InstallDirectory -Recurse -Force
Start-Service -Name $ServiceName
