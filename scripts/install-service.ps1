param(
    [Parameter(Mandatory = $true)]
    [string] $PublishDirectory
)

$ErrorActionPreference = 'Stop'

$serviceName = 'MwsManifestadorAgent'
$displayName = 'MWS Manifestador NF-e Agent'
$exePath = Join-Path -Path $PublishDirectory -ChildPath 'Mws.Manifestador.Agent.Worker.exe'

if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Worker executable not found at $exePath"
}

if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
    Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $serviceName | Out-Null
}

New-Service -Name $serviceName -BinaryPathName "`"$exePath`"" -DisplayName $displayName -StartupType Automatic
Start-Service -Name $serviceName
