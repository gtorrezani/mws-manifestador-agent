param(
    [string] $BaseUrl = 'http://127.0.0.1:8020',
    [string] $ActivationCode = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$env:AgentApi__BaseUrl = $BaseUrl
$env:LocalDiagnostics__Enabled = 'true'
$env:LocalDiagnostics__ListenUrl = 'http://127.0.0.1:8022'
$env:AgentPolling__IntervalSeconds = '10'
$env:AgentPolling__HeartbeatIntervalSeconds = '10'

if (-not [string]::IsNullOrWhiteSpace($ActivationCode)) {
    $env:AgentApi__ActivationCode = $ActivationCode
}

dotnet run --project src\Mws.Manifestador.Agent.Worker\Mws.Manifestador.Agent.Worker.csproj --environment Development
